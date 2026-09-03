// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Text;
using PicoGK.Geometry;

namespace PicoGK;

/// <summary>
/// Small managed policy layer around the raw C ABI.
/// Native error retrieval is performed only after a failed call/zero handle.
/// </summary>
internal static class NativeApi
{
    internal const int nInfoStringLength = 255;

    internal static void Check(bool bSuccess)
    {
        if (!bSuccess)
            throw new PicoGKException(strLastError());
    }

    internal static ulong hCheck(ulong hNative)
    {
        if (hNative == 0)
            throw new PicoGKException(strLastError());
        return hNative;
    }

    internal static SafeLibraryHandle hCreateLibrary(float fVoxelSizeMM)
    {
        return SafeLibraryHandle.oFromNative(
            hCheck(NativeMethods.Library_hCreateInstance(fVoxelSizeMM)));
    }

    internal static SafeMeshHandle hMesh(SafeLibraryHandle hLibrary, ulong hNative)
    {
        return SafeMeshHandle.oFromNative(hLibrary, hCheck(hNative));
    }

    internal static SafeVoxelsHandle hVoxels(SafeLibraryHandle hLibrary, ulong hNative)
    {
        return SafeVoxelsHandle.oFromNative(hLibrary, hCheck(hNative));
    }

    internal static unsafe SafeMeshHandle hCreateMesh(
        SafeLibraryHandle hLibrary,
        ReadOnlySpan<Vector3> avecVertices,
        ReadOnlySpan<Triangle> atriTriangles,
        ReadOnlySpan<Quad> aquadQuads)
    {
        fixed (Vector3* pVertices = avecVertices)
        fixed (Triangle* pTriangles = atriTriangles)
        fixed (Quad* pQuads = aquadQuads)
        {
            return hMesh(hLibrary, NativeMethods.Mesh_hCreateFromBuffers(
                hLibrary,
                pVertices, (uint)avecVertices.Length,
                pTriangles, (uint)atriTriangles.Length,
                pQuads, (uint)aquadQuads.Length));
        }
    }


    internal static unsafe SafeVoxelsHandle hCreateSpheres(
        SafeLibraryHandle hLibrary,
        ReadOnlySpan<Vector3> avecCenters,
        float fRadiusMM)
    {
        fixed (Vector3* pCenters = avecCenters)
        {
            return hVoxels(hLibrary, NativeMethods.Voxels_hCreateSpheres(
                hLibrary, pCenters, (uint)avecCenters.Length, fRadiusMM));
        }
    }

    internal static unsafe SafeVoxelsHandle hCreateSpheres(
        SafeLibraryHandle hLibrary,
        ReadOnlySpan<Vector3> avecCenters,
        ReadOnlySpan<float> afRadiiMM)
    {
        if (avecCenters.Length != afRadiiMM.Length)
            throw new ArgumentException("Sphere center and radius arrays must have the same length.");

        fixed (Vector3* pCenters = avecCenters)
        fixed (float* pRadiiMM = afRadiiMM)
        {
            return hVoxels(hLibrary, NativeMethods.Voxels_hCreateVariableSpheres(
                hLibrary, pCenters, pRadiiMM, (uint)avecCenters.Length));
        }
    }

    internal static unsafe SafeVoxelsHandle hCreateTubes(
        SafeLibraryHandle hLibrary,
        ReadOnlySpan<Vector3> avecVertices,
        ReadOnlySpan<Segment> aSegments,
        float fRadiusMM)
    {
        fixed (Vector3* pVertices = avecVertices)
        fixed (Segment* pSegments = aSegments)
        {
            return hVoxels(hLibrary, NativeMethods.Voxels_hCreateTubes(
                hLibrary,
                pVertices, (uint)avecVertices.Length,
                pSegments, (uint)aSegments.Length,
                fRadiusMM));
        }
    }

    internal static unsafe SafeVoxelsHandle hCreateTubes(
        SafeLibraryHandle hLibrary,
        ReadOnlySpan<Vector3> avecVertices,
        ReadOnlySpan<Segment> aSegments,
        ReadOnlySpan<float> afVertexRadiiMM)
    {
        if (avecVertices.Length != afVertexRadiiMM.Length)
            throw new ArgumentException("Tube vertex and radius arrays must have the same length.");

        fixed (Vector3* pVertices = avecVertices)
        fixed (Segment* pSegments = aSegments)
        fixed (float* pRadiiMM = afVertexRadiiMM)
        {
            return hVoxels(hLibrary, NativeMethods.Voxels_hCreateVariableTubes(
                hLibrary,
                pVertices, pRadiiMM, (uint)avecVertices.Length,
                pSegments, (uint)aSegments.Length));
        }
    }

    internal static unsafe void GetSdfSlice(
        SafeLibraryHandle hLibrary,
        SafeVoxelsHandle hVoxels,
        int nSlice,
        SdfSlice oSliceBuffer,
        Voxels.ESliceAxis eAxis)
    {
        fixed (short* pValues = oSliceBuffer.anValues)
        {
            NativeSdfSlice oSlice = new()
            {
                pValues        = pValues,
                nValueCapacity = (ulong)oSliceBuffer.nCapacity
            };

            bool bSuccess = eAxis switch
            {
                Voxels.ESliceAxis.X => NativeMethods.Voxels_bGetXSlice(
                    hLibrary, hVoxels, nSlice, ref oSlice),
                Voxels.ESliceAxis.Y => NativeMethods.Voxels_bGetYSlice(
                    hLibrary, hVoxels, nSlice, ref oSlice),
                _ => NativeMethods.Voxels_bGetZSlice(
                    hLibrary, hVoxels, nSlice, ref oSlice)
            };

            Check(bSuccess);
            oSliceBuffer.SetDimensions(checked((int)oSlice.nWidth), checked((int)oSlice.nHeight));
        }
    }

    internal static unsafe void GetInterpolatedZSdfSlice(
        SafeLibraryHandle hLibrary,
        SafeVoxelsHandle hVoxels,
        float fZSlice,
        SdfSlice oSliceBuffer)
    {
        fixed (short* pValues = oSliceBuffer.anValues)
        {
            NativeSdfSlice oSlice = new()
            {
                pValues        = pValues,
                nValueCapacity = (ulong)oSliceBuffer.nCapacity
            };

            Check(NativeMethods.Voxels_bGetInterpolatedZSlice(
                hLibrary, hVoxels, fZSlice, ref oSlice));
            oSliceBuffer.SetDimensions(checked((int)oSlice.nWidth), checked((int)oSlice.nHeight));
        }
    }

    internal static SafeVoxelsHandle hBeginSdfImport(
        SafeLibraryHandle hLibrary,
        in NativeSdfVolumeDesc oVolume)
    {
        return hVoxels(
            hLibrary,
            NativeMethods.Voxels_hBeginSdfImport(hLibrary, in oVolume));
    }

    internal static unsafe void ImportSdfZSlice(
        SafeLibraryHandle hLibrary,
        SafeVoxelsHandle hVoxels,
        int nZSlice,
        SdfSlice oSlice)
    {
        fixed (short* pValues = oSlice.anValues)
        {
            NativeSdfSlice oNativeSlice = new()
            {
                pValues = pValues,
                nValueCapacity = (ulong)oSlice.nCapacity,
                nWidth = checked((uint)oSlice.nWidth),
                nHeight = checked((uint)oSlice.nHeight)
            };

            Check(NativeMethods.Voxels_bImportSdfZSlice(
                hLibrary,
                hVoxels,
                checked((uint)nZSlice),
                in oNativeSlice));
        }
    }

    internal static void EndSdfImport(
        SafeLibraryHandle hLibrary,
        SafeVoxelsHandle hVoxels)
    {
        Check(NativeMethods.Voxels_bEndSdfImport(hLibrary, hVoxels));
    }

    internal static string strLastError()
    {
        StringBuilder oSb = new(nInfoStringLength);
        return NativeMethods.Library_bGetLastError(oSb, oSb.Capacity)
            ? oSb.ToString()
            : "Unknown PicoGK native error.";
    }

    /// <summary>
    /// Reads a SafeHandle value while holding a temporary SafeHandle reference.
    /// Used only when a child SafeHandle captures the parent Library identity.
    /// </summary>
    internal static ulong hGetNative(PicoGKSafeHandle hSafe)
    {
        bool bAddedRef = false;
        try
        {
            hSafe.DangerousAddRef(ref bAddedRef);
            if (hSafe.IsInvalid || hSafe.IsClosed)
                throw new ObjectDisposedException(nameof(hSafe));
            return hSafe.hNative;
        }
        finally
        {
            if (bAddedRef)
                hSafe.DangerousRelease();
        }
    }
}
