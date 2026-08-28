// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using PicoGK.Geometry;
using Xunit;

namespace PicoGK.Core.Tests;

public class VoxelsTests
{
    [Fact]
    public void EmptyVolume_IsEmpty()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = new(lib);

        Assert.True(vox.bIsEmpty());
        Assert.Equal(0f, vox.fCalculateVolume());
        Assert.Equal(Bounds3d.Empty, vox.oCalculateBoundingBox());
    }

    [Fact]
    public void Sphere_HasExpectedBasicGeometry()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        const float fRadius = 10f;
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, fRadius);

        Assert.False(vox.bIsEmpty());
        Assert.True(vox.bIsInside(Vector3.Zero));
        Assert.False(vox.bIsInside(new Vector3(20f, 0f, 0f)));

        float fExpectedVolume = 4f / 3f * MathF.PI * fRadius * fRadius * fRadius;
        float fActualVolume = vox.fCalculateVolume();

        Assert.InRange(fActualVolume, fExpectedVolume * 0.95f, fExpectedVolume * 1.05f);
    }

    [Fact]
    public void Copy_IsIndependent()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxSource = Voxels.voxFromSphere(lib, Vector3.Zero, 8f);
        using Voxels voxCopy = new(voxSource);

        float fSourceVolume = voxSource.fCalculateVolume();
        float fCopyVolume = voxCopy.fCalculateVolume();

        Assert.Equal(fSourceVolume, fCopyVolume);
        Assert.Equal(voxSource.oVoxelDimensions(), voxCopy.oVoxelDimensions());
        Assert.Equal(voxSource.bIsInside(Vector3.Zero), voxCopy.bIsInside(Vector3.Zero));

        voxCopy.Offset(2f);

        Assert.Equal(fSourceVolume, voxSource.fCalculateVolume());
        Assert.True(voxCopy.fCalculateVolume() > fCopyVolume);
    }

    [Fact]
    public void Addition_IsNonDestructive()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        Vector3 vecCenterA = new(-8f, 0f, 0f);
        Vector3 vecCenterB = new( 8f, 0f, 0f);

        using Voxels voxA = Voxels.voxFromSphere(lib, vecCenterA, 6f);
        using Voxels voxB = Voxels.voxFromSphere(lib, vecCenterB, 6f);

        float fVolumeA = voxA.fCalculateVolume();
        float fVolumeB = voxB.fCalculateVolume();
        Assert.False(voxA.bIsInside(vecCenterB));
        Assert.False(voxB.bIsInside(vecCenterA));

        using Voxels voxResult = voxA + voxB;

        Assert.Equal(fVolumeA, voxA.fCalculateVolume());
        Assert.Equal(fVolumeB, voxB.fCalculateVolume());
        Assert.True(voxA.bIsInside(vecCenterA));
        Assert.True(voxB.bIsInside(vecCenterB));
        Assert.False(voxA.bIsInside(vecCenterB));
        Assert.False(voxB.bIsInside(vecCenterA));
        Assert.True(voxResult.bIsInside(vecCenterA));
        Assert.True(voxResult.bIsInside(vecCenterB));
    }

    [Fact]
    public void AdditionInPlace_MutatesLeftOperand()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        Vector3 vecCenterA = new(-8f, 0f, 0f);
        Vector3 vecCenterB = new( 8f, 0f, 0f);

        Voxels voxA = Voxels.voxFromSphere(lib, vecCenterA, 6f);
        using Voxels voxB = Voxels.voxFromSphere(lib, vecCenterB, 6f);

        try
        {
            Assert.False(voxA.bIsInside(vecCenterB));

            voxA += voxB;

            Assert.True(voxA.bIsInside(vecCenterA));
            Assert.True(voxA.bIsInside(vecCenterB));
        }
        finally
        {
            voxA.Dispose();
        }
    }

    [Fact]
    public void Subtraction_RemovesOverlap()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxA = Voxels.voxFromSphere(lib, Vector3.Zero, 10f);
        using Voxels voxB = Voxels.voxFromSphere(lib, new Vector3(8f, 0f, 0f), 6f);

        using Voxels voxResult = voxA - voxB;

        Assert.True(voxA.bIsInside(new Vector3(8f, 0f, 0f)));
        Assert.False(voxResult.bIsInside(new Vector3(8f, 0f, 0f)));
        Assert.True(voxResult.bIsInside(new Vector3(-8f, 0f, 0f)));
    }

    [Fact]
    public void Intersection_KeepsOnlyOverlap()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxA = Voxels.voxFromSphere(lib, Vector3.Zero, 10f);
        using Voxels voxB = Voxels.voxFromSphere(lib, new Vector3(8f, 0f, 0f), 6f);

        using Voxels voxResult = voxA & voxB;

        Assert.True(voxResult.bIsInside(new Vector3(8f, 0f, 0f)));
        Assert.False(voxResult.bIsInside(Vector3.Zero));
    }

    [Fact]
    public void Offset_IncreasesVolume()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 8f);

        float fVolumeBefore = vox.fCalculateVolume();
        vox.Offset(2f);
        float fVolumeAfter = vox.fCalculateVolume();

        Assert.True(fVolumeAfter > fVolumeBefore);
    }

    [Fact]
    public void NativeFailure_BecomesPicoGKException()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);

        PicoGKException ex = Assert.Throws<PicoGKException>(
            () => Voxels.voxFromSphere(lib, Vector3.Zero, -1f));

        Assert.Contains("Radius", ex.Message);
    }
}
