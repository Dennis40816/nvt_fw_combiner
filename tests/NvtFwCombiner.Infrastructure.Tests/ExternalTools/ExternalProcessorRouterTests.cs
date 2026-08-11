using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests closed adapter selection without duplicating any processor byte semantics.</summary>
public sealed class ExternalProcessorRouterTests
{
    /// <summary>Verifies the compiled protocol selects the legacy postbuild adapter.</summary>
    [Fact]
    public async Task TransformRoutesCatalogProcessorIdToLegacyPostbuildAdapter()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest);

        ExternalProcessorResult result = await router.TransformAsync(
            Request("legacy-postbuild", ProtocolPlan()),
            CancellationToken.None);

        Assert.Equal("legacy", Assert.Single(result.Issues).Code);
        _ = Assert.Single(legacy.Requests);
        Assert.Empty(manifest.Requests);
    }

    /// <summary>Verifies an invocation without the legacy protocol uses the manifest adapter without id inference.</summary>
    [Fact]
    public async Task TransformRoutesUnregisteredProcessorIdToManifestAdapter()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest);

        ExternalProcessorResult result = await router.TransformAsync(Request("future-ab-profile"), CancellationToken.None);

        Assert.Equal("manifest", Assert.Single(result.Issues).Code);
        Assert.Empty(legacy.Requests);
        _ = Assert.Single(manifest.Requests);
    }

    /// <summary>Verifies a processor id cannot select the legacy adapter without the compiled protocol plan.</summary>
    [Fact]
    public async Task TransformDoesNotInferLegacyAdapterFromProcessorId()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest);

        ExternalProcessorResult result = await router.TransformAsync(Request("legacy-postbuild"), CancellationToken.None);

        Assert.Equal("manifest", Assert.Single(result.Issues).Code);
        Assert.Empty(legacy.Requests);
        _ = Assert.Single(manifest.Requests);
    }

    /// <summary>An unknown compiled protocol cannot fall back to another adapter.</summary>
    [Fact]
    public async Task TransformRejectsUnknownCompiledProtocolWithoutAdapterFallback()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest);
        var unknown = new ExternalProcessorProtocolPlan(
            "unknown-processor-protocol-v1",
            "firmware.bin",
            [
                new ExternalProcessorProtocolCommand(
                    "unknown",
                    [ExternalProcessorProtocolArgumentTokens.TargetFile],
                    [],
                    retainShortOutputTail: false),
            ]);

        ExternalProcessorResult result = await router.TransformAsync(
            Request("future-processor", unknown),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("external-processor.protocol.unsupported", Assert.Single(result.Issues).Code);
        Assert.Empty(legacy.Requests);
        Assert.Empty(manifest.Requests);
    }

    private static ExternalProcessorRequest Request(
        string processorId,
        ExternalProcessorProtocolPlan? protocolPlan = null)
    {
        return new ExternalProcessorRequest(
            "run",
            processorId,
            "combiner-1-13",
            new byte[] { 0 },
            [new ByteRange(0, 1)],
            protocolPlan: protocolPlan);
    }

    private static ExternalProcessorProtocolPlan ProtocolPlan()
    {
        return new ExternalProcessorProtocolPlan(
            LegacyCombinerPostbuildPlanCompiler.ProtocolId,
            "firmware.bin",
            [
                new ExternalProcessorProtocolCommand(
                    "crc",
                    ["CRC_Enable", ExternalProcessorProtocolArgumentTokens.TargetFile],
                    [],
                    retainShortOutputTail: false),
            ]);
    }

    private sealed class RecordingProcessor : IExternalProcessor
    {
        private readonly string _issueCode;

        internal RecordingProcessor(string issueCode)
        {
            _issueCode = issueCode;
        }

        internal List<ExternalProcessorRequest> Requests { get; } = [];

        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(ExternalProcessorResult.Failed([
                new CompositionIssue(_issueCode, "Synthetic adapter result."),
            ]));
        }
    }
}
