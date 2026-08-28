using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Important events are the default; Debug explicitly reveals user operations.</summary>
    [Fact]
    public void ActivityHistoryUsesTwoDisclosureLevels()
    {
        StubCatalog catalog = new();
        SystemInformationService diagnostics = new(
            "0.10.6-test",
            catalog,
            catalog,
            CreateExternalEnvironmentLoader(),
            new StubRuntimeProbe(),
            new StubClock());
        var text = ShellTextResources.For(ShellLanguage.English);
        var viewModel = new MessageCenterViewModel(
            () => text,
            diagnostics,
            CreateExternalEnvironmentLoader(),
            new CapturingDiagnosticsExporter(),
            new ReportPresentationViewModel(() => text, static () => { }),
            static _ => { });
        diagnostics.RecordActivity(new SystemActivityDraft(
            SystemActivityCodes.UserNavigated,
            SystemActivityImportance.Debug,
            SystemActivityCategory.Navigation,
            SystemActivitySeverity.Information,
            "Merge"));
        diagnostics.RecordActivity(new SystemActivityDraft(
            SystemActivityCodes.StartupReady,
            SystemActivityImportance.Important,
            SystemActivityCategory.Session,
            SystemActivitySeverity.Success,
            "1250",
            "managed-entry-to-required-ready"));
        viewModel.NotifyActivityChanged();

        Assert.DoesNotContain(viewModel.ActivityItems, item => item.Title == "Page changed");
        MessageCenterActivityItem startup = Assert.Single(
            viewModel.ActivityItems,
            item => item.Title == "Application ready");
        Assert.Equal("Managed entry to ready · 1,250 ms", startup.Detail);

        viewModel.ToggleDebugActivityCommand.Execute(null);

        Assert.Contains(viewModel.ActivityItems, item => item.Title == "Page changed");
        Assert.Contains("events", viewModel.SessionActivitySummary, StringComparison.Ordinal);

        text = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        viewModel.ApplyLanguageChanged();
        startup = Assert.Single(viewModel.ActivityItems, item => item.Title == "應用程式已就緒");
        Assert.Equal("Managed 進入點至就緒 · 1,250 毫秒", startup.Detail);
    }
}
