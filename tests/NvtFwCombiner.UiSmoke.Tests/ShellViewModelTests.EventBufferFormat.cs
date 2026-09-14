using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Missing configuration opens independent defaults, then saves only through the typed session.</summary>
    [Fact]
    public async Task EventBufferFormatUsesDraftDefaultsAndPreservesUnsavedCloseConfirmation()
    {
        var storage = new EventBufferFormatStorage();
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay"), new("other", "Other")], [new("desay", null, [0x97, 0xA6])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;

        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        Assert.Equal("desay", row.UniqueId);
        Assert.Equal([0x97, 0xA6], row.RecognitionValues);
        Assert.True(viewModel.Settings.IsEventBufferFormatMissingOrInvalid);
        Assert.True(viewModel.Settings.CanSaveEventBufferFormat);

        row.AliasName = "Desk display";
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal("Desk display", Assert.Single(session.Current.Configuration!.Entries).AliasName);

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.SelectedIdentity = row.IdentityChoices.Single(identity => identity.UniqueId == "other");
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        Assert.Equal("other", Assert.Single(session.Current.Configuration!.Entries).UniqueId);

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.AliasName = "Pending alias";
        viewModel.CloseSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsModalOpen);
        Assert.True(viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen);
        viewModel.Settings.ConfirmEventBufferFormatCloseCommand.Execute(null);
        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.Equal("Desk display", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
    }

    /// <summary>Does not turn a valid empty saved configuration into unrequested catalog entries.</summary>
    [Fact]
    public async Task EventBufferFormatRetainsAnAdmittedEmptySavedDraftWithoutExpandingTheCatalog()
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new EventBufferFormatStoredConfiguration(
                new EventBufferFormatConfigurationDocument(1, "event-buffer-format", []), "empty-draft"),
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;

        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
        Assert.Empty(viewModel.Settings.EventBufferFormatRows);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
    }

    /// <summary>A rejected write leaves both the editor draft and its prior admitted publication intact.</summary>
    [Fact]
    public async Task EventBufferFormatSaveFailureRetainsDraftAndReportsTheTypedPersistenceFailure()
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new EventBufferFormatStoredConfiguration(
                new EventBufferFormatConfigurationDocument(1, "event-buffer-format",
                    [new("desay", "Saved", ["0x97"])]), "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            WriteError = new IOException("disk unavailable"),
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.AliasName = "Unsaved";

        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Equal("Saved", Assert.Single(session.Current.Configuration!.Entries).AliasName);
        Assert.Equal("Unsaved", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
        Assert.Equal(viewModel.Text.EventBufferFormatSaveFailedLabel, viewModel.Settings.EventBufferFormatStatus);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal(viewModel.Text.EventBufferFormatSaveFailedLabel, viewModel.Settings.EventBufferFormatStatus);
        Assert.NotEqual(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
    }

    /// <summary>Save serialisation blocks destructive commands and leaves lexical versus semantic validation distinct.</summary>
    [Fact]
    public async Task EventBufferFormatSaveBusyAndValueFeedbackAreGuarded()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new EventBufferFormatStorage { WriteHold = hold };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.RecognitionValueDraft = "wrong";
        row.AddRecognitionValueCommand.Execute(null);
        Assert.Equal(viewModel.Text.EventBufferFormatInvalidHexLabel, row.RecognitionValueValidationMessage);

        row.AliasName = "pending";
        Task save = viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        await Task.Yield();

        try
        {
            Assert.True(viewModel.Settings.IsEventBufferFormatBusy);
            Assert.False(viewModel.Settings.CanRestoreEventBufferFormat);
            Assert.False(viewModel.Settings.CanDiscardEventBufferFormat);
            Assert.False(viewModel.Settings.RequestSettingsClose());
            viewModel.Settings.RestoreEventBufferFormatDefaultsCommand.Execute(null);
            Assert.Equal([0x97], row.RecognitionValues);
            Assert.Equal("pending", row.AliasName);
        }
        finally
        {
            hold.SetResult();
        }
        await save;

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.BeginAddRecognitionValueCommand.Execute(null);
        row.RecognitionValueDraft = "0x97";
        row.AddRecognitionValueCommand.Execute(null);
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Contains("0x97", viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
    }

    /// <summary>Reload failures remain actionable and localized without activating recovery defaults.</summary>
    [Theory]
    [InlineData(EventBufferFormatConfigurationFailure.InvalidValues, "already assigned", "其他識別碼")]
    [InlineData(EventBufferFormatConfigurationFailure.ScopeMismatch, "different configuration group", "不同設定群組")]
    [InlineData(EventBufferFormatConfigurationFailure.ReadFailed, "Cannot read", "無法讀取")]
    public async Task EventBufferFormatReloadPreservesSpecificFailureAcrossLanguageChange(
        EventBufferFormatConfigurationFailure failure, string englishDetail, string chineseDetail)
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new(new(1, failure == EventBufferFormatConfigurationFailure.ScopeMismatch ? "foreign-scope" : "event-buffer-format",
                [new("desay", null, ["0x97"]), new("other", null, ["0x97"])]), new string('b', 64)),
            ReadError = failure == EventBufferFormatConfigurationFailure.ReadFailed ? new IOException("unreadable") : null,
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay"), new("other", "Other")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal(EventBufferFormatConfigurationStatus.Invalid, session.Current.Status);
        Assert.Null(session.Current.Configuration);
        Assert.Contains(englishDetail, viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Contains(chineseDetail, viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
        Assert.Equal([0x97], Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues);
    }

    private static MainWindowViewModel CreateEventBufferFormatViewModel(
        IEventBufferFormatConfigurationSession session)
    {
        PresentationHostServices original = PresentationTestHost.CreateServices(
            ApplicationVersionProvider.InformationalVersion);
        var services = new PresentationHostServices(
            original.Composition,
            original.FileReveal,
            original.SupportMatrix,
            original.SystemInformation,
            original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader,
            original.LocalFiles,
            versionManagement: null,
            managedApplicationStartup: null,
            stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => Task.FromResult(session));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, ShellLanguage.English);
        return PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
    }

    private sealed class EventBufferFormatStorage : IEventBufferFormatConfigurationStorage
    {
        internal EventBufferFormatStoredConfiguration? Stored { get; set; }
        internal Exception? WriteError { get; set; }
        internal Exception? ReadError { get; set; }
        internal TaskCompletionSource? WriteHold { get; set; }

        public ValueTask<EventBufferFormatStoredConfiguration?> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadError is { } error ? throw error : ValueTask.FromResult(Stored);
        }

        public async ValueTask<string> WriteAsync(
            EventBufferFormatConfigurationDocument document,
            CancellationToken cancellationToken)
        {
            if (WriteHold is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }

            if (WriteError is { } error)
            {
                throw error;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Stored = new EventBufferFormatStoredConfiguration(document, new string('a', 64));
            return Stored.SourceSha256;
        }
    }
}
