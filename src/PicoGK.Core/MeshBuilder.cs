// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Runtime.InteropServices;
using PicoGK.Geometry;

namespace PicoGK;

/// <summary>
/// Mutable managed helper for constructing an immutable Mesh.
///
/// MeshBuilder owns no native resources and is independent of any Library.
/// Building a Mesh copies the current buffers once into native storage owned by
/// the supplied Library. Vertices are never deduplicated automatically.
/// </summary>
public sealed class MeshBuilder
{
    readonly List<Vector3> m_avecVertices;
    readonly List<Triangle> m_atriTriangles;
    readonly List<Quad> m_aquadQuads;

    /// <summary>Creates an empty MeshBuilder with optional initial capacities.</summary>
    public MeshBuilder(int nVertexCapacity = 0,
                       int nTriangleCapacity = 0,
                       int nQuadCapacity = 0)
    {
        if (nVertexCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(nVertexCapacity));
        if (nTriangleCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(nTriangleCapacity));
        if (nQuadCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(nQuadCapacity));

        m_avecVertices  = new List<Vector3>(nVertexCapacity);
        m_atriTriangles = new List<Triangle>(nTriangleCapacity);
        m_aquadQuads    = new List<Quad>(nQuadCapacity);
    }

    public int nVertexCount()   => m_avecVertices.Count;
    public int nTriangleCount() => m_atriTriangles.Count;
    public int nQuadCount()     => m_aquadQuads.Count;

    /// <summary>Adds a vertex and returns its zero-based index.</summary>
    public uint nAddVertex(Vector3 vecVertex)
    {
        ValidateVertex(vecVertex);

        uint nIndex = checked((uint)m_avecVertices.Count);
        m_avecVertices.Add(vecVertex);
        return nIndex;
    }

    /// <summary>Adds a triangle referencing existing vertices and returns its index.</summary>
    public uint nAddTriangle(uint nA, uint nB, uint nC)
        => nAddTriangle(new Triangle(nA, nB, nC));

    /// <summary>Adds a triangle referencing existing vertices and returns its index.</summary>
    public uint nAddTriangle(Triangle tri)
    {
        ValidateVertexIndex(tri.A);
        ValidateVertexIndex(tri.B);
        ValidateVertexIndex(tri.C);

        uint nIndex = checked((uint)m_atriTriangles.Count);
        m_atriTriangles.Add(tri);
        return nIndex;
    }

    /// <summary>
    /// Adds three new vertices and a triangle referencing them. No vertex
    /// deduplication is performed. Returns the triangle index.
    /// </summary>
    public uint nAddTriangle(Vector3 vecA, Vector3 vecB, Vector3 vecC)
    {
        ValidateVertex(vecA);
        ValidateVertex(vecB);
        ValidateVertex(vecC);

        uint nA = nAddVertex(vecA);
        uint nB = nAddVertex(vecB);
        uint nC = nAddVertex(vecC);
        return nAddTriangle(nA, nB, nC);
    }

    /// <summary>Adds a quad referencing existing vertices and returns its index.</summary>
    public uint nAddQuad(uint nA, uint nB, uint nC, uint nD)
        => nAddQuad(new Quad(nA, nB, nC, nD));

    /// <summary>Adds a quad referencing existing vertices and returns its index.</summary>
    public uint nAddQuad(Quad quad)
    {
        ValidateVertexIndex(quad.A);
        ValidateVertexIndex(quad.B);
        ValidateVertexIndex(quad.C);
        ValidateVertexIndex(quad.D);

        uint nIndex = checked((uint)m_aquadQuads.Count);
        m_aquadQuads.Add(quad);
        return nIndex;
    }

    /// <summary>
    /// Adds four new vertices and a quad referencing them. No vertex
    /// deduplication is performed. Returns the quad index.
    /// </summary>
    public uint nAddQuad(Vector3 vecA, Vector3 vecB, Vector3 vecC, Vector3 vecD)
    {
        ValidateVertex(vecA);
        ValidateVertex(vecB);
        ValidateVertex(vecC);
        ValidateVertex(vecD);

        uint nA = nAddVertex(vecA);
        uint nB = nAddVertex(vecB);
        uint nC = nAddVertex(vecC);
        uint nD = nAddVertex(vecD);
        return nAddQuad(nA, nB, nC, nD);
    }

    /// <summary>
    /// Builds an immutable native Mesh owned by the supplied Library.
    /// The builder remains valid and may be modified or built again afterwards.
    /// </summary>
    public Mesh mshBuild(Library lib)
    {
        ArgumentNullException.ThrowIfNull(lib);

        return Mesh.mshFromBuffers(
            lib,
            CollectionsMarshal.AsSpan(m_avecVertices),
            CollectionsMarshal.AsSpan(m_atriTriangles),
            CollectionsMarshal.AsSpan(m_aquadQuads));
    }

    /// <summary>Removes all accumulated geometry while retaining allocated capacities.</summary>
    public void Clear()
    {
        m_avecVertices.Clear();
        m_atriTriangles.Clear();
        m_aquadQuads.Clear();
    }

    void ValidateVertexIndex(uint nIndex)
    {
        if (nIndex >= (uint)m_avecVertices.Count)
            throw new ArgumentOutOfRangeException(nameof(nIndex),
                $"Vertex index {nIndex} is outside the current vertex range [0, {m_avecVertices.Count}).");
    }

    static void ValidateVertex(Vector3 vecVertex)
    {
        if (!float.IsFinite(vecVertex.X) ||
            !float.IsFinite(vecVertex.Y) ||
            !float.IsFinite(vecVertex.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(vecVertex),
                "Mesh vertex coordinates must be finite.");
        }
    }
}
