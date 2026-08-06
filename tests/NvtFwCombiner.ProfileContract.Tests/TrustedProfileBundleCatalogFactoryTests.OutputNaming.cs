using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class TrustedProfileBundleCatalogFactoryTests
{
    /// <summary>Canonical normal naming templates compile to their closed executable renderers.</summary>
    [Fact]
    public void BlankCopyLoweringAdmitsCanonicalNormalOutputRenderers()
    {
        string familyJson = RuntimeNormalOutputFamilyJson();
        V2CompositionPlanCompileResult flashCode =
            Compile(PrepareSupportedBlankCopy(
                familyHash => RuntimeNormalOutputProfileJson(
                    familyHash,
                    tpFirmware: false),
                familyJson));
        V2CompositionPlanCompileResult tpFirmware =
            Compile(PrepareSupportedBlankCopy(
                familyHash => RuntimeNormalOutputProfileJson(
                    familyHash,
                    tpFirmware: true),
                familyJson));

        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.RuleId);
        Assert.Equal(
            CompiledOutputArtifactType.FlashCode,
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.OutputArtifactType);
        Assert.Equal(
            ["dpcmi-inspection", "firmware-config-inspection"],
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.TokenRequirements
                .Where(static requirement => requirement.MetadataBindingId is not null)
                .Select(static requirement => requirement.MetadataBindingId));
        Assert.Equal(
            ["tp-source", "tp-source"],
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.TokenRequirements
                .Where(static requirement => requirement.MetadataSpaceId is not null)
                .Select(static requirement => requirement.MetadataSpaceId));
        Assert.Equal(
            CompiledOutputNameRendererKind.TpFirmwareV1,
            tpFirmware.CompiledComposition?.V2Details?.OutputNamingRequirement.RendererKind);
        Assert.Equal(
            CompiledCompositionEligibility.V2RuntimeExecutable,
            flashCode.CompiledComposition?.Eligibility);
        Assert.Equal(
            CompiledCompositionEligibility.V2RuntimeExecutable,
            tpFirmware.CompiledComposition?.Eligibility);
        Assert.Empty(flashCode.Issues);
        Assert.Empty(tpFirmware.Issues);
    }

    /// <summary>The closed Merge plus AB renderer contract remains executable without a workflow-name branch.</summary>
    [Fact]
    public void BlankCopyLoweringAdmitsAbRendererAcrossWorkflowIdentity()
    {
        V2CompositionPlanCompileResult result = Compile(PrepareSupportedBlankCopy(familyHash =>
        {
            JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(RuntimeSupportedProfileJson(familyHash)));
            JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
            output["fileNameTemplate"] = CompiledOutputNamingRequirement.AbCodeV1Template;
            output["requiredTokenIds"] = new JsonArray("date", "dp-a", "dp-b", "ic", "tp-a", "tp-b");
            return profile.ToJsonString();
        }));

        Assert.Empty(result.Issues);
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Equal("display-merge", composition.ExperienceId);
        Assert.NotEqual(ExperienceIds.AbMerge, composition.ExperienceId);
        Assert.Equal(CompiledOutputNameRendererKind.AbCodeV1, composition.V2Details?.OutputNamingRequirement.RendererKind);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
    }

    /// <summary>A matching legacy template cannot infer typed normal naming authority.</summary>
    [Fact]
    public void BlankCopyLoweringRejectsNormalTemplateWithoutTypedRule()
    {
        V2CompositionPlanCompileResult result =
            Compile(PrepareSupportedBlankCopy(
                familyHash => RuntimeLegacyNormalOutputProfileJson(
                    familyHash)));

        Assert.Null(result.CompiledComposition);
        Assert.Equal(
            "profile.v2.plan.unsupported-declaration",
            Assert.Single(result.Issues).Code);
    }

    /// <summary>Schema 2.15 rejects deferred replacement policy before any promotion-stage lowering.</summary>
    [Theory]
    [InlineData("compilable")]
    [InlineData("executable-candidate")]
    public void BundleAdmissionRejectsV215ReplaceUnderscore(string stage)
    {
        string familyJson = RuntimeNormalOutputFamilyJson();

        _ = Assert.Throws<TrustedProfileBundleCatalogException>(() =>
            PrepareSupportedBlankCopy(
                familyHash =>
                {
                    JsonObject profile = Assert.IsType<JsonObject>(
                        JsonNode.Parse(RuntimeNormalOutputProfileJson(
                            familyHash,
                            tpFirmware: false)));
                    Assert.IsType<JsonObject>(profile["promotion"])["stage"] =
                        stage;
                    Assert.IsType<JsonObject>(profile["output"])
                        ["invalidCharacterPolicy"] = "replace-underscore";
                    return profile.ToJsonString();
                },
                familyJson));
    }

    private static string RuntimeNormalOutputProfileJson(
        string familyHash,
        bool tpFirmware)
    {
        JsonObject profile = Assert.IsType<JsonObject>(
            JsonNode.Parse(RuntimeSupportedProfileJson(familyHash)));
        profile["schemaVersion"] = "2.15";
        profile["compilationContext"] =
            new JsonObject { ["kind"] = "resolved-map" };
        Assert.IsType<JsonObject>(profile["mapBinding"])
            ["requiredMetadataStructureIds"] =
            new JsonArray(
                "dpcmi",
                "firmware-config-general-parameters");
        profile["metadataBindings"] = new JsonArray(
            MetadataBinding(
                "dpcmi-inspection",
                "dpcmi",
                "version"),
            MetadataBinding(
                "firmware-config-inspection",
                "firmware-config-general-parameters",
                "tp-version"));
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] = tpFirmware
            ? CompiledOutputNamingRequirement.TpFirmwareV1Template
            : CompiledOutputNamingRequirement.NormalFlashCodeV1Template;
        output["requiredTokenIds"] = tpFirmware
            ? new JsonArray("date", "ic", "tp-version")
            : new JsonArray("date", "dp-version", "ic", "tp-version");
        output["ruleId"] = tpFirmware
            ? CompiledOutputNamingRequirement.TpFirmwareV1RuleId
            : CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId;
        output["outputArtifactType"] = tpFirmware
            ? "tp-firmware"
            : "flash-code";
        output["tokenRequirements"] = tpFirmware
            ? new JsonArray(
                OutputToken("date", "run-date-utc", "block"),
                OutputToken("ic", "compiled-ic", "block"),
                OutputToken(
                    "tp-version",
                    "firmware-config-tp-version",
                    "use-placeholder",
                    "firmware-config-inspection",
                    "xxxx"))
            : new JsonArray(
                OutputToken("date", "run-date-utc", "block"),
                OutputToken(
                    "dp-version",
                    "dpcmi-version",
                    "use-placeholder",
                    "dpcmi-inspection",
                    "xxxx"),
                OutputToken("ic", "compiled-ic", "block"),
                OutputToken(
                    "tp-version",
                    "firmware-config-tp-version",
                    "use-placeholder",
                    "firmware-config-inspection",
                    "xxxx"));
        return profile.ToJsonString(
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    private static string RuntimeLegacyNormalOutputProfileJson(
        string familyHash)
    {
        JsonObject profile = Assert.IsType<JsonObject>(
            JsonNode.Parse(RuntimeSupportedProfileJson(familyHash)));
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] =
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template;
        output["requiredTokenIds"] =
            new JsonArray("date", "dp-version", "ic", "tp-version");
        return profile.ToJsonString();
    }

    private static JsonObject MetadataBinding(
        string bindingId,
        string structureId,
        string fieldId)
    {
        return new JsonObject
        {
            ["bindingId"] = bindingId,
            ["spaceId"] = "tp-source",
            ["structureId"] = structureId,
            ["fieldIds"] = new JsonArray(fieldId),
            ["purposes"] = new JsonArray("output-naming"),
        };
    }

    private static JsonObject OutputToken(
        string tokenId,
        string sourceKind,
        string missingPolicy,
        string? metadataBindingId = null,
        string? placeholder = null)
    {
        var source = new JsonObject { ["kind"] = sourceKind };
        if (metadataBindingId is not null)
        {
            source["metadataBindingId"] = metadataBindingId;
        }

        var token = new JsonObject
        {
            ["tokenId"] = tokenId,
            ["source"] = source,
            ["missingPolicy"] = missingPolicy,
        };
        if (placeholder is not null)
        {
            token["placeholder"] = placeholder;
        }

        return token;
    }

    private static string RuntimeNormalOutputFamilyJson()
    {
        JsonObject family = ParseFamily(
            FamilyJsonWithRootWriteConstraint("whole-region"));
        family["metadataSets"] = new JsonArray(
            new JsonObject
            {
                ["metadataSetId"] = "output-naming-metadata",
                ["structures"] = new JsonArray(
                    OutputNamingStructure("dpcmi", "version"),
                    OutputNamingStructure(
                        "firmware-config-general-parameters",
                        "tp-version")),
                ["evidenceRefs"] =
                    new JsonArray("output-naming-metadata-evidence"),
            });
        JsonObject map = Assert.IsType<JsonObject>(
            Assert.IsType<JsonArray>(family["imageMaps"])[0]);
        map["metadataSetIds"] =
            new JsonArray("output-naming-metadata");
        return family.ToJsonString();
    }

    private static JsonObject OutputNamingStructure(
        string structureId,
        string fieldId)
    {
        return new JsonObject
        {
            ["structureId"] = structureId,
            ["artifactBindingId"] = "tp-firmware",
            ["length"] = 1,
            ["locator"] = new JsonObject
            {
                ["kind"] = "absolute-range",
                ["range"] = new JsonObject
                {
                    ["addressSpaceId"] = "flash",
                    ["start"] = 0,
                    ["length"] = 1,
                },
                ["allowedResultRegionId"] = "root",
            },
            ["fields"] = new JsonArray(
                new JsonObject
                {
                    ["fieldId"] = fieldId,
                    ["offset"] = 0,
                    ["widthBytes"] = 1,
                    ["encoding"] = "bytes",
                }),
            ["assertions"] = new JsonArray(),
        };
    }
}
