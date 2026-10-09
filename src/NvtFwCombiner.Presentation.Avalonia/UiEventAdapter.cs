using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Nvt.Core.Threading;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Connects observed UI work to the owning host's existing diagnostic routes.</summary>
internal sealed class UiEventAdapter : AvaloniaObject
{
    private static readonly AttachedProperty<UiEventAdapter?> AdapterProperty =
        AvaloniaProperty.RegisterAttached<UiEventAdapter, Control, UiEventAdapter?>(
            "Adapter", inherits: true);

    private readonly UiEventRunner _runner;

    internal UiEventAdapter(
        ISystemInformationService diagnostics,
        Action diagnosticsChanged,
        Action<string, Exception> fallbackReport)
        : this((operation, exception) =>
        {
            diagnostics.RecordActivity(new SystemActivityDraft(
                operation,
                SystemActivityImportance.Important,
                SystemActivityCategory.Diagnostics,
                SystemActivitySeverity.Error,
                SubjectId: exception.GetType().Name));
            diagnosticsChanged();
        }, fallbackReport)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(diagnosticsChanged);
    }

    private UiEventAdapter(Action<string, Exception> report, Action<string, Exception> fallbackReport)
    {
        _runner = new UiEventRunner(report, fallbackReport);
    }

    internal void Run(
        string operation, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        _runner.Run(operation, action, cancellationToken);
    }

    internal Task RunAsync(
        string operation, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return _runner.RunAsync(operation, action, cancellationToken);
    }

    internal static void Attach(Control owner, UiEventAdapter adapter)
    {
        _ = owner.SetValue(AdapterProperty, adapter);
    }

    internal static void Run(
        Control owner, string operation, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        // Detached controls have no application diagnostics owner; retain the host emergency route.
        UiEventAdapter adapter = owner.GetValue(AdapterProperty) ??
            new UiEventAdapter(ReportEmergency, ReportEmergency);
        adapter.Run(operation, action, cancellationToken);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "The host emergency sink must not allow a logging failure to escape the UI boundary.")]
    internal static void ReportEmergency(string operation, Exception exception)
    {
        try
        {
            // Desktop startup failures use this same host-owned stderr route.
            Console.Error.WriteLine($"UI event '{operation}': {exception}");
        }
        catch (Exception)
        {
            // Emergency reporting is best effort and must remain nonthrowing.
        }
    }
}
