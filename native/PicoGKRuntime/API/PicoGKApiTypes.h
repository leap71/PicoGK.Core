// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_API_TYPES_H_
#define PICOGK_API_TYPES_H_

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/// Opaque 64-bit handle used by the PicoGK C ABI.
///
/// Handles are globally unique within the process. A value of zero is invalid.
typedef uint64_t PKHANDLE;

/// Handle to a PicoGK Library instance.
typedef PKHANDLE PKINSTANCE;

/// Handle to an immutable native Mesh.
typedef PKHANDLE PKMESH;

/// Handle to a mutable native Voxels object.
typedef PKHANDLE PKVOXELS;

/// Three-dimensional vector in PicoGK world coordinates.
///
/// Unless documented otherwise, PicoGK world coordinates and distances are
/// expressed in millimetres.
typedef struct PKVector3
{
    float X;
    float Y;
    float Z;
} PKVector3;

/// Triangle defined by three unsigned vertex indices.
typedef struct PKTriangle
{
    uint32_t A;
    uint32_t B;
    uint32_t C;
} PKTriangle;

/// Quad defined by four unsigned vertex indices.
typedef struct PKQuad
{
    uint32_t A;
    uint32_t B;
    uint32_t C;
    uint32_t D;
} PKQuad;

/// Axis-aligned bounds in PicoGK world coordinates [mm].
///
/// nHasValue is zero for empty bounds and one for non-empty bounds. Extrema are
/// unspecified for empty bounds. A zero-size bounds containing one point is
/// non-empty.
typedef struct PKBounds3d
{
    PKVector3 vecMin;
    PKVector3 vecMax;
    uint32_t  nHasValue;
} PKBounds3d;

/// Canonical PicoGK signed-distance encoding.
///
/// SDF slice values are signed fixed-point distances in voxel units. The
/// representable narrow band is exactly [-3, +3] voxels:
///
///   -32767  -> -3 voxels (inside background)
///        0  ->  0 voxels (zero isosurface)
///   +32767  -> +3 voxels (outside background)
///
/// INT16_MIN (-32768) is reserved and must never appear in a valid slice.
/// For a Voxels object with voxel size V [mm], decode a non-reserved sample S as
/// S / 32767 * (3 * V) [mm]. Encoding clamps to the same three-voxel band.
enum
{
    PKSDF_HALF_WIDTH_VOXELS  = 3,
    PKSDF_INSIDE_BACKGROUND  = -32767,
    PKSDF_ZERO               = 0,
    PKSDF_OUTSIDE_BACKGROUND = 32767,
    PKSDF_RESERVED           = -32768
};

/// Caller-owned dense 2D signed-distance buffer.
///
/// For extraction, the caller sets pValues and nValueCapacity and the runtime
/// fills nWidth, nHeight and the first nWidth * nHeight values. For import, the
/// caller supplies all four fields. nValueCapacity is measured in int16 samples,
/// not bytes. The buffer is row-major, has no native handle and no native lifetime.
typedef struct PKSdfSlice
{
    int16_t*  pValues;
    uint64_t  nValueCapacity;
    uint32_t  nWidth;
    uint32_t  nHeight;
} PKSdfSlice;

/// Index-space extent of a canonical SDF volume.
///
/// Origin is the minimum voxel coordinate represented by the slice stack. Size
/// is the number of samples along each axis. Z slices are supplied in increasing
/// Z order and use the same orientation as Voxels_GetZSlice().
typedef struct PKSdfVolumeDesc
{
    int32_t  nXOrigin;
    int32_t  nYOrigin;
    int32_t  nZOrigin;
    uint32_t nXSize;
    uint32_t nYSize;
    uint32_t nZSize;
} PKSdfVolumeDesc;

/// Indexed line segment used by bulk tube creation.
typedef struct PKSegment
{
    uint32_t A;
    uint32_t B;
} PKSegment;

/// Direct read-only view into an immutable native Mesh.
///
/// All pointers remain valid until the Mesh or its owning Library instance is
/// destroyed. The caller must not modify or free any referenced memory.
typedef struct PKMeshView
{
    const PKVector3*  pVertices;
    uint32_t          nVertices;

    const PKTriangle* pTriangles;
    uint32_t          nTriangles;

    const PKQuad*     pQuads;
    uint32_t          nQuads;
} PKMeshView;

/// Direct read-only view of the complete Mesh surface as triangles.
///
/// If the Mesh contains quads, requesting this view lazily creates and retains
/// the triangulation cache. The pointer remains valid for the Mesh lifetime.
typedef struct PKTriangulatedMeshView
{
    const PKVector3*  pVertices;
    uint32_t          nVertices;

    const PKTriangle* pTriangles;
    uint32_t          nTriangles;
} PKTriangulatedMeshView;

#ifdef __cplusplus
} // extern "C"

#include <type_traits>

static_assert(std::is_unsigned_v<PKHANDLE>);
static_assert(sizeof(PKHANDLE)   == 8);
static_assert(sizeof(PKVector3)  == 12);
static_assert(sizeof(PKTriangle) == 12);
static_assert(sizeof(PKQuad)     == 16);
static_assert(sizeof(PKBounds3d) == 28);
static_assert(sizeof(PKSegment)  == 8);

static_assert(std::is_standard_layout_v<PKVector3>);
static_assert(std::is_trivially_copyable_v<PKVector3>);
static_assert(std::is_standard_layout_v<PKTriangle>);
static_assert(std::is_trivially_copyable_v<PKTriangle>);
static_assert(std::is_standard_layout_v<PKQuad>);
static_assert(std::is_trivially_copyable_v<PKQuad>);
static_assert(std::is_standard_layout_v<PKBounds3d>);
static_assert(std::is_trivially_copyable_v<PKBounds3d>);
static_assert(std::is_standard_layout_v<PKSdfSlice>);
static_assert(std::is_trivially_copyable_v<PKSdfSlice>);
static_assert(std::is_standard_layout_v<PKSdfVolumeDesc>);
static_assert(std::is_trivially_copyable_v<PKSdfVolumeDesc>);
static_assert(std::is_standard_layout_v<PKSegment>);
static_assert(std::is_trivially_copyable_v<PKSegment>);
#endif

#endif // PICOGK_API_TYPES_H_
