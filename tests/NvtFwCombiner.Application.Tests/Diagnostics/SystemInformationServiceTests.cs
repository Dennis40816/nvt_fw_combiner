using System.Runtime.CompilerServices;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Application.Tests.Diagnostics;

/// <summary>Protects the current-session diagnostic lifecycle from report/history coupling.</summary>
public sealed class SystemInformationServiceTests
{
    /// <summary>A cold catalog owns one global blocker that disappears after a successful refresh.</summary>
    [Fact]
    public void ColdCatalogBlocksBuildAndResolutionClearsTheActiveBadge()
    {
        StubCatalog catalog = new(
            Result(CanonicalSupportMatrixCatalogState.ColdStartBlocked),
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix()));
        SystemInformationService service = CreateService(catalog, transitionLimit: 4);

        Assert.True(service.Current.IsBuildBlocked);
        Assert.Equal(SystemDiagnosticCodes.CapabilityCatalogUnavailable,
            Assert.Single(service.Current.ActiveDiagnostics).Code);

        SystemInformationSnapshot refreshed = service.Refresh(
            reloadCatalog: true,
            TestContext.Current.CancellationToken);

        Assert.False(refreshed.IsBuildBlocked);
        Assert.Empty(refreshed.ActiveDiagnostics);
        Assert.Equal(1, catalog.ReloadCount);
        Assert.Contains(service.CreateBundle().Transitions, transition =>
            transition.ResolvedCodes.Contains(
                SystemDiagnosticCodes.CapabilityCatalogUnavailable,
                StringComparer.Ordinal));
    }

    /// <summary>A failed reload identifies LKG without disabling execution through the retained publication.</summary>
    [Fact]
    public void LastKnownGoodIsWarningAndRetainsSafeCatalogIdentity()
    {
        SystemInformationService service = CreateService(new StubCatalog(Result(
            CanonicalSupportMatrixCatalogState.LastKnownGood,
            Matrix(),
            new CapabilityCatalogIssue("catalog.invalid", "private/path/catalog.json", null))));

        ActionableSystemDiagnostic warning = Assert.Single(service.Current.ActiveDiagnostics);

        Assert.Equal(SystemDiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(SystemDiagnosticCategory.CapabilityCatalog, warning.Category);
        Assert.False(service.Current.IsBuildBlocked);
        Assert.Equal("1.5.0", service.Current.CatalogVersion);
        Assert.Equal(["catalog.invalid"], service.Current.CatalogIssueCodes);
        Assert.DoesNotContain("private/path", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>An explicit failed reload keeps and clearly identifies the prior coherent publication.</summary>
    [Fact]
    public void FailedExplicitReloadPublishesLastKnownGoodWarning()
    {
        CanonicalSupportMatrixSnapshot matrix = Matrix();
        StubCatalog catalog = new(
            Result(CanonicalSupportMatrixCatalogState.Current, matrix),
            Result(
                CanonicalSupportMatrixCatalogState.LastKnownGood,
                matrix,
                new CapabilityCatalogIssue("catalog.reload.failed", "private detail", null)));
        SystemInformationService service = CreateService(catalog);

        SystemInformationSnapshot result = service.Refresh(
            reloadCatalog: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(CanonicalSupportMatrixCatalogState.LastKnownGood, result.CatalogState);
        Assert.Equal("1.5.0", result.CatalogVersion);
        Assert.Equal(
            SystemDiagnosticCodes.CapabilityCatalogLastKnownGood,
            Assert.Single(result.ActiveDiagnostics).Code);
        Assert.False(result.IsBuildBlocked);
    }

    /// <summary>External unavailable/LKG state is actionable but only route-specific readiness blocks execution.</summary>
    [Fact]
    public void ExternalEnvironmentFailuresRemainNonGlobalWarnings()
    {
        foreach ((ExternalProcessorEnvironmentState state, string code) in new[]
                 {
                     (ExternalProcessorEnvironmentState.Unavailable,
                         SystemDiagnosticCodes.ExternalProcessorEnvironmentUnavailable),
                     (ExternalProcessorEnvironmentState.LastKnownGood,
                         SystemDiagnosticCodes.ExternalProcessorEnvironmentLastKnownGood),
                 })
        {
            var catalog = new StubCatalog(Result(
                CanonicalSupportMatrixCatalogState.Current,
                Matrix()));
            SystemInformationService service = CreateService(catalog, externalState: state);

            ActionableSystemDiagnostic warning = Assert.Single(service.Current.ActiveDiagnostics);
            Assert.Equal(code, warning.Code);
            Assert.Equal(SystemDiagnosticCategory.ExternalProcessorEnvironment, warning.Category);
            Assert.Equal(SystemDiagnosticSeverity.Warning, warning.Severity);
            Assert.False(service.Current.IsBuildBlocked);
        }
    }

    /// <summary>A slow source reload never holds the current-snapshot reader lock.</summary>
    [Fact]
    public async Task CurrentRemainsReadableWhileCatalogReloadIsBlocked()
    {
        var catalog = new BlockingReloadCatalog();
        SystemInformationService service = new(
            "0.10.3-test",
            catalog,
            catalog,
            new StubExternalEnvironmentLoader(),
            new StubRuntimeProbe(),
            new StubClock());
        Task<SystemInformationSnapshot> refresh = Task.Run(() => service.Refresh(
            reloadCatalog: true,
            TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        try
        {
            await catalog.ReloadEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            Task<SystemInformationSnapshot> read = Task.Run(
                () => service.Current,
                TestContext.Current.CancellationToken);
            Task completed = await Task.WhenAny(
                read,
                Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            Assert.Same(read, completed);
            Assert.Equal(1, (await read).Generation);
        }
        finally
        {
            catalog.ReleaseReload.SetResult();
        }

        Assert.Equal(2, (await refresh).Generation);
    }

    /// <summary>Transition history is bounded in memory and diagnostic bundles expose no report model.</summary>
    [Fact]
    public void CurrentSessionTransitionsAreBoundedAndBundleIsReportFree()
    {
        StubCatalog catalog = new(
            Result(CanonicalSupportMatrixCatalogState.ColdStartBlocked),
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix()),
            Result(CanonicalSupportMatrixCatalogState.ColdStartBlocked),
            Result(CanonicalSupportMatrixCatalogState.Current, Matrix()));
        SystemInformationService service = CreateService(catalog, transitionLimit: 2);

        _ = service.Refresh(true, TestContext.Current.CancellationToken);
        _ = service.Refresh(true, TestContext.Current.CancellationToken);
        _ = service.Refresh(true, TestContext.Current.CancellationToken);
        SystemDiagnosticsBundle bundle = service.CreateBundle();

        Assert.Equal(2, bundle.Transitions.Count);
        Assert.Equal(SystemDiagnosticsBundle.CurrentSchemaVersion, bundle.SchemaVersion);
        Assert.DoesNotContain(
            typeof(SystemDiagnosticsBundle).GetProperties(),
            property => property.Name.Contains("Report", StringComparison.OrdinalIgnoreCase));
    }

    private static SystemInformationService CreateService(
        StubCatalog catalog,
        int transitionLimit = 16,
        ExternalProcessorEnvironmentState externalState = ExternalProcessorEnvironmentState.Current)
    {
        return new SystemInformationService(
            "0.10.3-test",
            catalog,
            catalog,
            new StubExternalEnvironmentLoader(externalState),
            new StubRuntimeProbe(),
            new StubClock(),
            transitionLimit);
    }

    private static CanonicalSupportMatrixQueryResult Result(
        CanonicalSupportMatrixCatalogState state,
        CanonicalSupportMatrixSnapshot? matrix = null,
        params CapabilityCatalogIssue[] issues)
    {
        return new CanonicalSupportMatrixQueryResult(state, matrix, issues);
    }

    private static CanonicalSupportMatrixSnapshot Matrix()
    {
        return new CanonicalSupportMatrixSnapshot(
            "canonical-capability-policy",
            "1.5.0",
            new string('a', 64),
            new ResolutionToken("catalog:test"),
            []);
    }

    private sealed class StubCatalog(params CanonicalSupportMatrixQueryResult[] results) :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        private int _index;

        internal int ReloadCount { get; private set; }

        public CanonicalSupportMatrixQueryResult Query()
        {
            return results[Math.Min(_index, results.Length - 1)];
        }

        public void Reload(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReloadCount++;
            _index = Math.Min(_index + 1, results.Length - 1);
        }
    }

    private sealed class StubRuntimeProbe : ISystemRuntimeProbe
    {
        public SystemRuntimeFacts Probe()
        {
            return new SystemRuntimeFacts(".NET test", "Windows test", "x64");
        }
    }

    private sealed class StubExternalEnvironmentLoader(
        ExternalProcessorEnvironmentState state = ExternalProcessorEnvironmentState.Current) :
        IExternalProcessorEnvironmentLoader
    {
        public ExternalProcessorEnvironmentStatus Current { get; } = new(
            state,
            RequestGeneration: 1,
            PublicationGeneration: state == ExternalProcessorEnvironmentState.Unavailable ? 0 : 1,
            ManifestCount: 0,
            Issues: []);

        public async IAsyncEnumerable<ExternalProcessorEnvironmentLoadUpdate> LoadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class BlockingReloadCatalog :
        ICanonicalSupportMatrixQuery,
        ICanonicalCapabilityCatalogReloader
    {
        internal TaskCompletionSource ReloadEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseReload { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public CanonicalSupportMatrixQueryResult Query()
        {
            return Result(CanonicalSupportMatrixCatalogState.Current, Matrix());
        }

        public void Reload(CancellationToken cancellationToken)
        {
            ReloadEntered.SetResult();
            ReleaseReload.Task.Wait(cancellationToken);
        }
    }

    private sealed class StubClock : ISystemClock
    {
        private long _ticks;

        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddSeconds(_ticks++);
    }
}
