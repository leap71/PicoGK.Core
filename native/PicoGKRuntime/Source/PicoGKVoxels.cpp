// SPDX-License-Identifier: Apache-2.0
#include "PicoGKVoxels.h"
#include "PicoGKMesh.h"

#include <openvdb/openvdb.h>
#include <openvdb/math/Stencils.h>
#include <openvdb/tools/Composite.h>
#include <openvdb/tools/Diagnostics.h>
#include <openvdb/tools/Interpolation.h>
#include <openvdb/tools/LevelSetDilatedMesh.h>
#include <openvdb/tools/LevelSetFilter.h>
#include <openvdb/tools/LevelSetMeasure.h>
#include <openvdb/tools/LevelSetSphere.h>
#include <openvdb/tools/LevelSetTubes.h>
#include <openvdb/tools/MeshToVolume.h>
#include <openvdb/tools/ParticlesToLevelSet.h>
#include <openvdb/tools/Prune.h>
#include <openvdb/tools/RayIntersector.h>
#include <openvdb/tools/VolumeToMesh.h>
#include <openvdb/tools/VolumeToSpheres.h>

#include <algorithm>
#include <cmath>
#include <limits>
#include <mutex>
#include <stdexcept>
#include <vector>

namespace PicoGK
{
class Voxels::Impl
{
public:
    using FloatGrid = openvdb::FloatGrid;
    using ClosestSurfacePoint = openvdb::tools::ClosestSurfacePoint<FloatGrid>;

    struct SdfImportState
    {
        PKSdfVolumeDesc oVolume{};
        FloatGrid::Ptr roGrid;
        uint32_t nNextZSlice = 0;
        int32_t nCurrentLeafZOrigin = 0;
        std::vector<int16_t> aSlab;
    };

    FloatGrid::Ptr roGrid;

    // TODO: ClosestSurfacePoint owns additional acceleration data that is not
    // currently included in Voxels::nMemUsage(). Account for this cache so the
    // managed Library GC memory-pressure tracking reflects the full native cost.
    mutable ClosestSurfacePoint::Ptr roClosestSurface;
    mutable bool bClosestSurfaceCacheInitialized = false;
    mutable std::mutex oClosestSurfaceMutex;

    std::unique_ptr<SdfImportState> roSdfImport;

    void InvalidateCaches()
    {
        roClosestSurface.reset();
        bClosestSurfaceCacheInitialized = false;
    }
};

namespace
{
    using FloatGrid = openvdb::FloatGrid;
    using FloatTree = FloatGrid::TreeType;
    using FloatLeaf = FloatTree::LeafNodeType;

    // PicoGK Voxels are always represented by the canonical three-voxel SDF
    // half-band used by PKSdfSlice.
    constexpr float c_fSdfHalfWidthVoxels =
        static_cast<float>(PKSDF_HALF_WIDTH_VOXELS);

    constexpr int32_t c_nSdfLeafDim = static_cast<int32_t>(FloatLeaf::DIM);

    void ValidateVoxelSize(float fVoxelSizeMM)
    {
        if (!std::isfinite(fVoxelSizeMM) || fVoxelSizeMM <= 0.0f)
            throw std::invalid_argument("Voxel size must be finite and greater than zero");
    }

    void ValidateRadius(float fRadiusMM)
    {
        if (!std::isfinite(fRadiusMM) || fRadiusMM <= 0.0f)
            throw std::invalid_argument("Radius must be finite and greater than zero");
    }

    void ValidatePoint(const PKVector3& vecPoint)
    {
        if (!std::isfinite(vecPoint.X) ||
            !std::isfinite(vecPoint.Y) ||
            !std::isfinite(vecPoint.Z))
        {
            throw std::invalid_argument("Geometry point must be finite");
        }
    }

    int32_t nAlignDown(int32_t nValue, int32_t nAlignment)
    {
        const int32_t nRemainder = nValue % nAlignment;
        if (nRemainder >= 0) return nValue - nRemainder;
        return nValue - nRemainder - nAlignment;
    }

    size_t nCheckedProduct(uint32_t nA, uint32_t nB, uint32_t nC = 1)
    {
        const size_t nMax = std::numeric_limits<size_t>::max();
        if (nA != 0 && static_cast<size_t>(nB) > nMax / static_cast<size_t>(nA))
            throw std::length_error("Requested SDF buffer is too large");

        const size_t nAB = static_cast<size_t>(nA) * static_cast<size_t>(nB);
        if (nAB != 0 && static_cast<size_t>(nC) > nMax / nAB)
            throw std::length_error("Requested SDF buffer is too large");

        return nAB * static_cast<size_t>(nC);
    }

    int16_t nEncodeSdf(float fValueMM,
                       float fBackgroundMM,
                       float fSdfToIntScale)
    {
        if (fValueMM <= -fBackgroundMM) return PKSDF_INSIDE_BACKGROUND;
        if (fValueMM >=  fBackgroundMM) return PKSDF_OUTSIDE_BACKGROUND;

        const float fScaled = fValueMM * fSdfToIntScale;
        return static_cast<int16_t>(
            fScaled + (fScaled >= 0.0f ? 0.5f : -0.5f));
    }

    float fDecodeSdf(int16_t nValue, float fIntToSdfScale)
    {
        if (nValue == PKSDF_RESERVED)
            throw std::invalid_argument("Reserved SDF sample value encountered");

        return static_cast<float>(nValue) * fIntToSdfScale;
    }

    bool bHasValidPicoGKTransform(const FloatGrid::Ptr& roGrid)
    {
        if (!roGrid || !roGrid->transform().isLinear())
            return false;

        const openvdb::Vec3d vecVoxel = roGrid->voxelSize();
        return vecVoxel.x() == vecVoxel.y() && vecVoxel.x() == vecVoxel.z();
    }

    class SphereList
    {
    public:
        using PosType = openvdb::Vec3R;

        SphereList(const PKVector3* pCenters,
                   const float* pRadiiMM,
                   uint32_t nCount)
            : m_pCenters(pCenters), m_pRadiiMM(pRadiiMM), m_nCount(nCount)
        {
        }

        size_t size() const
        {
            return static_cast<size_t>(m_nCount);
        }

        void getPos(size_t n, openvdb::Vec3R& vecPosition) const
        {
            const PKVector3& vec = m_pCenters[n];
            vecPosition = openvdb::Vec3R(vec.X, vec.Y, vec.Z);
        }

        void getPosRad(size_t n,
                       openvdb::Vec3R& vecPosition,
                       openvdb::Real& fRadiusMM) const
        {
            getPos(n, vecPosition);
            fRadiusMM = static_cast<openvdb::Real>(m_pRadiiMM[n]);
        }

    private:
        const PKVector3* m_pCenters;
        const float* m_pRadiiMM;
        uint32_t m_nCount;
    };

    void ValidatePoints(const PKVector3* pPoints, uint32_t nCount)
    {
        if (nCount > 0 && !pPoints)
            throw std::invalid_argument("Geometry point buffer is null");

        for (uint32_t n = 0; n < nCount; ++n)
            ValidatePoint(pPoints[n]);
    }

    void ValidateRadii(const float* pRadiiMM, uint32_t nCount)
    {
        if (nCount > 0 && !pRadiiMM)
            throw std::invalid_argument("Radius buffer is null");

        for (uint32_t n = 0; n < nCount; ++n)
            ValidateRadius(pRadiiMM[n]);
    }

    std::vector<openvdb::Vec3f> aOpenVdbVertices(const PKVector3* pVertices,
                                                  uint32_t nVertexCount)
    {
        ValidatePoints(pVertices, nVertexCount);

        std::vector<openvdb::Vec3f> aVertices;
        aVertices.reserve(nVertexCount);
        for (uint32_t n = 0; n < nVertexCount; ++n)
        {
            const PKVector3& vec = pVertices[n];
            aVertices.emplace_back(vec.X, vec.Y, vec.Z);
        }
        return aVertices;
    }

    std::vector<openvdb::Vec2I> aOpenVdbSegments(const PKSegment* pSegments,
                                                  uint32_t nSegmentCount,
                                                  uint32_t nVertexCount)
    {
        if (nSegmentCount > 0 && !pSegments)
            throw std::invalid_argument("Segment buffer is null");

        std::vector<openvdb::Vec2I> aSegments;
        aSegments.reserve(nSegmentCount);

        for (uint32_t n = 0; n < nSegmentCount; ++n)
        {
            const PKSegment& seg = pSegments[n];
            if (seg.A >= nVertexCount || seg.B >= nVertexCount)
                throw std::out_of_range("Tube segment vertex index is out of range");
            if (seg.A > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()) ||
                seg.B > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
            {
                throw std::length_error("Tube segment index exceeds OpenVDB index range");
            }

            aSegments.emplace_back(static_cast<int32_t>(seg.A),
                                   static_cast<int32_t>(seg.B));
        }
        return aSegments;
    }

    void BuildOpenVdbMesh(const Mesh& oMesh,
                          std::vector<openvdb::Vec3s>& aVertices,
                          std::vector<openvdb::Vec3I>& aTriangles,
                          std::vector<openvdb::Vec4I>& aQuads)
    {
        aVertices.reserve(oMesh.aVertices().size());
        for (const PKVector3& vec : oMesh.aVertices())
            aVertices.emplace_back(vec.X, vec.Y, vec.Z);

        aTriangles.reserve(oMesh.aTriangles().size());
        for (const PKTriangle& tri : oMesh.aTriangles())
            aTriangles.emplace_back(tri.A, tri.B, tri.C);

        aQuads.reserve(oMesh.aQuads().size());
        for (const PKQuad& quad : oMesh.aQuads())
            aQuads.emplace_back(quad.A, quad.B, quad.C, quad.D);
    }

    FloatGrid::Ptr roGridFromMesh(const Mesh& oMesh,
                                  float fVoxelSizeMM)
    {
        std::vector<openvdb::Vec3s> aVertices;
        std::vector<openvdb::Vec3I> aTriangles;
        std::vector<openvdb::Vec4I> aQuads;
        BuildOpenVdbMesh(oMesh, aVertices, aTriangles, aQuads);

        auto roTransform = openvdb::math::Transform::createLinearTransform(fVoxelSizeMM);
        return openvdb::tools::meshToLevelSet<FloatGrid>(*roTransform,
                                                          aVertices,
                                                          aTriangles,
                                                          aQuads,
                                                          c_fSdfHalfWidthVoxels);
    }

    FloatGrid::Ptr roDilatedGridFromMesh(const Mesh& oMesh,
                                         float fVoxelSizeMM,
                                         float fRadius)
    {
        std::vector<openvdb::Vec3s> aVertices;
        std::vector<openvdb::Vec3I> aTriangles;
        std::vector<openvdb::Vec4I> aQuads;
        BuildOpenVdbMesh(oMesh, aVertices, aTriangles, aQuads);

        return openvdb::tools::createLevelSetDilatedMesh<FloatGrid>(aVertices,
                                                                     aTriangles,
                                                                     aQuads,
                                                                     fRadius,
                                                                     fVoxelSizeMM,
                                                                     c_fSdfHalfWidthVoxels);
    }
}

Voxels::Voxels(float fVoxelSizeMM)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);

    m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);
    ValidateGrid();
}

Voxels::Voxels(const Voxels& oSource)
    : m_roImpl(std::make_unique<Impl>())
{
    oSource.EnsureReady();
    m_roImpl->roGrid = openvdb::deepCopyTypedGrid<FloatGrid>(oSource.m_roImpl->roGrid);
    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3& vecCenter,
               float fRadius)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidatePoint(vecCenter);
    ValidateRadius(fRadius);

    m_roImpl->roGrid = openvdb::tools::createLevelSetSphere<FloatGrid>(
        fRadius,
        openvdb::Vec3f(vecCenter.X, vecCenter.Y, vecCenter.Z),
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);
    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3& vecStart,
               const PKVector3& vecEnd,
               float fRadiusStart,
               float fRadiusEnd)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidatePoint(vecStart);
    ValidatePoint(vecEnd);
    ValidateRadius(fRadiusStart);
    ValidateRadius(fRadiusEnd);

    m_roImpl->roGrid = openvdb::tools::createLevelSetTaperedCapsule<FloatGrid>(
        openvdb::Vec3f(vecStart.X, vecStart.Y, vecStart.Z),
        openvdb::Vec3f(vecEnd.X, vecEnd.Y, vecEnd.Z),
        fRadiusStart,
        fRadiusEnd,
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);
    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3* pCenters,
               uint32_t nCenterCount,
               float fRadiusMM)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidateRadius(fRadiusMM);
    ValidatePoints(pCenters, nCenterCount);

    m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);

    if (nCenterCount > 0)
    {
        const SphereList oSpheres(pCenters, nullptr, nCenterCount);
        openvdb::tools::ParticlesToLevelSet<FloatGrid> oRasterizer(*m_roImpl->roGrid);
        oRasterizer.setRmin(0.0);
        oRasterizer.setRmax(std::max(
            oRasterizer.getRmax(),
            static_cast<openvdb::Real>(fRadiusMM / fVoxelSizeMM) + 1.0));
        oRasterizer.rasterizeSpheres(oSpheres, fRadiusMM);
        oRasterizer.finalize(true);
    }

    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3* pCenters,
               const float* pRadiiMM,
               uint32_t nCenterCount)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidatePoints(pCenters, nCenterCount);
    ValidateRadii(pRadiiMM, nCenterCount);

    m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);

    if (nCenterCount > 0)
    {
        float fMaxRadiusMM = pRadiiMM[0];
        bool bConstantRadius = true;
        for (uint32_t n = 1; n < nCenterCount; ++n)
        {
            fMaxRadiusMM = std::max(fMaxRadiusMM, pRadiiMM[n]);
            bConstantRadius = bConstantRadius && pRadiiMM[n] == pRadiiMM[0];
        }

        const SphereList oSpheres(pCenters, pRadiiMM, nCenterCount);
        openvdb::tools::ParticlesToLevelSet<FloatGrid> oRasterizer(*m_roImpl->roGrid);
        oRasterizer.setRmin(0.0);
        oRasterizer.setRmax(std::max(
            oRasterizer.getRmax(),
            static_cast<openvdb::Real>(fMaxRadiusMM / fVoxelSizeMM) + 1.0));

        if (bConstantRadius)
            oRasterizer.rasterizeSpheres(oSpheres, pRadiiMM[0]);
        else
            oRasterizer.rasterizeSpheres(oSpheres);

        oRasterizer.finalize(true);
    }

    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3* pVertices,
               uint32_t nVertexCount,
               const PKSegment* pSegments,
               uint32_t nSegmentCount,
               float fRadiusMM)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidateRadius(fRadiusMM);

    if (nVertexCount > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
        throw std::length_error("Tube vertex count exceeds OpenVDB index range");

    auto aVertices = aOpenVdbVertices(pVertices, nVertexCount);
    auto aSegments = aOpenVdbSegments(pSegments, nSegmentCount, nVertexCount);

    if (aSegments.empty())
    {
        m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
            fVoxelSizeMM,
            c_fSdfHalfWidthVoxels);
    }
    else
    {
        m_roImpl->roGrid = openvdb::tools::createLevelSetTubeComplex<FloatGrid>(
            aVertices,
            aSegments,
            fRadiusMM,
            fVoxelSizeMM,
            c_fSdfHalfWidthVoxels);
    }

    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKVector3* pVertices,
               const float* pVertexRadiiMM,
               uint32_t nVertexCount,
               const PKSegment* pSegments,
               uint32_t nSegmentCount)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    ValidatePoints(pVertices, nVertexCount);
    ValidateRadii(pVertexRadiiMM, nVertexCount);

    if (nVertexCount > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
        throw std::length_error("Tube vertex count exceeds OpenVDB index range");

    auto aVertices = aOpenVdbVertices(pVertices, nVertexCount);
    auto aSegments = aOpenVdbSegments(pSegments, nSegmentCount, nVertexCount);

    if (aSegments.empty())
    {
        m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
            fVoxelSizeMM,
            c_fSdfHalfWidthVoxels);
    }
    else
    {
        bool bConstantRadius = true;
        for (uint32_t n = 1; n < nVertexCount; ++n)
            bConstantRadius = bConstantRadius && pVertexRadiiMM[n] == pVertexRadiiMM[0];

        if (bConstantRadius)
        {
            m_roImpl->roGrid = openvdb::tools::createLevelSetTubeComplex<FloatGrid>(
                aVertices,
                aSegments,
                pVertexRadiiMM[0],
                fVoxelSizeMM,
                c_fSdfHalfWidthVoxels);
        }
        else
        {
            std::vector<float> aRadii(pVertexRadiiMM, pVertexRadiiMM + nVertexCount);
            m_roImpl->roGrid = openvdb::tools::createLevelSetTubeComplex<FloatGrid>(
                aVertices,
                aSegments,
                aRadii,
                fVoxelSizeMM,
                c_fSdfHalfWidthVoxels,
                openvdb::tools::TUBE_VERTEX_RADII);
        }
    }

    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const PKSdfVolumeDesc& oVolume)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);

    const bool bEmpty =
        oVolume.nXSize == 0 && oVolume.nYSize == 0 && oVolume.nZSize == 0;
    const bool bPartiallyEmpty =
        (oVolume.nXSize == 0 || oVolume.nYSize == 0 || oVolume.nZSize == 0) && !bEmpty;
    if (bPartiallyEmpty)
        throw std::invalid_argument("SDF volume dimensions must either all be zero or all be non-zero");

    auto ValidateExtent = [](int32_t nOrigin, uint32_t nSize)
    {
        if (nSize == 0) return;
        const int64_t nMax = static_cast<int64_t>(nOrigin) +
                             static_cast<int64_t>(nSize) - 1;
        if (nMax > std::numeric_limits<int32_t>::max())
            throw std::out_of_range("SDF volume extent exceeds 32-bit index space");
    };
    ValidateExtent(oVolume.nXOrigin, oVolume.nXSize);
    ValidateExtent(oVolume.nYOrigin, oVolume.nYSize);
    ValidateExtent(oVolume.nZOrigin, oVolume.nZSize);

    m_roImpl->roGrid = openvdb::createLevelSet<FloatGrid>(
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);

    auto roImport = std::make_unique<Impl::SdfImportState>();
    roImport->oVolume = oVolume;
    roImport->roGrid = openvdb::createLevelSet<FloatGrid>(
        fVoxelSizeMM,
        c_fSdfHalfWidthVoxels);
    roImport->nCurrentLeafZOrigin = nAlignDown(oVolume.nZOrigin, c_nSdfLeafDim);

    if (!bEmpty)
    {
        roImport->aSlab.assign(
            nCheckedProduct(oVolume.nXSize,
                            oVolume.nYSize,
                            static_cast<uint32_t>(c_nSdfLeafDim)),
            static_cast<int16_t>(PKSDF_OUTSIDE_BACKGROUND));
    }

    m_roImpl->roSdfImport = std::move(roImport);
    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const Mesh& oMesh)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    m_roImpl->roGrid = roGridFromMesh(oMesh, fVoxelSizeMM);
    ValidateGrid();
}

Voxels::Voxels(float fVoxelSizeMM,
               const Mesh& oMesh,
               float fRadius)
    : m_roImpl(std::make_unique<Impl>())
{
    ValidateVoxelSize(fVoxelSizeMM);
    m_roImpl->roGrid = roDilatedGridFromMesh(oMesh, fVoxelSizeMM, fRadius);
    ValidateGrid();
}

Voxels::~Voxels() = default;

void Voxels::EnsureReady() const
{
    if (m_roImpl->roSdfImport)
        throw std::logic_error("Voxels object is still being reconstructed from SDF slices");
}

void Voxels::ValidateGrid() const
{
    if (!bHasValidPicoGKTransform(m_roImpl->roGrid))
        throw std::invalid_argument("Voxels grid must use a linear isotropic transform");

    const float fExpectedBackground = fVoxelSizeMM() * c_fSdfHalfWidthVoxels;
    const float fBackground = fBackgroundMM();
    const float fTolerance = std::max(1.0f, fExpectedBackground) * 1.0e-6f;
    if (std::abs(fBackground - fExpectedBackground) > fTolerance)
        throw std::invalid_argument("Voxels grid must use a three-voxel SDF half-band");

    m_roImpl->roGrid->setGridClass(openvdb::GRID_LEVEL_SET);
}

void Voxels::ValidateCompatible(const Voxels& oOther) const
{
    EnsureReady();
    oOther.EnsureReady();

    if (m_roImpl->roGrid->transform() != oOther.m_roImpl->roGrid->transform())
        throw std::invalid_argument("Voxel operations require matching voxel transforms");

    if (m_roImpl->roGrid->background() != oOther.m_roImpl->roGrid->background())
        throw std::invalid_argument("Voxel operations require matching SDF backgrounds");
}

int64_t Voxels::nMemUsage() const
{
    int64_t nBytes =
        static_cast<int64_t>(sizeof(Voxels) + sizeof(Impl)) +
        static_cast<int64_t>(m_roImpl->roGrid->memUsage());

    if (m_roImpl->roSdfImport)
    {
        nBytes += static_cast<int64_t>(sizeof(Impl::SdfImportState));
        nBytes += static_cast<int64_t>(m_roImpl->roSdfImport->roGrid->memUsage());
        nBytes += static_cast<int64_t>(
            m_roImpl->roSdfImport->aSlab.capacity() * sizeof(int16_t));
    }

    return nBytes;
}

std::string Voxels::strDiagnose() const
{
    EnsureReady();
    return openvdb::tools::checkLevelSet<FloatGrid>(*m_roImpl->roGrid);
}

bool Voxels::bIsEmpty() const
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty())
        return true;

    for (auto iter = m_roImpl->roGrid->cbeginValueOn(); iter.test(); ++iter)
        if (*iter < 0.0f)
            return false;

    return true;
}

float Voxels::fVoxelSizeMM() const
{
    return static_cast<float>(m_roImpl->roGrid->voxelSize().x());
}

float Voxels::fBackgroundMM() const
{
    return m_roImpl->roGrid->background();
}

// TODO: Revisit CSG implementation and benchmark OpenVDB's non-destructive
// csgUnionCopy / csgDifferenceCopy / csgIntersectionCopy paths. These may
// allow the C# + / - / & operators to avoid unnecessary deep copies and could
// potentially improve performance or peak memory usage more generally.
// Measure both execution time and peak native memory before changing the path.

void Voxels::BoolAdd(const Voxels& oOther)
{
    ValidateCompatible(oOther);
    auto roOperand = openvdb::deepCopyTypedGrid<FloatGrid>(oOther.m_roImpl->roGrid);

    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();
    openvdb::tools::csgUnion(*m_roImpl->roGrid, *roOperand);
}

void Voxels::BoolSubtract(const Voxels& oOther)
{
    ValidateCompatible(oOther);
    auto roOperand = openvdb::deepCopyTypedGrid<FloatGrid>(oOther.m_roImpl->roGrid);

    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();
    openvdb::tools::csgDifference(*m_roImpl->roGrid, *roOperand);
}

void Voxels::BoolIntersect(const Voxels& oOther)
{
    ValidateCompatible(oOther);
    auto roOperand = openvdb::deepCopyTypedGrid<FloatGrid>(oOther.m_roImpl->roGrid);

    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();
    openvdb::tools::csgIntersection(*m_roImpl->roGrid, *roOperand);
}

void Voxels::Offset(float fDistMM)
{
    EnsureReady();
    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();

    openvdb::tools::LevelSetFilter<FloatGrid> oFilter(*m_roImpl->roGrid);
    oFilter.offset(-fDistMM); // OpenVDB's sign convention is inward-positive.
}

void Voxels::DoubleOffset(float fDist1MM, float fDist2MM)
{
    EnsureReady();
    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();

    openvdb::tools::LevelSetFilter<FloatGrid> oFilter(*m_roImpl->roGrid);
    oFilter.offset(-fDist1MM);
    oFilter.offset(-fDist2MM);
}

void Voxels::TripleOffset(float fDistMM)
{
    EnsureReady();
    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
    m_roImpl->InvalidateCaches();

    openvdb::tools::LevelSetFilter<FloatGrid> oFilter(*m_roImpl->roGrid);
    oFilter.offset(-fDistMM);
    oFilter.offset(2.0f * fDistMM);
    oFilter.offset(-fDistMM);
}

void Voxels::FlushSdfImportSlab()
{
    if (!m_roImpl->roSdfImport)
        throw std::logic_error("No SDF import is active");

    Impl::SdfImportState& oImport = *m_roImpl->roSdfImport;
    const PKSdfVolumeDesc& oVolume = oImport.oVolume;
    if (oVolume.nXSize == 0) return;

    const int64_t nXMax64 = static_cast<int64_t>(oVolume.nXOrigin) + oVolume.nXSize - 1;
    const int64_t nYMax64 = static_cast<int64_t>(oVolume.nYOrigin) + oVolume.nYSize - 1;
    const int64_t nZMax64 = static_cast<int64_t>(oVolume.nZOrigin) + oVolume.nZSize - 1;

    const int32_t nXBlockMin = nAlignDown(oVolume.nXOrigin, c_nSdfLeafDim);
    const int32_t nYBlockMin = nAlignDown(oVolume.nYOrigin, c_nSdfLeafDim);
    const float fBackgroundMM = oImport.roGrid->background();
    const float fIntToSdfScale =
        fBackgroundMM / static_cast<float>(PKSDF_OUTSIDE_BACKGROUND);
    FloatTree& oTree = oImport.roGrid->tree();

    auto nSampleAt = [&](int32_t x, int32_t y, int32_t z) -> int16_t
    {
        if (x < oVolume.nXOrigin || static_cast<int64_t>(x) > nXMax64 ||
            y < oVolume.nYOrigin || static_cast<int64_t>(y) > nYMax64 ||
            z < oVolume.nZOrigin || static_cast<int64_t>(z) > nZMax64)
        {
            return static_cast<int16_t>(PKSDF_OUTSIDE_BACKGROUND);
        }

        const uint32_t nX = static_cast<uint32_t>(
            static_cast<int64_t>(x) - oVolume.nXOrigin);
        const uint32_t nY = static_cast<uint32_t>(
            static_cast<int64_t>(y) - oVolume.nYOrigin);
        const uint32_t nZ = static_cast<uint32_t>(
            static_cast<int64_t>(z) - oImport.nCurrentLeafZOrigin);

        const size_t nOffset =
            (static_cast<size_t>(nZ) * oVolume.nYSize + nY) * oVolume.nXSize + nX;
        return oImport.aSlab[nOffset];
    };

    for (int64_t nYBlock64 = nYBlockMin;
         nYBlock64 <= nYMax64;
         nYBlock64 += c_nSdfLeafDim)
    for (int64_t nXBlock64 = nXBlockMin;
         nXBlock64 <= nXMax64;
         nXBlock64 += c_nSdfLeafDim)
    {
        const int32_t nXBlock = static_cast<int32_t>(nXBlock64);
        const int32_t nYBlock = static_cast<int32_t>(nYBlock64);
        const int32_t nZBlock = oImport.nCurrentLeafZOrigin;

        bool bAllOutside = true;
        bool bAllInside = true;

        for (int32_t z = 0; z < c_nSdfLeafDim; ++z)
        for (int32_t y = 0; y < c_nSdfLeafDim; ++y)
        for (int32_t x = 0; x < c_nSdfLeafDim; ++x)
        {
            const int16_t nValue = nSampleAt(
                nXBlock + x,
                nYBlock + y,
                nZBlock + z);

            bAllOutside = bAllOutside && nValue == PKSDF_OUTSIDE_BACKGROUND;
            bAllInside  = bAllInside  && nValue == PKSDF_INSIDE_BACKGROUND;
        }

        const openvdb::Coord xyzOrigin(nXBlock, nYBlock, nZBlock);

        if (bAllOutside)
            continue;

        if (bAllInside)
        {
            // Level 1 is a leaf-sized tile in OpenVDB's standard FloatTree.
            oTree.addTile(1, xyzOrigin, -fBackgroundMM, false);
            continue;
        }

        auto roLeaf = std::make_unique<FloatLeaf>(xyzOrigin, fBackgroundMM, false);

        for (int32_t z = 0; z < c_nSdfLeafDim; ++z)
        for (int32_t y = 0; y < c_nSdfLeafDim; ++y)
        for (int32_t x = 0; x < c_nSdfLeafDim; ++x)
        {
            const openvdb::Coord xyz(nXBlock + x, nYBlock + y, nZBlock + z);
            const int16_t nValue = nSampleAt(xyz.x(), xyz.y(), xyz.z());

            if (nValue == PKSDF_OUTSIDE_BACKGROUND)
                continue;

            if (nValue == PKSDF_INSIDE_BACKGROUND)
            {
                roLeaf->setValueOff(xyz, -fBackgroundMM);
                continue;
            }

            roLeaf->setValueOn(xyz, fDecodeSdf(nValue, fIntToSdfScale));
        }

        oTree.addLeaf(roLeaf.release());
    }

    std::fill(oImport.aSlab.begin(),
              oImport.aSlab.end(),
              static_cast<int16_t>(PKSDF_OUTSIDE_BACKGROUND));
    if (oImport.nNextZSlice < oVolume.nZSize)
        oImport.nCurrentLeafZOrigin += c_nSdfLeafDim;
}

void Voxels::ImportSdfZSlice(uint32_t nZSlice, const PKSdfSlice& oSlice)
{
    if (!m_roImpl->roSdfImport)
        throw std::logic_error("No SDF import is active");

    Impl::SdfImportState& oImport = *m_roImpl->roSdfImport;
    const PKSdfVolumeDesc& oVolume = oImport.oVolume;

    if (nZSlice != oImport.nNextZSlice)
        throw std::invalid_argument("SDF Z slices must be imported in increasing order");
    if (nZSlice >= oVolume.nZSize)
        throw std::out_of_range("SDF Z slice index is out of range");
    if (oSlice.nWidth != oVolume.nXSize || oSlice.nHeight != oVolume.nYSize)
        throw std::invalid_argument("SDF Z slice dimensions do not match the import volume");

    const size_t nSliceValues = nCheckedProduct(oVolume.nXSize, oVolume.nYSize);
    if (oSlice.nValueCapacity < nSliceValues)
        throw std::length_error("SDF Z slice value buffer is too small");
    if (nSliceValues > 0 && !oSlice.pValues)
        throw std::invalid_argument("SDF Z slice value buffer is null");

    const int64_t nZ64 = static_cast<int64_t>(oVolume.nZOrigin) + nZSlice;
    const int32_t nZ = static_cast<int32_t>(nZ64);
    const int32_t nLocalZ = nZ - oImport.nCurrentLeafZOrigin;
    if (nLocalZ < 0 || nLocalZ >= c_nSdfLeafDim)
        throw std::logic_error("SDF import slab alignment is inconsistent");

    for (uint32_t nRow = 0; nRow < oVolume.nYSize; ++nRow)
    {
        const uint32_t nY = oVolume.nYSize - 1 - nRow;
        for (uint32_t nX = 0; nX < oVolume.nXSize; ++nX)
        {
            const int16_t nValue =
                oSlice.pValues[static_cast<size_t>(nRow) * oVolume.nXSize + nX];
            if (nValue == PKSDF_RESERVED)
                throw std::invalid_argument("Reserved SDF sample value encountered");

            const size_t nOffset =
                (static_cast<size_t>(nLocalZ) * oVolume.nYSize + nY) *
                oVolume.nXSize + nX;
            oImport.aSlab[nOffset] = nValue;
        }
    }

    ++oImport.nNextZSlice;

    if (nLocalZ == c_nSdfLeafDim - 1 || oImport.nNextZSlice == oVolume.nZSize)
        FlushSdfImportSlab();
}

void Voxels::EndSdfImport()
{
    if (!m_roImpl->roSdfImport)
        throw std::logic_error("No SDF import is active");

    Impl::SdfImportState& oImport = *m_roImpl->roSdfImport;
    if (oImport.nNextZSlice != oImport.oVolume.nZSize)
        throw std::logic_error("SDF import cannot finish before all Z slices are supplied");

    const float fBackgroundMM = oImport.roGrid->background();
    openvdb::tools::pruneLevelSet(
        oImport.roGrid->tree(),
        fBackgroundMM,
        -fBackgroundMM);
    oImport.roGrid->setGridClass(openvdb::GRID_LEVEL_SET);

    {
        std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);
        m_roImpl->InvalidateCaches();
        m_roImpl->roGrid = oImport.roGrid;
        m_roImpl->roSdfImport.reset();
    }

    ValidateGrid();
}

bool Voxels::bIsInside(const PKVector3& vecPoint) const
{
    EnsureReady();
    auto oAccessor = m_roImpl->roGrid->getConstAccessor();
    openvdb::tools::GridSampler<FloatGrid::ConstAccessor, openvdb::tools::BoxSampler> oSampler(
        oAccessor, m_roImpl->roGrid->transform());

    const float fSdf = oSampler.wsSample(
        openvdb::Vec3d(vecPoint.X, vecPoint.Y, vecPoint.Z));
    return fSdf < 0.0f;
}

float Voxels::fCalculateVolume()
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty())
        return 0.0f;
    return static_cast<float>(openvdb::tools::levelSetVolume(*m_roImpl->roGrid, true));
}

void Voxels::GetSurfaceNormal(const PKVector3& vecSurfacePoint, PKVector3* pvecNormal) const
{
    EnsureReady();
    if (!pvecNormal)
        throw std::invalid_argument("Surface normal output pointer is null");

    const openvdb::Vec3d vecIndex = m_roImpl->roGrid->worldToIndex(
        openvdb::Vec3d(vecSurfacePoint.X, vecSurfacePoint.Y, vecSurfacePoint.Z));

    openvdb::math::BoxStencil<FloatGrid> oStencil(*m_roImpl->roGrid);
    oStencil.moveTo(vecIndex);

    auto vecGradient = oStencil.gradient(openvdb::Vec3f(
        static_cast<float>(vecIndex.x()),
        static_cast<float>(vecIndex.y()),
        static_cast<float>(vecIndex.z())));

    if (!vecGradient.normalize())
    {
        *pvecNormal = {0.0f, 0.0f, 0.0f};
        return;
    }

    *pvecNormal = {static_cast<float>(vecGradient.x()),
                   static_cast<float>(vecGradient.y()),
                   static_cast<float>(vecGradient.z())};
}

bool Voxels::bFindClosestPointOnSurface(const PKVector3& vecSearch,
                                        PKVector3* pvecSurfacePoint,
                                        float* pfDistanceMM) const
{
    EnsureReady();
    if (!pvecSurfacePoint)
        throw std::invalid_argument("Closest surface point output pointer is null");
    if (!pfDistanceMM)
        throw std::invalid_argument("Closest surface point distance output pointer is null");

    const openvdb::Vec3R vecSearchWS(vecSearch.X, vecSearch.Y, vecSearch.Z);
    if (!vecSearchWS.isFinite())
        throw std::invalid_argument("Closest surface point search position must be finite");

    std::lock_guard<std::mutex> oLock(m_roImpl->oClosestSurfaceMutex);

    if (!m_roImpl->bClosestSurfaceCacheInitialized)
    {
        m_roImpl->roClosestSurface = Impl::ClosestSurfacePoint::create(
            *m_roImpl->roGrid, 0.0f);
        m_roImpl->bClosestSurfaceCacheInitialized = true;
    }

    if (!m_roImpl->roClosestSurface)
        return false;

    std::vector<openvdb::Vec3R> aPoints{vecSearchWS};
    std::vector<float> aDistances;
    if (!m_roImpl->roClosestSurface->searchAndReplace(aPoints, aDistances) ||
        aDistances.empty() || !std::isfinite(aDistances.front()))
    {
        return false;
    }

    const openvdb::Vec3R& vecSurface = aPoints.front();
    *pvecSurfacePoint = {static_cast<float>(vecSurface.x()),
                         static_cast<float>(vecSurface.y()),
                         static_cast<float>(vecSurface.z())};
    *pfDistanceMM = aDistances.front();
    return true;
}

bool Voxels::bRayCastToSurface(const PKVector3& vecSearch,
                               const PKVector3& vecDirection,
                               PKVector3* pvecSurfacePoint) const
{
    EnsureReady();
    if (!pvecSurfacePoint)
        throw std::invalid_argument("Ray cast output pointer is null");

    const openvdb::Vec3R vecRayOrigin(vecSearch.X, vecSearch.Y, vecSearch.Z);
    if (!vecRayOrigin.isFinite())
        throw std::invalid_argument("Ray origin must be finite");

    openvdb::Vec3R vecRayDirection(vecDirection.X, vecDirection.Y, vecDirection.Z);
    if (!vecRayDirection.isFinite() || !vecRayDirection.normalize())
        throw std::invalid_argument("Ray direction must be finite and non-zero");

    openvdb::tools::LevelSetRayIntersector<FloatGrid> oIntersector(*m_roImpl->roGrid);
    const openvdb::math::Ray<openvdb::Real> oRay(vecRayOrigin, vecRayDirection);

    openvdb::Vec3R vecHit;
    if (!oIntersector.intersectsWS(oRay, vecHit))
        return false;

    *pvecSurfacePoint = {static_cast<float>(vecHit.x()),
                         static_cast<float>(vecHit.y()),
                         static_cast<float>(vecHit.z())};
    return true;
}

void Voxels::GetVoxelDimensions(int32_t* pnXMin,
                                int32_t* pnYMin,
                                int32_t* pnZMin,
                                int32_t* pnXSize,
                                int32_t* pnYSize,
                                int32_t* pnZSize) const
{
    EnsureReady();
    if (!pnXMin || !pnYMin || !pnZMin || !pnXSize || !pnYSize || !pnZSize)
        throw std::invalid_argument("Voxel dimension output pointer is null");

    if (m_roImpl->roGrid->tree().empty())
    {
        *pnXMin = *pnYMin = *pnZMin = 0;
        *pnXSize = *pnYSize = *pnZSize = 0;
        return;
    }

    const openvdb::CoordBBox oBBox = m_roImpl->roGrid->evalActiveVoxelBoundingBox();
    *pnXMin = oBBox.min().x();
    *pnYMin = oBBox.min().y();
    *pnZMin = oBBox.min().z();
    *pnXSize = oBBox.extents().x();
    *pnYSize = oBBox.extents().y();
    *pnZSize = oBBox.extents().z();
}

void Voxels::GetZSlice(int32_t nZSlice, int16_t* pnBuffer) const
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty()) return;
    if (!pnBuffer)
        throw std::invalid_argument("Slice buffer is null");

    const openvdb::CoordBBox oBBox = m_roImpl->roGrid->evalActiveVoxelBoundingBox();
    openvdb::Coord xyz(0, 0, nZSlice + oBBox.min().z());
    auto oAccess = m_roImpl->roGrid->getConstAccessor();
    const float fGridBackgroundMM = this->fBackgroundMM();
    const float fSdfToIntScale =
        static_cast<float>(PKSDF_OUTSIDE_BACKGROUND) / fGridBackgroundMM;
    size_t n = 0;

    for (xyz.y() = oBBox.max().y(); xyz.y() >= oBBox.min().y(); --xyz.y())
    for (xyz.x() = oBBox.min().x(); xyz.x() <= oBBox.max().x(); ++xyz.x())
        pnBuffer[n++] = nEncodeSdf(oAccess.getValue(xyz), fGridBackgroundMM, fSdfToIntScale);
}

void Voxels::GetXSlice(int32_t nXSlice, int16_t* pnBuffer) const
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty()) return;
    if (!pnBuffer)
        throw std::invalid_argument("Slice buffer is null");

    const openvdb::CoordBBox oBBox = m_roImpl->roGrid->evalActiveVoxelBoundingBox();
    openvdb::Coord xyz(nXSlice + oBBox.min().x(), 0, 0);
    auto oAccess = m_roImpl->roGrid->getConstAccessor();
    const float fGridBackgroundMM = this->fBackgroundMM();
    const float fSdfToIntScale =
        static_cast<float>(PKSDF_OUTSIDE_BACKGROUND) / fGridBackgroundMM;
    size_t n = 0;

    for (xyz.z() = oBBox.max().z(); xyz.z() >= oBBox.min().z(); --xyz.z())
    for (xyz.y() = oBBox.min().y(); xyz.y() <= oBBox.max().y(); ++xyz.y())
        pnBuffer[n++] = nEncodeSdf(oAccess.getValue(xyz), fGridBackgroundMM, fSdfToIntScale);
}

void Voxels::GetYSlice(int32_t nYSlice, int16_t* pnBuffer) const
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty()) return;
    if (!pnBuffer)
        throw std::invalid_argument("Slice buffer is null");

    const openvdb::CoordBBox oBBox = m_roImpl->roGrid->evalActiveVoxelBoundingBox();
    openvdb::Coord xyz(0, nYSlice + oBBox.min().y(), 0);
    auto oAccess = m_roImpl->roGrid->getConstAccessor();
    const float fGridBackgroundMM = this->fBackgroundMM();
    const float fSdfToIntScale =
        static_cast<float>(PKSDF_OUTSIDE_BACKGROUND) / fGridBackgroundMM;
    size_t n = 0;

    for (xyz.z() = oBBox.max().z(); xyz.z() >= oBBox.min().z(); --xyz.z())
    for (xyz.x() = oBBox.min().x(); xyz.x() <= oBBox.max().x(); ++xyz.x())
        pnBuffer[n++] = nEncodeSdf(oAccess.getValue(xyz), fGridBackgroundMM, fSdfToIntScale);
}

void Voxels::GetInterpolatedZSlice(float fZSlice, int16_t* pnBuffer) const
{
    EnsureReady();
    if (m_roImpl->roGrid->tree().empty()) return;
    if (!pnBuffer)
        throw std::invalid_argument("Slice buffer is null");

    const openvdb::CoordBBox oBBox = m_roImpl->roGrid->evalActiveVoxelBoundingBox();
    auto oAccess = m_roImpl->roGrid->getConstAccessor();
    openvdb::Vec3R vec(0, 0, fZSlice + oBBox.min().z());
    openvdb::tools::BoxSampler oSampler;
    const float fGridBackgroundMM = this->fBackgroundMM();
    const float fSdfToIntScale =
        static_cast<float>(PKSDF_OUTSIDE_BACKGROUND) / fGridBackgroundMM;
    size_t n = 0;

    for (vec.y() = oBBox.max().y(); vec.y() >= oBBox.min().y(); --vec.y())
    for (vec.x() = oBBox.min().x(); vec.x() <= oBBox.max().x(); ++vec.x())
    {
        pnBuffer[n++] = nEncodeSdf(
            static_cast<float>(oSampler.sample(oAccess, vec)),
            fGridBackgroundMM,
            fSdfToIntScale);
    }
}

std::shared_ptr<Mesh> Voxels::roAsMesh() const
{
    EnsureReady();
    std::vector<openvdb::Vec3s> aPoints;
    std::vector<openvdb::Vec3I> aTrianglesVdb;
    std::vector<openvdb::Vec4I> aQuadsVdb;

    openvdb::tools::volumeToMesh<FloatGrid>(*m_roImpl->roGrid,
                                             aPoints,
                                             aTrianglesVdb,
                                             aQuadsVdb,
                                             0.0f,
                                             0.0,
                                             false);

    std::vector<PKVector3> aVertices;
    std::vector<PKTriangle> aTriangles;
    std::vector<PKQuad> aQuads;

    aVertices.reserve(aPoints.size());
    aTriangles.reserve(aTrianglesVdb.size());
    aQuads.reserve(aQuadsVdb.size());

    for (const openvdb::Vec3s& vec : aPoints)
        aVertices.push_back({vec.x(), vec.y(), vec.z()});

    // Preserve PicoGK's historical outward winding while retaining OpenVDB's
    // quad topology instead of immediately splitting quads into triangles.
    for (const openvdb::Vec3I& tri : aTrianglesVdb)
        aTriangles.push_back({static_cast<uint32_t>(tri[2]),
                              static_cast<uint32_t>(tri[1]),
                              static_cast<uint32_t>(tri[0])});

    for (const openvdb::Vec4I& q : aQuadsVdb)
        aQuads.push_back({static_cast<uint32_t>(q[0]),
                          static_cast<uint32_t>(q[3]),
                          static_cast<uint32_t>(q[2]),
                          static_cast<uint32_t>(q[1])});

    return std::make_shared<Mesh>(std::move(aVertices),
                                  std::move(aTriangles),
                                  std::move(aQuads));
}
}
