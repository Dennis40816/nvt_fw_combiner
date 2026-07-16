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
            const FileAttributes rejectedAttributes =
                FileAttributes.Device | FileAttributes.Directory | FileAttributes.ReparsePoint;
            if ((File.GetAttributes(path) & rejectedAttributes) != 0)
            {
                throw NotRegularFile(path);
            }

            return;
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

    internal static void RequireOpenHandle(
        SafeFileHandle handle,
        string expectedFullPath,
        string displayPath)
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

            RequireWindowsHandlePath(handle, expectedFullPath, displayPath);
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

        if (UnixLStat(Path.GetFullPath(expectedFullPath), out UnixFileStatus expectedStatus) != 0)
        {
            throw NativeInspectionFailure(displayPath);
        }

        if (status.Dev != expectedStatus.Dev || status.Ino != expectedStatus.Ino)
        {
            throw HandlePathMismatch(displayPath);
        }
    }

    private static unsafe void RequireWindowsHandlePath(
        SafeFileHandle handle,
        string expectedFullPath,
        string displayPath)
    {
        const int maximumPathLength = 32768;
        char* buffer = stackalloc char[maximumPathLength];
        uint length = WindowsGetFinalPathNameByHandle(
            handle,
            buffer,
            maximumPathLength,
            0);
        if (length is 0 or >= maximumPathLength)
        {
            throw NativeInspectionFailure(displayPath);
        }

        string actualPath = NormalizeWindowsFinalPath(new string(buffer, 0, checked((int)length)));
        string expectedPath = NormalizeWindowsFinalPath(Path.GetFullPath(expectedFullPath));
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw HandlePathMismatch(displayPath);
        }
    }

    private static string NormalizeWindowsFinalPath(string path)
    {
        return path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[@"\\?\UNC\".Length..]
            : path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
                ? path[@"\\?\".Length..]
                : path;
    }

    private static UnauthorizedAccessException NotRegularFile(string path)
    {
        return new($"Bundle file '{path}' must be a regular filesystem file.");
    }

    private static IOException NativeInspectionFailure(string path)
    {
        return new($"Could not inspect bundle file '{path}' (native error {Marshal.GetLastPInvokeError()}).");
    }

    private static UnauthorizedAccessException HandlePathMismatch(string path)
    {
        return new($"Bundle file '{path}' open handle does not match the validated path.");
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileType", SetLastError = true)]
    private static partial uint WindowsGetFileType(SafeFileHandle handle);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static unsafe partial uint WindowsGetFinalPathNameByHandle(
        SafeFileHandle handle,
        char* filePath,
        uint filePathLength,
        uint flags);

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
