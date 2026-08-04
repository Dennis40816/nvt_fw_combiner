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

    /// <summary>
    /// Explicit rebind accepts a fresh content identity without reapplying
    /// materialized full-file length.
    /// </summary>
    [Fact]
    public async Task InspectionAndRebindPreserveEditableLength()
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
        var draft = new GeneralMappingDraftState(
        [
            FileRow(
                "mapping-1",
                sourceStart: 0,
                targetStart: 0x100,
                length: 1),
        ]);

        GeneralSelectedFileDraftInspectionResult accepted =
            await service.AcceptDraftAsync(
                draft,
                new AuthoringRevision(4),
                CancellationToken.None);

        Assert.True(accepted.Succeeded);
        Assert.Equal(1, inspector.CallCount);
        GeneralMappingDraftRow acceptedRow = Assert.Single(accepted.Draft!.Rows);
        Assert.Equal(firstStamp, acceptedRow.Source.AcceptedFileStamp);

        GeneralMappingDraftState materialized =
            accepted.Draft.MaterializeFullFileLength("mapping-1");
        GeneralMappingDraftRow materializedRow = Assert.Single(materialized.Rows);
        Assert.Equal(4, materializedRow.SourceRange.Length);
        Assert.Equal(4, materializedRow.TargetRange.Length);

        GeneralMappingDraftState rebound = new(materialized.Rows.Select(row =>
            row.RebindSelectedFile(row.Source.Reference)));
        GeneralSelectedFileDraftInspectionResult reloaded =
            await service.AcceptDraftAsync(
                rebound,
                new AuthoringRevision(5),
                CancellationToken.None);

        Assert.True(reloaded.Succeeded);
        Assert.Equal(2, inspector.CallCount);
        GeneralMappingDraftRow reloadedRow = Assert.Single(reloaded.Draft!.Rows);
        Assert.Equal(secondStamp, reloadedRow.Source.AcceptedFileStamp);
        Assert.Equal(4, reloadedRow.SourceRange.Length);
        Assert.Equal(4, reloadedRow.TargetRange.Length);
    }

    /// <summary>Inspection failures are stable and never publish a partly bound draft.</summary>
    [Fact]
    public async Task InspectionFailureReturnsStableIssueWithoutPublishingDraft()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new IOException("The selected file became unavailable.")));
        var draft = new GeneralMappingDraftState(
        [
            FileRow(
                "mapping-1",
                sourceStart: 0,
                targetStart: 0x100,
                length: 4),
        ]);

        GeneralSelectedFileDraftInspectionResult result =
            await service.AcceptDraftAsync(
                draft,
                new AuthoringRevision(7),
                CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Draft);
        GeneralSelectedFileInspectionIssue issue = Assert.Single(result.Issues);
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
        var draft = new GeneralMappingDraftState(
        [
            FileRow(
                "mapping-1",
                sourceStart: 0,
                targetStart: 0x100,
                length: 4),
        ]);

        GeneralSelectedFileDraftInspectionResult result =
            await service.AcceptDraftAsync(
                draft,
                new AuthoringRevision(8),
                CancellationToken.None);

        GeneralSelectedFileInspectionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GeneralAuthoringIssueCodes.FileSizeExceeded, issue.Code);
        Assert.Contains("5 bytes", issue.Message, StringComparison.Ordinal);
        Assert.Contains("maximum 4", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>A failed explicit rebind cannot publish a replacement draft.</summary>
    [Fact]
    public async Task RebindInspectionFailureDoesNotPublishDraft()
    {
        var service = new GeneralSelectedFileInspectionService(
            new ThrowingSelectedFileContentInspector(
                new UnauthorizedAccessException("The selected file cannot be read.")));
        var draft = new GeneralMappingDraftState(
        [
            FileRow(
                "mapping-1",
                sourceStart: 0,
                targetStart: 0x100,
                length: 4),
        ]);

        GeneralMappingDraftState rebound = new(draft.Rows.Select(row =>
            row.RebindSelectedFile(row.Source.Reference)));
        GeneralSelectedFileDraftInspectionResult result =
            await service.AcceptDraftAsync(
                rebound,
                new AuthoringRevision(12),
                CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Draft);
        GeneralSelectedFileInspectionIssue issue = Assert.Single(result.Issues);
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
        var draft = new GeneralMappingDraftState(
        [
            FileRow(
                "mapping-1",
                sourceStart: 0,
                targetStart: 0x100,
                length: 4),
        ]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.AcceptDraftAsync(
                    draft,
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

        public ValueTask<long> ObserveLengthAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(_results.Peek().FileStamp.AcceptedLength);
        }

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
        public ValueTask<long> ObserveLengthAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<long>(exception);
        }

        public ValueTask<SelectedFileContentInspection> InspectAsync(
            string selectedPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<SelectedFileContentInspection>(exception);
        }
    }
}
