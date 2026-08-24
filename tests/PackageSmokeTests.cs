// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGKPackageTests;

public class PackageSmokeTests
{
    [Fact]
    public void NuGetPackage_LoadsNativeRuntime_AndBuildsMesh()
    {
        using Library lib = new(0.35f);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 8f);
        using Mesh msh = vox.mshAsMesh();

        Assert.False(string.IsNullOrWhiteSpace(Library.strVersion()));
        Assert.True(msh.nVertexCount() > 0);
        Assert.True(msh.nTriangleCount() + msh.nQuadCount() > 0);
    }
}
