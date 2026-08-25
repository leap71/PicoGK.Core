// SPDX-License-Identifier: Apache-2.0
using System.Numerics;
using System.Text;

namespace PicoGK;

/// <summary>
/// Owns one PicoGK native geometry context.
///
/// Mesh and Voxels objects created through a Library belong to its native
/// lifetime domain. Disposing the Library releases any remaining native
/// geometry objects owned by it.
/// </summary>
public sealed class Library : IDisposable
{
    internal readonly SafeLibraryHandle hNative;

    static readonly TimeSpan c_oMemoryCheckInterval = TimeSpan.FromSeconds(10);

    readonly object m_mtxMemory = new();
    Timer? m_oTimerMemCheck;
    long m_nUsedMemory;
    bool m_bDisposed;

    /// <summary>Voxel edge length used by this Library, in millimetres.</summary>
    public float fVoxelSizeMM { get; }

    /// <summary>Creates a Library with the specified voxel size in millimetres.</summary>
    public Library(float fVoxelSizeMM)
    {
        if (!float.IsFinite(fVoxelSizeMM) || fVoxelSizeMM <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(fVoxelSizeMM), "Voxel size must be finite and greater than zero.");

        hNative           = NativeApi.hCreateLibrary(fVoxelSizeMM);
        this.fVoxelSizeMM = fVoxelSizeMM;
        m_oTimerMemCheck  = oCreateMemoryTimer(this);
    }

    /// <summary>Returns the PicoGK native runtime name.</summary>
    public static string strName()
    {
        StringBuilder str = new(NativeApi.nInfoStringLength);
        NativeApi.Check(NativeMethods.Library_bGetName(str));
        return str.ToString();
    }

    /// <summary>Returns the PicoGK native runtime version.</summary>
    public static string strVersion()
    {
        StringBuilder str = new(NativeApi.nInfoStringLength);
        NativeApi.Check(NativeMethods.Library_bGetVersion(str));
        return str.ToString();
    }

    /// <summary>Returns native runtime build information.</summary>
    public static string strBuildInfo()
    {
        StringBuilder str = new(NativeApi.nInfoStringLength);
        NativeApi.Check(NativeMethods.Library_bGetBuildInfo(str));
        return str.ToString();
    }

    /// <summary>Returns total native memory owned by this Library, in bytes.</summary>
    public long nTotalMemUsage()
    {
        NativeApi.Check(NativeMethods.Library_bGetTotalMemUsage(hNative, out long nBytes));
        return nBytes;
    }

    /// <summary>Returns native memory owned by Mesh objects, in bytes.</summary>
    public long nMeshesMemUsage()
    {
        NativeApi.Check(NativeMethods.Library_bGetMeshesMemUsage(hNative, out long nBytes));
        return nBytes;
    }

    /// <summary>Returns native memory owned by Voxels objects, in bytes.</summary>
    public long nVoxelsMemUsage()
    {
        NativeApi.Check(NativeMethods.Library_bGetVoxelsMemUsage(hNative, out long nBytes));
        return nBytes;
    }

    /// <summary>Returns the number of native Mesh objects owned by this Library.</summary>
    public long nMeshesAllocated()
    {
        NativeApi.Check(NativeMethods.Library_bGetMeshesAllocated(hNative, out long nCount));
        return nCount;
    }

    /// <summary>Returns the number of native Voxels objects owned by this Library.</summary>
    public long nVoxelsAllocated()
    {
        NativeApi.Check(NativeMethods.Library_bGetVoxelsAllocated(hNative, out long nCount));
        return nCount;
    }

    /// <summary>Converts voxel-index coordinates to world coordinates in millimetres.</summary>
    public Vector3 vecVoxelsToMm(Vector3 vecVoxelCoordinate)
    {
        NativeApi.Check(NativeMethods.Library_bVoxelsToMm(hNative, in vecVoxelCoordinate, out Vector3 vecMmCoordinate));
        return vecMmCoordinate;
    }

    /// <summary>Converts voxel-index coordinates to world coordinates in millimetres.</summary>
    public Vector3 vecVoxelsToMm(int nX, int nY, int nZ)
        => vecVoxelsToMm(new Vector3(nX, nY, nZ));

    /// <summary>Converts world coordinates in millimetres to the nearest integer voxel coordinates.</summary>
    public Vector3 vecMmToVoxels(Vector3 vecMmCoordinate)
    {
        NativeApi.Check(NativeMethods.Library_bMmToVoxels(hNative, in vecMmCoordinate, out Vector3 vecVoxelCoordinate));
        return vecVoxelCoordinate;
    }

    ~Library()
    {
        DisposeCore();
    }

    /// <summary>
    /// Releases the native Library and all remaining native geometry objects
    /// owned by it.
    /// </summary>
    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    void DisposeCore()
    {
        // Dispose the timer first so no new monitor callbacks are scheduled.
        // A callback already queued may still run and is synchronized below.
        Timer? oTimer = Interlocked.Exchange(ref m_oTimerMemCheck, null);
        oTimer?.Dispose();

        lock (m_mtxMemory)
        {
            if (m_bDisposed)
                return;

            m_bDisposed = true;

            // Balance all pressure previously registered for native memory.
            if (m_nUsedMemory > 0)
            {
                GC.RemoveMemoryPressure(m_nUsedMemory);
                m_nUsedMemory = 0;
            }

            // SafeHandle performs native destruction on both Dispose and finalization.
            hNative.Dispose();
        }
    }

    static Timer oCreateMemoryTimer(Library lib)
    {
        // A normal Timer callback capturing lib would keep the Library alive and
        // prevent its finalizer from ever becoming eligible. Keep only a weak
        // reference and let the timer dispose itself after the Library is gone.
        WeakReference<Library> oWeakLibrary = new(lib);
        Timer? oTimer = null;

        oTimer = new Timer(_ =>
        {
            if (oWeakLibrary.TryGetTarget(out Library? oLibrary))
            {
                try
                {
                    oLibrary.MonitorMemory();
                }
                catch
                {
                    // Memory-pressure tracking is advisory. Never allow a timer
                    // callback to terminate the process because monitoring failed.
                }
            }
            else
            {
                oTimer?.Dispose();
            }
        }, null, c_oMemoryCheckInterval, c_oMemoryCheckInterval);

        return oTimer;
    }

    void MonitorMemory()
    {
        lock (m_mtxMemory)
        {
            // A callback may already have been queued when Dispose() was called.
            if (m_bDisposed)
                return;

            NativeApi.Check(NativeMethods.Library_bGetTotalMemUsage(hNative, out long nNewMemory));
            long nDiff = nNewMemory - m_nUsedMemory;

            if (nDiff > 0)
                GC.AddMemoryPressure(nDiff);
            else if (nDiff < 0)
                GC.RemoveMemoryPressure(-nDiff);

            m_nUsedMemory = nNewMemory;
        }
    }
}
