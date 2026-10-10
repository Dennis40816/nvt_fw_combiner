using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Verifies that UI event failures reach the owning diagnostic route.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class UiEventAdapterTests
{
    /// <summary>A child control inherits its window's adapter for the production static event route.</summary>
    [AvaloniaFact]
    public async Task RunChildControlInheritsWindowDiagnosticRoute()
    {
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fallbacks = new List<Exception>();
        var adapter = new UiEventAdapter(
            diagnostics,
            () => notified.TrySetResult(),
            (_, error) => fallbacks.Add(error));
        var child = new Button();
        var window = new Window { Content = child };
        UiEventAdapter.Attach(window, adapter);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiEventAdapter.Run(child, "Child action", _ => Task.FromException(new IOException("child failed")));
            Assert.True(notified.Task.IsCompletedSuccessfully);
            await notified.Task;

            Assert.Equal("Child action", Assert.Single(((CaptureDiagnostics)diagnostics).Activities).Code);
            Assert.Empty(fallbacks);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An action failure is recorded once with its operation and severity.</summary>
    [AvaloniaFact]
    public async Task RunAsyncActionThrowsRecordsOperationAndNotifiesOnce()
    {
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var notifications = 0;
        var fallbacks = new List<(string Operation, Exception Error)>();
        var adapter = new UiEventAdapter(
            diagnostics,
            () => notifications++,
            (operation, error) => fallbacks.Add((operation, error)));
        var failure = new IOException("picker failed");

        await adapter.RunAsync("Open firmware", _ => Task.FromException(failure));

        SystemActivityDraft activity = Assert.Single(((CaptureDiagnostics)diagnostics).Activities);
        Assert.Equal("Open firmware", activity.Code);
        Assert.Equal(SystemActivityImportance.Important, activity.Importance);
        Assert.Equal(SystemActivityCategory.Diagnostics, activity.Category);
        Assert.Equal(SystemActivitySeverity.Error, activity.Severity);
        Assert.Equal(nameof(IOException), activity.SubjectId);
        Assert.Equal(1, notifications);
        Assert.Empty(fallbacks);
    }

    /// <summary>A failed diagnostic sink invokes the fallback with the original error.</summary>
    [AvaloniaFact]
    public async Task RunAsyncDiagnosticsThrowsReportsOriginalFailureOnce()
    {
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        ((CaptureDiagnostics)diagnostics).ThrowOnRecord = true;
        var notifications = 0;
        var fallbacks = new List<(string Operation, Exception Error)>();
        var adapter = new UiEventAdapter(
            diagnostics,
            () => notifications++,
            (operation, error) => fallbacks.Add((operation, error)));
        var failure = new IOException("drop failed");

        await adapter.RunAsync("Load dropped firmware", _ => Task.FromException(failure));

        (string Operation, Exception Error) = Assert.Single(fallbacks);
        Assert.Equal("Load dropped firmware", Operation);
        Assert.Same(failure, Error);
        Assert.Equal(0, notifications);
        Assert.Empty(((CaptureDiagnostics)diagnostics).Activities);
    }

    /// <summary>A failed notification invokes the fallback with the original error.</summary>
    [AvaloniaFact]
    public async Task RunAsyncNotificationThrowsReportsOriginalFailureOnce()
    {
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var fallbacks = new List<(string Operation, Exception Error)>();
        var adapter = new UiEventAdapter(
            diagnostics,
            () => throw new IOException("notification failed"),
            (operation, error) => fallbacks.Add((operation, error)));
        var failure = new IOException("build failed");

        await adapter.RunAsync("Build merge", _ => Task.FromException(failure));

        _ = Assert.Single(((CaptureDiagnostics)diagnostics).Activities);
        (string Operation, Exception Error) = Assert.Single(fallbacks);
        Assert.Equal("Build merge", Operation);
        Assert.Same(failure, Error);
    }

    /// <summary>Cancellation from the supplied token produces no failure report.</summary>
    [AvaloniaFact]
    public async Task RunAsyncSuppliedTokenCancelsReportsNothing()
    {
        ISystemInformationService diagnostics = DispatchProxy.Create<ISystemInformationService, CaptureDiagnostics>();
        var notifications = 0;
        var fallbacks = new List<(string Operation, Exception Error)>();
        var adapter = new UiEventAdapter(
            diagnostics,
            () => notifications++,
            (operation, error) => fallbacks.Add((operation, error)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await adapter.RunAsync("Open firmware", Task.FromCanceled, cancellation.Token);

        Assert.Empty(((CaptureDiagnostics)diagnostics).Activities);
        Assert.Equal(0, notifications);
        Assert.Empty(fallbacks);
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy generates a subclass of this test port.")]
    private class CaptureDiagnostics : DispatchProxy
    {
        public List<SystemActivityDraft> Activities { get; } = [];

        public bool ThrowOnRecord { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(ISystemInformationService.RecordActivity), targetMethod!.Name);
            SystemActivityDraft activity = Assert.IsType<SystemActivityDraft>(Assert.Single(args!));
            if (ThrowOnRecord)
            {
                throw new IOException("diagnostics unavailable");
            }

            Activities.Add(activity);
            return null;
        }
    }
}
