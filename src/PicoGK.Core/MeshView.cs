// SPDX-License-Identifier: Apache-2.0
using System.Numerics;

namespace PicoGK;

/// <summary>
/// Zero-copy read-only view into an immutable Mesh.
///
/// The view keeps its owning Mesh reachable for the lifetime of the ref struct.
/// Do not dispose the owning Mesh or Library while using the view.
/// </summary>
public readonly ref struct MeshView
{
    // This reference is intentionally retained even though the view data itself
    // lives in native memory. It prevents normal GC/finalization from releasing
    // that memory while a zero-copy span is in use.
    readonly Mesh m_oOwner;

    public readonly ReadOnlySpan<Vector3>  avecVertices;
    public readonly ReadOnlySpan<Triangle> atriTriangles;
    public readonly ReadOnlySpan<Quad>     aquadQuads;

    internal unsafe MeshView(Mesh oOwner, NativeMeshView oView)
    {
        m_oOwner       = oOwner;
        avecVertices   = new ReadOnlySpan<Vector3>(oView.pVertices, checked((int)oView.nVertices));
        atriTriangles  = new ReadOnlySpan<Triangle>(oView.pTriangles, checked((int)oView.nTriangles));
        aquadQuads     = new ReadOnlySpan<Quad>(oView.pQuads, checked((int)oView.nQuads));
    }
}

/// <summary>
/// Zero-copy read-only triangulated view of a Mesh.
///
/// Requesting this view lazily creates the native triangulation cache if the
/// Mesh contains quads.
/// </summary>
public readonly ref struct TriangulatedMeshView
{
    readonly Mesh m_oOwner;

    public readonly ReadOnlySpan<Vector3>  avecVertices;
    public readonly ReadOnlySpan<Triangle> atriTriangles;

    internal unsafe TriangulatedMeshView(Mesh oOwner, NativeTriangulatedMeshView oView)
    {
        m_oOwner      = oOwner;
        avecVertices  = new ReadOnlySpan<Vector3>(oView.pVertices, checked((int)oView.nVertices));
        atriTriangles = new ReadOnlySpan<Triangle>(oView.pTriangles, checked((int)oView.nTriangles));
    }
}
