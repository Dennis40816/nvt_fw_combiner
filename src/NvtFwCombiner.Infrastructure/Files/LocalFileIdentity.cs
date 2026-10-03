using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Metadata-only physical identity using the neighbouring platform file primitives.</summary>
internal static partial class LocalFileIdentity
{
    internal static bool RefersToSameFile(string first, string second)
    {
        if (OperatingSystem.IsWindows())
        {
            using SafeFileHandle firstHandle = OpenWindowsIdentityHandle(first);
            if (firstHandle.IsInvalid)
            {
                return false;
            }
            using SafeFileHandle secondHandle = OpenWindowsIdentityHandle(second);
            if (secondHandle.IsInvalid)
            {
                return false;
            }
            WindowsFileIdentity firstIdentity = ReadWindowsIdentity(firstHandle);
            WindowsFileIdentity secondIdentity = ReadWindowsIdentity(secondHandle);
            return firstIdentity.VolumeSerialNumber == secondIdentity.VolumeSerialNumber &&
                firstIdentity.FileIndexHigh == secondIdentity.FileIndexHigh &&
                firstIdentity.FileIndexLow == secondIdentity.FileIndexLow;
        }
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            (long Device, long Inode)? firstIdentity = RegularFileGuard.ReadUnixIdentity(first);
            if (!firstIdentity.HasValue)
            {
                return false;
            }
            (long Device, long Inode)? secondIdentity = RegularFileGuard.ReadUnixIdentity(second);
            return secondIdentity.HasValue && firstIdentity == secondIdentity;
        }
        throw new PlatformNotSupportedException("Local file identity requires Windows, Linux, or macOS.");
    }

    private static SafeFileHandle OpenWindowsIdentityHandle(string path)
    {
        // No content-read access: an unreadable report still reaches the bounded report loader.
        // FILE_FLAG_BACKUP_SEMANTICS permits metadata inspection of a report path that is a directory;
        // admission remains the report loader's responsibility.
        SafeFileHandle handle = WindowsCreateFile(path, 0, 7, 0, 3, 0x02000000, 0);
        if (!handle.IsInvalid)
        {
            return handle;
        }
        int error = Marshal.GetLastPInvokeError();
        if (error is 2 or 3)
        {
            return handle;
        }
        handle.Dispose();
        throw new IOException($"Cannot open file identity for '{path}'.", new Win32Exception(error));
    }

    private static WindowsFileIdentity ReadWindowsIdentity(SafeFileHandle handle)
    {
        // FILE_ID_INFO includes the complete 128-bit file ID, including ReFS.
        if (WindowsGetFileInformationByHandleEx(handle, 18, out WindowsFileIdentity identity,
            (uint)Marshal.SizeOf<WindowsFileIdentity>()) != 0)
        {
            return identity;
        }

        int error = Marshal.GetLastPInvokeError();
        if (error is not (1 or 50 or 87 or 120 or 124))
        {
            throw new IOException("Cannot verify local file identity.", new Win32Exception(error));
        }

        // Older Windows/filesystems may not implement FileIdInfo. The complete legacy
        // structure carries the volume serial and 64-bit file index.
        return WindowsGetFileInformationByHandle(handle, out WindowsByHandleFileInformation legacy) == 0
            ? throw new IOException("Cannot verify local file identity.",
                new Win32Exception(Marshal.GetLastPInvokeError()))
            : new WindowsFileIdentity
            {
                VolumeSerialNumber = legacy.VolumeSerialNumber,
                FileIndexLow = ((ulong)legacy.FileIndexHigh << 32) | legacy.FileIndexLow,
            };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileIdentity
    {
        internal ulong VolumeSerialNumber;
        internal ulong FileIndexLow;
        internal ulong FileIndexHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsByHandleFileInformation
    {
        internal uint FileAttributes;
        internal uint CreationTimeLow;
        internal uint CreationTimeHigh;
        internal uint LastAccessTimeLow;
        internal uint LastAccessTimeHigh;
        internal uint LastWriteTimeLow;
        internal uint LastWriteTimeHigh;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    private static partial SafeFileHandle WindowsCreateFile(string fileName, uint desiredAccess, uint shareMode,
        nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    private static partial int WindowsGetFileInformationByHandleEx(
        SafeFileHandle handle, int informationClass, out WindowsFileIdentity identity, uint bufferSize);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", SetLastError = true)]
    private static partial int WindowsGetFileInformationByHandle(
        SafeFileHandle handle, out WindowsByHandleFileInformation identity);
}
