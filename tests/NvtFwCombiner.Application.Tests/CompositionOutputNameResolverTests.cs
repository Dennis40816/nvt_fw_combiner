using System.Security.Cryptography;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests compiled normal output naming over one accepted canonical inspection snapshot.</summary>
public sealed partial class CompositionOutputNameResolverTests
{
    private const int CapacityBytes = 0x40;
    private const string ArtifactBindingId = "input";
    private const string ArtifactSlotId = "input-slot";
    private const string OutputNamingRouteId = "test-output-naming-route";
    private const string CapabilityFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FamilyHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset RunTime =
        new(2026, 7, 28, 23, 59, 0, TimeSpan.Zero);

    /// <summary>Normal FlashCode uses only canonical DPCMI/FirmwareConfig facts and the UTC run date.</summary>
    [Fact]
    public void NormalFlashCodeUsesAcceptedCanonicalFacts()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            output,
            output.FileNameTemplate,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [fixture.InputSummary],
            RunTime);

        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            resolved.FileName);
        OutputNamingSummary summary = Assert.IsType<OutputNamingSummary>(resolved.Summary);
        Assert.False(summary.IsExplicitOverride);
        Assert.Equal(resolved.FileName, summary.ActualFileName);
        Assert.Collection(
            summary.Tokens,
            token => AssertToken(token, "ic", "NT51929", known: true, null),
            token => AssertToken(token, "dp-version", "8205", known: true, fixture.Artifact.Identity.Sha256),
            token => AssertToken(token, "tp-version", "8004", known: true, fixture.Artifact.Identity.Sha256),
            token => AssertToken(token, "date", "20260728", known: true, null));
        Assert.Empty(resolved.Issues);
    }

    /// <summary>The compiled binding id selects one DPCMI result even when another canonical structure is inspected.</summary>
    [Fact]
    public void NormalFlashCodeUsesExactCompiledMetadataBinding()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        MetadataPlanEntry selected = fixture.Plan.Entries.Single(entry =>
            StringComparer.Ordinal.Equals(
                entry.Definition.BindingId,
                "dpcmi-naming")).Definition;
        var duplicate = new MetadataPlanEntry(
            "other-dpcmi-naming",
            selected.SpaceId,
            selected.SlotId,
            selected.FamilyDefinition,
            selected.ResolvedMap,
            selected.MetadataSetBinding,
            selected.StructureDefinition,
            selected.TargetReferences,
            selected.Purposes,
            selected.EvidenceRefs);
        ResolvedMetadataPlan plan = new MetadataPlanDefinition(
            fixture.Plan.Entries
                .Select(static entry => entry.Definition)
                .Append(duplicate))
            .Resolve(new ResolutionToken("duplicate-dpcmi-publication"));
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(
                plan,
                fixture.Snapshot.AuthoringRevision,
                [fixture.Artifact]));
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            CapabilityFingerprint,
            plan,
            snapshot);

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            accepted,
            [fixture.InputSummary],
            RunTime);

        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            resolved.FileName);
        Assert.Empty(resolved.Issues);
    }

    /// <summary>A TP-firmware rule omits DP by contract instead of inventing a missing DP token.</summary>
    [Fact]
    public void TpFirmwareUsesOnlyDeclaredTpVersion()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: false);
        CompiledOutputNamingRequirement output = TpFirmwareOutput();

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51950",
            output,
            output.FileNameTemplate,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [fixture.InputSummary],
            RunTime);

        Assert.Equal("NT51950_TPFW_T8004_20260728.bin", resolved.FileName);
        Assert.DoesNotContain(
            resolved.Summary!.Tokens,
            static token => token.TokenId == "dp-version");
        Assert.Empty(resolved.Issues);
    }

    /// <summary>Missing DPCMI uses only the placeholder declared by the compiled normal rule.</summary>
    [Fact]
    public void MissingMetadataUsesCompiledPlaceholderAndTypedWarning()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: false);

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [fixture.InputSummary],
            RunTime);

        Assert.Equal(
            "NT51929_FlashCode_DxxxxT8004_20260728.bin",
            resolved.FileName);
        CompositionIssue issue = Assert.Single(resolved.Issues);
        Assert.Equal("output-naming.metadata-unknown", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
    }

    /// <summary>An explicit literal is effective while the automatic canonical candidate remains auditable.</summary>
    [Fact]
    public void ExplicitOverrideWinsWithoutCreatingASecondAutomaticIdentity()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            "operator-output.bin",
            isExplicitOverride: true,
            fixture.AcceptedInspection,
            [fixture.InputSummary],
            RunTime);

        Assert.Equal("operator-output.bin", resolved.FileName);
        Assert.Equal("operator-output.bin", resolved.Summary!.ActualFileName);
        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            resolved.Summary.AutomaticFileName);
        Assert.True(resolved.Summary.IsExplicitOverride);
    }

    /// <summary>A snapshot from different bytes is rejected before any of its values can name output.</summary>
    [Fact]
    public void DifferentAcceptedInputIdentityBlocksNameResolution()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        var changedSummary = new InputArtifactSummary(
            ArtifactBindingId,
            "changed-artifact",
            fixture.InputSummary.Size,
            new string('c', 64));

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [changedSummary],
            RunTime);

        Assert.Null(resolved.Summary);
        CompositionIssue issue = Assert.Single(resolved.Issues);
        Assert.Equal("output-naming.inspection-stale", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
    }

    /// <summary>Metadata outside an accepted execution prefix cannot influence a committed name.</summary>
    [Fact]
    public void MetadataOutsideAcceptedExecutionPrefixBlocksNameResolution()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        const int AcceptedBytes = 4;
        string acceptedSha256 = Convert.ToHexStringLower(
            SHA256.HashData(fixture.Bytes.AsSpan(0, AcceptedBytes)));
        var prefixSummary = new InputArtifactSummary(
            ArtifactBindingId,
            "input-artifact",
            fixture.InputSummary.Size,
            fixture.InputSummary.Sha256,
            executionSnapshot: new InputArtifactExecutionSnapshotSummary(
                new ByteRange(0, AcceptedBytes),
                acceptedSha256,
                new ByteRange(AcceptedBytes, CapacityBytes - AcceptedBytes)));

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [prefixSummary],
            RunTime);

        Assert.Null(resolved.Summary);
        CompositionIssue issue = Assert.Single(resolved.Issues);
        Assert.Equal("output-naming.inspection-stale", issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
    }

    /// <summary>Token provenance records the exact execution prefix when an outer source tail is ignored.</summary>
    [Fact]
    public void MetadataInsideAcceptedExecutionPrefixUsesPrefixIdentity()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        const int AcceptedBytes = 32;
        string acceptedSha256 = Convert.ToHexStringLower(
            SHA256.HashData(fixture.Bytes.AsSpan(0, AcceptedBytes)));
        var prefixSummary = new InputArtifactSummary(
            ArtifactBindingId,
            "input-artifact",
            fixture.InputSummary.Size,
            fixture.InputSummary.Sha256,
            executionSnapshot: new InputArtifactExecutionSnapshotSummary(
                new ByteRange(0, AcceptedBytes),
                acceptedSha256,
                new ByteRange(AcceptedBytes, CapacityBytes - AcceptedBytes)));

        OutputNameResolution resolved = CompiledOutputNameResolver.ResolveNormal(
            "NT51929",
            NormalFlashCodeOutput(),
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            isExplicitOverride: false,
            fixture.AcceptedInspection,
            [prefixSummary],
            RunTime);

        Assert.Equal(
            "NT51929_FlashCode_D8205T8004_20260728.bin",
            resolved.FileName);
        Assert.All(
            resolved.Summary!.Tokens.Where(static token =>
                token.TokenId is "dp-version" or "tp-version"),
            token => Assert.Equal(acceptedSha256, token.AcceptedSnapshotSha256));
    }

    /// <summary>A publication token mismatch cannot be wrapped as an accepted naming snapshot.</summary>
    [Fact]
    public void DifferentResolutionPublicationCannotBecomeAcceptedInspection()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        ResolvedMetadataPlan differentPlan = fixture.Plan.Definition.Resolve(
            new ResolutionToken("different-publication"));

        _ = Assert.Throws<ArgumentException>(() => new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            CapabilityFingerprint,
            differentPlan,
            fixture.Snapshot));
    }

    /// <summary>The public acceptance boundary pins the exact compilation and requires current publication state.</summary>
    [Fact]
    public void AcceptanceFactoryPinsCompilationAndRejectsStaleAuthoringRevision()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        CapabilityRouteIdentity route = new(
            "NT51929",
            "standard-merge",
            "none",
            "map");
        string capabilityFingerprint = CapabilityFingerprint;
        var capability = new ResolvedCapability(
            route,
            capabilityFingerprint,
            composition,
            Decision(
                "authoring",
                CapabilityAuthoringAvailability.Available),
            Decision(
                "publication",
                CapabilityPublicationStatus.Supported),
            Decision(
                "evidence",
                CapabilityEvidenceStatus.ContractOnly),
            fixture.Plan,
            fixture.Plan.ResolutionToken);

        var accepted =
            AcceptedOutputNamingInspection.Accept(
                capability,
                fixture.Snapshot,
                currentAuthoringRevision: 7,
                [fixture.Artifact]);
        var admission =
            OutputNamingAdmissionIdentity.Capture(
                capability,
                currentAuthoringRevision: 7);
        string compilationFingerprint =
            capability.CompiledComposition.CompilationFingerprint;

        Assert.NotEqual(capabilityFingerprint, compilationFingerprint);
        Assert.Equal(route.RouteId, accepted.RouteId);
        Assert.Equal(compilationFingerprint, accepted.CompilationFingerprint);
        Assert.Equal(route.RouteId, admission.RouteId);
        Assert.Equal(compilationFingerprint, admission.CompilationFingerprint);
        Assert.Equal(fixture.Plan.ResolutionToken, admission.ResolutionToken);
        Assert.Equal(7, admission.AuthoringRevision);
        _ = Assert.Throws<ArgumentException>(() =>
            AcceptedOutputNamingInspection.Accept(
                capability,
                fixture.Snapshot,
                currentAuthoringRevision: 8,
                [fixture.Artifact]));

        PinnedCapabilityDecision<TValue> Decision<TValue>(
            string decisionId,
            TValue value)
            where TValue : struct, Enum
        {
            return new PinnedCapabilityDecision<TValue>(
                decisionId,
                route.RouteId,
                capabilityFingerprint,
                value,
                "synthetic-output-naming");
        }
    }

    private static InspectionFixture CreateInspectionFixture(bool includeDpcmi)
    {
        byte[] bytes = new byte[CapacityBytes];
        bytes[0] = 0x34;
        bytes[1] = 0x82;
        bytes[2] = 0x51;
        WriteFirmwareConfig(bytes.AsSpan(8));
        var artifact = new FirmwareArtifactPayload(ArtifactBindingId, bytes);
        FirmwareMetadataStructure firmwareConfig = CreateFirmwareConfigStructure();
        List<FirmwareMetadataStructure> structures = [firmwareConfig];
        if (includeDpcmi)
        {
            structures.Add(CreateDpcmiStructure());
        }

        var metadataSet = new FirmwareMetadataSet(
            "output-naming-metadata",
            structures,
            ["synthetic-output-naming"]);
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT51929"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                CapacityBytes),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [
                new FirmwareRegionSet(
                    "physical",
                    "flash",
                    [
                        new FirmwareRegion(
                            "flash-image",
                            parentRegionId: null,
                            FirmwareRegionOwner.System,
                            FirmwareRegionKind.Image,
                            new ByteRange(0, CapacityBytes),
                            FirmwareWriteConstraint.Forbidden),
                    ],
                    ["synthetic-output-naming"]),
            ],
            [metadataSet],
            ["synthetic-output-naming"]);
        var family = new FirmwareFamilyResolutionDefinition(
            "synthetic-output-naming",
            "1.0.0",
            FamilyHash,
            [map],
            [metadataSet]);
        ResolvedFirmwareImageMap resolvedMap = Assert.IsType<ResolvedFirmwareImageMap>(
            family.ResolveMap(new FirmwareMapResolutionInputs(
                "NT51929",
                "standard",
                CapacityBytes,
                requestedTopology: null,
                artifacts: [artifact])).ResolvedMap);
        FirmwareMapFactBinding<FirmwareMetadataSet> metadataBinding =
            Assert.Single(map.MetadataSetBindings);
        MetadataPlanEntry[] entries =
        [
            .. structures.Select(structure => new MetadataPlanEntry(
                $"{structure.StructureId}-naming",
                ArtifactBindingId,
                ArtifactSlotId,
                family,
                resolvedMap,
                metadataBinding,
                structure,
                structure.Fields.Select(static field => field.FieldId),
                [MetadataReferencePurpose.OutputNaming])),
        ];
        ResolvedMetadataPlan plan = new MetadataPlanDefinition(entries)
            .Resolve(new ResolutionToken("output-naming-publication"));
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.Inspect(
            new MetadataInspectionRequest(plan, authoringRevision: 7, [artifact]));
        var accepted = new AcceptedOutputNamingInspection(
            OutputNamingRouteId,
            CapabilityFingerprint,
            plan,
            snapshot);
        var summary = new InputArtifactSummary(
            ArtifactBindingId,
            "input-artifact",
            artifact.LengthBytes,
            artifact.Identity.Sha256);
        return new InspectionFixture(
            bytes,
            artifact,
            plan,
            snapshot,
            accepted,
            summary);
    }

    private static CompiledComposition CreateRuntimeComposition(
        InspectionFixture fixture)
    {
        ResolvedMetadataPlanEntry first = fixture.Plan.Entries[0];
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "output-naming-bundle",
                "1.0.0",
                new string('d', 64),
                "output-naming-release"),
            new ProfileBundleEntryIdentity(
                "output-naming-profile-entry",
                new string('e', 64)),
            first.Definition.ResolvedMap,
            new CompiledProfilePromotion(
                CompiledProfilePromotionStage.Supported,
                []),
            ["synthetic-output-naming"],
            [],
            []);
        var inputContract = new CompiledInputContract(
            [
                CompiledInputSlotTestFactory.Create(
                    ArtifactSlotId,
                    "input",
                    CompiledInputArtifactClass.ReferenceImage,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(CapacityBytes),
                    new CompiledNoInputNormalization()),
            ],
            [
                new CompiledInputSpaceBinding(
                    ArtifactBindingId,
                    ArtifactSlotId,
                    CompiledInputInstancePolicy.Singleton),
            ]);
        CompiledOutputNamingRequirement output = NormalFlashCodeOutput();
        var details = new V2CompiledCompositionDetails(
            "output-naming-profile",
            "1.0.0",
            "standard-merge",
            CompositionKind.Merge,
            provenance,
            inputContract,
            new CompiledRegionAccessContract([], []),
            output);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output", CapacityBytes, 0),
            [
                new AddressSpace(
                    ArtifactBindingId,
                    CapacityBytes,
                    AddressSpaceMutability.Immutable,
                    allowedInputLengths: [CapacityBytes]),
                new AddressSpace(
                    "output",
                    CapacityBytes,
                    AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-input",
                    10,
                    ArtifactBindingId,
                    new ByteRange(0, CapacityBytes),
                    "output",
                    new ByteRange(0, CapacityBytes),
                    OverlapPolicy.Reject,
                    "copy accepted naming input"),
            ]);
        return CompiledComposition.CreateV2RuntimeExecutable(plan, details);
    }

    private static InputArtifactBinding CreateInputBinding()
    {
        return new InputArtifactBinding(
            ArtifactBindingId,
            ArtifactBindingId,
            "input-artifact",
            "input.bin",
            CompiledInputArtifactClass.ReferenceImage);
    }

    private static OutputNamingAdmissionIdentity CreateAdmission(
        CompiledComposition composition,
        InspectionFixture fixture)
    {
        return new OutputNamingAdmissionIdentity(
            fixture.AcceptedInspection.RouteId,
            composition.CompilationFingerprint,
            fixture.Plan.ResolutionToken,
            fixture.Snapshot.AuthoringRevision);
    }

    private static FirmwareMetadataStructure CreateDpcmiStructure()
    {
        return new FirmwareMetadataStructure(
            DpcmiMetadataContract.StructureId,
            ArtifactBindingId,
            lengthBytes: 3,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(0, 3)),
                "flash-image"),
            [
                Unsigned(DpcmiMetadataContract.JiraLowFieldId, 0),
                Unsigned(DpcmiMetadataContract.MajorVersionFieldId, 1),
                Unsigned(
                    DpcmiMetadataContract.MinorVersionFieldId,
                    2,
                    new FirmwareMetadataBitSlice(4, 4)),
                Unsigned(
                    DpcmiMetadataContract.JiraHighFieldId,
                    2,
                    new FirmwareMetadataBitSlice(0, 4)),
            ],
            assertions: []);
    }

    private static FirmwareMetadataStructure CreateFirmwareConfigStructure()
    {
        FirmwareMetadataField[] fields =
        [
            Unsigned(FirmwareConfigGeneralParametersContract.TpFirmwareVersion, 0),
            Unsigned(FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement, 1),
            Unsigned(FirmwareConfigGeneralParametersContract.SensorCountX, 2),
            Unsigned(FirmwareConfigGeneralParametersContract.SensorCountY, 3),
            Unsigned(FirmwareConfigGeneralParametersContract.DisplayResolutionX, 4, widthBytes: 2),
            Unsigned(FirmwareConfigGeneralParametersContract.DisplayResolutionY, 6, widthBytes: 2),
            Unsigned(FirmwareConfigGeneralParametersContract.MaximumOperableFingers, 8),
            Unsigned(FirmwareConfigGeneralParametersContract.ReportIrqType, 9),
            Unsigned(FirmwareConfigGeneralParametersContract.TpFirmwareSubVersion, 10),
            Unsigned(FirmwareConfigGeneralParametersContract.TpResolutionX, 11, widthBytes: 2),
            Unsigned(FirmwareConfigGeneralParametersContract.TpResolutionY, 13, widthBytes: 2),
            Unsigned(FirmwareConfigGeneralParametersContract.ObservedIcCount, 15),
            Unsigned(FirmwareConfigGeneralParametersContract.OutermostIcMasterEnable, 16),
            Unsigned(FirmwareConfigGeneralParametersContract.CommonFirmwareMajorVersion, 17),
            Unsigned(FirmwareConfigGeneralParametersContract.CommonFirmwareMinorVersion, 18),
            Unsigned(FirmwareConfigGeneralParametersContract.CommonFirmwareAdditionalVersion, 19),
            Unsigned(FirmwareConfigGeneralParametersContract.Pid, 20, widthBytes: 2),
            Unsigned(FirmwareConfigGeneralParametersContract.CascadeEnable, 22),
        ];
        return new FirmwareMetadataStructure(
            FirmwareConfigGeneralParametersContract.StructureId,
            ArtifactBindingId,
            lengthBytes: 23,
            new FirmwareAbsoluteRangeLocator(
                new FirmwareAddressedRange("flash", new ByteRange(8, 23)),
                "flash-image"),
            fields,
            assertions: [],
            [
                new FirmwareMetadataFieldRelation(
                    FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplementRelation,
                    FirmwareMetadataFieldRelationKind.BitwiseComplement,
                    FirmwareConfigGeneralParametersContract.TpFirmwareVersion,
                    FirmwareConfigGeneralParametersContract.TpFirmwareVersionComplement),
            ]);
    }

    private static FirmwareMetadataField Unsigned(
        string fieldId,
        long offset,
        FirmwareMetadataBitSlice? bitSlice = null,
        int widthBytes = 1)
    {
        return new FirmwareMetadataField(
            fieldId,
            offset,
            widthBytes,
            FirmwareMetadataEncoding.UnsignedInteger,
            FirmwareMetadataByteOrder.LittleEndian,
            bitSlice);
    }

    private static void WriteFirmwareConfig(Span<byte> destination)
    {
        destination[0] = 0x80;
        destination[1] = 0x7F;
        destination[2] = 18;
        destination[3] = 32;
        destination[4] = 0x80;
        destination[5] = 0x07;
        destination[6] = 0x38;
        destination[7] = 0x04;
        destination[8] = 10;
        destination[9] = 2;
        destination[10] = 0x04;
        destination[11] = 0x40;
        destination[12] = 0x0B;
        destination[13] = 0x08;
        destination[14] = 0x07;
        destination[15] = 3;
        destination[16] = 1;
        destination[17] = 4;
        destination[18] = 5;
        destination[19] = 6;
        destination[20] = 0x34;
        destination[21] = 0x12;
        destination[22] = 1;
    }

    private static void AssertToken(
        OutputNamingTokenSummary token,
        string expectedId,
        string expectedValue,
        bool known,
        string? expectedSha256)
    {
        Assert.Equal(expectedId, token.TokenId);
        Assert.Equal(expectedValue, token.Value);
        Assert.Equal(known, token.IsKnown);
        Assert.Equal(expectedSha256, token.AcceptedSnapshotSha256);
    }

    private sealed record InspectionFixture(
        byte[] Bytes,
        FirmwareArtifactPayload Artifact,
        ResolvedMetadataPlan Plan,
        MetadataInspectionSnapshot Snapshot,
        AcceptedOutputNamingInspection AcceptedInspection,
        InputArtifactSummary InputSummary);

    private sealed class RecordingOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            FileName = fileName;
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }
}
