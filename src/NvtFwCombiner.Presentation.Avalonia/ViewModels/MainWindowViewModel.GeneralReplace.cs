using System.ComponentModel;
using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Sets a local file path for a General Replace mapping row.</summary>
    public void SetGeneralReplaceMappingFile(string mappingId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        GeneralReplaceMappingViewModel? mapping = GeneralReplaceMappings.FirstOrDefault(row =>
            string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return;
        }

        mapping.FilePath = path;
        RefreshCommandState();
    }

    /// <summary>Removes a General Replace mapping row from the editable UI list.</summary>
    public void RemoveGeneralReplaceMappingRow(GeneralReplaceMappingViewModel mapping)
    {
        RemoveGeneralReplaceMapping(mapping);
    }

    private void SetGeneralReplacePatchOverwrite()
    {
        GeneralReplacePatchDraft.Kind = WorkbenchGeneralReplacePatchKind.Overwrite;
    }

    private void SetGeneralReplacePatchFill()
    {
        GeneralReplacePatchDraft.Kind = WorkbenchGeneralReplacePatchKind.Fill;
    }

    private bool CanApplyGeneralReplacePatch()
    {
        return !string.IsNullOrWhiteSpace(GeneralReplacePatchDraft.Value);
    }

    private void ApplyGeneralReplacePatch()
    {
        if (!CanApplyGeneralReplacePatch())
        {
            return;
        }

        _generalReplacePatchCounter++;
        GeneralReplacePatches.Add(new GeneralReplacePatchViewModel(
            $"hex-patch-{_generalReplacePatchCounter}",
            GeneralReplacePatches.Count + 1,
            GeneralReplacePatchDraft.StartAddress,
            GeneralReplacePatchDraft.EndAddress,
            GeneralReplacePatchDraft.Kind,
            GeneralReplacePatchDraft.Value));
        _generalReplacePatchRedo.Clear();
        GeneralReplacePatchDraft.Value = string.Empty;
        OnPropertyChanged(nameof(HasGeneralReplacePatches));
        OnPropertyChanged(nameof(IsGeneralReplacePatchListEmpty));
        RefreshGeneralReplacePatchCommands();
        RefreshGeneralReplaceHexViewport();
        RefreshCommandState();
    }

    private bool CanUndoGeneralReplacePatch()
    {
        return GeneralReplacePatches.Count > 0;
    }

    private void UndoGeneralReplacePatch()
    {
        if (GeneralReplacePatches.Count == 0)
        {
            return;
        }

        int lastIndex = GeneralReplacePatches.Count - 1;
        GeneralReplacePatchViewModel patch = GeneralReplacePatches[lastIndex];
        GeneralReplacePatches.RemoveAt(lastIndex);
        _generalReplacePatchRedo.Push(patch);
        OnPropertyChanged(nameof(HasGeneralReplacePatches));
        OnPropertyChanged(nameof(IsGeneralReplacePatchListEmpty));
        RefreshGeneralReplacePatchIndices();
        RefreshGeneralReplacePatchCommands();
        RefreshGeneralReplaceHexViewport();
        RefreshCommandState();
    }

    private bool CanRedoGeneralReplacePatch()
    {
        return _generalReplacePatchRedo.Count > 0;
    }

    private void RedoGeneralReplacePatch()
    {
        if (_generalReplacePatchRedo.TryPop(out GeneralReplacePatchViewModel? patch))
        {
            GeneralReplacePatches.Add(patch);
            RefreshGeneralReplacePatchIndices();
            OnPropertyChanged(nameof(HasGeneralReplacePatches));
            OnPropertyChanged(nameof(IsGeneralReplacePatchListEmpty));
        }

        RefreshGeneralReplacePatchCommands();
        RefreshGeneralReplaceHexViewport();
        RefreshCommandState();
    }

    private void AddGeneralReplaceMapping()
    {
        _generalReplaceMappingCounter++;
        var mapping = new GeneralReplaceMappingViewModel(
            $"general-map-{_generalReplaceMappingCounter}",
            GeneralReplaceMappings.Count + 1);
        mapping.PropertyChanged += GeneralReplaceMappingPropertyChanged;
        GeneralReplaceMappings.Add(mapping);
        RefreshCommandState();
    }

    private void RemoveGeneralReplaceMapping(GeneralReplaceMappingViewModel? mapping)
    {
        if (mapping is null || GeneralReplaceMappings.Count <= 1)
        {
            return;
        }

        mapping.PropertyChanged -= GeneralReplaceMappingPropertyChanged;
        _ = GeneralReplaceMappings.Remove(mapping);
        for (int index = 0; index < GeneralReplaceMappings.Count; index++)
        {
            GeneralReplaceMappings[index].SetIndex(index + 1);
        }

        RefreshCommandState();
    }

    private IReadOnlyList<WorkbenchGeneralReplaceMappingInput> CreateGeneralReplaceMappingInputs()
    {
        return
        [
            .. GeneralReplaceMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => new WorkbenchGeneralReplaceMappingInput(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.StartAddress,
                    mapping.EndAddress)),
        ];
    }

    private IReadOnlyList<WorkbenchGeneralReplacePatchInput> CreateGeneralReplacePatchInputs()
    {
        return
        [
            .. GeneralReplacePatches.Select(patch => new WorkbenchGeneralReplacePatchInput(
                patch.PatchId,
                patch.StartAddress,
                patch.EndAddress,
                patch.Kind,
                patch.Value)),
        ];
    }

    private void GeneralReplacePatchDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshGeneralReplaceHexViewport();
        RefreshGeneralReplacePatchCommands();
        if (e.PropertyName == nameof(GeneralReplacePatchDraftViewModel.Kind))
        {
            OnPropertyChanged(nameof(IsGeneralReplacePatchOverwrite));
            OnPropertyChanged(nameof(IsGeneralReplacePatchFill));
        }
    }

    private void GoToGeneralReplaceHexViewport()
    {
        RefreshGeneralReplaceHexViewport();
    }

    private void ToggleHexEditor()
    {
        IsHexEditorExpanded = !IsHexEditorExpanded;
        if (IsHexEditorExpanded)
        {
            RefreshGeneralReplaceEditableRanges();
            RefreshGeneralReplaceHexViewport();
        }
    }

    private void SelectGeneralReplaceHexByte(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        GeneralReplacePatchDraft.StartAddress = cell.Address;
        GeneralReplacePatchDraft.EndAddress = cell.Address;
        GeneralReplaceHexViewportAddress = cell.Address;
    }

    private void SelectGeneralReplaceEditableRange(GeneralReplaceEditableRangeViewModel range)
    {
        GeneralReplacePatchDraft.StartAddress = range.StartAddress;
        GeneralReplacePatchDraft.EndAddress = range.EndAddress;
        GeneralReplaceHexViewportAddress = range.StartAddress;
    }

    private void RefreshGeneralReplaceEditableRanges()
    {
        string? selectedId = SelectedGeneralReplaceEditableRange?.RegionId;
        GeneralReplaceEditableRanges.Clear();
        foreach (GeneralReplaceEditableRangeViewModel range in UiCompositionRunner.GetGeneralReplaceEditableRanges(
            SelectedIc,
            SelectedNumber,
            ReplaceBaseSlot.FilePath))
        {
            GeneralReplaceEditableRanges.Add(range);
        }

        SelectedGeneralReplaceEditableRange = string.IsNullOrWhiteSpace(selectedId)
            ? null
            : GeneralReplaceEditableRanges.FirstOrDefault(range =>
                string.Equals(range.RegionId, selectedId, StringComparison.Ordinal));
    }

    private void RefreshGeneralReplaceHexViewport()
    {
        GeneralReplaceHexViewportRows.Clear();
        if (!ReplaceBaseSlot.HasFile)
        {
            GeneralReplaceHexViewportStatus = Text.GeneralReplaceHexViewportNoBaseDetail;
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }

        if (!TryParseGeneralReplaceHexAddress(GeneralReplaceHexViewportAddress, out long viewportStart))
        {
            GeneralReplaceHexViewportStatus = Text.GeneralReplaceHexViewportAddressInvalidDetail;
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }

        WorkbenchGeneralReplaceHexViewport viewport = UiCompositionRunner.CreateGeneralReplaceHexViewport(
            ReplaceBaseSlot.FilePath!,
            viewportStart,
            CreateGeneralReplaceHexViewportPatchInputs());
        foreach (WorkbenchGeneralReplaceHexViewportRow row in viewport.Rows)
        {
            GeneralReplaceHexViewportRows.Add(new GeneralReplaceHexViewportRowViewModel(
                $"0x{row.Address:X6}",
                [
                    .. row.Bytes.Select(value => new GeneralReplaceHexByteCellViewModel(
                        $"0x{value.Address:X6}",
                        value.Before.ToString("X2", CultureInfo.InvariantCulture),
                        value.After.ToString("X2", CultureInfo.InvariantCulture),
                        value.IsChanged,
                        string.Equals(
                            $"0x{value.Address:X6}",
                            GeneralReplacePatchDraft.StartAddress,
                            StringComparison.OrdinalIgnoreCase))),
                ],
                row.BeforeAscii,
                row.AfterAscii));
        }

        GeneralReplaceHexViewportStatus = viewport.Issues.Count > 0
            ? viewport.Issues[0].Message
            : string.Format(
                CultureInfo.InvariantCulture,
                Text.GeneralReplaceHexViewportReadyDetail,
                viewport.BaseLength,
                viewport.ViewportStart,
                viewport.ViewportStart + viewport.ViewportLength - 1);
        NotifyGeneralReplaceHexViewportChanged();
    }

    private List<WorkbenchGeneralReplacePatchInput> CreateGeneralReplaceHexViewportPatchInputs()
    {
        List<WorkbenchGeneralReplacePatchInput> patches = [.. CreateGeneralReplacePatchInputs()];
        if (!string.IsNullOrWhiteSpace(GeneralReplacePatchDraft.Value))
        {
            patches.Add(new WorkbenchGeneralReplacePatchInput(
                "hex-draft",
                GeneralReplacePatchDraft.StartAddress,
                GeneralReplacePatchDraft.EndAddress,
                GeneralReplacePatchDraft.Kind,
                GeneralReplacePatchDraft.Value));
        }

        return patches;
    }

    private static bool TryParseGeneralReplaceHexAddress(string value, out long address)
    {
        address = 0;
        string trimmed = value.Trim();
        bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address)
            : long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
        return parsed && address >= 0;
    }

    partial void OnGeneralReplaceHexViewportAddressChanged(string value)
    {
        RefreshGeneralReplaceHexViewport();
    }

    partial void OnSelectedGeneralReplaceEditableRangeChanged(GeneralReplaceEditableRangeViewModel? value)
    {
        if (value is not null)
        {
            SelectGeneralReplaceEditableRange(value);
        }
    }

    private void NotifyGeneralReplaceHexViewportChanged()
    {
        OnPropertyChanged(nameof(GeneralReplaceHexViewportStatus));
        OnPropertyChanged(nameof(HasGeneralReplaceHexViewportRows));
    }

    private void RefreshGeneralReplacePatchIndices()
    {
        for (int index = 0; index < GeneralReplacePatches.Count; index++)
        {
            GeneralReplacePatches[index].SetIndex(index + 1);
        }
    }

    private void RefreshGeneralReplacePatchCommands()
    {
        ApplyGeneralReplacePatchCommand.NotifyCanExecuteChanged();
        UndoGeneralReplacePatchCommand.NotifyCanExecuteChanged();
        RedoGeneralReplacePatchCommand.NotifyCanExecuteChanged();
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCommandState();
    }
}
