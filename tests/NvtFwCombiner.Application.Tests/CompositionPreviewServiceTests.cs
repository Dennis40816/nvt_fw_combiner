using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests application preview orchestration over the shared domain engine.</summary>
public sealed class CompositionPreviewServiceTests
{
    /// <summary>Verifies preview reads artifact bindings and executes the compiled plan without a second executor.</summary>
    [Fact]
    public async Task PreviewReadsArtifactsAndReturnsEngineOutput()
    {
        CompositionPlan plan = CreatePlan();
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["source-artifact"] = [1, 2, 3, 4],
        });
        var service = new CompositionPreviewService(reader);
        var request = new CompositionPreviewRequest(
            plan,
            new Dictionary<string, string>
            {
                ["source"] = "source-artifact",
            });

        CompositionExecutionResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0, 2, 3, 0], result.OutputBytes.ToArray());
        Assert.Equal(["source-artifact"], reader.ReadArtifactIds);
    }

    /// <summary>Verifies preview fails closed when a required input address space has no artifact binding.</summary>
    [Fact]
    public async Task PreviewReportsMissingRequiredBinding()
    {
        var service = new CompositionPreviewService(
            new FakeArtifactReader([]));
        Dictionary<string, string> bindings = [];
        var request = new CompositionPreviewRequest(CreatePlan(), bindings);

        CompositionExecutionResult result = await service.PreviewAsync(request, CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("preview.binding.missing", issue.Code);
    }

    private static CompositionPlan CreatePlan()
    {
        AddressSpace[] addressSpaces =
        [
            new("source", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        CompositionOperation[] operations =
        [
            CompositionOperation.CopyRange(
                "copy-source",
                10,
                "source",
                new ByteRange(1, 2),
                "output-image",
                new ByteRange(1, 2),
                OverlapPolicy.Reject,
                "copy source"),
        ];
        return new CompositionPlan(ImageInitialization.Blank("output-image", 4, 0), addressSpaces, operations);
    }

    private sealed class FakeArtifactReader : IArtifactReader
    {
        private readonly Dictionary<string, byte[]> _artifacts;
        private readonly List<string> _readArtifactIds = [];

        internal FakeArtifactReader(Dictionary<string, byte[]> artifacts)
        {
            _artifacts = artifacts;
        }

        internal IReadOnlyList<string> ReadArtifactIds => _readArtifactIds;

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string artifactId, CancellationToken cancellationToken)
        {
            _readArtifactIds.Add(artifactId);
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_artifacts[artifactId]);
        }
    }
}
