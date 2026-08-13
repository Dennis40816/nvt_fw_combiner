namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class HexEditorWorkspaceViewModel
{
    /// <summary>Visible current-data addresses changed by the latest Undo or Redo operation.</summary>
    public IReadOnlyList<long> HistoryFeedbackAddresses { get; private set; } = [];

    public int HistoryFeedbackVersion { get; private set; }

    private Dictionary<long, VisibleByteFingerprint> CaptureVisibleByteFingerprints()
    {
        return ViewportSnapshot.Rows
            .SelectMany(row => row.Cells)
            .ToDictionary(
                cell => cell.Address,
                cell => new VisibleByteFingerprint(
                    cell.PrimaryValue,
                    cell.ComparisonValue,
                    cell.IsDataChanged,
                    cell.IsStructuralChanged));
    }

    private void PublishHistoryFeedback(IReadOnlyDictionary<long, VisibleByteFingerprint> before)
    {
        IReadOnlyDictionary<long, VisibleByteFingerprint> after = CaptureVisibleByteFingerprints();
        HistoryFeedbackAddresses = [
            .. after
                .Where(pair => !before.TryGetValue(pair.Key, out VisibleByteFingerprint previous) || previous != pair.Value)
                .Select(pair => pair.Key),
        ];
        OnPropertyChanged(nameof(HistoryFeedbackAddresses));
        HistoryFeedbackVersion++;
        OnPropertyChanged(nameof(HistoryFeedbackVersion));
        RefreshViewportSnapshot();
    }

    private void ResetHistoryFeedback()
    {
        HistoryFeedbackAddresses = [];
        OnPropertyChanged(nameof(HistoryFeedbackAddresses));
    }

    private readonly record struct VisibleByteFingerprint(
        byte Value,
        byte? Original,
        bool IsDataChanged,
        bool IsStructuralChanged);
}
