using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class ReportPresentationViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryDeleteConfirmationOpen))]
    public partial ReportHistoryEntryViewModel? PendingHistoryDeletion { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryDeleteConfirmationOpen))]
    public partial bool IsClearAllHistoryDeletionPending { get; private set; }

    public bool IsHistoryDeleteConfirmationOpen => PendingHistoryDeletion is not null || IsClearAllHistoryDeletionPending;

    public string HistoryDeletionTitle => IsClearAllHistoryDeletionPending ? Text.ClearHistoryTitle : Text.DeleteHistoryTitle;

    public string HistoryDeletionDetail => IsClearAllHistoryDeletionPending ? Text.ClearHistoryDetail : Text.DeleteHistoryDetail;

    public string HistoryDeletionConfirmLabel => IsClearAllHistoryDeletionPending ? Text.ClearAllLabel : Text.DeleteHistoryConfirmLabel;

    partial void OnIsClearAllHistoryDeletionPendingChanged(bool value) => NotifyHistoryDeletionTextChanged();

    private void NotifyHistoryDeletionTextChanged()
    {
        OnPropertyChanged(nameof(HistoryDeletionTitle));
        OnPropertyChanged(nameof(HistoryDeletionDetail));
        OnPropertyChanged(nameof(HistoryDeletionConfirmLabel));
    }

    private void RequestClearReportHistory()
    {
        if (IsHistoryDeleteConfirmationOpen || !HasReportHistory)
        {
            return;
        }

        if (!IsReportModalOpen)
        {
            ShowReportHistory();
        }

        CancelReportHistoryReopen();
        IsClearAllHistoryDeletionPending = true;
    }

    [RelayCommand]
    private void RequestReportHistoryDeletion(ReportHistoryEntryViewModel? entry)
    {
        if (IsHistoryDeleteConfirmationOpen || entry is null || !ReportHistoryEntries.Contains(entry))
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
        IsClearAllHistoryDeletionPending = false;
    }

    [RelayCommand]
    private void ConfirmReportHistoryDeletion()
    {
        if (!IsHistoryDeleteConfirmationOpen)
        {
            return;
        }

        ReportHistoryEntryViewModel? entry = PendingHistoryDeletion;
        bool clearAll = IsClearAllHistoryDeletionPending;
        CancelReportHistoryDeletion();
        if (clearAll)
        {
            ClearReportHistory();
        }
        else
        {
            RemoveReportHistoryEntry(entry);
        }
    }
}
