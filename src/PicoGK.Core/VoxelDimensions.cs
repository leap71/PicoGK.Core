// SPDX-License-Identifier: Apache-2.0

namespace PicoGK;

/// <summary>
/// Index-space origin and dimensions of a Voxels object's active voxel bounds.
/// </summary>
public readonly struct VoxelDimensions
{
    public readonly int nXOrigin;
    public readonly int nYOrigin;
    public readonly int nZOrigin;

    public readonly int nXSize;
    public readonly int nYSize;
    public readonly int nZSize;

    public VoxelDimensions(int nXOrigin, int nYOrigin, int nZOrigin,
                           int nXSize, int nYSize, int nZSize)
    {
        this.nXOrigin = nXOrigin;
        this.nYOrigin = nYOrigin;
        this.nZOrigin = nZOrigin;
        this.nXSize   = nXSize;
        this.nYSize   = nYSize;
        this.nZSize   = nZSize;
    }

    public bool bIsEmpty => nXSize == 0 || nYSize == 0 || nZSize == 0;
}
