namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps startup diagnostics opt-in, Presentation-local, and firmware-neutral.</summary>
    [Fact]
    public void StartupDiagnosticsStayPresentationLocalAndOptIn()
    {
        string session = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/StartupTraceSession.cs");
        string sink = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/StartupTraceFileSink.cs");
        string program = ReadText("src/NvtFwCombiner.Presentation.Avalonia/DesktopApplication.cs");
        string application = ReadText("src/NvtFwCombiner.Presentation.Avalonia/App.axaml.cs");
        string window = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string runner = ReadText("scripts/measure-startup.ps1");
        string diagnostics = string.Join(Environment.NewLine, session, sink);

        Assert.Contains("NFC_STARTUP_TRACE_PATH", session, StringComparison.Ordinal);
        Assert.Contains("FileMode.CreateNew", sink, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", sink, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Domain", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Func<PresentationHostServices>", program, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(program, "hostServicesFactory()"));
        int preparationStart = program.IndexOf(
            ") PrepareStartup(",
            StringComparison.Ordinal);
        Assert.True(preparationStart >= 0);
        string preparation = program[preparationStart..];
        Assert.Contains("Task.Run(", preparation, StringComparison.Ordinal);
        AssertStartupStageOrder(
            preparation,
            "shell-preferences.started",
            "shellPreferenceLoader()",
            "host-services.started",
            "hostServicesFactory()",
            "host-services.ready");
        AssertStartupStageOrder(
            program,
            "StartFromEnvironment()",
            "PrepareStartup(",
            "launch-options.parsed");
        Assert.Contains("ShellPreferenceFileStore.LoadAsync", program, StringComparison.Ordinal);
        Assert.Contains("launch-options.parsed", program, StringComparison.Ordinal);
        Assert.Contains("application-xaml.ready", application, StringComparison.Ordinal);
        AssertStartupStageOrder(
            application,
            "framework-initialization.started",
            "_startupPreferences.GetAwaiter().GetResult()",
            "shell-preferences.loaded",
            "new MainWindow(");
        Assert.Contains("shell-view-model.created", window, StringComparison.Ordinal);
        Assert.Contains("ShellPreferenceSnapshot startupPreferences", window, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceSnapshot? startupPreferences", window, StringComparison.Ordinal);
        Assert.DoesNotContain("startupPreferences ??", window, StringComparison.Ordinal);
        Assert.Contains(
            "CreateStartupViewModel(_hostServices, startupPreferences)",
            window,
            StringComparison.Ordinal);
        Assert.Contains("shell-data-context.assigned", window, StringComparison.Ordinal);
        Assert.Contains("shell-initial-content.ready", window, StringComparison.Ordinal);
        Assert.Contains("main-window.opened", window, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.completed", window, StringComparison.Ordinal);
        Assert.Contains("EnvironmentVariables[$TracePathEnvironmentVariable]", runner, StringComparison.Ordinal);
        Assert.Contains("workingSetBytesAtWindow", runner, StringComparison.Ordinal);
        Assert.Contains("workingSetBytesAtTrace", runner, StringComparison.Ordinal);
        Assert.Contains("peakWorkingSetBytes", runner, StringComparison.Ordinal);
        Assert.Contains("privateBytesAtWindow", runner, StringComparison.Ordinal);
        Assert.Contains("privateBytesAtTrace", runner, StringComparison.Ordinal);
        Assert.Contains("peakPrivateBytes", runner, StringComparison.Ordinal);
        Assert.Contains("RequirePreloadLifecycle", runner, StringComparison.Ordinal);
        Assert.Contains("Assert-ReleaseSampleCounts", runner, StringComparison.Ordinal);
        Assert.Contains("Assert-ReleaseStartupPage", runner, StringComparison.Ordinal);
        Assert.Contains("New-StartupMeasurementValidation", runner, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-ValidatedElapsedMilliseconds", runner, StringComparison.Ordinal);
        Assert.Contains("releaseAdmissionPassed", runner, StringComparison.Ordinal);
        Assert.Contains("exact lowercase 'home' startup page", runner, StringComparison.Ordinal);
        Assert.Contains("complete ordered preload stage set", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.catalog-state.applied", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.device-context.started", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.replace-view.started", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.merge-view.started", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.settings-view.started", runner, StringComparison.Ordinal);
        Assert.Contains("startup-warmup.hex-editor-view.started", runner, StringComparison.Ordinal);
        Assert.Contains("preloadLifecycle", runner, StringComparison.Ordinal);
        Assert.Contains("$stages = @(", runner, StringComparison.Ordinal);
        Assert.Contains("if ($null -ne $parsedTrace)", runner, StringComparison.Ordinal);
        Assert.Contains("AllocatedBytesSinceManagedEntry", session, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ShellPreloadStageSnapshot>", session, StringComparison.Ordinal);
        Assert.Contains("allocatedBytesSinceManagedEntry", sink, StringComparison.Ordinal);
        Assert.Contains("allocationDeltaBytes", sink, StringComparison.Ordinal);
        Assert.Contains("preloadStages", sink, StringComparison.Ordinal);
        Assert.Contains("stage.CurrentAttempt?.CompletedWork", sink, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize", sink, StringComparison.Ordinal);
        Assert.Contains("uiThreadWork", runner, StringComparison.Ordinal);
        Assert.Contains("firstFrameUiSynchronousWorkMilliseconds", runner, StringComparison.Ordinal);
        Assert.Contains("backgroundUiMaterializationMilliseconds", runner, StringComparison.Ordinal);
        Assert.Contains("nfc-startup-measurement-v3", runner, StringComparison.Ordinal);
        Assert.Contains("return [pscustomobject][ordered]@{", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-WebRequest", runner, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Keeps the approved v0.10.5 preload intake evidence and migration ledger exact.</summary>
    [Fact]
    public void V0105PreloadBaselineAndLifecycleLedgerStayFrozen()
    {
        AssertV0105PreloadBaselineIsFrozen();
    }

    private static void AssertV0105PreloadBaselineIsFrozen()
    {
        string baseline = ReadText(
            "docs/governance/v0.10.5-preload-baseline-and-ticket-ledger.md");
        string specification = ReadText("docs/specs/v0.10.5-unified-preload-lifecycle.md");
        string decision = ReadText("docs/adr/0049-unified-preload-lifecycle.md");
        string sizeAdr = ReadText("docs/adr/0021-code-size-ratchet-and-convergence.md");
        string changelog = ReadText("CHANGELOG.md");
        string codeSizePolicy = ReadText("scripts/code_size_policy.py");
        string mainWindow = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string preloadSession = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ShellPreloadSession.cs");
        string report = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Report.cs");
        string history = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs");
        string external = ReadText(
            "src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessorEnvironmentLoader.cs");
        string inspection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareInspectionSession.cs");
        string inspectionLifecycle = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");
        string firmwareSlot = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string runProgress = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionRunProgress.cs");
        string generalMerge = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs");
        string generalReplace = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.General.cs");
        string ctrlRamRunMetadata = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.CtrlRamFirmwareVersion.cs");
        string build = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Build.cs");
        string replaceModal = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/Views/ReplaceSelectionModal.axaml.cs");
        string outputDelivery = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/OutputDeliveryConfirmationViewModel.cs");
        string replaceExecution = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");

        foreach (string row in new[]
                 {
                     "| annotated tag | `v0.10.4` |",
                     "| source commit | `8c3cc51dc95cea2fbae8ec5ac0287db730d1b37b` |",
                     "| source tree | `48ee0fd87d942674acf83c910de78681db748458` |",
                     "| successful release workflow | run `31623880051`, attempt 2 |",
                     "| package | `NvtFwCombiner-v0.10.4-win-x64.zip`, 77,025,377 bytes |",
                 })
        {
            Assert.Equal(1, CountOccurrences(baseline, row));
        }

        foreach (string identity in new[]
                 {
                     "8c3cc51dc95cea2fbae8ec5ac0287db730d1b37b",
                     "48ee0fd87d942674acf83c910de78681db748458",
                     "run `31623880051`, attempt 2",
                     "77,025,377 bytes",
                     "06f9c63ef0d7384dfe7c6b850e80fec94b7a7c29368e4499dfb781f1187dcf3f",
                     "3ad2051d75730a5e1409e6398a83c11bfbab6ead05c497846e8a750d243299d4",
                     "cc63b5fa49bc6c27466bc1320e928c6d34ed169e2ef96f32157ed82762104f6c",
                     "c51a69f0e1783919532eef85336e249c8f8f9e920bc456777534602bd56ca0ac",
                     "88782b1a77f41002eede3779e4fc438bc876166ca4cb0a73db56a5407e344620",
                     "9da227e2acf585e389d630f8741c19309f333e9ca708cd725b5724968237d19c",
                     "f5c2062c3cec80299641be9cae2113f0cc199dd6c88e4503ed71e53374fa79a5",
                     "c921b58beea244504aa06e32556b22ab4fe20057bc485005d88ebc6ecaaab41d",
                     "ddd27f07311fd599b258c9e41e26c424b3bb8bae02b33e89a304b6f9b1f5b142",
                 })
        {
            Assert.Equal(1, CountOccurrences(baseline, identity));
        }
        (string Metric, int Lines)[] codeSizeRows =
        [
            ("predecessor full production", 97_306),
            ("runtime production", 67_186),
            ("Domain plus Profiles", 20_619),
            ("Application", 29_383),
            ("Bootstrap plus CLI", 3_255),
            ("Infrastructure plus Contracts plus CRC worker", 13_929),
        ];
        foreach ((string metric, int lines) in codeSizeRows)
        {
            string row = $"| {metric} | {lines.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} |";
            Assert.Equal(1, CountOccurrences(baseline, row));
        }
        Assert.Equal(67_186, codeSizeRows.Skip(2).Sum(static row => row.Lines));
        Assert.Contains("284 gross removed, 281", baseline, StringComparison.Ordinal);
        Assert.Contains("net **-3**", baseline, StringComparison.Ordinal);
        Assert.Contains("97,306 to 97,303", baseline, StringComparison.Ordinal);
        Assert.Contains("1,154 gross removed and 1,283", baseline, StringComparison.Ordinal);
        Assert.Contains("97,297 to 97,426", baseline, StringComparison.Ordinal);
        Assert.Contains("67,371 to 67,404", baseline, StringComparison.Ordinal);
        Assert.Contains("250 gross removed and 984", baseline, StringComparison.Ordinal);
        Assert.Contains("97,426 to 98,160", baseline, StringComparison.Ordinal);
        Assert.Contains("67,404 to 68,018", baseline, StringComparison.Ordinal);
        Assert.Contains("67 added and 38 removed", baseline, StringComparison.Ordinal);
        Assert.Contains("98,160 to 98,189", baseline, StringComparison.Ordinal);
        Assert.Contains("403 gross removed and 289", baseline, StringComparison.Ordinal);
        Assert.Contains("net **-114**", baseline, StringComparison.Ordinal);
        Assert.Contains("98,189 to 98,075", baseline, StringComparison.Ordinal);
        Assert.Contains("68,018 to 68,016", baseline, StringComparison.Ordinal);
        Assert.Contains("950 gross removed and 949", baseline, StringComparison.Ordinal);
        Assert.Contains("98,075 to 98,074", baseline, StringComparison.Ordinal);
        Assert.Contains("68,016 to 67,997", baseline, StringComparison.Ordinal);
        Assert.Contains("295 gross removed and 358", baseline, StringComparison.Ordinal);
        Assert.Contains("98,074 to 98,137", baseline, StringComparison.Ordinal);
        Assert.Contains("673 removed and 2,103 added", sizeAdr, StringComparison.Ordinal);
        Assert.Contains("98,133 to **99,563**", baseline, StringComparison.Ordinal);
        Assert.Contains("67,981 to **68,109**", baseline, StringComparison.Ordinal);
        Assert.Contains("full_production_ratchet=102_896", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("98,135 to **98,133**", baseline, StringComparison.Ordinal);
        Assert.Contains("runtime_production_ratchet=70_056", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("domain_profiles_ratchet=20_632", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("application_ratchet=30_690", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("bootstrap_cli_ratchet=3_378", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("infrastructure_contracts_worker_ratchet=15_356", codeSizePolicy, StringComparison.Ordinal);
        Assert.Contains("Historical original PL-00 instrumentation/semantic-control envelope", baseline, StringComparison.Ordinal);
        Assert.Contains("except for the owner-approved 2026-08-18", specification, StringComparison.Ordinal);
        Assert.Contains("from 98,137 to 98,135", specification, StringComparison.Ordinal);
        Assert.Contains("production from 98,135 to 98,133", specification, StringComparison.Ordinal);
        Assert.Contains("673 removed and 2,103 added", specification, StringComparison.Ordinal);
        Assert.Contains("full production from 98,133 to 99,563", specification, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(changelog, "#### Message Center, report, and System Information readability"));
        Assert.DoesNotContain("#### Message Center report and System Information readability", changelog, StringComparison.Ordinal);
        Assert.DoesNotContain("#### Message Center and report readability", changelog, StringComparison.Ordinal);
        Assert.Contains("PL-00 release-candidate validation is in", changelog, StringComparison.Ordinal);
        Assert.Contains("Historical PL-00 candidate evidence (superseded for release admission)", baseline, StringComparison.Ordinal);
        Assert.Contains("validation.releaseAdmissionPassed=true", baseline, StringComparison.Ordinal);
        Assert.Contains("Windows 11 Enterprise", baseline, StringComparison.Ordinal);
        Assert.Contains("Implementation complete; PL-00 release-candidate validation in progress", specification, StringComparison.Ordinal);
        Assert.Contains("631.132 ms and 634.589 ms", changelog, StringComparison.Ordinal);
        Assert.Contains("Screen-reader review", changelog, StringComparison.Ordinal);
        Assert.Contains("release approval", changelog, StringComparison.Ordinal);
        Assert.Contains("777.090 ms", baseline, StringComparison.Ordinal);
        Assert.Contains("806.930 ms", baseline, StringComparison.Ordinal);
        Assert.Contains("782.368 ms", baseline, StringComparison.Ordinal);
        Assert.Contains("Neither stable-package median satisfies", baseline, StringComparison.Ordinal);
        Assert.Contains(
            "78 static/dynamic routes plus one disclosure unit",
            baseline,
            StringComparison.Ordinal);

        (string Ticket, string Anchors, int Gross, int Added, int Net)[] ticketRows =
                 [
                     ("PL-01 #373", "this document; `RepositoryBoundaryTests.StartupDiagnostics.cs`", 0, 0, 0),
                     ("PL-02 #374", "`MainWindow.Report.cs:11-45,108-120`; `ReportHistoryFileStore.cs:10-51`; `ReportPresentationViewModel.History.cs:94-175`", 110, 90, -20),
                     ("PL-03 #375", "`CanonicalCatalogStartupCoordinator.cs:1-139`; `MainWindow.axaml.cs:22-33,219-318,337-412`", 260, 230, -30),
                     ("PL-04 #376", "`MainWindow.axaml.cs:227-265`; `MainWindow.Report.cs:38-120`; `MainWindow.StartupWarmup.cs:9-92`; `MessageCenterViewModel.cs:147-241`", 220, 190, -30),
                     ("PL-05 #377", "`ExternalProcessorFactory.cs:23-143,155-219` (exactly 170 nonblank owner lines); callers/wiring retained or migrated at `RuntimeDependencyReadinessLeaseProvider.cs:6-16` and `CompositionHostServices.cs:36-64`", 170, 145, -25),
                     ("PL-06 #378", "`FirmwareInspectionSession.cs:8-163`; `BuiltInFirmwareInspection.cs:19,470-537`; `BuiltInFirmwareInspection.FileIdentity.cs:19-74`", 220, 190, -30),
                     ("PL-07 #379", "`WorkflowSessionPresentationViewModel.FirmwareInspection.cs:8-11,126-211,341-539`; `MergePresentationViewModel.General.cs:14,66-115`; `ReplacePresentationViewModel.General.cs:16,93-157` (375 nonblank candidate lines before retained result application is separated)", 250, 220, -30),
                      ("PL-00 #380", "`StartupTraceFileSink.cs`; `StartupTraceSession.cs`; `MainWindow.axaml.cs`; `ShellTextResources`; shared button/navigation roles; CtrlRAM relocalization; immutable Application DTO backing-field aliases; `scripts/measure-startup.ps1`", 295, 358, 63),
                 ];
        foreach ((string ticket, string anchors, int gross, int added, int net) in ticketRows)
        {
            string row = Assert.Single(
                baseline.Split('\n', StringSplitOptions.TrimEntries),
                line => line.StartsWith($"| {ticket} |", StringComparison.Ordinal));
            string[] cells = row.Split('|', StringSplitOptions.TrimEntries);
            Assert.Equal(ticket, cells[1]);
            Assert.Equal(anchors, cells[2]);
            Assert.Equal(gross.ToString(System.Globalization.CultureInfo.InvariantCulture), cells[3]);
            Assert.Equal(added.ToString(System.Globalization.CultureInfo.InvariantCulture), cells[4]);
            string expectedNet = net > 0
                ? $"+{net.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : net.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedNet, cells[5]);
            Assert.Equal(added - gross, net);
        }

        Assert.Contains("at most **two workers**", baseline, StringComparison.Ordinal);
        Assert.Contains(
            "Every queued progress, terminal, dispatcher, and accessibility callback",
            baseline,
            StringComparison.Ordinal);
        Assert.Contains(
            "stage-state updates still carry the shell-session generation and stable",
            baseline,
            StringComparison.Ordinal);
        Assert.Contains(
            "stage id, but expose no current attempt identity until work is admitted",
            baseline,
            StringComparison.Ordinal);
        Assert.Contains("fake aggregate percentage", baseline, StringComparison.Ordinal);
        Assert.Contains("exactly one terminal outcome", baseline, StringComparison.Ordinal);
        Assert.Contains("third attempt deterministically evicts", baseline, StringComparison.Ordinal);
        Assert.Contains("Preview/Build progress remains excluded", baseline, StringComparison.Ordinal);
        Assert.Contains(
            "snapshots: the current attempt and the immediately preceding terminal summary.",
            specification,
            StringComparison.Ordinal);
        Assert.Contains("two concurrent workers.", decision, StringComparison.Ordinal);
        Assert.Contains("never summed into a false overall percent", decision, StringComparison.Ordinal);

        Assert.Contains("_startupLoadCancellation", mainWindow, StringComparison.Ordinal);
        Assert.Contains("RunRequiredPreloadAsync", mainWindow, StringComparison.Ordinal);
        Assert.Contains("RunCatalogAsync", preloadSession, StringComparison.Ordinal);
        Assert.Contains("ReadOnlyObservableCollection<ShellPreloadStageSnapshot>", preloadSession, StringComparison.Ordinal);
        Assert.Contains("OptionalWorkerBudget = 2", preloadSession, StringComparison.Ordinal);
        Assert.Contains("DrainTimeout = TimeSpan.FromSeconds(5)", preloadSession, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalCatalogStartupCoordinator", mainWindow, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Presentation.Avalonia/CanonicalCatalogStartupCoordinator.cs")));
        Assert.Contains("RefreshAfterStartupAsync", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadToEndAsync", report, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllText", report, StringComparison.Ordinal);
        Assert.Contains("MaximumStandaloneReportBytes", report, StringComparison.Ordinal);
        Assert.Contains("ReadTextAsync", report, StringComparison.Ordinal);
        Assert.Contains("MaximumHistoryFileBytes", history, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run(() => Load", history, StringComparison.Ordinal);
        Assert.Contains("files.ReadAsync", history, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded<ExternalProcessorEnvironmentLoadUpdate>", external, StringComparison.Ordinal);
        Assert.Contains("_requestGeneration", external, StringComparison.Ordinal);
        Assert.Contains("_publicationGeneration", external, StringComparison.Ordinal);
        Assert.Contains("ExternalProcessorEnvironmentState.LastKnownGood", external, StringComparison.Ordinal);
        Assert.Contains("FindExternalToolsRoot", external, StringComparison.Ordinal);
        Assert.Contains("MaximumVisitedEntries = 4_096", external, StringComparison.Ordinal);
        Assert.Contains("MaximumManifestCount = 256", external, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", external, StringComparison.Ordinal);
        Assert.DoesNotContain("_fileProjections", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain("_baseCache", inspection, StringComparison.Ordinal);
        Assert.Contains("InspectFirmwareBatchAsync", inspectionLifecycle, StringComparison.Ordinal);
        Assert.Contains("CurrentInspectionProjection", firmwareSlot, StringComparison.Ordinal);
        Assert.Contains("Channel.CreateBounded", runProgress, StringComparison.Ordinal);
        Assert.DoesNotContain("_generalMergePreparationQueue", generalMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralMergeReadinessRefreshTask", generalMerge, StringComparison.Ordinal);
        Assert.Contains("InspectionLifecycles[GeneralMergeMode].StartAsync", generalMerge, StringComparison.Ordinal);
        Assert.Contains("PrepareMergeSessionAsync(", generalMerge, StringComparison.Ordinal);
        Assert.Contains("cancellationToken,", generalMerge, StringComparison.Ordinal);
        Assert.Contains("progress);", generalMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("_generalReplacePreparationQueue", generalReplace, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceReadinessRefreshTask", generalReplace, StringComparison.Ordinal);
        Assert.Contains("InspectionLifecycles[GeneralReplaceMode].StartAsync", generalReplace, StringComparison.Ordinal);
        Assert.Contains("PrepareReplaceSessionAsync(", generalReplace, StringComparison.Ordinal);
        Assert.Contains("cancellationToken,", generalReplace, StringComparison.Ordinal);
        Assert.Contains("progress);", generalReplace, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/SerialTaskQueue.cs")));
        Assert.Contains("_ctrlRamFirmwareVersionMetadataGeneration", ctrlRamRunMetadata, StringComparison.Ordinal);
        Assert.Contains("TryOpenCtrlRamFirmwareVersionModalAsync", ctrlRamRunMetadata, StringComparison.Ordinal);
        Assert.Contains("TryCreateCtrlRamFirmwareVersionEditAsync", ctrlRamRunMetadata, StringComparison.Ordinal);
        Assert.Contains(
            "IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync",
            ctrlRamRunMetadata,
            StringComparison.Ordinal);
        Assert.Contains("ReadCtrlRamFirmwareVersionMetadataAsync", ctrlRamRunMetadata, StringComparison.Ordinal);
        Assert.Contains("RequestCtrlRamBuildSettingsAsync", build, StringComparison.Ordinal);
        Assert.Contains("RequestCtrlRamBuildSettingsAsync", replaceModal, StringComparison.Ordinal);
        Assert.Contains("TryCreateCtrlRamFirmwareVersionEditAsync", replaceExecution, StringComparison.Ordinal);
        Assert.Contains(
            "IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync",
            replaceExecution,
            StringComparison.Ordinal);
        Assert.Contains("PrepareModeSpecificAsync", outputDelivery, StringComparison.Ordinal);
        Assert.Contains("CtrlRAM run metadata", baseline, StringComparison.Ordinal);
        Assert.Contains("Explicitly excluded by the specification", baseline, StringComparison.Ordinal);
    }

    private static void AssertStartupStageOrder(string source, params string[] stages)
    {
        int previous = -1;
        foreach (string stage in stages)
        {
            int current = source.IndexOf(stage, StringComparison.Ordinal);
            Assert.True(current > previous, $"Startup stage '{stage}' is missing or out of order.");
            previous = current;
        }
    }
}
