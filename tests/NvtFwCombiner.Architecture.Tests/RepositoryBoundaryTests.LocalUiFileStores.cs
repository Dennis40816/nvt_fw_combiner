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

        Assert.Contains("LocalFiles = new LocalFileStore();", bootstrap, StringComparison.Ordinal);
        Assert.Contains("public static ILocalFileStore CreateLocalFileStore()", bootstrap, StringComparison.Ordinal);
        Assert.Contains("FileShare.Read | FileShare.Delete", adapter, StringComparison.Ordinal);
        Assert.Contains("AtomicFileWriteScope.Open(fullPath)", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllText", reportInput, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadToEndAsync", reportInput, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", reportInput, StringComparison.Ordinal);
        Assert.Contains("MaximumStandaloneReportBytes = 10L * 1024 * 1024", reportInput, StringComparison.Ordinal);
        Assert.Contains("MaximumHistoryFileBytes = 64L * 1024 * 1024", history, StringComparison.Ordinal);
        Assert.Contains("ReportPresentationViewModel.MaximumReportHistoryStorageBytes", history, StringComparison.Ordinal);
        Assert.Contains("EntryTooLargeToPersist", history, StringComparison.Ordinal);
        Assert.Contains("OmitDerivableReportHistoryMetadata", history, StringComparison.Ordinal);
        Assert.Contains("normalized!.Metadata == snapshot.Metadata", historyProjection, StringComparison.Ordinal);
        Assert.Contains("RemoveAt(retained.Count - 1)", history, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", stores, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializerOptions", stores, StringComparison.Ordinal);
        Assert.Contains("JsonSerializerOptions", codec, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.DeserializeAsync", codec, StringComparison.Ordinal);
        Assert.Contains("internal const long MaximumPreferencesFileBytes = 64L * 1024;", stores, StringComparison.Ordinal);
        Assert.Contains("MaximumPreferencesFileBytes,", stores, StringComparison.Ordinal);
        Assert.Contains("_reportHistoryPersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_shellPreferencePersistence.Queue", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.LoadInto(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains(
            "ShellTextResources.LanguageFromPreference(startupPreferences.Language)",
            startupFactory,
            StringComparison.Ordinal);
        Assert.Contains("private readonly bool _isInitializing = true;", construction, StringComparison.Ordinal);
        Assert.Contains("_isInitializing = false;", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshContextState();", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSettingsState();", construction, StringComparison.Ordinal);
        Assert.Contains("_deferredState.EnsureSettings(RefreshSettingsState)", navigation, StringComparison.Ordinal);
        Assert.Contains("WorkflowSession.EnsureWorkflowLoaded()", context, StringComparison.Ordinal);
        Assert.Contains("if (!_isInitializing)", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportHistoryFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferenceFileStore.Save(viewModel)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", mainWindow, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = false", mainWindow, StringComparison.Ordinal);
        Assert.Contains("viewModel.RunSession.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("finalViewModel.RunSession.CancelActiveRun();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("completion.WaitAsync(LocalStateCloseFlushTimeout)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_reportHistoryPersistence.CompleteAsync()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_shellPreferencePersistence.CompleteAsync()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Task.Run", persistenceCoordinator, StringComparison.Ordinal);
        Assert.Contains("_latestCancellation?.Cancel()", persistenceCoordinator, StringComparison.Ordinal);
        Assert.Contains("RecordFailure(exception)", persistenceCoordinator, StringComparison.Ordinal);
    }
}
