using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies content-authoritative General selected-file acceptance.</summary>
public sealed class GeneralSelectedFileContentTests
{
    /// <summary>Same-size byte changes produce a distinct accepted identity.</summary>
    [Fact]
    public void FileStampUsesAcceptedLengthAndSha256Only()
    {
        var first = FileStamp.FromBytes([0x10, 0x20, 0x30, 0x40]);
        var same = FileStamp.FromBytes([0x10, 0x20, 0x30, 0x40]);
        var mutated = FileStamp.FromBytes([0x10, 0x20, 0x31, 0x40]);

        Assert.Equal(first, same);
        Assert.NotEqual(first, mutated);
        Assert.Equal(4, first.AcceptedLength);
        Assert.Equal(64, first.Sha256.Length);
        Assert.DoesNotContain(first.Sha256, static character => character is >= 'A' and <= 'F');
    }

    /// <summary>Retained bytes are cloned and must agree with their exact content identity.</summary>
    [Fact]
    public void SelectedFileInspectionRetainsOnlyMatchingImmutableBytes()
    {
        byte[] bytes = [0x10, 0x20];
        var inspection = new SelectedFileContentInspection(
            FileStamp.FromBytes(bytes),
            acceptedBytes: bytes);

        bytes[0] = 0xFF;

        Assert.Equal([0x10, 0x20], inspection.AcceptedBytes!.Value.ToArray());
        _ = Assert.Throws<ArgumentException>(() => new SelectedFileContentInspection(
            FileStamp.FromBytes([0x10, 0x20]),
            acceptedBytes: new byte[] { 0x10, 0x21 }));
    }

    /// <summary>Explicit reinspection captures a fresh content identity.</summary>
    [Fact]
    public async Task ReinspectionCapturesFreshContentIdentity()
    {
        var firstStamp = FileStamp.FromBytes([1, 2, 3, 4]);
        var secondStamp = FileStamp.FromBytes([5, 6, 7, 8, 9, 10]);
        var inspector = new FakeSelectedFileContentInspector(
            new SelectedFileContentInspection(
                firstStamp,
                "source.bin",
                new DateTimeOffset(2026, 7, 30, 1, 0, 0, TimeSpan.Zero)),
            new SelectedFileContentInspection(
                secondStamp,
                "source.bin",
                new DateTimeOffset(2026, 7, 30, 2, 0, 0, TimeSpan.Zero)));
        var service = new GeneralSelectedFileInspectionService(inspector);
        GeneralSelectedFileInspectionResult accepted =
            await service.InspectAsync(
                "mapping-1",
                @"C:\firmware\source.bin",
                new AuthoringRevision(4),
                CancellationToken.None);

        Assert.True(accepted.Succeeded);
        Assert.Equal(1, inspector.CallCount);
        Assert.Equal(firstStamp, accepted.Inspection!.FileStamp);

        GeneralSelectedFileInspectionResult reloaded =
            await service.InspectAsync(
                "mapping-1",
                @"C:\firmware\source.bin",
                new AuthoringRevision(5),
                CancellationToken.None);

        Assert.True(reloaded.Succeeded);
        Assert.Equal(2, inspector.CallCount);
        Assert.Equal(secondStamp, reloaded.Inspection!.FileStamp);
    }

    /// <summary>Inspection failures are stable and never publish a partly bound draft.</summary>
    [Fact]
    public async Task InspectionFailureReturnsStableIssueWithoutPublishingDraft()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new IOException("The selected file became unavailable.")));
        GeneralSelectedFileInspectionResult result =
            await service.InspectAsync(
                "mapping-1",
                @"C:\firmware\source.bin",
                new AuthoringRevision(7),
                CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Inspection);
        GeneralSelectedFileInspectionIssue issue = Assert.IsType<GeneralSelectedFileInspectionIssue>(
            result.Issue);
        Assert.Equal(
            GeneralSelectedFileInspectionIssueCodes.InspectionFailed,
            issue.Code);
        Assert.Equal("mapping-1", issue.DefinitionId);
        Assert.Contains(nameof(IOException), issue.Message, StringComparison.Ordinal);
    }

    /// <summary>An inspector size rejection remains the canonical General admission blocker.</summary>
    [Fact]
    public async Task InspectionSizeLimitReturnsTypedFileSizeBlocker()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new SelectedFileSizeLimitExceededException(5, 4)),
            maximumFileBytes: 4);
        GeneralSelectedFileInspectionResult result =
            await service.InspectAsync(
                "mapping-1",
                @"C:\firmware\source.bin",
                new AuthoringRevision(8),
                CancellationToken.None);

        GeneralSelectedFileInspectionIssue issue = Assert.IsType<GeneralSelectedFileInspectionIssue>(
            result.Issue);
        Assert.Equal(GeneralAuthoringIssueCodes.FileSizeExceeded, issue.Code);
        Assert.Contains("5 bytes", issue.Message, StringComparison.Ordinal);
        Assert.Contains("maximum 4", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>An unauthorized inspection returns one typed failure.</summary>
    [Fact]
    public async Task UnauthorizedInspectionReturnsTypedFailure()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new UnauthorizedAccessException("The selected file cannot be read.")));
        GeneralSelectedFileInspectionResult result =
            await service.InspectAsync(
                "mapping-1",
                @"C:\firmware\source.bin",
                new AuthoringRevision(12),
                CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Inspection);
        GeneralSelectedFileInspectionIssue issue = Assert.IsType<GeneralSelectedFileInspectionIssue>(
            result.Issue);
        Assert.Equal(
            GeneralSelectedFileInspectionIssueCodes.InspectionFailed,
            issue.Code);
        Assert.Equal("mapping-1", issue.DefinitionId);
    }

    /// <summary>Cancellation remains cancellation rather than becoming an inspection issue.</summary>
    [Fact]
    public async Task InspectionCancellationIsPropagated()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new OperationCanceledException()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.InspectAsync(
                    "mapping-1",
                    @"C:\firmware\source.bin",
                    new AuthoringRevision(13),
                    cancellation.Token)
                .AsTask());
    }

    /// <summary>Display and timestamp hints are validated but do not participate in identity.</summary>
    [Fact]
    public void InspectionHintsMustBeWellFormedAndUtc()
    {
        var stamp = FileStamp.FromBytes([1, 2, 3, 4]);

        _ = Assert.Throws<ArgumentException>(() => new GeneralSelectedFileInspection(
            "mapping-1",
            new AuthoringRevision(1),
            @"C:\firmware\source.bin",
            stamp,
            displayNameHint: " "));
        _ = Assert.Throws<ArgumentException>(() => new GeneralSelectedFileInspection(
            "mapping-1",
            new AuthoringRevision(1),
            @"C:\firmware\source.bin",
            stamp,
            lastWriteTimeUtcHint: new DateTimeOffset(
                2026,
                7,
                30,
                9,
                0,
                0,
                TimeSpan.FromHours(8))));
    }

    /// <summary>Source Slice and From File Start remain presets over one Start + Length operation.</summary>
    [Fact]
    public void FileRangePresetsCompileTheSameCheckedOperationShape()
    {
        GeneralMappingDraftRow fromStart = FileRow(
            "from-start",
            sourceStart: 0,
            targetStart: 0x100,
            length: 0x20);
        GeneralMappingDraftRow sourceSlice = FileRow(
            "source-slice",
            sourceStart: 0x10,
            targetStart: 0x100,
            length: 0x20);

        Assert.Equal(
            GeneralMappingFileRangePreset.FromFileStart,
            fromStart.FileRangePreset);
        Assert.Equal(
            GeneralMappingFileRangePreset.SourceSlice,
            sourceSlice.FileRangePreset);
        Assert.Equal(fromStart.OperationKind, sourceSlice.OperationKind);
        Assert.Equal(fromStart.TargetRange, sourceSlice.TargetRange);
        Assert.Equal(fromStart.SourceRange.Length, sourceSlice.SourceRange.Length);
    }

    private static GeneralMappingDraftRow FileRow(
        string mappingId,
        long sourceStart,
        long targetStart,
        long length)
    {
        return new GeneralMappingDraftRow(
            mappingId,
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File(@"C:\firmware\source.bin"),
            new ByteRange(sourceStart, length),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(targetStart, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Copy selected General file.");
    }

    private sealed class FakeSelectedFileContentInspector(
        params SelectedFileContentInspection[] results)
        : ISelectedFileContentInspector
    {
        private readonly Queue<SelectedFileContentInspection> _results =
            new(results);

        internal int CallCount { get; private set; }

        public ValueTask<SelectedFileContentInspection> InspectAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(_results.Dequeue());
        }
    }

    private sealed class ThrowingSelectedFileContentInspector(Exception exception)
        : ISelectedFileContentInspector
    {
        public ValueTask<SelectedFileContentInspection> InspectAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<SelectedFileContentInspection>(exception);
        }
    }
}
