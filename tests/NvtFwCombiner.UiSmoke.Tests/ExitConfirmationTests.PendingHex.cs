using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.HexEditor;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ExitConfirmationTests
{
    /// <summary>The first Hex load counts before I/O finishes; Cancel preserves Settings and the pending read.</summary>
    [AvaloniaFact]
    public async Task PendingHexLoadAndSettingsSurviveCancelledExit()
    {
        using var workspace = TempWorkspace.Create("v114-exit-pending-hex");
        PresentationHostServices original = await CreateServicesAsync(workspace);
        var files = new DelayedHexFiles(original.RawBinaryEditorFileSessions);
        var services = new PresentationHostServices(original.Composition, original.FileReveal, original.SupportMatrix,
            original.SystemInformation, original.SystemDiagnosticsExporter, files,
            original.CanonicalCatalogLoader, original.ExternalEnvironmentLoader, original.LocalFiles, original.LocalStateDirectory);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        Task load = shell.HexEditorWorkspace.LoadAsync(workspace.Write("pending.bin", [0x12, 0x34]), TestContext.Current.CancellationToken);
        try
        {
            Assert.False(load.IsCompleted);
            Assert.False(shell.HexEditorWorkspace.HasDocument);
            shell.OpenSettingsCommand.Execute(null);
            Assert.True(shell.IsSettingsModalOpen);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(shell.Navigation.IsExitConfirmationOpen);
            Assert.True(shell.IsSettingsModalOpen);
            NavigationClearConfirmationModal confirmation = Assert.Single(window.GetVisualDescendants()
                .OfType<NavigationClearConfirmationModal>());
            SettingsModal settings = Assert.Single(window.GetVisualDescendants().OfType<SettingsModal>());
            Button cancel = confirmation.FindControl<Button>("CancelButton")!;
            Button confirm = Assert.Single(confirmation.GetVisualDescendants().OfType<Button>(), button => button != cancel);
            Assert.Same(cancel, window.FocusManager!.GetFocusedElement());
            foreach (RawInputModifiers modifiers in new[] { RawInputModifiers.None, RawInputModifiers.Shift })
            {
                for (int step = 0; step < 6; step++)
                {
                    window.KeyPress(Key.Tab, modifiers, PhysicalKey.Tab, null);
                    window.KeyRelease(Key.Tab, modifiers, PhysicalKey.Tab, null);
                    Dispatcher.UIThread.RunJobs();
                    Control focused = Assert.IsType<Control>(window.FocusManager.GetFocusedElement(), exactMatch: false);
                    Assert.Same(step % 2 == 0 ? confirm : cancel, focused);
                    Assert.Contains(confirmation, focused.GetVisualAncestors());
                    Assert.DoesNotContain(settings, focused.GetVisualAncestors());
                }
            }
            shell.Navigation.CancelNavigationClearCommand.Execute(null);
            Assert.True(shell.IsSettingsModalOpen);
            Assert.True(window.IsEnabled);
            _ = files.Release.TrySetResult();
            await load;
            Assert.True(shell.HexEditorWorkspace.HasDocument);
            Assert.False(shell.HexEditorWorkspace.HasUnsavedChanges);
        }
        finally
        {
            _ = files.Release.TrySetResult();
            await load;
            await CloseAndFlushAsync(window);
        }
    }

    private sealed class DelayedHexFiles(IRawBinaryEditorFileSessionFactory inner) : IRawBinaryEditorFileSessionFactory
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IRawBinaryEditorFileSession Create(RawBinaryEditorSession editor)
        {
            return new DelayedSession(inner.Create(editor), Release.Task);
        }

        private sealed class DelayedSession(IRawBinaryEditorFileSession inner, Task release) : IRawBinaryEditorFileSession
        {
            public string? SourcePath => inner.SourcePath;
            public RawBinaryEditorFileResult? AcceptedLoad => inner.AcceptedLoad;
            public string SuggestedOutputFileName => inner.SuggestedOutputFileName;

            public async Task<RawBinaryEditorFileResult> LoadAsync(string sourcePath, CancellationToken cancellationToken = default)
            {
                await release.WaitAsync(cancellationToken);
                return await inner.LoadAsync(sourcePath, cancellationToken);
            }

            public Task<RawBinaryEditorSearchResult> FindAsciiAsync(string text, long startOffset, CancellationToken cancellationToken = default)
            {
                return inner.FindAsciiAsync(text, startOffset, cancellationToken);
            }

            public Task<RawBinaryEditorFileResult> SaveAsAsync(string outputPath, CancellationToken cancellationToken = default)
            {
                return inner.SaveAsAsync(outputPath, cancellationToken);
            }
        }
    }
}
