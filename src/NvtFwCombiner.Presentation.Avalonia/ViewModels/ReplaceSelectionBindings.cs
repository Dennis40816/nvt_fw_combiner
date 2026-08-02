namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Explicit state projections consumed by the Replace selection child.</summary>
internal sealed record ReplaceSelectionBindings(
    Func<ShellTextResources> Text,
    Func<string> SelectedIc,
    Func<string> SelectedNumber,
    Func<string> SelectedMode,
    Func<bool> IsGeneralModeSelected,
    Func<bool> IsCtrlRamModeSelected,
    Func<bool> CanRun,
    Func<bool> CanBuild,
    Func<string> ReadinessStatus,
    Func<FirmwareSlotViewModel> BaseSlot,
    Func<IEnumerable<FirmwareSlotViewModel>> Slots,
    Func<IEnumerable<GeneralReplaceMappingViewModel>> GeneralMappings);
