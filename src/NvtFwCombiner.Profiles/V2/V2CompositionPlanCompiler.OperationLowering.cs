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
        foreach (CompositionProfileOperation operation in profile.Operations)
        {
            if (activeOperationIds is not null && !activeOperationIds.Contains(operation.OperationId))
            {
                continue;
            }

            if (!TryResolveSequence(operation, issues, out int sequence))
            {
                continue;
            }

            switch (operation)
            {
                case CopyOrReplaceProfileOperation copy:
                    LowerCopyOrReplaceOperation(profile, copy, sequence, views, regionAccess, operations, issues);
                    break;
                case FillRangeProfileOperation fill:
                    LowerFillOperation(fill, sequence, views, regionAccess, operations, issues);
                    break;
                case PatchScalarProfileOperation patch:
                    LowerPatchOperation(patch, sequence, views, regionAccess, operations, issues);
                    break;
                case TransformScalarProfileOperation transform:
                    LowerTransformOperation(
                        transform,
                        sequence,
                        resolvedMap,
                        views,
                        regionAccess,
                        operations,
                        issues);
                    break;
                case RunProcessorProfileOperation processor:
                    LowerProcessorOperation(
                        profile,
                        processor,
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

    private static void ValidateOperationOverlaps(
        IReadOnlyList<CompositionOperation> operations,
        List<CompositionIssue> issues)
    {
        var priorWrites = new List<CompositionOperation>();
        foreach (CompositionOperation operation in operations.OrderBy(static operation => operation.Sequence).ThenBy(static operation => operation.OperationId, StringComparer.Ordinal))
        {
            ByteRange[] writeRanges = GetDeclaredWriteRanges(operation);
            CompositionOperation[] overlaps = [.. priorWrites.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.TargetSpaceId, operation.TargetSpaceId) &&
                GetDeclaredWriteRanges(candidate).Any(candidateRange =>
                    writeRanges.Any(writeRange => candidateRange.Overlaps(writeRange))))];
            if (overlaps.Length == 0)
            {
                if (operation.OverlapPolicy == OverlapPolicy.ReplaceExisting)
                {
                    issues.Add(new CompositionIssue(
                        OperationOverlap,
                        $"Operation '{operation.OperationId}' declares ReplaceExisting but has no earlier write covering its target range in target space '{operation.TargetSpaceId}'.",
                        operation.OperationId));
                    return;
                }
            }
            else if (operation.OverlapPolicy != OverlapPolicy.ReplaceExisting)
            {
                CompositionOperation prior = overlaps[0];
                issues.Add(new CompositionIssue(
                    OperationOverlap,
                    $"Operation '{operation.OperationId}' overlaps earlier operation '{prior.OperationId}' in target space '{operation.TargetSpaceId}'.",
                    operation.OperationId));
                return;
            }
            else if (operation.Kind is not (CompositionOperationKind.CopyRange or CompositionOperationKind.RunExternalProcessor) ||
                     !writeRanges.All(writeRange => overlaps.Any(candidate =>
                         GetDeclaredWriteRanges(candidate).Any(candidateRange => candidateRange.Contains(writeRange)))))
            {
                issues.Add(new CompositionIssue(
                    OperationOverlap,
                    $"Operation '{operation.OperationId}' declares ReplaceExisting but no earlier write fully covers its target range in target space '{operation.TargetSpaceId}'.",
                    operation.OperationId));
                return;
            }

            priorWrites.Add(operation);
        }
    }

    private static ByteRange[] GetDeclaredWriteRanges(CompositionOperation operation)
    {
        return operation.Kind == CompositionOperationKind.RunExternalProcessor
            ? [.. operation.ExternalProcessorInvocation!.AllowedWriteRanges]
            : [operation.TargetRange];
    }

    private static void LowerCopyOrReplaceOperation(
        CompositionProfileDefinition profile,
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

        if (profile.CompositionKind == CompositionKind.Replace &&
            StringComparer.Ordinal.Equals(profile.ExperienceId, ExperienceIds.DpReplace) &&
            operation.Kind == CompositionOperationKind.ReplaceRange &&
            !TryAuthorizeDpReplacePayloadTarget(profile, operation, target, issues))
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

        operations.Add(operation.Kind switch
        {
            CompositionOperationKind.CopyRange => CompositionOperation.CopyRange(
                operation.OperationId,
                sequence,
                source.SpaceId,
                source.Range,
                target.SpaceId,
                target.Range,
                operation.OverlapPolicy,
                operation.Reason),
            CompositionOperationKind.ReplaceRange => CompositionOperation.ReplaceRange(
                operation.OperationId,
                sequence,
                source.SpaceId,
                source.Range,
                target.SpaceId,
                target.Range,
                operation.OverlapPolicy,
                operation.Reason),
            CompositionOperationKind.FillRange or
            CompositionOperationKind.PatchScalar or
            CompositionOperationKind.TransformScalar or
            CompositionOperationKind.RunExternalProcessor => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation.Kind,
                "Copy-like lowering requires a copy-range or replace-range operation."),
            _ => throw new InvalidOperationException("Validated V2 lowering encountered an unsupported copy-like operation."),
        });
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
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
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
        RunProcessorProfileOperation operation,
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
                operation.Width,
                operation.ByteOrder,
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
        TransformScalarProfileOperation operation,
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
                        out FirmwareRegionSet? sourceSet,
                        out FirmwareRegionInstance? source,
                        out error) ||
                    sourceSet is null ||
                    source is null)
                {
                    addend = default;
                    addendSource = null;
                    return false;
                }

                if (!TryResolveRegionInstance(
                        resolvedMap,
                        operation.AddendSource.TargetRegionInstanceId!,
                        out FirmwareRegionSet? targetSet,
                        out FirmwareRegionInstance? target,
                        out error) ||
                    targetSet is null ||
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

                if (!StringComparer.Ordinal.Equals(sourceSet.AddressSpaceId, targetSet.AddressSpaceId))
                {
                    addend = default;
                    addendSource = null;
                    error = $"region instances '{operation.AddendSource.SourceRegionInstanceId}' and " +
                        $"'{operation.AddendSource.TargetRegionInstanceId}' use incompatible address spaces";
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
