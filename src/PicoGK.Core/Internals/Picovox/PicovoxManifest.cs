// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PicoGK;

internal readonly record struct PicovoxManifest(
    float fVoxelSizeMM,
    int nXOrigin,
    int nYOrigin,
    int nZOrigin,
    int nXSize,
    int nYSize,
    int nZSize)
{
    internal const string c_strEntryName = "manifest.txt";
    internal const int c_nVersion = 1;
    const int c_nMaxManifestBytes = 1024;

    internal bool bIsEmpty => nXSize == 0;

    internal static PicovoxManifest oFromVoxels(Voxels vox)
    {
        VoxelDimensions oDimensions = vox.oVoxelDimensions();
        PicovoxManifest oManifest = new(
            vox.fVoxelSizeMM,
            oDimensions.nXOrigin,
            oDimensions.nYOrigin,
            oDimensions.nZOrigin,
            oDimensions.nXSize,
            oDimensions.nYSize,
            oDimensions.nZSize);
        oManifest.Validate();
        return oManifest;
    }

    internal static PicovoxManifest oRead(ZipArchiveEntry oEntry)
    {
        if (oEntry.Length > c_nMaxManifestBytes)
            throw new InvalidDataException("The PicoVox manifest is too large.");

        using Stream oStream = oEntry.Open();
        using StreamReader oReader = new(
            oStream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: c_nMaxManifestBytes,
            leaveOpen: false);

        string strManifest = oReader.ReadToEnd();
        if (Encoding.UTF8.GetByteCount(strManifest) != oEntry.Length)
            throw new InvalidDataException("The PicoVox manifest is not canonical UTF-8 text.");

        string[] astrLines = strManifest.Split('\n');
        if (astrLines.Length == 9 && astrLines[^1].Length == 0)
            Array.Resize(ref astrLines, 8);
        if (astrLines.Length != 8)
            throw new InvalidDataException("The PicoVox manifest must contain exactly eight properties.");

        for (int nLine = 0; nLine < astrLines.Length; ++nLine)
        {
            if (astrLines[nLine].EndsWith('\r'))
                astrLines[nLine] = astrLines[nLine][..^1];
        }

        int nReadVersion = nReadInt32(astrLines[0], "PicoVoxVersion", bNonNegative: true);
        if (nReadVersion != c_nVersion)
            throw new InvalidDataException($"Unsupported PicoVox version {nReadVersion}.");

        float fVoxelSizeMM = fReadSingle(astrLines[1], "VoxelSizeMM");
        PicovoxManifest oManifest = new(
            fVoxelSizeMM,
            nReadInt32(astrLines[2], "OriginX", bNonNegative: false),
            nReadInt32(astrLines[3], "OriginY", bNonNegative: false),
            nReadInt32(astrLines[4], "OriginZ", bNonNegative: false),
            nReadInt32(astrLines[5], "SizeX", bNonNegative: true),
            nReadInt32(astrLines[6], "SizeY", bNonNegative: true),
            nReadInt32(astrLines[7], "SizeZ", bNonNegative: true));
        oManifest.Validate();
        return oManifest;
    }

    internal void Write(ZipArchiveEntry oEntry)
    {
        StringBuilder oSb = new();
        oSb.Append("PicoVoxVersion: ").Append(c_nVersion).Append('\n');
        oSb.Append("VoxelSizeMM: ").Append(fVoxelSizeMM.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("OriginX: ").Append(nXOrigin.ToString(CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("OriginY: ").Append(nYOrigin.ToString(CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("OriginZ: ").Append(nZOrigin.ToString(CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("SizeX: ").Append(nXSize.ToString(CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("SizeY: ").Append(nYSize.ToString(CultureInfo.InvariantCulture)).Append('\n');
        oSb.Append("SizeZ: ").Append(nZSize.ToString(CultureInfo.InvariantCulture)).Append('\n');

        byte[] anUtf8 = Encoding.UTF8.GetBytes(oSb.ToString());
        using Stream oStream = oEntry.Open();
        oStream.Write(anUtf8);
    }

    internal void ValidateForLibrary(Library lib)
    {
        if (BitConverter.SingleToInt32Bits(fVoxelSizeMM) !=
            BitConverter.SingleToInt32Bits(lib.fVoxelSizeMM))
        {
            throw new InvalidDataException(
                "The PicoVox voxel size does not match the target Library voxel size.");
        }
    }

    internal NativeSdfVolumeDesc oNativeDescription() => new()
    {
        nXOrigin = nXOrigin,
        nYOrigin = nYOrigin,
        nZOrigin = nZOrigin,
        nXSize = checked((uint)nXSize),
        nYSize = checked((uint)nYSize),
        nZSize = checked((uint)nZSize)
    };

    void Validate()
    {
        if (!float.IsFinite(fVoxelSizeMM) || fVoxelSizeMM <= 0f)
            throw new InvalidDataException("VoxelSizeMM must be finite and greater than zero.");

        bool bAllZero = nXSize == 0 && nYSize == 0 && nZSize == 0;
        bool bAllPositive = nXSize > 0 && nYSize > 0 && nZSize > 0;
        if (!bAllZero && !bAllPositive)
            throw new InvalidDataException("PicoVox dimensions must either all be zero or all be positive.");
        if (bAllZero)
        {
            if (nXOrigin != 0 || nYOrigin != 0 || nZOrigin != 0)
                throw new InvalidDataException("An empty PicoVox volume must have zero origins.");
            return;
        }

        if ((long)nXSize * nYSize > Array.MaxLength)
            throw new InvalidDataException("A PicoVox slice exceeds the managed sample-buffer limit.");
        if (1L + 2L * nXSize > Array.MaxLength)
            throw new InvalidDataException("A PicoVox scanline exceeds the managed buffer limit.");

        ValidateExtent(nXOrigin, nXSize, "X");
        ValidateExtent(nYOrigin, nYSize, "Y");
        ValidateExtent(nZOrigin, nZSize, "Z");
    }

    static void ValidateExtent(int nOrigin, int nSize, string strAxis)
    {
        long nMaximum = (long)nOrigin + nSize - 1L;
        if (nMaximum > int.MaxValue)
            throw new InvalidDataException($"The PicoVox {strAxis} extent exceeds signed 32-bit index space.");
    }

    static int nReadInt32(string strLine, string strProperty, bool bNonNegative)
    {
        string strValue = strReadValue(strLine, strProperty);
        if (!int.TryParse(strValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int nValue) ||
            strValue != nValue.ToString(CultureInfo.InvariantCulture) ||
            (bNonNegative && nValue < 0))
        {
            throw new InvalidDataException($"Invalid PicoVox property {strProperty}.");
        }
        return nValue;
    }

    static float fReadSingle(string strLine, string strProperty)
    {
        string strValue = strReadValue(strLine, strProperty);
        if (!float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float fValue) ||
            !float.IsFinite(fValue) || fValue <= 0f ||
            strValue != fValue.ToString("R", CultureInfo.InvariantCulture))
        {
            throw new InvalidDataException($"Invalid PicoVox property {strProperty}.");
        }
        return fValue;
    }

    static string strReadValue(string strLine, string strProperty)
    {
        string strPrefix = strProperty + ": ";
        if (!strLine.StartsWith(strPrefix, StringComparison.Ordinal) || strLine.Length == strPrefix.Length)
            throw new InvalidDataException($"Expected PicoVox property {strProperty}.");

        string strValue = strLine[strPrefix.Length..];
        if (char.IsWhiteSpace(strValue[0]) || char.IsWhiteSpace(strValue[^1]))
            throw new InvalidDataException($"Invalid whitespace in PicoVox property {strProperty}.");
        return strValue;
    }
}
