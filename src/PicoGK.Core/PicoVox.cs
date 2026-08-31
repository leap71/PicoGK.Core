// SPDX-License-Identifier: Apache-2.0

namespace PicoGK;

/// <summary>
/// Reads and writes the deliberately narrow PicoVox voxel exchange format.
/// </summary>
public static class PicoVox
{
    /// <summary>
    /// Reads the voxel size from a PicoVox stream without creating native geometry.
    /// The readable, seekable stream must be positioned at its beginning and
    /// remains open and positioned at its beginning.
    /// </summary>
    public static float fReadVoxelSizeMM(Stream oStream)
        => PicovoxArchive.fReadVoxelSizeMM(oStream);

    /// <summary>Reads the voxel size from a PicoVox file.</summary>
    public static float fReadVoxelSizeMM(string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(strFilePath);
        using FileStream oStream = File.OpenRead(strFilePath);
        return fReadVoxelSizeMM(oStream);
    }

    /// <summary>
    /// Reconstructs Voxels from a PicoVox stream.
    /// The readable, seekable stream must be positioned at its beginning and
    /// remains open. Its voxel size must exactly match the supplied Library.
    /// </summary>
    public static Voxels voxFromStream(Library lib, Stream oStream)
        => PicovoxArchive.voxRead(lib, oStream);

    /// <summary>
    /// Reconstructs Voxels from a PicoVox file. Its voxel size must exactly
    /// match the supplied Library.
    /// </summary>
    public static Voxels voxFromFile(Library lib, string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(strFilePath);
        using FileStream oStream = File.OpenRead(strFilePath);
        return voxFromStream(lib, oStream);
    }

    /// <summary>
    /// Writes Voxels to a PicoVox stream. The supplied stream remains open.
    /// </summary>
    public static void Write(Voxels vox, Stream oStream)
        => PicovoxArchive.Write(vox, oStream);

    /// <summary>Writes Voxels to a PicoVox file, replacing it if it exists.</summary>
    public static void Write(Voxels vox, string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(strFilePath);
        using FileStream oStream = File.Create(strFilePath);
        Write(vox, oStream);
    }
}
