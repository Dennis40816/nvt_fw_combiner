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

        var candidate = new WorkbenchGeneralReplacePatchInput(
            "hex-draft",
            GeneralReplacePatchDraft.StartAddress,
            GeneralReplacePatchDraft.EndAddress,
            GeneralReplacePatchDraft.Kind,
            GeneralReplacePatchDraft.Value);
        if (!TryValidateGeneralReplacePatch(candidate, out string validationMessage))
        {
            GeneralReplaceHexViewportStatus = validationMessage;
            NotifyGeneralReplaceHexViewportChanged();
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
        NotifyGeneralReplacePatchCollectionChanged();
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
        RefreshGeneralReplacePatchIndices();
        NotifyGeneralReplacePatchCollectionChanged();
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
        }

        NotifyGeneralReplacePatchCollectionChanged();
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
        RefreshGeneralReplacePatchCommands();
        if (e.PropertyName == nameof(GeneralReplacePatchDraftViewModel.Kind))
        {
            OnPropertyChanged(nameof(IsGeneralReplacePatchOverwrite));
            OnPropertyChanged(nameof(IsGeneralReplacePatchFill));
        }

        if (e.PropertyName == nameof(GeneralReplacePatchDraftViewModel.StartAddress))
        {
            UpdateGeneralReplaceHexSelection(GeneralReplacePatchDraft.StartAddress);
        }
    }

    private void GoToGeneralReplaceHexViewport()
    {
        RefreshGeneralReplaceHexViewport();
    }

    private void ShowHexEditor()
    {
        RefreshGeneralReplaceEditableRanges();
        RefreshGeneralReplaceHexViewport();
        SetSelectedPage(ShellPage.HexEditor);
    }

    private void SelectGeneralReplaceHexByte(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference)
        {
            return;
        }

        GeneralReplacePatchDraft.StartAddress = cell.Address;
        GeneralReplacePatchDraft.EndAddress = cell.Address;
        GeneralReplaceHexViewportAddress = cell.Address;
        UpdateGeneralReplaceHexSelection(cell.Address);
    }

    private void SelectGeneralReplaceEditableRange(GeneralReplaceEditableRangeViewModel range)
    {
        GeneralReplacePatchDraft.StartAddress = range.StartAddress;
        GeneralReplacePatchDraft.EndAddress = range.EndAddress;
        GeneralReplaceHexViewportAddress = range.StartAddress;
        RefreshGeneralReplaceHexViewport();
    }

    private void RefreshGeneralReplaceEditableRanges()
    {
        string? selectedId = SelectedGeneralReplaceEditableRange?.RegionId;
        GeneralReplaceEditableRanges.Clear();
        if (_generalReplaceBaseSnapshot is null)
        {
            SelectedGeneralReplaceEditableRange = null;
            return;
        }

        foreach (GeneralReplaceEditableRangeViewModel range in UiCompositionRunner.GetGeneralReplaceEditableRanges(
            SelectedIc,
            SelectedNumber,
            ReplaceBaseSlot.FilePath,
            _generalReplaceBaseSnapshot))
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

        if (_generalReplaceBaseSnapshot is null)
        {
            GeneralReplaceHexViewportStatus = _generalReplaceBaseSnapshotError ?? Text.GeneralReplaceHexViewportNoBaseDetail;
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
            _generalReplaceBaseSnapshot,
            viewportStart,
            CreateGeneralReplaceHexViewportPatchInputs());
        foreach (WorkbenchGeneralReplaceHexViewportRow row in viewport.Rows)
        {
            bool hasChanges = row.Bytes.Any(value => value.IsChanged);
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
                            StringComparison.OrdinalIgnoreCase),
                        false,
                        Text.GeneralReplaceHexContextEditLabel,
                        Text.GeneralReplaceHexContextRangeStartLabel,
                        Text.GeneralReplaceHexContextRangeEndLabel,
                        Text.GeneralReplaceHexContextClearLabel)),
                ],
                row.BeforeAscii,
                row.AfterAscii,
                isReferenceRow: false,
                hasChanges: hasChanges));
            if (IsGeneralReplaceHexReferenceRowsVisible && hasChanges)
            {
                GeneralReplaceHexViewportRows.Add(new GeneralReplaceHexViewportRowViewModel(
                    $"0x{row.Address:X6}",
                    [
                        .. row.Bytes.Select(value => new GeneralReplaceHexByteCellViewModel(
                            $"0x{value.Address:X6}",
                            value.Before.ToString("X2", CultureInfo.InvariantCulture),
                            value.Before.ToString("X2", CultureInfo.InvariantCulture),
                            false,
                            false,
                            true,
                            Text.GeneralReplaceHexContextEditLabel,
                            Text.GeneralReplaceHexContextRangeStartLabel,
                            Text.GeneralReplaceHexContextRangeEndLabel,
                            Text.GeneralReplaceHexContextClearLabel)),
                    ],
                    row.BeforeAscii,
                    row.BeforeAscii,
                    isReferenceRow: true,
                    hasChanges: false));
            }
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

    private bool TryValidateGeneralReplacePatch(
        WorkbenchGeneralReplacePatchInput candidate,
        out string validationMessage)
    {
        validationMessage = Text.GeneralReplaceHexEditValidationDetail;
        if (!ReplaceBaseSlot.HasFile ||
            _generalReplaceBaseSnapshot is null ||
            !TryParseGeneralReplaceHexAddress(candidate.TargetStart, out long start) ||
            !TryParseGeneralReplaceHexAddress(candidate.TargetEndInclusive, out long end) ||
            end < start)
        {
            return false;
        }

        if (!IsGeneralReplaceHexRangeAuthorized(start, end))
        {
            validationMessage = Text.GeneralReplaceHexUnauthorizedRangeDetail;
            return false;
        }

        List<WorkbenchGeneralReplacePatchInput> staged = [.. CreateGeneralReplacePatchInputs(), candidate];
        WorkbenchGeneralReplaceHexViewport viewport = UiCompositionRunner.CreateGeneralReplaceHexViewport(
            _generalReplaceBaseSnapshot,
            start,
            staged);
        if (viewport.Issues.Count > 0)
        {
            validationMessage = viewport.Issues[0].Message;
            return false;
        }

        return true;
    }

    private bool IsGeneralReplaceHexRangeAuthorized(long start, long end)
    {
        return GeneralReplaceEditableRanges.Any(range =>
            TryParseGeneralReplaceHexAddress(range.StartAddress, out long authorizedStart) &&
            TryParseGeneralReplaceHexAddress(range.EndAddress, out long authorizedEnd) &&
            start >= authorizedStart &&
            end <= authorizedEnd);
    }

    private void CaptureGeneralReplaceBaseSnapshot(string path)
    {
        _generalReplaceBaseSnapshot = null;
        _generalReplaceBaseSnapshotError = null;
        _activeGeneralReplaceHexByteEdit = null;
        GeneralReplacePatches.Clear();
        _generalReplacePatchRedo.Clear();
        _generalReplacePatchCounter = 0;
        GeneralReplacePatchDraft.Value = string.Empty;

        if (!UiCompositionRunner.TryLoadGeneralReplaceBaseSnapshot(
                path,
                out WorkbenchGeneralReplaceBaseSnapshot? snapshot,
                out string? errorMessage))
        {
            _generalReplaceBaseSnapshotError = errorMessage;
        }
        else
        {
            _generalReplaceBaseSnapshot = snapshot;
        }

        OnPropertyChanged(nameof(HasGeneralReplaceBaseSnapshot));
        OnPropertyChanged(nameof(HexEditorReadinessStatus));
        OnPropertyChanged(nameof(CanBuildHexEditor));
        OnPropertyChanged(nameof(HasGeneralReplacePatches));
        OnPropertyChanged(nameof(IsGeneralReplacePatchListEmpty));
        RefreshGeneralReplacePatchCommands();
    }

    partial void OnIsGeneralReplaceHexReferenceRowsVisibleChanged(bool value)
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

    private void NotifyGeneralReplacePatchCollectionChanged(bool refreshViewport = true)
    {
        OnPropertyChanged(nameof(HasGeneralReplacePatches));
        OnPropertyChanged(nameof(IsGeneralReplacePatchListEmpty));
        RefreshGeneralReplacePatchCommands();
        if (refreshViewport)
        {
            RefreshGeneralReplaceHexViewport();
        }

        RefreshCommandState();
    }

    private void ApplyStagedGeneralReplaceHexByteToViewport(long address, string value)
    {
        string addressLabel = $"0x{address:X6}";
        foreach (GeneralReplaceHexViewportRowViewModel row in GeneralReplaceHexViewportRows.Where(row => !row.IsReferenceRow))
        {
            GeneralReplaceHexByteCellViewModel? cell = row.Bytes.FirstOrDefault(candidate =>
                string.Equals(candidate.Address, addressLabel, StringComparison.OrdinalIgnoreCase));
            if (cell is null)
            {
                continue;
            }

            cell.ValueHex = value;
            cell.IsChanged = !string.Equals(cell.BeforeHex, value, StringComparison.OrdinalIgnoreCase);
            row.HasChanges = row.Bytes.Any(candidate => candidate.IsChanged);
            row.AfterAscii = string.Concat(row.Bytes.Select(FormatGeneralReplaceHexAsciiCharacter));
            SynchronizeGeneralReplaceHexReferenceRow(row);
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }
    }

    private void SynchronizeGeneralReplaceHexReferenceRow(GeneralReplaceHexViewportRowViewModel authoringRow)
    {
        int rowIndex = GeneralReplaceHexViewportRows.IndexOf(authoringRow);
        if (rowIndex < 0)
        {
            return;
        }

        bool hasReferenceRow = rowIndex + 1 < GeneralReplaceHexViewportRows.Count &&
                               GeneralReplaceHexViewportRows[rowIndex + 1].IsReferenceRow &&
                               string.Equals(
                                   GeneralReplaceHexViewportRows[rowIndex + 1].Address,
                                   authoringRow.Address,
                                   StringComparison.OrdinalIgnoreCase);
        if (authoringRow.HasChanges && IsGeneralReplaceHexReferenceRowsVisible && !hasReferenceRow)
        {
            GeneralReplaceHexViewportRows.Insert(rowIndex + 1, CreateGeneralReplaceHexReferenceRow(authoringRow));
        }
        else if ((!authoringRow.HasChanges || !IsGeneralReplaceHexReferenceRowsVisible) && hasReferenceRow)
        {
            GeneralReplaceHexViewportRows.RemoveAt(rowIndex + 1);
        }
    }

    private GeneralReplaceHexViewportRowViewModel CreateGeneralReplaceHexReferenceRow(
        GeneralReplaceHexViewportRowViewModel authoringRow)
    {
        return new GeneralReplaceHexViewportRowViewModel(
            authoringRow.Address,
            [
                .. authoringRow.Bytes.Select(cell => new GeneralReplaceHexByteCellViewModel(
                    cell.Address,
                    cell.BeforeHex,
                    cell.BeforeHex,
                    false,
                    false,
                    true,
                    Text.GeneralReplaceHexContextEditLabel,
                    Text.GeneralReplaceHexContextRangeStartLabel,
                    Text.GeneralReplaceHexContextRangeEndLabel,
                    Text.GeneralReplaceHexContextClearLabel)),
            ],
            authoringRow.BeforeAscii,
            authoringRow.BeforeAscii,
            isReferenceRow: true,
            hasChanges: false);
    }

    private static char FormatGeneralReplaceHexAsciiCharacter(GeneralReplaceHexByteCellViewModel cell)
    {
        return byte.TryParse(cell.ValueHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value) &&
               value is >= 0x20 and <= 0x7E
            ? (char)value
            : '.';
    }

    private void UpdateGeneralReplaceHexSelection(string address)
    {
        foreach (GeneralReplaceHexByteCellViewModel byteCell in GeneralReplaceHexViewportRows
                     .Where(row => !row.IsReferenceRow)
                     .SelectMany(row => row.Bytes))
        {
            byteCell.IsSelected = string.Equals(byteCell.Address, address, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCommandState();
    }
}
