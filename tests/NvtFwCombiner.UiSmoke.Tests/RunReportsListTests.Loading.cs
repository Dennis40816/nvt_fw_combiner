using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class RunReportsListTests
{
    /// <summary>The actual window loader preserves list/current state on cancel, failure and supersession.</summary>
    [AvaloniaTheory]
    [InlineData("cancel")]
    [InlineData("invalid")]
    [InlineData("oversized")]
    [InlineData("superseded")]
    [InlineData("success")]
    public async Task RunReportsHostLoaderKeepsOutcomeAndFocus(string scenario)
    {
        using var workspace = TempWorkspace.Create("run-report-import");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            shell.MessageCenter.OpenRunReportsCommand.Execute(null);
            Render();
            Button load = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "LoadRunReportButton");
            if (scenario == "success")
            {
                shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "previous-report"), "previous.json");
                Render();
                _ = window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("reportListRow")).Focus();
            }
            _ = load.Focus();
            string before = shell.Reports.LoadedReportJson;
            string json = scenario switch
            {
                "invalid" => "{",
                "oversized" => new string(' ', 10_485_761),
                _ => ReportJsonSamples.Succeeded(runId: "picked-report"),
            };
            var pending = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
            using IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
            ((ReportStorageProxy)file).Call = (method, _) => method switch
            {
                "get_Name" => "picked.json",
                "Dispose" => null,
                "OpenReadAsync" => scenario == "superseded" ? pending.Task :
                    Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(json))),
                _ => throw new NotSupportedException(method),
            };
            IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
            ((ReportStorageProxy)picker).Call = (method, args) =>
            {
                Assert.Equal(nameof(IStorageProvider.OpenFilePickerAsync), method);
                FilePickerOpenOptions options = Assert.IsType<FilePickerOpenOptions>(Assert.Single(args!));
                Assert.False(options.AllowMultiple);
                Assert.Contains("*.json", options.FileTypeFilter!.Single().Patterns!);
                IReadOnlyList<IStorageFile> selected = scenario == "cancel" ? [] : [file];
                return Task.FromResult(selected);
            };
            Task loading = window.LoadReportJsonAsync(load, picker);
            if (scenario == "superseded")
            {
                before = ReportJsonSamples.Succeeded(runId: "newer-report");
                shell.Reports.LoadReportJson(before, "newer.json");
                pending.SetResult(new MemoryStream(Encoding.UTF8.GetBytes(json)));
            }
            await loading;
            Render();
            if (scenario == "success")
            {
                Assert.Equal(json, shell.Reports.LoadedReportJson);
                Assert.True(shell.Reports.IsReportModalOpen);
                Button close = Assert.IsType<Button>(window.FocusManager!.GetFocusedElement());
                Assert.Same(shell.Reports.CloseReportCommand, close.Command);
                shell.MessageCenter.OpenRunReportsCommand.Execute(null);
                Render();
                Assert.True(load.IsFocused);
            }
            else
            {
                Assert.Equal(before, shell.Reports.LoadedReportJson);
                Assert.False(shell.Reports.IsReportModalOpen);
                Assert.True(load.IsFocused);
                Assert.Equal(scenario == "superseded" ? 1 : 0, shell.Reports.ReportHistoryCount);
            }
            Assert.True(shell.MessageCenter.IsOpen);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>A pending picker cannot import into a replaced or dismissed report context.</summary>
    [AvaloniaTheory]
    [InlineData("context")]
    [InlineData("reopen")]
    [InlineData("newer-report")]
    [InlineData("close")]
    public async Task ReportPickerDropsStaleSelection(string change)
    {
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            PresentationTestHost.CreateServices("0.10.5"), ShellPreferenceSnapshot.Default);
        window.Show();
        await AwaitHistoryReadyAsync(window);
        var shell = (MainWindowViewModel)window.DataContext!;
        shell.MessageCenter.OpenRunReportsCommand.Execute(null);
        var selected = new TaskCompletionSource<IReadOnlyList<IStorageFile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        using IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
        ((ReportStorageProxy)file).Call = (method, _) => method switch
        {
            "get_Name" => "stale.json",
            "Dispose" => null,
            "OpenReadAsync" => Read(),
            _ => throw new NotSupportedException(method),
        };
        Task<Stream> Read()
        {
            reads++;
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(
                ReportJsonSamples.Succeeded(runId: "stale"))));
        }
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (_, _) => selected.Task;
        Task loading = window.LoadReportJsonAsync(null, picker);
        switch (change)
        {
            case "context": window.DataContext = PresentationTestHost.CreateViewModel(); break;
            case "reopen":
                shell.MessageCenter.CloseCommand.Execute(null);
                shell.MessageCenter.OpenRunReportsCommand.Execute(null);
                break;
            case "newer-report":
                shell.Reports.LoadReportJson(ReportJsonSamples.Succeeded(runId: "newer"), "newer.json");
                break;
            case "close": window.Close(); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        string before = shell.Reports.LoadedReportJson;
        selected.SetResult([file]);
        await loading;
        Assert.Equal(0, reads);
        Assert.Equal(before, shell.Reports.LoadedReportJson);
        Assert.False(shell.Reports.IsReportModalOpen);
        await CloseAndFlushAsync(window);
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a runtime subclass.")]
    private class ReportStorageProxy : DispatchProxy
    {
        internal Func<string, object?[]?, object?> Call { get; set; } = (_, _) => throw new NotSupportedException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Call(targetMethod!.Name, args);
        }
    }
}
