// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

/// <summary>Compatibility entry point for compiled Legacy Combiner postbuild plans through the shared host.</summary>
/// <remarks>Retained only for existing behavior tests; delete this test entry point in the g.2 wiring wave.</remarks>
public sealed class LegacyCombinerPostbuildProcessor : IExternalProcessor
{
    private readonly StagedExternalProcessorHost _host;

    /// <summary>Creates a compiled postbuild adapter using the shared staged execution host.</summary>
    public LegacyCombinerPostbuildProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner)
        : this(registry, toolRoot, stagingRoot, processRunner, null)
    {
    }

    internal LegacyCombinerPostbuildProcessor(
        ExternalCombinerToolRegistry registry,
        string toolRoot,
        string stagingRoot,
        IExternalProcessRunner processRunner,
        ExternalRuntimeDeployment? runtimeDeployment)
    {
        _host = new StagedExternalProcessorHost(
            registry,
            toolRoot,
            stagingRoot,
            processRunner,
            [new LegacyCombinerProcessorAdapter([], LegacyCombinerInvocationMode.CompiledPostbuild)],
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
