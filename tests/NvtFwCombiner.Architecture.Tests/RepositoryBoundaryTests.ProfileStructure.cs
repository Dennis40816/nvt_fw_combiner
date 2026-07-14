using System.Xml.Linq;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies legacy Replace profiles retain only synthetic contracts.</summary>
    [Fact]
    public void BuiltInReplaceProfileConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.cs");
        string synthetic = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.Synthetic.cs");

        Assert.Contains("public static partial class BuiltInReplaceProfiles", root, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<CompositionProfileDefinition> All", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticIc", root, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspective", root, StringComparison.Ordinal);
        Assert.Contains("SyntheticDpReplace", synthetic, StringComparison.Ordinal);
        Assert.Contains("SyntheticCtrlRamReplace", synthetic, StringComparison.Ordinal);
        Assert.Contains("SyntheticGeneralReplace", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", synthetic, StringComparison.Ordinal);
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

    /// <summary>Verifies built-in Standard Merge profiles keep exposure order separate from firmware facts.</summary>
    [Fact]
    public void BuiltInStandardMergeProfileConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.cs");
        string genFlash = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.GenFlash.cs");
        string synthetic = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.Synthetic.cs");
        string dpPerspective = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.DpPerspective.cs");

        Assert.Contains("public static partial class BuiltInStandardMergeProfiles", root, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<CompositionProfileDefinition> ExecutableStandardMergeProfiles", root, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<CompositionProfileDefinition> All", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGenFlashProfile", root, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeRegion", root, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", root, StringComparison.Ordinal);
        Assert.Contains("GenFlashStandardMergeProfiles", genFlash, StringComparison.Ordinal);
        Assert.Contains("OwnerConfirmedAliasStandardMergeProfiles", genFlash, StringComparison.Ordinal);
        Assert.Contains("FlashMapStandardMergeProfiles", genFlash, StringComparison.Ordinal);
        Assert.Contains("CreateGenFlashProfile", genFlash, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", genFlash, StringComparison.Ordinal);
        Assert.Contains("SyntheticStandardMerge", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGenFlashProfile", synthetic, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveStandardMergeProfiles", dpPerspective, StringComparison.Ordinal);
        Assert.Contains("CreateDpPerspectiveProfileForInputLength", dpPerspective, StringComparison.Ordinal);
        Assert.DoesNotContain("StandardMergeRegion", dpPerspective, StringComparison.Ordinal);
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
            "nt51923-standard-merge",
            "nt51927-standard-merge",
            "nt51928-standard-merge",
            "nt51929-standard-merge",
            "nt51919-nt51929-nt51932-ab-merge",
            "nt51930-standard-merge",
            "nt51931-standard-merge",
            "nt51950-nt51951-standard-merge",
        ];

        Assert.Equal(
            expectedBundleIds,
            bundles.Select(bundle => bundle.Attribute("Include")?.Value));
        foreach (XElement bundle in bundles)
        {
            XAttribute include = Assert.Single(bundle.Attributes());

            Assert.Equal("Include", include.Name.LocalName);
            Assert.Empty(bundle.Elements());
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

    /// <summary>Verifies DP Perspective operation and region ids stay owned by the DP Perspective catalog.</summary>
    [Fact]
    public void DpPerspectiveOperationIdsStayCatalogOwned()
    {
        string catalog = ReadText("src/NvtFwCombiner.Profiles/DpPerspectiveCatalog.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.DpPerspective.cs");
        string planning = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Planning.cs");

        Assert.Contains("public const string ContainerRegionId = \"dp-perspective-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string CopyDpContainerOperationId = \"copy-dp-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string OverlayTpOperationId = \"overlay-tp\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceDpContainerOperationId = \"replace-dp-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string RestoreBaseTpOperationId = \"restore-base-tp\";", catalog, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ContainerRegionId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.CopyDpContainerOperationId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.OverlayTpOperationId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ReplaceDpContainerOperationId", planning, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.RestoreBaseTpOperationId", planning, StringComparison.Ordinal);

        foreach (string literal in new[]
        {
            "\"dp-perspective-container\"",
            "\"copy-dp-container\"",
            "\"overlay-tp\"",
            "\"replace-dp-container\"",
            "\"restore-base-tp\"",
        })
        {
            Assert.DoesNotContain(literal, standardMerge, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, planning, StringComparison.Ordinal);
        }
    }

}
