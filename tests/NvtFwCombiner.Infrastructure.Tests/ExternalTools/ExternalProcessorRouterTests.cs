using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Tests closed adapter selection without duplicating any processor byte semantics.</summary>
public sealed class ExternalProcessorRouterTests
{
    /// <summary>Verifies a registered postbuild processor id never falls through to the manifest adapter.</summary>
    [Fact]
    public async Task TransformRoutesCatalogProcessorIdToLegacyPostbuildAdapter()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest, ["legacy-postbuild"]);

        ExternalProcessorResult result = await router.TransformAsync(Request("legacy-postbuild"), CancellationToken.None);

        Assert.Equal("legacy", Assert.Single(result.Issues).Code);
        _ = Assert.Single(legacy.Requests);
        Assert.Empty(manifest.Requests);
    }

    /// <summary>Verifies a profile-declared processor id uses the manifest adapter without catalog inference.</summary>
    [Fact]
    public async Task TransformRoutesUnregisteredProcessorIdToManifestAdapter()
    {
        var legacy = new RecordingProcessor("legacy");
        var manifest = new RecordingProcessor("manifest");
        var router = new ExternalProcessorRouter(legacy, manifest, ["legacy-postbuild"]);

        ExternalProcessorResult result = await router.TransformAsync(Request("future-ab-profile"), CancellationToken.None);

        Assert.Equal("manifest", Assert.Single(result.Issues).Code);
        Assert.Empty(legacy.Requests);
        _ = Assert.Single(manifest.Requests);
    }

    /// <summary>Verifies catalog dispatch identifiers are closed and unambiguous.</summary>
    [Fact]
    public void ConstructorRejectsDuplicateCatalogProcessorIds()
    {
        _ = Assert.Throws<ArgumentException>(() => new ExternalProcessorRouter(
            new RecordingProcessor("legacy"),
            new RecordingProcessor("manifest"),
            ["legacy-postbuild", "legacy-postbuild"]));
    }

    private static ExternalProcessorRequest Request(string processorId)
    {
        return new ExternalProcessorRequest(
            "run",
            processorId,
            "combiner-1-13",
            new byte[] { 0 },
            [new ByteRange(0, 1)]);
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
