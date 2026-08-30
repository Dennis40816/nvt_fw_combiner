using Microsoft.Win32.SafeHandles;
using NvtFwCombiner.Application.VersionManagement;
using NativeMethods = NvtFwCombiner.Infrastructure.VersionManagement.WindowsStablePathCustody.NativeMethods;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed partial class WindowsManagedSetupPathCustody
{
    private static bool IsPlainDirectory(SafeFileHandle handle)
    {
        return WindowsStablePathCustody.HasExpectedType(handle, directory: true);
    }

    private static bool MarkDeleteOnClose(SafeFileHandle handle)
    {
        return WindowsStablePathCustody.MarkDeleteOnClose(handle);
    }

    private static bool IsDirectChild(string child, string parent)
    {
        return ManagedPathSafety.PathComparer.Equals(
            Path.GetDirectoryName(Path.GetFullPath(child)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)));
    }

    private static bool IsSingleFileName(string value)
    {
        return WindowsStablePathCustody.IsSingleComponent(value);
    }

    internal static int GetRenameFileNameOffset(int pointerSize)
    {
        return WindowsStablePathCustody.GetRenameFileNameOffset(pointerSize);
    }

    private static ManagedFirstInstallationMaterializationIssue MapNtCreateStatus(int status)
    {
        return status switch
        {
            NativeMethods.StatusAccessDenied => ManagedFirstInstallationMaterializationIssue.PermissionDenied,
            NativeMethods.StatusObjectNameCollision or
            NativeMethods.StatusSharingViolation or
            NativeMethods.StatusDeletePending or
            NativeMethods.StatusNotADirectory or
            NativeMethods.StatusFileIsADirectory =>
                ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
            NativeMethods.StatusObjectNameInvalid or
            NativeMethods.StatusObjectPathInvalid or
            NativeMethods.StatusObjectPathNotFound =>
                ManagedFirstInstallationMaterializationIssue.InvalidDestination,
            _ => ManagedFirstInstallationMaterializationIssue.PromotionFailed,
        };
    }

    private static ManagedFirstInstallationMaterializationIssue MapProbeStatus(int status)
    {
        return status switch
        {
            NativeMethods.StatusAccessDenied =>
                ManagedFirstInstallationMaterializationIssue.PermissionDenied,
            NativeMethods.StatusObjectNameInvalid or
            NativeMethods.StatusObjectPathInvalid or
            NativeMethods.StatusObjectPathNotFound or
            NativeMethods.StatusNotADirectory =>
                ManagedFirstInstallationMaterializationIssue.InvalidDestination,
            _ => ManagedFirstInstallationMaterializationIssue.StateUnavailable,
        };
    }

    private static ManagedFirstInstallationMaterializationIssue ObserveRelativeEntry(
        SafeFileHandle parent,
        string name)
    {
        int status = CreateRelative(
            parent,
            name,
            NativeMethods.ReadAttributes | NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite | NativeMethods.ShareDelete,
            NativeMethods.SynchronousIoNonAlert | NativeMethods.OpenReparsePoint,
            NativeMethods.FileOpen,
            out SafeFileHandle handle);
        using (handle)
        {
            return status switch
            {
                NativeMethods.StatusObjectNameNotFound or
                NativeMethods.StatusObjectPathNotFound =>
                    ManagedFirstInstallationMaterializationIssue.None,
                NativeMethods.StatusSuccess or
                NativeMethods.StatusReparsePointEncountered or
                NativeMethods.StatusSharingViolation or
                NativeMethods.StatusDeletePending or
                NativeMethods.StatusNotADirectory or
                NativeMethods.StatusFileIsADirectory =>
                    ManagedFirstInstallationMaterializationIssue.RecoveryRequired,
                NativeMethods.StatusAccessDenied =>
                    ManagedFirstInstallationMaterializationIssue.PermissionDenied,
                NativeMethods.StatusObjectNameInvalid or
                NativeMethods.StatusObjectPathInvalid or
                NativeMethods.StatusNotADirectory =>
                    ManagedFirstInstallationMaterializationIssue.InvalidDestination,
                _ => ManagedFirstInstallationMaterializationIssue.StateUnavailable,
            };
        }
    }

    private static int CreateRelativeDirectory(
        SafeFileHandle parent,
        string directoryName,
        bool allowChildCreation,
        out SafeFileHandle handle)
    {
        return CreateRelative(
            parent,
            directoryName,
            NativeMethods.ReadAttributes |
                (allowChildCreation
                    ? NativeMethods.AddFile | NativeMethods.AddSubdirectory
                    : 0) |
                NativeMethods.Delete |
                NativeMethods.Synchronize,
            NativeMethods.ShareRead | NativeMethods.ShareWrite,
            NativeMethods.DirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileCreate,
            out handle);
    }

    private static int CreateRelativeProbeFile(
        SafeFileHandle parent,
        string fileName,
        out SafeFileHandle handle)
    {
        return CreateRelative(
            parent,
            fileName,
            NativeMethods.WriteData |
                NativeMethods.WriteAttributes |
                NativeMethods.Delete |
                NativeMethods.Synchronize,
            shareAccess: 0,
            NativeMethods.NonDirectoryFile |
                NativeMethods.SynchronousIoNonAlert |
                NativeMethods.DeleteOnClose |
                NativeMethods.WriteThrough |
                NativeMethods.OpenReparsePoint,
            NativeMethods.FileCreate,
            out handle);
    }

    private static int CreateRelative(
        SafeFileHandle parent,
        string name,
        uint desiredAccess,
        uint shareAccess,
        uint createOptions,
        uint createDisposition,
        out SafeFileHandle handle)
    {
        return WindowsStablePathCustody.CreateRelative(
            parent,
            name,
            desiredAccess,
            shareAccess,
            createOptions,
            createDisposition,
            out handle);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
