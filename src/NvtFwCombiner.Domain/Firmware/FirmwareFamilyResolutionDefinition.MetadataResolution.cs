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
        if (!TryResolveStructure(mapId, metadataStructureId, out FirmwareMetadataStructure? structure))
        {
            throw new KeyNotFoundException(
                $"Image map '{mapId}' does not select metadata structure '{metadataStructureId}'.");
        }

        FirmwareArtifactPayload? artifact = inputs.Artifacts.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.ArtifactId, structure.ArtifactBindingId));
        if (artifact is null)
        {
            return FirmwareMetadataStructureResolution.Pending(mapId, structure);
        }

        if (!TryResolveLocator(
            map,
            structure,
            artifact,
            out FirmwareMetadataLocatorOutcome? locatorOutcome,
            out FirmwareMetadataStructureResolutionFailure failure))
        {
            return FirmwareMetadataStructureResolution.Rejected(mapId, structure, failure);
        }

        ByteRange resolvedRange = locatorOutcome.ResolvedRange.Range;
        int start = checked((int)resolvedRange.Start);
        int length = checked((int)resolvedRange.Length);
        return !structure.TryDecode(
            artifact.Bytes.Slice(start, length),
            out FirmwareDecodedMetadataStructure? decoded)
            ? FirmwareMetadataStructureResolution.Rejected(
                mapId,
                structure,
                FirmwareMetadataStructureResolutionFailure.StructureDecodeFailed)
            : FirmwareMetadataStructureResolution.Success(
            new FirmwareResolvedMetadataStructure(
                mapId,
                artifact.Identity,
                locatorOutcome,
                decoded));
    }

    private static bool TryResolveLocator(
        FirmwareImageMap map,
        FirmwareMetadataStructure structure,
        FirmwareArtifactPayload artifact,
        [NotNullWhen(true)] out FirmwareMetadataLocatorOutcome? outcome,
        out FirmwareMetadataStructureResolutionFailure failure)
    {
        outcome = null;
        failure = FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds;
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
                    out failure);
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
        out FirmwareMetadataStructureResolutionFailure failure)
    {
        outcome = null;
        failure = FirmwareMetadataStructureResolutionFailure.ArtifactRangeOutOfBounds;
        if (!ArtifactContains(artifact, marker.SearchRange.Range))
        {
            return false;
        }

        MarkerMatches matches = FindMarkerMatches(artifact, marker);
        if (!TrySelectMarker(marker.Selection, matches, out long selectedMarkerStart))
        {
            failure = FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch;
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
