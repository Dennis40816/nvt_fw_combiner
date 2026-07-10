using System.Globalization;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private GeneralReplaceHexByteCellViewModel? _activeGeneralReplaceHexByteEdit;

    private void BeginGeneralReplaceHexByteEdit(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference)
        {
            return;
        }

        if (_activeGeneralReplaceHexByteEdit is not null &&
            !ReferenceEquals(_activeGeneralReplaceHexByteEdit, cell))
        {
            CancelGeneralReplaceHexByteEdit(_activeGeneralReplaceHexByteEdit);
        }

        SelectGeneralReplaceHexByte(cell);
        cell.EditValue = cell.ValueHex;
        cell.InlineValidationMessage = string.Empty;
        cell.IsEditing = true;
        _activeGeneralReplaceHexByteEdit = cell;
    }

    private void CommitGeneralReplaceHexByteEdit(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference || !cell.IsEditing)
        {
            return;
        }

        string value = cell.EditValue.Trim().ToUpperInvariant();
        if (value.Length != 2 ||
            !byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            cell.InlineValidationMessage = Text.GeneralReplaceHexEditValidationDetail;
            return;
        }

        if (string.Equals(value, cell.ValueHex, StringComparison.Ordinal))
        {
            CancelGeneralReplaceHexByteEdit(cell);
            return;
        }

        cell.IsEditing = false;
        _activeGeneralReplaceHexByteEdit = null;
        if (!TryStageGeneralReplaceHexByte(cell, value))
        {
            cell.IsEditing = true;
            _activeGeneralReplaceHexByteEdit = cell;
            return;
        }

    }

    private void CancelGeneralReplaceHexByteEdit(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        cell.EditValue = cell.ValueHex;
        cell.InlineValidationMessage = string.Empty;
        cell.IsEditing = false;
        if (ReferenceEquals(_activeGeneralReplaceHexByteEdit, cell))
        {
            _activeGeneralReplaceHexByteEdit = null;
        }
    }

    private void SetGeneralReplacePatchStart(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference)
        {
            return;
        }

        GeneralReplacePatchDraft.StartAddress = cell.Address;
    }

    private void SetGeneralReplacePatchEnd(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference)
        {
            return;
        }

        GeneralReplacePatchDraft.EndAddress = cell.Address;
        UpdateGeneralReplaceHexSelection(cell.Address);
    }

    private void ClearGeneralReplaceHexByte(GeneralReplaceHexByteCellViewModel? cell)
    {
        if (cell is null || cell.IsReference || !TryStageGeneralReplaceHexByte(cell, "FF"))
        {
            return;
        }
    }

    private bool TryStageGeneralReplaceHexByte(GeneralReplaceHexByteCellViewModel cell, string value)
    {
        if (!TryParseGeneralReplaceHexAddress(cell.Address, out long address))
        {
            cell.InlineValidationMessage = Text.GeneralReplaceHexEditValidationDetail;
            return false;
        }

        if (!IsGeneralReplaceHexRangeAuthorized(address, address))
        {
            cell.InlineValidationMessage = Text.GeneralReplaceHexUnauthorizedRangeDetail;
            return false;
        }

        for (int index = 0; index < GeneralReplacePatches.Count; index++)
        {
            GeneralReplacePatchViewModel patch = GeneralReplacePatches[index];
            if (!TryParseGeneralReplaceHexAddress(patch.StartAddress, out long start) ||
                !TryParseGeneralReplaceHexAddress(patch.EndAddress, out long end) ||
                address < start ||
                address > end)
            {
                continue;
            }

            if (start == address &&
                end == address &&
                patch.Kind == WorkbenchGeneralReplacePatchKind.Overwrite)
            {
                GeneralReplacePatches[index] = new GeneralReplacePatchViewModel(
                    patch.PatchId,
                    patch.Index,
                    patch.StartAddress,
                    patch.EndAddress,
                    patch.Kind,
                    value);
                NotifyGeneralReplacePatchCollectionChanged(refreshViewport: false);
                ApplyStagedGeneralReplaceHexByteToViewport(address, value);
                return true;
            }

            cell.InlineValidationMessage = Text.GeneralReplaceHexOverlapEditDetail;
            return false;
        }

        _generalReplacePatchCounter++;
        GeneralReplacePatches.Add(new GeneralReplacePatchViewModel(
            $"hex-patch-{_generalReplacePatchCounter}",
            GeneralReplacePatches.Count + 1,
            cell.Address,
            cell.Address,
            WorkbenchGeneralReplacePatchKind.Overwrite,
            value));
        _generalReplacePatchRedo.Clear();
        NotifyGeneralReplacePatchCollectionChanged(refreshViewport: false);
        ApplyStagedGeneralReplaceHexByteToViewport(address, value);
        return true;
    }
}
