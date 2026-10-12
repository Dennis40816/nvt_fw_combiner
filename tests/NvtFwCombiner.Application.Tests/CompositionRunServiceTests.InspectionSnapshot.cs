using System.Runtime.InteropServices;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>Verifies successful Replace inspection preserves the selected reference and authoritative output.</summary>
    [Fact]
    public async Task SuccessfulReplaceCapturesImmutableSelectedOutputInspection()
    {
        byte[] selectedReference = [0x10, 0x20, 0x30, 0x40];
        byte[] nonOutputReference = [0xA0, 0xB0, 0xC0, 0xD0];
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["output-reference-artifact"] = selectedReference,
                ["scratch-reference-artifact"] = nonOutputReference,
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]));

        CompositionRunResult result = await service.PreviewAsync(
            CreateMultiReferenceReplaceRequest(),
            CancellationToken.None);

        CompositionRunInspectionSnapshot snapshot = Assert.IsType<CompositionRunInspectionSnapshot>(
            result.InspectionSnapshot);
        Assert.Equal("run-multi-reference-replace", snapshot.RunId);
        Assert.Equal("output-image", snapshot.OutputSpaceId);
        Assert.Equal("output-reference", snapshot.ReferenceSpaceId);
        GoldenAssert.AssertHashEqualsGolden(result.Report.Output.Sha256, snapshot.OutputSha256, "SHA-256");
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x20, 0x30, 0x40], snapshot.ReferenceBytes.ToArray(), "firmware image");
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x99, 0x30, 0x40], snapshot.OutputBytes.ToArray(), "firmware image");
        GoldenAssert.AssertEqualsGolden(result.OutputBytes.ToArray(), snapshot.OutputBytes.ToArray(), "firmware image");
        Assert.True(MemoryMarshal.TryGetArray(result.OutputBytes, out ArraySegment<byte> resultOutput));
        Assert.True(MemoryMarshal.TryGetArray(snapshot.OutputBytes, out ArraySegment<byte> inspectionOutput));
        Assert.True(MemoryMarshal.TryGetArray(snapshot.ReferenceBytes, out ArraySegment<byte> inspectionReference));
        Assert.True(ReferenceEquals(resultOutput.Array, inspectionOutput.Array), "Expected firmware buffer ownership.");
        Assert.True(!ReferenceEquals(selectedReference, inspectionReference.Array), "Expected firmware buffer ownership.");

        selectedReference.AsSpan().Fill(0xFF);
        nonOutputReference.AsSpan().Fill(0xEE);
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x20, 0x30, 0x40], snapshot.ReferenceBytes.ToArray(), "firmware image");
    }

    /// <summary>Verifies Merge and Replace executions without an output do not claim complete inspection bytes.</summary>
    [Fact]
    public async Task InspectionSnapshotIsAbsentWithoutCompletedReplaceExecution()
    {
        CompositionRunService mergeService = CreateService(out _);
        CompositionRunResult merge = await mergeService.PreviewAsync(CreateRequest(), CancellationToken.None);

        var failedReplaceService = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunResult failedReplace = await failedReplaceService.PreviewAsync(
            CreateDpReplaceRequest("51923"),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, merge.Status);
        Assert.Null(merge.InspectionSnapshot);
        Assert.Equal(CompositionExecutionStatus.Failed, failedReplace.Status);
        Assert.Null(failedReplace.InspectionSnapshot);
    }

    /// <summary>Verifies Build retains complete review bytes after validated Replace execution.</summary>
    [Fact]
    public async Task BuildInspectionSnapshotRetainsSuccessfulReplaceOutput()
    {
        var writer = new FakeOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["output-reference-artifact"] = [0x10, 0x20, 0x30, 0x40],
                ["scratch-reference-artifact"] = [0xA0, 0xB0, 0xC0, 0xD0],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]),
            writer);

        CompositionRunResult result = await service.BuildAsync(
            CreateMultiReferenceReplaceRequest(),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.True(writer.WasCalled);
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x99, 0x30, 0x40], result.OutputBytes.ToArray(), "firmware image");
        CompositionRunInspectionSnapshot inspection = Assert.IsType<CompositionRunInspectionSnapshot>(
            result.InspectionSnapshot);
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x20, 0x30, 0x40], inspection.ReferenceBytes.ToArray(), "firmware image");
        GoldenAssert.AssertEqualsGolden((ReadOnlySpan<byte>)[0x10, 0x99, 0x30, 0x40], inspection.OutputBytes.ToArray(), "firmware image");
        GoldenAssert.AssertHashEqualsGolden(result.Report.Output.Sha256, inspection.OutputSha256, "SHA-256");
        Assert.Empty(result.Report.Issues);
    }
}
