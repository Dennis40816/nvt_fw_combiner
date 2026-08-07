using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static Dictionary<string, FirmwareMetadataSet> NormalizeMetadataSets(
        IReadOnlyList<FirmwareMetadataSetDocument> documents,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver)
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
            IReadOnlyList<FirmwareMetadataStructureDocument> structureDocuments = document.Structures;
            var structures = new FirmwareMetadataStructure[structureDocuments.Count];
            for (int index = 0; index < structureDocuments.Count; index++)
            {
                structures[index] = NormalizeStructure(
                    structureDocuments[index],
                    $"{path}.structures[{index}]",
                    metadataDefinitionResolver);
            }

            TranslateInvariant(path, () => normalized.Add(
                metadataSetId,
                new FirmwareMetadataSet(metadataSetId, structures, document.EvidenceRefs)));
        }

        return normalized;
    }

    private static FirmwareMetadataStructure NormalizeStructure(
        FirmwareMetadataStructureDocument document,
        string path,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver)
    {
        FirmwareMetadataLocator locator = NormalizeLocator(
            document.Locator,
            $"{path}.locator");
        if (document.DefinitionReference is { } referenceDocument)
        {
            var reference = new FirmwareMetadataStructureDefinitionReference(
                referenceDocument.FamilyId,
                referenceDocument.FamilyVersion,
                referenceDocument.FamilyContentHash,
                referenceDocument.StructureId);
            FirmwareMetadataStructureDefinition definition =
                metadataDefinitionResolver is not null &&
                metadataDefinitionResolver.TryResolve(
                    reference,
                    out FirmwareMetadataStructureDefinition? resolvedDefinition) &&
                resolvedDefinition is not null
                    ? resolvedDefinition
                    : throw Error(
                        $"{path}.definitionReference",
                        "Exact trusted metadata definition could not be resolved.");

            return TranslateInvariant(path, () => new FirmwareMetadataStructure(
                    document.StructureId,
                    document.ArtifactBindingId,
                    definition,
                    locator));
        }

        FirmwareMetadataTypedDefinition? typedDefinition =
            NormalizeTypedDefinition(document, path);
        IReadOnlyList<FirmwareMetadataFieldDocument> fieldDocuments = document.Fields;
        var fields = new FirmwareMetadataField[fieldDocuments.Count];
        for (int index = 0; index < fieldDocuments.Count; index++)
        {
            fields[index] = NormalizeField(fieldDocuments[index], $"{path}.fields[{index}]");
        }

        IReadOnlyList<FirmwareByteAssertionDocument> assertionDocuments = document.Assertions;
        var assertions = new FirmwareMetadataByteAssertion[assertionDocuments.Count];
        for (int index = 0; index < assertionDocuments.Count; index++)
        {
            assertions[index] = NormalizeAssertion(
                assertionDocuments[index],
                $"{path}.assertions[{index}]");
        }

        IReadOnlyList<FirmwareMetadataFieldRelationDocument> relationDocuments =
            document.Relations ?? [];
        var relations = new FirmwareMetadataFieldRelation[relationDocuments.Count];
        for (int index = 0; index < relationDocuments.Count; index++)
        {
            relations[index] = NormalizeRelation(
                relationDocuments[index],
                $"{path}.relations[{index}]");
        }

        return TranslateInvariant(path, () => new FirmwareMetadataStructure(
                document.StructureId,
                document.ArtifactBindingId,
                ReadInt64(document.Length, $"{path}.length"),
                locator,
                fields,
                assertions,
                relations,
                typedDefinition));
    }

    private static FirmwareTpFlashHeaderDefinition? NormalizeTypedDefinition(
        FirmwareMetadataStructureDocument document,
        string path)
    {
        return document.StructureKind switch
        {
            null => null,
            "tp-flash-header" => NormalizeTpFlashHeader(
                document.TpFlashHeader!,
                $"{path}.tpFlashHeader"),
            _ => throw Error(
                $"{path}.structureKind",
                "Unknown metadata structure kind."),
        };
    }

    private static FirmwareTpFlashHeaderDefinition NormalizeTpFlashHeader(
        FirmwareTpFlashHeaderDocument document,
        string path)
    {
        IReadOnlyList<FirmwareMetadataNamedSpanDocument> spanDocuments = document.Spans;
        var spans = new FirmwareMetadataNamedSpan[spanDocuments.Count];
        for (int index = 0; index < spanDocuments.Count; index++)
        {
            FirmwareMetadataNamedSpanDocument span = spanDocuments[index];
            spans[index] = TranslateInvariant(
                $"{path}.spans[{index}]",
                () => new FirmwareMetadataNamedSpan(
                    span.SpanId,
                    NormalizeRange(span.Range, $"{path}.spans[{index}].range")));
        }

        IReadOnlyList<FirmwareTpFlashHeaderFieldSemanticsDocument> semanticsDocuments =
            document.FieldSemantics;
        var fieldSemantics =
            new FirmwareTpFlashHeaderFieldSemantics[semanticsDocuments.Count];
        for (int index = 0; index < semanticsDocuments.Count; index++)
        {
            fieldSemantics[index] = NormalizeTpFlashHeaderFieldSemantics(
                semanticsDocuments[index],
                $"{path}.fieldSemantics[{index}]");
        }

        IReadOnlyList<FirmwareMetadataFieldSeriesDocument> seriesDocuments = document.FieldSeries;
        var fieldSeries = new FirmwareMetadataFieldSeries[seriesDocuments.Count];
        for (int index = 0; index < seriesDocuments.Count; index++)
        {
            fieldSeries[index] = NormalizeMetadataFieldSeries(
                seriesDocuments[index],
                $"{path}.fieldSeries[{index}]");
        }

        IReadOnlyList<FirmwareMetadataFieldGroupDocument> groupDocuments = document.FieldGroups;
        var fieldGroups = new FirmwareMetadataFieldGroup[groupDocuments.Count];
        for (int index = 0; index < groupDocuments.Count; index++)
        {
            FirmwareMetadataFieldGroupDocument group = groupDocuments[index];
            fieldGroups[index] = TranslateInvariant(
                $"{path}.fieldGroups[{index}]",
                () => new FirmwareMetadataFieldGroup(
                    group.GroupId,
                    group.FieldIds,
                    group.SeriesIds));
        }

        return TranslateInvariant(
            path,
            () => new FirmwareTpFlashHeaderDefinition(
                spans,
                fieldSemantics,
                fieldSeries,
                fieldGroups));
    }

    private static FirmwareTpFlashHeaderFieldSemantics
        NormalizeTpFlashHeaderFieldSemantics(
            FirmwareTpFlashHeaderFieldSemanticsDocument document,
            string path)
    {
        TpFlashHeaderFieldSubject subject = document.Subject switch
        {
            "header" => TpFlashHeaderFieldSubject.Header,
            "ilm" => TpFlashHeaderFieldSubject.Ilm,
            "dlm" => TpFlashHeaderFieldSubject.Dlm,
            "data" => TpFlashHeaderFieldSubject.Data,
            "dlm-difference" => TpFlashHeaderFieldSubject.DlmDifference,
            "firmware-config" => TpFlashHeaderFieldSubject.FirmwareConfig,
            "ctrlram" => TpFlashHeaderFieldSubject.CtrlRam,
            "mp-ctrlram" => TpFlashHeaderFieldSubject.MpCtrlRam,
            _ => throw Error($"{path}.subject", "Unknown TP Header field subject."),
        };
        TpFlashHeaderFieldRole role = document.Role switch
        {
            "integrity-value" => TpFlashHeaderFieldRole.IntegrityValue,
            "destination-address" => TpFlashHeaderFieldRole.DestinationAddress,
            "size" => TpFlashHeaderFieldRole.Size,
            "tp-bin-start-address" => TpFlashHeaderFieldRole.TpBinStartAddress,
            "option" => TpFlashHeaderFieldRole.Option,
            _ => throw Error($"{path}.role", "Unknown TP Header field role."),
        };
        int? logicalIndex = document.LogicalIndex is { } sourceIndex
            ? ReadInt32(sourceIndex, $"{path}.logicalIndex")
            : null;
        FirmwareTpFlashHeaderStoredAddressSemantics? storedAddress =
            document.StoredAddress is { } address
                ? NormalizeStoredAddress(
                    address,
                    $"{path}.storedAddress")
                : null;
        return TranslateInvariant(
            path,
            () => new FirmwareTpFlashHeaderFieldSemantics(
                document.FieldId,
                document.SpanId,
                subject,
                role,
                logicalIndex,
                storedAddress));
    }

    private static FirmwareTpFlashHeaderStoredAddressSemantics NormalizeStoredAddress(
        FirmwareTpFlashHeaderStoredAddressDocument document,
        string path)
    {
        TpFlashHeaderStoredAddressBasis basis = document.Basis switch
        {
            "absolute" => TpFlashHeaderStoredAddressBasis.Absolute,
            "tp-bin-offset" => TpFlashHeaderStoredAddressBasis.TpBinOffset,
            _ => throw Error(
                $"{path}.basis",
                "Unknown TP Header stored-address basis."),
        };
        return TranslateInvariant(
            path,
            () => new FirmwareTpFlashHeaderStoredAddressSemantics(
                document.AddressSpaceId,
                basis));
    }

    private static FirmwareMetadataFieldSeries NormalizeMetadataFieldSeries(
        FirmwareMetadataFieldSeriesDocument document,
        string path)
    {
        IReadOnlyList<FirmwareMetadataFieldSeriesMemberDocument> memberDocuments = document.Members;
        var members = new FirmwareMetadataFieldSeriesMember[memberDocuments.Count];
        for (int index = 0; index < memberDocuments.Count; index++)
        {
            FirmwareMetadataFieldSeriesMemberDocument member = memberDocuments[index];
            members[index] = TranslateInvariant(
                $"{path}.members[{index}]",
                () => new FirmwareMetadataFieldSeriesMember(
                    ReadInt32(member.Index, $"{path}.members[{index}].index"),
                    member.FieldId));
        }

        IReadOnlyList<FirmwareMetadataFieldSeriesApplicabilityDocument>
            applicabilityDocuments = document.Applicability;
        var applicability =
            new FirmwareMetadataFieldSeriesApplicability[applicabilityDocuments.Count];
        for (int index = 0; index < applicabilityDocuments.Count; index++)
        {
            FirmwareMetadataFieldSeriesApplicabilityDocument row =
                applicabilityDocuments[index];
            IReadOnlyList<System.Text.Json.JsonElement> activeIndexDocuments = row.ActiveIndices;
            int[] activeIndices = new int[activeIndexDocuments.Count];
            for (int activeIndex = 0; activeIndex < activeIndexDocuments.Count; activeIndex++)
            {
                activeIndices[activeIndex] = ReadInt32(
                    activeIndexDocuments[activeIndex],
                    $"{path}.applicability[{index}].activeIndices[{activeIndex}]");
            }

            applicability[index] = TranslateInvariant(
                $"{path}.applicability[{index}]",
                () => new FirmwareMetadataFieldSeriesApplicability(
                    ReadInt32(row.IcCount, $"{path}.applicability[{index}].icCount"),
                    activeIndices));
        }

        return TranslateInvariant(
            path,
            () => new FirmwareMetadataFieldSeries(
                document.SeriesId,
                members,
                applicability));
    }

    private static FirmwareMetadataFieldRelation NormalizeRelation(
        FirmwareMetadataFieldRelationDocument document,
        string path)
    {
        FirmwareMetadataFieldRelationKind kind = document.Kind switch
        {
            "bitwise-complement" => FirmwareMetadataFieldRelationKind.BitwiseComplement,
            _ => throw Error($"{path}.kind", "Unknown metadata field relation kind."),
        };
        return TranslateInvariant(path, () => new FirmwareMetadataFieldRelation(
            document.RelationId,
            kind,
            document.SourceFieldId,
            document.RelatedFieldId));
    }

    private static FirmwareMetadataField NormalizeField(
        FirmwareMetadataFieldDocument document,
        string path)
    {
        return TranslateInvariant(path, () =>
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
                    ReadInt32(sourceSlice.LeastSignificantBit, $"{path}.bitSlice.leastSignificantBit"),
                    ReadInt32(sourceSlice.BitCount, $"{path}.bitSlice.bitCount"))
                : null;

            return new FirmwareMetadataField(
                document.FieldId,
                ReadInt64(document.Offset, $"{path}.offset"),
                ReadInt32(document.WidthBytes, $"{path}.widthBytes"),
                encoding,
                byteOrder,
                bitSlice,
                document.SourceName);
        });
    }

    private static FirmwareMetadataByteAssertion NormalizeAssertion(
        FirmwareByteAssertionDocument document,
        string path)
    {
        long offset = ReadInt64(document.Offset, $"{path}.offset");
        byte[] expectedBytes = ParseHex(document.ExpectedHex, $"{path}.expectedHex");
        return TranslateInvariant(path, () => document.MaskHex is { } maskHex
                ? FirmwareMetadataByteAssertion.Masked(
                    offset,
                    expectedBytes,
                    ParseHex(maskHex, $"{path}.maskHex"))
                : FirmwareMetadataByteAssertion.Exact(offset, expectedBytes));
    }

    private static FirmwareMetadataLocator NormalizeLocator(
        FirmwareMetadataLocatorDocument document,
        string path)
    {
        return TranslateInvariant<FirmwareMetadataLocator>(path, () => document.Kind switch
            {
                "absolute-range" => new FirmwareAbsoluteRangeLocator(
                    NormalizeAddressedRange(
                        document.Range!,
                        $"{path}.range"),
                    document.AllowedResultRegionId),
                "region-relative" => new FirmwareRegionRelativeLocator(
                    document.RegionId!,
                    ReadInt64(document.Offset!.Value, $"{path}.offset"),
                    document.AllowedResultRegionId),
                "marker-relative" => new FirmwareMarkerRelativeLocator(
                    NormalizeAddressedRange(
                        document.SearchRange!,
                        $"{path}.searchRange"),
                    ParseHex(document.MarkerHex!, $"{path}.markerHex"),
                    NormalizeMarkerSelection(
                        document.Selection!,
                        $"{path}.selection"),
                    ReadInt64(document.ResultOffset!.Value, $"{path}.resultOffset"),
                    document.AllowedResultRegionId),
                "metadata-field-selected" => new FirmwareMetadataFieldSelectedLocator(
                    document.PrerequisiteStructureId!,
                    document.PrerequisiteFieldId!,
                    NormalizeMetadataSelectedBranches(
                        document.Branches!,
                        $"{path}.branches"),
                    ReadInt64(document.ResultOffset!.Value, $"{path}.resultOffset"),
                    document.AllowedResultRegionId),
                _ => throw Error($"{path}.kind", "Unknown metadata locator kind."),
            });
    }

    private static FirmwareMetadataFieldSelectedBranch[]
        NormalizeMetadataSelectedBranches(
            IReadOnlyList<FirmwareMetadataFieldSelectedBranchDocument> documents,
            string path)
    {
        var branches =
            new FirmwareMetadataFieldSelectedBranch[documents.Count];
        for (int index = 0; index < documents.Count; index++)
        {
            FirmwareMetadataFieldSelectedBranchDocument document =
                documents[index];
            branches[index] = new FirmwareMetadataFieldSelectedBranch(
                ReadUInt64(document.MinimumValue, $"{path}[{index}].minimumValue"),
                ReadUInt64(document.MaximumValue, $"{path}[{index}].maximumValue"),
                NormalizeAddressedRange(
                    document.AnchorRange,
                    $"{path}[{index}].anchorRange"));
        }

        return branches;
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
                ReadInt32(document.ExpectedMatchCount!.Value, $"{path}.expectedMatchCount")),
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
                ReadInt64(document.Start, $"{path}.start"),
                ReadInt64(document.Length, $"{path}.length")));
    }

    private static ByteRange NormalizeRange(FirmwareByteRangeDocument document, string path)
    {
        return new ByteRange(
            ReadInt64(document.Start, $"{path}.start"),
            ReadInt64(document.Length, $"{path}.length"));
    }

}
