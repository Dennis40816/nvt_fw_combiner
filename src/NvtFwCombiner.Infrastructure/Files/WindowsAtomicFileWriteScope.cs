using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Holds a verified Windows parent handle and an undeletable handle-relative
/// anchor so string-based staging cannot be redirected by rename or reparse.
/// </summary>
internal sealed partial class WindowsAtomicFileWriteScope : IAtomicFileWriteScope
{
    private const uint DirectoryAccess = 0x000000A3;
    private const uint DeleteAccess = 0x00010000;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDoNotReparse = 0x00001000;
    private const uint FileCreate = 2;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileDeleteOnClose = 0x00001000;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileAttributeTagInfoClass = 9;
    private readonly SafeFileHandle _anchorHandle;
    private readonly SafeFileHandle _directoryHandle;
    private readonly string _destinationPath;

    private WindowsAtomicFileWriteScope(
        string destinationPath,
        SafeFileHandle directoryHandle,
        SafeFileHandle anchorHandle)
    {
        _destinationPath = destinationPath;
        _directoryHandle = directoryHandle;
        _anchorHandle = anchorHandle;
    }

    internal static WindowsAtomicFileWriteScope Open(string destinationPath)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string? directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new DirectoryNotFoundException(
                $"Saved Rule target directory was not found: {directoryPath}");
        }

        SafeFileHandle directoryHandle = OpenDirectory(directoryPath);
        SafeFileHandle? anchorHandle = null;
        try
        {
            RequireExactDirectory(directoryHandle, directoryPath);
            anchorHandle = CreateAnchor(
                directoryHandle,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.lease");
            RequireExactDirectory(directoryHandle, directoryPath);
            return new WindowsAtomicFileWriteScope(
                fullPath,
                directoryHandle,
                anchorHandle);
        }
        catch
        {
            anchorHandle?.Dispose();
            directoryHandle.Dispose();
            throw;
        }
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(_destinationPath)!;
        RequireExactDirectory(_directoryHandle, directory);
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 131_072,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan |
                             FileOptions.WriteThrough))
            {
                await stream.WriteAsync(documentBytes, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            RequireExactDirectory(_directoryHandle, directory);
            File.Move(tempPath, _destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public void Dispose()
    {
        _anchorHandle.Dispose();
        _directoryHandle.Dispose();
    }

    private static SafeFileHandle OpenDirectory(string directoryPath)
    {
        SafeFileHandle handle = WindowsCreateFile(
            directoryPath,
            DirectoryAccess,
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            0);
        if (!handle.IsInvalid)
        {
            return handle;
        }

        int nativeError = Marshal.GetLastPInvokeError();
        handle.Dispose();
        throw new IOException(
            $"Could not open Saved Rule directory '{directoryPath}' (native error {nativeError}).");
    }

    private static SafeFileHandle CreateAnchor(
        SafeFileHandle directoryHandle,
        string fileName)
    {
        nint nameBuffer = Marshal.StringToHGlobalUni(fileName);
        nint unicodeStringPointer = 0;
        try
        {
            var unicodeString = new UnicodeString(
                checked((ushort)(fileName.Length * sizeof(char))),
                checked((ushort)((fileName.Length + 1) * sizeof(char))),
                nameBuffer);
            unicodeStringPointer = Marshal.AllocHGlobal(
                Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(
                unicodeString,
                unicodeStringPointer,
                fDeleteOld: false);
            var attributes = new ObjectAttributes(
                Marshal.SizeOf<ObjectAttributes>(),
                directoryHandle.DangerousGetHandle(),
                unicodeStringPointer,
                ObjectCaseInsensitive | ObjectDoNotReparse,
                0,
                0);
            int status = WindowsNtCreateFile(
                out SafeFileHandle anchorHandle,
                DeleteAccess | SynchronizeAccess,
                ref attributes,
                out _,
                0,
                0,
                FileShareRead | FileShareWrite,
                FileCreate,
                FileFlagOpenReparsePoint |
                FileDeleteOnClose |
                FileNonDirectoryFile |
                FileSynchronousIoNonAlert,
                0,
                0);
            if (status == 0 && !anchorHandle.IsInvalid)
            {
                return anchorHandle;
            }

            anchorHandle.Dispose();
            throw new IOException(
                $"Could not anchor Saved Rule directory (NTSTATUS 0x{status:X8}).");
        }
        finally
        {
            if (unicodeStringPointer != 0)
            {
                Marshal.FreeHGlobal(unicodeStringPointer);
            }

            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static unsafe void RequireExactDirectory(
        SafeFileHandle directoryHandle,
        string expectedPath)
    {
        var attributes = new FileAttributeTagInfo();
        if (WindowsGetFileInformationByHandleEx(
                directoryHandle,
                FileAttributeTagInfoClass,
                out attributes,
                (uint)Marshal.SizeOf<FileAttributeTagInfo>()) == 0)
        {
            throw NativeInspectionFailure(expectedPath);
        }

        if ((attributes.FileAttributes & FileAttributeReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException(
                "Saved Rule target directories cannot be reparse points.");
        }

        const int maximumPathLength = 32_768;
        char* buffer = stackalloc char[maximumPathLength];
        uint length = WindowsGetFinalPathNameByHandle(
            directoryHandle,
            buffer,
            maximumPathLength,
            0);
        if (length is 0 or >= maximumPathLength)
        {
            throw NativeInspectionFailure(expectedPath);
        }

        string actualPath = NormalizeWindowsHandlePath(
            new string(buffer, 0, checked((int)length)));
        if (!StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(expectedPath),
                Path.GetFullPath(actualPath)))
        {
            throw new UnauthorizedAccessException(
                "Saved Rule target directory changed during secure open.");
        }
    }

    private static string NormalizeWindowsHandlePath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string devicePrefix = @"\\?\";
        return path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[uncPrefix.Length..]
            : path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase)
                ? path[devicePrefix.Length..]
                : path;
    }

    private static IOException NativeInspectionFailure(string path)
    {
        return new IOException(
            $"Could not inspect Saved Rule directory '{path}' (native error {Marshal.GetLastPInvokeError()}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct UnicodeString(
        ushort Length,
        ushort MaximumLength,
        nint Buffer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ObjectAttributes(
        int Length,
        nint RootDirectory,
        nint ObjectName,
        uint Attributes,
        nint SecurityDescriptor,
        nint SecurityQualityOfService);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct IoStatusBlock(
        nint Status,
        nint Information);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        internal uint FileAttributes;
        internal uint ReparseTag;
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    private static partial SafeFileHandle WindowsCreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetFileInformationByHandleEx",
        SetLastError = true)]
    private static partial int WindowsGetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        int fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        SetLastError = true)]
    private static unsafe partial uint WindowsGetFinalPathNameByHandle(
        SafeFileHandle fileHandle,
        char* filePath,
        uint filePathLength,
        uint flags);

    [LibraryImport("ntdll.dll", EntryPoint = "NtCreateFile")]
    private static partial int WindowsNtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        nint allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        nint extendedAttributes,
        uint extendedAttributesLength);
}
