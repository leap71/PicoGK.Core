// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PicoGK.Geometry;

namespace PicoGK;

internal static class PicomshBinary
{
    internal const int c_nHeaderSize = 96;
    internal const uint c_nVersion = 1;
    const int c_nVertexStride = 12;
    const int c_nQuadStride = 16;
    const int c_nTriangleStride = 12;

    internal static void Write(Mesh msh, Stream oOutput)
    {
        ArgumentNullException.ThrowIfNull(msh);
        ArgumentNullException.ThrowIfNull(oOutput);
        if (!oOutput.CanWrite)
            throw new ArgumentException("The PicoMsh output stream must be writable.", nameof(oOutput));
        ValidateRuntimeLayout();

        MeshView oView = msh.oView();
        PicomshHeader oHeader = oCreateHeader(
            oView.avecVertices.Length,
            oView.aquadQuads.Length,
            oView.atriTriangles.Length);

        Span<byte> anHeader = stackalloc byte[c_nHeaderSize];
        WriteHeader(anHeader, oHeader);
        oOutput.Write(anHeader);
        WriteVertices(oOutput, oView.avecVertices);
        WriteQuads(oOutput, oView.aquadQuads);
        WriteTriangles(oOutput, oView.atriTriangles);
    }

    internal static Mesh mshRead(Library lib, Stream oInput)
    {
        ArgumentNullException.ThrowIfNull(lib);
        ArgumentNullException.ThrowIfNull(oInput);
        if (!oInput.CanRead || !oInput.CanSeek)
            throw new ArgumentException(
                "The PicoMsh input stream must be readable and seekable.",
                nameof(oInput));
        if (oInput.Position != 0)
            throw new ArgumentException("The PicoMsh input stream must be positioned at its beginning.", nameof(oInput));
        ValidateRuntimeLayout();

        try
        {
            Span<byte> anHeader = stackalloc byte[c_nHeaderSize];
            oInput.ReadExactly(anHeader);
            PicomshHeader oHeader = oReadHeader(anHeader);
            ValidateHeader(oHeader, oInput.CanSeek ? checked((ulong)oInput.Length) : null);

            int nVertexCount = nManagedCount(oHeader.nVertexCount, "vertex");
            int nQuadCount = nManagedCount(oHeader.nQuadCount, "quad");
            int nTriangleCount = nManagedCount(oHeader.nTriangleCount, "triangle");

            Vector3[] avecVertices = new Vector3[nVertexCount];
            Quad[] aquadQuads = new Quad[nQuadCount];
            Triangle[] atriTriangles = new Triangle[nTriangleCount];

            ReadVertices(oInput, avecVertices);
            ReadQuads(oInput, aquadQuads);
            ReadTriangles(oInput, atriTriangles);
            if (oInput.ReadByte() != -1)
                throw new InvalidDataException("The PicoMsh file contains trailing data.");

            ValidateGeometry(avecVertices, aquadQuads, atriTriangles);
            return Mesh.mshFromBuffers(lib, avecVertices, atriTriangles, aquadQuads);
        }
        catch (EndOfStreamException oException)
        {
            throw new InvalidDataException("The PicoMsh file is truncated.", oException);
        }
        catch (OverflowException oException)
        {
            throw new InvalidDataException("The PicoMsh header exceeds supported integer bounds.", oException);
        }
    }

    internal static unsafe Mesh mshReadMapped(Library lib, string strFilePath)
    {
        ArgumentNullException.ThrowIfNull(lib);
        ArgumentNullException.ThrowIfNull(strFilePath);

        if (!BitConverter.IsLittleEndian)
        {
            using FileStream oFallback = File.OpenRead(strFilePath);
            return mshRead(lib, oFallback);
        }

        using FileStream oFile = File.OpenRead(strFilePath);
        if (oFile.Length < c_nHeaderSize)
            throw new InvalidDataException("The PicoMsh file is truncated.");

        using MemoryMappedFile oMapping = MemoryMappedFile.CreateFromFile(
            oFile,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);
        using MemoryMappedViewAccessor oAccessor = oMapping.CreateViewAccessor(
            0,
            0,
            MemoryMappedFileAccess.Read);

        byte* pView = null;
        oAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pView);
        try
        {
            byte* pData = pView + checked((nint)oAccessor.PointerOffset);
            PicomshHeader oHeader = oReadHeader(new ReadOnlySpan<byte>(pData, c_nHeaderSize));
            ValidateHeader(oHeader, checked((ulong)oFile.Length));

            int nVertexCount = nManagedCount(oHeader.nVertexCount, "vertex");
            int nQuadCount = nManagedCount(oHeader.nQuadCount, "quad");
            int nTriangleCount = nManagedCount(oHeader.nTriangleCount, "triangle");

            ReadOnlySpan<Vector3> avecVertices = new(
                pData + checked((nint)oHeader.nVertexOffset),
                nVertexCount);
            ReadOnlySpan<Quad> aquadQuads = new(
                pData + checked((nint)oHeader.nQuadOffset),
                nQuadCount);
            ReadOnlySpan<Triangle> atriTriangles = new(
                pData + checked((nint)oHeader.nTriangleOffset),
                nTriangleCount);

            ValidateGeometry(avecVertices, aquadQuads, atriTriangles);
            return Mesh.mshFromBuffers(lib, avecVertices, atriTriangles, aquadQuads);
        }
        catch (OverflowException oException)
        {
            throw new InvalidDataException("The PicoMsh header exceeds supported integer bounds.", oException);
        }
        finally
        {
            oAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    static PicomshHeader oCreateHeader(int nVertexCount, int nQuadCount, int nTriangleCount)
    {
        ulong nVertexOffset = c_nHeaderSize;
        ulong nQuadOffset = checked(nVertexOffset + (ulong)nVertexCount * c_nVertexStride);
        ulong nTriangleOffset = checked(nQuadOffset + (ulong)nQuadCount * c_nQuadStride);
        ulong nFileLength = checked(nTriangleOffset + (ulong)nTriangleCount * c_nTriangleStride);

        return new PicomshHeader(
            (ulong)nVertexCount,
            (ulong)nQuadCount,
            (ulong)nTriangleCount,
            nVertexOffset,
            nQuadOffset,
            nTriangleOffset,
            nFileLength);
    }

    static PicomshHeader oReadHeader(ReadOnlySpan<byte> anHeader)
    {
        if (!anHeader[..8].SequenceEqual("PICOMSH\0"u8))
            throw new InvalidDataException("Invalid PicoMsh magic header.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(anHeader[8..]) != c_nVersion)
            throw new InvalidDataException("Unsupported PicoMsh version.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(anHeader[12..]) != c_nHeaderSize)
            throw new InvalidDataException("Unsupported PicoMsh header size.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(anHeader[16..]) != 0)
            throw new InvalidDataException("Unsupported PicoMsh flags.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(anHeader[20..]) != c_nVertexStride ||
            BinaryPrimitives.ReadUInt32LittleEndian(anHeader[24..]) != c_nQuadStride ||
            BinaryPrimitives.ReadUInt32LittleEndian(anHeader[28..]) != c_nTriangleStride)
        {
            throw new InvalidDataException("Unsupported PicoMsh element representation.");
        }
        if (BinaryPrimitives.ReadUInt64LittleEndian(anHeader[88..]) != 0)
            throw new InvalidDataException("The PicoMsh reserved header field must be zero.");

        return new PicomshHeader(
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[32..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[40..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[48..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[56..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[64..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[72..]),
            BinaryPrimitives.ReadUInt64LittleEndian(anHeader[80..]));
    }

    static void WriteHeader(Span<byte> anHeader, PicomshHeader oHeader)
    {
        anHeader.Clear();
        "PICOMSH\0"u8.CopyTo(anHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(anHeader[8..], c_nVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(anHeader[12..], c_nHeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(anHeader[20..], c_nVertexStride);
        BinaryPrimitives.WriteUInt32LittleEndian(anHeader[24..], c_nQuadStride);
        BinaryPrimitives.WriteUInt32LittleEndian(anHeader[28..], c_nTriangleStride);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[32..], oHeader.nVertexCount);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[40..], oHeader.nQuadCount);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[48..], oHeader.nTriangleCount);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[56..], oHeader.nVertexOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[64..], oHeader.nQuadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[72..], oHeader.nTriangleOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(anHeader[80..], oHeader.nFileLength);
    }

    static void ValidateHeader(PicomshHeader oHeader, ulong? nActualLength)
    {
        try
        {
            PicomshHeader oCanonical = oCreateHeader(
                nManagedCount(oHeader.nVertexCount, "vertex"),
                nManagedCount(oHeader.nQuadCount, "quad"),
                nManagedCount(oHeader.nTriangleCount, "triangle"));

            if (oHeader.nVertexOffset != oCanonical.nVertexOffset ||
                oHeader.nQuadOffset != oCanonical.nQuadOffset ||
                oHeader.nTriangleOffset != oCanonical.nTriangleOffset ||
                oHeader.nFileLength != oCanonical.nFileLength)
            {
                throw new InvalidDataException("The PicoMsh section table is inconsistent with its counts.");
            }
            if (nActualLength.HasValue && nActualLength.Value != oHeader.nFileLength)
                throw new InvalidDataException("The PicoMsh file length does not match its header.");
        }
        catch (OverflowException oException)
        {
            throw new InvalidDataException("The PicoMsh section table overflows its integer representation.", oException);
        }
    }

    static int nManagedCount(ulong nCount, string strElement)
    {
        if (nCount > int.MaxValue)
            throw new InvalidDataException($"The PicoMsh {strElement} count exceeds the managed Mesh limit.");
        return (int)nCount;
    }

    static void ValidateGeometry(
        ReadOnlySpan<Vector3> avecVertices,
        ReadOnlySpan<Quad> aquadQuads,
        ReadOnlySpan<Triangle> atriTriangles)
    {
        foreach (Vector3 vecVertex in avecVertices)
        {
            if (!float.IsFinite(vecVertex.X) ||
                !float.IsFinite(vecVertex.Y) ||
                !float.IsFinite(vecVertex.Z))
            {
                throw new InvalidDataException("PicoMsh vertex coordinates must be finite.");
            }
        }

        uint nVertexCount = checked((uint)avecVertices.Length);
        foreach (Quad quad in aquadQuads)
        {
            if (quad.A >= nVertexCount || quad.B >= nVertexCount ||
                quad.C >= nVertexCount || quad.D >= nVertexCount)
            {
                throw new InvalidDataException("A PicoMsh quad contains an out-of-range vertex index.");
            }
        }
        foreach (Triangle tri in atriTriangles)
        {
            if (tri.A >= nVertexCount || tri.B >= nVertexCount || tri.C >= nVertexCount)
                throw new InvalidDataException("A PicoMsh triangle contains an out-of-range vertex index.");
        }
    }

    static void WriteVertices(Stream oOutput, ReadOnlySpan<Vector3> avecVertices)
    {
        if (BitConverter.IsLittleEndian)
        {
            WriteLittleEndianBytes(oOutput, avecVertices, c_nVertexStride);
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nVertexStride];
        foreach (Vector3 vec in avecVertices)
        {
            WriteSingle(anValue, 0, vec.X);
            WriteSingle(anValue, 4, vec.Y);
            WriteSingle(anValue, 8, vec.Z);
            oOutput.Write(anValue);
        }
    }

    static void WriteQuads(Stream oOutput, ReadOnlySpan<Quad> aquadQuads)
    {
        if (BitConverter.IsLittleEndian)
        {
            WriteLittleEndianBytes(oOutput, aquadQuads, c_nQuadStride);
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nQuadStride];
        foreach (Quad quad in aquadQuads)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(anValue, quad.A);
            BinaryPrimitives.WriteUInt32LittleEndian(anValue[4..], quad.B);
            BinaryPrimitives.WriteUInt32LittleEndian(anValue[8..], quad.C);
            BinaryPrimitives.WriteUInt32LittleEndian(anValue[12..], quad.D);
            oOutput.Write(anValue);
        }
    }

    static void WriteTriangles(Stream oOutput, ReadOnlySpan<Triangle> atriTriangles)
    {
        if (BitConverter.IsLittleEndian)
        {
            WriteLittleEndianBytes(oOutput, atriTriangles, c_nTriangleStride);
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nTriangleStride];
        foreach (Triangle tri in atriTriangles)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(anValue, tri.A);
            BinaryPrimitives.WriteUInt32LittleEndian(anValue[4..], tri.B);
            BinaryPrimitives.WriteUInt32LittleEndian(anValue[8..], tri.C);
            oOutput.Write(anValue);
        }
    }

    static void ReadVertices(Stream oInput, Span<Vector3> avecVertices)
    {
        if (BitConverter.IsLittleEndian)
        {
            oInput.ReadExactly(MemoryMarshal.AsBytes(avecVertices));
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nVertexStride];
        for (int n = 0; n < avecVertices.Length; ++n)
        {
            oInput.ReadExactly(anValue);
            avecVertices[n] = new Vector3(
                fReadSingle(anValue, 0),
                fReadSingle(anValue, 4),
                fReadSingle(anValue, 8));
        }
    }

    static void ReadQuads(Stream oInput, Span<Quad> aquadQuads)
    {
        if (BitConverter.IsLittleEndian)
        {
            oInput.ReadExactly(MemoryMarshal.AsBytes(aquadQuads));
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nQuadStride];
        for (int n = 0; n < aquadQuads.Length; ++n)
        {
            oInput.ReadExactly(anValue);
            aquadQuads[n] = new Quad(
                BinaryPrimitives.ReadUInt32LittleEndian(anValue),
                BinaryPrimitives.ReadUInt32LittleEndian(anValue[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(anValue[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(anValue[12..]));
        }
    }

    static void ReadTriangles(Stream oInput, Span<Triangle> atriTriangles)
    {
        if (BitConverter.IsLittleEndian)
        {
            oInput.ReadExactly(MemoryMarshal.AsBytes(atriTriangles));
            return;
        }

        Span<byte> anValue = stackalloc byte[c_nTriangleStride];
        for (int n = 0; n < atriTriangles.Length; ++n)
        {
            oInput.ReadExactly(anValue);
            atriTriangles[n] = new Triangle(
                BinaryPrimitives.ReadUInt32LittleEndian(anValue),
                BinaryPrimitives.ReadUInt32LittleEndian(anValue[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(anValue[8..]));
        }
    }

    static void WriteLittleEndianBytes<T>(
        Stream oOutput,
        ReadOnlySpan<T> aValues,
        int nStride)
        where T : struct
    {
        int nMaxElementsPerWrite = int.MaxValue / nStride;
        while (!aValues.IsEmpty)
        {
            int nCount = Math.Min(aValues.Length, nMaxElementsPerWrite);
            oOutput.Write(MemoryMarshal.AsBytes(aValues[..nCount]));
            aValues = aValues[nCount..];
        }
    }

    static void ValidateRuntimeLayout()
    {
        if (Unsafe.SizeOf<Vector3>() != c_nVertexStride ||
            Unsafe.SizeOf<Quad>() != c_nQuadStride ||
            Unsafe.SizeOf<Triangle>() != c_nTriangleStride)
        {
            throw new PlatformNotSupportedException(
                "The runtime Mesh element layout is incompatible with PicoMsh v1.");
        }
    }

    static void WriteSingle(Span<byte> anValue, int nOffset, float fValue)
        => BinaryPrimitives.WriteInt32LittleEndian(
            anValue[nOffset..],
            BitConverter.SingleToInt32Bits(fValue));

    static float fReadSingle(ReadOnlySpan<byte> anValue, int nOffset)
        => BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(anValue[nOffset..]));

    readonly record struct PicomshHeader(
        ulong nVertexCount,
        ulong nQuadCount,
        ulong nTriangleCount,
        ulong nVertexOffset,
        ulong nQuadOffset,
        ulong nTriangleOffset,
        ulong nFileLength);
}
