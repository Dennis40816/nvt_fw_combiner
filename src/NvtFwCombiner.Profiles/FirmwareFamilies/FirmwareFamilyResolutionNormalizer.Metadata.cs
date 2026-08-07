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
            FirmwareMetadataStructure[] structures = NormalizeItems(
                structureDocuments,
                $"{path}.structures",
                (structure, structurePath) => NormalizeStructure(
                    structure,
                    structurePath,
                    metadataDefinitionResolver));

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
        FirmwareMetadataField[] fields = NormalizeItems(
            fieldDocuments,
            $"{path}.fields",
            NormalizeField);

        IReadOnlyList<FirmwareByteAssertionDocument> assertionDocuments = document.Assertions;
        FirmwareMetadataByteAssertion[] assertions = NormalizeItems(
            assertionDocuments,
            $"{path}.assertions",
            NormalizeAssertion);

        IReadOnlyList<FirmwareMetadataFieldRelationDocument> relationDocuments =
            document.Relations ?? [];
        FirmwareMetadataFieldRelation[] relations = NormalizeItems(
            relationDocuments,
            $"{path}.relations",
            NormalizeRelation);

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
        FirmwareMetadataNamedSpan[] spans = NormalizeItems(
            spanDocuments,
            $"{path}.spans",
            (span, spanPath) => TranslateInvariant(
                spanPath,
                () => new FirmwareMetadataNamedSpan(
                    span.SpanId,
                    NormalizeRange(span.Range, $"{spanPath}.range"))));

        IReadOnlyList<FirmwareTpFlashHeaderFieldSemanticsDocument> semanticsDocuments =
            document.FieldSemantics;
        FirmwareTpFlashHeaderFieldSemantics[] fieldSemantics = NormalizeItems(
            semanticsDocuments,
            $"{path}.fieldSemantics",
            NormalizeTpFlashHeaderFieldSemantics);

        IReadOnlyList<FirmwareMetadataFieldSeriesDocument> seriesDocuments = document.FieldSeries;
        FirmwareMetadataFieldSeries[] fieldSeries = NormalizeItems(
            seriesDocuments,
            $"{path}.fieldSeries",
            NormalizeMetadataFieldSeries);

        IReadOnlyList<FirmwareMetadataFieldGroupDocument> groupDocuments = document.FieldGroups;
        FirmwareMetadataFieldGroup[] fieldGroups = NormalizeItems(
            groupDocuments,
            $"{path}.fieldGroups",
            (group, groupPath) => TranslateInvariant(
                groupPath,
                () => new FirmwareMetadataFieldGroup(
                    group.GroupId,
                    group.FieldIds,
                    group.SeriesIds)));

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
        FirmwareMetadataFieldSeriesMember[] members = NormalizeItems(
            memberDocuments,
            $"{path}.members",
            (member, memberPath) => TranslateInvariant(
                memberPath,
                () => new FirmwareMetadataFieldSeriesMember(
                    ReadInt32(member.Index, $"{memberPath}.index"),
                    member.FieldId)));

        IReadOnlyList<FirmwareMetadataFieldSeriesApplicabilityDocument>
            applicabilityDocuments = document.Applicability;
        FirmwareMetadataFieldSeriesApplicability[] applicability = NormalizeItems(
            applicabilityDocuments,
            $"{path}.applicability",
            (row, applicabilityPath) =>
            {
                IReadOnlyList<System.Text.Json.JsonElement> activeIndexDocuments = row.ActiveIndices;
                int[] activeIndices = NormalizeItems(
                    activeIndexDocuments,
                    $"{applicabilityPath}.activeIndices",
                    ReadInt32);
                return TranslateInvariant(
                    applicabilityPath,
                    () => new FirmwareMetadataFieldSeriesApplicability(
                        ReadInt32(row.IcCount, $"{applicabilityPath}.icCount"),
                        activeIndices));
            });

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
        return NormalizeItems(
            documents,
            path,
            (document, branchPath) => new FirmwareMetadataFieldSelectedBranch(
                ReadUInt64(document.MinimumValue, $"{branchPath}.minimumValue"),
                ReadUInt64(document.MaximumValue, $"{branchPath}.maximumValue"),
                NormalizeAddressedRange(
                    document.AnchorRange,
                    $"{branchPath}.anchorRange")));
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
