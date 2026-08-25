// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGK.Core.Tests;

public class SdfSliceTests
{
    [Fact]
    public void Encoding_UsesCanonicalSignedRange()
    {
        TestHelpers.AssertNear(-3f, SdfSlice.fDistanceVoxels(SdfSlice.nInsideBackground), 1e-5f);
        TestHelpers.AssertNear( 0f, SdfSlice.fDistanceVoxels(SdfSlice.nZero), 1e-5f);
        TestHelpers.AssertNear( 3f, SdfSlice.fDistanceVoxels(SdfSlice.nOutsideBackground), 1e-5f);

        Assert.Equal(SdfSlice.nInsideBackground, SdfSlice.nEncodeDistanceVoxels(-10f));
        Assert.Equal(SdfSlice.nZero, SdfSlice.nEncodeDistanceVoxels(0f));
        Assert.Equal(SdfSlice.nOutsideBackground, SdfSlice.nEncodeDistanceVoxels(10f));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SdfSlice.fDistanceVoxels(SdfSlice.nReserved));
    }

    [Fact]
    public void SphereSlice_ContainsInsideAndOutsideSamples()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 8f);

        SdfSlice oSlice = vox.oCreateSlice(out int nSliceCount);
        Assert.True(nSliceCount > 0);

        vox.GetSlice(nSliceCount / 2, oSlice);

        Assert.True(oSlice.nWidth > 0);
        Assert.True(oSlice.nHeight > 0);

        bool bHasInside = false;
        bool bHasOutside = false;

        foreach (short nValue in oSlice.aValues)
        {
            bHasInside |= nValue < SdfSlice.nZero;
            bHasOutside |= nValue > SdfSlice.nZero;
        }

        Assert.True(bHasInside);
        Assert.True(bHasOutside);
    }
}
