// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Compatibility entry point for manifest-based Legacy Combiner invocations through the shared host.</summary>
/// <remarks>Retained only for existing behavior tests; delete this test entry point in the g.2 wiring wave.</remarks>
public sealed class ExternalCombinerProcessor : IExternalProcessor
{
    private readonly StagedExternalProcessorHost _host;

    /// <summary>Creates a manifest invocation adapter using the shared staged execution host.</summary>
    public ExternalCombinerProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner,
        IEnumerable<ExternalCombinerInvocationProfile> invocationProfiles)
        : this(registry, toolRoot, stagingRoot, processRunner, invocationProfiles, null)
    {
    }

    internal ExternalCombinerProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner,
        IEnumerable<ExternalCombinerInvocationProfile> invocationProfiles,
        ExternalRuntimeDeployment? runtimeDeployment)
    {
        _host = new StagedExternalProcessorHost(
            registry,
            toolRoot,
            stagingRoot,
            processRunner,
            [new LegacyCombinerProcessorAdapter(invocationProfiles, LegacyCombinerInvocationMode.Manifest)],
            runtimeDeployment);
    }

    /// <inheritdoc />
    public ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        return _host.TransformAsync(request, cancellationToken);
    }
}
