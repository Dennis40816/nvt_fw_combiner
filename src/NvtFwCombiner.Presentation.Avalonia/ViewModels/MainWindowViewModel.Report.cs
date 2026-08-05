using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Focused report review, history, persistence projection, and notification presentation.</summary>
    public ReportPresentationViewModel Reports { get; }

    private void Reports_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MessageCenter.NotifyReportHistoryChanged();
        if (e.PropertyName is nameof(ReportPresentationViewModel.IsReportModalOpen) or
            nameof(ReportPresentationViewModel.HasReportToast))
        {
            OnPropertyChanged(nameof(Reports));
        }

        if (e.PropertyName == nameof(ReportPresentationViewModel.IsReportModalOpen))
        {
            NotifyCompositionActionRailVisibilityChanged();
        }
    }
}
