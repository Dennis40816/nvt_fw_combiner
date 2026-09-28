using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>The fallback warning localizes repair/restart guidance without suggesting a re-selection on Reload.</summary>
    [Fact]
    public void PrebuiltFallbackWarningHasLocalizedRepairAndRestartGuidance()
    {
        var diagnostic = new ActionableSystemDiagnostic(SystemDiagnosticCodes.PrebuiltCatalogUnused,
            SystemDiagnosticCategory.CapabilityCatalog, SystemDiagnosticSeverity.Warning,
            "JSON admission is in use.", "Repair the package and restart.");
        ShellTextResources english = ShellTextResources.For(ShellLanguage.English);
        ShellTextResources chinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        Assert.Equal(diagnostic.Message, english.GetSystemDiagnosticMessage(diagnostic));
        Assert.Equal(diagnostic.Action, english.GetSystemDiagnosticAction(diagnostic));
        Assert.Equal("未使用預建 Profile 目錄；目前使用 JSON admission。", chinese.GetSystemDiagnosticMessage(diagnostic));
        Assert.Equal("請修復或重新安裝套件，然後重新啟動應用程式。", chinese.GetSystemDiagnosticAction(diagnostic));
    }

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
