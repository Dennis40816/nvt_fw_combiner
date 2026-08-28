using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    public bool UseSameTpForAbMerge { get; private set; }

    public bool IsAbSameTpConflictPromptOpen { get; private set; }

    /// <summary>Switches between independent TPA/TPB authoring and the explicit linked convenience.</summary>
    public IAsyncRelayCommand ToggleAbSameTpCommand { get; }

    /// <summary>Resolves a linked-mode conflict by keeping the current TPA selection.</summary>
    public IAsyncRelayCommand KeepTpAForAbSameTpCommand { get; }

    /// <summary>Resolves a linked-mode conflict by keeping the current TPB selection.</summary>
    public IAsyncRelayCommand KeepTpBForAbSameTpCommand { get; }

    /// <summary>Cancels linked mode without changing either current TP selection.</summary>
    public IRelayCommand CancelAbSameTpConflictCommand { get; }

    internal bool MirrorsAbTpSelection(string slotId)
    {
        return UseSameTpForAbMerge && IsAbAddressSpace(slotId, CompositionAddressSpaceIds.TpAInput);
    }

    internal bool BlocksIndependentAbTpSelection(string slotId)
    {
        return UseSameTpForAbMerge && IsAbAddressSpace(slotId, CompositionAddressSpaceIds.TpBInput);
    }

    private async Task ToggleAbSameTpAsync()
    {
        if (UseSameTpForAbMerge)
        {
            SetAbSameTpMode(enabled: false);
            return;
        }

        if (!_abMergeSlotsByAddressSpace.TryGetValue(
                CompositionAddressSpaceIds.TpAInput,
                out FirmwareSlotViewModel? tpA) ||
            !_abMergeSlotsByAddressSpace.TryGetValue(
                CompositionAddressSpaceIds.TpBInput,
                out FirmwareSlotViewModel? tpB))
        {
            return;
        }

        if (tpA.HasFile && tpB.HasFile &&
            !FirmwarePathDisplay.AreSame(tpA.FilePath!, tpB.FilePath!))
        {
            IsAbSameTpConflictPromptOpen = true;
            OnPropertyChanged(nameof(IsAbSameTpConflictPromptOpen));
            OnPropertyChanged(nameof(UseSameTpForAbMerge));
            return;
        }

        string? selectedPath = tpA.FilePath ?? tpB.FilePath;
        SetAbSameTpMode(enabled: true);
        if (selectedPath is not null)
        {
            await _stateBindings.SetAbSameTpFileAsync(selectedPath, CancellationToken.None);
        }
    }

    private async Task KeepTpForAbSameTpAsync(string addressSpaceId)
    {
        if (!IsAbSameTpConflictPromptOpen ||
            !_abMergeSlotsByAddressSpace.TryGetValue(addressSpaceId, out FirmwareSlotViewModel? selected) ||
            selected.FilePath is not { } selectedPath)
        {
            return;
        }

        CloseAbSameTpConflict();
        SetAbSameTpMode(enabled: true);
        await _stateBindings.SetAbSameTpFileAsync(selectedPath, CancellationToken.None);
    }

    private void CancelAbSameTpConflict()
    {
        CloseAbSameTpConflict();
    }

    private void CloseAbSameTpConflict()
    {
        if (!IsAbSameTpConflictPromptOpen)
        {
            return;
        }

        IsAbSameTpConflictPromptOpen = false;
        OnPropertyChanged(nameof(IsAbSameTpConflictPromptOpen));
    }

    private void SetAbSameTpMode(bool enabled)
    {
        if (UseSameTpForAbMerge == enabled)
        {
            return;
        }

        UseSameTpForAbMerge = enabled;
        ApplyAbSameTpPresentation();
        OnPropertyChanged(nameof(UseSameTpForAbMerge));
    }

    private void ApplyAbSameTpPresentation()
    {
        if (_abMergeSlotsByAddressSpace.TryGetValue(
                CompositionAddressSpaceIds.TpBInput,
                out FirmwareSlotViewModel? tpB))
        {
            tpB.SetLinkedSelection(UseSameTpForAbMerge, Text.AbSameTpLinkedLabel);
        }
    }

    private bool IsAbAddressSpace(string slotId, string addressSpaceId)
    {
        return _abMergeAddressSpaceBySlotId.TryGetValue(slotId, out string? actual) &&
            StringComparer.Ordinal.Equals(actual, addressSpaceId);
    }

}
