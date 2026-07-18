using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly OutputFileNameProjectionCache _mergeOutputFileNameCache = new();
    private readonly OutputFileNameProjectionCache _replaceOutputFileNameCache = new();

    private string CreateFlashCodeOutputFileName(
        IEnumerable<FirmwareSlotViewModel> candidateSlots,
        OutputFileNameProjectionCache cache)
    {
        ArgumentNullException.ThrowIfNull(candidateSlots);
        var date = DateOnly.FromDateTime(DateTime.Now);
        return cache.GetOrCreate(
            SelectedIc,
            date,
            candidateSlots,
            static (icId, snapshotDate, slots) => WorkbenchCompositionService.CreateFlashCodeOutputFileName(
                icId,
                [.. slots.Select(ToOutputNameCandidate)],
                snapshotDate).FileName);
    }

    private static WorkbenchOutputNameCandidate ToOutputNameCandidate(FirmwareSlotViewModel slot)
    {
        return new WorkbenchOutputNameCandidate(
            slot.SlotKind switch
            {
                FirmwareSlotKind.Dp => WorkbenchOutputNameCandidateKind.Dp,
                FirmwareSlotKind.Tp => WorkbenchOutputNameCandidateKind.Tp,
                FirmwareSlotKind.CtrlRam => WorkbenchOutputNameCandidateKind.CtrlRam,
                FirmwareSlotKind.Base => WorkbenchOutputNameCandidateKind.Base,
                FirmwareSlotKind.Unknown => WorkbenchOutputNameCandidateKind.Unknown,
                _ => WorkbenchOutputNameCandidateKind.Unknown,
            },
            slot.FilePath);
    }

}

/// <summary>Caches one file-stamped UI output-name projection without retaining firmware bytes.</summary>
internal sealed class OutputFileNameProjectionCache
{
    private OutputFileNameCacheEntry? _entry;

    internal string GetOrCreate(
        string icId,
        DateOnly date,
        IEnumerable<FirmwareSlotViewModel> slots,
        Func<string, DateOnly, IReadOnlyList<FirmwareSlotViewModel>, string> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(create);
        if (_entry?.Matches(icId, date, slots) == true)
        {
            return _entry.FileName;
        }

        FirmwareSlotViewModel[] slotSnapshot = [.. slots];
        OutputNameCandidateIdentity[] before = [.. slotSnapshot.Select(OutputNameCandidateIdentity.Capture)];
        string fileName = create(icId, date, slotSnapshot);
        OutputNameCandidateIdentity[] after = [.. slotSnapshot.Select(OutputNameCandidateIdentity.Capture)];
        _entry = before.AsSpan().SequenceEqual(after)
            ? new OutputFileNameCacheEntry(icId, date, before, fileName)
            : null;
        return fileName;
    }

    private sealed record OutputFileNameCacheEntry(
        string IcId,
        DateOnly Date,
        IReadOnlyList<OutputNameCandidateIdentity> Candidates,
        string FileName)
    {
        internal bool Matches(string icId, DateOnly date, IEnumerable<FirmwareSlotViewModel> slots)
        {
            if (!string.Equals(IcId, icId, StringComparison.Ordinal) || Date != date)
            {
                return false;
            }

            using IEnumerator<FirmwareSlotViewModel> enumerator = slots.GetEnumerator();
            foreach (OutputNameCandidateIdentity candidate in Candidates)
            {
                if (!enumerator.MoveNext() || !candidate.Matches(enumerator.Current))
                {
                    return false;
                }
            }

            return !enumerator.MoveNext();
        }
    }

    private readonly record struct OutputNameCandidateIdentity(
        FirmwareSlotKind Kind,
        string? Path,
        bool Exists,
        long Length,
        DateTime LastWriteTimeUtc)
    {
        internal static OutputNameCandidateIdentity Capture(FirmwareSlotViewModel slot)
        {
            string? path = slot.FilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new OutputNameCandidateIdentity(slot.SlotKind, path, false, 0, DateTime.MinValue);
            }

            try
            {
                var file = new FileInfo(path);
                return file.Exists
                    ? new OutputNameCandidateIdentity(
                        slot.SlotKind,
                        path,
                        true,
                        file.Length,
                        file.LastWriteTimeUtc)
                    : new OutputNameCandidateIdentity(slot.SlotKind, path, false, 0, DateTime.MinValue);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return new OutputNameCandidateIdentity(slot.SlotKind, path, false, 0, DateTime.MinValue);
            }
        }

        internal bool Matches(FirmwareSlotViewModel slot)
        {
            return Equals(Capture(slot));
        }
    }
}
