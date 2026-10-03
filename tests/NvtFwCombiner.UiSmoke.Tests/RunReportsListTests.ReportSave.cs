using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class RunReportsListTests
{
    /// <summary>A local picker result must use the existing atomic file adapter.</summary>
    [AvaloniaFact]
    public async Task LocalReportSaveUsesAtomicFileStoreWithoutOpeningProviderWriteStream()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-atomic-success");
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        string json = ReportJsonSamples.Succeeded(runId: "atomic-local");
        reports.LoadReportJson(json, "source.json");
        reports.LocalFiles = new LocalFileStore();
        string destination = workspace.Write("atomic-local-report.json", "older report"u8.ToArray());
        using IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
        ((ReportStorageProxy)file).Call = (method, _) => method switch
        {
            "get_Name" => "atomic-local-report.json",
            "get_Path" => new Uri(destination),
            "OpenWriteAsync" => throw new Xunit.Sdk.XunitException("Local reports must use the atomic file store."),
            "Dispose" => null,
            _ => throw new NotSupportedException(method),
        };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (_, _) => Task.FromResult<IStorageFile?>(file);

        await new ReportModal { DataContext = reports }.SaveReportAsync(picker);

        Assert.Equal(json, await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        Assert.Equal(reports.Text.FormatReportSavedToast("atomic-local-report.json"), reports.ReportToastText);
    }

    /// <summary>A failed local precommit keeps the original report file and never publishes success.</summary>
    [AvaloniaFact]
    public async Task LocalReportSaveFailurePreservesExistingDestination()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-report-atomic-failure");
        string destination = workspace.Write("existing.json", "original"u8.ToArray());
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { })
        {
            LocalFiles = new CancelledLocalFileStore(),
        };
        reports.LoadReportJson(ReportJsonSamples.Succeeded(), "source.json");
        using IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
        ((ReportStorageProxy)file).Call = (method, _) => method switch
        {
            "get_Name" => "existing.json",
            "get_Path" => new Uri(destination),
            "OpenWriteAsync" => throw new Xunit.Sdk.XunitException("Local reports must not open the provider stream."),
            "Dispose" => null,
            _ => throw new NotSupportedException(method),
        };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (_, _) => Task.FromResult<IStorageFile?>(file);

        await new ReportModal { DataContext = reports }.SaveReportAsync(picker);

        Assert.Equal("original", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        Assert.Contains("Report save failed", reports.ReportToastText, StringComparison.Ordinal);
        Assert.DoesNotContain("Report saved", reports.ReportToastText, StringComparison.Ordinal);
    }

    /// <summary>The actual save operation contains provider and stream failures without losing the report.</summary>
    [AvaloniaTheory]
    [InlineData("picker")]
    [InlineData("open")]
    [InlineData("write")]
    [InlineData("write-cancel")]
    [InlineData("flush")]
    [InlineData("stream-dispose")]
    [InlineData("file-dispose")]
    public async Task ReportSaveFailurePreservesReportAndNeverNotifiesSuccess(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        string json = ReportJsonSamples.Succeeded(runId: "save-failure");
        reports.LoadReportJson(json, "source.json");
        var modal = new ReportModal { DataContext = reports };
        IStorageProvider picker = CreateSaveProvider(failure, reports);

        await modal.SaveReportAsync(picker);

        Assert.Equal(json, reports.LoadedReportJson);
        Assert.Contains("Report save failed", reports.ReportToastText, StringComparison.Ordinal);
        Assert.DoesNotContain("Report saved", reports.ReportToastText, StringComparison.Ordinal);
        Assert.Contains(failure == "open" ? "denied" : failure.Replace('-', ' '), reports.ReportToastText, StringComparison.Ordinal);

        await modal.SaveReportAsync(CreateSaveProvider("none", reports));
        Assert.Equal(reports.Text.FormatReportSavedToast("saved.json", bestEffortProviderWrite: true), reports.ReportToastText);
        Assert.Contains("Atomic replacement is not guaranteed", reports.ReportToastText, StringComparison.Ordinal);
        Assert.Equal(json, reports.LoadedReportJson);
    }

    /// <summary>Pending picker preserves the original snapshot, suppresses duplicate save and completes disposal first.</summary>
    [AvaloniaFact]
    public async Task ReportSaveCapturesSnapshotAndDisposesBeforeSuccess()
    {
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.English), static () => { });
        string original = ReportJsonSamples.Succeeded(runId: "original");
        reports.LoadReportJson(original, "original.json");
        var modal = new ReportModal { DataContext = reports };
        using var stream = new MemoryStream();
        bool fileDisposed = false;
        using IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
        ((ReportStorageProxy)file).Call = (method, _) =>
        {
            if (method == "Dispose")
            {
                fileDisposed = true;
                return null;
            }
            return method switch
            {
                "get_Name" => "saved.json",
                "get_Path" => new Uri("https://storage.example/saved.json"),
                "OpenWriteAsync" => Task.FromResult<Stream>(stream),
                _ => throw new NotSupportedException(method),
            };
        };
        var selected = new TaskCompletionSource<IStorageFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int pickerCount = 0;
        var successObservations = new List<(bool FileDisposed, bool StreamClosed)>();
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (_, _) => { pickerCount++; return selected.Task; };
        reports.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(reports.ReportToastText) && reports.ReportToastText.StartsWith("Report saved", StringComparison.Ordinal))
            {
                successObservations.Add((fileDisposed, !stream.CanWrite));
            }
        };
        Task saving = modal.SaveReportAsync(picker);
        string newer = ReportJsonSamples.Succeeded(runId: "newer");
        reports.LoadReportJson(newer, "newer.json");
        await modal.SaveReportAsync(picker);
        Assert.Equal(1, pickerCount);
        selected.SetResult(file);
        await saving;
        Assert.Equal((true, true), Assert.Single(successObservations));
        Assert.Equal(original, Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(newer, reports.LoadedReportJson);
        Assert.Equal(reports.Text.FormatReportSavedToast("saved.json", bestEffortProviderWrite: true), reports.ReportToastText);
    }

    /// <summary>Both native cancellation forms leave the loaded report intact and permit another attempt.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportSaveCancellationDoesNotReportFailure(bool throwsCancellation)
    {
        var reports = new ReportPresentationViewModel(() => ShellTextResources.For(ShellLanguage.ChineseTraditional), static () => { });
        string json = ReportJsonSamples.Succeeded();
        reports.LoadReportJson(json, "source.json");
        string previousToast = reports.ReportToastText;
        var modal = new ReportModal { DataContext = reports };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (_, _) => throwsCancellation
            ? Task.FromException<IStorageFile?>(new OperationCanceledException())
            : Task.FromResult<IStorageFile?>(null);
        await modal.SaveReportAsync(picker);
        Assert.Equal(previousToast, reports.ReportToastText);
        Assert.Equal(json, reports.LoadedReportJson);
        await modal.SaveReportAsync(CreateSaveProvider("open", reports));
        Assert.Contains("Report 儲存失敗", reports.ReportToastText, StringComparison.Ordinal);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The simulated picker transfers file ownership to the actual save operation under test.")]
    private static IStorageProvider CreateSaveProvider(string failure, ReportPresentationViewModel reports)
    {
        IStorageFile file = DispatchProxy.Create<IStorageFile, ReportStorageProxy>();
        ((ReportStorageProxy)file).Call = (method, _) => method switch
        {
            "get_Name" => "saved.json",
            "get_Path" => new Uri("https://storage.example/saved.json"),
            "OpenWriteAsync" => failure == "open"
                ? Task.FromException<Stream>(new UnauthorizedAccessException("denied"))
                : Task.FromResult<Stream>(new SaveFaultStream(failure, reports)),
            "Dispose" => failure == "file-dispose" ? throw new IOException("file dispose") : null,
            _ => throw new NotSupportedException(method),
        };
        IStorageProvider picker = DispatchProxy.Create<IStorageProvider, ReportStorageProxy>();
        ((ReportStorageProxy)picker).Call = (method, _) =>
        {
            Assert.Equal(nameof(IStorageProvider.SaveFilePickerAsync), method);
            return failure == "picker" ? Task.FromException<IStorageFile?>(new IOException("picker"))
                : Task.FromResult<IStorageFile?>(file);
        };
        return picker;
    }

    private sealed class SaveFaultStream(string failure, ReportPresentationViewModel reports) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return failure switch
            {
                "write" => ValueTask.FromException(new IOException("write")),
                "write-cancel" => ValueTask.FromException(new OperationCanceledException("write cancel")),
                _ => base.WriteAsync(buffer, cancellationToken),
            };
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return failure == "flush" ? Task.FromException(new IOException("flush")) : base.FlushAsync(cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            Assert.DoesNotContain("Report saved", reports.ReportToastText, StringComparison.Ordinal);
            return failure == "stream-dispose" ? ValueTask.FromException(new IOException("stream dispose"))
                : base.DisposeAsync();
        }
    }

    private sealed class CancelledLocalFileStore : ILocalFileStore
    {
        public ValueTask<T> ReadAsync<T>(string path, long maximumBytes,
            Func<Stream, CancellationToken, ValueTask<T>> project, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string> ReadTextAsync(string path, long maximumBytes,
            CancellationToken cancellationToken, Action<LocalFileReadProgress>? progress = null)
        {
            throw new NotSupportedException();
        }

        public ValueTask<string> ReadTextAsync(Func<CancellationToken, ValueTask<Stream>> openReadAsync,
            long maximumBytes, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            return new LocalFileStore().WriteAsync(path, bytes, new CancellationToken(canceled: true));
        }

        public ValueTask<LocalFileDestinationInfo> InspectDestinationAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteOptions options, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> RefersToSameFileAsync(string first, string second, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes,
            LocalFileWriteMode mode, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
