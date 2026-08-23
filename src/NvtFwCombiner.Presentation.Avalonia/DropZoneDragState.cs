using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static class DropZoneDragState
{
    private const string DragActiveClass = "dragActive";

    public static bool ApplyFileDropEffect(DragEventArgs e)
    {
        bool canDrop = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        return canDrop;
    }

    public static SingleLocalFileDropSelection GetSingleLocalFile(DragEventArgs e)
    {
        IStorageItem[]? items = e.DataTransfer.TryGetFiles();
        if (items is not { Length: 1 })
        {
            return new(SingleLocalFileDropStatus.WrongItemCount, null);
        }

        string? path = (items[0] as IStorageFile)?.TryGetLocalPath();
        return string.IsNullOrWhiteSpace(path)
            ? new(SingleLocalFileDropStatus.NonLocalFile, null)
            : new(SingleLocalFileDropStatus.Accepted, path);
    }

    public static void SetActive(object? sender, bool isActive)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (isActive)
        {
            if (!control.Classes.Contains(DragActiveClass))
            {
                control.Classes.Add(DragActiveClass);
            }
        }
        else
        {
            _ = control.Classes.Remove(DragActiveClass);
        }
    }
}

internal enum SingleLocalFileDropStatus
{
    Accepted,
    WrongItemCount,
    NonLocalFile,
}

internal readonly record struct SingleLocalFileDropSelection(
    SingleLocalFileDropStatus Status,
    string? Path)
{
    public bool IsAccepted =>
        Status == SingleLocalFileDropStatus.Accepted && !string.IsNullOrWhiteSpace(Path);
}
