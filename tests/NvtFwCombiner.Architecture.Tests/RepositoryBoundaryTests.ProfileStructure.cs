using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies synthetic Replace definitions cannot re-enter production profile catalogs.</summary>
    [Fact]
    public void SyntheticReplaceProfilesStayTestOnly()
    {
        string synthetic = ReadText("tests/NvtFwCombiner.TestSupport/SyntheticReplaceProfiles.cs");
        string v2Registration = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.Synthetic.cs")));
        Assert.DoesNotContain("synthetic-dp-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-ctrlram-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-general-replace", ReadProfileSources(), StringComparison.Ordinal);
        Assert.Contains("public static class SyntheticReplaceProfiles", synthetic, StringComparison.Ordinal);
        Assert.Contains("CompositionProfileDefinition General", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", synthetic, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.DpPerspective.cs")));
        Assert.Contains("BuiltInV2Registration", v2Registration, StringComparison.Ordinal);
    }

    /// <summary>Verifies profile compiler shared region helpers stay separate from explicit-mapping policy.</summary>
    [Fact]
    public void ProfileCompilerRegionResolutionStaysShared()
    {
        string explicitMappings = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.ExplicitMappings.cs");
        string explicitCompilation = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.ExplicitMappingCompilation.cs");
        string processorPolicy = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.ExplicitMappingProcessorPolicy.cs");
        string regionResolution = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.RegionResolution.cs");
        string operationValidation = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.OperationValidation.cs");
        string profileValidation = ReadText(
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler.ProfileValidation.cs");

        Assert.Contains("ValidateExplicitMappings", explicitMappings, StringComparison.Ordinal);
        Assert.Contains("ResolveExplicitMappingTargetRegion", explicitMappings, StringComparison.Ordinal);
        Assert.DoesNotContain("private static ProfileRegion? ResolveTargetRegionByRange", explicitMappings, StringComparison.Ordinal);
        Assert.DoesNotContain("private static RegionAccessRule? FindAccessRule", explicitMappings, StringComparison.Ordinal);
        Assert.DoesNotContain("private static CompositionOperation CompileExplicitMapping", explicitMappings, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool HasExternalProcessorAfterMapping", explicitMappings, StringComparison.Ordinal);
        Assert.Contains("private static CompositionOperation CompileExplicitMapping", explicitCompilation, StringComparison.Ordinal);
        Assert.Contains("private static bool HasExternalProcessorAfterMapping", processorPolicy, StringComparison.Ordinal);
        Assert.Contains("private static ProfileRegion? ResolveTargetRegionByRange", regionResolution, StringComparison.Ordinal);
        Assert.Contains("private static RegionAccessRule? FindAccessRule", regionResolution, StringComparison.Ordinal);
        Assert.Contains("ResolveTargetRegionByRange", operationValidation, StringComparison.Ordinal);
        Assert.Contains("ResolveTargetRegionByRange", profileValidation, StringComparison.Ordinal);
    }

    /// <summary>Verifies production Standard Merge facts live only in V2 bundles and registrations.</summary>
    [Fact]
    public void StandardMergeRetiresLegacyCSharpProfiles()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.GenFlash.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInStandardMergeProfiles.DpPerspective.cs")));

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "SyntheticCompositionProfiles.cs")));
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "tests",
            "NvtFwCombiner.TestSupport",
            "SyntheticStandardMergeProfile.cs")));
        Assert.Contains("BuiltInV2Registration", registration, StringComparison.Ordinal);
    }

    /// <summary>Verifies built-in bundle materialization remains an explicit identity allowlist, never source discovery.</summary>
    [Fact]
    public void BuiltInBundleMaterializationUsesExplicitIdentityAllowlist()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var document = XDocument.Parse(project);
        XElement[] bundles = [
            .. document.Descendants("BuiltInProfileBundle")
                .Where(static bundle => bundle.Attribute("Include") is not null),
        ];
        string[] expectedBundleIds =
        [
            "nt51920-standard-merge",
            "nt51920-general-merge-logical-candidate",
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51930-general-merge-logical-candidate",
            "nt51931-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
            "nt51926-ctrlram-replace-candidate",
            "nt51930-ctrlram-replace-candidate",
            "nt51932-ctrlram-replace-candidate",
            "nt51951-ctrlram-replace-candidate",
            "nt51923-standard-merge",
            "nt51927-standard-merge",
            "nt51928-standard-merge",
            "nt51929-standard-merge",
            "nt51919-nt51929-nt51932-ab-merge",
            "nt51930-standard-merge",
            "nt51931-standard-merge",
            "nt51950-nt51951-standard-merge",
        ];
        string[] logicalOutputCandidateBundleIds =
        [
            "nt51920-general-merge-logical-candidate",
            "nt51917-nt51927-general-merge-logical-candidate",
            "nt51919-nt51929-nt51932-general-merge-logical-candidate",
            "nt51923-nt51926-general-merge-logical-candidate",
            "nt51928-general-merge-logical-candidate",
            "nt51930-general-merge-logical-candidate",
            "nt51931-general-merge-logical-candidate",
            "nt51950-nt51951-general-merge-logical-candidate",
        ];

        Assert.Equal(
            expectedBundleIds,
            bundles.Select(bundle => bundle.Attribute("Include")?.Value));
        foreach (XElement bundle in bundles)
        {
            XAttribute include = Assert.Single(bundle.Attributes());

            Assert.Equal("Include", include.Name.LocalName);
            if (logicalOutputCandidateBundleIds.Contains(
                    bundle.Attribute("Include")?.Value,
                    StringComparer.Ordinal))
            {
                Assert.Equal(
                    "composition-profile-v2.5.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else if (bundle.Attribute("Include")?.Value is
                         "nt51926-ctrlram-replace-candidate" or
                         "nt51930-ctrlram-replace-candidate" or
                         "nt51932-ctrlram-replace-candidate" or
                         "nt51951-ctrlram-replace-candidate")
            {
                Assert.Equal(
                    "composition-profile-v2.9.schema.json",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaFile")).Value);
            }
            else
            {
                Assert.Empty(bundle.Elements());
            }
            Assert.DoesNotContain("*", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("$(", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("@(", include.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("%(", include.Value, StringComparison.Ordinal);
        }

        Assert.Contains("$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\**\\*", project, StringComparison.Ordinal);
        Assert.Contains(
            "Exclude=\"$(BuiltInProfileSourceRoot)\\%(BuiltInProfileBundle.Identity)\\schemas\\**\\*\"",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<DefaultCompositionProfileSchemaFile>composition-profile-v2.schema.json</DefaultCompositionProfileSchemaFile>",
            project,
            StringComparison.Ordinal);
        Assert.Equal(
            "$(DefaultCompositionProfileSchemaFile)",
            Assert.Single(document.Descendants("ItemDefinitionGroup")
                .Descendants("BuiltInProfileBundle"))
                .Element("CompositionProfileSchemaFile")?.Value);
        Assert.Contains(
            "$(ProfileContractRoot)\\%(BuiltInProfileBundle.CompositionProfileSchemaFile)",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileSchemaSourceRoot", project, StringComparison.Ordinal);
        string retiredSchemaSource = Path.Combine(Root.FullName, "profiles", "schema-source");
        Assert.False(Directory.Exists(retiredSchemaSource) &&
            Directory.EnumerateFiles(retiredSchemaSource, "*", SearchOption.AllDirectories).Any());
        Assert.DoesNotContain(bundles, static bundle => bundle.Element("SourceRoot") is not null);
        Assert.DoesNotContain("**\\profile-bundle.json", project, StringComparison.Ordinal);
    }

    /// <summary>Verifies canonical firmware-family reuse is explicit, closed-root, and limited to NT51930.</summary>
    [Fact]
    public void Nt51930CandidateMaterializesOneCanonicalFirmwareFamily()
    {
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        var document = XDocument.Parse(project);
        XElement candidate = Assert.Single(document.Descendants("BuiltInProfileBundle"), static bundle =>
            StringComparer.Ordinal.Equals(
                bundle.Attribute("Include")?.Value,
                "nt51930-general-merge-logical-candidate"));

        Assert.Equal(
            "nt51930-standard-merge\\families\\nt51930.json",
            candidate.Element("CanonicalFirmwareFamilySource")?.Value);
        Assert.Equal(
            "families\\nt51930.json",
            candidate.Element("CanonicalFirmwareFamilyDestination")?.Value);
        _ = Assert.Single(document.Descendants("CanonicalFirmwareFamilySource"), static element =>
            !string.IsNullOrWhiteSpace(element.Value));
        _ = Assert.Single(document.Descendants("CanonicalFirmwareFamilyDestination"), static element =>
            !string.IsNullOrWhiteSpace(element.Value));

        string canonicalFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51930-standard-merge",
            "families",
            "nt51930.json");
        string candidateFamilyPath = Path.Combine(
            Root.FullName,
            "profiles",
            "built-in",
            "nt51930-general-merge-logical-candidate",
            "families",
            "nt51930.json");
        Assert.True(File.Exists(canonicalFamilyPath));
        Assert.False(File.Exists(candidateFamilyPath));

        using var manifest = JsonDocument.Parse(ReadText(
            "profiles/built-in/nt51930-general-merge-logical-candidate/profile-bundle.json"));
        JsonElement familyEntry = Assert.Single(
            manifest.RootElement.GetProperty("entries").EnumerateArray(),
            static entry => StringComparer.Ordinal.Equals(
                entry.GetProperty("kind").GetString(),
                "firmware-family"));
        Assert.Equal("families/nt51930.json", familyEntry.GetProperty("path").GetString());
        Assert.Equal(
            familyEntry.GetProperty("contentHash").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(canonicalFamilyPath))).ToLowerInvariant());

        Assert.Contains("Built-in profile canonical firmware-family metadata must declare both source and destination", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source escapes the approved source root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination escapes the bundle families root", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family source is missing", project, StringComparison.Ordinal);
        Assert.Contains("Built-in profile canonical firmware-family destination collides", project, StringComparison.Ordinal);
        Assert.Contains("@(_BuiltInProfileCanonicalFamily->'%(SourceFile)')", project, StringComparison.Ordinal);
        Assert.Contains("@(_BuiltInProfileCanonicalFamily->'%(DestinationFile)')", project, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CanonicalFirmwareFamily",
            ReadText("src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs"),
            StringComparison.Ordinal);
    }

    /// <summary>Verifies retired DP Perspective C# facts cannot become a second oracle beside trusted V2 plans.</summary>
    [Fact]
    public void DpPerspectiveFactsStayOwnedByTrustedV2Profiles()
    {
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");
        string display = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.V2Display.cs");
        string planning = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Planning.cs");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "DpPerspectiveCatalog.cs")));
        Assert.DoesNotContain("BuiltInReplaceProfiles", registration, StringComparison.Ordinal);
        Assert.Contains("TryResolveBuiltInV2DpReplaceDisplay", registration, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.OrderedOperations", display, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInTpFlashMapCatalog", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDpReplaceRegions", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionOperationKind", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", ReadBootstrapSources(), StringComparison.Ordinal);
    }

}
