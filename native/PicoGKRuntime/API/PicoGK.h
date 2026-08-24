// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_H_
#define PICOGK_H_

#include "PicoGKApiTypes.h"

#ifdef __cplusplus
    #define PICOGK_EXTC extern "C"
#else
    #include <stdbool.h>
    #define PICOGK_EXTC
#endif

#if defined(PICOGK_BUILD_LIBRARY)
    #if defined(_WIN32)
        #define PICOGK_API PICOGK_EXTC __declspec(dllexport)
    #else
        #define PICOGK_API PICOGK_EXTC __attribute__((visibility("default")))
    #endif
#else
    #if defined(_WIN32)
        #define PICOGK_API PICOGK_EXTC __declspec(dllimport)
    #else
        #define PICOGK_API PICOGK_EXTC
    #endif
#endif

#define PKINFOSTRINGLEN 255

/// @file
/// PicoGK.Core native C API.
///
/// PicoGK uses millimetres for world-space coordinates and distances. Voxels
/// are sparse signed-distance level sets with negative values inside the solid
/// and a fixed narrow-band half-width of three voxels.
///
/// ABI result convention:
/// - Functions that create native objects return a handle. Zero means failure.
/// - All other normal API functions return true when the call completed
///   successfully and false when an API/native error occurred. Their actual
///   result values are written through output parameters.
/// - Library_bGetLastError() is the sole diagnostic exception to this rule. It
///   returns true when a stored error message was available.
///
/// A failed normal call stores a thread-local diagnostic message which can be
/// retrieved with Library_bGetLastError(). Successful calls do not require a
/// second native transition for error checking.

// Runtime / Library -----------------------------------------------------------

/// Returns the native runtime name.
PICOGK_API bool Library_bGetName(char psz[PKINFOSTRINGLEN]);

/// Returns the native runtime semantic version, for example "3.0.0".
PICOGK_API bool Library_bGetVersion(char psz[PKINFOSTRINGLEN]);

/// Returns a human-readable native runtime build description.
PICOGK_API bool Library_bGetBuildInfo(char psz[PKINFOSTRINGLEN]);

/// Copies the current thread's last native API error into psz.
///
/// Unlike every normal API function, this call does not clear the stored error.
/// It returns true if an error message was present and copied successfully.
PICOGK_API bool Library_bGetLastError(char* psz, int32_t nMaxStringLen);

/// Creates a Library instance with a fixed voxel size in millimetres.
///
/// Mesh and Voxels objects created through this instance are owned by it and are
/// destroyed when the instance is destroyed. Returns zero on failure.
PICOGK_API PKINSTANCE Library_hCreateInstance(float fVoxelSizeMM);

/// Destroys a Library instance and all native objects owned by it.
///
/// Destruction is idempotent; an invalid or already-destroyed handle is a
/// successful no-op.
PICOGK_API bool Library_bDestroyInstance(PKINSTANCE hThis);

/// Reports whether hThis identifies a currently live Library instance.
PICOGK_API bool Library_bGetIsValid(PKINSTANCE hThis, bool* pbValid);

/// Returns estimated native memory owned by this Library instance [bytes].
PICOGK_API bool Library_bGetTotalMemUsage(PKINSTANCE hThis, int64_t* pnBytes);

/// Returns estimated native Mesh memory owned by this Library instance [bytes].
PICOGK_API bool Library_bGetMeshesMemUsage(PKINSTANCE hThis, int64_t* pnBytes);

/// Returns estimated native Voxels memory owned by this Library instance [bytes].
PICOGK_API bool Library_bGetVoxelsMemUsage(PKINSTANCE hThis, int64_t* pnBytes);

/// Returns the number of live Mesh objects owned by this Library instance.
PICOGK_API bool Library_bGetMeshesAllocated(PKINSTANCE hThis, int64_t* pnCount);

/// Returns the number of live Voxels objects owned by this Library instance.
PICOGK_API bool Library_bGetVoxelsAllocated(PKINSTANCE hThis, int64_t* pnCount);

/// Converts voxel coordinates to world coordinates [mm].
PICOGK_API bool Library_bVoxelsToMm(PKINSTANCE hThis,
                                    const PKVector3* pvecVoxelCoordinate,
                                    PKVector3* pvecMmCoordinate);

/// Converts world coordinates [mm] to the nearest integer voxel coordinates.
PICOGK_API bool Library_bMmToVoxels(PKINSTANCE hThis,
                                    const PKVector3* pvecMmCoordinate,
                                    PKVector3* pvecVoxelCoordinate);


// Mesh ------------------------------------------------------------------------

/// Creates an immutable Mesh by copying completed caller-owned buffers once.
///
/// Triangles and quads are preserved separately. Returns zero on failure.
PICOGK_API PKMESH Mesh_hCreateFromBuffers(PKINSTANCE hInstance,
                                           const PKVector3* pVertices,
                                           uint32_t nVertices,
                                           const PKTriangle* pTriangles,
                                           uint32_t nTriangles,
                                           const PKQuad* pQuads,
                                           uint32_t nQuads);

/// Extracts the zero isosurface of a Voxels object as an immutable Mesh.
PICOGK_API PKMESH Mesh_hCreateFromVoxels(PKINSTANCE hInstance,
                                          PKVOXELS hVoxels);

/// Reports whether hThis identifies a live Mesh owned by hInstance.
PICOGK_API bool Mesh_bGetIsValid(PKINSTANCE hInstance,
                                 PKMESH hThis,
                                 bool* pbValid);

/// Destroys a Mesh. Invalid/already-destroyed handles are successful no-ops.
PICOGK_API bool Mesh_bDestroy(PKINSTANCE hInstance, PKMESH hThis);

/// Returns estimated native memory owned by this Mesh [bytes].
PICOGK_API bool Mesh_bGetMemUsage(PKINSTANCE hInstance,
                                  PKMESH hThis,
                                  int64_t* pnBytes);

/// Returns the Mesh axis-aligned bounding box in world coordinates [mm].
PICOGK_API bool Mesh_bGetBoundingBox(PKINSTANCE hInstance,
                                     PKMESH hThis,
                                     PKBBox3* poBox);

/// Returns direct read-only views into immutable Mesh storage.
///
/// The returned pointers remain valid until the Mesh or its owning Library
/// instance is destroyed.
PICOGK_API bool Mesh_bGetView(PKINSTANCE hInstance,
                              PKMESH hThis,
                              PKMeshView* poView);

/// Returns the complete Mesh surface as a read-only triangle view.
///
/// If the Mesh contains quads, this call lazily builds the triangulation cache.
PICOGK_API bool Mesh_bGetTriangulatedView(PKINSTANCE hInstance,
                                          PKMESH hThis,
                                          PKTriangulatedMeshView* poView);


// Voxels: creation and lifetime -----------------------------------------------

/// Creates an empty Voxels level set using the Library instance voxel size.
PICOGK_API PKVOXELS Voxels_hCreate(PKINSTANCE hInstance);

/// Creates a deep copy of an existing Voxels object.
PICOGK_API PKVOXELS Voxels_hCreateCopy(PKINSTANCE hInstance,
                                        PKVOXELS hSource);

/// Creates a spherical solid in world coordinates [mm].
PICOGK_API PKVOXELS Voxels_hCreateSphere(PKINSTANCE hInstance,
                                          const PKVector3* pvecCenter,
                                          float fRadius);

/// Creates a tapered capsule between two world-space points [mm].
PICOGK_API PKVOXELS Voxels_hCreateCapsule(PKINSTANCE hInstance,
                                           const PKVector3* pvecStart,
                                           const PKVector3* pvecStop,
                                           float fRadius1,
                                           float fRadius2);

/// Creates the CSG union of equal-radius spheres [mm].
PICOGK_API PKVOXELS Voxels_hCreateSpheres(PKINSTANCE hInstance,
                                           const PKVector3* pCenters,
                                           uint32_t nCenterCount,
                                           float fRadiusMM);

/// Creates the CSG union of variable-radius spheres [mm].
PICOGK_API PKVOXELS Voxels_hCreateVariableSpheres(PKINSTANCE hInstance,
                                                   const PKVector3* pCenters,
                                                   const float* pRadiiMM,
                                                   uint32_t nCenterCount);

/// Creates a tube complex with one common radius [mm].
PICOGK_API PKVOXELS Voxels_hCreateTubes(PKINSTANCE hInstance,
                                        const PKVector3* pVertices,
                                        uint32_t nVertexCount,
                                        const PKSegment* pSegments,
                                        uint32_t nSegmentCount,
                                        float fRadiusMM);

/// Creates a tube complex with a radius stored at every vertex [mm].
PICOGK_API PKVOXELS Voxels_hCreateVariableTubes(PKINSTANCE hInstance,
                                                 const PKVector3* pVertices,
                                                 const float* pVertexRadiiMM,
                                                 uint32_t nVertexCount,
                                                 const PKSegment* pSegments,
                                                 uint32_t nSegmentCount);

/// Begins streaming reconstruction of a Voxels object from canonical Z slices.
///
/// The returned handle represents an import-in-progress object. Returns zero on
/// failure. Destroying the handle safely abandons an incomplete import.
PICOGK_API PKVOXELS Voxels_hBeginSdfImport(PKINSTANCE hInstance,
                                            const PKSdfVolumeDesc* pVolume);

/// Supplies the next canonical Z slice to a streaming SDF import.
PICOGK_API bool Voxels_bImportSdfZSlice(PKINSTANCE hInstance,
                                        PKVOXELS hThis,
                                        uint32_t nZSlice,
                                        const PKSdfSlice* pSlice);

/// Completes a streaming SDF import and makes the reconstructed object available.
PICOGK_API bool Voxels_bEndSdfImport(PKINSTANCE hInstance, PKVOXELS hThis);

/// Converts an immutable Mesh into a solid Voxels level set.
PICOGK_API PKVOXELS Voxels_hCreateFromMesh(PKINSTANCE hInstance,
                                            PKMESH hMesh);

/// Creates a shell by dilating a Mesh surface by fRadius [mm].
PICOGK_API PKVOXELS Voxels_hCreateMeshShell(PKINSTANCE hInstance,
                                             PKMESH hMesh,
                                             float fRadius);

/// Reports whether hThis identifies live Voxels owned by hInstance.
PICOGK_API bool Voxels_bGetIsValid(PKINSTANCE hInstance,
                                   PKVOXELS hThis,
                                   bool* pbValid);

/// Destroys a Voxels object. Invalid/already-destroyed handles are successful no-ops.
PICOGK_API bool Voxels_bDestroy(PKINSTANCE hInstance, PKVOXELS hThis);

/// Runs OpenVDB level-set diagnostics.
///
/// pbHealthy receives true when no diagnostic message is produced. psz receives
/// the diagnostic description when pbHealthy is false.
PICOGK_API bool Voxels_bDiagnose(PKINSTANCE hInstance,
                                 PKVOXELS hThis,
                                 bool* pbHealthy,
                                 char psz[PKINFOSTRINGLEN]);

/// Reports whether the level set contains no interior (negative-SDF) samples.
PICOGK_API bool Voxels_bGetIsEmpty(PKINSTANCE hInstance,
                                   PKVOXELS hThis,
                                   bool* pbEmpty);

/// Returns estimated native memory owned by this Voxels object [bytes].
PICOGK_API bool Voxels_bGetMemUsage(PKINSTANCE hInstance,
                                    PKVOXELS hThis,
                                    int64_t* pnBytes);

/// Returns the voxel edge length in millimetres.
PICOGK_API bool Voxels_bGetVoxelSize(PKINSTANCE hInstance,
                                     PKVOXELS hThis,
                                     float* pfVoxelSizeMM);


// Voxels: modification --------------------------------------------------------

/// Replaces hThis with the union of hThis and hOther.
PICOGK_API bool Voxels_bBoolAdd(PKINSTANCE hInstance,
                                PKVOXELS hThis,
                                PKVOXELS hOther);

/// Subtracts hOther from hThis.
PICOGK_API bool Voxels_bBoolSubtract(PKINSTANCE hInstance,
                                     PKVOXELS hThis,
                                     PKVOXELS hOther);

/// Replaces hThis with the intersection of hThis and hOther.
PICOGK_API bool Voxels_bBoolIntersect(PKINSTANCE hInstance,
                                      PKVOXELS hThis,
                                      PKVOXELS hOther);

/// Offsets the surface by fDist [mm]. Positive values grow the solid.
PICOGK_API bool Voxels_bOffset(PKINSTANCE hInstance,
                               PKVOXELS hThis,
                               float fDist);

/// Applies two sequential offsets [mm]. Positive values grow the solid.
PICOGK_API bool Voxels_bDoubleOffset(PKINSTANCE hInstance,
                                     PKVOXELS hThis,
                                     float fDist1,
                                     float fDist2);

/// Applies +fDist, -2*fDist, +fDist sequentially [mm].
PICOGK_API bool Voxels_bTripleOffset(PKINSTANCE hInstance,
                                     PKVOXELS hThis,
                                     float fDist);


// Voxels: queries -------------------------------------------------------------

/// Reports whether the interpolated signed distance at pvecTestPoint is negative.
/// A point exactly on the zero isosurface is not considered inside.
PICOGK_API bool Voxels_bGetIsInside(PKINSTANCE hInstance,
                                    PKVOXELS hThis,
                                    const PKVector3* pvecTestPoint,
                                    bool* pbInside);

/// Calculates enclosed volume [mm^3].
PICOGK_API bool Voxels_bGetVolume(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  float* pfVolumeMM3);

/// Evaluates the normalized SDF gradient at pvecSurfacePoint.
PICOGK_API bool Voxels_bGetSurfaceNormal(PKINSTANCE hInstance,
                                         PKVOXELS hThis,
                                         const PKVector3* pvecSurfacePoint,
                                         PKVector3* pvecNormal);

/// Finds the closest point on the zero isosurface to pvecSearch.
///
/// pbFound carries the geometric result. A successful call with pbFound=false is
/// not an API error. Distance is unsigned Euclidean distance [mm].
PICOGK_API bool Voxels_bClosestPointOnSurface(PKINSTANCE hInstance,
                                              PKVOXELS hThis,
                                              const PKVector3* pvecSearch,
                                              bool* pbFound,
                                              PKVector3* pvecSurfacePoint,
                                              float* pfDistanceMM);

/// Casts a world-space ray and returns its first intersection with the surface.
///
/// pbHit carries the geometric result. A successful call with pbHit=false is not
/// an API error. pvecDirection may have any non-zero magnitude.
PICOGK_API bool Voxels_bRayCastToSurface(PKINSTANCE hInstance,
                                         PKVOXELS hThis,
                                         const PKVector3* pvecSearch,
                                         const PKVector3* pvecDirection,
                                         bool* pbHit,
                                         PKVector3* pvecSurfacePoint);


// Voxels: canonical SDF access -----------------------------------------------

/// Returns the active voxel bounding-box origin and dimensions in index space.
PICOGK_API bool Voxels_bGetVoxelDimensions(PKINSTANCE hInstance,
                                           PKVOXELS hThis,
                                           int32_t* pnXOrigin,
                                           int32_t* pnYOrigin,
                                           int32_t* pnZOrigin,
                                           int32_t* pnXSize,
                                           int32_t* pnYSize,
                                           int32_t* pnZSize);

/// Extracts an integer X slice into a caller-owned canonical int16 SDF buffer.
PICOGK_API bool Voxels_bGetXSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nXSlice,
                                  PKSdfSlice* pSlice);

/// Extracts an integer Y slice into a caller-owned canonical int16 SDF buffer.
PICOGK_API bool Voxels_bGetYSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nYSlice,
                                  PKSdfSlice* pSlice);

/// Extracts an integer Z slice into a caller-owned canonical int16 SDF buffer.
PICOGK_API bool Voxels_bGetZSlice(PKINSTANCE hInstance,
                                  PKVOXELS hThis,
                                  int32_t nZSlice,
                                  PKSdfSlice* pSlice);

/// Extracts a linearly interpolated Z slice into a canonical int16 SDF buffer.
PICOGK_API bool Voxels_bGetInterpolatedZSlice(PKINSTANCE hInstance,
                                              PKVOXELS hThis,
                                              float fZSlice,
                                              PKSdfSlice* pSlice);

#endif // PICOGK_H_
