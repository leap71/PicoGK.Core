// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace PicoGK;

/// <summary>
/// Raw P/Invoke declarations for the PicoGK.Core C ABI.
/// Do not call these from public API code without applying NativeApi.Check() or
/// NativeApi.hCheck() to the result as appropriate.
/// </summary>
internal static unsafe class NativeMethods
{
    internal const string strLibrary = "picogk";

    // Runtime / Library -------------------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetName", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetName(StringBuilder psz);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetVersion", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetVersion(StringBuilder psz);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetBuildInfo", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetBuildInfo(StringBuilder psz);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetLastError", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetLastError(StringBuilder psz, int nMaxStringLen);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_hCreateInstance")]
    internal static extern ulong Library_hCreateInstance(float fVoxelSizeMM);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bDestroyInstance")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bDestroyInstanceRaw(ulong hThis);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetIsValid")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetIsValid(
        SafeLibraryHandle hThis,
        [MarshalAs(UnmanagedType.I1)] out bool bValid);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetTotalMemUsage")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetTotalMemUsage(SafeLibraryHandle hThis, out long nBytes);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetMeshesMemUsage")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetMeshesMemUsage(SafeLibraryHandle hThis, out long nBytes);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetVoxelsMemUsage")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetVoxelsMemUsage(SafeLibraryHandle hThis, out long nBytes);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetMeshesAllocated")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetMeshesAllocated(SafeLibraryHandle hThis, out long nCount);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bGetVoxelsAllocated")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bGetVoxelsAllocated(SafeLibraryHandle hThis, out long nCount);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bVoxelsToMm")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bVoxelsToMm(SafeLibraryHandle hThis, in Vector3 vecVoxelCoordinate, out Vector3 vecMmCoordinate);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Library_bMmToVoxels")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Library_bMmToVoxels(SafeLibraryHandle hThis, in Vector3 vecMmCoordinate, out Vector3 vecVoxelCoordinate);

    // Mesh --------------------------------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_hCreateFromBuffers")]
    internal static extern ulong Mesh_hCreateFromBuffers(
        SafeLibraryHandle hInstance,
        Vector3* pVertices,
        uint nVertices,
        Triangle* pTriangles,
        uint nTriangles,
        Quad* pQuads,
        uint nQuads);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_hCreateFromVoxels")]
    internal static extern ulong Mesh_hCreateFromVoxels(SafeLibraryHandle hInstance, SafeVoxelsHandle hVoxels);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bGetIsValid")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bGetIsValid(
        SafeLibraryHandle hInstance,
        SafeMeshHandle hThis,
        [MarshalAs(UnmanagedType.I1)] out bool bValid);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bDestroy")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bDestroyRaw(ulong hInstance, ulong hThis);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bGetMemUsage")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bGetMemUsage(SafeLibraryHandle hInstance, SafeMeshHandle hThis, out long nBytes);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bGetBounds")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bGetBounds(SafeLibraryHandle hInstance, SafeMeshHandle hThis, out NativeBounds3d oBounds);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bGetView")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bGetView(SafeLibraryHandle hInstance, SafeMeshHandle hThis, out NativeMeshView oView);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mesh_bGetTriangulatedView")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Mesh_bGetTriangulatedView(
        SafeLibraryHandle hInstance, SafeMeshHandle hThis, out NativeTriangulatedMeshView oView);

    // Voxels: creation and lifetime -------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreate")]
    internal static extern ulong Voxels_hCreate(SafeLibraryHandle hInstance);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateCopy")]
    internal static extern ulong Voxels_hCreateCopy(SafeLibraryHandle hInstance, SafeVoxelsHandle hSource);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateSphere")]
    internal static extern ulong Voxels_hCreateSphere(SafeLibraryHandle hInstance, in Vector3 vecCenter, float fRadius);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateCapsule")]
    internal static extern ulong Voxels_hCreateCapsule(
        SafeLibraryHandle hInstance,
        in Vector3 vecStart,
        in Vector3 vecStop,
        float fRadius1,
        float fRadius2);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateSpheres")]
    internal static extern ulong Voxels_hCreateSpheres(
        SafeLibraryHandle hInstance,
        Vector3* pCenters,
        uint nCenterCount,
        float fRadiusMM);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateVariableSpheres")]
    internal static extern ulong Voxels_hCreateVariableSpheres(
        SafeLibraryHandle hInstance,
        Vector3* pCenters,
        float* pRadiiMM,
        uint nCenterCount);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateTubes")]
    internal static extern ulong Voxels_hCreateTubes(
        SafeLibraryHandle hInstance,
        Vector3* pVertices,
        uint nVertexCount,
        Segment* pSegments,
        uint nSegmentCount,
        float fRadiusMM);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateVariableTubes")]
    internal static extern ulong Voxels_hCreateVariableTubes(
        SafeLibraryHandle hInstance,
        Vector3* pVertices,
        float* pVertexRadiiMM,
        uint nVertexCount,
        Segment* pSegments,
        uint nSegmentCount);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hBeginSdfImport")]
    internal static extern ulong Voxels_hBeginSdfImport(SafeLibraryHandle hInstance, in NativeSdfVolumeDesc oVolume);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bImportSdfZSlice")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bImportSdfZSlice(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        uint nZSlice,
        in NativeSdfSlice oSlice);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bEndSdfImport")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bEndSdfImport(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateFromMesh")]
    internal static extern ulong Voxels_hCreateFromMesh(SafeLibraryHandle hInstance, SafeMeshHandle hMesh);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_hCreateMeshShell")]
    internal static extern ulong Voxels_hCreateMeshShell(SafeLibraryHandle hInstance, SafeMeshHandle hMesh, float fRadius);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetIsValid")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetIsValid(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        [MarshalAs(UnmanagedType.I1)] out bool bValid);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bDestroy")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bDestroyRaw(ulong hInstance, ulong hThis);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bDiagnose", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bDiagnose(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        [MarshalAs(UnmanagedType.I1)] out bool bHealthy,
        StringBuilder psz);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetIsEmpty")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetIsEmpty(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        [MarshalAs(UnmanagedType.I1)] out bool bEmpty);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetMemUsage")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetMemUsage(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, out long nBytes);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetVoxelSize")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetVoxelSize(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, out float fVoxelSizeMM);

    // Voxels: modification ----------------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bBoolAdd")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bBoolAdd(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, SafeVoxelsHandle hOther);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bBoolSubtract")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bBoolSubtract(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, SafeVoxelsHandle hOther);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bBoolIntersect")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bBoolIntersect(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, SafeVoxelsHandle hOther);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bOffset")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bOffset(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, float fDist);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bDoubleOffset")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bDoubleOffset(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, float fDist1, float fDist2);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bTripleOffset")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bTripleOffset(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, float fDist);

    // Voxels: queries ---------------------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetIsInside")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetIsInside(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        in Vector3 vecTestPoint,
        [MarshalAs(UnmanagedType.I1)] out bool bInside);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetVolume")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetVolume(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, out float fVolumeMM3);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetSurfaceNormal")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetSurfaceNormal(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        in Vector3 vecSurfacePoint,
        out Vector3 vecNormal);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bClosestPointOnSurface")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bClosestPointOnSurface(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        in Vector3 vecSearch,
        [MarshalAs(UnmanagedType.I1)] out bool bFound,
        out Vector3 vecSurfacePoint,
        out float fDistanceMM);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bRayCastToSurface")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bRayCastToSurface(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        in Vector3 vecSearch,
        in Vector3 vecDirection,
        [MarshalAs(UnmanagedType.I1)] out bool bHit,
        out Vector3 vecSurfacePoint);

    // Voxels: canonical SDF access -------------------------------------------

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetVoxelDimensions")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetVoxelDimensions(
        SafeLibraryHandle hInstance,
        SafeVoxelsHandle hThis,
        out int nXOrigin,
        out int nYOrigin,
        out int nZOrigin,
        out int nXSize,
        out int nYSize,
        out int nZSize);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetXSlice")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetXSlice(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, int nXSlice, ref NativeSdfSlice oSlice);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetYSlice")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetYSlice(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, int nYSlice, ref NativeSdfSlice oSlice);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetZSlice")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetZSlice(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, int nZSlice, ref NativeSdfSlice oSlice);

    [DllImport(strLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Voxels_bGetInterpolatedZSlice")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Voxels_bGetInterpolatedZSlice(SafeLibraryHandle hInstance, SafeVoxelsHandle hThis, float fZSlice, ref NativeSdfSlice oSlice);
}
