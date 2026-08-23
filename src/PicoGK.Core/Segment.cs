// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;

namespace PicoGK;

/// <summary>
/// Indexed line segment used by bulk tube construction.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Segment
{
    public readonly uint A;
    public readonly uint B;

    public Segment(uint A, uint B)
    {
        this.A = A;
        this.B = B;
    }
}
