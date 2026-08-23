// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;

namespace PicoGK;

/// <summary>Triangle defined by three zero-based unsigned vertex indices.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Triangle
{
    public readonly uint A;
    public readonly uint B;
    public readonly uint C;

    public Triangle(uint A, uint B, uint C)
    {
        this.A = A;
        this.B = B;
        this.C = C;
    }
}
