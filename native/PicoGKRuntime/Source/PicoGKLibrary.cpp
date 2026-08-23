// SPDX-License-Identifier: Apache-2.0
#include "PicoGK.h"

#include "PicoGKLibraryMgr.h"
#include "PicoGKMesh.h"
#include "PicoGKVoxels.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <exception>
#include <stdexcept>
#include <string>

namespace
{
thread_local std::string g_strLastError;

void ClearLastError() noexcept
{
    g_strLastError.clear();
}

void SetLastError(const char* psz) noexcept
{
    try
    {
        g_strLastError = psz ? psz : "Unknown native error";
    }
    catch (...)
    {
        // Error reporting itself must never escape the C ABI.
    }
}

void SetCurrentException() noexcept
{
    try
    {
        throw;
    }
    catch (const std::exception& ex)
    {
        SetLastError(ex.what());
    }
    catch (...)
    {
        SetLastError("Unknown native exception");
    }
}

void CopyString(const std::string& str, char* psz, int32_t nMaxStringLen)
{
    if (!psz || nMaxStringLen <= 0)
        throw std::invalid_argument("String output buffer is invalid");

    const size_t nMaxCopy = static_cast<size_t>(nMaxStringLen - 1);
    const size_t nCopy = std::min(nMaxCopy, str.size());
    if (nCopy > 0)
        std::memcpy(psz, str.data(), nCopy);
    psz[nCopy] = '\0';
}

/// Executes an exported C API operation while preventing C++ exceptions from
/// crossing the ABI boundary. Each call clears the previous thread-local error;
/// failures are converted into that error state and, for value-returning calls,
/// the supplied fallback value.
class ApiBoundary
{
public:
    template<typename TFn>
    static void Run(TFn&& fn) noexcept
    {
        ClearLastError();

        try
        {
            fn();
        }
        catch (...)
        {
            SetCurrentException();
        }
    }

    template<typename TResult, typename TFn>
    static TResult Run(TResult oFallback, TFn&& fn) noexcept
    {
        ClearLastError();

        try
        {
            return fn();
        }
        catch (...)
        {
            SetCurrentException();
            return oFallback;
        }
    }
};

PicoGK::Library::Instance::Ptr roInstance(PKINSTANCE hInstance)
{
    return PicoGK::Library::oLib().roGetInstance(hInstance);
}

PicoGK::Mesh::Ptr roMesh(PKINSTANCE hInstance, PKMESH hMesh)
{
    return roInstance(hInstance)->m_oMeshes.roGet(hMesh);
}

PicoGK::Voxels::Ptr roVoxels(PKINSTANCE hInstance, PKVOXELS hVoxels)
{
    return roInstance(hInstance)->m_oVoxels.roGet(hVoxels);
}

int32_t nMmToVoxel(float fMM, float fVoxelSizeMM)
{
    return static_cast<int32_t>(std::lround(fMM / fVoxelSizeMM));
}

enum class ESliceAxis
{
    X,
    Y,
    Z
};

/// Prepares and validates a caller-owned canonical SDF slice descriptor before
/// native extraction writes into its buffer. Dimensions are outputs; pValues
/// and nValueCapacity are supplied by the caller.
void PrepareSdfSlice(const PicoGK::Voxels& oVoxels,
                     ESliceAxis eAxis,
                     PKSdfSlice* pSlice)
{
    if (!pSlice)
        throw std::invalid_argument("SDF slice descriptor is null");

    int32_t nXOrigin = 0, nYOrigin = 0, nZOrigin = 0;
    int32_t nXSize = 0, nYSize = 0, nZSize = 0;
    oVoxels.GetVoxelDimensions(
        &nXOrigin, &nYOrigin, &nZOrigin, &nXSize, &nYSize, &nZSize);

    (void)nXOrigin;
    (void)nYOrigin;
    (void)nZOrigin;

    int32_t nWidth = 0;
    int32_t nHeight = 0;

    switch (eAxis)
    {
        case ESliceAxis::X: nWidth = nYSize; nHeight = nZSize; break;
        case ESliceAxis::Y: nWidth = nXSize; nHeight = nZSize; break;
        case ESliceAxis::Z: nWidth = nXSize; nHeight = nYSize; break;
    }

    if (nWidth < 0 || nHeight < 0)
        throw std::runtime_error("Invalid negative SDF slice dimensions");

    pSlice->nWidth  = static_cast<uint32_t>(nWidth);
    pSlice->nHeight = static_cast<uint32_t>(nHeight);

    const uint64_t nRequired =
        static_cast<uint64_t>(pSlice->nWidth) *
        static_cast<uint64_t>(pSlice->nHeight);

    if (nRequired == 0)
        return;

    if (!pSlice->pValues)
        throw std::invalid_argument("SDF slice value buffer is null");

    if (pSlice->nValueCapacity < nRequired)
        throw std::length_error("SDF slice value buffer is too small");
}
}

// Runtime / Library -----------------------------------------------------------
PICOGK_API bool Library_bGetName(char psz[PKINFOSTRINGLEN])
{
    if (psz) psz[0] = '\0';
    return ApiBoundary::Run(false, [&]
    {
        CopyString(PicoGK::Library::oLib().strName(), psz, PKINFOSTRINGLEN);
        return true;
    });
}

PICOGK_API bool Library_bGetVersion(char psz[PKINFOSTRINGLEN])
{
    if (psz) psz[0] = '\0';
    return ApiBoundary::Run(false, [&]
    {
        CopyString(PicoGK::Library::oLib().strVersion(), psz, PKINFOSTRINGLEN);
        return true;
    });
}

PICOGK_API bool Library_bGetBuildInfo(char psz[PKINFOSTRINGLEN])
{
    if (psz) psz[0] = '\0';
    return ApiBoundary::Run(false, [&]
    {
        CopyString(PicoGK::Library::oLib().strBuildInfo(), psz, PKINFOSTRINGLEN);
        return true;
    });
}

PICOGK_API bool Library_bGetLastError(char* psz, int32_t nMaxStringLen)
{
    if (g_strLastError.empty())
    {
        if (psz && nMaxStringLen > 0) psz[0] = '\0';
        return false;
    }

    try
    {
        CopyString(g_strLastError, psz, nMaxStringLen);
        return true;
    }
    catch (...)
    {
        return false;
    }
}

PICOGK_API PKINSTANCE Library_hCreateInstance(float fVoxelSizeMM)
{
    return ApiBoundary::Run(PKINSTANCE{0}, [&]
    {
        return PicoGK::Library::oLib().hCreateInstance(fVoxelSizeMM);
    });
}

PICOGK_API bool Library_bDestroyInstance(PKINSTANCE hThis)
{
    return ApiBoundary::Run(false, [&]
    {
        // Deliberately idempotent. Managed SafeHandle finalizers may run after
        // explicit disposal has already destroyed the Library instance.
        PicoGK::Library::oLib().bDestroyInstance(hThis);
        return true;
    });
}

PICOGK_API bool Library_bGetIsValid(PKINSTANCE hThis, bool* pbValid)
{
    if (pbValid) *pbValid = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pbValid) throw std::invalid_argument("Validity output pointer is null");
        *pbValid = PicoGK::Library::oLib().bIsValid(hThis);
        return true;
    });
}

PICOGK_API bool Library_bGetTotalMemUsage(PKINSTANCE hThis, int64_t* pnBytes)
{
    if (pnBytes) *pnBytes = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnBytes) throw std::invalid_argument("Memory usage output pointer is null");
        *pnBytes = roInstance(hThis)->nMemUsage();
        return true;
    });
}

PICOGK_API bool Library_bGetMeshesMemUsage(PKINSTANCE hThis, int64_t* pnBytes)
{
    if (pnBytes) *pnBytes = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnBytes) throw std::invalid_argument("Memory usage output pointer is null");
        *pnBytes = roInstance(hThis)->m_oMeshes.nMemUsage();
        return true;
    });
}

PICOGK_API bool Library_bGetVoxelsMemUsage(PKINSTANCE hThis, int64_t* pnBytes)
{
    if (pnBytes) *pnBytes = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnBytes) throw std::invalid_argument("Memory usage output pointer is null");
        *pnBytes = roInstance(hThis)->m_oVoxels.nMemUsage();
        return true;
    });
}

PICOGK_API bool Library_bGetMeshesAllocated(PKINSTANCE hThis, int64_t* pnCount)
{
    if (pnCount) *pnCount = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnCount) throw std::invalid_argument("Allocated count output pointer is null");
        *pnCount = roInstance(hThis)->m_oMeshes.nAllocatedCount();
        return true;
    });
}

PICOGK_API bool Library_bGetVoxelsAllocated(PKINSTANCE hThis, int64_t* pnCount)
{
    if (pnCount) *pnCount = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnCount) throw std::invalid_argument("Allocated count output pointer is null");
        *pnCount = roInstance(hThis)->m_oVoxels.nAllocatedCount();
        return true;
    });
}

PICOGK_API bool Library_bVoxelsToMm(PKINSTANCE hThis,
                                    const PKVector3* pvecVoxelCoordinate,
                                    PKVector3* pvecMmCoordinate)
{
    if (pvecMmCoordinate) *pvecMmCoordinate = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!pvecVoxelCoordinate || !pvecMmCoordinate)
            throw std::invalid_argument("Coordinate pointer is null");

        const float fVoxel = roInstance(hThis)->fVoxelSizeMM();
        *pvecMmCoordinate = {pvecVoxelCoordinate->X * fVoxel,
                             pvecVoxelCoordinate->Y * fVoxel,
                             pvecVoxelCoordinate->Z * fVoxel};
        return true;
    });
}

PICOGK_API bool Library_bMmToVoxels(PKINSTANCE hThis,
                                    const PKVector3* pvecMmCoordinate,
                                    PKVector3* pvecVoxelCoordinate)
{
    if (pvecVoxelCoordinate) *pvecVoxelCoordinate = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!pvecMmCoordinate || !pvecVoxelCoordinate)
            throw std::invalid_argument("Coordinate pointer is null");

        const float fVoxel = roInstance(hThis)->fVoxelSizeMM();
        *pvecVoxelCoordinate = {static_cast<float>(nMmToVoxel(pvecMmCoordinate->X, fVoxel)),
                                static_cast<float>(nMmToVoxel(pvecMmCoordinate->Y, fVoxel)),
                                static_cast<float>(nMmToVoxel(pvecMmCoordinate->Z, fVoxel))};
        return true;
    });
}

// Mesh ------------------------------------------------------------------------
PICOGK_API PKMESH Mesh_hCreateFromBuffers(PKINSTANCE hInstance,
                                           const PKVector3* pVertices,
                                           uint32_t nVertices,
                                           const PKTriangle* pTriangles,
                                           uint32_t nTriangles,
                                           const PKQuad* pQuads,
                                           uint32_t nQuads)
{
    return ApiBoundary::Run(PKMESH{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oMeshes.hAdd(std::make_shared<PicoGK::Mesh>(
            pVertices, nVertices, pTriangles, nTriangles, pQuads, nQuads));
    });
}

PICOGK_API PKMESH Mesh_hCreateFromVoxels(PKINSTANCE hInstance, PKVOXELS hVoxels)
{
    return ApiBoundary::Run(PKMESH{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roSource = roLib->m_oVoxels.roGet(hVoxels);
        return roLib->m_oMeshes.hAdd(roSource->roAsMesh());
    });
}

PICOGK_API bool Mesh_bGetIsValid(PKINSTANCE hInstance, PKMESH hThis, bool* pbValid)
{
    if (pbValid) *pbValid = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pbValid) throw std::invalid_argument("Validity output pointer is null");
        auto roLib = PicoGK::Library::oLib().roTryGetInstance(hInstance);
        *pbValid = roLib && roLib->m_oMeshes.bIsValid(hThis);
        return true;
    });
}

PICOGK_API bool Mesh_bDestroy(PKINSTANCE hInstance, PKMESH hThis)
{
    return ApiBoundary::Run(false, [&]
    {
        // Deliberately idempotent so a child SafeHandle may finalize after its
        // owning Library has already destroyed all native geometry.
        auto roLib = PicoGK::Library::oLib().roTryGetInstance(hInstance);
        if (roLib) roLib->m_oMeshes.bDestroy(hThis);
        return true;
    });
}

PICOGK_API bool Mesh_bGetMemUsage(PKINSTANCE hInstance, PKMESH hThis, int64_t* pnBytes)
{
    if (pnBytes) *pnBytes = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnBytes) throw std::invalid_argument("Memory usage output pointer is null");
        *pnBytes = roMesh(hInstance, hThis)->nMemUsage();
        return true;
    });
}

PICOGK_API bool Mesh_bGetBoundingBox(PKINSTANCE hInstance, PKMESH hThis, PKBBox3* poBox)
{
    if (poBox) *poBox = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!poBox) throw std::invalid_argument("Bounding box output pointer is null");
        *poBox = roMesh(hInstance, hThis)->oBBox();
        return true;
    });
}

PICOGK_API bool Mesh_bGetView(PKINSTANCE hInstance, PKMESH hThis, PKMeshView* poView)
{
    if (poView) *poView = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!poView) throw std::invalid_argument("Mesh view output pointer is null");
        roMesh(hInstance, hThis)->GetView(poView);
        return true;
    });
}

PICOGK_API bool Mesh_bGetTriangulatedView(PKINSTANCE hInstance, PKMESH hThis, PKTriangulatedMeshView* poView)
{
    if (poView) *poView = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!poView) throw std::invalid_argument("Triangulated Mesh view output pointer is null");
        roMesh(hInstance, hThis)->GetTriangulatedView(poView);
        return true;
    });
}

// Voxels: creation and lifetime -----------------------------------------------
PICOGK_API PKVOXELS Voxels_hCreate(PKINSTANCE hInstance)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(roLib->fVoxelSizeMM()));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateCopy(PKINSTANCE hInstance, PKVOXELS hSource)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(*roLib->m_oVoxels.roGet(hSource)));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateSphere(PKINSTANCE hInstance,
                                          const PKVector3* pvecCenter,
                                          float fRadius)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        if (!pvecCenter) throw std::invalid_argument("Sphere center pointer is null");
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), *pvecCenter, fRadius));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateCapsule(PKINSTANCE hInstance,
                                           const PKVector3* pvecStart,
                                           const PKVector3* pvecStop,
                                           float fRadius1,
                                           float fRadius2)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        if (!pvecStart || !pvecStop) throw std::invalid_argument("Capsule endpoint pointer is null");
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), *pvecStart, *pvecStop, fRadius1, fRadius2));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateSpheres(PKINSTANCE hInstance,
                                           const PKVector3* pCenters,
                                           uint32_t nCenterCount,
                                           float fRadiusMM)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), pCenters, nCenterCount, fRadiusMM));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateVariableSpheres(PKINSTANCE hInstance,
                                                   const PKVector3* pCenters,
                                                   const float* pRadiiMM,
                                                   uint32_t nCenterCount)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), pCenters, pRadiiMM, nCenterCount));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateTubes(PKINSTANCE hInstance,
                                        const PKVector3* pVertices,
                                        uint32_t nVertexCount,
                                        const PKSegment* pSegments,
                                        uint32_t nSegmentCount,
                                        float fRadiusMM)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(),
            pVertices, nVertexCount,
            pSegments, nSegmentCount,
            fRadiusMM));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateVariableTubes(PKINSTANCE hInstance,
                                                 const PKVector3* pVertices,
                                                 const float* pVertexRadiiMM,
                                                 uint32_t nVertexCount,
                                                 const PKSegment* pSegments,
                                                 uint32_t nSegmentCount)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(),
            pVertices, pVertexRadiiMM, nVertexCount,
            pSegments, nSegmentCount));
    });
}

PICOGK_API PKVOXELS Voxels_hBeginSdfImport(PKINSTANCE hInstance,
                                            const PKSdfVolumeDesc* pVolume)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        if (!pVolume) throw std::invalid_argument("SDF volume descriptor is null");
        auto roLib = roInstance(hInstance);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), *pVolume));
    });
}

PICOGK_API bool Voxels_bImportSdfZSlice(PKINSTANCE hInstance,
                                        PKVOXELS hThis,
                                        uint32_t nZSlice,
                                        const PKSdfSlice* pSlice)
{
    return ApiBoundary::Run(false, [&]
    {
        if (!pSlice) throw std::invalid_argument("SDF slice descriptor is null");
        roVoxels(hInstance, hThis)->ImportSdfZSlice(nZSlice, *pSlice);
        return true;
    });
}

PICOGK_API bool Voxels_bEndSdfImport(PKINSTANCE hInstance, PKVOXELS hThis)
{
    return ApiBoundary::Run(false, [&]
    {
        roVoxels(hInstance, hThis)->EndSdfImport();
        return true;
    });
}

PICOGK_API PKVOXELS Voxels_hCreateFromMesh(PKINSTANCE hInstance, PKMESH hMesh)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roSource = roLib->m_oMeshes.roGet(hMesh);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), *roSource));
    });
}

PICOGK_API PKVOXELS Voxels_hCreateMeshShell(PKINSTANCE hInstance, PKMESH hMesh, float fRadius)
{
    return ApiBoundary::Run(PKVOXELS{0}, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roSource = roLib->m_oMeshes.roGet(hMesh);
        return roLib->m_oVoxels.hAdd(std::make_shared<PicoGK::Voxels>(
            roLib->fVoxelSizeMM(), *roSource, fRadius));
    });
}

PICOGK_API bool Voxels_bGetIsValid(PKINSTANCE hInstance, PKVOXELS hThis, bool* pbValid)
{
    if (pbValid) *pbValid = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pbValid) throw std::invalid_argument("Validity output pointer is null");
        auto roLib = PicoGK::Library::oLib().roTryGetInstance(hInstance);
        *pbValid = roLib && roLib->m_oVoxels.bIsValid(hThis);
        return true;
    });
}

PICOGK_API bool Voxels_bDestroy(PKINSTANCE hInstance, PKVOXELS hThis)
{
    return ApiBoundary::Run(false, [&]
    {
        auto roLib = PicoGK::Library::oLib().roTryGetInstance(hInstance);
        if (roLib) roLib->m_oVoxels.bDestroy(hThis);
        return true;
    });
}

PICOGK_API bool Voxels_bDiagnose(PKINSTANCE hInstance,
                                 PKVOXELS hThis,
                                 bool* pbHealthy,
                                 char psz[PKINFOSTRINGLEN])
{
    if (pbHealthy) *pbHealthy = false;
    if (psz) psz[0] = '\0';
    return ApiBoundary::Run(false, [&]
    {
        if (!pbHealthy) throw std::invalid_argument("Diagnostic result output pointer is null");
        const std::string strDiagnostic = roVoxels(hInstance, hThis)->strDiagnose();
        CopyString(strDiagnostic, psz, PKINFOSTRINGLEN);
        *pbHealthy = strDiagnostic.empty();
        return true;
    });
}

PICOGK_API bool Voxels_bGetIsEmpty(PKINSTANCE hInstance, PKVOXELS hThis, bool* pbEmpty)
{
    if (pbEmpty) *pbEmpty = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pbEmpty) throw std::invalid_argument("Empty result output pointer is null");
        *pbEmpty = roVoxels(hInstance, hThis)->bIsEmpty();
        return true;
    });
}

PICOGK_API bool Voxels_bGetMemUsage(PKINSTANCE hInstance, PKVOXELS hThis, int64_t* pnBytes)
{
    if (pnBytes) *pnBytes = 0;
    return ApiBoundary::Run(false, [&]
    {
        if (!pnBytes) throw std::invalid_argument("Memory usage output pointer is null");
        *pnBytes = roVoxels(hInstance, hThis)->nMemUsage();
        return true;
    });
}

PICOGK_API bool Voxels_bGetVoxelSize(PKINSTANCE hInstance, PKVOXELS hThis, float* pfVoxelSizeMM)
{
    if (pfVoxelSizeMM) *pfVoxelSizeMM = 0.0f;
    return ApiBoundary::Run(false, [&]
    {
        if (!pfVoxelSizeMM) throw std::invalid_argument("Voxel size output pointer is null");
        *pfVoxelSizeMM = roVoxels(hInstance, hThis)->fVoxelSizeMM();
        return true;
    });
}

// Voxels: modification --------------------------------------------------------
PICOGK_API bool Voxels_bBoolAdd(PKINSTANCE hInstance, PKVOXELS hThis, PKVOXELS hOther)
{
    return ApiBoundary::Run(false, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roThis = roLib->m_oVoxels.roGet(hThis);
        auto roOther = roLib->m_oVoxels.roGet(hOther);
        roThis->BoolAdd(*roOther);
        return true;
    });
}

PICOGK_API bool Voxels_bBoolSubtract(PKINSTANCE hInstance, PKVOXELS hThis, PKVOXELS hOther)
{
    return ApiBoundary::Run(false, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roThis = roLib->m_oVoxels.roGet(hThis);
        auto roOther = roLib->m_oVoxels.roGet(hOther);
        roThis->BoolSubtract(*roOther);
        return true;
    });
}

PICOGK_API bool Voxels_bBoolIntersect(PKINSTANCE hInstance, PKVOXELS hThis, PKVOXELS hOther)
{
    return ApiBoundary::Run(false, [&]
    {
        auto roLib = roInstance(hInstance);
        auto roThis = roLib->m_oVoxels.roGet(hThis);
        auto roOther = roLib->m_oVoxels.roGet(hOther);
        roThis->BoolIntersect(*roOther);
        return true;
    });
}

PICOGK_API bool Voxels_bOffset(PKINSTANCE hInstance, PKVOXELS hThis, float fDist)
{
    return ApiBoundary::Run(false, [&]
    {
        roVoxels(hInstance, hThis)->Offset(fDist);
        return true;
    });
}

PICOGK_API bool Voxels_bDoubleOffset(PKINSTANCE hInstance, PKVOXELS hThis, float fDist1, float fDist2)
{
    return ApiBoundary::Run(false, [&]
    {
        roVoxels(hInstance, hThis)->DoubleOffset(fDist1, fDist2);
        return true;
    });
}

PICOGK_API bool Voxels_bTripleOffset(PKINSTANCE hInstance, PKVOXELS hThis, float fDist)
{
    return ApiBoundary::Run(false, [&]
    {
        roVoxels(hInstance, hThis)->TripleOffset(fDist);
        return true;
    });
}

// Voxels: queries -------------------------------------------------------------
PICOGK_API bool Voxels_bGetIsInside(PKINSTANCE hInstance,
                                    PKVOXELS hThis,
                                    const PKVector3* pvecTestPoint,
                                    bool* pbInside)
{
    if (pbInside) *pbInside = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pvecTestPoint || !pbInside)
            throw std::invalid_argument("Inside query pointer is null");
        *pbInside = roVoxels(hInstance, hThis)->bIsInside(*pvecTestPoint);
        return true;
    });
}

PICOGK_API bool Voxels_bGetIsEqual(PKINSTANCE hInstance,
                                   PKVOXELS hThis,
                                   PKVOXELS hOther,
                                   bool* pbEqual)
{
    if (pbEqual) *pbEqual = false;
    return ApiBoundary::Run(false, [&]
    {
        if (!pbEqual) throw std::invalid_argument("Equality result output pointer is null");
        auto roLib = roInstance(hInstance);
        *pbEqual = roLib->m_oVoxels.roGet(hThis)->bIsEqual(*roLib->m_oVoxels.roGet(hOther));
        return true;
    });
}

PICOGK_API bool Voxels_bGetVolume(PKINSTANCE hInstance, PKVOXELS hThis, float* pfVolumeMM3)
{
    if (pfVolumeMM3) *pfVolumeMM3 = 0.0f;
    return ApiBoundary::Run(false, [&]
    {
        if (!pfVolumeMM3) throw std::invalid_argument("Volume output pointer is null");
        *pfVolumeMM3 = roVoxels(hInstance, hThis)->fCalculateVolume();
        return true;
    });
}

PICOGK_API bool Voxels_bGetSurfaceNormal(PKINSTANCE hInstance,
                                         PKVOXELS hThis,
                                         const PKVector3* pvecSurfacePoint,
                                         PKVector3* pvecNormal)
{
    if (pvecNormal) *pvecNormal = {};
    return ApiBoundary::Run(false, [&]
    {
        if (!pvecSurfacePoint || !pvecNormal)
            throw std::invalid_argument("Surface normal query pointer is null");
        roVoxels(hInstance, hThis)->GetSurfaceNormal(*pvecSurfacePoint, pvecNormal);
        return true;
    });
}

PICOGK_API bool Voxels_bClosestPointOnSurface(PKINSTANCE hInstance,
                                              PKVOXELS hThis,
                                              const PKVector3* pvecSearch,
                                              bool* pbFound,
                                              PKVector3* pvecSurfacePoint,
                                              float* pfDistanceMM)
{
    if (pbFound) *pbFound = false;
    if (pvecSurfacePoint) *pvecSurfacePoint = {};
    if (pfDistanceMM) *pfDistanceMM = 0.0f;

    return ApiBoundary::Run(false, [&]
    {
        if (!pvecSearch || !pbFound || !pvecSurfacePoint || !pfDistanceMM)
            throw std::invalid_argument("Closest surface point query pointer is null");
        *pbFound = roVoxels(hInstance, hThis)->bFindClosestPointOnSurface(
            *pvecSearch, pvecSurfacePoint, pfDistanceMM);
        return true;
    });
}

PICOGK_API bool Voxels_bRayCastToSurface(PKINSTANCE hInstance,
                                         PKVOXELS hThis,
                                         const PKVector3* pvecSearch,
                                         const PKVector3* pvecDirection,
                                         bool* pbHit,
                                         PKVector3* pvecSurfacePoint)
{
    if (pbHit) *pbHit = false;
    if (pvecSurfacePoint) *pvecSurfacePoint = {};

    return ApiBoundary::Run(false, [&]
    {
        if (!pvecSearch || !pvecDirection || !pbHit || !pvecSurfacePoint)
            throw std::invalid_argument("Ray query pointer is null");
        *pbHit = roVoxels(hInstance, hThis)->bRayCastToSurface(
            *pvecSearch, *pvecDirection, pvecSurfacePoint);
        return true;
    });
}

// Voxels: canonical SDF access -----------------------------------------------
PICOGK_API bool Voxels_bGetVoxelDimensions(PKINSTANCE hInstance,
                                           PKVOXELS hThis,
                                           int32_t* pnXOrigin,
                                           int32_t* pnYOrigin,
                                           int32_t* pnZOrigin,
                                           int32_t* pnXSize,
                                           int32_t* pnYSize,
                                           int32_t* pnZSize)
{
    if (pnXOrigin) *pnXOrigin = 0;
    if (pnYOrigin) *pnYOrigin = 0;
    if (pnZOrigin) *pnZOrigin = 0;
    if (pnXSize) *pnXSize = 0;
    if (pnYSize) *pnYSize = 0;
    if (pnZSize) *pnZSize = 0;

    return ApiBoundary::Run(false, [&]
    {
        if (!pnXOrigin || !pnYOrigin || !pnZOrigin ||
            !pnXSize || !pnYSize || !pnZSize)
            throw std::invalid_argument("Voxel dimensions output pointer is null");

        roVoxels(hInstance, hThis)->GetVoxelDimensions(
            pnXOrigin, pnYOrigin, pnZOrigin, pnXSize, pnYSize, pnZSize);
        return true;
    });
}

PICOGK_API bool Voxels_bGetXSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nXSlice,
                                  PKSdfSlice* pSlice)
{
    if (pSlice) { pSlice->nWidth = 0; pSlice->nHeight = 0; }
    return ApiBoundary::Run(false, [&]
    {
        auto roThis = roVoxels(hInstance, hThis);
        PrepareSdfSlice(*roThis, ESliceAxis::X, pSlice);
        roThis->GetXSlice(nXSlice, pSlice->pValues);
        return true;
    });
}

PICOGK_API bool Voxels_bGetYSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nYSlice,
                                  PKSdfSlice* pSlice)
{
    if (pSlice) { pSlice->nWidth = 0; pSlice->nHeight = 0; }
    return ApiBoundary::Run(false, [&]
    {
        auto roThis = roVoxels(hInstance, hThis);
        PrepareSdfSlice(*roThis, ESliceAxis::Y, pSlice);
        roThis->GetYSlice(nYSlice, pSlice->pValues);
        return true;
    });
}

PICOGK_API bool Voxels_bGetZSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nZSlice,
                                  PKSdfSlice* pSlice)
{
    if (pSlice) { pSlice->nWidth = 0; pSlice->nHeight = 0; }
    return ApiBoundary::Run(false, [&]
    {
        auto roThis = roVoxels(hInstance, hThis);
        PrepareSdfSlice(*roThis, ESliceAxis::Z, pSlice);
        roThis->GetZSlice(nZSlice, pSlice->pValues);
        return true;
    });
}

PICOGK_API bool Voxels_bGetInterpolatedZSlice(PKINSTANCE hInstance,
                                              PKVOXELS hThis,
                                              float fZSlice,
                                              PKSdfSlice* pSlice)
{
    if (pSlice) { pSlice->nWidth = 0; pSlice->nHeight = 0; }
    return ApiBoundary::Run(false, [&]
    {
        auto roThis = roVoxels(hInstance, hThis);
        PrepareSdfSlice(*roThis, ESliceAxis::Z, pSlice);
        roThis->GetInterpolatedZSlice(fZSlice, pSlice->pValues);
        return true;
    });
}
