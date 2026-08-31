// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;

namespace PicoGK;

internal static class PicovoxArchive
{
    static readonly DateTimeOffset s_oCanonicalTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static void Write(Voxels vox, Stream oOutput)
    {
        ArgumentNullException.ThrowIfNull(vox);
        ArgumentNullException.ThrowIfNull(oOutput);
        if (!oOutput.CanWrite)
            throw new ArgumentException("The PicoVox output stream must be writable.", nameof(oOutput));

        PicovoxManifest oManifest = PicovoxManifest.oFromVoxels(vox);
        using ZipArchive oArchive = new(oOutput, ZipArchiveMode.Create, leaveOpen: true);

        ZipArchiveEntry oManifestEntry = oCreateStoredEntry(oArchive, PicovoxManifest.c_strEntryName);
        oManifest.Write(oManifestEntry);

        if (oManifest.bIsEmpty)
            return;

        SdfSlice oSlice = new(oManifest.nXSize, oManifest.nYSize);
        for (int z = 0; z < oManifest.nZSize; ++z)
        {
            vox.GetSlice(z, oSlice, Voxels.ESliceAxis.Z);
            ZipArchiveEntry oSliceEntry = oCreateStoredEntry(oArchive, strSliceEntryName(z));
            using Stream oEntryStream = oSliceEntry.Open();
            PicovoxPng.Write(oEntryStream, oSlice);
        }
    }

    internal static float fReadVoxelSizeMM(Stream oInput)
    {
        ValidateInput(oInput);
        try
        {
            ValidateZipStructure(oInput);
            oInput.Position = 0;
            using ZipArchive oArchive = new(oInput, ZipArchiveMode.Read, leaveOpen: true);
            return oReadManifest(oArchive).fVoxelSizeMM;
        }
        finally
        {
            oInput.Position = 0;
        }
    }

    internal static Voxels voxRead(Library lib, Stream oInput)
    {
        ArgumentNullException.ThrowIfNull(lib);
        ValidateInput(oInput);

        ValidateZipStructure(oInput);
        oInput.Position = 0;
        using ZipArchive oArchive = new(oInput, ZipArchiveMode.Read, leaveOpen: true);
        PicovoxManifest oManifest = oReadManifest(oArchive);
        oManifest.ValidateForLibrary(lib);

        long nExpectedEntries = 1L + oManifest.nZSize;
        if (oArchive.Entries.Count != nExpectedEntries)
            throw new InvalidDataException("The PicoVox archive entry count does not match SizeZ.");

        SafeVoxelsHandle hVoxels = NativeApi.hBeginSdfImport(
            lib.hNative,
            oManifest.oNativeDescription());
        Voxels vox = new(lib, hVoxels);

        try
        {
            if (!oManifest.bIsEmpty)
            {
                SdfSlice oSlice = new(oManifest.nXSize, oManifest.nYSize);
                for (int z = 0; z < oManifest.nZSize; ++z)
                {
                    ZipArchiveEntry oEntry = oArchive.Entries[z + 1];
                    string strExpectedName = strSliceEntryName(z);
                    if (oEntry.FullName != strExpectedName)
                        throw new InvalidDataException($"Expected PicoVox entry {strExpectedName}.");
                    ValidateStoredEntry(oEntry);

                    using Stream oEntryStream = oEntry.Open();
                    PicovoxPng.Read(oEntryStream, oSlice);
                    NativeApi.ImportSdfZSlice(lib.hNative, hVoxels, z, oSlice);
                }
            }

            NativeApi.EndSdfImport(lib.hNative, hVoxels);
            return vox;
        }
        catch
        {
            vox.Dispose();
            throw;
        }
    }

    internal static string strSliceEntryName(int z) =>
        "slices/src_" + z.ToString("D10", CultureInfo.InvariantCulture) + ".png";

    static ZipArchiveEntry oCreateStoredEntry(ZipArchive oArchive, string strName)
    {
        ZipArchiveEntry oEntry = oArchive.CreateEntry(strName, CompressionLevel.NoCompression);
        oEntry.LastWriteTime = s_oCanonicalTimestamp;
        oEntry.ExternalAttributes = 0;
        return oEntry;
    }

    static void ValidateInput(Stream oInput)
    {
        ArgumentNullException.ThrowIfNull(oInput);
        if (!oInput.CanRead || !oInput.CanSeek)
            throw new ArgumentException("The PicoVox input stream must be readable and seekable.", nameof(oInput));
        if (oInput.Position != 0)
            throw new ArgumentException("The PicoVox input stream must be positioned at its beginning.", nameof(oInput));
    }

    static PicovoxManifest oReadManifest(ZipArchive oArchive)
    {
        if (oArchive.Entries.Count == 0 ||
            oArchive.Entries[0].FullName != PicovoxManifest.c_strEntryName)
        {
            throw new InvalidDataException("A PicoVox archive must begin with manifest.txt.");
        }

        ValidateStoredEntry(oArchive.Entries[0]);
        return PicovoxManifest.oRead(oArchive.Entries[0]);
    }

    static void ValidateZipStructure(Stream oInput)
    {
        const uint c_nEocdSignature = 0x06054b50u;
        const uint c_nZip64LocatorSignature = 0x07064b50u;
        const uint c_nZip64EocdSignature = 0x06064b50u;
        const uint c_nCentralEntrySignature = 0x02014b50u;

        if (oInput.Length < 22)
            throw new InvalidDataException("The PicoVox ZIP archive is truncated.");

        Span<byte> anEocd = stackalloc byte[22];
        oInput.Position = oInput.Length - anEocd.Length;
        ReadExactly(oInput, anEocd);
        if (BinaryPrimitives.ReadUInt32LittleEndian(anEocd) != c_nEocdSignature ||
            BinaryPrimitives.ReadUInt16LittleEndian(anEocd[20..]) != 0)
        {
            throw new InvalidDataException("PicoVox ZIP archives must have a canonical uncommented end record.");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(anEocd[4..]) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(anEocd[6..]) != 0)
        {
            throw new InvalidDataException("Multi-disk ZIP archives are not valid PicoVox files.");
        }

        ulong nEntryCount = BinaryPrimitives.ReadUInt16LittleEndian(anEocd[10..]);
        ulong nCentralSize = BinaryPrimitives.ReadUInt32LittleEndian(anEocd[12..]);
        ulong nCentralOffset = BinaryPrimitives.ReadUInt32LittleEndian(anEocd[16..]);

        bool bZip64 = nEntryCount == ushort.MaxValue ||
                      nCentralSize == uint.MaxValue ||
                      nCentralOffset == uint.MaxValue;
        if (bZip64)
        {
            if (oInput.Length < 98)
                throw new InvalidDataException("The PicoVox ZIP64 archive is truncated.");

            Span<byte> anLocator = stackalloc byte[20];
            oInput.Position = oInput.Length - 42;
            ReadExactly(oInput, anLocator);
            if (BinaryPrimitives.ReadUInt32LittleEndian(anLocator) != c_nZip64LocatorSignature ||
                BinaryPrimitives.ReadUInt32LittleEndian(anLocator[4..]) != 0 ||
                BinaryPrimitives.ReadUInt32LittleEndian(anLocator[16..]) != 1)
            {
                throw new InvalidDataException("Invalid PicoVox ZIP64 locator.");
            }

            ulong nZip64Offset = BinaryPrimitives.ReadUInt64LittleEndian(anLocator[8..]);
            if (nZip64Offset > (ulong)oInput.Length - 56)
                throw new InvalidDataException("Invalid PicoVox ZIP64 end-record offset.");

            Span<byte> anZip64 = stackalloc byte[56];
            oInput.Position = checked((long)nZip64Offset);
            ReadExactly(oInput, anZip64);
            if (BinaryPrimitives.ReadUInt32LittleEndian(anZip64) != c_nZip64EocdSignature ||
                BinaryPrimitives.ReadUInt64LittleEndian(anZip64[4..]) < 44 ||
                BinaryPrimitives.ReadUInt32LittleEndian(anZip64[16..]) != 0 ||
                BinaryPrimitives.ReadUInt32LittleEndian(anZip64[20..]) != 0)
            {
                throw new InvalidDataException("Invalid PicoVox ZIP64 end record.");
            }

            nEntryCount = BinaryPrimitives.ReadUInt64LittleEndian(anZip64[32..]);
            nCentralSize = BinaryPrimitives.ReadUInt64LittleEndian(anZip64[40..]);
            nCentralOffset = BinaryPrimitives.ReadUInt64LittleEndian(anZip64[48..]);
        }

        if (nEntryCount > int.MaxValue ||
            nCentralOffset > (ulong)oInput.Length ||
            nCentralSize > (ulong)oInput.Length - nCentralOffset)
        {
            throw new InvalidDataException("Invalid PicoVox ZIP central-directory bounds.");
        }

        oInput.Position = checked((long)nCentralOffset);
        long nCentralEnd = checked((long)(nCentralOffset + nCentralSize));
        Span<byte> anCentralHeader = stackalloc byte[46];
        for (ulong nEntry = 0; nEntry < nEntryCount; ++nEntry)
        {
            ReadExactly(oInput, anCentralHeader);
            if (BinaryPrimitives.ReadUInt32LittleEndian(anCentralHeader) != c_nCentralEntrySignature)
                throw new InvalidDataException("Invalid PicoVox ZIP central-directory entry.");

            ushort nFlags = BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[8..]);
            ushort nMethod = BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[10..]);
            ushort nDisk = BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[34..]);
            if ((nFlags & 1) != 0)
                throw new InvalidDataException("Encrypted ZIP entries are not valid PicoVox data.");
            if (nMethod != 0)
                throw new InvalidDataException("PicoVox ZIP entries must use compression method NONE.");
            if (nDisk != 0)
                throw new InvalidDataException("Multi-disk ZIP entries are not valid PicoVox data.");

            int nVariableBytes =
                BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[28..]) +
                BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[30..]) +
                BinaryPrimitives.ReadUInt16LittleEndian(anCentralHeader[32..]);
            if (oInput.Position > nCentralEnd - nVariableBytes)
                throw new InvalidDataException("A PicoVox ZIP central-directory entry is truncated.");
            oInput.Position += nVariableBytes;
        }

        if (oInput.Position != nCentralEnd)
            throw new InvalidDataException("The PicoVox ZIP central-directory size is inconsistent.");
    }

    static void ValidateStoredEntry(ZipArchiveEntry oEntry)
    {
        if (oEntry.CompressedLength != oEntry.Length)
            throw new InvalidDataException("Invalid stored PicoVox ZIP entry length.");
        if (oEntry.FullName.EndsWith("/", StringComparison.Ordinal))
            throw new InvalidDataException("PicoVox archives must not contain directory entries.");
    }

    static void ReadExactly(Stream oInput, Span<byte> anBuffer)
    {
        int nOffset = 0;
        while (nOffset < anBuffer.Length)
        {
            int nRead = oInput.Read(anBuffer[nOffset..]);
            if (nRead == 0)
                throw new InvalidDataException("Unexpected end of PicoVox ZIP data.");
            nOffset += nRead;
        }
    }
}
