using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>OS-backed exclusive writer for one exact managed-root/state pair.</summary>
internal static class FileSystemVersionManagerWriteLease
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);

    internal static async ValueTask<VersionManagerWriteLeaseResult> TryAcquireAsync(
        string statePath,
        string managedRoot,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(waitTimeout, TimeSpan.Zero);
        string lockPath;
        try
        {
            lockPath = GetLockPath(statePath, managedRoot);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(VersionManagerWriteLeaseIssue.Unavailable);
        }

        long started = Environment.TickCount64;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
#pragma warning disable CA2000 // Ownership transfers to VersionManagerWriteLeaseResult.
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
#pragma warning restore CA2000
                return new(VersionManagerWriteLeaseIssue.None, stream);
            }
            catch (UnauthorizedAccessException)
            {
                return new(VersionManagerWriteLeaseIssue.Unavailable);
            }
            catch (IOException exception) when (IsSharingViolation(exception))
            {
                TimeSpan elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - started);
                if (elapsed >= waitTimeout)
                {
                    return new(VersionManagerWriteLeaseIssue.Busy);
                }
                TimeSpan remaining = waitTimeout - elapsed;
                await Task.Delay(
                    remaining < RetryInterval ? remaining : RetryInterval,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return new(VersionManagerWriteLeaseIssue.Unavailable);
            }
        }
    }

    internal static string GetLockPath(string statePath, string managedRoot)
    {
        string state = NormalizeIdentityPath(statePath);
        string root = NormalizeIdentityPath(managedRoot);
        string identity = state + "\n" + root;
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        string directory = Path.GetDirectoryName(Path.GetFullPath(statePath)) ??
            throw new ArgumentException("Version-manager state has no parent directory.", nameof(statePath));
        return Path.Combine(
            directory,
            $".{Path.GetFileName(statePath)}.{hash[..24]}.writer.lock");
    }

    private static string NormalizeIdentityPath(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private static bool IsSharingViolation(IOException exception)
    {
        int nativeCode = exception.HResult & 0xFFFF;
        return nativeCode is 32 or 33;
    }
}
