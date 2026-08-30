using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32.SafeHandles;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed partial class WindowsStablePathCustody
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The duplicate of the exact held root transfers into the returned custody; every failure disposes it.")]
    internal static WindowsStableCustodyResult TryCaptureImmutableTreeFromHeldDirectory(
        string absoluteRoot,
        SafeFileHandle heldRoot,
        WindowsStableTreeLimits limits,
        CancellationToken cancellationToken = default)
    {
        return TryCaptureTreeFromHeldDirectory(
            absoluteRoot,
            heldRoot,
            limits,
            deleteCapable: false,
            cancellationToken);
    }

    internal static WindowsStableCustodyIssue TryDeleteExactTreeFromHeldDirectory(
        string absoluteRoot,
        SafeFileHandle heldRoot,
        WindowsStableOwnedTreeSnapshot expected,
        WindowsStableTreeLimits limits,
        CancellationToken cancellationToken = default)
    {
        WindowsStableCustodyResult captured = TryCaptureTreeFromHeldDirectory(
            absoluteRoot,
            heldRoot,
            limits,
            deleteCapable: true,
            cancellationToken);
        if (!captured.IsAcquired)
        {
            return captured.Issue;
        }
        using WindowsStablePathCustody custody = captured.Custody!;
        return custody.MatchesOwnedSnapshot(expected) &&
               custody.RevalidateClosedTree()
            ? custody.DeleteHeldTree()
            : WindowsStableCustodyIssue.Changed;
    }

    internal static bool TrySnapshotOwnedTree(
        IReadOnlyDictionary<string, SafeFileHandle> files,
        IReadOnlyDictionary<string, SafeFileHandle> directories,
        out WindowsStableOwnedTreeSnapshot? snapshot)
    {
        var fileIdentities = new Dictionary<string, WindowsStablePathIdentity>(
            ManagedPathSafety.PathComparer);
        var directoryIdentities = new Dictionary<string, WindowsStablePathIdentity>(
            ManagedPathSafety.PathComparer);
        foreach ((string path, SafeFileHandle handle) in files)
        {
            if (!TryGetIdentity(handle, out WindowsStablePathIdentity identity))
            {
                snapshot = null;
                return false;
            }
            fileIdentities.Add(path, identity);
        }
        foreach ((string path, SafeFileHandle handle) in directories)
        {
            if (!TryGetIdentity(handle, out WindowsStablePathIdentity identity))
            {
                snapshot = null;
                return false;
            }
            directoryIdentities.Add(path, identity);
        }
        snapshot = new(fileIdentities, directoryIdentities);
        return true;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The duplicate of the exact held root transfers into the returned custody; every failure disposes it.")]
    private static WindowsStableCustodyResult TryCaptureTreeFromHeldDirectory(
        string absoluteRoot,
        SafeFileHandle heldRoot,
        WindowsStableTreeLimits limits,
        bool deleteCapable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heldRoot);
        if (!OperatingSystem.IsWindows() || heldRoot.IsInvalid || heldRoot.IsClosed ||
            !TryNormalizeAbsoluteLocalPath(absoluteRoot, out string root) ||
            !HasExpectedType(heldRoot, directory: true))
        {
            return WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.InvalidPath);
        }

        var handles = new List<SafeFileHandle>();
        var files = new Dictionary<string, HeldEntry>(ManagedPathSafety.PathComparer);
        var identities = new List<(string Path, SafeFileHandle Handle)>();
        var topology = new Dictionary<string, string[]>(ManagedPathSafety.PathComparer);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SafeFileHandle rootHandle = Duplicate(heldRoot);
            handles.Add(rootHandle);
            identities.Add((root, rootHandle));
            if (!IsSameIdentity(heldRoot, rootHandle))
            {
                DisposeHandles(handles);
                return WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.Changed);
            }
            WindowsStableCustodyIssue captured = CaptureTree(
                root,
                rootHandle,
                handles,
                files,
                identities,
                topology,
                limits,
                deleteCapable,
                cancellationToken);
            if (captured != WindowsStableCustodyIssue.None ||
                !RevalidateIdentities(identities))
            {
                DisposeHandles(handles);
                return WindowsStableCustodyResult.Failure(
                    captured == WindowsStableCustodyIssue.None
                        ? WindowsStableCustodyIssue.Changed
                        : captured);
            }
            return new(
                new WindowsStablePathCustody(
                    root,
                    rootHandle,
                    handles,
                    files,
                    identities,
                    topology),
                WindowsStableCustodyIssue.None);
        }
        catch (OperationCanceledException)
        {
            DisposeHandles(handles);
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or NotSupportedException)
        {
            DisposeHandles(handles);
            return WindowsStableCustodyResult.Failure(WindowsStableCustodyIssue.Unavailable);
        }
    }

    internal bool MatchesOwnedSnapshot(WindowsStableOwnedTreeSnapshot expected)
    {
        if (_files.Count != expected.Files.Count || _topology.Count != expected.Directories.Count)
        {
            return false;
        }
        foreach ((string relative, HeldEntry entry) in _files)
        {
            if (!expected.Files.TryGetValue(relative, out WindowsStablePathIdentity identity) ||
                !TryGetIdentity(entry.Handle, out WindowsStablePathIdentity actual) ||
                actual != identity)
            {
                return false;
            }
        }
        foreach ((string absolute, SafeFileHandle handle) in _identities)
        {
            if (!IsDirectory(handle))
            {
                continue;
            }
            string relative = Path.GetRelativePath(RootPath, absolute);
            relative = relative == "." ? string.Empty : relative.Replace('\\', '/');
            if (!expected.Directories.TryGetValue(relative, out WindowsStablePathIdentity identity) ||
                !TryGetIdentity(handle, out WindowsStablePathIdentity actual) ||
                actual != identity)
            {
                return false;
            }
        }
        return true;
    }

    private WindowsStableCustodyIssue DeleteHeldTree()
    {
        WindowsStableCustodyIssue issue = WindowsStableCustodyIssue.None;
        (string Path, SafeFileHandle Handle)[] directories =
        [
            .. _identities
                .Where(static identity => IsDirectory(identity.Handle))
                .OrderByDescending(static identity => identity.Path.Length),
        ];
        foreach (HeldEntry file in _files.Values.OrderByDescending(
                     static entry => entry.AbsolutePath.Length))
        {
            if (!MarkDeleteOnClose(file.Handle))
            {
                issue = WindowsStableCustodyIssue.Changed;
            }
            file.Handle.Dispose();
        }
        foreach ((string _, SafeFileHandle directory) in directories)
        {
            if (!MarkDeleteOnClose(directory))
            {
                issue = WindowsStableCustodyIssue.Changed;
            }
            directory.Dispose();
        }
        return issue;
    }
}
