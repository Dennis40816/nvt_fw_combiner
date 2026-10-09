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
            ArgumentlessReadOnlyCommand("state", ReadState),
            ArgumentlessReadOnlyCommand("catalog.list", ReadCatalog),
        ]);
        // This slice has only read-only commands. Retain all arguments, including confirm, for validation.
        Router = new(Commands, requireConfirmation: false);
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

    private static RuntimeQueryCommand ArgumentlessReadOnlyCommand(string name,
        Func<RuntimeQueryResponseEnvelope> read)
    {
        return new(name, RuntimeQueryCommandRisk.ReadOnly, arguments =>
        {
            Dispatcher.UIThread.VerifyAccess();
            return Task.FromResult(arguments is { Count: > 0 }
                ? RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS", "This command accepts no arguments.")
                : read());
        });
    }

    private RuntimeQueryResponseEnvelope ReadState()
    {
        var state = new DesktopRuntimeQueryState(
            _viewModel.SelectedPage.ToString(), _viewModel.WorkflowSession.ActiveInspectionContext?.Mode,
            _preload.IsSettled,
            _viewModel.RunSession.IsRunInProgress, _window.ClosePhase.ToString());
        return RuntimeQueryResponseEnvelope.Success(JsonSerializer.SerializeToElement(
            state, DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryState));
    }

    private RuntimeQueryResponseEnvelope ReadCatalog()
    {
        WorkflowContextSetupViewModel? setup = _viewModel.WorkflowSession.IsWorkflowContextModalOpen
            ? _viewModel.WorkflowSession.WorkflowContextSetup
            : null;
        CanonicalSupportMatrixQueryResult result = _supportMatrix.Query();
        var catalog = new DesktopRuntimeQueryCatalog(
            result.State, result.IsStale, [.. result.ReloadIssues],
            [.. setup?.IcChoices ?? []],
            [.. (setup?.NumberChoices ?? []).Select(static choice => new DesktopRuntimeQueryNumberChoice(
                choice.Token, choice.DisplayLabel))],
            [.. (result.Matrix?.Rows ?? [])
                .Select(static row => row.Identity.WorkflowId).Distinct(StringComparer.Ordinal)]);
        return RuntimeQueryResponseEnvelope.Success(JsonSerializer.SerializeToElement(
            catalog, DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryCatalog));
    }
}

internal sealed record DesktopRuntimeQueryState(string Page, string? WorkflowMode,
    bool StartupPreloadFinished, bool IsRunInProgress, string ClosePhase);

internal sealed record DesktopRuntimeQueryCatalog(
    [property: JsonConverter(typeof(JsonStringEnumConverter<CanonicalSupportMatrixCatalogState>))]
    CanonicalSupportMatrixCatalogState State, bool IsStale, CapabilityCatalogIssue[] ReloadIssues, string[] IcChoices,
    DesktopRuntimeQueryNumberChoice[] NumberChoices, string[] WorkflowModes);

internal sealed record DesktopRuntimeQueryNumberChoice(string Token, string DisplayLabel);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DesktopRuntimeQueryState))]
[JsonSerializable(typeof(DesktopRuntimeQueryCatalog))]
internal sealed partial class DesktopRuntimeQueryJsonContext : JsonSerializerContext;
