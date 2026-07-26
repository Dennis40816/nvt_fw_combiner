using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

public sealed partial class FirmwareFamilyResolutionDefinition
{
    /// <summary>Evaluates one map-selected metadata structure against the exact bound artifact.</summary>
    public FirmwareMetadataStructureResolution ResolveMetadataStructure(
        string mapId,
        string metadataStructureId,
        FirmwareMapResolutionInputs inputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataStructureId);
        ArgumentNullException.ThrowIfNull(inputs);

        FirmwareImageMap? map = _imageMaps.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.MapId, mapId)) ?? throw new KeyNotFoundException($"Unknown firmware image map '{mapId}'.");
        FirmwareMetadataStructure structure =
            TryResolveStructure(
                mapId,
                metadataStructureId,
                out FirmwareMetadataStructure? selectedStructure)
                ? selectedStructure
                : throw new KeyNotFoundException(
                    $"Image map '{mapId}' does not select metadata structure '{metadataStructureId}'.");

        return ResolveMetadataStructureCore(map, structure, inputs);
    }

    private FirmwareMetadataStructureResolution ResolveMetadataStructureCore(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareMapResolutionInputs inputs)
    {
        FirmwareArtifactPayload? artifact = inputs.Artifacts.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.ArtifactId, structure.ArtifactBindingId));
        if (artifact is null)
        {
            return FirmwareMetadataStructureResolution.Pending(map.MapId, structure);
        }

        if (structure.Locator is FirmwareMetadataFieldSelectedLocator selected)
        {
            return ResolveMetadataSelectedStructure(
                map,
                structure,
                selected,
                artifact,
                inputs);
        }

        if (!TryResolveLocator(
            map,
            structure,
            artifact,
            out FirmwareMetadataLocatorOutcome? locatorOutcome,
            out FirmwareMetadataStructureResolutionFailure failure,
            out int? observedMarkerMatchCount))
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                failure,
                observedMarkerMatchCount);
        }

        ByteRange resolvedRange = locatorOutcome.ResolvedRange.Range;
        int start = checked((int)resolvedRange.Start);
        int length = checked((int)resolvedRange.Length);
        return !structure.TryDecode(
            artifact.Bytes.Slice(start, length),
            out FirmwareDecodedMetadataStructure? decoded)
            ? FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed)
            : FirmwareMetadataStructureResolution.Success(
            new FirmwareResolvedMetadataStructure(
                map.MapId,
                artifact.Identity,
                locatorOutcome,
                decoded));
    }

    private FirmwareMetadataStructureResolution ResolveMetadataSelectedStructure(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareMetadataFieldSelectedLocator locator,
        FirmwareArtifactPayload artifact,
        FirmwareMapResolutionInputs inputs)
    {
        if (!TryResolveStructure(
            map.MapId,
            locator.PrerequisiteStructureId,
            out FirmwareMetadataStructure? prerequisiteStructure))
        {
            throw new InvalidOperationException(
                $"Validated metadata prerequisite '{locator.PrerequisiteStructureId}' is unavailable.");
        }

        var prerequisite = new FirmwareMetadataPrerequisite(
            prerequisiteStructure.ArtifactBindingId,
            prerequisiteStructure.StructureId,
            locator.PrerequisiteFieldId);
        FirmwareMetadataStructureResolution prerequisiteResolution =
            ResolveMetadataStructureCore(map, prerequisiteStructure, inputs);
        if (prerequisiteResolution.Status == FirmwareMetadataStructureResolutionStatus.Pending)
        {
            return FirmwareMetadataStructureResolution.PendingForPrerequisite(
                map.MapId,
                structure,
                prerequisite);
        }

        if (prerequisiteResolution.Status == FirmwareMetadataStructureResolutionStatus.Rejected)
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected,
                prerequisite: prerequisite);
        }

        FirmwareDecodedMetadataFact? selectedFact =
            prerequisiteResolution.Resolved!.DecodedStructure.Facts.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.FieldId, locator.PrerequisiteFieldId));
        if (selectedFact?.Value.UnsignedIntegerValue is not { } selectedValue ||
            !locator.TrySelect(selectedValue, out FirmwareMetadataFieldSelectedBranch? branch))
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.PrerequisiteValueUnsupported,
                prerequisite: prerequisite);
        }

        long resultStart = checked(branch.AnchorRange.Range.Start + locator.ResultOffset);
        if (resultStart < branch.AnchorRange.Range.Start)
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds);
        }

        ByteRange resolvedRange = new(resultStart, structure.LengthBytes);
        FirmwareRegion allowedRegion = FindRegion(map, locator.AllowedResultRegionId);
        if (!branch.AnchorRange.Range.Contains(resolvedRange) ||
            !allowedRegion.Range.Contains(resolvedRange) ||
            !ArtifactContains(artifact, resolvedRange))
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds);
        }

        int start = checked((int)resolvedRange.Start);
        int length = checked((int)resolvedRange.Length);
        if (!structure.TryDecode(
            artifact.Bytes.Slice(start, length),
            out FirmwareDecodedMetadataStructure? decoded))
        {
            return FirmwareMetadataStructureResolution.Rejected(
                map.MapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed);
        }

        var outcome = new FirmwareMetadataLocatorOutcome(
            FirmwareMetadataLocatorKind.MetadataFieldSelected,
            new FirmwareAddressedRange(map.AddressSpaceId, resolvedRange));
        return FirmwareMetadataStructureResolution.Success(
            new FirmwareResolvedMetadataStructure(
                map.MapId,
                artifact.Identity,
                outcome,
                decoded));
    }

    private static bool TryResolveLocator(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareArtifactPayload artifact,
        [NotNullWhen(true)] out FirmwareMetadataLocatorOutcome? outcome,
        out FirmwareMetadataStructureResolutionFailure failure,
        out int? observedMarkerMatchCount)
    {
        outcome = null;
        failure = FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds;
        observedMarkerMatchCount = null;
        switch (structure.Locator)
        {
            case FirmwareAbsoluteRangeLocator absolute:
                return TryCreateStaticOutcome(
                    map,
                    artifact,
                    FirmwareMetadataLocatorKind.AbsoluteRange,
                    absolute.Range.Range,
                    out outcome,
                    out failure);
            case FirmwareRegionRelativeLocator relative:
                FirmwareRegion baseRegion = FindRegion(map, relative.RegionId);
                ByteRange relativeRange = new(
                    checked(baseRegion.Range.Start + relative.Offset),
                    structure.LengthBytes);
                return TryCreateStaticOutcome(
                    map,
                    artifact,
                    FirmwareMetadataLocatorKind.RegionRelative,
                    relativeRange,
                    out outcome,
                    out failure);
            case FirmwareMarkerRelativeLocator marker:
                return TryResolveMarker(
                    map,
                    structure,
                    marker,
                    artifact,
                    out outcome,
                    out failure,
                    out observedMarkerMatchCount);
            case FirmwareMetadataFieldSelectedLocator:
                throw new InvalidOperationException(
                    "Metadata-selected locators require prerequisite resolution.");
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(structure),
                    "Unknown firmware metadata locator type.");
        }
    }

    private static bool TryCreateStaticOutcome(
        FirmwareImageMap map,
        FirmwareArtifactPayload artifact,
        FirmwareMetadataLocatorKind kind,
        ByteRange range,
        [NotNullWhen(true)] out FirmwareMetadataLocatorOutcome? outcome,
        out FirmwareMetadataStructureResolutionFailure failure)
    {
        outcome = null;
        failure = FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds;
        if (!ArtifactContains(artifact, range))
        {
            return false;
        }

        outcome = new FirmwareMetadataLocatorOutcome(
            kind,
            new FirmwareAddressedRange(map.AddressSpaceId, range));
        return true;
    }

    private static bool TryResolveMarker(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareMarkerRelativeLocator marker,
        FirmwareArtifactPayload artifact,
        [NotNullWhen(true)] out FirmwareMetadataLocatorOutcome? outcome,
        out FirmwareMetadataStructureResolutionFailure failure,
        out int? observedMarkerMatchCount)
    {
        outcome = null;
        failure = FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds;
        observedMarkerMatchCount = null;
        if (!ArtifactContains(artifact, marker.SearchRange.Range))
        {
            return false;
        }

        MarkerMatches matches = FindMarkerMatches(artifact, marker);
        if (!TrySelectMarker(marker.Selection, matches, out long selectedMarkerStart))
        {
            failure = FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch;
            observedMarkerMatchCount = matches.Count;
            return false;
        }

        long resultStart = checked(selectedMarkerStart + marker.ResultOffset);
        if (resultStart < 0)
        {
            failure = FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds;
            return false;
        }

        ByteRange resultRange = new(resultStart, structure.LengthBytes);
        FirmwareRegion allowedRegion = FindRegion(map, marker.AllowedResultRegionId);
        if (!allowedRegion.Range.Contains(resultRange) || !ArtifactContains(artifact, resultRange))
        {
            failure = FirmwareMetadataStructureResolutionFailure.ResolvedRangeOutOfBounds;
            return false;
        }

        outcome = new FirmwareMetadataLocatorOutcome(
            FirmwareMetadataLocatorKind.MarkerRelative,
            new FirmwareAddressedRange(map.AddressSpaceId, resultRange),
            matches.Count,
            selectedMarkerStart);
        return true;
    }

    private static MarkerMatches FindMarkerMatches(
        FirmwareArtifactPayload artifact,
        FirmwareMarkerRelativeLocator marker)
    {
        ByteRange searchRange = marker.SearchRange.Range;
        int searchStart = checked((int)searchRange.Start);
        int searchLength = checked((int)searchRange.Length);
        ReadOnlySpan<byte> search = artifact.Bytes.Slice(searchStart, searchLength);
        ReadOnlySpan<byte> markerBytes = marker.MarkerBytes.Bytes;

        int count = 0;
        long firstStart = -1;
        long lastStart = -1;
        for (int offset = 0; offset <= search.Length - markerBytes.Length; offset++)
        {
            if (!search.Slice(offset, markerBytes.Length).SequenceEqual(markerBytes))
            {
                continue;
            }

            long absoluteStart = checked(searchRange.Start + offset);
            if (count == 0)
            {
                firstStart = absoluteStart;
            }

            lastStart = absoluteStart;
            count = checked(count + 1);
        }

        return new MarkerMatches(count, firstStart, lastStart);
    }

    private static bool TrySelectMarker(
        FirmwareMarkerSelection selection,
        MarkerMatches matches,
        out long selectedStart)
    {
        selectedStart = -1;
        switch (selection)
        {
            case FirmwareUniqueMarkerSelection:
                if (matches.Count != 1)
                {
                    return false;
                }

                selectedStart = matches.FirstStart;
                return true;
            case FirmwareTerminalMarkerSelection terminal:
                if (matches.Count != terminal.ExpectedMatchCount)
                {
                    return false;
                }

                selectedStart = terminal.Terminal == FirmwareMarkerTerminal.LowestAddress
                    ? matches.FirstStart
                    : matches.LastStart;
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(selection), "Unknown marker selection type.");
        }
    }

    private static FirmwareRegion FindRegion(FirmwareImageMap map, string regionId)
    {
        return map.Regions.First(region => StringComparer.Ordinal.Equals(region.RegionId, regionId));
    }

    private static bool ArtifactContains(FirmwareArtifactPayload artifact, ByteRange range)
    {
        return range.EndExclusive <= artifact.LengthBytes;
    }

    private readonly record struct MarkerMatches(int Count, long FirstStart, long LastStart);
}
