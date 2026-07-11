using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Rejects non-regular filesystem objects before and after opening bundle files.</summary>
internal static partial class RegularFileGuard
{
    private const uint WindowsDiskFileType = 0x0001;
    private const int UnixFileTypeMask = 0xF000;
    private const int UnixRegularFileType = 0x8000;

    internal static void RequirePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OperatingSystem.IsWindows())
        {
            FileAttributes attributes = File.GetAttributes(path);
            const FileAttributes rejectedAttributes =
                FileAttributes.Device | FileAttributes.Directory | FileAttributes.ReparsePoint;
            if ((attributes & rejectedAttributes) == 0)
            {
                return;
            }

            throw NotRegularFile(path);
        }

        if (UnixLStat(path, out UnixFileStatus status) != 0)
        {
            throw NativeInspectionFailure(path);
        }

        if ((status.Mode & UnixFileTypeMask) != UnixRegularFileType)
        {
            throw NotRegularFile(path);
        }
    }

    internal static void RequireOpenHandle(SafeFileHandle handle, string displayPath)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);
        if (handle.IsInvalid || handle.IsClosed)
        {
            throw new IOException($"Bundle file '{displayPath}' has no valid open handle.");
        }

        if (OperatingSystem.IsWindows())
        {
            if (WindowsGetFileType(handle) != WindowsDiskFileType)
            {
                throw NotRegularFile(displayPath);
            }

            return;
        }

        if (UnixFStat(handle, out UnixFileStatus status) != 0)
        {
            throw NativeInspectionFailure(displayPath);
        }

        if ((status.Mode & UnixFileTypeMask) != UnixRegularFileType)
        {
            throw NotRegularFile(displayPath);
        }
    }

    private static UnauthorizedAccessException NotRegularFile(string path)
    {
        return new UnauthorizedAccessException(
            $"Bundle file '{path}' must be a regular filesystem file.");
    }

    private static IOException NativeInspectionFailure(string path)
    {
        return new IOException(
            $"Could not inspect bundle file '{path}' (native error {Marshal.GetLastPInvokeError()}).");
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileType", SetLastError = true)]
    private static partial uint WindowsGetFileType(SafeFileHandle handle);

    [LibraryImport(
        "System.Native",
        EntryPoint = "SystemNative_LStat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixLStat(string path, out UnixFileStatus status);

    [LibraryImport("System.Native", EntryPoint = "SystemNative_FStat", SetLastError = true)]
    private static partial int UnixFStat(SafeFileHandle handle, out UnixFileStatus status);

    // Stable System.Native FileStatus ABI used by the pinned .NET runtime, not a platform struct stat.
    [StructLayout(LayoutKind.Sequential)]
    private struct UnixFileStatus
    {
        internal int Flags;
        internal int Mode;
        internal uint Uid;
        internal uint Gid;
        internal long Size;
        internal long ATime;
        internal long ATimeNsec;
        internal long MTime;
        internal long MTimeNsec;
        internal long CTime;
        internal long CTimeNsec;
        internal long BirthTime;
        internal long BirthTimeNsec;
        internal long Dev;
        internal long RDev;
        internal long Ino;
        internal uint UserFlags;
    }
}
