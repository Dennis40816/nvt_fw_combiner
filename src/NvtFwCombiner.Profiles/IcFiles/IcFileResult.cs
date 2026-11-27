// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Profiles.IcFiles;

internal readonly record struct SourceMark(int Line, int Column);

internal enum IcFileErrorCode
{
    EmptyDocument,
    InvalidIndentation,
    TabIndentation,
    DuplicateKey,
    AnchorNotAllowed,
    AliasNotAllowed,
    TagNotAllowed,
    MultipleDocuments,
    MultilineScalarNotAllowed,
    UnsupportedSyntax,
    InvalidScalar,
    InvalidFlowSequence,
    NestingTooDeep,
    InvalidHex,
    UnsignedOverflow,
    RangeOverflow,
    EmptyRange,
    EndBeforeStart,
    MissingAddressSpace,
    DuplicateId,
    DanglingReference,
    RegionOutsideLayout,
    RegionOutsideParent,
    RegionPartitionDiscontinuity,
    RegionPartitionIncomplete,
    RegionParentCycle,
    InvalidCondition,
    MissingFact,
    NoVariant,
    AmbiguousVariant,
    UnsupportedStepKind,
    InvalidStepFields,
}

internal sealed record IcFileError(
    IcFileErrorCode Code,
    SourceMark Mark,
    string? Subject = null,
    string? RelatedSubject = null);

internal abstract record IcFileResult<T>
{
    private IcFileResult() { }

    internal sealed record Success(T Value) : IcFileResult<T>;

    internal sealed record Failure(IcFileError Error) : IcFileResult<T>;
}
