// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;

namespace PicoGK;

/// <summary>Quad defined by four zero-based unsigned vertex indices.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Quad
{
    public readonly uint A;
    public readonly uint B;
    public readonly uint C;
    public readonly uint D;

    public Quad(uint A, uint B, uint C, uint D)
    {
        this.A = A;
        this.B = B;
        this.C = C;
        this.D = D;
    }
}
