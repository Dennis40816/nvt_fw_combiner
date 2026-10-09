using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises picker, drop, and Build failures through their UI Task handlers.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class UiEventHandlerFailureTests
{
    /// <summary>A failed native picker is reported under the Hex Editor operation.</summary>
    [AvaloniaFact]
    public async Task OpenSourcePickerThrowsRecordsOperationOnce()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        var failure = new IOException("picker failed");
        var panel = new HexEditorPanel
        {
            DataContext = shell.HexEditorWorkspace,
            PickFirmwareFileAsync = (_, _) => Task.FromException<string?>(failure),
        };
        var window = new Window { Content = panel };
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var fallback = new List<Exception>();
        var adapter = new UiEventAdapter(diagnostics, () => { }, (_, error) => fallback.Add(error));

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            await adapter.RunAsync("HexEditorPanel.OpenSource",
                _ => panel.HandleOpenHexEditorSourceButton_OnClickAsync(
                    null, new RoutedEventArgs(Button.ClickEvent)));

            SystemActivityDraft activity = Assert.Single(((CaptureDiagnostics)diagnostics).Activities);
            Assert.Equal("HexEditorPanel.OpenSource", activity.Code);
            Assert.Equal(nameof(IOException), activity.SubjectId);
            Assert.Empty(fallback);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A failed drop transfer is reported under the Hex Editor drop operation.</summary>
    [AvaloniaFact]
    public async Task SourceDropTransferThrowsRecordsOperationOnce()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        var panel = new HexEditorPanel { DataContext = shell.HexEditorWorkspace };
        var failure = new IOException("drop transfer failed");
        using IDataTransfer transfer = DispatchProxy.Create<IDataTransfer, ThrowingTransfer>();
        ((ThrowingTransfer)transfer).Failure = failure;
        var drop = new DragEventArgs(DragDrop.DropEvent, transfer, panel, default, KeyModifiers.None);
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var fallback = new List<Exception>();
        var adapter = new UiEventAdapter(diagnostics, () => { }, (_, error) => fallback.Add(error));

        await adapter.RunAsync("HexEditorPanel.SourceDrop",
            _ => panel.HandleHexEditorSourceDrop_OnDropAsync(null, drop));

        SystemActivityDraft activity = Assert.Single(((CaptureDiagnostics)diagnostics).Activities);
        Assert.Equal("HexEditorPanel.SourceDrop", activity.Code);
        Assert.Equal(nameof(IOException), activity.SubjectId);
        Assert.Empty(fallback);
    }

    /// <summary>A failed Build settings callback is reported under the Build operation.</summary>
    [AvaloniaFact]
    public async Task BuildMergeSettingsCallbackThrowsRecordsOperationOnce()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-event-build-failure");
        PresentationHostServices services = await ReportControlTestHost.CreateServicesAsync(workspace);
        using var window = new MainWindow(
            UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var failure = new IOException("build settings failed");
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var fallback = new List<Exception>();
        var adapter = new UiEventAdapter(diagnostics, () => { }, (_, error) => fallback.Add(error));

        await adapter.RunAsync("MainWindow.BuildMerge",
            _ => window.OpenBuildSettingsAsync(null, _ => Task.FromException<bool>(failure)));

        SystemActivityDraft activity = Assert.Single(((CaptureDiagnostics)diagnostics).Activities);
        Assert.Equal("MainWindow.BuildMerge", activity.Code);
        Assert.Equal(nameof(IOException), activity.SubjectId);
        Assert.Empty(fallback);
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a subclass of this test port.")]
    private class CaptureDiagnostics : DispatchProxy
    {
        public List<SystemActivityDraft> Activities { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(ISystemInformationService.RecordActivity), targetMethod!.Name);
            Activities.Add(Assert.IsType<SystemActivityDraft>(Assert.Single(args!)));
            return null;
        }
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a subclass of this test transfer.")]
    private class ThrowingTransfer : DispatchProxy
    {
        public Exception Failure { get; set; } = new IOException("transfer failed");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name == nameof(IDisposable.Dispose) ? null : throw Failure;
        }
    }
}
