using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Uses one verified Unix directory descriptor for creation, cleanup, and promotion.</summary>
internal sealed partial class UnixAtomicFileWriteScope : IAtomicFileWriteScope
{
    private const int LinuxCreate = 0x00000040;
    private const int LinuxExclusive = 0x00000080;
    private const int LinuxDirectory = 0x00010000;
    private const int LinuxNoFollow = 0x00020000;
    private const int LinuxCloseOnExec = 0x00080000;
    private const int MacNoFollow = 0x00000100;
    private const int MacCreate = 0x00000200;
    private const int MacExclusive = 0x00000800;
    private const int MacDirectory = 0x00100000;
    private const int MacCloseOnExec = 0x01000000;
    private const int OpenWriteOnly = 1;
    private const uint OwnerReadWriteMode = 0x00000180;
    private const int MacGetPath = 50;
    private const int NotDirectory = 20;
    private const int LinuxSymbolicLinkLoop = 40;
    private const int MacSymbolicLinkLoop = 62;
    private readonly SafeFileHandle _directoryHandle;
    private readonly string _directoryPath;
    private readonly string _destinationName;

    private UnixAtomicFileWriteScope(
        SafeFileHandle directoryHandle,
        string directoryPath,
        string destinationName)
    {
        _directoryHandle = directoryHandle;
        _directoryPath = directoryPath;
        _destinationName = destinationName;
    }

    internal static UnixAtomicFileWriteScope Open(string destinationPath)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string? directoryPath = Path.GetDirectoryName(fullPath);
        return string.IsNullOrWhiteSpace(directoryPath)
            ? throw new DirectoryNotFoundException($"Saved Rule target directory was not found: {directoryPath}")
            : new UnixAtomicFileWriteScope(
                OpenDirectory(directoryPath),
                directoryPath,
                Path.GetFileName(fullPath));
    }

    internal static bool HasExactDirectoryPath(string directoryPath)
    {
        try
        {
            using SafeFileHandle handle = OpenDirectory(directoryPath);
            return true;
        }
        // Path mismatches and ENOTDIR/ELOOP open failures are non-exact; other native errors propagate.
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException exception) when (exception.InnerException is Win32Exception native &&
            (native.NativeErrorCode == NotDirectory ||
                native.NativeErrorCode == (OperatingSystem.IsLinux() ? LinuxSymbolicLinkLoop : MacSymbolicLinkLoop)))
        {
            return false;
        }
    }

    private static SafeFileHandle OpenDirectory(string directoryPath)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Local directory inspection requires Windows, Linux, or macOS.");
        }
        int descriptor = UnixOpen(
            directoryPath,
            DirectoryOpenFlags(),
            0);
        if (descriptor < 0)
        {
            throw NativeFailure("open", directoryPath);
        }

        var handle = new SafeFileHandle(descriptor, ownsHandle: true);
        try
        {
            RequireExactDirectory(handle, directoryPath);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken cancellationToken)
    {
        return WriteCoreAsync(documentBytes, mode: null, cancellationToken);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> documentBytes,
        LocalFileWriteMode mode,
        CancellationToken cancellationToken)
    {
        return WriteCoreAsync(documentBytes, mode, cancellationToken);
    }

    private async ValueTask WriteCoreAsync(
        ReadOnlyMemory<byte> documentBytes,
        LocalFileWriteMode? mode,
        CancellationToken cancellationToken)
    {
        string tempName = $".{_destinationName}.{Guid.NewGuid():N}.tmp";
        int directoryDescriptor = _directoryHandle.DangerousGetHandle().ToInt32();
        int tempDescriptor = UnixOpenAt(
            directoryDescriptor,
            tempName,
            TemporaryOpenFlags(),
            OwnerReadWriteMode);
        if (tempDescriptor < 0)
        {
            throw NativeFailure("openat", tempName);
        }

        try
        {
            using var tempHandle = new SafeFileHandle(
                tempDescriptor,
                ownsHandle: true);
            tempDescriptor = -1;
            await using (var stream = new FileStream(
                             tempHandle,
                             FileAccess.Write,
                             bufferSize: 131_072,
                             isAsync: false))
            {
                await stream.WriteAsync(documentBytes, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            RequireExactDirectory(_directoryHandle, _directoryPath);
            // Explicit write options add the cancellation boundary; legacy callers retain their promotion behavior.
            if (mode.HasValue) { cancellationToken.ThrowIfCancellationRequested(); }
            if (mode == LocalFileWriteMode.CreateNew)
            {
                // linkat atomically admits only an absent destination, using the held directory authority.
                if (UnixLinkAt(directoryDescriptor, tempName, directoryDescriptor, _destinationName, 0) != 0)
                {
                    throw NativeFailure("linkat", _destinationName);
                }
                _ = UnixUnlinkAt(directoryDescriptor, tempName, 0);
            }
            else if (UnixRenameAt(
                    directoryDescriptor,
                    tempName,
                    directoryDescriptor,
                    _destinationName) != 0)
            {
                throw NativeFailure("renameat", _destinationName);
            }

            // The verified directory descriptor is the acquired write authority;
            // successful renameat/linkat is the final, non-fallible commit boundary.
        }
        catch
        {
            if (tempDescriptor >= 0)
            {
                _ = UnixClose(tempDescriptor);
            }

            _ = UnixUnlinkAt(directoryDescriptor, tempName, 0);
            throw;
        }
    }

    public void Dispose()
    {
        _directoryHandle.Dispose();
    }

    private static int DirectoryOpenFlags()
    {
        return OperatingSystem.IsLinux()
            ? LinuxDirectory | LinuxNoFollow | LinuxCloseOnExec
            : MacDirectory | MacNoFollow | MacCloseOnExec;
    }

    private static int TemporaryOpenFlags()
    {
        return OperatingSystem.IsLinux()
            ? OpenWriteOnly |
                LinuxCreate |
                LinuxExclusive |
                LinuxNoFollow |
                LinuxCloseOnExec
            : OpenWriteOnly |
                MacCreate |
                MacExclusive |
                MacNoFollow |
                MacCloseOnExec;
    }

    private static unsafe void RequireExactDirectory(
        SafeFileHandle directoryHandle,
        string expectedPath)
    {
        int descriptor = directoryHandle.DangerousGetHandle().ToInt32();
        string actualPath;
        if (OperatingSystem.IsLinux())
        {
            const int maximumPathLength = 4096;
            byte* buffer = stackalloc byte[maximumPathLength];
            nint length = UnixReadLink(
                $"/proc/self/fd/{descriptor}",
                buffer,
                maximumPathLength);
            if (length is <= 0 or >= maximumPathLength)
            {
                throw NativeFailure("readlink", expectedPath);
            }

            actualPath = Encoding.UTF8.GetString(
                new ReadOnlySpan<byte>(buffer, checked((int)length)));
        }
        else
        {
            const int maximumPathLength = 1024;
            byte* buffer = stackalloc byte[maximumPathLength];
            if (MacFcntlGetPath(descriptor, MacGetPath, buffer) != 0)
            {
                throw NativeFailure("fcntl", expectedPath);
            }

            int length = 0;
            while (length < maximumPathLength && buffer[length] != 0)
            {
                length++;
            }

            actualPath = Encoding.UTF8.GetString(
                new ReadOnlySpan<byte>(buffer, length));
        }

        if (!StringComparer.Ordinal.Equals(
                Path.GetFullPath(expectedPath),
                Path.GetFullPath(actualPath)))
        {
            throw new UnauthorizedAccessException(
                "Saved Rule target directory changed during secure open.");
        }
    }

    private static IOException NativeFailure(string operation, string path)
    {
        int error = Marshal.GetLastPInvokeError();
        return new IOException(
            $"Saved Rule {operation} failed for '{path}' (native error {error}).",
            new Win32Exception(error));
    }

    [LibraryImport(
        "libc",
        EntryPoint = "open",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixOpen(string path, int flags, uint mode);

    [LibraryImport(
        "libc",
        EntryPoint = "openat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixOpenAt(
        int directoryDescriptor,
        string path,
        int flags,
        uint mode);

    [LibraryImport(
        "libc",
        EntryPoint = "renameat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixRenameAt(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath);

    [LibraryImport(
        "libc",
        EntryPoint = "linkat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixLinkAt(
        int oldDirectoryDescriptor,
        string oldPath,
        int newDirectoryDescriptor,
        string newPath,
        int flags);

    [LibraryImport(
        "libc",
        EntryPoint = "unlinkat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int UnixUnlinkAt(
        int directoryDescriptor,
        string path,
        int flags);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int UnixClose(int descriptor);

    [LibraryImport(
        "libc",
        EntryPoint = "readlink",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static unsafe partial nint UnixReadLink(
        string path,
        byte* buffer,
        nuint bufferSize);

    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static unsafe partial int MacFcntlGetPath(
        int descriptor,
        int command,
        byte* buffer);
}
