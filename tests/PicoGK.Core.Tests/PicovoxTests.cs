// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using PicoGK;
using Xunit;

namespace PicoGK.Core.Tests;

public class PicovoxTests
{
    [Fact]
    public void Png_RoundTripsCanonicalSamples()
    {
        SdfSlice oSource = new(5, 1);
        short[] anSamples =
        [
            SdfSlice.nInsideBackground,
            -1,
            SdfSlice.nZero,
            1,
            SdfSlice.nOutsideBackground
        ];
        anSamples.CopyTo(oSource.aValues);

        using MemoryStream oStream = new();
        PicovoxPng.Write(oStream, oSource);
        oStream.Position = 0;

        SdfSlice oRoundTrip = new(5, 1);
        PicovoxPng.Read(oStream, oRoundTrip);
        Assert.Equal(anSamples, oRoundTrip.aValues.ToArray());
    }

    [Fact]
    public void Png_RejectsReservedSample()
    {
        SdfSlice oSlice = new(1, 1);
        oSlice[0, 0] = SdfSlice.nReserved;
        using MemoryStream oStream = new();

        Assert.Throws<InvalidDataException>(() => PicovoxPng.Write(oStream, oSlice));
    }

    [Fact]
    public void Sphere_RoundTripsCanonicalSlices()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxSource = Voxels.voxFromSphere(lib, new Vector3(1f, -2f, 3f), 5f);
        using MemoryStream oStream = new();

        PicoVox.Write(voxSource, oStream);
        oStream.Position = 0;
        using Voxels voxRoundTrip = PicoVox.voxFromStream(lib, oStream);

        Assert.Equal(voxSource.oVoxelDimensions(), voxRoundTrip.oVoxelDimensions());
        Assert.Equal(voxSource.fVoxelSizeMM, voxRoundTrip.fVoxelSizeMM);

        SdfSlice oExpected = voxSource.oCreateSlice(out int nExpectedSlices);
        SdfSlice oActual = voxRoundTrip.oCreateSlice(out int nActualSlices);
        Assert.Equal(nExpectedSlices, nActualSlices);

        for (int z = 0; z < nExpectedSlices; ++z)
        {
            voxSource.GetSlice(z, oExpected);
            voxRoundTrip.GetSlice(z, oActual);
            Assert.True(oExpected.aValues.SequenceEqual(oActual.aValues));
        }
    }

    [Fact]
    public void EmptyVolume_RoundTripsWithoutSlices()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxSource = new(lib);
        using MemoryStream oStream = new();

        PicoVox.Write(voxSource, oStream);
        oStream.Position = 0;
        using ZipArchive oArchive = new(oStream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Single(oArchive.Entries);
        Assert.Equal("manifest.txt", oArchive.Entries[0].FullName);
        oArchive.Dispose();

        oStream.Position = 0;
        using Voxels voxRoundTrip = PicoVox.voxFromStream(lib, oStream);
        Assert.True(voxRoundTrip.bIsEmpty);
    }

    [Fact]
    public void Writer_UsesCanonicalEntryNamesAndStoredZipEntries()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 2f);
        using MemoryStream oStream = new();

        PicoVox.Write(vox, oStream);
        byte[] anArchive = oStream.ToArray();
        AssertAllCentralDirectoryEntriesAreStored(anArchive);

        oStream.Position = 0;
        using ZipArchive oArchive = new(oStream, ZipArchiveMode.Read);
        Assert.Equal("manifest.txt", oArchive.Entries[0].FullName);
        Assert.Equal("slices/src_0000000000.png", oArchive.Entries[1].FullName);
        Assert.All(oArchive.Entries, oEntry => Assert.Equal(oEntry.Length, oEntry.CompressedLength));
    }

    [Fact]
    public void Writer_IsDeterministic()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Voxels vox = Voxels.voxFromSphere(lib, Vector3.Zero, 2f);
        using MemoryStream oFirst = new();
        using MemoryStream oSecond = new();

        PicoVox.Write(vox, oFirst);
        PicoVox.Write(vox, oSecond);

        Assert.Equal(oFirst.ToArray(), oSecond.ToArray());
    }

    [Fact]
    public void VoxelSizeReader_LeavesStreamRewoundForLoading()
    {
        using Library libSource = new(TestHelpers.fVoxelSizeMM);
        using Voxels voxSource = Voxels.voxFromSphere(libSource, Vector3.Zero, 2f);
        using MemoryStream oStream = new();
        PicoVox.Write(voxSource, oStream);
        oStream.Position = 0;

        float fVoxelSizeMM = PicoVox.fReadVoxelSizeMM(oStream);

        Assert.Equal(TestHelpers.fVoxelSizeMM, fVoxelSizeMM);
        Assert.Equal(0, oStream.Position);
        using Library libCompatible = new(fVoxelSizeMM);
        using Voxels voxRoundTrip = PicoVox.voxFromStream(libCompatible, oStream);
        Assert.Equal(voxSource.oVoxelDimensions(), voxRoundTrip.oVoxelDimensions());
    }

    [Fact]
    public void FileHelpers_ReadAndWritePicoVox()
    {
        string strFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.picovox");
        try
        {
            using Library lib = new(TestHelpers.fVoxelSizeMM);
            using Voxels voxSource = Voxels.voxFromSphere(lib, Vector3.Zero, 2f);

            PicoVox.Write(voxSource, strFilePath);

            Assert.Equal(TestHelpers.fVoxelSizeMM, PicoVox.fReadVoxelSizeMM(strFilePath));
            using Voxels voxRoundTrip = PicoVox.voxFromFile(lib, strFilePath);
            Assert.Equal(voxSource.oVoxelDimensions(), voxRoundTrip.oVoxelDimensions());
        }
        finally
        {
            File.Delete(strFilePath);
        }
    }

    [Fact]
    public void Reader_RejectsZipCompression()
    {
        using MemoryStream oStream = new();
        using (ZipArchive oArchive = new(oStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry oEntry = oArchive.CreateEntry("manifest.txt", CompressionLevel.Optimal);
            using StreamWriter oWriter = new(oEntry.Open());
            oWriter.Write(new string('x', 512));
        }
        oStream.Position = 0;

        using Library lib = new(TestHelpers.fVoxelSizeMM);
        InvalidDataException oException = Assert.Throws<InvalidDataException>(
            () => PicoVox.voxFromStream(lib, oStream));
        Assert.Contains("NONE", oException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_RejectsDifferentLibraryVoxelSize()
    {
        using Library libSource = new(0.5f);
        using Library libTarget = new(1f);
        using Voxels vox = Voxels.voxFromSphere(libSource, Vector3.Zero, 2f);
        using MemoryStream oStream = new();
        PicoVox.Write(vox, oStream);
        oStream.Position = 0;

        InvalidDataException oException = Assert.Throws<InvalidDataException>(
            () => PicoVox.voxFromStream(libTarget, oStream));
        Assert.Contains("voxel size", oException.Message, StringComparison.OrdinalIgnoreCase);
    }

    static void AssertAllCentralDirectoryEntriesAreStored(byte[] anArchive)
    {
        int nEocd = -1;
        for (int n = anArchive.Length - 22; n >= 0; --n)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(anArchive.AsSpan(n, 4)) == 0x06054b50u)
            {
                nEocd = n;
                break;
            }
        }
        Assert.True(nEocd >= 0);

        int nEntryCount = BinaryPrimitives.ReadUInt16LittleEndian(anArchive.AsSpan(nEocd + 10, 2));
        int nOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(anArchive.AsSpan(nEocd + 16, 4)));

        for (int nEntry = 0; nEntry < nEntryCount; ++nEntry)
        {
            Assert.Equal(0x02014b50u,
                BinaryPrimitives.ReadUInt32LittleEndian(anArchive.AsSpan(nOffset, 4)));
            Assert.Equal(0,
                BinaryPrimitives.ReadUInt16LittleEndian(anArchive.AsSpan(nOffset + 10, 2)));

            int nNameLength = BinaryPrimitives.ReadUInt16LittleEndian(anArchive.AsSpan(nOffset + 28, 2));
            int nExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(anArchive.AsSpan(nOffset + 30, 2));
            int nCommentLength = BinaryPrimitives.ReadUInt16LittleEndian(anArchive.AsSpan(nOffset + 32, 2));
            nOffset += 46 + nNameLength + nExtraLength + nCommentLength;
        }
    }
}
