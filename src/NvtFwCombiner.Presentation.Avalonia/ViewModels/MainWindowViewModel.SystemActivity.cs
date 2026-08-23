using System.Globalization;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    internal void RecordStartupDuration(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        long milliseconds = checked((long)Math.Round(elapsed.TotalMilliseconds));
        RecordSystemActivity(new SystemActivityDraft(
            SystemActivityCodes.StartupReady,
            SystemActivityImportance.Important,
            SystemActivityCategory.Session,
            SystemActivitySeverity.Success,
            milliseconds.ToString(CultureInfo.InvariantCulture),
            "managed-entry-to-required-ready"));
    }

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
