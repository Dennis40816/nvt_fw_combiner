using System.Runtime.InteropServices;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Owns cleanup only after atomically creating a previously absent run directory.</summary>
internal sealed partial class ExternalStagingDirectory : IDisposable
{
    private string? _ownedPath;

    private ExternalStagingDirectory(string path)
    {
        _ownedPath = path;
    }

    internal static ExternalStagingDirectory? TryAcquire(string path)
    {
        string fullPath = Path.GetFullPath(path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        bool created;
        if (OperatingSystem.IsWindows())
        {
            string nativePath = fullPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? fullPath
                : fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                    ? @"\\?\UNC\" + fullPath[2..]
                    : @"\\?\" + fullPath;
            created = WindowsCreateDirectory(nativePath, IntPtr.Zero);
        }
        else
        {
            created = UnixCreateDirectory(fullPath, 0x1C0) == 0; // owner-only 0700
        }

        if (created)
        {
            return new ExternalStagingDirectory(fullPath);
        }

        int error = Marshal.GetLastPInvokeError();
        return (OperatingSystem.IsWindows() ? error is 80 or 183 : error == 17)
            ? null
            : throw new IOException($"Could not acquire staging directory (native error {error}).");
    }

    public void Dispose()
    {
        string? path = Interlocked.Exchange(ref _ownedPath, null);
        if (path is null)
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateDirectoryW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WindowsCreateDirectory(string path, IntPtr securityAttributes);

    [LibraryImport("libc", EntryPoint = "mkdir", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int UnixCreateDirectory(string path, uint mode);
}
