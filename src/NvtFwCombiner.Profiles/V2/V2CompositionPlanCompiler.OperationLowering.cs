using System.Numerics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static CompositionOperation[] LowerOperations(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyDictionary<string, AddressSpace> spaces,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionIssue> issues,
        bool useProcessorWriteAuthority = false,
        IReadOnlySet<string>? activeOperationIds = null)
    {
        var operations = new List<CompositionOperation>();
        string? replaceReferenceSourceSpaceId = profile.CompositionKind == CompositionKind.Replace
            ? ResolveCloneReferenceSourceSpaceId(profile)
            : null;
        foreach (CompositionOperationDefinition operation in profile.Operations)
        {
            if (!useProcessorWriteAuthority &&
                !IsAdmittedOperation(profile, operation, replaceReferenceSourceSpaceId))
            {
                AddUnsupported(
                    issues,
                    $"operation '{operation.OperationId}' is outside the closed Merge or reference-clone Replace operation subset",
                    operation.OperationId);
                continue;
            }

            if (activeOperationIds is not null && !activeOperationIds.Contains(operation.OperationId))
            {
                continue;
            }

            if (!TryResolveSequence(operation, issues, out int sequence))
            {
                continue;
            }

            switch (operation.Kind)
            {
                case CompositionOperationKind.CopyRange:
                case CompositionOperationKind.ReplaceRange:
                    LowerCopyOrReplaceOperation(profile, operation, sequence, views, regionAccess, operations, issues);
                    break;
                case CompositionOperationKind.FillRange:
                    LowerFillOperation(operation, sequence, views, regionAccess, operations, issues);
                    break;
                case CompositionOperationKind.PatchScalar:
                    LowerPatchOperation(operation, sequence, views, regionAccess, operations, issues);
                    break;
                case CompositionOperationKind.TransformScalar:
                    LowerTransformOperation(
                        operation,
                        sequence,
                        resolvedMap,
                        views,
                        regionAccess,
                        operations,
                        issues);
                    break;
                case CompositionOperationKind.RunExternalProcessor:
                    LowerProcessorOperation(
                        profile,
                        operation,
                        sequence,
                        spaces,
                        views,
                        regionAccess,
                        operations,
                        issues,
                        useProcessorWriteAuthority);
                    break;
                default:
                    throw new InvalidOperationException("Validated V2 lowering encountered an unsupported operation shape.");
            }
        }

        return [.. operations];
    }

    private static bool IsAdmittedOperation(
        CompositionProfileDefinition profile,
        CompositionOperationDefinition operation,
        string? replaceReferenceSourceSpaceId)
    {
        return profile.CompositionKind == CompositionKind.Merge
            ? operation.Kind switch
            {
                CompositionOperationKind.CopyRange or
                    CompositionOperationKind.RunExternalProcessor =>
                    operation.OverlapPolicy is OverlapPolicy.Reject or OverlapPolicy.ReplaceExisting,
                CompositionOperationKind.FillRange or
                    CompositionOperationKind.PatchScalar or
                    CompositionOperationKind.TransformScalar =>
                    operation.OverlapPolicy == OverlapPolicy.Reject,
                CompositionOperationKind.ReplaceRange => false,
                _ => throw new InvalidOperationException("Unknown canonical operation kind."),
            }
            : operation.Kind switch
            {
                CompositionOperationKind.ReplaceRange =>
                    operation.OverlapPolicy == OverlapPolicy.Reject &&
                    IsReplacePayloadInputSource(profile, operation),
                CompositionOperationKind.RunExternalProcessor =>
                    operation.OverlapPolicy == OverlapPolicy.Reject,
                CompositionOperationKind.CopyRange =>
                    operation.OverlapPolicy == OverlapPolicy.ReplaceExisting &&
                    StringComparer.Ordinal.Equals(
                        replaceReferenceSourceSpaceId,
                        profile.Views.Single(view => StringComparer.Ordinal.Equals(
                            view.ViewId,
                            operation.SourceViewId)).SpaceId),
                CompositionOperationKind.FillRange or
                    CompositionOperationKind.PatchScalar or
                    CompositionOperationKind.TransformScalar => false,
                _ => throw new InvalidOperationException("Unknown canonical operation kind."),
            };
    }

    private static void ValidateOperationOverlaps(
        IReadOnlyList<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        var priorWrites = new List<CompositionOperation>();
        foreach (CompositionOperation operation in operations.OrderBy(static operation => operation.Sequence).ThenBy(static operation => operation.OperationId, StringComparer.Ordinal))
        {
            string? error = operation.GetProfileOverlapError(priorWrites);
            if (error is not null)
            {
                issues.Add(new CompositionIssue(
                    OperationOverlap,
                    error,
                    operation.OperationId));
                return;
            }

            priorWrites.Add(operation);
        }
    }

    private static void LowerCopyOrReplaceOperation(
        CompositionProfileDefinition profile,
        CompositionOperationDefinition operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        ResolvedView source = views[operation.SourceViewId];
        ResolvedView target = views[operation.TargetViewId];

        if (source.Range.Length != target.Range.Length)
        {
            issues.Add(new CompositionIssue(
                CopyLengthMismatch,
                $"Operation '{operation.OperationId}' source and target views have different lengths.",
                operation.OperationId));
            return;
        }

        if (profile.CompositionKind == CompositionKind.Replace &&
            operation.Kind == CompositionOperationKind.ReplaceRange &&
            !TryAuthorizeReplacePayloadTarget(profile, operation, target, issues))
        {
            return;
        }

        if (profile.CompositionKind == CompositionKind.Replace &&
            operation.Kind == CompositionOperationKind.CopyRange &&
            operation.OverlapPolicy == OverlapPolicy.ReplaceExisting &&
            source.Range != target.Range)
        {
            AddUnsupported(
                issues,
                $"reference restore operation '{operation.OperationId}' must use one identical resolved source and target range.",
                operation.OperationId);
            return;
        }

        if (!TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
        {
            return;
        }

        operations.Add(operation.Kind == CompositionOperationKind.CopyRange
            ? CompositionOperation.CopyRange(
                operation.OperationId,
                sequence,
                source.SpaceId,
                source.Range,
                target.SpaceId,
                target.Range,
                operation.OverlapPolicy,
                operation.Reason)
            : CompositionOperation.ReplaceRange(
                operation.OperationId,
                sequence,
                source.SpaceId,
                source.Range,
                target.SpaceId,
                target.Range,
                operation.OverlapPolicy,
                operation.Reason));
    }

    private static void LowerFillOperation(
        CompositionOperationDefinition operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        ResolvedView target = views[operation.TargetViewId];
        if (!TryAuthorizeTargetWrite(operation.OperationId, operation.TargetViewId, target, regionAccess, issues))
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
        CompositionOperationDefinition operation,
        int sequence,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        ResolvedView target = views[operation.TargetViewId];

        if (operation.PatchBytes.Length != target.Range.Length)
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
            operation.PatchBytes.Bytes.ToArray(),
            OverlapPolicy.Reject,
            operation.Reason));
    }

    private static void LowerTransformOperation(
        CompositionOperationDefinition operation,
        int sequence,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        ResolvedView source = views[operation.SourceViewId];
        ResolvedView target = views[operation.TargetViewId];

        if (source.Range.Length != target.Range.Length)
        {
            AddOperationLengthMismatch(operation.OperationId, "scalar source and target views have different lengths", issues);
            return;
        }

        if (!TryCreateScalarTransform(
                operation,
                resolvedMap,
                out ScalarTransform? transform,
                out string? transformError) ||
            transform is null)
        {
            issues.Add(new CompositionIssue(
                InvalidScalarTransform,
                $"Operation '{operation.OperationId}' scalar transform is invalid: {transformError}",
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

    private static void LowerProcessorOperation(
        CompositionProfileDefinition profile,
        CompositionOperationDefinition operation,
        int sequence,
        IReadOnlyDictionary<string, AddressSpace> spaces,
        IReadOnlyDictionary<string, ResolvedView> views,
        LoweredRegionAccess regionAccess,
        List<CompositionOperation> operations,
        List<CompositionIssue> issues,
        bool useProcessorWriteAuthority)
    {
        CompositionProfileProcessorStage stage = profile.ProcessorStages.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.ProcessorStageId, operation.ProcessorStageId));
        if (stage is not LegacyCombinerProfileProcessorStage legacy)
        {
            AddUnsupported(
                issues,
                $"processor stage '{operation.ProcessorStageId}' is not an approved legacy-combiner transform",
                operation.OperationId);
            return;
        }

        AddressSpace targetSpace = spaces[legacy.TargetSpaceId];
        ByteRange targetRange = legacy.TargetViewId is null
            ? new ByteRange(0, targetSpace.Length)
            : views[legacy.TargetViewId].Range;
        if (targetRange.Start != 0)
        {
            AddUnsupported(
                issues,
                $"processor stage '{operation.ProcessorStageId}' target view must be a zero-based image prefix",
                operation.OperationId);
            return;
        }
        foreach (string writeViewId in legacy.AllowedWriteViewIds)
        {
            bool authorized = useProcessorWriteAuthority
                ? TryAuthorizeProcessorWrite(
                    operation.OperationId,
                    writeViewId,
                    views[writeViewId],
                    regionAccess,
                    issues)
                : TryAuthorizeTargetWrite(
                    operation.OperationId,
                    writeViewId,
                    views[writeViewId],
                    regionAccess,
                    issues);
            if (!authorized)
            {
                return;
            }
        }

        var stagedSources = new List<ExternalProcessorStagedSourceBinding>();
        foreach (CompositionProfileStagedSourceBinding binding in legacy.StagedSourceBindings)
        {
            ResolvedView source = views[binding.SourceViewId];
            ResolvedView target = views[binding.TargetViewId];
            if (target.IsSourceOnly)
            {
                AddUnsupported(
                    issues,
                    $"processor stage '{operation.ProcessorStageId}' staged target " +
                    $"'{binding.TargetViewId}' cannot use a source-only selector",
                    operation.OperationId);
                return;
            }

            if (spaces[source.SpaceId].Mutability != AddressSpaceMutability.Immutable)
            {
                AddUnsupported(
                    issues,
                    $"processor stage '{operation.ProcessorStageId}' staged source '{binding.SourceViewId}' must be immutable",
                    operation.OperationId);
                return;
            }

            if (source.Range.Length != target.Range.Length)
            {
                AddOperationLengthMismatch(
                    operation.OperationId,
                    $"processor staged source '{binding.SourceViewId}' and target '{binding.TargetViewId}' have different lengths",
                    issues);
                return;
            }

            if (IsTruncatedCtrlRamInputSource(profile, source.SpaceId) &&
                !IsTpCtrlRamTarget(target))
            {
                AddUnsupported(
                    issues,
                    $"processor staged source '{binding.SourceViewId}' with CtrlRAM truncation must target a TP-owned CtrlRAM region",
                    operation.OperationId);
                return;
            }

            stagedSources.Add(new ExternalProcessorStagedSourceBinding(source.SpaceId, source.Range, target.Range));
        }

        ExternalProcessorStagedArtifactBinding[] stagedArtifacts = [.. legacy.StagedArtifactBindings.Select(binding =>
        {
            ResolvedView source = views[binding.SourceViewId];
            return new ExternalProcessorStagedArtifactBinding(binding.ArtifactId, source.SpaceId, source.Range);
        })];
        ByteRange[] allowedReadRanges = [.. legacy.AllowedReadViewIds.Select(viewId => views[viewId].Range)];
        ByteRange[] allowedWriteRanges = [.. legacy.AllowedWriteViewIds.Select(viewId => views[viewId].Range)];
        var invocation = new ExternalProcessorInvocation(
            legacy.InvocationProfileId,
            legacy.ToolBindingId,
            allowedReadRanges,
            allowedWriteRanges,
            stagedSources,
            stagedArtifactBindings: stagedArtifacts);
        operations.Add(CompositionOperation.RunExternalProcessor(
            operation.OperationId,
            sequence,
            targetSpace.AddressSpaceId,
            targetRange,
            invocation,
            operation.OverlapPolicy,
            operation.Reason));
    }

    private static bool IsTruncatedCtrlRamInputSource(
        CompositionProfileDefinition profile,
        string addressSpaceId)
    {
        InputArtifactProfileSpace? input = profile.Spaces.OfType<InputArtifactProfileSpace>().SingleOrDefault(space =>
            StringComparer.Ordinal.Equals(space.SpaceId, addressSpaceId));
        return input is not null &&
            profile.InputSlots.Single(slot => StringComparer.Ordinal.Equals(slot.SlotId, input.SlotId)).Normalization
                is CompiledTruncateCtrlRamInputNormalization;
    }

    private static bool IsTpCtrlRamTarget(ResolvedView target)
    {
        return target.GoverningRegionChain.Count != 0 &&
            target.GoverningRegionChain[^1] is
            {
                Owner: FirmwareRegionOwner.Tp,
                Kind: FirmwareRegionKind.CtrlRam,
            };
    }

    private static bool TryResolveSequence(
        CompositionOperationDefinition operation,
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
        CompositionOperationDefinition operation,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        out ScalarTransform? transform,
        out string? error)
    {
        if (!TryResolveTransformAddend(
                operation,
                resolvedMap,
                out BigInteger addend,
                out ScalarTransformAddendSource? addendSource,
                out error) ||
            addendSource is null)
        {
            transform = null;
            return false;
        }

        try
        {
            transform = new ScalarTransform(
                operation.TransformWidth,
                operation.TransformByteOrder,
                addend,
                operation.ExpectedBefore,
                ScalarTransformOverflowPolicy.Reject,
                addendSource);
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            transform = null;
            error = exception.Message;
            return false;
        }
    }

    private static bool TryResolveTransformAddend(
        CompositionOperationDefinition operation,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        out BigInteger addend,
        out ScalarTransformAddendSource? addendSource,
        out string? error)
    {
        switch (operation.AddendSource.Kind)
        {
            case ScalarTransformAddendSourceKind.Fixed:
                addend = operation.Addend;
                addendSource = operation.AddendSource;
                error = null;
                return true;
            case ScalarTransformAddendSourceKind.RegionInstanceDelta:
                if (!TryResolveRegionInstance(
                        resolvedMap,
                        operation.AddendSource.SourceRegionInstanceId!,
                        out FirmwareRegionInstance? source,
                        out error) ||
                    source is null)
                {
                    addend = default;
                    addendSource = null;
                    return false;
                }

                if (!TryResolveRegionInstance(
                        resolvedMap,
                        operation.AddendSource.TargetRegionInstanceId!,
                        out FirmwareRegionInstance? target,
                        out error) ||
                    target is null)
                {
                    addend = default;
                    addendSource = null;
                    return false;
                }

                if (!ReferenceEquals(source.Template, target.Template))
                {
                    addend = default;
                    addendSource = null;
                    error = $"region instances '{operation.AddendSource.SourceRegionInstanceId}' and " +
                        $"'{operation.AddendSource.TargetRegionInstanceId}' do not reference the same canonical template";
                    return false;
                }

                addend = new BigInteger(target.BaseOffset) - new BigInteger(source.BaseOffset);
                addendSource = operation.AddendSource;
                error = null;
                return true;
            default:
                throw new InvalidOperationException(
                    "Validated V2 lowering encountered an unknown transform addend source.");
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
