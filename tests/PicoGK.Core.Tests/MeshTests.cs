// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using PicoGK.Geometry;
using Xunit;

namespace PicoGK.Core.Tests;

public class MeshTests
{
    [Fact]
    public void MeshFromVoxels_HasGeometry()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 8f);
        using Mesh msh = vox.mshAsMesh();

        Assert.True(msh.nVertexCount() > 0);
        Assert.True(msh.nTriangleCount() + msh.nQuadCount() > 0);
    }

    [Fact]
    public void TriangulatedView_ExpandsQuadIntoTwoTriangles()
    {
        MeshBuilder bld = new();
        bld.nAddQuad(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f));

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = bld.mshBuild(lib);

        MeshView oView = msh.oView();

        Assert.Equal(4, oView.avecVertices.Length);
        Assert.Equal(0, oView.atriTriangles.Length);
        Assert.Equal(1, oView.aquadQuads.Length);

        TriangulatedMeshView oTriangulated = msh.oTriangulatedView();

        Assert.Equal(4, oTriangulated.avecVertices.Length);
        Assert.Equal(2, oTriangulated.atriTriangles.Length);
        Assert.Equal(2, msh.nTriangulatedTriangleCount());
    }

    [Fact]
    public void EmptyMesh_HasEmptyBounds()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = new MeshBuilder().mshBuild(lib);

        IBounded3d oBounded = msh;
        Assert.True(oBounded.oBounds.bIsEmpty);
    }

    [Fact]
    public void OriginVertex_HasNonEmptyZeroSizeBounds()
    {
        MeshBuilder bld = new();
        bld.nAddVertex(Vector3.Zero);

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = bld.mshBuild(lib);

        Bounds3d oBounds = msh.oBounds;
        Assert.False(oBounds.bIsEmpty);
        Assert.Equal(Vector3.Zero, oBounds.vecMin);
        Assert.Equal(Vector3.Zero, oBounds.vecMax);
        Assert.Equal(Vector3.Zero, oBounds.vecSize);
    }

    [Fact]
    public void MeshBounds_ContainAllVertices()
    {
        MeshBuilder bld = new();
        bld.nAddVertex(new Vector3(-2f, 4f, 1f));
        bld.nAddVertex(new Vector3(3f, -5f, 7f));

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = bld.mshBuild(lib);

        Assert.Equal(new Vector3(-2f, -5f, 1f), msh.oBounds.vecMin);
        Assert.Equal(new Vector3(3f, 4f, 7f), msh.oBounds.vecMax);
    }
}
