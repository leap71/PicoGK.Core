// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGK.Core.Tests;

public class QueryTests
{
    [Fact]
    public void RayCast_FindsSphereSurface()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 10f);

        bool bHit = vox.bRayCastToSurface(
            new Vector3(-20f, 0f, 0f),
            Vector3.UnitX,
            out Vector3 vecHit);

        Assert.True(bHit);
        TestHelpers.AssertVectorNear(new Vector3(-10f, 0f, 0f), vecHit, 1f);
    }

    [Fact]
    public void ClosestPoint_FindsSphereSurfaceAndDistance()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 10f);

        bool bFound = vox.bClosestPointOnSurface(
            new Vector3(20f, 0f, 0f),
            out Vector3 vecPoint,
            out float fDistanceMM);

        Assert.True(bFound);
        TestHelpers.AssertVectorNear(new Vector3(10f, 0f, 0f), vecPoint, 1f);
        TestHelpers.AssertNear(10f, fDistanceMM, 1f);
    }

    [Fact]
    public void SurfaceNormal_OnPositiveXSidePointsOutward()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 10f);

        Vector3 vecNormal = vox.vecSurfaceNormal(new Vector3(10f, 0f, 0f));

        Assert.True(vecNormal.X > 0.9f);
        Assert.True(MathF.Abs(vecNormal.Y) < 0.2f);
        Assert.True(MathF.Abs(vecNormal.Z) < 0.2f);
        TestHelpers.AssertNear(1f, vecNormal.Length(), 0.05f);
    }
}
