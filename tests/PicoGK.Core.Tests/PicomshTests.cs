// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Binary;
using System.Numerics;
using PicoGK;
using PicoGK.Geometry;
using Xunit;

namespace PicoGK.Core.Tests;

public class PicomshTests
{
    [Fact]
    public void HybridMesh_RoundTripsLosslessly()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh mshSource = mshCreateHybrid(lib);
        using MemoryStream oStream = new();

        PicoMsh.Write(mshSource, oStream);
        oStream.Position = 0;
        using Mesh mshRoundTrip = PicoMsh.mshFromStream(lib, oStream);

        MeshView oExpected = mshSource.oView();
        MeshView oActual = mshRoundTrip.oView();
        Assert.Equal(oExpected.avecVertices.ToArray(), oActual.avecVertices.ToArray());
        Assert.Equal(oExpected.aquadQuads.ToArray(), oActual.aquadQuads.ToArray());
        Assert.Equal(oExpected.atriTriangles.ToArray(), oActual.atriTriangles.ToArray());

        for (int n = 0; n < oExpected.avecVertices.Length; ++n)
        {
            Assert.Equal(
                BitConverter.SingleToInt32Bits(oExpected.avecVertices[n].X),
                BitConverter.SingleToInt32Bits(oActual.avecVertices[n].X));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(oExpected.avecVertices[n].Y),
                BitConverter.SingleToInt32Bits(oActual.avecVertices[n].Y));
            Assert.Equal(
                BitConverter.SingleToInt32Bits(oExpected.avecVertices[n].Z),
                BitConverter.SingleToInt32Bits(oActual.avecVertices[n].Z));
        }
    }

    [Fact]
    public void Writer_EmitsCanonicalHeaderAndSectionTable()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = mshCreateHybrid(lib);
        using MemoryStream oStream = new();

        PicoMsh.Write(msh, oStream);
        byte[] anFile = oStream.ToArray();

        Assert.Equal("PICOMSH\0"u8.ToArray(), anFile.AsSpan(0, 8).ToArray());
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(anFile.AsSpan(8, 4)));
        Assert.Equal(96u, BinaryPrimitives.ReadUInt32LittleEndian(anFile.AsSpan(12, 4)));
        Assert.Equal(5ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(32, 8)));
        Assert.Equal(1ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(40, 8)));
        Assert.Equal(1ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(48, 8)));
        Assert.Equal(96ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(56, 8)));
        Assert.Equal(156ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(64, 8)));
        Assert.Equal(172ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(72, 8)));
        Assert.Equal(184ul, BinaryPrimitives.ReadUInt64LittleEndian(anFile.AsSpan(80, 8)));
        Assert.Equal(184, anFile.Length);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(anFile.AsSpan(156, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(anFile.AsSpan(172, 4)));
    }

    [Fact]
    public void FileReader_MemoryMapsAndRoundTripsMesh()
    {
        string strFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.picomsh");
        try
        {
            using Library lib = new(TestHelpers.fVoxelSizeMM);
            using Mesh mshSource = mshCreateHybrid(lib);
            PicoMsh.Write(mshSource, strFilePath);

            using Mesh mshRoundTrip = PicoMsh.mshFromFile(lib, strFilePath);
            Assert.Equal(
                mshSource.oView().atriTriangles.ToArray(),
                mshRoundTrip.oView().atriTriangles.ToArray());
            Assert.Equal(
                mshSource.oView().aquadQuads.ToArray(),
                mshRoundTrip.oView().aquadQuads.ToArray());
        }
        finally
        {
            File.Delete(strFilePath);
        }
    }

    [Fact]
    public void Writer_IsDeterministic()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = mshCreateHybrid(lib);
        using MemoryStream oFirst = new();
        using MemoryStream oSecond = new();

        PicoMsh.Write(msh, oFirst);
        PicoMsh.Write(msh, oSecond);

        Assert.Equal(oFirst.ToArray(), oSecond.ToArray());
    }

    [Fact]
    public void Reader_RejectsOutOfRangeIndex()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = mshCreateHybrid(lib);
        using MemoryStream oWritten = new();
        PicoMsh.Write(msh, oWritten);
        byte[] anFile = oWritten.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(anFile.AsSpan(156, 4), 5u);
        using MemoryStream oInput = new(anFile);

        Assert.Throws<InvalidDataException>(() => PicoMsh.mshFromStream(lib, oInput));
    }

    [Fact]
    public void Reader_RejectsTruncationAndTrailingData()
    {
        using Library lib = new(TestHelpers.fVoxelSizeMM);
        using Mesh msh = mshCreateHybrid(lib);
        using MemoryStream oWritten = new();
        PicoMsh.Write(msh, oWritten);
        byte[] anFile = oWritten.ToArray();

        using MemoryStream oTruncated = new(anFile[..^1]);
        Assert.Throws<InvalidDataException>(() => PicoMsh.mshFromStream(lib, oTruncated));

        using MemoryStream oTrailing = new([.. anFile, 0]);
        Assert.Throws<InvalidDataException>(() => PicoMsh.mshFromStream(lib, oTrailing));
    }

    static Mesh mshCreateHybrid(Library lib)
    {
        MeshBuilder bld = new();
        float fNegativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        bld.nAddVertex(new Vector3(fNegativeZero, 0f, 0f));
        bld.nAddVertex(new Vector3(2f, 0f, 0f));
        bld.nAddVertex(new Vector3(2f, 2f, 0f));
        bld.nAddVertex(new Vector3(0f, 2f, 0f));
        bld.nAddVertex(new Vector3(1f, 1f, 3f));
        bld.nAddQuad(new Quad(0, 1, 2, 3));
        bld.nAddTriangle(new Triangle(0, 3, 4));
        return bld.mshBuild(lib);
    }
}
