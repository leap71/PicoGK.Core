// SPDX-License-Identifier: Apache-2.0
using System.Numerics;

namespace PicoGK;

/// <summary>
/// Immutable polygon mesh preserving triangles and quads.
/// </summary>
public sealed class Mesh : IDisposable
{
    internal readonly Library lib;
    internal readonly SafeMeshHandle hNative;

    internal Mesh(Library lib, SafeMeshHandle hNative)
    {
        ArgumentNullException.ThrowIfNull(lib);
        this.lib     = lib;
        this.hNative = hNative;
    }

    /// <summary>
    /// Creates an immutable Mesh by copying completed vertex, triangle and quad
    /// buffers once into native storage.
    /// </summary>
    public static Mesh mshFromBuffers(Library lib,
                                      ReadOnlySpan<Vector3> avecVertices,
                                      ReadOnlySpan<Triangle> atriTriangles,
                                      ReadOnlySpan<Quad> aquadQuads = default)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return new Mesh(lib, NativeApi.hCreateMesh(lib.hNative, avecVertices, atriTriangles, aquadQuads));
    }

    /// <summary>Returns the number of vertices in the original Mesh.</summary>
    public int nVertexCount() => oView().avecVertices.Length;

    /// <summary>Returns the number of original triangles in the Mesh.</summary>
    public int nTriangleCount() => oView().atriTriangles.Length;

    /// <summary>Returns the number of original quads in the Mesh.</summary>
    public int nQuadCount() => oView().aquadQuads.Length;

    /// <summary>Returns the triangle count after lazily triangulating all quads.</summary>
    public int nTriangulatedTriangleCount() => oTriangulatedView().atriTriangles.Length;

    /// <summary>Returns estimated native memory owned by this Mesh, in bytes.</summary>
    public long nMemUsage()
    {
        NativeApi.Check(NativeMethods.Mesh_bGetMemUsage(lib.hNative, hNative, out long nBytes));
        return nBytes;
    }

    /// <summary>Returns the immutable world-space bounding box in millimetres.</summary>
    public BBox3 oBoundingBox()
    {
        NativeApi.Check(NativeMethods.Mesh_bGetBoundingBox(lib.hNative, hNative, out NativeBBox3 oBox));
        return new BBox3(oBox.vecMin, oBox.vecMax);
    }

    /// <summary>
    /// Returns zero-copy views of the original immutable vertex, triangle and
    /// quad buffers. This does not build the triangulation cache.
    /// </summary>
    public MeshView oView()
    {
        NativeApi.Check(NativeMethods.Mesh_bGetView(lib.hNative, hNative, out NativeMeshView oView));
        return new MeshView(this, oView);
    }

    /// <summary>
    /// Returns a zero-copy view of the complete surface as triangles.
    /// If this Mesh contains quads, the triangulation cache is built lazily on
    /// the first call.
    /// </summary>
    public TriangulatedMeshView oTriangulatedView()
    {
        NativeApi.Check(NativeMethods.Mesh_bGetTriangulatedView(
            lib.hNative, hNative, out NativeTriangulatedMeshView oView));
        return new TriangulatedMeshView(this, oView);
    }

    public void Dispose() => hNative.Dispose();
}
