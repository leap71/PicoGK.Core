// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_VOXELS_H_
#define PICOGK_VOXELS_H_

#include "PicoGKApiTypes.h"

#include <cstdint>
#include <memory>
#include <string>

namespace PicoGK
{
class Mesh;

/// Mutable sparse signed-distance representation of solid geometry.
///
/// Voxels is backed by an OpenVDB level set, but OpenVDB types are deliberately
/// hidden from this header. World-space coordinates and distances are expressed
/// in millimetres. Negative SDF values represent the solid interior. PicoGK uses
/// a fixed narrow-band half-width of three voxels.
class Voxels
{
public:
    using Ptr = std::shared_ptr<Voxels>;

    /// Creates an empty level set.
    explicit Voxels(float fVoxelSizeMM);

    /// Creates a deep copy. Derived acceleration caches are not copied.
    Voxels(const Voxels& oSource);

    /// Creates a spherical solid [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3& vecCenter,
           float fRadius);

    /// Creates a tapered capsule solid [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3& vecStart,
           const PKVector3& vecEnd,
           float fRadiusStart,
           float fRadiusEnd);

    /// Creates the CSG union of equal-radius spheres [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3* pCenters,
           uint32_t nCenterCount,
           float fRadiusMM);

    /// Creates the CSG union of variable-radius spheres [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3* pCenters,
           const float* pRadiiMM,
           uint32_t nCenterCount);

    /// Creates a round-capped tube complex with one common radius [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3* pVertices,
           uint32_t nVertexCount,
           const PKSegment* pSegments,
           uint32_t nSegmentCount,
           float fRadiusMM);

    /// Creates a round-capped tube complex with per-vertex radii [mm].
    Voxels(float fVoxelSizeMM,
           const PKVector3* pVertices,
           const float* pVertexRadiiMM,
           uint32_t nVertexCount,
           const PKSegment* pSegments,
           uint32_t nSegmentCount);

    /// Creates an object in streaming SDF-import state.
    Voxels(float fVoxelSizeMM, const PKSdfVolumeDesc& oVolume);

    /// Converts a polygon Mesh into a solid level set.
    Voxels(float fVoxelSizeMM,
           const Mesh& oMesh);

    /// Creates a shell by dilating a Mesh surface by fRadius [mm].
    Voxels(float fVoxelSizeMM,
           const Mesh& oMesh,
           float fRadius);

    ~Voxels();

    Voxels& operator=(const Voxels&) = delete;

    /// Returns estimated native memory owned by this object [bytes].
    ///
    /// During SDF reconstruction this includes the staging grid and slab buffer.
    int64_t nMemUsage() const;

    /// Runs OpenVDB level-set diagnostics and returns the diagnostic message.
    std::string strDiagnose() const;

    /// Returns true if the level set contains no interior samples.
    bool bIsEmpty() const;

    /// Returns the voxel edge length [mm].
    float fVoxelSizeMM() const;

    /// Returns the positive SDF background value [mm].
    float fBackgroundMM() const;

    /// Replaces this object with its union with oOther.
    void BoolAdd(const Voxels& oOther);

    /// Subtracts oOther from this object.
    void BoolSubtract(const Voxels& oOther);

    /// Replaces this object with its intersection with oOther.
    void BoolIntersect(const Voxels& oOther);

    /// Offsets the surface [mm]. Positive values grow the solid.
    void Offset(float fDistMM);

    /// Applies two sequential offsets [mm].
    void DoubleOffset(float fDist1MM, float fDist2MM);

    /// Applies +fDist, -2*fDist, +fDist sequentially [mm].
    void TripleOffset(float fDistMM);

    /// Adds the next canonical int16 Z slice to a streaming SDF import.
    void ImportSdfZSlice(uint32_t nZSlice, const PKSdfSlice& oSlice);

    /// Finalizes a streaming SDF import and makes the reconstructed grid active.
    void EndSdfImport();

    /// Classifies a continuous world-space point using interpolated SDF sign.
    bool bIsInside(const PKVector3& vecPoint) const;

    /// Calculates enclosed volume [mm^3].
    float fCalculateVolume();

    /// Evaluates the normalized continuous SDF gradient near the surface.
    void GetSurfaceNormal(const PKVector3& vecSurfacePoint,
                          PKVector3* pvecNormal) const;

    /// Finds the closest zero-isosurface point and unsigned distance [mm].
    ///
    /// The OpenVDB closest-surface accelerator is built lazily and cached until
    /// this Voxels object is modified.
    bool bFindClosestPointOnSurface(const PKVector3& vecSearch,
                                    PKVector3* pvecSurfacePoint,
                                    float* pfDistanceMM) const;

    /// Casts a world-space ray and returns the first surface intersection.
    bool bRayCastToSurface(const PKVector3& vecSearch,
                           const PKVector3& vecDirection,
                           PKVector3* pvecSurfacePoint) const;

    /// Returns the active voxel bounding-box origin and size in index space.
    void GetVoxelDimensions(int32_t* pnXMin,
                            int32_t* pnYMin,
                            int32_t* pnZMin,
                            int32_t* pnXSize,
                            int32_t* pnYSize,
                            int32_t* pnZSize) const;

    /// Fills a caller-owned dense X slice.
    void GetXSlice(int32_t nXSlice, int16_t* pnBuffer) const;

    /// Fills a caller-owned dense Y slice.
    void GetYSlice(int32_t nYSlice, int16_t* pnBuffer) const;

    /// Fills a caller-owned dense Z slice.
    void GetZSlice(int32_t nZSlice, int16_t* pnBuffer) const;

    /// Fills a caller-owned Z slice using interpolation at fractional index Z.
    void GetInterpolatedZSlice(float fZSlice, int16_t* pnBuffer) const;

    /// Extracts the zero isosurface as an immutable Mesh.
    std::shared_ptr<Mesh> roAsMesh() const;

private:
    class Impl;
    std::unique_ptr<Impl> m_roImpl;

    void EnsureReady() const;
    void ValidateGrid() const;
    void ValidateCompatible(const Voxels& oOther) const;
    void FlushSdfImportSlab();
};
}

#endif // PICOGK_VOXELS_H_
