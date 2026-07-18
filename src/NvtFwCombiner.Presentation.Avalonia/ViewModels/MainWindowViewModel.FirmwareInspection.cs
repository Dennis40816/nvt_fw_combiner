using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly Func<string, CancellationToken, Task<WorkbenchFirmwareArtifactSnapshot?>>
        _firmwareArtifactSnapshotLoader;
    private readonly Dictionary<FirmwareSlotViewModel, CancellationTokenSource> _firmwareInspectionRequests = [];
    private readonly Dictionary<FirmwareSlotViewModel, long> _firmwareInspectionGenerations = [];
    private long _firmwareInspectionGeneration;

    /// <summary>
    /// Selects a firmware file and captures its inspection snapshot without reading or hashing on the UI dispatcher.
    /// </summary>
    public async Task SetSlotFileAsync(
        string slotId,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            SetNonFirmwareSlotFile(slotId, path);
            return;
        }

        long generation = BeginFirmwareSlotSelection(slot, path, cancellationToken, out CancellationToken requestToken);
        WorkbenchFirmwareArtifactSnapshot? snapshot;
        try
        {
            snapshot = await Task.Run(
                () => _firmwareArtifactSnapshotLoader(path, requestToken),
                requestToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            if (IsCurrentFirmwareInspection(slot, path, generation))
            {
                CompleteFirmwareInspectionRequest(slot);
            }

            throw;
        }

        if (!IsCurrentFirmwareInspection(slot, path, generation))
        {
            return;
        }

        CompleteFirmwareInspectionRequest(slot);
        CompleteFirmwareSlotSelection(slot, snapshot, allowFileReadFallback: false);
    }

    private void SetSlotFileSynchronously(string slotId, string path)
    {
        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            SetNonFirmwareSlotFile(slotId, path);
            return;
        }

        CancelFirmwareInspectionRequest(slot);
        slot.FilePath = path;
        WorkbenchFirmwareArtifactSnapshot? snapshot =
            WorkbenchCompositionService.TryCaptureFirmwareArtifact(path);
        CompleteFirmwareSlotSelection(slot, snapshot, allowFileReadFallback: true);
    }

    private long BeginFirmwareSlotSelection(
        FirmwareSlotViewModel slot,
        string path,
        CancellationToken cancellationToken,
        out CancellationToken requestToken)
    {
        CancelFirmwareInspectionRequest(slot);
        var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _firmwareInspectionRequests[slot] = request;
        long generation = checked(++_firmwareInspectionGeneration);
        _firmwareInspectionGenerations[slot] = generation;
        requestToken = request.Token;

        slot.FilePath = path;
        slot.SetFirmwareFacts([]);
        NotifySelectedSlotFileState();
        RefreshCommandState();
        return generation;
    }

    private void CompleteFirmwareSlotSelection(
        FirmwareSlotViewModel slot,
        WorkbenchFirmwareArtifactSnapshot? snapshot,
        bool allowFileReadFallback)
    {
        if (snapshot is not null)
        {
            WorkbenchFirmwareArtifactSnapshot? tpSnapshot = GetTpSnapshotFor(slot);
            WorkbenchFirmwareInspection inspection = WorkbenchCompositionService.InspectFirmwareArtifact(
                SelectedIc,
                snapshot,
                tpSnapshot);
            slot.SetFirmwareInspection(snapshot, inspection);
        }

        RefreshFirmwareFacts(slot, allowFileReadFallback);
        PromptForFirmwareIcMismatch(slot, allowFileReadFallback);
        if (!IsFirmwareIcMismatchModalOpen)
        {
            TryApplyVerifiedFirmwareContext(slot, allowFileReadFallback);
        }

        if (slot.SlotId == MergeTpSlotId && _mergeDpSlot.HasFile)
        {
            RefreshFirmwareFacts(_mergeDpSlot, allowFileReadFallback);
        }

        NotifySelectedSlotFileState();
        if (slot.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
        {
            RefreshCtrlRamRegions();
            RefreshReplaceModeState(preserveSlotFiles: true);
            RefreshMemoryMapState();
        }
        else if (slot.SlotId is MergeDpSlotId or ReplaceBaseSlotId)
        {
            RefreshMemoryMapState();
        }

        RefreshCommandState();
    }

    private WorkbenchFirmwareArtifactSnapshot? GetTpSnapshotFor(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind == FirmwareSlotKind.Base
            ? slot.ArtifactSnapshot
            : slot.SlotId == MergeDpSlotId
                ? _mergeTpSlot.ArtifactSnapshot
                : null;
    }

    private bool IsCurrentFirmwareInspection(FirmwareSlotViewModel slot, string path, long generation)
    {
        return _firmwareInspectionGenerations.TryGetValue(slot, out long currentGeneration) &&
            currentGeneration == generation &&
            string.Equals(slot.FilePath, path, StringComparison.Ordinal);
    }

    private void CompleteFirmwareInspectionRequest(FirmwareSlotViewModel slot)
    {
        if (_firmwareInspectionRequests.Remove(slot, out CancellationTokenSource? request))
        {
            request.Dispose();
        }

        _ = _firmwareInspectionGenerations.Remove(slot);
    }

    private void CancelFirmwareInspectionRequest(FirmwareSlotViewModel slot)
    {
        if (_firmwareInspectionRequests.Remove(slot, out CancellationTokenSource? request))
        {
            request.Cancel();
            request.Dispose();
        }

        _ = _firmwareInspectionGenerations.Remove(slot);
    }

    private void SetNonFirmwareSlotFile(string slotId, string path)
    {
        if (SetGeneralMergeMappingFile(slotId, path))
        {
            return;
        }

        SetGeneralReplaceMappingFile(slotId, path);
    }

    private void NotifySelectedSlotFileState()
    {
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }
}
