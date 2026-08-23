// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Text;

namespace PicoGK;

/// <summary>
/// Mutable sparse signed-distance representation of solid geometry.
///
/// PicoGK Voxels use millimetre world units, negative-inside SDF convention,
/// and a fixed narrow-band half-width of three voxels.
/// </summary>
public sealed class Voxels : IDisposable
{
    public enum ESliceAxis
    {
        X,
        Y,
        Z
    }

    internal readonly Library lib;
    internal readonly SafeVoxelsHandle hNative;

    /// <summary>
    /// Creates an empty Voxels object.
    /// </summary>
    public Voxels(Library lib)
        : this(lib, hCreateEmpty(lib))
    { }

    /// <summary>
    /// Creates a deep copy of an existing Voxels object.
    /// </summary>
    public Voxels(Voxels voxSource)
        : this(libFromSource(voxSource), hCreateCopy(voxSource))
    { }

    internal Voxels(Library lib, SafeVoxelsHandle hNative)
    {
        ArgumentNullException.ThrowIfNull(lib);
        this.lib     = lib;
        this.hNative = hNative;
    }

    public void Dispose() => hNative.Dispose();

    /// <summary>
    /// Creates a deep copy of this Voxels object.
    /// </summary>
    public Voxels voxDuplicate() => new(this);

    // Creation ----------------------------------------------------------------

    public static Voxels voxFromSphere(Library lib, Vector3 vecCenter, float fRadiusMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        ulong h = NativeMethods.Voxels_hCreateSphere(lib.hNative, in vecCenter, fRadiusMM);
        return new Voxels(lib, NativeApi.hVoxels(lib.hNative, h));
    }

    public static Voxels voxFromCapsule(Library lib,
                                        Vector3 vecStart,
                                        Vector3 vecEnd,
                                        float fRadiusMM)
        => voxFromCapsule(lib, vecStart, fRadiusMM, vecEnd, fRadiusMM);

    public static Voxels voxFromCapsule(Library lib,
                                        Vector3 vecStart,
                                        float fStartRadiusMM,
                                        Vector3 vecEnd,
                                        float fEndRadiusMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        ulong h = NativeMethods.Voxels_hCreateCapsule(
            lib.hNative, in vecStart, in vecEnd, fStartRadiusMM, fEndRadiusMM);
        return new Voxels(lib, NativeApi.hVoxels(lib.hNative, h));
    }

    public static Voxels voxFromSpheres(Library lib,
                                        ReadOnlySpan<Vector3> avecCenters,
                                        float fRadiusMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return new Voxels(lib, NativeApi.hCreateSpheres(lib.hNative, avecCenters, fRadiusMM));
    }

    public static Voxels voxFromSpheres(Library lib,
                                        ReadOnlySpan<Vector3> avecCenters,
                                        ReadOnlySpan<float> afRadiiMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return new Voxels(lib, NativeApi.hCreateSpheres(lib.hNative, avecCenters, afRadiiMM));
    }

    public static Voxels voxFromTubes(Library lib,
                                      ReadOnlySpan<Vector3> avecVertices,
                                      ReadOnlySpan<Segment> asegSegments,
                                      float fRadiusMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return new Voxels(lib, NativeApi.hCreateTubes(
            lib.hNative, avecVertices, asegSegments, fRadiusMM));
    }

    public static Voxels voxFromTubes(Library lib,
                                      ReadOnlySpan<Vector3> avecVertices,
                                      ReadOnlySpan<Segment> asegSegments,
                                      ReadOnlySpan<float> afVertexRadiiMM)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return new Voxels(lib, NativeApi.hCreateTubes(
            lib.hNative, avecVertices, asegSegments, afVertexRadiiMM));
    }

    public static Voxels voxFromMesh(Mesh msh)
    {
        ArgumentNullException.ThrowIfNull(msh);
        ulong h = NativeMethods.Voxels_hCreateFromMesh(msh.lib.hNative, msh.hNative);
        return new Voxels(msh.lib, NativeApi.hVoxels(msh.lib.hNative, h));
    }

    public static Voxels voxFromMeshShell(Mesh msh, float fRadiusMM)
    {
        ArgumentNullException.ThrowIfNull(msh);
        ulong h = NativeMethods.Voxels_hCreateMeshShell(msh.lib.hNative, msh.hNative, fRadiusMM);
        return new Voxels(msh.lib, NativeApi.hVoxels(msh.lib.hNative, h));
    }

    /// <summary>
    /// Converts this Voxels object to an immutable Mesh.
    /// </summary>
    public Mesh mshAsMesh()
    {
        ulong h = NativeMethods.Mesh_hCreateFromVoxels(lib.hNative, hNative);
        return new Mesh(lib, NativeApi.hMesh(lib.hNative, h));
    }

    /// <summary>
    /// Returns the bounding box of the actual zero isosurface.
    ///
    /// This intentionally derives the box from the extracted Mesh rather than
    /// from OpenVDB active-value bounds. Narrow-band grids can retain values
    /// away from the actual surface, so active topology is not a sufficiently
    /// clear definition of geometric extent.
    /// </summary>
    public BBox3 oBoundingBox()
    {
        using Mesh msh = mshAsMesh();
        return msh.oBoundingBox();
    }

    // Boolean operations ------------------------------------------------------

    /// <summary>
    /// In-place Boolean union. C# 14 resolves += to this instance operator,
    /// avoiding creation of a copy of the left operand.
    /// </summary>
    public void operator +=(Voxels voxOperand)
    {
        CheckSameLibrary(voxOperand);
        NativeApi.Check(NativeMethods.Voxels_bBoolAdd(lib.hNative, hNative, voxOperand.hNative));
    }

    /// <summary>In-place Boolean subtraction.</summary>
    public void operator -=(Voxels voxOperand)
    {
        CheckSameLibrary(voxOperand);
        NativeApi.Check(NativeMethods.Voxels_bBoolSubtract(lib.hNative, hNative, voxOperand.hNative));
    }

    /// <summary>In-place Boolean intersection.</summary>
    public void operator &=(Voxels voxOperand)
    {
        CheckSameLibrary(voxOperand);
        NativeApi.Check(NativeMethods.Voxels_bBoolIntersect(lib.hNative, hNative, voxOperand.hNative));
    }

    /// <summary>Returns the Boolean union without modifying either operand.</summary>
    public static Voxels operator +(Voxels voxA, Voxels voxB)
    {
        Voxels voxResult = voxCreateBooleanCopy(voxA, voxB);
        try { voxResult += voxB; return voxResult; }
        catch { voxResult.Dispose(); throw; }
    }

    /// <summary>Returns the Boolean difference without modifying either operand.</summary>
    public static Voxels operator -(Voxels voxA, Voxels voxB)
    {
        Voxels voxResult = voxCreateBooleanCopy(voxA, voxB);
        try { voxResult -= voxB; return voxResult; }
        catch { voxResult.Dispose(); throw; }
    }

    /// <summary>Returns the Boolean intersection without modifying either operand.</summary>
    public static Voxels operator &(Voxels voxA, Voxels voxB)
    {
        Voxels voxResult = voxCreateBooleanCopy(voxA, voxB);
        try { voxResult &= voxB; return voxResult; }
        catch { voxResult.Dispose(); throw; }
    }

    // Morphology --------------------------------------------------------------

    /// <summary>Offsets the surface in-place. Positive values grow the solid.</summary>
    public void Offset(float fDistMM)
        => NativeApi.Check(NativeMethods.Voxels_bOffset(lib.hNative, hNative, fDistMM));

    public Voxels voxOffset(float fDistMM)
    {
        Voxels vox = new(this);
        try
        {
            vox.Offset(fDistMM);
            return vox;
        }
        catch
        {
            vox.Dispose();
            throw;
        }
    }

    public void DoubleOffset(float fDist1MM, float fDist2MM)
        => NativeApi.Check(NativeMethods.Voxels_bDoubleOffset(
            lib.hNative, hNative, fDist1MM, fDist2MM));

    public Voxels voxDoubleOffset(float fDist1MM, float fDist2MM)
    {
        Voxels vox = new(this);
        try
        {
            vox.DoubleOffset(fDist1MM, fDist2MM);
            return vox;
        }
        catch
        {
            vox.Dispose();
            throw;
        }
    }

    public void TripleOffset(float fDistMM)
        => NativeApi.Check(NativeMethods.Voxels_bTripleOffset(lib.hNative, hNative, fDistMM));

    public Voxels voxTripleOffset(float fDistMM)
    {
        Voxels vox = new(this);
        try
        {
            vox.TripleOffset(fDistMM);
            return vox;
        }
        catch
        {
            vox.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Applies a fillet-like smoothing operation while returning to the original surface offset.
    /// </summary>
    public void Fillet(float fRoundingMM)
        => DoubleOffset(fRoundingMM, -fRoundingMM);

    public Voxels voxFillet(float fRoundingMM)
    {
        Voxels vox = new(this);
        try
        {
            vox.Fillet(fRoundingMM);
            return vox;
        }
        catch
        {
            vox.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a shell whose wall extends inward for a negative offset or outward
    /// for a positive offset.
    /// </summary>
    public Voxels voxShell(float fOffsetMM)
    {
        if (fOffsetMM < 0f)
        {
            using Voxels voxInner = voxOffset(fOffsetMM);
            Voxels voxResult = new(this);
            try
            {
                voxResult -= voxInner;
                return voxResult;
            }
            catch
            {
                voxResult.Dispose();
                throw;
            }
        }

        Voxels voxOuter = voxOffset(fOffsetMM);
        try
        {
            voxOuter -= this;
            return voxOuter;
        }
        catch
        {
            voxOuter.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a shell between two offsets of the current surface.
    /// </summary>
    public Voxels voxShell(float fNegativeOffsetMM,
                           float fPositiveOffsetMM,
                           float fSmoothInnerMM = 0f)
    {
        if (fNegativeOffsetMM > fPositiveOffsetMM)
            (fNegativeOffsetMM, fPositiveOffsetMM) = (fPositiveOffsetMM, fNegativeOffsetMM);

        using Voxels voxInner = voxOffset(fNegativeOffsetMM);
        if (fSmoothInnerMM > 0f)
            voxInner.TripleOffset(fSmoothInnerMM);

        Voxels voxOuter = voxOffset(fPositiveOffsetMM);
        try
        {
            voxOuter -= voxInner;
            return voxOuter;
        }
        catch
        {
            voxOuter.Dispose();
            throw;
        }
    }

    // Queries -----------------------------------------------------------------

    public bool bIsEmpty()
    {
        NativeApi.Check(NativeMethods.Voxels_bGetIsEmpty(lib.hNative, hNative, out bool bEmpty));
        return bEmpty;
    }

    public bool bIsInside(in Vector3 vecTestPoint)
    {
        NativeApi.Check(NativeMethods.Voxels_bGetIsInside(
            lib.hNative, hNative, in vecTestPoint, out bool bInside));
        return bInside;
    }

    public bool bIsEqual(Voxels voxOther)
    {
        CheckSameLibrary(voxOther);
        NativeApi.Check(NativeMethods.Voxels_bGetIsEqual(
            lib.hNative, hNative, voxOther.hNative, out bool bEqual));
        return bEqual;
    }

    public long nMemUsage()
    {
        NativeApi.Check(NativeMethods.Voxels_bGetMemUsage(lib.hNative, hNative, out long nBytes));
        return nBytes;
    }

    public float fVoxelSizeMM
    {
        get
        {
            NativeApi.Check(NativeMethods.Voxels_bGetVoxelSize(
                lib.hNative, hNative, out float fVoxelSizeMM));
            return fVoxelSizeMM;
        }
    }

    public float fCalculateVolume()
    {
        NativeApi.Check(NativeMethods.Voxels_bGetVolume(
            lib.hNative, hNative, out float fVolumeMM3));
        return fVolumeMM3;
    }

    public Vector3 vecSurfaceNormal(in Vector3 vecSurfacePoint)
    {
        NativeApi.Check(NativeMethods.Voxels_bGetSurfaceNormal(
            lib.hNative, hNative, in vecSurfacePoint, out Vector3 vecNormal));
        return vecNormal;
    }

    public bool bClosestPointOnSurface(in Vector3 vecSearch,
                                       out Vector3 vecSurfacePoint,
                                       out float fDistanceMM)
    {
        NativeApi.Check(NativeMethods.Voxels_bClosestPointOnSurface(
            lib.hNative, hNative, in vecSearch,
            out bool bFound, out vecSurfacePoint, out fDistanceMM));
        return bFound;
    }

    public Vector3 vecClosestPointOnSurface(in Vector3 vecSearch)
        => vecClosestPointOnSurface(in vecSearch, out _);

    public Vector3 vecClosestPointOnSurface(in Vector3 vecSearch, out float fDistanceMM)
    {
        if (!bClosestPointOnSurface(in vecSearch, out Vector3 vecSurfacePoint, out fDistanceMM))
            throw new InvalidOperationException("Cannot find a closest surface point on an empty Voxels object.");
        return vecSurfacePoint;
    }

    public bool bRayCastToSurface(in Vector3 vecSearch,
                                  in Vector3 vecDirection,
                                  out Vector3 vecSurfacePoint)
    {
        NativeApi.Check(NativeMethods.Voxels_bRayCastToSurface(
            lib.hNative, hNative, in vecSearch, in vecDirection,
            out bool bHit, out vecSurfacePoint));
        return bHit;
    }

    public Vector3 vecRayCastToSurface(in Vector3 vecSearch, in Vector3 vecDirection)
    {
        if (!bRayCastToSurface(in vecSearch, in vecDirection, out Vector3 vecSurfacePoint))
            throw new InvalidOperationException("Ray does not intersect the Voxels surface.");
        return vecSurfacePoint;
    }

    public VoxelDimensions oVoxelDimensions()
    {
        NativeApi.Check(NativeMethods.Voxels_bGetVoxelDimensions(
            lib.hNative, hNative,
            out int nXOrigin, out int nYOrigin, out int nZOrigin,
            out int nXSize, out int nYSize, out int nZSize));

        return new VoxelDimensions(
            nXOrigin, nYOrigin, nZOrigin,
            nXSize, nYSize, nZSize);
    }

    public int nSliceCount(ESliceAxis eAxis = ESliceAxis.Z)
    {
        VoxelDimensions oDim = oVoxelDimensions();
        return eAxis switch
        {
            ESliceAxis.X => oDim.nXSize,
            ESliceAxis.Y => oDim.nYSize,
            _            => oDim.nZSize
        };
    }

    /// <summary>
    /// Allocates a reusable SDF slice of the correct dimensions for the selected axis.
    /// </summary>
    public SdfSlice slcCreateSlice(out int nSliceCount, ESliceAxis eAxis = ESliceAxis.Z)
    {
        VoxelDimensions oDim = oVoxelDimensions();
        switch (eAxis)
        {
            case ESliceAxis.X:
                nSliceCount = oDim.nXSize;
                return new SdfSlice(oDim.nYSize, oDim.nZSize);

            case ESliceAxis.Y:
                nSliceCount = oDim.nYSize;
                return new SdfSlice(oDim.nXSize, oDim.nZSize);

            default:
                nSliceCount = oDim.nZSize;
                return new SdfSlice(oDim.nXSize, oDim.nYSize);
        }
    }

    /// <summary>
    /// Fills a reusable canonical SDF slice. Slice indices are relative to the
    /// active voxel bounding-box origin. Z slices store +X across each row and
    /// rows in -Y; X slices store +Y across each row and rows in -Z; Y slices
    /// store +X across each row and rows in -Z.
    /// </summary>
    public void GetSlice(int nSlice, SdfSlice slc, ESliceAxis eAxis = ESliceAxis.Z)
    {
        ArgumentNullException.ThrowIfNull(slc);
        NativeApi.GetSdfSlice(lib.hNative, hNative, nSlice, slc, eAxis);
    }

    /// <summary>
    /// Fills a reusable Z slice sampled at a fractional index-space Z coordinate.
    /// </summary>
    public void GetInterpolatedZSlice(float fZSlice, SdfSlice slc)
    {
        ArgumentNullException.ThrowIfNull(slc);
        NativeApi.GetInterpolatedZSdfSlice(lib.hNative, hNative, fZSlice, slc);
    }

    /// <summary>
    /// Returns an empty string for a healthy level set, otherwise the OpenVDB diagnostic text.
    /// </summary>
    public string strDiagnose()
    {
        StringBuilder str = new(NativeApi.nInfoStringLength);
        NativeApi.Check(NativeMethods.Voxels_bDiagnose(
            lib.hNative, hNative, out bool bHealthy, str));
        return bHealthy ? string.Empty : str.ToString();
    }

    // Helpers -----------------------------------------------------------------

    void CheckSameLibrary(Voxels voxOther)
    {
        ArgumentNullException.ThrowIfNull(voxOther);
        if (!ReferenceEquals(lib, voxOther.lib))
            throw new InvalidOperationException("Voxels objects belong to different Library instances.");
    }

    static Library libFromSource(Voxels voxSource)
    {
        ArgumentNullException.ThrowIfNull(voxSource);
        return voxSource.lib;
    }

    static SafeVoxelsHandle hCreateEmpty(Library lib)
    {
        ArgumentNullException.ThrowIfNull(lib);
        return NativeApi.hVoxels(lib.hNative, NativeMethods.Voxels_hCreate(lib.hNative));
    }

    static SafeVoxelsHandle hCreateCopy(Voxels voxSource)
    {
        ArgumentNullException.ThrowIfNull(voxSource);
        return NativeApi.hVoxels(voxSource.lib.hNative,
            NativeMethods.Voxels_hCreateCopy(voxSource.lib.hNative, voxSource.hNative));
    }

    static Voxels voxCreateBooleanCopy(Voxels voxA, Voxels voxB)
    {
        ArgumentNullException.ThrowIfNull(voxA);
        ArgumentNullException.ThrowIfNull(voxB);
        voxA.CheckSameLibrary(voxB);
        return new Voxels(voxA);
    }
}
