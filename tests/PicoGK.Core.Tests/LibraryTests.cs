// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGK.Core.Tests;

public class LibraryTests
{
    [Fact]
    public void Library_CanCreateAndDispose()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);

        Assert.Equal(TestHelpers.fVoxelSizeMM, lib.fVoxelSizeMM);
        Assert.False(string.IsNullOrWhiteSpace(Library.strName));
        Assert.False(string.IsNullOrWhiteSpace(Library.strVersion));
    }

    [Fact]
    public void Library_OwnsRemainingNativeObjects()
    {
        Library lib = new(TestHelpers.fVoxelSizeMM);
        Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 5f);
        Mesh msh = vox.mshAsMesh();

        Assert.Equal(1L, lib.nVoxelsAllocated);
        Assert.Equal(1L, lib.nMeshesAllocated);

        // Library disposal is authoritative. Disposing child wrappers afterwards
        // must remain safe and must not double-free native objects.
        lib.Dispose();
        msh.Dispose();
        vox.Dispose();
    }
}
