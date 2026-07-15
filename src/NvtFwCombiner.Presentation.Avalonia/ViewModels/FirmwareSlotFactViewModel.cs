namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact firmware fact displayed below a selected BIN file name.</summary>
public sealed record FirmwareSlotFactViewModel(string Label, string Value, bool IsWarning = false);
