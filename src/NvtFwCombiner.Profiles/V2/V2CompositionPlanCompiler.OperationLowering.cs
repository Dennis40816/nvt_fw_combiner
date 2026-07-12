using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static CompositionOperation[] LowerOperations(
        CompositionProfileDefinition profile,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionIssue> issues)
    {
        var operations = new List<CompositionOperation>();
        foreach (CompositionProfileOperation operation in profile.Operations)
        {
            if (!TryResolveSequence(operation, issues, out int sequence))
            {
                continue;
            }

            switch (operation)
            {
                case CopyOrReplaceProfileOperation copy:
                    LowerCopyOperation(copy, sequence, views, regionAccess, operations, issues);
                    break;
                case FillRangeProfileOperation fill:
                    LowerFillOperation(fill, sequence, views, regionAccess, operations, issues);
                    break;
                case PatchScalarProfileOperation patch:
                    LowerPatchOperation(patch, sequence, views, regionAccess, operations, issues);
                    break;
                case TransformScalarProfileOperation transform:
                    LowerTransformOperation(transform, sequence, views, regionAccess, operations, issues);
                    break;
                default:
                    throw new InvalidOperationException("Validated V2 lowering encountered an unsupported operation shape.");
            }
        }

        return [.. operations];
    }

    private static void ValidateRejectOperationOverlaps(
        IReadOnlyList<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        var priorWrites = new List<CompositionOperation>();
        foreach (CompositionOperation operation in operations.OrderBy(static operation => operation.Sequence).ThenBy(static operation => operation.OperationId, StringComparer.Ordinal))
        {
            CompositionOperation? prior = priorWrites.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.TargetSpaceId, operation.TargetSpaceId) &&
                candidate.TargetRange.Overlaps(operation.TargetRange));
            if (prior is not null)
            {
                issues.Add(new CompositionIssue(
                    OperationOverlap,
                    $"Operation '{operation.OperationId}' overlaps earlier operation '{prior.OperationId}' in target space '{operation.TargetSpaceId}'.",
                    operation.OperationId));
                return;
            }

            priorWrites.Add(operation);
        }
    }

    private static void LowerCopyOperation(
        CopyOrReplaceProfileOperation operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        if (!TryResolveSourceAndTarget(
                operation.OperationId,
                operation.SourceViewId,
                operation.TargetViewId,
                views,
                issues,
                out ResolvedView source,
                out ResolvedView target))
        {
            return;
        }

        if (source.Range.Length != target.Range.Length)
        {
            issues.Add(new CompositionIssue(
                CopyLengthMismatch,
                $"Operation '{operation.OperationId}' source and target views have different lengths.",
                operation.OperationId));
            return;
        }

        if (!TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
        {
            return;
        }

        operations.Add(CompositionOperation.CopyRange(
            operation.OperationId,
            sequence,
            source.SpaceId,
            source.Range,
            target.SpaceId,
            target.Range,
            OverlapPolicy.Reject,
            operation.Reason));
    }

    private static void LowerFillOperation(
        FillRangeProfileOperation operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        if (!TryResolveTarget(operation.OperationId, operation.TargetViewId, views, issues, out ResolvedView target) ||
            !TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
        {
            return;
        }

        operations.Add(CompositionOperation.FillRange(
            operation.OperationId,
            sequence,
            target.SpaceId,
            target.Range,
            operation.FillByte,
            OverlapPolicy.Reject,
            operation.Reason));
    }

    private static void LowerPatchOperation(
        PatchScalarProfileOperation operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        if (!TryResolveTarget(operation.OperationId, operation.TargetViewId, views, issues, out ResolvedView target))
        {
            return;
        }

        if (operation.Value.Length != target.Range.Length)
        {
            AddOperationLengthMismatch(operation.OperationId, "patch bytes and target view have different lengths", issues);
            return;
        }

        if (!TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
        {
            return;
        }

        operations.Add(CompositionOperation.PatchScalar(
            operation.OperationId,
            sequence,
            target.SpaceId,
            target.Range,
            operation.Value.Bytes.ToArray(),
            OverlapPolicy.Reject,
            operation.Reason));
    }

    private static void LowerTransformOperation(
        TransformScalarProfileOperation operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        if (!TryResolveSourceAndTarget(
                operation.OperationId,
                operation.SourceViewId,
                operation.TargetViewId,
                views,
                issues,
                out ResolvedView source,
                out ResolvedView target))
        {
            return;
        }

        if (source.Range.Length != target.Range.Length)
        {
            AddOperationLengthMismatch(operation.OperationId, "scalar source and target views have different lengths", issues);
            return;
        }

        if (!TryCreateScalarTransform(operation, out ScalarTransform? transform) || transform is null)
        {
            issues.Add(new CompositionIssue(
                InvalidScalarTransform,
                $"Operation '{operation.OperationId}' scalar addend or expected value cannot be represented at its declared width.",
                operation.OperationId));
            return;
        }

        if (source.Range.Length != transform.WidthBytes)
        {
            issues.Add(new CompositionIssue(
                ScalarWidthMismatch,
                $"Operation '{operation.OperationId}' scalar width does not match its source and target views.",
                operation.OperationId));
            return;
        }

        if (!TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
        {
            return;
        }

        operations.Add(CompositionOperation.TransformScalar(
            operation.OperationId,
            sequence,
            source.SpaceId,
            source.Range,
            target.SpaceId,
            target.Range,
            transform,
            OverlapPolicy.Reject,
            operation.Reason));
    }

    private static bool TryResolveSourceAndTarget(
        string operationId,
        string sourceViewId,
        string targetViewId,
        IReadOnlyDictionary<string, ResolvedView> views,
        List<CompositionIssue> issues,
        out ResolvedView source,
        out ResolvedView target)
    {
        if (!views.TryGetValue(sourceViewId, out ResolvedView? resolvedSource) || resolvedSource is null ||
            !views.TryGetValue(targetViewId, out ResolvedView? resolvedTarget) || resolvedTarget is null)
        {
            issues.Add(new CompositionIssue(InvalidView, $"Operation '{operationId}' references an unresolved view.", operationId));
            source = null!;
            target = null!;
            return false;
        }

        source = resolvedSource;
        target = resolvedTarget;
        return true;
    }

    private static bool TryResolveTarget(
        string operationId,
        string targetViewId,
        IReadOnlyDictionary<string, ResolvedView> views,
        List<CompositionIssue> issues,
        out ResolvedView target)
    {
        if (!views.TryGetValue(targetViewId, out ResolvedView? resolvedTarget) || resolvedTarget is null)
        {
            issues.Add(new CompositionIssue(InvalidView, $"Operation '{operationId}' references an unresolved view.", operationId));
            target = null!;
            return false;
        }

        target = resolvedTarget;
        return true;
    }

    private static bool TryResolveSequence(
        CompositionProfileOperation operation,
        List<CompositionIssue> issues,
        out int sequence)
    {
        sequence = 0;
        if (operation.Sequence > int.MaxValue)
        {
            AddUnsupported(issues, $"operation '{operation.OperationId}' sequence exceeds Int32", operation.OperationId);
            return false;
        }

        sequence = (int)operation.Sequence;
        return true;
    }

    private static bool TryCreateScalarTransform(
        TransformScalarProfileOperation operation,
        out ScalarTransform? transform)
    {
        try
        {
            transform = new ScalarTransform(
                operation.Width switch
                {
                    CompositionProfileScalarWidth.OneByte => ScalarTransformWidth.OneByte,
                    CompositionProfileScalarWidth.TwoBytes => ScalarTransformWidth.TwoBytes,
                    CompositionProfileScalarWidth.FourBytes => ScalarTransformWidth.FourBytes,
                    CompositionProfileScalarWidth.EightBytes => ScalarTransformWidth.EightBytes,
                    _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown scalar width."),
                },
                operation.ByteOrder switch
                {
                    CompositionProfileScalarByteOrder.LittleEndian => ScalarTransformByteOrder.LittleEndian,
                    CompositionProfileScalarByteOrder.BigEndian => ScalarTransformByteOrder.BigEndian,
                    _ => throw new InvalidOperationException("Validated V2 lowering encountered an unknown scalar byte order."),
                },
                operation.Addend,
                operation.ExpectedBefore,
                ScalarTransformOverflowPolicy.Reject);
            return true;
        }
        catch (ArgumentException)
        {
            transform = null;
            return false;
        }
    }

    private static void AddOperationLengthMismatch(string operationId, string detail, List<CompositionIssue> issues)
    {
        issues.Add(new CompositionIssue(
            OperationLengthMismatch,
            $"Operation '{operationId}' {detail}.",
            operationId));
    }
}
