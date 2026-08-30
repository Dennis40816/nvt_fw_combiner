using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>OS-backed exclusive writer for one exact canonical state path.</summary>
internal static class FileSystemVersionManagerWriteLease
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);

    internal static async ValueTask<VersionManagerWriteLeaseResult> TryAcquireAsync(
        string statePath,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(waitTimeout, TimeSpan.Zero);
        string lockPath;
        try
        {
            lockPath = GetLockPath(statePath);
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
                return new(
                    VersionManagerWriteLeaseIssue.None,
                    new FileSystemVersionManagerWriteLeaseCustody(statePath, stream));
#pragma warning restore CA2000
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

    internal static string GetLockPath(string statePath)
    {
        string identity = NormalizeIdentityPath(statePath);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        string directory = Path.GetDirectoryName(Path.GetFullPath(statePath)) ??
            throw new ArgumentException("Version-manager state has no parent directory.", nameof(statePath));
        return Path.Combine(
            directory,
            $".{Path.GetFileName(statePath)}.{hash[..24]}.writer.lock");
    }

    internal static string NormalizeIdentityPath(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private sealed class FileSystemVersionManagerWriteLeaseCustody(
        string statePath,
        FileStream stream) : IVersionManagerWriteLeaseCustody
    {
        private readonly string _statePath = NormalizeIdentityPath(statePath);
        private FileStream? _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        public bool HoldsStatePath(string statePath)
        {
            FileStream? current = Volatile.Read(ref _stream);
            return current is not null &&
                !current.SafeFileHandle.IsClosed &&
                !current.SafeFileHandle.IsInvalid &&
                string.Equals(_statePath, NormalizeIdentityPath(statePath), StringComparison.Ordinal);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _stream, null)?.Dispose();
        }
    }

    private static bool IsSharingViolation(IOException exception)
    {
        int nativeCode = exception.HResult & 0xFFFF;
        return nativeCode is 32 or 33;
    }
}
