// SPDX-License-Identifier: Apache-2.0

namespace PicoGK;

/// <summary>Reads and writes the lossless PicoMsh mesh exchange format.</summary>
public static class PicoMsh
{
    /// <summary>
    /// Reconstructs a Mesh from a PicoMsh stream. The readable, seekable stream
    /// must be positioned at its beginning and remains open.
    /// </summary>
    public static Mesh mshFromStream(Library lib, Stream oStream)
        => PicomshBinary.mshRead(lib, oStream);

    /// <summary>
    /// Reconstructs a Mesh from a PicoMsh file. On little-endian platforms the
    /// file is memory-mapped and copied directly into native Mesh storage.
    /// </summary>
    public static Mesh mshFromFile(Library lib, string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(strFilePath);
        return PicomshBinary.mshReadMapped(lib, strFilePath);
    }

    /// <summary>
    /// Writes a Mesh to a PicoMsh stream without triangulating its quads. The
    /// supplied stream remains open.
    /// </summary>
    public static void Write(Mesh msh, Stream oStream)
        => PicomshBinary.Write(msh, oStream);

    /// <summary>Writes a Mesh to a PicoMsh file, replacing it if it exists.</summary>
    public static void Write(Mesh msh, string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(strFilePath);
        using FileStream oStream = File.Create(strFilePath);
        Write(msh, oStream);
    }
}
