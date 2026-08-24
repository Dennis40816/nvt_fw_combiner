namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies local UI state and report inputs share one bounded platform file adapter.</summary>
    [Fact]
    public void LocalUiFileStoresShareOneBoundedPlatformAdapter()
    {
        string adapter = ReadText("src/NvtFwCombiner.Infrastructure/Files/LocalFileStore.cs");
        string codec = ReadText("src/NvtFwCombiner.Presentation.Avalonia/LocalJsonDocument.cs");
        string mainWindow = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs");
        string startupFactory = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/MainWindow.StartupFactory.cs");
        string reportInput = ReadText("src/NvtFwCombiner.Presentation.Avalonia/MainWindow.Report.cs");
        string history = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs");
        string historyProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportPresentationViewModel.History.cs");
        string bootstrap = ReadText("src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs");
        string construction = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Construction.cs");
        string settings = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Settings.cs");
        string context = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Context.cs");
        string navigation = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MainWindowViewModel.Navigation.cs");
        string persistenceCoordinator = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/LatestSnapshotPersistenceCoordinator.cs");
        string stores = ReadText("src/NvtFwCombiner.Presentation.Avalonia/ReportHistoryFileStore.cs") +
            ReadText("src/NvtFwCombiner.Presentation.Avalonia/ShellPreferenceFileStore.cs");

        AssertContainsAll(bootstrap, "LocalFiles = new LocalFileStore();",
            "public static ILocalFileStore CreateLocalFileStore()");
        AssertContainsAll(adapter, "FileShare.Read | FileShare.Delete",
            "AtomicFileWriteScope.Open(fullPath)");
        AssertContainsAll(reportInput, "MaximumStandaloneReportBytes = 10L * 1024 * 1024");
        AssertDoesNotContainAny(reportInput, "File.ReadAllText", "ReadToEndAsync", "Task.Run");
        AssertContainsAll(history, "MaximumHistoryFileBytes = 64L * 1024 * 1024",
            "ReportPresentationViewModel.MaximumReportHistoryStorageBytes", "EntryTooLargeToPersist",
            "OmitDerivableReportHistoryMetadata", "RemoveAt(retained.Count - 1)");
        AssertContainsAll(historyProjection, "normalized!.Metadata == snapshot.Metadata");
        AssertContainsAll(stores, "internal const long MaximumPreferencesFileBytes = 64L * 1024;",
            "MaximumPreferencesFileBytes,");
        AssertDoesNotContainAny(stores, "File.", "JsonSerializerOptions");
        AssertContainsAll(codec, "JsonSerializerOptions", "JsonSerializer.DeserializeAsync");
        AssertContainsAll(mainWindow, "_reportHistoryPersistence.Queue", "_shellPreferencePersistence.Queue",
            "e.Cancel = true", "IsEnabled = false", "viewModel.RunSession.CancelActiveRun();",
            "finalViewModel.RunSession.CancelActiveRun();", "Task.WhenAll(",
            "completion.WaitAsync(LocalStateCloseFlushTimeout)", "_reportHistoryPersistence.CompleteAsync()",
            "_shellPreferencePersistence.CompleteAsync()");
        AssertDoesNotContainAny(mainWindow, "ShellPreferenceFileStore.LoadInto(viewModel)",
            "ReportHistoryFileStore.Save(viewModel)", "ShellPreferenceFileStore.Save(viewModel)");
        AssertContainsAll(startupFactory,
            "ShellTextResources.LanguageFromPreference(startupPreferences.Language)");
        AssertContainsAll(construction, "private readonly bool _isInitializing = true;",
            "_isInitializing = false;");
        AssertDoesNotContainAny(construction, "RefreshContextState();", "RefreshSettingsState();");
        AssertContainsAll(navigation, "_deferredState.EnsureSettings(RefreshSettingsState)");
        AssertContainsAll(context, "WorkflowSession.EnsureWorkflowLoaded()");
        AssertContainsAll(settings, "if (!_isInitializing)");
        AssertContainsAll(persistenceCoordinator, "Task.Run", "_latestCancellation?.Cancel()",
            "RecordFailure(exception)");
    }
}
