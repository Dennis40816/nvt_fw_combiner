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

    public static string? GetFirstLocalFilePath(DragEventArgs e)
    {
        return e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault()?.TryGetLocalPath();
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
