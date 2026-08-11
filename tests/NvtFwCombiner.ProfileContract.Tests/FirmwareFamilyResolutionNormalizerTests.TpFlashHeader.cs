using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class FirmwareFamilyResolutionNormalizerTests
{
    /// <summary>
    /// Verifies the TP Flash Header wire payload normalizes into one typed
    /// definition while retaining the common physical-field declarations.
    /// </summary>
    [Fact]
    public void NormalizeMapsTpFlashHeaderTypedPayload()
    {
        FirmwareFamilyDocument document = WithTpFlashHeader(
            Document(includePredicate: false),
            "tp-flash-header",
            TpFlashHeaderPayload());

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        FirmwareMetadataStructure structure =
            Assert.Single(definition.GetStructuresForMap("map"));
        FirmwareTpFlashHeaderDefinition typed = Assert.IsType<FirmwareTpFlashHeaderDefinition>(
            structure.Definition.TypedDefinition);
        Assert.Equal(FirmwareMetadataStructureKind.TpFlashHeader, structure.Definition.StructureKind);
        Assert.Equal(
            ["raw", "label", "chip-number", "signed-offset"],
            structure.Fields.Select(static field => field.FieldId));
        Assert.Equal(["header", "dlm"], typed.Spans.Select(static span => span.SpanId));
        Assert.Equal(
            ["chip-number", "label", "raw", "signed-offset"],
            typed.FieldSemantics.Select(static semantics => semantics.FieldId));

        FirmwareMetadataFieldSeries series = Assert.Single(typed.FieldSeries);
        Assert.Equal("dlm-records", series.SeriesId);
        Assert.Equal([0, 1], series.Members.Select(static member => member.Index));
        Assert.Equal(
            ["chip-number", "signed-offset"],
            series.Members.Select(static member => member.FieldId));
        Assert.Equal([1, 2], series.Applicability.Select(static row => row.ChipCount));
        Assert.Equal([0], series.Applicability[0].ActiveIndices);
        Assert.Equal([0, 1], series.Applicability[1].ActiveIndices);

        Assert.Equal(
            ["dlm-values", "header-values"],
            typed.FieldGroups.Select(static group => group.GroupId));
        Assert.Equal(
            ["dlm-records"],
            typed.FieldGroups[0].SeriesIds);
        Assert.Equal(
            ["label", "raw"],
            typed.FieldGroups[1].FieldIds);
    }

    /// <summary>Verifies omission of both discriminator and payload remains the legacy wire shape.</summary>
    [Fact]
    public void NormalizeKeepsLegacyMetadataStructureGeneric()
    {
        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(
                Document(includePredicate: false),
                FamilyHash);

        FirmwareMetadataStructure structure =
            Assert.Single(definition.GetStructuresForMap("map"));
        Assert.Equal(FirmwareMetadataStructureKind.Generic, structure.Definition.StructureKind);
        Assert.Null(structure.Definition.TypedDefinition);
    }

    /// <summary>Verifies every accepted TP Header subject and value-role token maps explicitly.</summary>
    [Theory]
    [InlineData(
        "header",
        "integrity-value",
        (int)TpFlashHeaderFieldSubject.Header,
        (int)TpFlashHeaderFieldRole.IntegrityValue)]
    [InlineData(
        "ilm",
        "destination-address",
        (int)TpFlashHeaderFieldSubject.Ilm,
        (int)TpFlashHeaderFieldRole.DestinationAddress)]
    [InlineData(
        "dlm",
        "size",
        (int)TpFlashHeaderFieldSubject.Dlm,
        (int)TpFlashHeaderFieldRole.Size)]
    [InlineData(
        "data",
        "size",
        (int)TpFlashHeaderFieldSubject.Data,
        (int)TpFlashHeaderFieldRole.Size)]
    [InlineData(
        "firmware-config",
        "size",
        (int)TpFlashHeaderFieldSubject.FirmwareConfig,
        (int)TpFlashHeaderFieldRole.Size)]
    [InlineData(
        "ctrlram",
        "size",
        (int)TpFlashHeaderFieldSubject.CtrlRam,
        (int)TpFlashHeaderFieldRole.Size)]
    [InlineData(
        "mp-ctrlram",
        "size",
        (int)TpFlashHeaderFieldSubject.MpCtrlRam,
        (int)TpFlashHeaderFieldRole.Size)]
    [InlineData(
        "dlm-difference",
        "tp-bin-start-address",
        (int)TpFlashHeaderFieldSubject.DlmDifference,
        (int)TpFlashHeaderFieldRole.TpBinStartAddress)]
    [InlineData(
        "header",
        "option",
        (int)TpFlashHeaderFieldSubject.Header,
        (int)TpFlashHeaderFieldRole.Option)]
    public void NormalizeMapsClosedTpHeaderSemanticTokens(
        string subject,
        string role,
        int expectedSubjectValue,
        int expectedRoleValue)
    {
        var expectedSubject = (TpFlashHeaderFieldSubject)expectedSubjectValue;
        var expectedRole = (TpFlashHeaderFieldRole)expectedRoleValue;
        FirmwareTpFlashHeaderDocument payload = TpFlashHeaderPayload();
        FirmwareTpFlashHeaderFieldSemanticsDocument first = payload.FieldSemantics[0];
        payload = payload with
        {
            FieldSemantics =
            [
                first with
                {
                    Subject = subject,
                    Role = role,
                    StoredAddress = role switch
                    {
                        "destination-address" =>
                            new FirmwareTpFlashHeaderStoredAddressDocument(
                                "sram",
                                "absolute"),
                        "tp-bin-start-address" =>
                            new FirmwareTpFlashHeaderStoredAddressDocument(
                                "tp-bin",
                                "tp-bin-offset"),
                        _ => null,
                    },
                },
                .. payload.FieldSemantics.Skip(1),
            ],
        };
        FirmwareFamilyDocument document = WithTpFlashHeader(
            Document(includePredicate: false),
            "tp-flash-header",
            payload);

        FirmwareFamilyResolutionDefinition definition =
            FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash);

        FirmwareMetadataStructure structure =
            Assert.Single(definition.GetStructuresForMap("map"));
        FirmwareTpFlashHeaderDefinition typed = Assert.IsType<FirmwareTpFlashHeaderDefinition>(
            structure.Definition.TypedDefinition);
        FirmwareTpFlashHeaderFieldSemantics semantics =
            Assert.Single(typed.FieldSemantics, static candidate => candidate.FieldId == "raw");
        Assert.Equal(expectedSubject, semantics.Subject);
        Assert.Equal(expectedRole, semantics.Role);
        if (expectedRole is
            TpFlashHeaderFieldRole.DestinationAddress or
            TpFlashHeaderFieldRole.TpBinStartAddress)
        {
            Assert.NotNull(semantics.StoredAddress);
        }
        else
        {
            Assert.Null(semantics.StoredAddress);
        }
    }

    /// <summary>Verifies subject and role tokens remain closed at the wire boundary.</summary>
    [Theory]
    [InlineData("unknown-subject", "size", "subject")]
    [InlineData("dlm", "unknown-role", "role")]
    public void NormalizeRejectsUnknownTpHeaderSemanticTokens(
        string subject,
        string role,
        string invalidProperty)
    {
        FirmwareTpFlashHeaderDocument payload = TpFlashHeaderPayload();
        FirmwareTpFlashHeaderFieldSemanticsDocument first = payload.FieldSemantics[0];
        payload = payload with
        {
            FieldSemantics =
            [
                first with { Subject = subject, Role = role },
                .. payload.FieldSemantics.Skip(1),
            ],
        };
        FirmwareFamilyDocument document = WithTpFlashHeader(
            Document(includePredicate: false),
            "tp-flash-header",
            payload);

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal(
            $"metadataSets[metadata].structures[0].tpFlashHeader.fieldSemantics[0].{invalidProperty}",
            exception.Path);
    }

    /// <summary>Stored-address basis tokens remain closed at the wire boundary.</summary>
    [Fact]
    public void NormalizeRejectsUnknownTpHeaderStoredAddressBasis()
    {
        FirmwareTpFlashHeaderDocument payload = TpFlashHeaderPayload();
        FirmwareTpFlashHeaderFieldSemanticsDocument first =
            payload.FieldSemantics[0] with
            {
                Role = "destination-address",
                StoredAddress = new FirmwareTpFlashHeaderStoredAddressDocument(
                    "sram",
                    "relative-somehow"),
            };
        payload = payload with
        {
            FieldSemantics = [first, .. payload.FieldSemantics.Skip(1)],
        };
        FirmwareFamilyDocument document = WithTpFlashHeader(
            Document(includePredicate: false),
            "tp-flash-header",
            payload);

        FirmwareFamilyNormalizationException exception =
            Assert.Throws<FirmwareFamilyNormalizationException>(() =>
                FirmwareFamilyResolutionNormalizer.Normalize(document, FamilyHash));

        Assert.Equal(
            "metadataSets[metadata].structures[0].tpFlashHeader.fieldSemantics[0].storedAddress.basis",
            exception.Path);
    }

    private static FirmwareFamilyDocument WithTpFlashHeader(
        FirmwareFamilyDocument source,
        string? structureKind,
        FirmwareTpFlashHeaderDocument? payload)
    {
        FirmwareMetadataSetDocument metadataSet = Assert.Single(source.MetadataSets);
        FirmwareMetadataStructureDocument structure = Assert.Single(metadataSet.Structures);
        return source with
        {
            MetadataSets =
            [
                metadataSet with
                {
                    Structures =
                    [
                        structure with
                        {
                            StructureKind = structureKind,
                            TpFlashHeader = payload,
                        },
                    ],
                },
            ],
        };
    }

    private static FirmwareTpFlashHeaderDocument TpFlashHeaderPayload()
    {
        return new FirmwareTpFlashHeaderDocument(
            [
                new FirmwareMetadataNamedSpanDocument("header", Range(0, 2)),
                new FirmwareMetadataNamedSpanDocument("dlm", Range(2, 2)),
            ],
            [
                new FirmwareTpFlashHeaderFieldSemanticsDocument(
                    "raw",
                    "header",
                    "header",
                    "integrity-value"),
                new FirmwareTpFlashHeaderFieldSemanticsDocument(
                    "label",
                    "header",
                    "header",
                    "option"),
                new FirmwareTpFlashHeaderFieldSemanticsDocument(
                    "chip-number",
                    "dlm",
                    "dlm",
                    "size",
                    Number("0")),
                new FirmwareTpFlashHeaderFieldSemanticsDocument(
                    "signed-offset",
                    "dlm",
                    "dlm",
                    "size",
                    Number("1")),
            ],
            [
                new FirmwareMetadataFieldSeriesDocument(
                    "dlm-records",
                    [
                        new FirmwareMetadataFieldSeriesMemberDocument(Number("0"), "chip-number"),
                        new FirmwareMetadataFieldSeriesMemberDocument(Number("1"), "signed-offset"),
                    ],
                    [
                        new FirmwareMetadataFieldSeriesApplicabilityDocument(
                            Number("1"),
                            [Number("0")]),
                        new FirmwareMetadataFieldSeriesApplicabilityDocument(
                            Number("2"),
                            [Number("0"), Number("1")]),
                    ]),
            ],
            [
                new FirmwareMetadataFieldGroupDocument(
                    "header-values",
                    ["raw", "label"],
                    []),
                new FirmwareMetadataFieldGroupDocument(
                    "dlm-values",
                    [],
                    ["dlm-records"]),
            ]);
    }
}
