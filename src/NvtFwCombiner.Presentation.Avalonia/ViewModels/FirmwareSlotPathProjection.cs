namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Projects selected firmware-slot paths into workflow input dictionaries.</summary>
internal static class FirmwareSlotPathProjection
{
    internal static void Add(
        IDictionary<string, string> paths,
        string addressSpaceId,
        FirmwareSlotViewModel slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.FilePath))
        {
            paths[addressSpaceId] = slot.FilePath;
        }
    }
}
