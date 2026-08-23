// SPDX-License-Identifier: Apache-2.0

namespace PicoGK;

/// <summary>
/// Reusable canonical PicoGK signed-distance slice.
///
/// Values are signed 16-bit fixed-point samples over the fixed +/-3 voxel
/// narrow band. -32767 is the inside background, 0 is the zero isosurface,
/// +32767 is the outside background, and -32768 is reserved.
/// </summary>
public sealed class SdfSlice
{
    public const int   nHalfWidthVoxels  = 3;
    public const short nInsideBackground = -32767;
    public const short nZero             = 0;
    public const short nOutsideBackground= 32767;
    public const short nReserved         = -32768;

    readonly short[] m_anValues;

    public int nWidth  { get; private set; }
    public int nHeight { get; private set; }
    public int nCapacity => m_anValues.Length;

    /// <summary>
    /// Active slice values in row-major order.
    /// </summary>
    public Span<short> aValues => m_anValues.AsSpan(0, checked(nWidth * nHeight));

    internal short[] anValues => m_anValues;

    public SdfSlice(int nWidth, int nHeight)
    {
        if (nWidth < 0)  throw new ArgumentOutOfRangeException(nameof(nWidth));
        if (nHeight < 0) throw new ArgumentOutOfRangeException(nameof(nHeight));

        int nCount = checked(nWidth * nHeight);
        m_anValues  = new short[nCount];
        this.nWidth = nWidth;
        this.nHeight= nHeight;
    }

    public short this[int x, int y]
    {
        get => m_anValues[nIndex(x, y)];
        set => m_anValues[nIndex(x, y)] = value;
    }

    /// <summary>
    /// Decodes a canonical sample to signed distance in voxel units.
    /// </summary>
    public static float fDistanceVoxels(short nValue)
    {
        if (nValue == nReserved)
            throw new ArgumentOutOfRangeException(nameof(nValue), "Reserved SDF sample value.");

        return nValue * (nHalfWidthVoxels / (float)nOutsideBackground);
    }

    /// <summary>
    /// Decodes a canonical sample to signed distance in millimetres.
    /// </summary>
    public static float fDistanceMM(short nValue, float fVoxelSizeMM)
    {
        if (!float.IsFinite(fVoxelSizeMM) || fVoxelSizeMM <= 0f)
            throw new ArgumentOutOfRangeException(nameof(fVoxelSizeMM));
        return fDistanceVoxels(nValue) * fVoxelSizeMM;
    }

    /// <summary>
    /// Encodes a signed distance in voxel units into the canonical int16 range.
    /// Values outside the narrow band are clamped to the corresponding background.
    /// </summary>
    public static short nEncodeDistanceVoxels(float fDistanceVoxels)
    {
        if (!float.IsFinite(fDistanceVoxels))
            throw new ArgumentOutOfRangeException(nameof(fDistanceVoxels));

        float fClamped = Math.Clamp(fDistanceVoxels, -nHalfWidthVoxels, nHalfWidthVoxels);
        return (short)MathF.Round(fClamped * (nOutsideBackground / (float)nHalfWidthVoxels), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Encodes a signed distance in millimetres into the canonical int16 range.
    /// </summary>
    public static short nEncodeDistanceMM(float fDistanceMM, float fVoxelSizeMM)
    {
        if (!float.IsFinite(fVoxelSizeMM) || fVoxelSizeMM <= 0f)
            throw new ArgumentOutOfRangeException(nameof(fVoxelSizeMM));

        return nEncodeDistanceVoxels(fDistanceMM / fVoxelSizeMM);
    }

    internal void SetDimensions(int nWidth, int nHeight)
    {
        if (nWidth < 0 || nHeight < 0 || (long)nWidth * nHeight > m_anValues.Length)
            throw new InvalidOperationException("Native SDF slice dimensions exceed the managed buffer capacity.");

        this.nWidth  = nWidth;
        this.nHeight = nHeight;
    }

    int nIndex(int x, int y)
    {
        if ((uint)x >= (uint)nWidth)  throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)nHeight) throw new ArgumentOutOfRangeException(nameof(y));
        return y * nWidth + x;
    }
}
