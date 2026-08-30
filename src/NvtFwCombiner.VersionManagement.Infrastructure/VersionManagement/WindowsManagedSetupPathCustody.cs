using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NativeMethods = NvtFwCombiner.Infrastructure.VersionManagement.WindowsStablePathCustody.NativeMethods;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed class ManagedSetupPathChangedException(string message) : Exception(message);

internal enum ManagedSetupStagingCleanupState
{
    Absent,
    Observed,
    Deleted,
    RetryableContention,
    OwnedDeletionPending,
    ChangedOrUnsafe,
}

internal readonly record struct ManagedSetupStagingCleanupResult(
    ManagedSetupStagingCleanupState State,
    WindowsStablePathIdentity Identity = default);

/// <summary>Windows-only stable custody for one fresh-install destination transaction.</summary>
internal sealed partial class WindowsManagedSetupPathCustody : IDisposable
{
    private readonly WindowsStablePathCustody _parentCustody;
    private SafeFileHandle? _containerHandle;
    private SafeFileHandle? _stagingRootHandle;
    private string? _stagingRootPath;
    private WindowsStablePathCustody? _treeCustody;
    private readonly Func<int, int>? _stagingDeleteOpenStatusOverride;
    private readonly Func<int, int>? _ownedDeletionObservationStatusOverride;
    private bool _disposed;

    private WindowsManagedSetupPathCustody(
        string managedRoot,
        string parentPath,
        WindowsStablePathCustody parentCustody,
        Func<int, int>? stagingDeleteOpenStatusOverride,
        Func<int, int>? ownedDeletionObservationStatusOverride)
    {
        ManagedRoot = managedRoot;
        ParentPath = parentPath;
        _parentCustody = parentCustody;
        _stagingDeleteOpenStatusOverride = stagingDeleteOpenStatusOverride;
        _ownedDeletionObservationStatusOverride = ownedDeletionObservationStatusOverride;
    }

    internal string ManagedRoot { get; }
    internal string ParentPath { get; }
    internal SafeFileHandle ParentHandle => _parentCustody.RootDirectoryHandle ??
        throw new InvalidOperationException("Writable parent custody did not retain its root handle.");

    internal static ManagedFirstInstallationMaterializationIssue Admit(string managedRoot)
    {
        ManagedFirstInstallationMaterializationIssue acquired = TryAcquire(
            managedRoot,
            out WindowsManagedSetupPathCustody? custody);
        using (custody)
        {
            return acquired != ManagedFirstInstallationMaterializationIssue.None
                ? acquired
                : custody!.AdmitFreshDestination();
        }
    }

    internal ManagedFirstInstallationMaterializationIssue AdmitFreshDestination()
    {
        ThrowIfDisposed();
        string[] reservedNames =
        [
            Path.GetFileName(ManagedRoot),
            Path.GetFileName(FileSystemManagedInstallationRootProbe.GetTransactionMarkerPath(ManagedRoot)),
            Path.GetFileName(FileSystemManagedInstallationRootProbe.GetStagingContainerPath(ManagedRoot)),
        ];
        foreach (string reservedName in reservedNames)
        {
            ManagedFirstInstallationMaterializationIssue observed = ObserveRelativeEntry(
                ParentHandle,
                reservedName);
            if (observed != ManagedFirstInstallationMaterializationIssue.None)
            {
                return observed;
            }
        }
        return ProbeWriteAccess();
    }

    internal ManagedFirstInstallationMaterializationIssue CreateMarker(
        string markerName,
        out SafeFileHandle handle)
    {
        ThrowIfDisposed();
        int status = CreateRelative(
            ParentHandle,
            markerName,
            NativeMethods.ReadData |
                NativeMethods.WriteData |
                NativeMethods.ReadAttributes |
                NativeMethods.WriteAttributes |
                NativeMethods.Delete |
                NativeMethods.Synchronize,
            NativeMethods.ShareRead,
            NativeMethods.NonDirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.WriteThrough |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileCreate,
            out handle);
        return status == NativeMethods.StatusSuccess
            ? ManagedFirstInstallationMaterializationIssue.None
            : MapNtCreateStatus(status);
    }

    internal static ManagedFirstInstallationMaterializationIssue TryAcquire(
        string managedRoot,
        out WindowsManagedSetupPathCustody? custody,
        Func<int, int>? stagingDeleteOpenStatusOverride = null,
        Func<int, int>? ownedDeletionObservationStatusOverride = null)
    {
        custody = null;
        if (!OperatingSystem.IsWindows())
        {
            return ManagedFirstInstallationMaterializationIssue.InvalidDestination;
        }
        ManagedInstallationRootStatus rootStatus = FileSystemManagedInstallationRootProbe.AdmitRoot(
            managedRoot,
            out string root);
        if (rootStatus != ManagedInstallationRootStatus.Absent)
        {
            return rootStatus switch
            {
                ManagedInstallationRootStatus.InvalidDestination =>
                    ManagedFirstInstallationMaterializationIssue.InvalidDestination,
                ManagedInstallationRootStatus.PermissionDenied =>
                    ManagedFirstInstallationMaterializationIssue.PermissionDenied,
                ManagedInstallationRootStatus.Unavailable =>
                    ManagedFirstInstallationMaterializationIssue.StateUnavailable,
                ManagedInstallationRootStatus.Present or ManagedInstallationRootStatus.Residue =>
                    ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
                ManagedInstallationRootStatus.Absent => throw new InvalidOperationException(
                    "An absent root was handled as an admission failure."),
                _ => throw new InvalidOperationException("Root admission returned an undefined status."),
            };
        }

        string? parent = Path.GetDirectoryName(root);
        if (parent is null || !Directory.Exists(parent))
        {
            return ManagedFirstInstallationMaterializationIssue.InvalidDestination;
        }

        try
        {
            WindowsStableCustodyResult acquired =
                WindowsStablePathCustody.TryAcquireWritableParent(parent);
            if (!acquired.IsAcquired)
            {
                return MapCustodyIssue(acquired.Issue);
            }
            custody = new WindowsManagedSetupPathCustody(
                root,
                parent,
                acquired.Custody!,
                stagingDeleteOpenStatusOverride,
                ownedDeletionObservationStatusOverride);
            return ManagedFirstInstallationMaterializationIssue.None;
        }
        catch (UnauthorizedAccessException)
        {
            return ManagedFirstInstallationMaterializationIssue.PermissionDenied;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return ManagedFirstInstallationMaterializationIssue.StateUnavailable;
        }
    }

    internal ManagedFirstInstallationMaterializationIssue CreateStaging(
        string stagingContainer,
        string stagingRoot)
    {
        ThrowIfDisposed();
        if (!IsDirectChild(stagingContainer, ParentPath) ||
            !IsDirectChild(stagingRoot, stagingContainer))
        {
            return ManagedFirstInstallationMaterializationIssue.InvalidDestination;
        }
        int containerStatus = CreateRelativeDirectory(
            ParentHandle,
            Path.GetFileName(stagingContainer),
            allowChildCreation: true,
            out SafeFileHandle containerHandle);
        if (containerStatus != NativeMethods.StatusSuccess)
        {
            containerHandle.Dispose();
            return MapNtCreateStatus(containerStatus);
        }
        _containerHandle = containerHandle;
        if (!IsPlainDirectory(_containerHandle))
        {
            return ManagedFirstInstallationMaterializationIssue.PromotionFailed;
        }

        int stagingStatus = CreateRelativeDirectory(
            _containerHandle,
            Path.GetFileName(stagingRoot),
            allowChildCreation: true,
            out SafeFileHandle stagingHandle);
        if (stagingStatus != NativeMethods.StatusSuccess)
        {
            stagingHandle.Dispose();
            return MapNtCreateStatus(stagingStatus);
        }
        _stagingRootHandle = stagingHandle;
        _stagingRootPath = stagingRoot;
        return !IsPlainDirectory(_stagingRootHandle)
            ? ManagedFirstInstallationMaterializationIssue.PromotionFailed
            : ManagedFirstInstallationMaterializationIssue.None;
    }

    internal WindowsStableRelativeWriteRoot OpenPackageWriteRoot()
    {
        ThrowIfDisposed();
        return _stagingRootHandle is not null && !_stagingRootHandle.IsInvalid &&
               _stagingRootPath is not null
            ? WindowsStableRelativeWriteRoot.FromHeldDirectory(
                _stagingRootPath,
                _stagingRootHandle)
            : throw new IOException("The managed setup package root is unavailable.");
    }

    internal ManagedFirstInstallationMaterializationIssue CaptureClosedTree(
        string root,
        WindowsStableTreeLimits limits,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (_treeCustody is not null || _stagingRootHandle is null ||
            _stagingRootHandle.IsInvalid)
        {
            return ManagedFirstInstallationMaterializationIssue.StateUnavailable;
        }

        WindowsStableCustodyResult acquired =
            WindowsStablePathCustody.TryCaptureImmutableTreeFromHeldDirectory(
            root,
            _stagingRootHandle,
            limits,
            cancellationToken);
        if (!acquired.IsAcquired)
        {
            return MapCustodyIssue(acquired.Issue);
        }
        _treeCustody = acquired.Custody;
        _stagingRootHandle.Dispose();
        _stagingRootHandle = null;
        return ManagedFirstInstallationMaterializationIssue.None;
    }

    internal bool RevalidateClosedTree()
    {
        ThrowIfDisposed();
        return _treeCustody?.RevalidateClosedTree() == true;
    }

    internal ManagedFirstInstallationMaterializationIssue Promote(string finalDirectoryName)
    {
        ThrowIfDisposed();
        if (_stagingRootHandle is null || _containerHandle is null ||
            _stagingRootHandle.IsInvalid || _containerHandle.IsInvalid ||
            !IsSingleFileName(finalDirectoryName))
        {
            return ManagedFirstInstallationMaterializationIssue.PromotionFailed;
        }

        int status = WindowsStablePathCustody.RenameRelative(
            _stagingRootHandle,
            ParentHandle,
            finalDirectoryName);
        return status switch
        {
            NativeMethods.StatusSuccess => MarkDeleteOnClose(_containerHandle)
                ? ManagedFirstInstallationMaterializationIssue.None
                : ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            NativeMethods.StatusObjectNameCollision =>
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            NativeMethods.StatusAccessDenied =>
                ManagedFirstInstallationMaterializationIssue.PermissionDenied,
            _ => ManagedFirstInstallationMaterializationIssue.PromotionFailed,
        };
    }

    internal ManagedSetupStagingCleanupResult ObserveEmptyStagingChild(string path)
    {
        ThrowIfDisposed();
        if (_stagingRootHandle is null || _stagingRootHandle.IsInvalid ||
            _stagingRootPath is null || !IsDirectChild(path, _stagingRootPath))
        {
            return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
        }

        int status = CreateRelative(
            _stagingRootHandle,
            Path.GetFileName(path),
            NativeMethods.ReadData | NativeMethods.ReadAttributes |
                NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite | NativeMethods.ShareDelete,
            NativeMethods.DirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileOpen,
            out SafeFileHandle handle);
        using (handle)
        {
            if (status is NativeMethods.StatusObjectNameNotFound or
                NativeMethods.StatusObjectPathNotFound)
            {
                return new(ManagedSetupStagingCleanupState.Absent);
            }
            if (status != NativeMethods.StatusSuccess || !IsPlainDirectory(handle) ||
                !WindowsStablePathCustody.TryGetIdentity(
                    handle,
                    out WindowsStablePathIdentity identity))
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
            try
            {
                return Directory.EnumerateFileSystemEntries(path).Any()
                    ? new(ManagedSetupStagingCleanupState.ChangedOrUnsafe)
                    : new(ManagedSetupStagingCleanupState.Observed, identity);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
        }
    }

    internal ManagedSetupStagingCleanupResult DeleteObservedEmptyStagingChild(
        string path,
        WindowsStablePathIdentity expectedIdentity,
        Action<string>? beforeDelete)
    {
        ThrowIfDisposed();
        if (_stagingRootHandle is null || _stagingRootHandle.IsInvalid ||
            _stagingRootPath is null || !IsDirectChild(path, _stagingRootPath))
        {
            return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
        }

        int status = CreateRelative(
            _stagingRootHandle,
            Path.GetFileName(path),
            NativeMethods.ReadData |
                NativeMethods.ReadAttributes |
                NativeMethods.Delete |
                NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite,
            NativeMethods.DirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileOpen,
            out SafeFileHandle handle);
        status = _stagingDeleteOpenStatusOverride?.Invoke(status) ?? status;
        bool deleteMarked = false;
        using (handle)
        {
            if (status == NativeMethods.StatusSharingViolation)
            {
                return new(ManagedSetupStagingCleanupState.RetryableContention);
            }
            bool plain = status == NativeMethods.StatusSuccess && IsPlainDirectory(handle);
            WindowsStablePathIdentity actualIdentity = default;
            bool hasIdentity = plain && WindowsStablePathCustody.TryGetIdentity(
                handle,
                out actualIdentity);
            if (status != NativeMethods.StatusSuccess || !plain || !hasIdentity ||
                actualIdentity != expectedIdentity)
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
            try
            {
                beforeDelete?.Invoke(path);
                if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                    return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
                }
                deleteMarked = MarkDeleteOnClose(handle);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
        }
        if (!deleteMarked)
        {
            return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
        }
        ManagedSetupStagingCleanupResult observed = ObserveOwnedStagingDeletion(
            path,
            expectedIdentity);
        return observed.State == ManagedSetupStagingCleanupState.Absent
            ? new(ManagedSetupStagingCleanupState.Deleted, expectedIdentity)
            : observed.State == ManagedSetupStagingCleanupState.OwnedDeletionPending
                ? observed
                : new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
    }

    internal ManagedSetupStagingCleanupResult ObserveOwnedStagingDeletion(
        string path,
        WindowsStablePathIdentity expectedIdentity)
    {
        ThrowIfDisposed();
        if (_stagingRootHandle is null || _stagingRootHandle.IsInvalid ||
            _stagingRootPath is null || !IsDirectChild(path, _stagingRootPath))
        {
            return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
        }

        int status = CreateRelative(
            _stagingRootHandle,
            Path.GetFileName(path),
            NativeMethods.ReadData | NativeMethods.ReadAttributes | NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite | NativeMethods.ShareDelete,
            NativeMethods.DirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileOpen,
            out SafeFileHandle handle);
        status = _ownedDeletionObservationStatusOverride?.Invoke(status) ?? status;
        using (handle)
        {
            if (status is NativeMethods.StatusObjectNameNotFound or
                NativeMethods.StatusObjectPathNotFound)
            {
                return new(ManagedSetupStagingCleanupState.Absent, expectedIdentity);
            }
            if (status == NativeMethods.StatusDeletePending)
            {
                return new(ManagedSetupStagingCleanupState.OwnedDeletionPending, expectedIdentity);
            }
            if (status != NativeMethods.StatusSuccess || !IsPlainDirectory(handle) ||
                !WindowsStablePathCustody.TryGetIdentity(
                    handle,
                    out WindowsStablePathIdentity actualIdentity) ||
                actualIdentity != expectedIdentity)
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
            try
            {
                return Directory.EnumerateFileSystemEntries(path).Any()
                    ? new(ManagedSetupStagingCleanupState.ChangedOrUnsafe)
                    : new(ManagedSetupStagingCleanupState.OwnedDeletionPending, expectedIdentity);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(ManagedSetupStagingCleanupState.ChangedOrUnsafe);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _treeCustody?.Dispose();
        _stagingRootHandle?.Dispose();
        _containerHandle?.Dispose();
        _parentCustody.Dispose();
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "The returned native file handle transfers immediately to FileStream or is disposed on failure.")]
    private ManagedFirstInstallationMaterializationIssue ProbeWriteAccess()
    {
        string probeName = $".nfc-setup-probe-{Guid.NewGuid():N}.tmp";
        string probePath = Path.Combine(ParentPath, probeName);
        try
        {
            int status = CreateRelativeProbeFile(ParentHandle, probeName, out SafeFileHandle handle);
            if (status != NativeMethods.StatusSuccess)
            {
                handle.Dispose();
                return MapProbeStatus(status);
            }
            SafeFileHandle? ownedHandle = handle;
            FileStream stream;
            try
            {
                stream = new FileStream(
                    ownedHandle,
                    FileAccess.Write,
                    bufferSize: 1,
                    isAsync: false);
                ownedHandle = null;
            }
            finally
            {
                ownedHandle?.Dispose();
            }
            using (stream)
            {
                stream.WriteByte(0);
                stream.Flush(flushToDisk: true);
            }
            return File.Exists(probePath)
                ? ManagedFirstInstallationMaterializationIssue.StateUnavailable
                : ManagedFirstInstallationMaterializationIssue.None;
        }
        catch (UnauthorizedAccessException)
        {
            return ManagedFirstInstallationMaterializationIssue.PermissionDenied;
        }
        catch (IOException)
        {
            return ManagedFirstInstallationMaterializationIssue.StateUnavailable;
        }
    }

    private static ManagedFirstInstallationMaterializationIssue MapCustodyIssue(
        WindowsStableCustodyIssue issue)
    {
        return issue switch
        {
            WindowsStableCustodyIssue.InvalidPath or WindowsStableCustodyIssue.ReparsePoint =>
                ManagedFirstInstallationMaterializationIssue.InvalidDestination,
            WindowsStableCustodyIssue.AccessDenied =>
                ManagedFirstInstallationMaterializationIssue.PermissionDenied,
            WindowsStableCustodyIssue.Contended or WindowsStableCustodyIssue.Changed =>
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            WindowsStableCustodyIssue.Unavailable =>
                ManagedFirstInstallationMaterializationIssue.StateUnavailable,
            WindowsStableCustodyIssue.None => throw new InvalidOperationException(
                "Successful custody did not return its owner."),
            _ => throw new InvalidOperationException("Stable custody returned an undefined issue."),
        };
    }

}
