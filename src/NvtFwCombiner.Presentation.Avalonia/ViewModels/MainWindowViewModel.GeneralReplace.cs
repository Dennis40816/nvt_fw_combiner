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
        if (!ReplaceBaseSlot.HasFile)
        {
            ReplaceGeneralReplaceHexViewportRows([]);
            GeneralReplaceHexViewportStatus = Text.GeneralReplaceHexViewportNoBaseDetail;
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }

        if (_generalReplaceBaseSnapshot is null)
        {
            ReplaceGeneralReplaceHexViewportRows([]);
            GeneralReplaceHexViewportStatus = _generalReplaceBaseSnapshotError ?? Text.GeneralReplaceHexViewportNoBaseDetail;
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }

        if (!TryParseGeneralReplaceHexAddress(GeneralReplaceHexViewportAddress, out long viewportStart))
        {
            ReplaceGeneralReplaceHexViewportRows([]);
            GeneralReplaceHexViewportStatus = Text.GeneralReplaceHexViewportAddressInvalidDetail;
            NotifyGeneralReplaceHexViewportChanged();
            return;
        }

        WorkbenchGeneralReplaceHexViewport viewport = UiCompositionRunner.CreateGeneralReplaceHexViewport(
            _generalReplaceBaseSnapshot,
            viewportStart,
            CreateGeneralReplaceHexViewportPatchInputs());
        if (!TryReconcileGeneralReplaceHexViewport(viewport))
        {
            ReplaceGeneralReplaceHexViewportRows(CreateGeneralReplaceHexViewportRows(viewport));
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

    private List<GeneralReplaceHexViewportRowViewModel> CreateGeneralReplaceHexViewportRows(
        WorkbenchGeneralReplaceHexViewport viewport)
    {
        List<GeneralReplaceHexViewportRowViewModel> rows = [];
        foreach (WorkbenchGeneralReplaceHexViewportRow row in viewport.Rows)
        {
            GeneralReplaceHexViewportRowViewModel authoringRow = CreateGeneralReplaceHexAuthoringRow(row);
            rows.Add(authoringRow);
            if (IsGeneralReplaceHexReferenceRowsVisible && authoringRow.HasChanges)
            {
                rows.Add(CreateGeneralReplaceHexReferenceRow(authoringRow));
            }
        }

        return rows;
    }

    private GeneralReplaceHexViewportRowViewModel CreateGeneralReplaceHexAuthoringRow(
        WorkbenchGeneralReplaceHexViewportRow row)
    {
        return new GeneralReplaceHexViewportRowViewModel(
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
            hasChanges: row.Bytes.Any(value => value.IsChanged));
    }

    private bool TryReconcileGeneralReplaceHexViewport(WorkbenchGeneralReplaceHexViewport viewport)
    {
        List<GeneralReplaceHexViewportRowViewModel> authoringRows =
        [
            .. GeneralReplaceHexViewportRows.Where(row => !row.IsReferenceRow),
        ];
        if (authoringRows.Count != viewport.Rows.Count)
        {
            return false;
        }

        for (int rowIndex = 0; rowIndex < authoringRows.Count; rowIndex++)
        {
            GeneralReplaceHexViewportRowViewModel existing = authoringRows[rowIndex];
            WorkbenchGeneralReplaceHexViewportRow updated = viewport.Rows[rowIndex];
            if (!string.Equals(existing.Address, $"0x{updated.Address:X6}", StringComparison.OrdinalIgnoreCase) ||
                existing.Bytes.Count != updated.Bytes.Count)
            {
                return false;
            }
        }

        for (int rowIndex = 0; rowIndex < authoringRows.Count; rowIndex++)
        {
            GeneralReplaceHexViewportRowViewModel existing = authoringRows[rowIndex];
            WorkbenchGeneralReplaceHexViewportRow updated = viewport.Rows[rowIndex];
            for (int byteIndex = 0; byteIndex < existing.Bytes.Count; byteIndex++)
            {
                GeneralReplaceHexByteCellViewModel existingByte = existing.Bytes[byteIndex];
                WorkbenchGeneralReplaceHexByte updatedByte = updated.Bytes[byteIndex];
                existingByte.ValueHex = updatedByte.After.ToString("X2", CultureInfo.InvariantCulture);
                existingByte.IsChanged = updatedByte.IsChanged;
            }

            existing.AfterAscii = updated.AfterAscii;
            existing.HasChanges = updated.Bytes.Any(value => value.IsChanged);
            SynchronizeGeneralReplaceHexReferenceRow(existing);
        }

        return true;
    }

    private void ReplaceGeneralReplaceHexViewportRows(
        IReadOnlyList<GeneralReplaceHexViewportRowViewModel> rows)
    {
        GeneralReplaceHexViewportRows.ReplaceAll(rows);
        _generalReplaceHexAuthoringCells.Clear();
        _selectedGeneralReplaceHexByte = null;
        foreach (GeneralReplaceHexByteCellViewModel byteCell in rows
                     .Where(row => !row.IsReferenceRow)
                     .SelectMany(row => row.Bytes))
        {
            _generalReplaceHexAuthoringCells[byteCell.Address] = byteCell;
            if (byteCell.IsSelected)
            {
                _selectedGeneralReplaceHexByte = byteCell;
            }
        }
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
        OnPropertyChanged(nameof(CanBuildHexEditor));
        OnPropertyChanged(nameof(HexEditorReadinessStatus));
        RefreshGeneralReplacePatchCommands();
        if (refreshViewport)
        {
            RefreshGeneralReplaceHexViewport();
        }
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
        GeneralReplaceHexByteCellViewModel? selected = _generalReplaceHexAuthoringCells.TryGetValue(
            address,
            out GeneralReplaceHexByteCellViewModel? value)
            ? value
            : null;
        if (ReferenceEquals(_selectedGeneralReplaceHexByte, selected))
        {
            return;
        }

        if (_selectedGeneralReplaceHexByte is { } previous)
        {
            previous.IsSelected = false;
        }

        _selectedGeneralReplaceHexByte = selected;
        if (selected is { })
        {
            selected.IsSelected = true;
        }
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCommandState();
    }
}
