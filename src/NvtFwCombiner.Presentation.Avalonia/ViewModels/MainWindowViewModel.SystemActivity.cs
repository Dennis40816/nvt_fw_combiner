using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void RecordSystemActivity(SystemActivityDraft activity)
    {
        if (_isInitializing)
        {
            return;
        }

        _systemInformationService.RecordActivity(activity);
        MessageCenter.NotifyActivityChanged();
    }

    private void RecordDebugActivity(
        string code,
        SystemActivityCategory category,
        string? subjectId = null,
        string? contextId = null)
    {
        RecordSystemActivity(new SystemActivityDraft(
            code,
            SystemActivityImportance.Debug,
            category,
            SystemActivitySeverity.Information,
            subjectId,
            contextId));
    }
}
