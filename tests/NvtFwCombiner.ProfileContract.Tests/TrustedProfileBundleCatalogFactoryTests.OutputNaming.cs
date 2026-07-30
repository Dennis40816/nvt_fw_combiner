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
        V2CompositionPlanCompileResult flashCode =
            V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
                familyHash => RuntimeNormalOutputProfileJson(
                    familyHash,
                    tpFirmware: false)));
        V2CompositionPlanCompileResult tpFirmware =
            V2CompositionPlanCompiler.Compile(PrepareSupportedBlankCopy(
                familyHash => RuntimeNormalOutputProfileJson(
                    familyHash,
                    tpFirmware: true)));

        Assert.Equal(
            CompiledOutputNameRendererKind.NormalFlashCodeV1,
            flashCode.CompiledComposition?.V2Details?.OutputNamingRequirement.RendererKind);
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

    private static string RuntimeNormalOutputProfileJson(
        string familyHash,
        bool tpFirmware)
    {
        JsonObject profile = Assert.IsType<JsonObject>(
            JsonNode.Parse(RuntimeSupportedProfileJson(familyHash)));
        JsonObject output = Assert.IsType<JsonObject>(profile["output"]);
        output["fileNameTemplate"] = tpFirmware
            ? CompiledOutputNamingRequirement.TpFirmwareV1Template
            : CompiledOutputNamingRequirement.NormalFlashCodeV1Template;
        output["requiredTokenIds"] = tpFirmware
            ? new JsonArray("date", "ic", "tp-version")
            : new JsonArray("date", "dp-version", "ic", "tp-version");
        return profile.ToJsonString(
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
