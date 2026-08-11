using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies the canonical typed General mapping draft and range codec.</summary>
public sealed class GeneralMappingDraftStateTests
{
    /// <summary>Draft snapshots retain typed facts without caller-owned collection aliases.</summary>
    [Fact]
    public void DraftDefensivelyCopiesRowsAndPreservesCanonicalFacts()
    {
        List<GeneralMappingDraftRow> rows =
        [
            FileRow(
                "copy-dp",
                ExplicitMappingOperationKind.CopyRange,
                new ByteRange(0x10, 0x20),
                new ByteRange(0x100, 0x20)),
        ];

        var draft = new GeneralMappingDraftState(rows);
        rows.Clear();

        GeneralMappingDraftRow row = Assert.Single(draft.Rows);
        Assert.Equal(AuthoringDraftKind.GeneralMapping, draft.DraftKind);
        Assert.Equal(GeneralMappingSourceKind.FileArtifact, row.Source.Kind);
        Assert.Equal("dp.bin", row.Source.Reference);
        Assert.Equal(new ByteRange(0x10, 0x20), row.SourceRange);
        Assert.Equal(new ByteRange(0x100, 0x20), row.TargetRange);
        Assert.Equal(0x120, row.TargetRange.EndExclusive);
        Assert.Equal(ExplicitMappingOperationKind.CopyRange, row.OperationKind);
        Assert.Equal(OverlapPolicy.Reject, row.OverlapPolicy);
    }

    /// <summary>Stable row identities remain unique within one draft.</summary>
    [Fact]
    public void DraftRejectsDuplicateMappingIds()
    {
        GeneralMappingDraftRow row = FileRow(
            "duplicate",
            ExplicitMappingOperationKind.CopyRange,
            new ByteRange(0, 1),
            new ByteRange(0, 1));

        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralMappingDraftState([row, row]));
    }

    /// <summary>Source and target ranges must describe an equal-length operation.</summary>
    [Fact]
    public void MappingRowRejectsUnequalSourceAndTargetLengths()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralMappingDraftRow(
                "bad-length",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File("input.bin"),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 3),
                OverlapPolicy.Reject,
                alignment: 1,
                "Copy explicit mapping."));
    }

    /// <summary>Copy operations cannot consume virtual patch sources.</summary>
    [Fact]
    public void MappingRowRejectsPatchSourceForCopyOperation()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralMappingDraftRow(
                "bad-source-kind",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.HexOverwrite("AABB"),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "Copy explicit mapping."));
    }

    /// <summary>Editable state reports non-range mapping invariants without throwing.</summary>
    [Fact]
    public void AuthoringStateCapturesMappingInvariantFailure()
    {
        var state = AuthoringMappingState.Create(
            "bad-alignment",
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("input.bin"),
            "0",
            "0",
            "1",
            CompositionAddressSpaceIds.OutputImage,
            OverlapPolicy.Reject,
            alignment: 0,
            "Copy explicit mapping.");

        Assert.False(state.IsValid);
        Assert.Null(state.Mapping);
        Assert.Equal(AuthoringMappingIssueCodes.MappingInvalid, state.Issue!.Code);
    }

    /// <summary>Inline sources cannot acquire file-only selection or inspection state.</summary>
    [Fact]
    public void InlineMappingRowsRejectFileRebinding()
    {
        var row = new GeneralMappingDraftRow(
            "inline",
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexOverwrite("AABB"),
            new ByteRange(0, 2),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(0, 2),
            OverlapPolicy.Reject,
            alignment: 1,
            "Inline overwrite mapping.");

        InvalidOperationException inspection = Assert.Throws<InvalidOperationException>(() =>
            row.WithAcceptedFileStamp(FileStamp.FromBytes([0xAA, 0xBB])));
        InvalidOperationException selection = Assert.Throws<InvalidOperationException>(() =>
            row.RebindSelectedFile("source.bin"));

        Assert.Equal(
            "Only file-backed General sources can accept a content stamp.",
            inspection.Message);
        Assert.Equal(
            "Only file-backed General sources can be rebound.",
            selection.Message);
    }

    /// <summary>Hexadecimal and decimal authoring text produce one half-open range.</summary>
    [Theory]
    [InlineData("0x10", "0x20", 0x10, 0x20)]
    [InlineData("16", "32", 0x10, 0x20)]
    public void RangeCodecUsesStartAndLength(
        string start,
        string length,
        long expectedStart,
        long expectedLength)
    {
        bool parsed = AuthoringByteRangeCodec.TryParseStartAndLength(
            start,
            length,
            out ByteRange range,
            out AuthoringRangeTextIssue? issue);

        Assert.True(parsed, issue?.Message);
        Assert.Null(issue);
        Assert.Equal(expectedStart, range.Start);
        Assert.Equal(expectedLength, range.Length);
        Assert.Equal(expectedStart + expectedLength, range.EndExclusive);
        Assert.Equal("0x10", AuthoringByteRangeCodec.FormatHex(range.Start));
    }

    /// <summary>Invalid or overflowing authoring ranges fail closed with typed causes.</summary>
    [Theory]
    [InlineData("0", "0", AuthoringRangeTextIssueKind.LengthInvalid)]
    [InlineData("-1", "1", AuthoringRangeTextIssueKind.StartInvalid)]
    [InlineData("9223372036854775807", "1", AuthoringRangeTextIssueKind.RangeOverflow)]
    public void RangeCodecFailsClosed(
        string start,
        string length,
        AuthoringRangeTextIssueKind expectedKind)
    {
        bool parsed = AuthoringByteRangeCodec.TryParseStartAndLength(
            start,
            length,
            out _,
            out AuthoringRangeTextIssue? issue);

        Assert.False(parsed);
        Assert.Equal(expectedKind, Assert.IsType<AuthoringRangeTextIssue>(issue).Kind);
    }

    /// <summary>UI and CLI text resolve through one capacity/fill validation contract.</summary>
    [Theory]
    [InlineData("0x10", null, 0x10, 0x00)]
    [InlineData("16", "90", 0x10, 0x5A)]
    [InlineData("0x10", "0xFF", 0x10, 0xFF)]
    public void InitializerInputResolvesCanonicalTypedValue(
        string capacity,
        string? fill,
        long expectedCapacity,
        int expectedFill)
    {
        bool resolved = new GeneralMergeInitializerInput(
            capacity,
            fill).TryResolve(
                out GeneralMergeOutputInitializer? initializer,
                out CompositionIssue? issue);

        Assert.True(resolved, issue?.Message);
        Assert.Null(issue);
        Assert.Equal(expectedCapacity, initializer!.Capacity);
        Assert.Equal(expectedFill, initializer.FillByte);
    }

    /// <summary>Out-of-domain fill text fails with the same stable adapter issue.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("0x100")]
    [InlineData("invalid")]
    public void InitializerInputRejectsInvalidFill(string fill)
    {
        bool resolved = new GeneralMergeInitializerInput(
            "0x10",
            fill).TryResolve(
                out GeneralMergeOutputInitializer? initializer,
                out CompositionIssue? issue);

        Assert.False(resolved);
        Assert.Null(initializer);
        Assert.Equal(
            GeneralMergeInitializerIssueCodes.FillByteInvalid,
            issue!.Code);
    }

    /// <summary>The shared inline codec accepts only complete hexadecimal byte pairs.</summary>
    [Theory]
    [InlineData(null, false, 0)]
    [InlineData(" ", false, 0)]
    [InlineData("A-", false, 0)]
    [InlineData("GG", false, 0)]
    [InlineData("AA-BB,cc_dd ", true, 4)]
    public void InlineCodecMeasuresAndParsesCanonicalText(
        string? value,
        bool expectedSuccess,
        long expectedByteCount)
    {
        bool measured = GeneralInlineSourceCodec.TryMeasure(value, out long byteCount);
        bool parsed = GeneralInlineSourceCodec.TryParse(value, out byte[]? bytes);

        Assert.Equal(expectedSuccess, measured);
        Assert.Equal(expectedSuccess, parsed);
        Assert.Equal(expectedByteCount, byteCount);
        Assert.Equal(expectedSuccess ? expectedByteCount : 0, bytes?.LongLength ?? 0);
    }

    /// <summary>Closed mapping values and file-only presets fail at the typed row boundary.</summary>
    [Fact]
    public void MappingRowRejectsInvalidClosedValuesAndPresets()
    {
        _ = Assert.Throws<ArgumentException>(() => GeneralMappingSource.HexOverwrite(" "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateRow(
            (ExplicitMappingOperationKind)int.MaxValue,
            GeneralMappingSource.File("input.bin")));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("input.bin"),
            overlapPolicy: (OverlapPolicy)int.MaxValue));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("input.bin"),
            alignment: 0));
        _ = Assert.Throws<ArgumentException>(() => CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("input.bin"),
            targetStart: 1,
            alignment: 2));
        _ = Assert.Throws<ArgumentException>(() => CreateRow(
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexOverwrite("AA"),
            sourceStart: 1));
        _ = Assert.Throws<ArgumentException>(() => CreateRow(
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexFill("AA"),
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart));
        _ = Assert.Throws<ArgumentException>(() => CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("input.bin"),
            sourceStart: 1,
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralMappingDraftState([null!]));
    }

    /// <summary>Use-full-file-length requires one exact inspected From File Start row.</summary>
    [Fact]
    public void MaterializeFullFileLengthFailsClosedAndUpdatesOnlyTheSelectedRow()
    {
        GeneralMappingDraftRow sourceSlice = CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("slice.bin", FileStamp.FromBytes([0x01])),
            sourceStart: 1,
            fileRangePreset: GeneralMappingFileRangePreset.SourceSlice,
            mappingId: "slice");
        GeneralMappingDraftRow uninspected = CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("pending.bin"),
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart,
            mappingId: "pending");
        GeneralMappingDraftRow empty = CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("empty.bin", FileStamp.FromBytes([])),
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart,
            mappingId: "empty");
        GeneralMappingDraftRow accepted = CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("accepted.bin", FileStamp.FromBytes([1, 2, 3])),
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart,
            mappingId: "accepted");
        GeneralMappingDraftRow untouched = CreateRow(
            ExplicitMappingOperationKind.CopyRange,
            GeneralMappingSource.File("other.bin"),
            mappingId: "other");
        var draft = new GeneralMappingDraftState(
            [sourceSlice, uninspected, empty, accepted, untouched]);

        _ = Assert.Throws<ArgumentException>(() => draft.MaterializeFullFileLength("missing"));
        _ = Assert.Throws<InvalidOperationException>(() =>
            draft.MaterializeFullFileLength("slice"));
        _ = Assert.Throws<InvalidOperationException>(() =>
            draft.MaterializeFullFileLength("pending"));
        _ = Assert.Throws<InvalidOperationException>(() =>
            draft.MaterializeFullFileLength("empty"));

        GeneralMappingDraftState materialized =
            draft.MaterializeFullFileLength("accepted");

        Assert.Equal(3, materialized.Rows.Single(row =>
            row.MappingId == "accepted").SourceRange.Length);
        Assert.Same(
            untouched,
            materialized.Rows.Single(row => row.MappingId == "other"));
    }

    private static GeneralMappingDraftRow CreateRow(
        ExplicitMappingOperationKind operationKind,
        GeneralMappingSource source,
        OverlapPolicy overlapPolicy = OverlapPolicy.Reject,
        int alignment = 1,
        long sourceStart = 0,
        long targetStart = 0,
        GeneralMappingFileRangePreset? fileRangePreset = null,
        string mappingId = "mapping")
    {
        return new GeneralMappingDraftRow(
            mappingId,
            operationKind,
            source,
            new ByteRange(sourceStart, 1),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(targetStart, 1),
            overlapPolicy,
            alignment,
            "Test mapping.",
            fileRangePreset: fileRangePreset);
    }

    private static GeneralMappingDraftRow FileRow(
        string mappingId,
        ExplicitMappingOperationKind operationKind,
        ByteRange sourceRange,
        ByteRange targetRange)
    {
        return new GeneralMappingDraftRow(
            mappingId,
            operationKind,
            GeneralMappingSource.File("dp.bin"),
            sourceRange,
            CompositionAddressSpaceIds.OutputImage,
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            "Copy explicit mapping.");
    }
}
