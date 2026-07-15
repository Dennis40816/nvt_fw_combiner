using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Applies a number token only after the Bootstrap reader finds one NVT Backup FWConfig.
    /// This is shared by Merge and Replace slot loading.
    /// </summary>
    private void TryApplyVerifiedFirmwareContext(FirmwareSlotViewModel slot)
    {
        if (!slot.HasFile || string.IsNullOrWhiteSpace(slot.FilePath) ||
            slot.SlotKind is not (FirmwareSlotKind.Tp or FirmwareSlotKind.Base))
        {
            return;
        }

        WorkbenchFirmwareContextSuggestion? suggestion = WorkbenchCompositionService.TryReadFirmwareContextSuggestion(
            SelectedIc,
            slot.FilePath);
        if (suggestion is null || string.Equals(SelectedNumber, suggestion.NumberToken, StringComparison.Ordinal))
        {
            return;
        }

        SelectedNumber = suggestion.NumberToken;
        string selectionLabel = NumberSelectionChoices.FirstOrDefault(choice =>
            string.Equals(choice.Token, suggestion.NumberToken, StringComparison.Ordinal))?.DisplayLabel ??
            suggestion.NumberToken;
        SetShellToast(
            Text.ContextUpdatedToastTitle,
            Text.FormatVerifiedFirmwareContextToast(selectionLabel, suggestion.ChipNumber));
    }
}
