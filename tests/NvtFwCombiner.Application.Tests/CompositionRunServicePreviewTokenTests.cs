using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Verifies preview tokens bind the compiled artifact when overwritten operations produce equal bytes.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenCompiledOperationDetailsChange()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));
        CompositionRunRequest firstRequest = CreateOverwriteRequest("run-overwrite-a", 0x11);
        CompositionRunRequest secondRequest = CreateOverwriteRequest("run-overwrite-b", 0x12);

        CompositionRunResult first = await service.PreviewAsync(firstRequest, CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(secondRequest, CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    /// <summary>Verifies preview approval binds compiled address-space padding policy, not only output bytes.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenInputPaddingPolicyChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["exact-artifact"] = [0x11, 0x22, 0x33, 0x44],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult withoutPadding = await service.PreviewAsync(
            CreatePaddedInputRequest(inputPaddingByte: null, artifactId: "exact-artifact"),
            CancellationToken.None);
        CompositionRunResult withPadding = await service.PreviewAsync(
            CreatePaddedInputRequest(inputPaddingByte: 0xFF, artifactId: "exact-artifact"),
            CancellationToken.None);

        Assert.Equal(withoutPadding.OutputBytes.ToArray(), withPadding.OutputBytes.ToArray());
        Assert.NotEqual(withoutPadding.PreviewToken, withPadding.PreviewToken);
    }

    /// <summary>Verifies preview approval binds compiled address-space truncation policy.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenInputOversizePolicyChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0, 0, 0, 0],
                ["ctrlram-exact"] = [0xAA, 0xBB],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult withoutTruncation = await service.PreviewAsync(
            CreateCtrlRamReplaceRequest(InputOversizePolicy.Reject, "ctrlram-exact"),
            CancellationToken.None);
        CompositionRunResult withTruncation = await service.PreviewAsync(
            CreateCtrlRamReplaceRequest(InputOversizePolicy.TruncateWithWarning, "ctrlram-exact"),
            CancellationToken.None);

        Assert.Equal(withoutTruncation.OutputBytes.ToArray(), withTruncation.OutputBytes.ToArray());
        Assert.NotEqual(withoutTruncation.PreviewToken, withTruncation.PreviewToken);
    }

    /// <summary>Verifies preview approval binds the selected IC number context.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenIcNumberChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0, 0, 0, 0, 0, 0, 0, 0],
                ["dp-artifact"] = [1, 2, 3, 4],
                ["ld-artifact"] = [5, 6],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult first = await service.PreviewAsync(
            CreateDpReplaceRequest("51923"),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            CreateDpReplaceRequest("51926"),
            CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }

    /// <summary>Verifies preview approval binds the caller-selected output file name.</summary>
    [Fact]
    public async Task PreviewTokenChangesWhenOutputFileNameChanges()
    {
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["dp-artifact"] = [1, 2, 3, 4],
                ["tp-artifact"] = [9, 8, 7, 6],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]));

        CompositionRunResult first = await service.PreviewAsync(
            CreateRequest(outputFileName: "first.bin"),
            CancellationToken.None);
        CompositionRunResult second = await service.PreviewAsync(
            CreateRequest(outputFileName: "second.bin"),
            CancellationToken.None);

        Assert.Equal(first.OutputBytes.ToArray(), second.OutputBytes.ToArray());
        Assert.NotEqual(first.PreviewToken, second.PreviewToken);
    }
}
