using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>The composition root shares one Toolchain session with UI and environment loading.</summary>
public sealed class ToolchainConfigurationHostTests
{
    /// <summary>Repeated consumers receive the same loaded session instead of independent configuration state.</summary>
    [Fact]
    public async Task HostPublishesOneSharedToolchainSession()
    {
        var session = new StubSession();
        CompositionHostServices host = CompositionHostServices.Create(
            new ExternalProcessorEnvironmentLoader(static (_, _) =>
                ValueTask.FromResult(new ExternalProcessorRuntimeEnvironment(
                    null, new StubReadiness(), 0))),
            loadPolicy: null,
            toolchainConfiguration: session);

        IToolchainRuntimeConfigurationSession first = await host.GetToolchainRuntimeConfigurationAsync(
            TestContext.Current.CancellationToken);
        IToolchainRuntimeConfigurationSession second = await host.GetToolchainRuntimeConfigurationAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(session, first);
        Assert.Same(first, second);
        Assert.Equal(1, session.ReloadCount);
    }

    private sealed class StubSession : IToolchainRuntimeConfigurationSession
    {
        public ToolchainRuntimeConfigurationSnapshot Current { get; private set; } = new(
            0, ToolchainRuntimeConfigurationStatus.NotLoaded, null, null, []);
        internal int ReloadCount { get; private set; }
        public ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
        {
            ReloadCount++;
            var selection = new ToolchainRuntimeSelection(ToolchainRuntimeSource.Bundled);
            Current = new(1, ToolchainRuntimeConfigurationStatus.Current, selection, selection, []);
            return ValueTask.FromResult(new ToolchainRuntimeConfigurationOperationResult(Current, true, []));
        }
        public ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(ToolchainRuntimeSelection selection, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubReadiness : IRuntimeDependencyReadinessProvider
    {
        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
