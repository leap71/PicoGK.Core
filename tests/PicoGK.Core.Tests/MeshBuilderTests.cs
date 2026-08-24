// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGK.Core.Tests;

public class MeshBuilderTests
{
    [Fact]
    public void MeshBuilder_BuildsExactTriangle()
    {
        MeshBuilder bld = new();

        uint nA = bld.nAddVertex(new Vector3(0f, 0f, 0f));
        uint nB = bld.nAddVertex(new Vector3(1f, 0f, 0f));
        uint nC = bld.nAddVertex(new Vector3(0f, 1f, 0f));
        bld.nAddTriangle(nA, nB, nC);

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = bld.mshBuild(lib);

        Assert.Equal(3, msh.nVertexCount());
        Assert.Equal(1, msh.nTriangleCount());
        Assert.Equal(0, msh.nQuadCount());

        MeshView oView = msh.oView();
        Assert.Equal(new Vector3(0f, 0f, 0f), oView.avecVertices[0]);
        Assert.Equal(new Vector3(1f, 0f, 0f), oView.avecVertices[1]);
        Assert.Equal(new Vector3(0f, 1f, 0f), oView.avecVertices[2]);

        Triangle tri = oView.atriTriangles[0];
        Assert.Equal(0u, tri.A);
        Assert.Equal(1u, tri.B);
        Assert.Equal(2u, tri.C);
    }

    [Fact]
    public void MeshBuilder_RemainsReusableAfterBuild()
    {
        MeshBuilder bld = new();

        bld.nAddTriangle(
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f));

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh mshFirst = bld.mshBuild(lib);

        bld.nAddTriangle(
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(0f, 1f, 1f));

        using Mesh mshSecond = bld.mshBuild(lib);

        Assert.Equal(3, mshFirst.nVertexCount());
        Assert.Equal(1, mshFirst.nTriangleCount());
        Assert.Equal(6, mshSecond.nVertexCount());
        Assert.Equal(2, mshSecond.nTriangleCount());

        bld.Clear();

        Assert.Equal(0, bld.nVertexCount());
        Assert.Equal(0, bld.nTriangleCount());
        Assert.Equal(0, bld.nQuadCount());
    }
}
