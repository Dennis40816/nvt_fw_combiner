using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.FirmwareFamilies;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    private static readonly JsonSerializerOptions s_sourceEnvelopeJsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>Executes a synthetic Standard profile with a complete DP envelope.</summary>
    [Theory]
    [InlineData(0x37000)]
    [InlineData(0x37001)]
    [InlineData(0x40001)]
    [InlineData(0x100001)]
    public void SourceEnvelopeCompilesAndExecutesCompleteDpWithOnlyTpOverlay(int length)
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        byte[] dp = [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
        byte[] tp = [.. Enumerable.Repeat((byte)0xA5, 0x37000)];
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(catalog, dp, tp, length);
        Assert.True(result.IsCompiled, string.Join("; ", result.Issues.Select(static issue =>
            $"{issue.Code}: {issue.Message}")));

        CompiledComposition compiled = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        SourceEnvelopeExtent envelope = Assert.IsType<SourceEnvelopeExtent>(
            Assert.IsType<ResolvedMapV2CompilationContext>(compiled.V2Details.Provenance.Context).SourceEnvelope);
        Assert.Equal("nt51950-standard-merge-256k", envelope.LayoutTemplateMapId);
        Assert.Equal(0x40000, envelope.LayoutTemplateCapacity);
        Assert.Equal(length, envelope.ActualOutputLength);
        Assert.Equal(length, compiled.Plan.OutputInitialization.Capacity);
        CompiledInputSlotRequirement dpSlot = Assert.Single(
            compiled.V2Details.InputContract.Slots,
            static slot => slot.SlotId == "dp-input");
        Assert.Equal(length, Assert.IsType<CompiledExactBytesInputLengthRequirement>(dpSlot.LengthRequirement).Bytes);

        CompositionExecutionResult execution = CompositionEngine.Execute(
            compiled.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));
        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        byte[] expected = [.. dp];
        Array.Copy(tp, 0xA000, expected, 0xA000, 0x2D000);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
        _ = Assert.Single(execution.Issues, static issue =>
            issue.Code == "DP_NONSTANDARD_SIZE_WARNING" &&
            issue.Severity == CompositionIssueSeverity.Warning);
        Assert.Equal(2, execution.Mutations.Count);
    }

    /// <summary>A short DP may lack display-only DPCMI while retaining its canonical inspection and naming contract.</summary>
    [Fact]
    public void ShortEnvelopeRetainsCanonicalDpcmiInspectionAndPlaceholderNaming()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog(
            preserveCanonicalInspectionMetadata: true);
        byte[] dp = [.. Enumerable.Range(0, 0x37000).Select(index => (byte)(index % 251))];
        byte[] tp = [.. Enumerable.Repeat((byte)0xA5, 0x37000)];
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(catalog, dp, tp, dp.Length);

        Assert.True(result.IsCompiled, string.Join("; ", result.Issues.Select(static issue =>
            $"{issue.Code}: {issue.Message}")));
        CompiledComposition compiled = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        CompiledOutputNamingRequirement naming = compiled.V2Details.OutputNamingRequirement;
        Assert.Equal(CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId, naming.RuleId);
        CompiledOutputTokenRequirement dpVersion = Assert.Single(naming.TokenRequirements,
            static requirement => requirement.TokenId == "dp-version");
        Assert.Equal("nt51950-dpcmi-standard-merge-inspection", dpVersion.MetadataBindingId);
        Assert.Equal(CompiledOutputTokenMissingPolicy.UsePlaceholder, dpVersion.MissingPolicy);
        Assert.Equal("xxxx", dpVersion.Placeholder);

        CompositionExecutionResult execution = CompositionEngine.Execute(compiled.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));
        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        byte[] expected = [.. dp];
        Array.Copy(tp, 0xA000, expected, 0xA000, 0x2D000);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
    }

    /// <summary>Keeps the existing physical-map path for a declared capacity.</summary>
    [Theory]
    [InlineData(0x40000)]
    [InlineData(0x80000)]
    [InlineData(0x100000)]
    public void ExactDeclaredCapacityKeepsPhysicalMapAndExistingLengthTerminal(int length)
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        byte[] dp = [.. Enumerable.Range(0, length).Select(index => (byte)(index % 251))];
        byte[] tp = [.. Enumerable.Repeat((byte)0xA5, 0x37000)];
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(catalog, dp, tp, dp.Length);
        Assert.True(result.IsCompiled, string.Join("; ", result.Issues.Select(static issue =>
            $"{issue.Code}: {issue.Message}")));

        CompiledComposition compiled = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Null(Assert.IsType<ResolvedMapV2CompilationContext>(
            compiled.V2Details.Provenance.Context).SourceEnvelope);
        CompiledInputSlotRequirement dpSlot = Assert.Single(
            compiled.V2Details.InputContract.Slots,
            static slot => slot.SlotId == "dp-input");
        Assert.Equal(dp.Length,
            Assert.IsType<CompiledExactResolvedMapCapacityInputLengthRequirement>(dpSlot.LengthRequirement).Bytes);
        CompositionExecutionResult execution = CompositionEngine.Execute(compiled.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));
        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        byte[] expected = [.. dp];
        Array.Copy(tp, 0xA000, expected, 0xA000, 0x2D000);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
    }

    /// <summary>Only a captured complete DP matching the requested nonstandard extent may enter the template path.</summary>
    [Theory]
    [InlineData(0x40001, 0x40000)]
    [InlineData(0x36fff, 0x36fff)]
    public void EnvelopeRejectsMissingExtentOrMandatoryWriteCoverage(int requestedLength, int dpLength)
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(
            catalog, new byte[dpLength], new byte[0x37000], requestedLength);

        Assert.False(result.IsCompiled);
        Assert.NotEmpty(result.Issues);
    }

    /// <summary>An exact map stays on its original route and runtime rejects a mismatched complete DP.</summary>
    [Fact]
    public void ExactMapDoesNotFallbackAfterInputLengthMismatch()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        byte[] dp = new byte[0x80001];
        byte[] tp = new byte[0x37000];
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(catalog, dp, tp, 0x80000);

        CompiledComposition compiled = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Null(Assert.IsType<ResolvedMapV2CompilationContext>(
            compiled.V2Details.Provenance.Context).SourceEnvelope);
        CompositionExecutionResult rejected = CompositionEngine.Execute(compiled.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));
        Assert.Equal(CompositionExecutionStatus.Failed, rejected.Status);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == CompositionIssueCodes.InputAddressSpaceLengthMismatch);
    }

    /// <summary>The explicit reject policy does not compile a source-sized profile without its DP.</summary>
    [Fact]
    public void RejectPolicyRequiresCapturedDpEvenAtExactMapCapacity()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        V2CompositionPlanCompileResult result = catalog.Compile(
            "nt51950-standard-merge-dp-perspective", "0.7.0", "NT51950", "standard-merge",
            0x40000, requestedTopology: null,
            [new FirmwareArtifactPayload("tp-input", new byte[0x37000])]);

        Assert.False(result.IsCompiled);
        Assert.NotEmpty(result.Issues);
    }

    /// <summary>Only the admitted full-DP seed may write outside the canonical TP overlay.</summary>
    [Fact]
    public void EnvelopeRejectsAnotherWriteOutsideTpOverlay()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog(profile =>
        {
            JsonObject target = Assert.Single(
                Assert.IsType<JsonArray>(profile["views"]).Select(static item => Assert.IsType<JsonObject>(item)),
                static view => view["viewId"]?.GetValue<string>() == "tp-overlay-output");
            target["selector"] = new JsonObject
            {
                ["kind"] = "space-range",
                ["range"] = new JsonObject { ["start"] = 0, ["length"] = 0x2D000 },
            };
        });
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(
            catalog, new byte[0x40001], new byte[0x37000], 0x40001);

        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "profile.v2.source-envelope.non-tp-output-write");
    }

    /// <summary>A template for the other member cannot substitute for this member's canonical layout.</summary>
    [Fact]
    public void EnvelopeRejectsWrongMemberTemplate()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog(profile =>
        {
            Assert.IsType<JsonObject>(profile["sourceEnvelopeBinding"])["layoutTemplateMapId"] =
                "nt51951-standard-merge-256k";
            Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(profile["mapBinding"])["mapIds"])
                .Add("nt51951-standard-merge-256k");
        });
        V2CompositionPlanCompileResult result = CompileStandardEnvelope(
            catalog, new byte[0x40001], new byte[0x37000], 0x40001);

        Assert.False(result.IsCompiled);
        Assert.NotEmpty(result.Issues);
    }

    /// <summary>A rejected topology on a declared exact map does not trigger source-envelope fallback.</summary>
    [Fact]
    public void ExactMapTopologyFailureDoesNotFallbackToTemplate()
    {
        TrustedProfileBundleCatalog catalog = CreateStandardEnvelopeCatalog();
        V2CompositionPlanCompileResult result = catalog.Compile(
            "nt51950-standard-merge-dp-perspective", "0.7.0", "NT51950", "standard-merge",
            0x40000,
            new TopologySelection(2, "cascade", TopologySelectionSource.Requested, "ic-number"),
            [
                new FirmwareArtifactPayload("dp-input", new byte[0x40000]),
                new FirmwareArtifactPayload("tp-input", new byte[0x37000]),
            ]);

        Assert.False(result.IsCompiled);
        Assert.NotEmpty(result.Issues);
    }

    private static V2CompositionPlanCompileResult CompileStandardEnvelope(
        TrustedProfileBundleCatalog catalog,
        byte[] dp,
        byte[] tp,
        int requestedLength)
    {
        return catalog.Compile(
            "nt51950-standard-merge-dp-perspective",
            "0.7.0",
            "NT51950",
            "standard-merge",
            requestedLength,
            requestedTopology: null,
            [new FirmwareArtifactPayload("dp-input", dp), new FirmwareArtifactPayload("tp-input", tp)]);
    }

    private static TrustedProfileBundleCatalog CreateStandardEnvelopeCatalog(
        Action<JsonObject>? mutateProfile = null,
        bool preserveCanonicalInspectionMetadata = false)
    {
        string familyJson = File.ReadAllText(Path.Combine(FindSourceEnvelopeRepositoryRoot(),
            "profiles", "built-in", "nt51950-nt51951-standard-merge", "families",
            "nt51950-nt51951-dp-perspective.json"));
        string profileJson = File.ReadAllText(Path.Combine(FindSourceEnvelopeRepositoryRoot(),
            "profiles", "built-in", "nt51950-nt51951-standard-merge", "profiles",
            "nt51950-standard-merge.json"));
        // The default synthetic fixture isolates map geometry. The short-DP
        // inspection case keeps the shipped metadata and naming declarations.
        JsonObject familyNode = Assert.IsType<JsonObject>(JsonNode.Parse(familyJson));
        if (!preserveCanonicalInspectionMetadata)
        {
            familyNode["metadataSets"] = new JsonArray();
            familyNode["fullImageMetadataViews"] = new JsonArray();
            familyNode["factAliases"] = new JsonArray();
            foreach (JsonNode? map in Assert.IsType<JsonArray>(familyNode["imageMaps"]))
            {
                Assert.IsType<JsonObject>(map)["metadataSetIds"] = new JsonArray();
            }
        }

        familyJson = familyNode.ToJsonString();
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(profileJson));
        profile["schemaVersion"] = "2.16";
        Assert.IsType<JsonObject>(profile["promotion"])["stage"] = "executable-candidate";
        JsonObject mapBinding = Assert.IsType<JsonObject>(profile["mapBinding"]);
        mapBinding["familyContentHash"] = Hash(familyJson);
        if (!preserveCanonicalInspectionMetadata)
        {
            mapBinding["requiredMetadataStructureIds"] = new JsonArray();
            profile["metadataBindings"] = new JsonArray();
            JsonObject outputNaming = Assert.IsType<JsonObject>(profile["output"]);
            outputNaming["ruleId"] = null;
            outputNaming["fileNameTemplate"] = "{ic}_{date}.bin";
            outputNaming["requiredTokenIds"] = new JsonArray("date", "ic");
            JsonArray tokenRequirements = Assert.IsType<JsonArray>(outputNaming["tokenRequirements"]);
            outputNaming["tokenRequirements"] = new JsonArray(
                tokenRequirements[0]!.DeepClone(),
                tokenRequirements[2]!.DeepClone());
        }
        else
        {
            JsonObject dpcmiSet = Assert.Single(
                Assert.IsType<JsonArray>(familyNode["metadataSets"])
                    .Select(static item => Assert.IsType<JsonObject>(item)),
                static set => set["metadataSetId"]?.GetValue<string>() ==
                    "nt51950-dpcmi-standard-merge");
            JsonObject dpcmiStructure = Assert.Single(
                Assert.IsType<JsonArray>(dpcmiSet["structures"])
                    .Select(static item => Assert.IsType<JsonObject>(item)));
            JsonObject locator = Assert.IsType<JsonObject>(dpcmiStructure["locator"]);
            JsonObject singleChipBranch = Assert.Single(
                Assert.IsType<JsonArray>(locator["branches"])
                    .Select(static item => Assert.IsType<JsonObject>(item)),
                static branch => branch["minimumValue"]?.GetValue<int>() == 1 &&
                    branch["maximumValue"]?.GetValue<int>() == 1);
            Assert.True(Assert.IsType<JsonObject>(singleChipBranch["anchorRange"])["start"]!
                .GetValue<int>() >= 0x37000);

            JsonObject dpcmiBinding = Assert.Single(
                Assert.IsType<JsonArray>(profile["metadataBindings"])
                    .Select(static item => Assert.IsType<JsonObject>(item)),
                static binding => binding["bindingId"]?.GetValue<string>() ==
                    "nt51950-dpcmi-standard-merge-inspection");
            string[] purposes = [.. Assert.IsType<JsonArray>(dpcmiBinding["purposes"])
                .Select(static purpose => purpose!.GetValue<string>())];
            Assert.Contains("output-naming", purposes);
            Assert.DoesNotContain("map-resolution", purposes);
            Assert.Contains("nt51950-dpcmi-standard-merge",
                Assert.IsType<JsonArray>(mapBinding["requiredMetadataStructureIds"])
                    .Select(static item => item!.GetValue<string>()));
        }
        profile["sourceEnvelopeBinding"] = new JsonObject
        {
            ["sourceSlotId"] = "dp-input",
            ["layoutTemplateMapId"] = "nt51950-standard-merge-256k",
            ["rootRegionId"] = "dp-container",
            ["whenSourceAbsent"] = "reject",
            ["expectedOuterLengths"] = new JsonArray(0x40000, 0x80000, 0x100000),
            ["unexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
        };
        JsonArray spaces = Assert.IsType<JsonArray>(profile["spaces"]);
        JsonObject output = Assert.Single(spaces.Select(static item => Assert.IsType<JsonObject>(item)),
            static space => space["kind"]?.GetValue<string>() == "output-image");
        output["capacity"] = new JsonObject
        {
            ["kind"] = "source-slot",
            ["sourceSlotId"] = "dp-input",
        };
        mutateProfile?.Invoke(profile);
        string updatedProfileJson = profile.ToJsonString();
        using JsonDocument family = JsonDocument.Parse(familyJson);
        using JsonDocument updatedProfile = JsonDocument.Parse(updatedProfileJson);
        (TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document)[] familySources =
            [Family("standard-family", Hash(familyJson), family.RootElement.Clone())];
        (TrustedProfileBundleCatalogEntryIdentity Identity, JsonElement Document)[] profileSources =
            [Profile("standard-profile", Hash(updatedProfileJson), updatedProfile.RootElement.Clone())];
        return preserveCanonicalInspectionMetadata
            ? TrustedProfileBundleCatalogFactory.Create(
                ManifestHash,
                new ProfileBundleIdentity("bundle", "1.0.0", BundleHash, "release-binding"),
                familySources,
                profileSources,
                CreateCanonicalInspectionDefinitionResolver())
            : CreateCatalogFromSources(familySources, profileSources);
    }

    private static ExactSourceEnvelopeDefinitionResolver CreateCanonicalInspectionDefinitionResolver()
    {
        static FirmwareFamilyDocument ParseProviderDefinitions(string json)
        {
            JsonObject provider = Assert.IsType<JsonObject>(JsonNode.Parse(json));
            // Relationship declarations are unrelated to the canonical metadata definitions.
            // The direct DTO serializer requires its polymorphic discriminator first.
            provider["familyRelationships"] = new JsonArray();
            return Assert.IsType<FirmwareFamilyDocument>(
                JsonSerializer.Deserialize<FirmwareFamilyDocument>(provider.ToJsonString(),
                    s_sourceEnvelopeJsonOptions));
        }

        static (FirmwareFamilyResolutionDefinition Family, string Hash) LoadProvider(string relativePath)
        {
            string json = File.ReadAllText(Path.Combine(FindSourceEnvelopeRepositoryRoot(), relativePath));
            FirmwareFamilyDocument document = ParseProviderDefinitions(json);
            return (FirmwareFamilyResolutionNormalizer.Normalize(document, Hash(json)), Hash(json));
        }

        (FirmwareFamilyResolutionDefinition dpcmiFamily, string dpcmiHash) = LoadProvider(
            Path.Combine("profiles", "built-in", "nt51919-nt51929-nt51932-shared-facts",
                "families", "nt51929-nt51932.json"));
        FirmwareMetadataStructureDefinition dpcmi = Assert.Single(
            Assert.Single(dpcmiFamily.MetadataSets).Structures).Definition;
        var dpcmiReference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            dpcmiFamily.FamilyId, dpcmiFamily.FamilyVersion, dpcmiHash, dpcmi.DefinitionId);
        var dpcmiResolver = new ExactSourceEnvelopeDefinitionResolver((dpcmiReference, dpcmi));

        string firmwareJson = File.ReadAllText(Path.Combine(FindSourceEnvelopeRepositoryRoot(),
            "profiles", "built-in", "nt51927-standard-merge", "families", "nt51927-nt51928.json"));
        FirmwareFamilyDocument firmwareDocument = ParseProviderDefinitions(firmwareJson);
        FirmwareFamilyResolutionDefinition firmwareFamily = FirmwareFamilyResolutionNormalizer.Normalize(
            firmwareDocument, Hash(firmwareJson), dpcmiResolver);
        FirmwareMetadataStructureDefinition firmwareConfig = Assert.Single(
            Assert.Single(firmwareFamily.MetadataSets, static set =>
                set.MetadataSetId == "firmware-config-general-parameters").Structures).Definition;
        var firmwareReference = new FirmwareMetadataStructureDefinitionReferenceDocument(
            firmwareFamily.FamilyId, firmwareFamily.FamilyVersion,
            Hash(firmwareJson), firmwareConfig.DefinitionId);
        return new ExactSourceEnvelopeDefinitionResolver(
            (dpcmiReference, dpcmi),
            (firmwareReference, firmwareConfig));
    }

    private sealed class ExactSourceEnvelopeDefinitionResolver(
        params (FirmwareMetadataStructureDefinitionReferenceDocument Reference,
            FirmwareMetadataStructureDefinition Definition)[] definitions)
        : IFirmwareMetadataStructureDefinitionResolver
    {
        public bool TryResolve(
            FirmwareMetadataStructureDefinitionReferenceDocument reference,
            out FirmwareMetadataStructureDefinition? definition)
        {
            foreach ((FirmwareMetadataStructureDefinitionReferenceDocument expected,
                         FirmwareMetadataStructureDefinition canonical) in definitions)
            {
                if (reference == expected)
                {
                    definition = canonical;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }

    private static string FindSourceEnvelopeRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NvtFwCombiner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root is unavailable to synthetic source-envelope tests.");
    }
}
