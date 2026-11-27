// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Numerics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies step Lowering Tests.</summary>
public sealed class StepLoweringTests
{
    private static readonly SourceMark Mark = new(4, 3);
    private static readonly ByteRange Source = new(8, 2);
    private static readonly ByteRange Target = new(0x20, 2);

    private static object[][] FactoryCases =>
    [
        [Copy(), CompositionOperation.CopyRange("synthetic-step", 7, "input-space", Source, "output-space", Target,
            OverlapPolicy.ReplaceExisting, "synthetic reason")],
        [Copy() with { Kind = CompositionOperationKind.ReplaceRange },
            CompositionOperation.ReplaceRange("synthetic-step", 7, "input-space", Source, "output-space", Target,
                OverlapPolicy.ReplaceExisting, "synthetic reason")],
        [Base(CompositionOperationKind.FillRange) with { FillByte = 0xA5 },
            CompositionOperation.FillRange("synthetic-step", 7, "output-space", Target, 0xA5,
                OverlapPolicy.ReplaceExisting, "synthetic reason")],
        [Base(CompositionOperationKind.PatchScalar) with { PatchBytes = [0x12, 0x34] },
            CompositionOperation.PatchScalar("synthetic-step", 7, "output-space", Target, [0x12, 0x34],
                OverlapPolicy.ReplaceExisting, "synthetic reason")],
        TransformCase(),
        ProcessorCase(),
    ];

    /// <summary>Provides factory Indexes.</summary>
    public static TheoryData<int> FactoryIndexes => [.. Enumerable.Range(0, FactoryCases.Length)];

    /// <summary>Provides invalid Field Indexes.</summary>
    public static TheoryData<int> InvalidFieldIndexes => [.. Enumerable.Range(0, InvalidFieldCases.Length)];

    /// <summary>Verifies each Primitive Preserves The Complete Domain Factory Contract.</summary>
    [Theory]
    [MemberData(nameof(FactoryIndexes))]
    public void EachPrimitivePreservesTheCompleteDomainFactoryContract(int caseIndex)
    {
        object[] factoryCase = FactoryCases[caseIndex];
        ResolvedStep resolved = (ResolvedStep)factoryCase[0];
        CompositionOperation expected = (CompositionOperation)factoryCase[1];
        CompositionOperation actual = IcFileAssertions.Success(StepLowering.Lower(resolved));

        Assert.Equal(expected.OperationId, actual.OperationId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.SourceSpaceId, actual.SourceSpaceId);
        Assert.Equal(expected.SourceRange, actual.SourceRange);
        Assert.Equal(expected.TargetSpaceId, actual.TargetSpaceId);
        Assert.Equal(expected.TargetRange, actual.TargetRange);
        Assert.Equal(expected.OverlapPolicy, actual.OverlapPolicy);
        Assert.Equal(expected.FillByte, actual.FillByte);
        Assert.Equal(expected.PatchBytes.ToArray(), actual.PatchBytes.ToArray());
        Assert.Same(expected.ScalarTransform, actual.ScalarTransform);
        Assert.Same(expected.ExternalProcessorInvocation, actual.ExternalProcessorInvocation);
        Assert.Equal(expected.DeclaredWriteRanges, actual.DeclaredWriteRanges);
        Assert.Equal(expected.Reason, actual.Reason);
        Assert.Equal(expected.Provenance, actual.Provenance);
    }

    private static ResolvedStep[] InvalidFieldCases =>
    [
        Copy() with { Id = " " },
        Copy() with { Sequence = -1 },
        Copy() with { Reason = " " },
        Copy() with { TargetSpaceId = " " },
        Copy() with { TargetRange = default },
        Copy() with { OverlapPolicy = (OverlapPolicy)99 },
        Copy() with { SourceSpaceId = null },
        Copy() with { SourceSpaceId = " " },
        Copy() with { SourceRange = null },
        Copy() with { SourceRange = default(ByteRange) },
        Copy() with { SourceRange = new ByteRange(8, 1) },
        Copy() with { FillByte = 0 },
        Copy() with { PatchBytes = [0] },
        Copy() with { PatchBytes = default },
        Copy() with { Transform = Transform() },
        Copy() with { Processor = Processor() },
        Base(CompositionOperationKind.FillRange),
        Base(CompositionOperationKind.FillRange) with { FillByte = 0, SourceSpaceId = "input-space" },
        Base(CompositionOperationKind.FillRange) with { FillByte = 0, SourceRange = Source },
        Base(CompositionOperationKind.PatchScalar) with { PatchBytes = [0] },
        Copy() with { Kind = CompositionOperationKind.TransformScalar },
        Copy() with { Kind = CompositionOperationKind.TransformScalar, Transform = Transform(), TargetRange = new ByteRange(0x20, 1), SourceRange = new ByteRange(8, 1) },
        Base(CompositionOperationKind.RunExternalProcessor),
        Base(CompositionOperationKind.RunExternalProcessor) with { Processor = Processor(), TargetRange = new ByteRange(0x20, 1) },
    ];

    /// <summary>Verifies unmapped Field Combinations Return Typed Errors.</summary>
    [Theory]
    [MemberData(nameof(InvalidFieldIndexes))]
    public void UnmappedFieldCombinationsReturnTypedErrors(int caseIndex)
    {
        ResolvedStep resolved = InvalidFieldCases[caseIndex];
        Assert.Equal(new IcFileError(IcFileErrorCode.InvalidStepFields, Mark, resolved.Id,
                resolved.TargetSpaceId),
            IcFileAssertions.Failure(StepLowering.Lower(resolved), IcFileErrorCode.InvalidStepFields));
    }

    /// <summary>Verifies unknown Primitive Kind Is ATyped Error.</summary>
    [Fact]
    public void UnknownPrimitiveKindIsATypedError()
    {
        _ = IcFileAssertions.Failure(StepLowering.Lower(Base((CompositionOperationKind)99)), IcFileErrorCode.UnsupportedStepKind);
    }

    private static ResolvedStep Base(CompositionOperationKind kind)
    {
        return new("synthetic-step", 7, kind, "output-space", Target, OverlapPolicy.ReplaceExisting, "synthetic reason", Mark);
    }

    private static ResolvedStep Copy()
    {
        return Base(CompositionOperationKind.CopyRange) with
        {
            SourceSpaceId = "input-space",
            SourceRange = Source,
        };
    }

    private static ScalarTransform Transform()
    {
        return new(
            ScalarTransformWidth.TwoBytes, ScalarTransformByteOrder.BigEndian, new BigInteger(3), 5,
            ScalarTransformOverflowPolicy.Reject);
    }

    private static ExternalProcessorInvocation Processor()
    {
        return new(
            "synthetic-processor", "synthetic-binding", [Target], [new ByteRange(0x21, 1)]);
    }

    private static object[] TransformCase()
    {
        ScalarTransform transform = Transform();
        return [Copy() with { Kind = CompositionOperationKind.TransformScalar, Transform = transform },
            CompositionOperation.TransformScalar("synthetic-step", 7, "input-space", Source, "output-space", Target,
                transform, OverlapPolicy.ReplaceExisting, "synthetic reason")];
    }

    private static object[] ProcessorCase()
    {
        ExternalProcessorInvocation processor = Processor();
        return [Base(CompositionOperationKind.RunExternalProcessor) with { Processor = processor },
            CompositionOperation.RunExternalProcessor("synthetic-step", 7, "output-space", Target, processor,
                OverlapPolicy.ReplaceExisting, "synthetic reason")];
    }
}
