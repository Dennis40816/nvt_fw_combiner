using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Build verifies the accepted content stamp before composition consumes mutated bytes.</summary>
    [Fact]
    public async Task BuildFailsClosedWhenAcceptedArtifactChangesAtTheSameLength()
    {
        byte[] acceptedDp = [1, 2, 3, 4];
        var reader = new MutableArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = acceptedDp,
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        var writer = new FakeOutputWriter();
        var service = new CompositionRunService(
            reader,
            new FakeClock(
            [
                FirstTimestamp,
                SecondTimestamp,
                ThirdTimestamp,
                FourthTimestamp,
            ]),
            writer);
        CompositionRunRequest request = CreateRequest(bindings:
        [
            new InputArtifactBinding(
                "dp-input",
                "dp-input",
                "dp-artifact",
                "dp-input.bin",
                CompiledInputArtifactClass.TpFirmware,
                acceptedContentStamp: FileStamp.FromBytes(acceptedDp)),
            new InputArtifactBinding(
                "tp-input",
                "tp-input",
                "tp-artifact",
                "tp-input.bin",
                CompiledInputArtifactClass.TpFirmware,
                acceptedContentStamp: FileStamp.FromBytes([9, 8, 7, 6])),
        ]);
        CompositionRunResult preview =
            await service.PreviewAsync(request, CancellationToken.None);
        reader.Set("dp-artifact", [1, 2, 9, 4]);

        CompositionRunResult build = await service.BuildAsync(
            request.WithApprovedPreviewToken(preview.PreviewToken!),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, build.Status);
        Assert.False(writer.WasCalled);
        CompositionIssue issue = Assert.Single(build.Report.Issues);
        Assert.Equal(
            CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch,
            issue.Code);
        Assert.DoesNotContain(
            build.Report.Inputs,
            static input => input.AddressSpaceId == "dp-input");
        Assert.True(build.OutputBytes.IsEmpty);
    }

    private sealed class MutableArtifactReader(
        IReadOnlyDictionary<string, byte[]> artifacts)
        : IArtifactReader
    {
        private readonly Dictionary<string, byte[]> _artifacts =
            artifacts.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);

        internal void Set(string artifactId, byte[] bytes)
        {
            _artifacts[artifactId] = bytes;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(
            string artifactId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(
                _artifacts[artifactId]);
        }
    }
}
