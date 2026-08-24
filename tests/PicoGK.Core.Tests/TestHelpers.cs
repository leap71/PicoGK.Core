// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using Xunit;

namespace PicoGK.Core.Tests;

internal static class TestHelpers
{
    public const float fVoxelSizeMM = 0.5f;

    public static void AssertNear(float fExpected, float fActual, float fTolerance)
        => Assert.InRange(fActual, fExpected - fTolerance, fExpected + fTolerance);

    public static void AssertVectorNear(Vector3 vecExpected, Vector3 vecActual, float fTolerance)
    {
        AssertNear(vecExpected.X, vecActual.X, fTolerance);
        AssertNear(vecExpected.Y, vecActual.Y, fTolerance);
        AssertNear(vecExpected.Z, vecActual.Z, fTolerance);
    }
}
