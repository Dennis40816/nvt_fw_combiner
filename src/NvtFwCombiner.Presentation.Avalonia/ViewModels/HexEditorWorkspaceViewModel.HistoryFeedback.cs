namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class HexEditorWorkspaceViewModel
{
    /// <summary>Visible current-data addresses changed by the latest Undo or Redo operation.</summary>
    public IReadOnlyList<string> HistoryFeedbackAddresses { get; private set; } = [];

    /// <summary>Monotonic trigger used by the low-cost renderer to restart its feedback animation.</summary>
    public int HistoryFeedbackVersion { get; private set; }

    private Dictionary<string, VisibleByteFingerprint> CaptureVisibleByteFingerprints()
    {
        return ViewportRows
            .SelectMany(row => row.Bytes)
            .ToDictionary(
                cell => cell.Address,
                cell => new VisibleByteFingerprint(
                    cell.ValueHex,
                    cell.OriginalHex,
                    cell.HasOriginalValue,
                    cell.IsDataChanged,
                    cell.IsStructuralChanged),
                StringComparer.Ordinal);
    }

    private void PublishHistoryFeedback(IReadOnlyDictionary<string, VisibleByteFingerprint> before)
    {
        IReadOnlyDictionary<string, VisibleByteFingerprint> after = CaptureVisibleByteFingerprints();
        HistoryFeedbackAddresses = [
            .. after
                .Where(pair => !before.TryGetValue(pair.Key, out VisibleByteFingerprint previous) || previous != pair.Value)
                .Select(pair => pair.Key),
        ];
        OnPropertyChanged(nameof(HistoryFeedbackAddresses));
        HistoryFeedbackVersion++;
        OnPropertyChanged(nameof(HistoryFeedbackVersion));
    }

    private void ResetHistoryFeedback()
    {
        HistoryFeedbackAddresses = [];
        OnPropertyChanged(nameof(HistoryFeedbackAddresses));
    }

    private readonly record struct VisibleByteFingerprint(
        string ValueHex,
        string OriginalHex,
        bool HasOriginalValue,
        bool IsDataChanged,
        bool IsStructuralChanged);
}
