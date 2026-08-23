// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Runtime.InteropServices;

namespace PicoGK;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeBBox3
{
    internal Vector3 vecMin;
    internal Vector3 vecMax;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeSdfSlice
{
    internal short* pValues;
    internal ulong  nValueCapacity;
    internal uint   nWidth;
    internal uint   nHeight;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSdfVolumeDesc
{
    internal int  nXOrigin;
    internal int  nYOrigin;
    internal int  nZOrigin;
    internal uint nXSize;
    internal uint nYSize;
    internal uint nZSize;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMeshView
{
    internal Vector3*  pVertices;
    internal uint      nVertices;

    internal Triangle* pTriangles;
    internal uint      nTriangles;

    internal Quad*     pQuads;
    internal uint      nQuads;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeTriangulatedMeshView
{
    internal Vector3*  pVertices;
    internal uint      nVertices;

    internal Triangle* pTriangles;
    internal uint      nTriangles;
}
