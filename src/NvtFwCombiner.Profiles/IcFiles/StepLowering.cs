// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.IcFiles;

// A resolved input to the factory projection, not another executable operation model.
internal sealed record ResolvedStep(
    string Id,
    int Sequence,
    CompositionOperationKind Kind,
    string TargetSpaceId,
    ByteRange TargetRange,
    OverlapPolicy OverlapPolicy,
    string Reason,
    SourceMark Mark)
{
    internal string? SourceSpaceId { get; init; }

    internal ByteRange? SourceRange { get; init; }

    internal byte? FillByte { get; init; }

    internal ImmutableArray<byte> PatchBytes { get; init; } = [];

    internal ScalarTransform? Transform { get; init; }

    internal ExternalProcessorInvocation? Processor { get; init; }
}

internal static class StepLowering
{
    internal static IcFileResult<CompositionOperation> Lower(ResolvedStep step)
    {
        if (!Enum.IsDefined(step.Kind))
        {
            return Failure(step, IcFileErrorCode.UnsupportedStepKind);
        }

        if (string.IsNullOrWhiteSpace(step.Id) || step.Sequence < 0 || string.IsNullOrWhiteSpace(step.Reason) ||
            string.IsNullOrWhiteSpace(step.TargetSpaceId) || step.TargetRange.Length <= 0 ||
            !Enum.IsDefined(step.OverlapPolicy) || !HasMappedFields(step))
        {
            return Failure(step, IcFileErrorCode.InvalidStepFields);
        }

        CompositionOperation operation = step.Kind switch
        {
            CompositionOperationKind.CopyRange => CompositionOperation.CopyRange(
                step.Id, step.Sequence, step.SourceSpaceId!, step.SourceRange!.Value,
                step.TargetSpaceId, step.TargetRange, step.OverlapPolicy, step.Reason),
            CompositionOperationKind.ReplaceRange => CompositionOperation.ReplaceRange(
                step.Id, step.Sequence, step.SourceSpaceId!, step.SourceRange!.Value,
                step.TargetSpaceId, step.TargetRange, step.OverlapPolicy, step.Reason),
            CompositionOperationKind.FillRange => CompositionOperation.FillRange(
                step.Id, step.Sequence, step.TargetSpaceId, step.TargetRange,
                step.FillByte!.Value, step.OverlapPolicy, step.Reason),
            CompositionOperationKind.PatchScalar => CompositionOperation.PatchScalar(
                step.Id, step.Sequence, step.TargetSpaceId, step.TargetRange,
                step.PatchBytes, step.OverlapPolicy, step.Reason),
            CompositionOperationKind.TransformScalar => CompositionOperation.TransformScalar(
                step.Id, step.Sequence, step.SourceSpaceId!, step.SourceRange!.Value,
                step.TargetSpaceId, step.TargetRange, step.Transform!, step.OverlapPolicy, step.Reason),
            CompositionOperationKind.RunExternalProcessor => CompositionOperation.RunExternalProcessor(
                step.Id, step.Sequence, step.TargetSpaceId, step.TargetRange,
                step.Processor!, step.OverlapPolicy, step.Reason),
            _ => throw new InvalidOperationException("A validated operation kind has no factory."),
        };
        return new IcFileResult<CompositionOperation>.Success(operation);
    }

    private static bool HasMappedFields(ResolvedStep step)
    {
        bool requiresSource = step.Kind is CompositionOperationKind.CopyRange or
            CompositionOperationKind.ReplaceRange or CompositionOperationKind.TransformScalar;
        bool validSource = requiresSource
            ? !string.IsNullOrWhiteSpace(step.SourceSpaceId) && step.SourceRange is { Length: > 0 } source &&
                source.Length == step.TargetRange.Length
            : step.SourceSpaceId is null && step.SourceRange is null;
        return validSource && !step.PatchBytes.IsDefault &&
            (step.Kind == CompositionOperationKind.FillRange || step.FillByte is null) &&
            (step.Kind == CompositionOperationKind.PatchScalar || step.PatchBytes.Length == 0) &&
            (step.Kind == CompositionOperationKind.TransformScalar || step.Transform is null) &&
            (step.Kind == CompositionOperationKind.RunExternalProcessor || step.Processor is null) &&
            (step.Kind switch
            {
                CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange => true,
                CompositionOperationKind.FillRange => step.FillByte is not null,
                CompositionOperationKind.PatchScalar => step.PatchBytes.Length == step.TargetRange.Length,
                CompositionOperationKind.TransformScalar => step.Transform is { } transform && transform.WidthBytes == step.TargetRange.Length,
                CompositionOperationKind.RunExternalProcessor => step.Processor is { } processor &&
                    processor.AllowedReadRanges.All(step.TargetRange.Contains) &&
                    processor.AllowedWriteRanges.All(step.TargetRange.Contains),
                _ => false,
            });
    }

    private static IcFileResult<CompositionOperation>.Failure Failure(ResolvedStep step, IcFileErrorCode code)
    {
        return new(new IcFileError(code, step.Mark, step.Id, step.TargetSpaceId));
    }
}
