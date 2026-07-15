using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
{
    private static Dictionary<string, FirmwareRegionSet> NormalizeRegionSets(
        IReadOnlyList<FirmwareRegionSetDocument> documents)
    {
        Dictionary<string, FirmwareRegionSetDocument> documentsById = IndexUnique(
            documents,
            static document => document.RegionSetId,
            "regionSets",
            "regionSetId");
        Dictionary<string, FirmwareRegionSet> normalized = new(StringComparer.Ordinal);
        foreach ((string regionSetId, FirmwareRegionSetDocument document) in documentsById)
        {
            string path = $"regionSets[{regionSetId}]";
            IReadOnlyList<FirmwareRegionDocument> regionDocuments =
                RequireList(document.Regions, $"{path}.regions");
            var regions = new FirmwareRegion[regionDocuments.Count];
            for (int index = 0; index < regionDocuments.Count; index++)
            {
                regions[index] = NormalizeRegion(regionDocuments[index], $"{path}.regions[{index}]");
            }

            try
            {
                normalized.Add(
                    regionSetId,
                    new FirmwareRegionSet(
                        regionSetId,
                        document.AddressSpaceId,
                        regions,
                        document.EvidenceRefs));
            }
            catch (ArgumentException exception)
            {
                throw Error(path, exception.Message, exception);
            }
            catch (OverflowException exception)
            {
                throw Error(path, exception.Message, exception);
            }
        }

        return normalized;
    }

    private static FirmwareRegion NormalizeRegion(FirmwareRegionDocument document, string path)
    {
        try
        {
            return new FirmwareRegion(
                document.RegionId,
                document.ParentRegionId,
                NormalizeOwner(document.Owner, $"{path}.owner"),
                NormalizeRegionKind(document.Kind, $"{path}.kind"),
                NormalizeRange(document.Range, $"{path}.range"),
                NormalizeWriteConstraint(document.WriteConstraint, $"{path}.writeConstraint"),
                ReadInt32(document.Alignment, 1, int.MaxValue, $"{path}.alignment"));
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static Dictionary<string, FirmwareMetadataSet> NormalizeMetadataSets(
        IReadOnlyList<FirmwareMetadataSetDocument> documents)
    {
        Dictionary<string, FirmwareMetadataSetDocument> documentsById = IndexUnique(
            documents,
            static document => document.MetadataSetId,
            "metadataSets",
            "metadataSetId");
        Dictionary<string, FirmwareMetadataSet> normalized = new(StringComparer.Ordinal);
        foreach ((string metadataSetId, FirmwareMetadataSetDocument document) in documentsById)
        {
            string path = $"metadataSets[{metadataSetId}]";
            IReadOnlyList<FirmwareMetadataStructureDocument> structureDocuments =
                RequireList(document.Structures, $"{path}.structures");
            var structures = new FirmwareMetadataStructure[structureDocuments.Count];
            for (int index = 0; index < structureDocuments.Count; index++)
            {
                structures[index] = NormalizeStructure(
                    structureDocuments[index],
                    $"{path}.structures[{index}]");
            }

            try
            {
                normalized.Add(
                    metadataSetId,
                    new FirmwareMetadataSet(metadataSetId, structures, document.EvidenceRefs));
            }
            catch (ArgumentException exception)
            {
                throw Error(path, exception.Message, exception);
            }
            catch (OverflowException exception)
            {
                throw Error(path, exception.Message, exception);
            }
        }

        return normalized;
    }

    private static FirmwareMetadataStructure NormalizeStructure(
        FirmwareMetadataStructureDocument document,
        string path)
    {
        IReadOnlyList<FirmwareMetadataFieldDocument> fieldDocuments =
            RequireList(document.Fields, $"{path}.fields");
        var fields = new FirmwareMetadataField[fieldDocuments.Count];
        for (int index = 0; index < fieldDocuments.Count; index++)
        {
            fields[index] = NormalizeField(fieldDocuments[index], $"{path}.fields[{index}]");
        }

        IReadOnlyList<FirmwareByteAssertionDocument> assertionDocuments =
            RequireList(document.Assertions, $"{path}.assertions");
        var assertions = new FirmwareMetadataByteAssertion[assertionDocuments.Count];
        for (int index = 0; index < assertionDocuments.Count; index++)
        {
            assertions[index] = NormalizeAssertion(
                assertionDocuments[index],
                $"{path}.assertions[{index}]");
        }

        try
        {
            return new FirmwareMetadataStructure(
                document.StructureId,
                document.ArtifactBindingId,
                ReadInt64(document.Length, 1, long.MaxValue, $"{path}.length"),
                NormalizeLocator(document.Locator, $"{path}.locator"),
                fields,
                assertions);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMetadataField NormalizeField(
        FirmwareMetadataFieldDocument document,
        string path)
    {
        try
        {
            FirmwareMetadataEncoding encoding = document.Encoding switch
            {
                "bytes" => FirmwareMetadataEncoding.Bytes,
                "printable-ascii" => FirmwareMetadataEncoding.PrintableAscii,
                "unsigned-integer" => FirmwareMetadataEncoding.UnsignedInteger,
                "signed-integer" => FirmwareMetadataEncoding.SignedInteger,
                _ => throw Error($"{path}.encoding", "Unknown metadata field encoding."),
            };
            FirmwareMetadataByteOrder? byteOrder = document.ByteOrder switch
            {
                null => null,
                "little" => FirmwareMetadataByteOrder.LittleEndian,
                "big" => FirmwareMetadataByteOrder.BigEndian,
                _ => throw Error($"{path}.byteOrder", "Unknown metadata byte order."),
            };
            FirmwareMetadataBitSlice? bitSlice = document.BitSlice is { } sourceSlice
                ? new FirmwareMetadataBitSlice(
                    ReadInt32(
                        sourceSlice.LeastSignificantBit,
                        0,
                        int.MaxValue,
                        $"{path}.bitSlice.leastSignificantBit"),
                    ReadInt32(sourceSlice.BitCount, 1, int.MaxValue, $"{path}.bitSlice.bitCount"))
                : null;

            return new FirmwareMetadataField(
                document.FieldId,
                ReadInt64(document.Offset, 0, long.MaxValue, $"{path}.offset"),
                ReadInt32(document.WidthBytes, 1, int.MaxValue, $"{path}.widthBytes"),
                encoding,
                byteOrder,
                bitSlice);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMetadataByteAssertion NormalizeAssertion(
        FirmwareByteAssertionDocument document,
        string path)
    {
        long offset = ReadInt64(document.Offset, 0, long.MaxValue, $"{path}.offset");
        byte[] expectedBytes = ParseHex(document.ExpectedHex, $"{path}.expectedHex");
        try
        {
            return document.MaskHex is { } maskHex
                ? FirmwareMetadataByteAssertion.Masked(
                    offset,
                    expectedBytes,
                    ParseHex(maskHex, $"{path}.maskHex"))
                : FirmwareMetadataByteAssertion.Exact(offset, expectedBytes);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMetadataLocator NormalizeLocator(
        FirmwareMetadataLocatorDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            return document.Kind switch
            {
                "absolute-range" => new FirmwareAbsoluteRangeLocator(
                    NormalizeAddressedRange(
                        Require(document.Range, $"{path}.range"),
                        $"{path}.range"),
                    document.AllowedResultRegionId),
                "region-relative" => new FirmwareRegionRelativeLocator(
                    Require(document.RegionId, $"{path}.regionId"),
                    ReadInt64(
                        Require(document.Offset, $"{path}.offset"),
                        0,
                        long.MaxValue,
                        $"{path}.offset"),
                    document.AllowedResultRegionId),
                "marker-relative" => new FirmwareMarkerRelativeLocator(
                    NormalizeAddressedRange(
                        Require(document.SearchRange, $"{path}.searchRange"),
                        $"{path}.searchRange"),
                    ParseHex(Require(document.MarkerHex, $"{path}.markerHex"), $"{path}.markerHex"),
                    NormalizeMarkerSelection(
                        Require(document.Selection, $"{path}.selection"),
                        $"{path}.selection"),
                    ReadInt64(
                        Require(document.ResultOffset, $"{path}.resultOffset"),
                        long.MinValue,
                        long.MaxValue,
                        $"{path}.resultOffset"),
                    document.AllowedResultRegionId),
                _ => throw Error($"{path}.kind", "Unknown metadata locator kind."),
            };
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMarkerSelection NormalizeMarkerSelection(
        FirmwareMarkerSelectionDocument document,
        string path)
    {
        return document.Kind switch
        {
            "unique" => new FirmwareUniqueMarkerSelection(),
            "terminal-match" => new FirmwareTerminalMarkerSelection(
                document.Terminal switch
                {
                    "lowest-address" => FirmwareMarkerTerminal.LowestAddress,
                    "highest-address" => FirmwareMarkerTerminal.HighestAddress,
                    _ => throw Error($"{path}.terminal", "Unknown marker terminal direction."),
                },
                ReadInt32(
                    Require(document.ExpectedMatchCount, $"{path}.expectedMatchCount"),
                    1,
                    int.MaxValue,
                    $"{path}.expectedMatchCount")),
            _ => throw Error($"{path}.kind", "Unknown marker selection kind."),
        };
    }

    private static FirmwareAddressedRange NormalizeAddressedRange(
        FirmwareAddressedRangeDocument document,
        string path)
    {
        return new FirmwareAddressedRange(
            document.AddressSpaceId,
            new ByteRange(
                ReadInt64(document.Start, 0, long.MaxValue, $"{path}.start"),
                ReadInt64(document.Length, 1, long.MaxValue, $"{path}.length")));
    }

    private static ByteRange NormalizeRange(FirmwareByteRangeDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new ByteRange(
            ReadInt64(document.Start, 0, long.MaxValue, $"{path}.start"),
            ReadInt64(document.Length, 1, long.MaxValue, $"{path}.length"));
    }

    private static void ValidateGlobalStructureIds(IEnumerable<FirmwareMetadataSet> metadataSets)
    {
        HashSet<string> structureIds = new(StringComparer.Ordinal);
        foreach (FirmwareMetadataStructure structure in metadataSets.SelectMany(static set => set.Structures))
        {
            if (!structureIds.Add(structure.StructureId))
            {
                throw Error(
                    "metadataSets",
                    $"Metadata structure id '{structure.StructureId}' must be unique across the family.");
            }
        }
    }

    private static T Require<T>(T? value, string path)
        where T : class
    {
        return value ?? throw Error(path, "Required value is missing.");
    }

    private static System.Text.Json.JsonElement Require(
        System.Text.Json.JsonElement? value,
        string path)
    {
        return value ?? throw Error(path, "Required integer is missing.");
    }
}
