// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;

namespace PicoGK;

/// <summary>
/// Base class for PicoGK's opaque 64-bit native handles.
/// PicoGK.Core currently targets 64-bit runtimes only.
/// </summary>
internal abstract class PicoGKSafeHandle : SafeHandle
{
    protected PicoGKSafeHandle() : base(IntPtr.Zero, true)
    {
        if (IntPtr.Size != sizeof(ulong))
            throw new PlatformNotSupportedException("PicoGK.Core native handles require a 64-bit process.");
    }

    protected PicoGKSafeHandle(ulong hNative) : this()
    {
        SetHandle(unchecked((IntPtr)(long)hNative));
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal ulong hNative => unchecked((ulong)handle.ToInt64());
}

internal sealed class SafeLibraryHandle : PicoGKSafeHandle
{
    private SafeLibraryHandle(ulong hNative) : base(hNative) { }

    internal static SafeLibraryHandle oFromNative(ulong hNative)
    {
        if (hNative == 0)
            throw new ArgumentException("Cannot create a SafeLibraryHandle from a zero native handle.", nameof(hNative));
        return new SafeLibraryHandle(hNative);
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            return NativeMethods.Library_bDestroyInstanceRaw(hNative);
        }
        catch
        {
            // SafeHandle.ReleaseHandle must never throw, including during
            // process shutdown when native library loading may already fail.
            return false;
        }
    }
}

internal sealed class SafeMeshHandle : PicoGKSafeHandle
{
    readonly ulong m_hLibrary;

    private SafeMeshHandle(ulong hLibrary, ulong hNative) : base(hNative)
    {
        m_hLibrary = hLibrary;
    }

    internal static SafeMeshHandle oFromNative(SafeLibraryHandle hLibrary, ulong hNative)
    {
        if (hNative == 0)
            throw new ArgumentException("Cannot create a SafeMeshHandle from a zero native handle.", nameof(hNative));
        return new SafeMeshHandle(NativeApi.hGetNative(hLibrary), hNative);
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            // Deliberately use the captured raw Library handle. Child handles
            // must not keep the Library alive; Library disposal is the lifetime
            // backstop and native destruction is idempotent afterwards.
            return NativeMethods.Mesh_bDestroyRaw(m_hLibrary, hNative);
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class SafeVoxelsHandle : PicoGKSafeHandle
{
    readonly ulong m_hLibrary;

    private SafeVoxelsHandle(ulong hLibrary, ulong hNative) : base(hNative)
    {
        m_hLibrary = hLibrary;
    }

    internal static SafeVoxelsHandle oFromNative(SafeLibraryHandle hLibrary, ulong hNative)
    {
        if (hNative == 0)
            throw new ArgumentException("Cannot create a SafeVoxelsHandle from a zero native handle.", nameof(hNative));
        return new SafeVoxelsHandle(NativeApi.hGetNative(hLibrary), hNative);
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            return NativeMethods.Voxels_bDestroyRaw(m_hLibrary, hNative);
        }
        catch
        {
            return false;
        }
    }
}
