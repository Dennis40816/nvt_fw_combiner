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
        Assert.Equal("output-reference", snapshot.ReferenceSpaceId);
        Assert.Equal([0x10, 0x20, 0x30, 0x40], snapshot.ReferenceBytes.ToArray());
        Assert.Equal([0x10, 0x99, 0x30, 0x40], snapshot.OutputBytes.ToArray());
        Assert.Equal(result.OutputBytes.ToArray(), snapshot.OutputBytes.ToArray());
        Assert.True(MemoryMarshal.TryGetArray(result.OutputBytes, out ArraySegment<byte> resultOutput));
        Assert.True(MemoryMarshal.TryGetArray(snapshot.OutputBytes, out ArraySegment<byte> inspectionOutput));
        Assert.True(MemoryMarshal.TryGetArray(snapshot.ReferenceBytes, out ArraySegment<byte> inspectionReference));
        Assert.Same(resultOutput.Array, inspectionOutput.Array);
        Assert.NotSame(selectedReference, inspectionReference.Array);

        selectedReference.AsSpan().Fill(0xFF);
        nonOutputReference.AsSpan().Fill(0xEE);
        Assert.Equal([0x10, 0x20, 0x30, 0x40], snapshot.ReferenceBytes.ToArray());
    }

    /// <summary>Verifies Merge and failed Replace runs do not claim a complete before/after inspection.</summary>
    [Fact]
    public async Task InspectionSnapshotIsAbsentOutsideSuccessfulReplace()
    {
        CompositionRunService mergeService = CreateService(out _);
        CompositionRunResult merge = await mergeService.PreviewAsync(CreateRequest(), CancellationToken.None);

        var failedReplaceService = new CompositionRunService(
            new FakeArtifactReader([]),
            new FakeClock([FirstTimestamp, SecondTimestamp]));
        CompositionRunResult failedReplace = await failedReplaceService.PreviewAsync(
            CreateDpReplaceRequest("51920"),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, merge.Status);
        Assert.Null(merge.InspectionSnapshot);
        Assert.Equal(CompositionExecutionStatus.Failed, failedReplace.Status);
        Assert.Null(failedReplace.InspectionSnapshot);
    }

    /// <summary>Verifies successful Replace byte execution cannot expose inspection after publication is rejected.</summary>
    [Fact]
    public async Task InspectionSnapshotIsAbsentWhenSuccessfulReplaceExecutionFailsPublication()
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
            CreateMultiReferenceReplaceRequest().WithApprovedPreviewToken("mismatched-preview-token"),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.False(writer.WasCalled);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Null(result.InspectionSnapshot);
        Assert.Contains(result.Report.Issues, issue => issue.Code == "build.preview-token.mismatch");
    }
}
