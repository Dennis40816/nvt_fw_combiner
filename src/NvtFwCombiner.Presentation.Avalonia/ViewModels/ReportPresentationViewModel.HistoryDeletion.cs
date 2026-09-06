using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportPresentationViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryDeleteConfirmationOpen))]
    public partial ReportHistoryEntryViewModel? PendingHistoryDeletion { get; private set; }

    public bool IsHistoryDeleteConfirmationOpen => PendingHistoryDeletion is not null;

    [RelayCommand]
    private void RequestReportHistoryDeletion(ReportHistoryEntryViewModel? entry)
    {
        if (PendingHistoryDeletion is not null || entry is null || !ReportHistoryEntries.Contains(entry))
        {
            return;
        }

        CancelReportHistoryReopen();
        PendingHistoryDeletion = entry;
    }

    [RelayCommand]
    private void CancelReportHistoryDeletion()
    {
        PendingHistoryDeletion = null;
    }

    [RelayCommand]
    private void ConfirmReportHistoryDeletion()
    {
        ReportHistoryEntryViewModel? entry = PendingHistoryDeletion;
        PendingHistoryDeletion = null;
        RemoveReportHistoryEntry(entry);
    }
}
