using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies synthetic Replace definitions cannot re-enter production profile catalogs.</summary>
    [Fact]
    public void SyntheticReplaceProfilesStayTestOnly()
    {
        string synthetic = ReadText("tests/NvtFwCombiner.TestSupport/SyntheticReplaceProfiles.cs");
        string v2Registration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");

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
        Assert.Contains("CompositionProfileDefinition Dp", synthetic, StringComparison.Ordinal);
        Assert.Contains("CompositionProfileDefinition CtrlRam", synthetic, StringComparison.Ordinal);
        Assert.Contains("CompositionProfileDefinition General", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", synthetic, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "BuiltInReplaceProfiles.DpPerspective.cs")));
        Assert.Contains("BuiltInV2DpReplaceRegistration", v2Registration, StringComparison.Ordinal);
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

        string synthetic = ReadText("src/NvtFwCombiner.Profiles/SyntheticCompositionProfiles.cs");
        string registration = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs");
        Assert.Contains("CreateStandardMerge", synthetic, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2StandardMergeRegistration", registration, StringComparison.Ordinal);
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
                    "ab3bed384c5d78590ad6a87ee23c12f23a1ea4a1bdc6001273f254b6e5f3547f",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaHash")).Value);
            }
            else if (string.Equals(
                         "nt51926-ctrlram-replace-candidate",
                         bundle.Attribute("Include")?.Value,
                         StringComparison.Ordinal))
            {
                Assert.Equal(
                    "61abe3f9eaa9d1821067788d08014868a81f42a01bd1eb75406aabb9c56df8a3",
                    Assert.Single(bundle.Elements("CompositionProfileSchemaHash")).Value);
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
            "<DefaultCompositionProfileSchemaHash>1af166d37379329cc7a298ea24637a665b183f286e5a2323d4ed014e893dc9f0</DefaultCompositionProfileSchemaHash>",
            project,
            StringComparison.Ordinal);
        Assert.Equal(
            "$(DefaultCompositionProfileSchemaHash)",
            Assert.Single(document.Descendants("ItemDefinitionGroup")
                .Descendants("BuiltInProfileBundle"))
                .Element("CompositionProfileSchemaHash")?.Value);
        Assert.Contains(
            "$(ProfileSchemaSourceRoot)\\%(BuiltInProfileBundle.CompositionProfileSchemaHash)\\composition-profile-v2.schema.json",
            project,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<SourceRoot>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("**\\profile-bundle.json", project, StringComparison.Ordinal);
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
        Assert.Contains("TryResolveDpPerspectiveDpReplaceDisplay", registration, StringComparison.Ordinal);
        Assert.Contains("composition.Plan.OrderedOperations", display, StringComparison.Ordinal);
        Assert.Contains("IsBuiltInV2DpReplaceIc", planning, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", ReadBootstrapSources(), StringComparison.Ordinal);
    }

}
