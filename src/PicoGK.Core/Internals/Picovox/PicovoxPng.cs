// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PicoGK;

internal static class PicovoxPng
{
    static readonly byte[] s_anSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    static readonly byte[] s_anIhdr = "IHDR"u8.ToArray();
    static readonly byte[] s_anIdat = "IDAT"u8.ToArray();
    static readonly byte[] s_anIend = "IEND"u8.ToArray();

    internal static void Write(Stream oOutput, SdfSlice oSlice)
    {
        ArgumentNullException.ThrowIfNull(oOutput);
        ArgumentNullException.ThrowIfNull(oSlice);

        oOutput.Write(s_anSignature);

        byte[] anHeader = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(anHeader.AsSpan(0, 4), checked((uint)oSlice.nWidth));
        BinaryPrimitives.WriteUInt32BigEndian(anHeader.AsSpan(4, 4), checked((uint)oSlice.nHeight));
        anHeader[8] = 16; // bit depth
        anHeader[9] = 0;  // grayscale
        anHeader[10] = 0; // zlib/Deflate
        anHeader[11] = 0; // adaptive filtering method
        anHeader[12] = 0; // non-interlaced
        WriteChunk(oOutput, s_anIhdr, anHeader);

        using (PngIdatWriteStream oIdat = new(oOutput))
        {
            using ZLibStream oZlib = new(oIdat, CompressionLevel.Optimal, leaveOpen: true);
            byte[] anRow = new byte[checked(1 + 2 * oSlice.nWidth)];
            ReadOnlySpan<short> anValues = oSlice.aValues;

            for (int y = 0; y < oSlice.nHeight; ++y)
            {
                anRow[0] = 0;
                int nSource = y * oSlice.nWidth;
                for (int x = 0; x < oSlice.nWidth; ++x)
                {
                    short nValue = anValues[nSource + x];
                    if (nValue == SdfSlice.nReserved)
                        throw new InvalidDataException("A reserved SDF sample cannot be written to PicoVox.");

                    ushort nPngValue = unchecked((ushort)(nValue + 32768));
                    BinaryPrimitives.WriteUInt16BigEndian(
                        anRow.AsSpan(1 + 2 * x, 2),
                        nPngValue);
                }
                oZlib.Write(anRow);
            }
        }

        WriteChunk(oOutput, s_anIend, ReadOnlySpan<byte>.Empty);
    }

    internal static void Read(Stream oInput, SdfSlice oSlice)
    {
        ArgumentNullException.ThrowIfNull(oInput);
        ArgumentNullException.ThrowIfNull(oSlice);

        byte[] anSignature = new byte[s_anSignature.Length];
        ReadExactly(oInput, anSignature);
        if (!anSignature.AsSpan().SequenceEqual(s_anSignature))
            throw new InvalidDataException("Invalid PicoVox PNG signature.");

        PngChunkHeader oHeader = oReadChunkHeader(oInput);
        if (!oHeader.anType.AsSpan().SequenceEqual(s_anIhdr) || oHeader.nLength != 13)
            throw new InvalidDataException("A PicoVox PNG must begin with a 13-byte IHDR chunk.");

        byte[] anIhdr = new byte[13];
        ReadChunkPayloadAndCrc(oInput, oHeader.anType, anIhdr);

        uint nWidth = BinaryPrimitives.ReadUInt32BigEndian(anIhdr.AsSpan(0, 4));
        uint nHeight = BinaryPrimitives.ReadUInt32BigEndian(anIhdr.AsSpan(4, 4));
        if (nWidth != oSlice.nWidth || nHeight != oSlice.nHeight)
            throw new InvalidDataException("PicoVox PNG dimensions do not match the manifest.");
        if (anIhdr[8] != 16 || anIhdr[9] != 0 || anIhdr[10] != 0 ||
            anIhdr[11] != 0 || anIhdr[12] != 0)
        {
            throw new InvalidDataException("The PNG is outside the PicoVox grayscale-16 profile.");
        }

        PngChunkHeader oFirstIdat = oReadChunkHeader(oInput);
        if (!oFirstIdat.anType.AsSpan().SequenceEqual(s_anIdat))
            throw new InvalidDataException("IHDR must be followed immediately by IDAT.");

        using PngIdatReadStream oIdat = new(oInput, oFirstIdat.nLength);
        using ZLibStream oZlib = new(oIdat, CompressionMode.Decompress, leaveOpen: true);
        byte[] anRow = new byte[checked(1 + 2 * oSlice.nWidth)];
        Span<short> anValues = oSlice.aValues;

        for (int y = 0; y < oSlice.nHeight; ++y)
        {
            ReadExactly(oZlib, anRow);
            if (anRow[0] != 0)
                throw new InvalidDataException("PicoVox PNG scanlines must use filter None.");

            int nDestination = y * oSlice.nWidth;
            for (int x = 0; x < oSlice.nWidth; ++x)
            {
                ushort nPngValue = BinaryPrimitives.ReadUInt16BigEndian(
                    anRow.AsSpan(1 + 2 * x, 2));
                short nValue = unchecked((short)(nPngValue - 32768));
                if (nValue == SdfSlice.nReserved)
                    throw new InvalidDataException("A PicoVox PNG contains the reserved SDF sample.");
                anValues[nDestination + x] = nValue;
            }
        }

        if (oZlib.ReadByte() != -1)
            throw new InvalidDataException("A PicoVox PNG contains extra decompressed data.");
        oIdat.Complete();
    }

    static void WriteChunk(Stream oOutput, ReadOnlySpan<byte> anType, ReadOnlySpan<byte> anPayload)
    {
        Span<byte> anInteger = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(anInteger, checked((uint)anPayload.Length));
        oOutput.Write(anInteger);
        oOutput.Write(anType);
        oOutput.Write(anPayload);

        PngCrc32 oCrc = new();
        oCrc.Append(anType);
        oCrc.Append(anPayload);
        BinaryPrimitives.WriteUInt32BigEndian(anInteger, oCrc.nValue);
        oOutput.Write(anInteger);
    }

    static PngChunkHeader oReadChunkHeader(Stream oInput)
    {
        byte[] anHeader = new byte[8];
        ReadExactly(oInput, anHeader);
        uint nLength = BinaryPrimitives.ReadUInt32BigEndian(anHeader.AsSpan(0, 4));
        byte[] anType = anHeader.AsSpan(4, 4).ToArray();
        return new PngChunkHeader(nLength, anType);
    }

    static void ReadChunkPayloadAndCrc(Stream oInput, byte[] anType, byte[] anPayload)
    {
        ReadExactly(oInput, anPayload);
        PngCrc32 oCrc = new();
        oCrc.Append(anType);
        oCrc.Append(anPayload);
        ValidateCrc(oInput, oCrc.nValue);
    }

    static void ValidateCrc(Stream oInput, uint nActual)
    {
        Span<byte> anCrc = stackalloc byte[4];
        ReadExactly(oInput, anCrc);
        uint nExpected = BinaryPrimitives.ReadUInt32BigEndian(anCrc);
        if (nActual != nExpected)
            throw new InvalidDataException("Invalid PicoVox PNG chunk CRC-32.");
    }

    static void ReadExactly(Stream oInput, Span<byte> anBuffer)
    {
        int nOffset = 0;
        while (nOffset < anBuffer.Length)
        {
            int nRead = oInput.Read(anBuffer[nOffset..]);
            if (nRead == 0)
                throw new InvalidDataException("Unexpected end of PicoVox PNG data.");
            nOffset += nRead;
        }
    }

    readonly record struct PngChunkHeader(uint nLength, byte[] anType);

    sealed class PngIdatWriteStream : Stream
    {
        const int c_nChunkBytes = 64 * 1024;
        readonly Stream m_oOutput;
        readonly byte[] m_anBuffer = new byte[c_nChunkBytes];
        int m_nCount;
        bool m_bDisposed;

        internal PngIdatWriteStream(Stream oOutput) => m_oOutput = oOutput;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !m_bDisposed;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override void Write(byte[] anBuffer, int offset, int count) =>
            Write(anBuffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> anSource)
        {
            ObjectDisposedException.ThrowIf(m_bDisposed, this);
            while (!anSource.IsEmpty)
            {
                int nCopy = Math.Min(m_anBuffer.Length - m_nCount, anSource.Length);
                anSource[..nCopy].CopyTo(m_anBuffer.AsSpan(m_nCount));
                m_nCount += nCopy;
                anSource = anSource[nCopy..];
                if (m_nCount == m_anBuffer.Length)
                    FlushChunk();
            }
        }

        protected override void Dispose(bool bDisposing)
        {
            if (bDisposing && !m_bDisposed)
            {
                FlushChunk();
                m_bDisposed = true;
            }
            base.Dispose(bDisposing);
        }

        void FlushChunk()
        {
            if (m_nCount == 0)
                return;
            WriteChunk(m_oOutput, s_anIdat, m_anBuffer.AsSpan(0, m_nCount));
            m_nCount = 0;
        }

        public override int Read(byte[] anBuffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    sealed class PngIdatReadStream : Stream
    {
        readonly Stream m_oInput;
        uint m_nRemaining;
        PngCrc32 m_oCrc;
        bool m_bEnded;

        internal PngIdatReadStream(Stream oInput, uint nFirstLength)
        {
            m_oInput = oInput;
            BeginIdat(nFirstLength);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] anBuffer, int offset, int count) =>
            Read(anBuffer.AsSpan(offset, count));

        public override int Read(Span<byte> anDestination)
        {
            if (m_bEnded || anDestination.IsEmpty)
                return 0;

            while (m_nRemaining == 0)
            {
                ValidateCrc(m_oInput, m_oCrc.nValue);
                PngChunkHeader oNext = oReadChunkHeader(m_oInput);
                if (oNext.anType.AsSpan().SequenceEqual(s_anIdat))
                {
                    BeginIdat(oNext.nLength);
                    continue;
                }

                if (!oNext.anType.AsSpan().SequenceEqual(s_anIend) || oNext.nLength != 0)
                    throw new InvalidDataException("Consecutive IDAT chunks must be followed by a zero-length IEND.");

                PngCrc32 oIendCrc = new();
                oIendCrc.Append(s_anIend);
                ValidateCrc(m_oInput, oIendCrc.nValue);
                if (m_oInput.ReadByte() != -1)
                    throw new InvalidDataException("A PicoVox PNG contains data after IEND.");
                m_bEnded = true;
                return 0;
            }

            int nReadRequest = (int)Math.Min((uint)anDestination.Length, m_nRemaining);
            int nRead = m_oInput.Read(anDestination[..nReadRequest]);
            if (nRead == 0)
                throw new InvalidDataException("Unexpected end of PicoVox IDAT data.");
            m_oCrc.Append(anDestination[..nRead]);
            m_nRemaining -= (uint)nRead;
            return nRead;
        }

        internal void Complete()
        {
            Span<byte> anExtra = stackalloc byte[1];
            if (Read(anExtra) != 0)
                throw new InvalidDataException("A PicoVox PNG contains extra compressed data.");
            if (!m_bEnded)
                throw new InvalidDataException("A PicoVox PNG is missing IEND.");
        }

        void BeginIdat(uint nLength)
        {
            m_nRemaining = nLength;
            m_oCrc = new PngCrc32();
            m_oCrc.Append(s_anIdat);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] anBuffer, int offset, int count) => throw new NotSupportedException();
    }

    struct PngCrc32
    {
        const uint c_nPolynomial = 0xedb88320u;
        static readonly uint[] s_anTable = aCreateTable();
        uint m_nCrc;
        bool m_bInitialized;

        internal readonly uint nValue => ~m_nCrc;

        internal void Append(ReadOnlySpan<byte> anBytes)
        {
            if (!m_bInitialized)
            {
                m_nCrc = uint.MaxValue;
                m_bInitialized = true;
            }

            foreach (byte nByte in anBytes)
                m_nCrc = s_anTable[(m_nCrc ^ nByte) & 0xff] ^ (m_nCrc >> 8);
        }

        static uint[] aCreateTable()
        {
            uint[] anTable = new uint[256];
            for (uint n = 0; n < anTable.Length; ++n)
            {
                uint nCrc = n;
                for (int nBit = 0; nBit < 8; ++nBit)
                    nCrc = (nCrc & 1) != 0 ? c_nPolynomial ^ (nCrc >> 1) : nCrc >> 1;
                anTable[n] = nCrc;
            }
            return anTable;
        }
    }
}
