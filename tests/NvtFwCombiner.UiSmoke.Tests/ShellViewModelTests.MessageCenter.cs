using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Cold catalog state owns the badge/global Build blocker and refresh clears it without a report.</summary>
    [Fact]
    public async Task MessageCenterKeepsSystemLifecycleSeparateFromRunReports()
    {
        StubCatalog catalog = new();
        SystemInformationService diagnostics = new(
            "0.10.3-test",
            catalog,
            catalog,
            new StubRuntimeProbe(),
            new StubClock());
        var exporter = new CapturingDiagnosticsExporter();
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.3-test");
        MainWindowViewModel viewModel = new(
            "test",
            "0.10.3-test",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                metadataReader: static (_, _) => null,
                batchReader: static (_, _) => []),
            systemInformationService: diagnostics,
            systemDiagnosticsExporter: exporter);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);

        Assert.Equal(1, viewModel.MessageCenter.ActiveBadgeCount);
        Assert.NotNull(viewModel.MessageCenter.GlobalBuildBlocker);
        Assert.Empty(viewModel.Reports.ReportHistoryEntries);
        Assert.Contains("Build is disabled", viewModel.MergeBuildBlockerText, StringComparison.Ordinal);

        viewModel.MessageCenter.OpenCommand.Execute(null);
        viewModel.MessageCenter.ShowRunReportsCommand.Execute(null);
        Assert.True(viewModel.MessageCenter.IsOpen);
        Assert.True(viewModel.MessageCenter.IsRunReportsSelected);
        Assert.Same(viewModel.Reports, viewModel.MessageCenter.Reports);

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, viewModel.MessageCenter.ActiveBadgeCount);
        Assert.Null(viewModel.MessageCenter.GlobalBuildBlocker);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.InputPending,
            viewModel.Merge.PrimaryBuildBlocker?.Code);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.InputPending,
            viewModel.Replace.PrimaryBuildBlocker?.Code);
        Assert.Empty(viewModel.Reports.ReportHistoryEntries);
        Assert.Contains(diagnostics.CreateBundle().Transitions, transition =>
            transition.ResolvedCodes.Contains(
                SystemDiagnosticCodes.CapabilityCatalogUnavailable,
                StringComparer.Ordinal));
    }

    /// <summary>Blocker navigation selects the exact localized System Information surface.</summary>
    [Fact]
    public async Task BuildBlockerAndDiagnosticsUseLocalizedUiProjections()
    {
        StubCatalog catalog = new();
        SystemInformationService diagnostics = new(
            "0.10.3-test",
            catalog,
            catalog,
            new StubRuntimeProbe(),
            new StubClock());
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.3-test");
        MainWindowViewModel viewModel = new(
            "test",
            "0.10.3-test",
            ShellLanguage.ChineseTraditional,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                metadataReader: static (_, _) => null,
                batchReader: static (_, _) => []),
            systemInformationService: diagnostics,
            systemDiagnosticsExporter: new CapturingDiagnosticsExporter());
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.MessageCenter.OpenCommand.Execute(null);

        Assert.True(viewModel.MessageCenter.IsOpen);
        Assert.True(viewModel.MessageCenter.IsSystemInformationSelected);
        Assert.Equal("無法使用", viewModel.MessageCenter.CatalogSummary);
        MessageCenterDiagnosticItem item = Assert.Single(viewModel.MessageCenter.ActiveDiagnostics);
        Assert.Contains("已停用 Build", item.Message, StringComparison.Ordinal);
        Assert.Contains("重新載入", item.Action, StringComparison.Ordinal);
        Assert.Contains("1 個目前診斷", viewModel.MessageCenter.MessageCenterAccessibleName,
            StringComparison.Ordinal);

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("必要輸入", viewModel.MergeBuildBlockerText, StringComparison.Ordinal);
        Assert.Contains("請載入必要輸入", viewModel.MergeBuildBlockerText, StringComparison.Ordinal);

        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        viewModel.SelectedLanguage = "English";
        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Contains(nameof(MainWindowViewModel.MergeBuildBlockerText), changed);
        Assert.Contains(nameof(MainWindowViewModel.ReplaceBuildBlockerText), changed);
        Assert.Contains("必要輸入", viewModel.MergeBuildBlockerText, StringComparison.Ordinal);
    }

    /// <summary>Only a changed publication token requests structured-session rebinding.</summary>
    [Fact]
    public async Task RefreshCallbackDistinguishesSameTokenLkgFromFreshPublication()
    {
        CanonicalSupportMatrixSnapshot first = Matrix("catalog:first");
        bool[] sameTokenCallbacks = await CaptureRefreshCallbacksAsync(new SequencedCatalog(
            Result(CanonicalSupportMatrixCatalogState.Current, first),
            Result(
                CanonicalSupportMatrixCatalogState.LastKnownGood,
                first,
                new CapabilityCatalogIssue("catalog.reload.failed", "private", null))));
        bool[] freshTokenCallbacks = await CaptureRefreshCallbacksAsync(new SequencedCatalog(
            Result(CanonicalSupportMatrixCatalogState.Current, first),
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix("catalog:second"))));

        Assert.Equal([false, false], sameTokenCallbacks);
        Assert.Equal([false, true], freshTokenCallbacks);
    }

    /// <summary>A same-token LKG warning preserves an already verified Standard Merge session.</summary>
    [Fact]
    public async Task SameTokenLkgReloadRetainsVerifiedStandardMergeSession()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        CanonicalSupportMatrixSnapshot current = Matrix("catalog:retained");
        var catalog = new SequencedCatalog(
            Result(CanonicalSupportMatrixCatalogState.Current, current),
            Result(
                CanonicalSupportMatrixCatalogState.LastKnownGood,
                current,
                new CapabilityCatalogIssue("catalog.reload.failed", "private", null)));
        MainWindowViewModel viewModel = CreateDiagnosticsViewModel(
            catalog,
            ReadBuiltInFirmwareInspectionBatch);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input")),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.All(viewModel.Merge.MergeSlots.Where(static slot => !slot.IsOptional), static slot =>
            Assert.Contains(
                slot.SemanticState,
                new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning }));
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.LastKnownGood,
            viewModel.MessageCenter.Current.CatalogState);
    }

    /// <summary>A fresh token fail-closes DP Replace until automatic reinspection republishes readiness.</summary>
    [Fact]
    public async Task FreshTokenReloadReinspectsVerifiedDpReplaceBeforeBuildReturns()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-message-center-rebind");
        var reader = new BlockingInspectionReader(
            (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience);
        var catalog = new SequencedCatalog(
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix("catalog:before")),
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix("catalog:after")));
        MainWindowViewModel viewModel = CreateDiagnosticsViewModel(catalog, reader.Read);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, ExperienceIds.DpReplace);
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceBase,
            workspace.Write("reference.bin", new byte[0x40000]));
        viewModel.SetSlotFile(
            CompositionSlotIds.ReplaceDp,
            workspace.Write("initial-code.bin", CreatePattern(0x40000, 0x41)));
        Assert.True(viewModel.Replace.CanBuildReplace);
        reader.BlockNextBatch();

        await viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        try
        {
            await reader.InspectionEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.False(viewModel.Replace.CanBuildReplace);
            Assert.True(viewModel.WorkflowSession.IsFirmwareInspectionLoading);
        }
        finally
        {
            reader.ReleaseInspection.SetResult();
        }

        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
    }

    /// <summary>Refresh exposes a visible progress label while the catalog source is busy.</summary>
    [Fact]
    public async Task RefreshCommandPublishesVisibleProgressUntilReloadCompletes()
    {
        var catalog = new BlockingReloadCatalog();
        MainWindowViewModel viewModel = CreateDiagnosticsViewModel(
            catalog,
            ReadBuiltInFirmwareInspectionBatch);
        Task refresh = viewModel.MessageCenter.RefreshCommand.ExecuteAsync(null);
        try
        {
            await catalog.ReloadEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Assert.True(viewModel.MessageCenter.IsRefreshInProgress);
            Assert.Equal(
                viewModel.Text.RefreshingDiagnosticsLabel,
                viewModel.MessageCenter.RefreshActionLabel);
        }
        finally
        {
            catalog.ReleaseReload.SetResult();
        }

        await refresh;
        Assert.False(viewModel.MessageCenter.IsRefreshInProgress);
        Assert.Equal(
            viewModel.Text.RefreshDiagnosticsLabel,
            viewModel.MessageCenter.RefreshActionLabel);
    }

    /// <summary>Diagnostics export consumes only the System Information bundle and never creates report history.</summary>
    [Fact]
    public async Task DiagnosticsExportDoesNotCreateBuildReport()
    {
        StubCatalog catalog = new();
        SystemInformationService diagnostics = new(
            "0.10.3-test",
            catalog,
            catalog,
            new StubRuntimeProbe(),
            new StubClock());
        var exporter = new CapturingDiagnosticsExporter();
        MainWindowViewModel viewModel = new(
            "test",
            "0.10.3-test",
            ShellLanguage.English,
            PresentationTestHost.CreateServices("0.10.3-test"),
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                metadataReader: static (_, _) => null,
                batchReader: static (_, _) => []),
            systemInformationService: diagnostics,
            systemDiagnosticsExporter: exporter);

        await viewModel.MessageCenter.ExportAsync(
            "ignored.json",
            TestContext.Current.CancellationToken);

        Assert.NotNull(exporter.Bundle);
        Assert.Equal(SystemDiagnosticsBundle.CurrentSchemaVersion, exporter.Bundle.SchemaVersion);
        Assert.Empty(viewModel.Reports.ReportHistoryEntries);
        Assert.False(viewModel.Reports.HasLoadedReport);
    }

    private static async Task<bool[]> CaptureRefreshCallbacksAsync(SequencedCatalog catalog)
    {
        SystemInformationService diagnostics = new(
            "0.10.3-test",
            catalog,
            catalog,
            new StubRuntimeProbe(),
            new StubClock());
        var text = ShellTextResources.For(ShellLanguage.English);
        var reports = new ReportPresentationViewModel(() => text, static () => { });
        var callbacks = new List<bool>();
        var viewModel = new MessageCenterViewModel(
            () => text,
            diagnostics,
            new CapturingDiagnosticsExporter(),
            reports,
            callbacks.Add);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        return [.. callbacks];
    }

    private MainWindowViewModel CreateDiagnosticsViewModel(
        ICanonicalSupportMatrixQuery catalog,
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>> firmwareInspectionReader)
    {
        ICanonicalCapabilityCatalogReloader reloader = catalog as ICanonicalCapabilityCatalogReloader ??
            throw new ArgumentException("The diagnostic test catalog must support reload.", nameof(catalog));
        SystemInformationService diagnostics = new(
            "0.10.3-test",
            catalog,
            reloader,
            new StubRuntimeProbe(),
            new StubClock());
        PresentationHostServices services = PresentationTestHost.CreateServices("0.10.3-test");
        var viewModel = new MainWindowViewModel(
            "test",
            "0.10.3-test",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: firmwareInspectionReader),
            systemInformationService: diagnostics,
            systemDiagnosticsExporter: new CapturingDiagnosticsExporter());
        return PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
    }

    private IReadOnlyList<FirmwareInspectionSnapshotResult> ReadBuiltInFirmwareInspectionBatch(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
    {
        return BuiltInFirmwareInspection.InspectFirmwareBatch(
            (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
            icId,
            inputs);
    }

    private static CanonicalSupportMatrixQueryResult Result(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix,
        params CapabilityCatalogIssue[] issues)
    {
        return new CanonicalSupportMatrixQueryResult(state, matrix, issues);
    }

    private static CanonicalSupportMatrixSnapshot Matrix(string token)
    {
        return new CanonicalSupportMatrixSnapshot(
            "canonical-capability-policy",
            "1.5.0",
            new string('a', 64),
            new ResolutionToken(token),
            []);
    }

    private sealed class SequencedCatalog(params CanonicalSupportMatrixQueryResult[] results) :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        private int _index;

        public CanonicalSupportMatrixQueryResult Query()
        {
            return results[Math.Min(_index, results.Length - 1)];
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _index = Math.Min(_index + 1, results.Length - 1);
        }
    }

    private sealed class BlockingReloadCatalog :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        internal TaskCompletionSource ReloadEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseReload { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public CanonicalSupportMatrixQueryResult Query()
        {
            return Result(CanonicalSupportMatrixCatalogState.Current, Matrix("catalog:blocking"));
        }

        public void Reload(CancellationToken cancellationToken)
        {
            ReloadEntered.SetResult();
            ReleaseReload.Task.Wait(cancellationToken);
        }
    }

    private sealed class BlockingInspectionReader(BuiltInFirmwareInspection firmwareInspection)
    {
        private int _blockNextBatch;

        internal TaskCompletionSource InspectionEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseInspection { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal void BlockNextBatch()
        {
            Volatile.Write(ref _blockNextBatch, 1);
        }

        internal IReadOnlyList<FirmwareInspectionSnapshotResult> Read(
            string selectedIc,
            IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
        {
            if (Interlocked.Exchange(ref _blockNextBatch, 0) == 1)
            {
                InspectionEntered.SetResult();
                ReleaseInspection.Task.Wait(TestContext.Current.CancellationToken);
            }

            return BuiltInFirmwareInspection.InspectFirmwareBatch(firmwareInspection, selectedIc, inputs);
        }
    }

    private sealed class StubCatalog :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        private bool _reloaded;

        public CanonicalSupportMatrixQueryResult Query()
        {
            return _reloaded
                ? new CanonicalSupportMatrixQueryResult(
                    CanonicalSupportMatrixCatalogState.Current,
                    new CanonicalSupportMatrixSnapshot(
                        "canonical-capability-policy",
                        "1.5.0",
                        new string('a', 64),
                        new ResolutionToken("catalog:ui-test"),
                        []))
                : new CanonicalSupportMatrixQueryResult(
                    CanonicalSupportMatrixCatalogState.ColdStartBlocked,
                    matrix: null,
                    [new CapabilityCatalogIssue("catalog.invalid", "private detail", null)]);
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reloaded = true;
        }
    }

    private sealed class StubRuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe()
        {
            return new SystemRuntimeFacts(".NET test", "Windows test", "x64");
        }
    }

    private sealed class StubClock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class CapturingDiagnosticsExporter : ISystemDiagnosticsExporter
    {
        internal SystemDiagnosticsBundle? Bundle { get; private set; }

        public ValueTask ExportAsync(
            SystemDiagnosticsBundle bundle,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Bundle = bundle;
            return ValueTask.CompletedTask;
        }
    }
}
