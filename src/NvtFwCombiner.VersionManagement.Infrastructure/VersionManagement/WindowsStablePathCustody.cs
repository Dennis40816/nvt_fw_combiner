using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal enum WindowsStableCustodyIssue
{
    None,
    InvalidPath,
    ReparsePoint,
    AccessDenied,
    Contended,
    Changed,
    Unavailable,
}
internal enum WindowsStableCustodyStage
{
    BeforeRootOpen,
    AfterMissingRootObservation,
    AfterTreeCaptured,
}
internal sealed record WindowsStableCustodyResult(
    WindowsStablePathCustody? Custody,
    WindowsStableCustodyIssue Issue,
    bool IsExactChildMissing = false)
{
    internal bool IsAcquired => Custody is not null && Issue == WindowsStableCustodyIssue.None;
    internal static WindowsStableCustodyResult Failure(WindowsStableCustodyIssue issue)
    {
        return new(null, issue);
    }

    internal static WindowsStableCustodyResult MissingExactChild()
    {
        return new(
            Custody: null,
            Issue: WindowsStableCustodyIssue.Unavailable,
            IsExactChildMissing: true);
    }
}
internal readonly record struct WindowsStableTreeLimits
{
    internal WindowsStableTreeLimits(int maximumFiles, int maximumDirectories, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumFiles);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDirectories);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        MaximumFiles = maximumFiles;
        MaximumDirectories = maximumDirectories;
        MaximumBytes = maximumBytes;
    }

    internal int MaximumFiles { get; }
    internal int MaximumDirectories { get; }
    internal long MaximumBytes { get; }

    internal bool Allows(int files, int directories, long bytes)
    {
        return files >= 0 && directories >= 0 && bytes >= 0 &&
            files <= MaximumFiles && directories <= MaximumDirectories && bytes <= MaximumBytes;
    }

    internal static WindowsStableTreeLimits ForInstalledVersion(long maximumExpandedBytes)
    {
        return new(
            FileSystemManagedVersionRepository.MaximumInstalledFiles,
            FileSystemManagedVersionRepository.MaximumInstalledDirectories,
            checked(maximumExpandedBytes + FileSystemManagedVersionRepository.MaximumAdmissionBytes));
    }

    internal static WindowsStableTreeLimits InstalledVersion { get; } =
        ForInstalledVersion(FileSystemManagedVersionRepository.MaximumExpandedBytes);
}

internal readonly record struct WindowsStablePathIdentity(
    ulong VolumeSerialNumber,
    ulong FileIdLow,
    ulong FileIdHigh);

internal sealed record WindowsStableOwnedTreeSnapshot(
    IReadOnlyDictionary<string, WindowsStablePathIdentity> Files,
    IReadOnlyDictionary<string, WindowsStablePathIdentity> Directories);

/// <summary>Owns one no-follow Windows path binding until disposal.</summary>
internal sealed partial class WindowsStablePathCustody : IDisposable
{
    private readonly Dictionary<string, HeldEntry> _files;
    private readonly List<SafeFileHandle> _handles;
    private readonly List<(string Path, SafeFileHandle Handle)> _identities;
    private readonly Dictionary<string, string[]> _topology;
    private bool _disposed;
    private WindowsStablePathCustody(
        string rootPath,
        SafeFileHandle? rootDirectoryHandle,
        List<SafeFileHandle> handles,
        Dictionary<string, HeldEntry> files,
        List<(string Path, SafeFileHandle Handle)> identities,
        Dictionary<string, string[]> topology)
    {
        RootPath = rootPath;
        RootDirectoryHandle = rootDirectoryHandle;
        _handles = handles;
        _files = files;
        _identities = identities;
        _topology = topology;
    }
    internal string RootPath { get; }
    internal SafeFileHandle? RootDirectoryHandle { get; }
    internal bool HasSameRootIdentity(SafeFileHandle heldRoot)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(heldRoot);
        return RootDirectoryHandle is not null && !heldRoot.IsInvalid && !heldRoot.IsClosed &&
            IsSameIdentity(RootDirectoryHandle, heldRoot);
    }
    internal static WindowsStableCustodyResult TryAcquireFile(
        string absoluteFilePath,
        Action<WindowsStableCustodyStage>? testHook = null,
        CancellationToken cancellationToken = default)
    {
        return TryNormalizeAbsoluteLocalPath(absoluteFilePath, out string filePath) &&
               Path.GetDirectoryName(filePath) is not null
            ? TryAcquire(filePath, Path.GetFileName(filePath), AcquisitionKind.SingleFile, testHook,
                treeLimits: null, cancellationToken: cancellationToken)
            : WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
    }
    internal static WindowsStableCustodyResult TryAcquireImmutableTree(
        string absoluteRoot,
        Action<WindowsStableCustodyStage>? testHook,
        CancellationToken cancellationToken)
    {
        return TryAcquireImmutableTree(
            absoluteRoot,
            testHook,
            WindowsStableTreeLimits.InstalledVersion,
            cancellationToken);
    }
    internal static WindowsStableCustodyResult TryAcquireImmutableTree(
        string absoluteRoot,
        Action<WindowsStableCustodyStage>? testHook = null,
        WindowsStableTreeLimits? treeLimits = null,
        CancellationToken cancellationToken = default)
    {
        return TryNormalizeAbsoluteLocalPath(absoluteRoot, out string root)
            ? TryAcquire(root, relativeFileName: null, AcquisitionKind.ImmutableTree, testHook,
                treeLimits ?? WindowsStableTreeLimits.InstalledVersion, cancellationToken)
            : WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
    }
    internal static WindowsStableCustodyResult TryAcquireWritableParent(
        string absoluteParent,
        Action<WindowsStableCustodyStage>? testHook = null,
        CancellationToken cancellationToken = default)
    {
        return TryNormalizeAbsoluteLocalPath(absoluteParent, out string parent)
            ? TryAcquire(parent, relativeFileName: null, AcquisitionKind.WritableParent, testHook,
                treeLimits: null, cancellationToken: cancellationToken)
            : WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
    }
    internal static WindowsStableCustodyResult TryAcquirePromotableTree(
        string absoluteRoot,
        Action<WindowsStableCustodyStage>? testHook = null,
        WindowsStableTreeLimits? treeLimits = null,
        CancellationToken cancellationToken = default)
    {
        return TryNormalizeAbsoluteLocalPath(absoluteRoot, out string root)
            ? TryAcquire(root, relativeFileName: null, AcquisitionKind.PromotableTree, testHook,
                treeLimits ?? WindowsStableTreeLimits.InstalledVersion, cancellationToken)
            : WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
    }

    internal string GetAbsoluteFilePath(string relativePath)
    {
        ThrowIfDisposed();
        string normalized = NormalizeRelativeFile(relativePath);
        return _files.TryGetValue(normalized, out HeldEntry? entry)
            ? entry.AbsolutePath
            : throw new FileNotFoundException("The file is not part of the held tree.", relativePath);
    }
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The duplicated handle transfers to FileStream; construction failures dispose it locally.")]
    internal FileStream OpenReadOnlyFile(string relativePath)
    {
        ThrowIfDisposed();
        string normalized = NormalizeRelativeFile(relativePath);
        if (!_files.TryGetValue(normalized, out HeldEntry? entry))
        {
            throw new FileNotFoundException("The file is not part of the held tree.", relativePath);
        }
        SafeFileHandle duplicate = Duplicate(entry.Handle);
        try
        {
            var stream = new FileStream(duplicate, FileAccess.Read, bufferSize: 4096, isAsync: false);
            duplicate = null!;
            return stream;
        }
        finally
        {
            duplicate?.Dispose();
        }
    }

    internal bool RevalidateClosedTree()
    {
        ThrowIfDisposed();
        try
        {
            if (!RevalidateIdentities(_identities))
            {
                return false;
            }
            foreach ((string directory, string[] expected) in _topology)
            {
                if (!TryEnumerateChildNames(directory, expected.Length + 1, out string[] actual) ||
                    !expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        DisposeHandles(_handles);
    }

    private static WindowsStableCustodyResult TryAcquire(
        string rootPath,
        string? relativeFileName,
        AcquisitionKind kind,
        Action<WindowsStableCustodyStage>? testHook,
        WindowsStableTreeLimits? treeLimits,
        CancellationToken cancellationToken)
    {
        return OperatingSystem.IsWindows()
            ? TryAcquireWindows(rootPath, relativeFileName, kind, testHook, treeLimits, cancellationToken)
            : WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.Unavailable);
    }

    private static partial WindowsStableCustodyResult TryAcquireWindows(
        string rootPath,
        string? relativeFileName,
        AcquisitionKind kind,
        Action<WindowsStableCustodyStage>? testHook,
        WindowsStableTreeLimits? treeLimits,
        CancellationToken cancellationToken);

    private static WindowsStableCustodyIssue OpenAncestorChain(
        string volumeRoot,
        string parentPath,
        List<SafeFileHandle> handles,
        List<(string Path, SafeFileHandle Handle)> identities,
        out SafeFileHandle? parentHandle)
    {
        parentHandle = null;
        SafeFileHandle volume = NativeMethods.CreateFile(
            volumeRoot,
            NativeMethods.ReadAttributes | NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.BackupSemantics | NativeMethods.OpenReparsePoint,
            0);
        if (volume.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            volume.Dispose();
            return MapWin32Error(error);
        }
        if (!HasExpectedType(volume, directory: true))
        {
            volume.Dispose();
            return WindowsStableCustodyIssue.ReparsePoint;
        }
        handles.Add(volume);
        identities.Add((volumeRoot, volume));
        parentHandle = volume;

        string relative = Path.GetRelativePath(volumeRoot, parentPath);
        if (relative == ".")
        {
            return WindowsStableCustodyIssue.None;
        }
        string current = volumeRoot;
        foreach (string component in relative.Split(Path.DirectorySeparatorChar))
        {
            int status = OpenRelative(
                parentHandle,
                component,
                directory: true,
                writableParent: false,
                allowWriteShare: true,
                allowDeleteShare: false,
                requestDeleteAccess: false,
                out SafeFileHandle handle);
            if (status != NativeMethods.StatusSuccess)
            {
                handle.Dispose();
                return MapStatus(status);
            }
            if (!HasExpectedType(handle, directory: true))
            {
                handle.Dispose();
                return WindowsStableCustodyIssue.ReparsePoint;
            }
            current = Path.Combine(current, component);
            handles.Add(handle);
            identities.Add((current, handle));
            parentHandle = handle;
        }
        return WindowsStableCustodyIssue.None;
    }

    private static WindowsStableCustodyIssue CaptureTree(
        string rootPath,
        SafeFileHandle rootHandle,
        List<SafeFileHandle> handles,
        Dictionary<string, HeldEntry> files,
        List<(string Path, SafeFileHandle Handle)> identities,
        Dictionary<string, string[]> topology,
        WindowsStableTreeLimits limits,
        bool deleteCapable,
        CancellationToken cancellationToken)
    {
        return CaptureTree(
            rootPath,
            rootHandle,
            handles,
            files,
            identities,
            topology,
            limits,
            allowDeleteShare: deleteCapable,
            requestDeleteAccess: deleteCapable,
            cancellationToken);
    }

    private static WindowsStableCustodyIssue CaptureTree(
        string rootPath,
        SafeFileHandle rootHandle,
        List<SafeFileHandle> handles,
        Dictionary<string, HeldEntry> files,
        List<(string Path, SafeFileHandle Handle)> identities,
        Dictionary<string, string[]> topology,
        WindowsStableTreeLimits limits,
        bool allowDeleteShare,
        bool requestDeleteAccess,
        CancellationToken cancellationToken)
    {
        int fileCount = 0;
        int directoryCount = 0;
        long totalBytes = 0;
        var pending = new Queue<(string Path, SafeFileHandle Handle)>();
        pending.Enqueue((rootPath, rootHandle));
        while (pending.TryDequeue(out (string Path, SafeFileHandle Handle) directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int maximumChildren = checked(
                limits.MaximumFiles - fileCount +
                limits.MaximumDirectories - directoryCount +
                1);
            if (!TryEnumerateChildNames(
                    directory.Path,
                    maximumChildren,
                    out string[] first))
            {
                return WindowsStableCustodyIssue.Unavailable;
            }
            foreach (string name in first)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string absolute = Path.Combine(directory.Path, name);
                FileAttributes attributes = File.GetAttributes(absolute);
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return WindowsStableCustodyIssue.ReparsePoint;
                }
                int status = OpenRelative(
                    directory.Handle,
                    name,
                    isDirectory,
                    writableParent: false,
                    allowWriteShare: false,
                    allowDeleteShare,
                    requestDeleteAccess,
                    out SafeFileHandle child);
                if (status != NativeMethods.StatusSuccess)
                {
                    child.Dispose();
                    return MapStatus(status);
                }
                if (!HasExpectedType(child, isDirectory))
                {
                    child.Dispose();
                    return WindowsStableCustodyIssue.ReparsePoint;
                }
                handles.Add(child);
                identities.Add((absolute, child));
                if (isDirectory)
                {
                    if (++directoryCount > limits.MaximumDirectories)
                    {
                        return WindowsStableCustodyIssue.Unavailable;
                    }
                    pending.Enqueue((absolute, child));
                }
                else
                {
                    if (++fileCount > limits.MaximumFiles)
                    {
                        return WindowsStableCustodyIssue.Unavailable;
                    }
                    long length = RandomAccess.GetLength(child);
                    if (length < 0 ||
                        totalBytes > limits.MaximumBytes - length)
                    {
                        return WindowsStableCustodyIssue.Unavailable;
                    }
                    totalBytes += length;
                    string relative = NormalizeRelativeFile(Path.GetRelativePath(rootPath, absolute));
                    if (!files.TryAdd(relative, new(absolute, child)))
                    {
                        return WindowsStableCustodyIssue.Changed;
                    }
                }
            }
            if (!TryEnumerateChildNames(directory.Path, first.Length + 1, out string[] second) ||
                !first.SequenceEqual(second, StringComparer.Ordinal))
            {
                return WindowsStableCustodyIssue.Changed;
            }
            topology.Add(directory.Path, second);
        }
        return WindowsStableCustodyIssue.None;
    }

    internal static bool TryEnumerateChildNames(
        string directory,
        int maximumCount,
        out string[] names)
    {
        var captured = new List<string>(Math.Min(maximumCount, 256));
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            if (captured.Count >= maximumCount)
            {
                names = [];
                return false;
            }
            captured.Add(Path.GetFileName(path) ?? string.Empty);
        }
        captured.Sort(StringComparer.Ordinal);
        names = [.. captured];
        return true;
    }

    private static bool TryNormalizeAbsoluteLocalPath(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("\\\\", StringComparison.Ordinal) ||
            path.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            path.StartsWith("\\??\\", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            normalized = Path.GetFullPath(path);
            string? root = Path.GetPathRoot(normalized);
            if (root is null ||
                ManagedPathSafety.PathComparer.Equals(
                    Path.TrimEndingDirectorySeparator(normalized),
                    Path.TrimEndingDirectorySeparator(root)))
            {
                normalized = string.Empty;
                return false;
            }
            if (OperatingSystem.IsWindows())
            {
                if (normalized.AsSpan(2).Contains(':') || new DriveInfo(root).DriveType != DriveType.Fixed)
                {
                    normalized = string.Empty;
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static string NormalizeRelativeFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string value = relativePath.Replace('\\', '/');
        return Path.IsPathFullyQualified(relativePath) || value.StartsWith('/') ||
               value.Split('/').Any(static component =>
                   component.Length == 0 || component is "." or ".." || component.Contains(':'))
            ? throw new ArgumentException(
                "A normalized relative file path is required.",
                nameof(relativePath))
            : value;
    }

    internal static bool IsSingleComponent(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal) &&
            value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    internal static WindowsStableCustodyIssue MapStatus(int status)
    {
        return status switch
        {
            NativeMethods.StatusAccessDenied => WindowsStableCustodyIssue.AccessDenied,
            NativeMethods.StatusSharingViolation => WindowsStableCustodyIssue.Contended,
            NativeMethods.StatusObjectNameCollision or NativeMethods.StatusDeletePending =>
                WindowsStableCustodyIssue.Changed,
            NativeMethods.StatusReparsePointEncountered or
            NativeMethods.StatusNotADirectory or
            NativeMethods.StatusFileIsADirectory => WindowsStableCustodyIssue.ReparsePoint,
            NativeMethods.StatusObjectNameInvalid => WindowsStableCustodyIssue.InvalidPath,
            NativeMethods.StatusObjectNameNotFound or NativeMethods.StatusObjectPathNotFound =>
                WindowsStableCustodyIssue.Unavailable,
            _ => WindowsStableCustodyIssue.Unavailable,
        };
    }

    internal static bool IsExactChildMissingStatus(int status)
    {
        return status is NativeMethods.StatusObjectNameNotFound or
            NativeMethods.StatusObjectPathNotFound;
    }

    private static WindowsStableCustodyIssue MapWin32Error(int error)
    {
        return error switch
        {
            NativeMethods.ErrorAccessDenied => WindowsStableCustodyIssue.AccessDenied,
            NativeMethods.ErrorSharingViolation => WindowsStableCustodyIssue.Contended,
            NativeMethods.ErrorFileNotFound or NativeMethods.ErrorPathNotFound =>
                WindowsStableCustodyIssue.Unavailable,
            _ => WindowsStableCustodyIssue.Unavailable,
        };
    }

    private static void DisposeHandles(IEnumerable<SafeFileHandle> handles)
    {
        foreach (SafeFileHandle handle in handles.Reverse())
        {
            handle.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record HeldEntry(string AbsolutePath, SafeFileHandle Handle);

    private enum AcquisitionKind
    {
        SingleFile,
        ImmutableTree,
        WritableParent,
        PromotableTree,
    }
}
