using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using Nvt.Core.Avalonia.RuntimeQuery;
using Nvt.Core.RuntimeQuery;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>One window's read-only product commands. Owns no UI state or transport.</summary>
internal sealed class DesktopRuntimeQuery
{
    // Immutable references; all reads of their mutable presentation facts require the UI thread.
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _viewModel;
    private readonly ShellPreloadSession _preload;
    private readonly ICanonicalSupportMatrixQuery _supportMatrix;
    private readonly Func<RuntimeQueryRequest?, string, CancellationToken, Task<RuntimeQueryResponseEnvelope>> _execute;

    internal DesktopRuntimeQuery(MainWindow window, MainWindowViewModel viewModel,
        ShellPreloadSession preload, ICanonicalSupportMatrixQuery supportMatrix)
    {
        _window = window;
        _viewModel = viewModel;
        _preload = preload;
        _supportMatrix = supportMatrix;
        Commands = Array.AsReadOnly<RuntimeQueryCommand>(
        [
            new("state", RuntimeQueryCommandRisk.ReadOnly, ReadStateAsync),
            new("catalog.list", RuntimeQueryCommandRisk.ReadOnly, ReadCatalogAsync),
        ]);
        Router = new(Commands, requireConfirmation: true);
        _execute = RuntimeQueryUiThread.Wrap(
            (request, version, _) => Router.ExecuteAsync(request, version),
            static (_, _) => new("IPC_ERROR", "The UI dispatcher is unavailable."));
    }

    internal IReadOnlyList<RuntimeQueryCommand> Commands { get; }
    internal RuntimeQueryCommandRouter Router { get; }

    internal Task<RuntimeQueryResponseEnvelope> ExecuteAsync(RuntimeQueryRequest? request,
        string expectedVersion, CancellationToken cancellationToken)
    {
        return _execute(request, expectedVersion, cancellationToken);
    }

    private Task<RuntimeQueryResponseEnvelope> ReadStateAsync(IReadOnlyDictionary<string, string>? arguments)
    {
        Dispatcher.UIThread.VerifyAccess();
        string? workflowMode = _viewModel.SelectedPage switch
        {
            ShellPage.Merge => _viewModel.Merge.SelectedMergeMode,
            ShellPage.Replace => _viewModel.Replace.SelectedReplaceMode,
            ShellPage.Home or ShellPage.HexEditor => null,
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
        var state = new DesktopRuntimeQueryState(
            _viewModel.SelectedPage.ToString(), workflowMode,
            _preload.Stages.All(static stage => stage.State is ShellPreloadStageState.Succeeded or
                ShellPreloadStageState.Failed or ShellPreloadStageState.Skipped or ShellPreloadStageState.Cancelled),
            _viewModel.RunSession.IsRunInProgress, _window.ClosePhase.ToString());
        return Task.FromResult(RuntimeQueryResponseEnvelope.Success(JsonSerializer.SerializeToElement(
            state, DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryState)));
    }

    private Task<RuntimeQueryResponseEnvelope> ReadCatalogAsync(IReadOnlyDictionary<string, string>? arguments)
    {
        Dispatcher.UIThread.VerifyAccess();
        WorkflowContextSetupViewModel setup = _viewModel.WorkflowSession.WorkflowContextSetup;
        var catalog = new DesktopRuntimeQueryCatalog(
            [.. setup.IcChoices],
            [.. setup.NumberChoices.Select(static choice => new DesktopRuntimeQueryNumberChoice(
                choice.Token, choice.DisplayLabel))],
            [.. (_supportMatrix.Query().Matrix?.Rows ?? [])
                .Select(static row => row.Identity.WorkflowId).Distinct(StringComparer.Ordinal)]);
        return Task.FromResult(RuntimeQueryResponseEnvelope.Success(JsonSerializer.SerializeToElement(
            catalog, DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryCatalog)));
    }
}

internal sealed record DesktopRuntimeQueryState(string Page, string? WorkflowMode,
    bool StartupPreloadFinished, bool IsRunInProgress, string ClosePhase);

internal sealed record DesktopRuntimeQueryCatalog(string[] IcChoices,
    DesktopRuntimeQueryNumberChoice[] NumberChoices, string[] WorkflowModes);

internal sealed record DesktopRuntimeQueryNumberChoice(string Token, string DisplayLabel);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DesktopRuntimeQueryState))]
[JsonSerializable(typeof(DesktopRuntimeQueryCatalog))]
internal sealed partial class DesktopRuntimeQueryJsonContext : JsonSerializerContext;
