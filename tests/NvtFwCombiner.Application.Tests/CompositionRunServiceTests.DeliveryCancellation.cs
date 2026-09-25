using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Cancellation of loose delivery keeps the exact primary commit receipt without another execution.</summary>
    [Fact]
    public async Task LooseDeliveryCancellationRetainsCommittedPrimaryReceipt()
    {
        CompositionRunRequest original = CreateRequest();
        V2CompiledCompositionDetails source = original.CompiledComposition.V2Details;
        var details = new V2CompiledCompositionDetails(
            source.ProfileId,
            source.ProfileVersion,
            source.ExperienceId,
            source.CompositionKind,
            source.Provenance,
            source.InputContract,
            source.RegionAccessContract,
            source.OutputNamingRequirement,
            source.IcNumberInputMode,
            [new CompiledAdditionalDelivery("test-delivery", new ByteRange(0, 4), "{name}.bin", ["name"])]);
        CompiledComposition composition = CompiledComposition.CreateV2RuntimeExecutable(
            original.CompiledComposition.Plan,
            details);
        var request = new CompositionRunRequest(
            original.RunId,
            composition,
            original.ArtifactBindings.Values,
            original.OutputFileName);
        var naming = new OutputNamingSummary(
            "synthetic",
            "synthetic-standard-merge.bin",
            request.OutputFileName,
            request.OutputFileName,
            false,
            "utc",
            FirstTimestamp,
            [new OutputNamingTokenSummary("name", "additional", true, null, null, "test")]);
        request.PreparedOutputName = new OutputNameResolution(request.OutputFileName, naming, []);
        var reader = new FakeArtifactReader(new Dictionary<string, byte[]>
        {
            ["dp-artifact"] = [1, 2, 3, 4],
            ["tp-artifact"] = [9, 8, 7, 6],
        });
        var outputWriter = new FakeOutputWriter();
        using var cancellation = new CancellationTokenSource();
        var deliveryWriter = new CancellingDeliveryWriter(cancellation);
        var service = new CompositionRunService(
            reader,
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            outputWriter,
            externalProcessor: null,
            deliveryWriter);

        CompositionRunResult result = await service.PreviewOrBuildAsync(request, build: true, cancellation.Token);

        Assert.True(outputWriter.WasCalled);
        Assert.Equal(1, deliveryWriter.CommitCalls);
        Assert.True(result.Succeeded);
        Assert.False(result.IsDeliveryComplete);
        Assert.Equal("committed:synthetic-standard-merge.bin", result.CommittedOutputId);
        Assert.Equal(8, result.OutputSize);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(outputWriter.OutputBytes)).ToLowerInvariant(), result.OutputSha256);
        Assert.True(result.Report.Output.Committed);
        Assert.Contains(result.Report.Issues, issue => issue.Code == "delivery.test-delivery.failed");
    }

    private sealed class CancellingDeliveryWriter(CancellationTokenSource cancellation) : ICompositionDeliveryWriter
    {
        public string DeliveryKind => "test-delivery";

        internal int CommitCalls { get; private set; }

        public string EnsureCanCommit(string primaryOutputFileName, string suggestedDeliveryFileName)
        {
            return suggestedDeliveryFileName;
        }

        public ValueTask<string> CommitAsync(
            string deliveryFileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            CommitCalls++;
            cancellation.Cancel();
            return ValueTask.FromCanceled<string>(cancellationToken);
        }
    }
}
