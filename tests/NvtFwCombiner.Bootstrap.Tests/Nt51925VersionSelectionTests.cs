// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json;
using System.Security.Cryptography;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Synthetic TP FWConfig selection through the actual NT51925 candidate bundle.</summary>
public sealed class Nt51925VersionSelectionTests
{
    /// <summary>The canonical TP reader and CtrlRAM Backup reader agree on the same FWConfig bytes.</summary>
    [Theory]
    [InlineData(1, 4, 1, "nt51925-standard-merge-256k")]
    [InlineData(2, 0, 0, "nt51925-standard-merge-fw2-256k")]
    [InlineData(2, 7, 9, "nt51925-standard-merge-fw2-256k")]
    public void TpAndBackupReadersAgreeAndSelectDeclaredMap(byte major, byte minor, byte patch, string expectedMap)
    {
        byte[] tp = CreateTp(major, minor, patch);
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51925"].GetFirmwareFamily();
        var inputs = new FirmwareMapResolutionInputs("NT51925", ExperienceIds.StandardMerge, 0x40000, null,
            [new FirmwareArtifactPayload("tp-input", tp)]);
        FirmwareMetadataStructureResolution read = family.ResolveMetadataStructure(
            "nt51925-standard-merge-256k", CommonFirmwareVersionMetadata.StructureId, inputs);
        Assert.True(CommonFirmwareVersionMetadata.TryRead(read.Resolved!.DecodedStructure, out CommonFirmwareVersion version));
        CanonicalCapabilityCatalog catalog = ProductionCatalog(authorable: true);
        Assert.True(BuiltInFirmwareInspection.TryReadFirmwareConfigBackupMetadata(
            new CanonicalCapabilityExperience(catalog, catalog), "NT51925", tp, out FirmwareConfigMetadata backup));
        Assert.Equal(backup.CommonFwVersion, version.ToString());
        Assert.Equal(expectedMap, CompileStandard(tp).V2Details.Provenance.ResolvedMap.ImageMap.MapId);
    }

    /// <summary>Every operation field and initialization is identical across slots and frozen task-base definitions.</summary>
    [Fact]
    public void BothVersionMapsKeepTheBaseCopyOperationList()
    {
        CompiledComposition first = CompileStandard(CreateTp(1, 4, 1));
        CompiledComposition second = CompileStandard(CreateTp(2, 0, 0));
        CompiledComposition baseline = CompileBaseDefinitions();
        Assert.Equal(JsonSerializer.Serialize(baseline.Plan.OrderedOperations), JsonSerializer.Serialize(first.Plan.OrderedOperations));
        Assert.Equal(JsonSerializer.Serialize(first.Plan.OrderedOperations), JsonSerializer.Serialize(second.Plan.OrderedOperations));
        Assert.Equal(JsonSerializer.Serialize(first.Plan.Initializations), JsonSerializer.Serialize(second.Plan.Initializations));
        Assert.Equal(new ByteRange(0, 0x30000), first.Plan.OrderedOperations[0].TargetRange);
        Assert.Equal(new ByteRange(0x3E000, 0x2000), first.Plan.OrderedOperations[1].TargetRange);
    }

    /// <summary>Complete synthetic output bytes match the same inputs compiled with frozen base definitions.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SelectedMapOutputIsByteIdenticalToTaskBase(byte major)
    {
        byte[] tp = CreateTp(major, 0, 0);
        byte[] dp = [.. Enumerable.Range(0, 0x40000).Select(static index => (byte)(index % 251))];
        var input = new CompositionExecutionInput(new Dictionary<string, byte[]> { ["tp-input"] = tp, ["dp-input"] = dp });
        CompositionExecutionResult expected = CompositionEngine.Execute(CompileBaseDefinitions().Plan, input);
        CompositionExecutionResult actual = CompositionEngine.Execute(CompileStandard(tp).Plan, input);

        Assert.Equal(CompositionExecutionStatus.Succeeded, expected.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, actual.Status);
        Assert.Equal(expected.OutputBytes.ToArray(), actual.OutputBytes.ToArray());
    }

    /// <summary>Version-only selection retains the typed value without the TP SHA.</summary>
    [Fact]
    public void UnrelatedTpChangesDoNotChangeResolvedMapFingerprint()
    {
        byte[] tp = CreateTp(2, 0, 0);
        CompiledComposition first = CompileStandard(tp);
        tp[0x1000] = 0x77;
        CompiledComposition changed = CompileStandard(tp);
        Assert.Empty(first.V2Details.Provenance.ResolvedMap.ArtifactIdentities);
        Assert.Equal(first.V2Details.Provenance.ResolvedMap.ResolutionFingerprint,
            changed.V2Details.Provenance.ResolvedMap.ResolutionFingerprint);
    }

    /// <summary>Swapping an equal-length TP reselects the exact capability before retained input inspection.</summary>
    [Fact]
    public void RetainedAuthoringCapabilityReselectsAfterTpSwap()
    {
        (StandardMergeAuthoringExperience authoring, CanonicalCapabilityCompilerAdapter compiler) = AuthorableTestRoute();
        Assert.True(compiler.TryCompileStandardMerge("NT51925", CreateTp(1, 4, 1), ["tp-input", "dp-input"],
            out _, out ResolvedCapability? retained, out IReadOnlyList<CompositionIssue> issues));
        Assert.Empty(issues);
        FirmwareInspectionSnapshotInput[] inputs = [
            new("tp", "synthetic-tp.bin", StandardMergeAddressSpaceId: "tp-input", AuthoringRevision: 1, ExactCapability: retained),
            new("dp", "synthetic-dp.bin", StandardMergeAddressSpaceId: "dp-input", AuthoringRevision: 1, ExactCapability: retained)];
        FirmwareInspectionStatusBatch result = authoring.InspectInputSlots("NT51925", inputs,
            path => path == "synthetic-tp.bin" ? CreateTp(2, 0, 0) : new byte[0x40000]);

        Assert.Empty(result.Issues);
        ResolvedCapability exact = Assert.Single(result.Catalog!.Routes).ExactCapability!;
        Assert.Equal("nt51925-standard-merge-fw2-256k", exact.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.NotSame(retained, exact);
    }

    /// <summary>A swapped unreadable TP cannot retain the previous exact map.</summary>
    [Fact]
    public void RetainedAuthoringCapabilityRejectsUnreadableReplacement()
    {
        (StandardMergeAuthoringExperience authoring, CanonicalCapabilityCompilerAdapter compiler) = AuthorableTestRoute();
        Assert.True(compiler.TryCompileStandardMerge("NT51925", CreateTp(1, 4, 1), ["tp-input", "dp-input"],
            out _, out ResolvedCapability? retained, out _));
        FirmwareInspectionStatusBatch result = authoring.InspectInputSlots("NT51925",
            [new("tp", "synthetic-tp.bin", StandardMergeAddressSpaceId: "tp-input", AuthoringRevision: 1, ExactCapability: retained),
             new("dp", "synthetic-dp.bin", StandardMergeAddressSpaceId: "dp-input", AuthoringRevision: 1, ExactCapability: retained)],
            path => path == "synthetic-tp.bin" ? new byte[0x30000] : new byte[0x40000]);

        Assert.Contains(result.Issues, static issue => issue.Code == "profile.v2.compile.common-fw-version-required");
        Assert.Null(Assert.Single(result.Catalog!.Routes).ExactCapability);
    }

    /// <summary>The profile compiler preserves version failures as typed issues without a fallback map.</summary>
    [Theory]
    [InlineData(false, "profile.v2.compile.common-fw-version-required")]
    [InlineData(true, "profile.v2.compile.common-fw-version-below-minimum")]
    public void CompilerRejectsUnreadableAndBelowMinimumVersion(bool readable, string expectedCode)
    {
        byte[] tp = readable ? CreateTp(0, 9, 9) : new byte[0x30000];
        BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51925"].TryCompile(null, null, ["tp-input", "dp-input"],
            [new FirmwareArtifactPayload("tp-input", tp)], out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Null(composition);
        Assert.Equal(expectedCode, Assert.Single(issues).Code);
    }

    internal static byte[] CreateTp(byte major, byte minor, byte patch)
    {
        byte[] tp = new byte[0x30000];
        tp[0x2F000 + FirmwareConfigLayout.CommonFwMajorVersionOffset] = major;
        tp[0x2F000 + FirmwareConfigLayout.CommonFwMinorVersionOffset] = minor;
        tp[0x2F000 + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = patch;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x2FFFC);
        return tp;
    }

    internal static CompiledComposition CompileStandard(byte[] tp)
    {
        BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51925"].TryCompile(null, null, ["tp-input", "dp-input"],
            [new FirmwareArtifactPayload("tp-input", tp)], out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return Assert.IsType<CompiledComposition>(composition);
    }

    private static CompiledComposition CompileBaseDefinitions()
    {
        byte[] familyBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata/profiles/nt51925-version-selection-base/family.json"));
        byte[] profileBytes = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata/profiles/nt51925-version-selection-base/profile.json"));
        const string familyHash = "8ea45c0eb951defaa4d4b4ee211a9d97cc814eeba7d39021cb622201d9d74440";
        const string profileHash = "e8fc07076e6cefaae6584264a31d3a088bf086ac9afe46274a4b2601d039b2bc";
        Assert.Equal(familyHash, Convert.ToHexStringLower(SHA256.HashData(familyBytes)));
        Assert.Equal(profileHash, Convert.ToHexStringLower(SHA256.HashData(profileBytes)));
        using JsonDocument family = JsonDocument.Parse(familyBytes);
        using JsonDocument profile = JsonDocument.Parse(profileBytes);
        TrustedProfileBundleCatalog catalog = TrustedProfileBundleCatalogFactory.Create(new string('a', 64),
            new ProfileBundleIdentity("nt51925-standard-merge-candidate", "1.2.2-nt51925.1",
                "b2d8211d2f4b1d9eca4b2d3c4cde3d3d898567771f8274eca9430daa22f580af", "built-in-profile-bundle-v2"),
            [(new("base-family", "families/base.json", "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json", familyHash), family.RootElement)],
            [(new("base-profile", "profiles/base.json", "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json", profileHash), profile.RootElement)],
            BuiltInCanonicalMetadataDefinitionResolver.Instance);
        V2CompositionPlanCompileResult result = catalog.Compile("nt51925-standard-merge-gen-flash", "0.1.0", "NT51925",
            ExperienceIds.StandardMerge, 0x40000, null, []);
        Assert.Empty(result.Issues);
        return Assert.IsType<CompiledComposition>(result.CompiledComposition);
    }


    private static CanonicalCapabilityCatalog ProductionCatalog(bool authorable = false)
    {
        CanonicalCapabilityPolicySnapshot original = BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicySnapshot policy = !authorable ? original : original with
        {
            Routes = [.. original.Routes.Select(route =>
            route.Identity.IcId == "NT51925" && route.Identity.WorkflowId == ExperienceIds.StandardMerge
                ? route with { Authoring = new(route.Authoring.DecisionId, route.Identity.RouteId, route.CapabilityFingerprint,
                    CapabilityAuthoringAvailability.Available, "synthetic-test-only") } : route)]
        };
        var catalog = new CanonicalCapabilityCatalog(CompositionHostServices.CreateCanonicalCapabilityCatalogSource(() => policy));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        return catalog;
    }

    private static (StandardMergeAuthoringExperience Authoring, CanonicalCapabilityCompilerAdapter Compiler) AuthorableTestRoute()
    {
        CanonicalCapabilityCatalog catalog = ProductionCatalog(authorable: true);
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, new BuiltInV2DynamicCompilationAdapter());
        return (new StandardMergeAuthoringExperience(compiler, catalog, new CanonicalCapabilityExperience(catalog, catalog)), compiler);
    }
}
