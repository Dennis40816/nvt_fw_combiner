using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static bool IsCurrentInputLengthRuleSupported(CompositionProfileInputSlot slot)
    {
        return slot.LengthRule is ExactResolvedMapCapacityLengthRule or NormalDpExtractWithWarningLengthRule ||
            (slot.ArtifactClass == CompositionProfileArtifactClass.TpFirmware &&
             (slot.LengthRule is TpMaximum256KLengthRule ||
              slot.LengthRule is ExactBytesLengthRule { Bytes: <= TpMaximum256KLengthRule.MaximumBytes })) ||
            (slot.ArtifactClass == CompositionProfileArtifactClass.CtrlRamReplacement &&
             slot.LengthRule is ExactBytesLengthRule &&
             slot.Normalization is TruncateCtrlRamInputNormalization);
    }

    private static bool TryResolveInputSpaceLength(
        CompositionProfileDefinition profile,
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
            case TpMaximum256KLengthRule:
                return TryResolveTpSourceSpan(profile, input, resolvedMap, issues, out length);
            case NormalDpExtractWithWarningLengthRule:
                return TryResolveNormalDpSourceSpan(profile, input, resolvedMap, issues, out length);
            default:
                throw new InvalidOperationException("Validated V2 lowering encountered an unsupported input length rule.");
        }
    }

    private static bool TryResolveTpSourceSpan(
        CompositionProfileDefinition profile,
        InputArtifactProfileSpace input,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        out long length)
    {
        if (!TryResolveInputSourceSpan(profile, input, resolvedMap, "TP", "exact plan", issues, out length))
        {
            return false;
        }

        if (length > TpMaximum256KLengthRule.MaximumBytes)
        {
            issues.Add(new CompositionIssue(
                InvalidInputGeometry,
                $"TP input space '{input.SpaceId}' requires source bytes through 0x{length:X}, exceeding the 256 KiB policy limit.",
                input.SpaceId));
            length = 0;
            return false;
        }

        return true;
    }

    private static bool TryResolveNormalDpSourceSpan(
        CompositionProfileDefinition profile,
        InputArtifactProfileSpace input,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        List<CompositionIssue> issues,
        out long length)
    {
        return TryResolveInputSourceSpan(
            profile,
            input,
            resolvedMap,
            "Normal DP",
            "declared extraction",
            issues,
            out length);
    }

    private static IReadOnlyList<long> ResolveNormalDpExpectedInputLengths(
        NormalDpExtractWithWarningLengthRule rule,
        long resolvedMapCapacity)
    {
        return rule.ExpectedInputLengths.Count == 0
            ? [resolvedMapCapacity]
            : rule.ExpectedInputLengths;
    }

    private static bool TryResolveInputSourceSpan(
        CompositionProfileDefinition profile,
        InputArtifactProfileSpace input,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        string inputKind,
        string spanDescription,
        List<CompositionIssue> issues,
        out long length)
    {
        long maximumEndExclusive = 0;
        bool hasView = false;
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
            hasView = true;
        }

        if (!hasView)
        {
            issues.Add(new CompositionIssue(
                InvalidInputGeometry,
                $"{inputKind} input space '{input.SpaceId}' has no source view from which to derive its {spanDescription} span.",
                input.SpaceId));
            length = 0;
            return false;
        }

        length = maximumEndExclusive;
        return true;
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
}
