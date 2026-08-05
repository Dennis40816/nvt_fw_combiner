using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static AddressSpace CreateInputAddressSpace(
        string addressSpaceId,
        long length,
        CompositionProfileInputSlot slot,
        long resolvedMapCapacity,
        CompositionKind compositionKind,
        bool isCloneSource)
    {
        return slot.LengthRule switch
        {
            SourceViewCoverageLengthRule sourceView => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                expectedInputLengths: ResolveSourceViewExpectedOuterLengths(sourceView, resolvedMapCapacity),
                unexpectedInputLengthIssueCode: sourceView.UnexpectedOuterLengthIssueCode),
            ExactBytesLengthRule when slot.Normalization is TruncateCtrlRamInputNormalization => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
            ExactBytesLengthRule => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable),
            ExactResolvedMapCapacityLengthRule when slot.Normalization is PadShorterInputNormalization padded => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable,
                inputPaddingByte: padded.FillByte),
            ExactResolvedMapCapacityLengthRule when isCloneSource ||
                                                   (compositionKind == CompositionKind.Replace &&
                                                    slot.ArtifactClass == CompositionProfileArtifactClass.ReferenceImage) => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable),
            _ => new AddressSpace(
                addressSpaceId,
                length,
                AddressSpaceMutability.Immutable,
                allowedInputLengths: [length]),
        };
    }

    private static bool IsCurrentInputLengthRuleSupported(CompositionProfileInputSlot slot)
    {
        return slot.LengthRule is ExactResolvedMapCapacityLengthRule or
            SourceViewCoverageLengthRule ||
            (slot.ArtifactClass == CompositionProfileArtifactClass.TpFirmware &&
             slot.LengthRule is ExactBytesLengthRule { Bytes: <= 262144 }) ||
            (slot.ArtifactClass == CompositionProfileArtifactClass.CtrlRamReplacement &&
             slot.LengthRule is ExactBytesLengthRule &&
             slot.Normalization is TruncateCtrlRamInputNormalization);
    }

    private static bool TryResolveInputSpaceLength(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        InputArtifactProfileSpace input,
        CompositionProfileInputSlot slot,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        out long length)
    {
        switch (slot.LengthRule)
        {
            case ExactBytesLengthRule exact:
                length = exact.Bytes;
                return true;
            case ExactResolvedMapCapacityLengthRule:
                length = resolvedMap.CapacityBytes;
                return true;
            case SourceViewCoverageLengthRule { RequiredEndExclusive: { } requiredEndExclusive }:
                length = requiredEndExclusive;
                return true;
            case SourceViewCoverageLengthRule sourceView:
                return TryResolveSourceViewSpan(
                    profile,
                    family,
                    input,
                    sourceView,
                    resolvedMap,
                    issues,
                    out length);
            default:
                throw new InvalidOperationException("Validated V2 lowering encountered an unsupported input length rule.");
        }
    }

    private static bool TryResolveSourceViewSpan(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        InputArtifactProfileSpace input,
        SourceViewCoverageLengthRule sourceView,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        out long length)
    {
        if (!TryResolveInputSourceSpan(
                profile,
                family,
                input,
                resolvedMap,
                sourceView.MaximumOuterLength is null ? "Section" : "TP",
                sourceView.MaximumOuterLength is null ? "source-view coverage" : "exact plan",
                issues,
                out length))
        {
            return false;
        }

        if (sourceView.MaximumOuterLength is { } maximumOuterLength &&
            length > maximumOuterLength)
        {
            issues.Add(new CompositionIssue(
                InvalidInputGeometry,
                maximumOuterLength == 262144 ? $"TP input space '{input.SpaceId}' requires source bytes through 0x{length:X}, exceeding the 256 KiB policy limit." : $"Input space '{input.SpaceId}' requires source bytes through 0x{length:X}, exceeding its declared maximum outer length 0x{maximumOuterLength:X}.",
                input.SpaceId));
            length = 0;
            return false;
        }

        return true;
    }

    private static IReadOnlyList<long>? ResolveSourceViewExpectedOuterLengths(
        SourceViewCoverageLengthRule rule,
        long resolvedMapCapacity)
    {
        return rule.ExpectedOuterLengths.Count == 0
            ? rule.UnexpectedOuterLengthIssueCode is null ? null : [resolvedMapCapacity]
            : rule.ExpectedOuterLengths;
    }

    private static bool TryResolveInputSourceSpan(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition family,
        InputArtifactProfileSpace input,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string inputKind,
        string spanDescription,
        List<CompositionIssue> issues,
        out long length)
    {
        long maximumEndExclusive = 0;
        bool hasRead = false;
        foreach (CompositionProfileView view in profile.Views.Where(view =>
                     StringComparer.Ordinal.Equals(view.SpaceId, input.SpaceId)))
        {
            if (!TryResolveSelectorRange(view, resolvedMap, out ByteRange range, out string? error))
            {
                issues.Add(new CompositionIssue(
                    InvalidInputGeometry,
                    $"{inputKind} input space '{input.SpaceId}' cannot resolve view '{view.ViewId}': {error}",
                    view.ViewId));
                length = 0;
                return false;
            }

            maximumEndExclusive = Math.Max(maximumEndExclusive, range.EndExclusive);
            hasRead = true;
        }

        foreach (CompositionProfileMetadataBinding binding in profile.MetadataBindings.Where(binding =>
                     StringComparer.Ordinal.Equals(binding.SpaceId, input.SpaceId)))
        {
            string? error = null;
            if (!family.TryResolveStructure(
                    resolvedMap.ImageMap.MapId,
                    binding.StructureId,
                    out FirmwareMetadataStructure? structure) ||
                structure is null ||
                !TryResolveMetadataReadEnd(
                    family,
                    resolvedMap,
                    structure,
                    new HashSet<string>(StringComparer.Ordinal),
                    out long metadataEnd,
                    out error))
            {
                issues.Add(new CompositionIssue(
                    InvalidInputGeometry,
                    $"{inputKind} input space '{input.SpaceId}' cannot resolve metadata binding " +
                    $"'{binding.BindingId}': {error ?? "the selected structure is unavailable."}",
                    binding.BindingId));
                length = 0;
                return false;
            }

            maximumEndExclusive = Math.Max(maximumEndExclusive, metadataEnd);
            hasRead = true;
        }

        foreach (MutableCompositionProfileSpace cloneTarget in profile.Spaces
                     .OfType<MutableCompositionProfileSpace>()
                     .Where(space => space.Initializer is CloneProfileInitializer clone &&
                         StringComparer.Ordinal.Equals(clone.SourceSlotId, input.SlotId)))
        {
            maximumEndExclusive = Math.Max(
                maximumEndExclusive,
                ResolveMutableSpaceCapacity(cloneTarget, resolvedMap.CapacityBytes));
            hasRead = true;
        }

        if (!hasRead)
        {
            issues.Add(new CompositionIssue(
                InvalidInputGeometry,
                $"{inputKind} input space '{input.SpaceId}' has no source view, metadata read, or clone " +
                $"initialization from which to derive its {spanDescription} span.",
                input.SpaceId));
            length = 0;
            return false;
        }

        length = maximumEndExclusive;
        return true;
    }

    private static bool TryResolveMetadataReadEnd(
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        FirmwareMetadataStructure structure,
        HashSet<string> visitedStructureIds,
        out long endExclusive,
        out string? error)
    {
        endExclusive = 0;
        error = null;
        if (!visitedStructureIds.Add(structure.StructureId))
        {
            error = $"metadata dependency cycle at '{structure.StructureId}'.";
            return false;
        }

        try
        {
            switch (structure.Locator)
            {
                case FirmwareAbsoluteRangeLocator absolute:
                    endExclusive = absolute.Range.Range.EndExclusive;
                    return true;
                case FirmwareRegionRelativeLocator relative:
                    FirmwareRegion? region = resolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(candidate.RegionId, relative.RegionId));
                    if (region is null)
                    {
                        error = $"unknown metadata base region '{relative.RegionId}'.";
                        return false;
                    }

                    endExclusive = checked(region.Range.Start + relative.Offset + structure.LengthBytes);
                    return true;
                case FirmwareMarkerRelativeLocator marker:
                    long maximumMarkerStart = checked(
                        marker.SearchRange.Range.EndExclusive - marker.MarkerBytes.Length);
                    long maximumResultStart = checked(maximumMarkerStart + marker.ResultOffset);
                    if (maximumResultStart < 0)
                    {
                        error = $"marker-relative structure '{structure.StructureId}' can resolve below zero.";
                        return false;
                    }

                    endExclusive = Math.Max(
                        marker.SearchRange.Range.EndExclusive,
                        checked(maximumResultStart + structure.LengthBytes));
                    return true;
                case FirmwareMetadataFieldSelectedLocator selected:
                    if (!family.TryResolveStructure(
                            resolvedMap.ImageMap.MapId,
                            selected.PrerequisiteStructureId,
                            out FirmwareMetadataStructure? prerequisite) ||
                        prerequisite is null ||
                        !TryResolveMetadataReadEnd(
                            family,
                            resolvedMap,
                            prerequisite,
                            visitedStructureIds,
                            out long prerequisiteEnd,
                            out error))
                    {
                        error ??= $"unknown metadata prerequisite '{selected.PrerequisiteStructureId}'.";
                        return false;
                    }

                    long maximumResultEnd = selected.Branches.Max(branch =>
                        checked(branch.AnchorRange.Range.Start + selected.ResultOffset + structure.LengthBytes));
                    if (maximumResultEnd < 0)
                    {
                        error = $"field-selected structure '{structure.StructureId}' can resolve below zero.";
                        return false;
                    }

                    endExclusive = Math.Max(prerequisiteEnd, maximumResultEnd);
                    return true;
                default:
                    error = $"unsupported locator for metadata structure '{structure.StructureId}'.";
                    return false;
            }
        }
        catch (OverflowException)
        {
            error = $"metadata structure '{structure.StructureId}' read range overflows.";
            return false;
        }
        finally
        {
            _ = visitedStructureIds.Remove(structure.StructureId);
        }
    }

    private static bool TryResolveSelectorRange(
        CompositionProfileView view,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        out ByteRange range,
        out string? error)
    {
        range = default;
        error = null;
        try
        {
            switch (view.Selector)
            {
                case MapRegionViewSelector regionSelector:
                    FirmwareRegion? region = resolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(candidate.RegionId, regionSelector.RegionId));
                    if (region is null)
                    {
                        error = $"View '{view.ViewId}' names unknown map region '{regionSelector.RegionId}'.";
                        return false;
                    }

                    range = region.Range;
                    return true;
                case MapRegionSliceViewSelector sliceSelector:
                    FirmwareRegion? slicedRegion = resolvedMap.ImageMap.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(candidate.RegionId, sliceSelector.RegionId));
                    if (slicedRegion is null)
                    {
                        error = $"View '{view.ViewId}' names unknown map region '{sliceSelector.RegionId}'.";
                        return false;
                    }

                    range = new ByteRange(
                        checked(slicedRegion.Range.Start + sliceSelector.RelativeRange.Start),
                        sliceSelector.RelativeRange.Length);
                    if (!slicedRegion.Range.Contains(range))
                    {
                        error = $"View '{view.ViewId}' escapes map region '{sliceSelector.RegionId}'.";
                        return false;
                    }

                    return true;
                case SpaceRangeViewSelector spaceSelector:
                    range = spaceSelector.Range;
                    return true;
                case RegionTemplateRangeViewSelector templateSelector:
                    if (!TryResolveRegionInstance(
                            resolvedMap,
                            templateSelector.RegionInstanceId,
                            out _,
                            out FirmwareRegionInstance? instance,
                            out string? instanceError) ||
                        instance is null)
                    {
                        error = $"View '{view.ViewId}' names {instanceError}.";
                        return false;
                    }

                    FirmwareRelativeRegion? relativeRegion = instance.Template.Regions.SingleOrDefault(candidate =>
                        StringComparer.Ordinal.Equals(
                            candidate.RegionId,
                            templateSelector.TemplateRegionId));
                    if (relativeRegion is null ||
                        !instance.ResolvedRegionIds.TryGetValue(
                            templateSelector.TemplateRegionId,
                            out string? resolvedRegionId) ||
                        !resolvedMap.ImageMap.Regions.Any(candidate => StringComparer.Ordinal.Equals(
                            candidate.RegionId,
                            resolvedRegionId)))
                    {
                        error = $"View '{view.ViewId}' names unknown template region " +
                            $"'{templateSelector.TemplateRegionId}' in instance " +
                            $"'{templateSelector.RegionInstanceId}'.";
                        return false;
                    }

                    range = relativeRegion.Range;
                    return true;
                default:
                    error = $"View '{view.ViewId}' uses an unsupported selector.";
                    return false;
            }
        }
        catch (OverflowException)
        {
            error = $"View '{view.ViewId}' range overflows its declared bounds.";
            return false;
        }
    }

    private static bool TryResolveRegionInstance(
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string instanceId,
        out FirmwareRegionSet? regionSet,
        out FirmwareRegionInstance? instance,
        out string? error)
    {
        (
            FirmwareRegionSet RegionSet,
            FirmwareRegionInstance Instance
        )[] matches =
        [
            .. resolvedMap.ImageMap.RegionSets
                .SelectMany(candidateSet => candidateSet.RegionInstances.Select(candidateInstance =>
                    (RegionSet: candidateSet, Instance: candidateInstance)))
                .Where(candidate => StringComparer.Ordinal.Equals(
                    candidate.Instance.InstanceId,
                    instanceId)),
        ];
        if (matches.Length == 0)
        {
            regionSet = null;
            instance = null;
            error = $"unknown region instance '{instanceId}'";
            return false;
        }

        if (matches.Length != 1)
        {
            regionSet = null;
            instance = null;
            error = $"ambiguous region instance '{instanceId}'";
            return false;
        }

        (regionSet, instance) = matches[0];
        if (!StringComparer.Ordinal.Equals(
                regionSet.AddressSpaceId,
                resolvedMap.ImageMap.AddressSpaceId))
        {
            regionSet = null;
            instance = null;
            error = $"region instance '{instanceId}' with an incompatible address space";
            return false;
        }

        error = null;
        return true;
    }
}
