// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
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
}
