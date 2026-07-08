namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies built-in Replace profiles keep synthetic contracts separate from DP Perspective production policy.</summary>
    [Fact]
    public void BuiltInReplaceProfileConcernsStaySplit()
    {
        string root = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.cs");
        string synthetic = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.Synthetic.cs");
        string dpPerspective = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.DpPerspective.cs");

        Assert.Contains("public static partial class BuiltInReplaceProfiles", root, StringComparison.Ordinal);
        Assert.Contains("public static IReadOnlyList<CompositionProfileDefinition> All", root, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticIc", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDpPerspectiveDpReplaceProfileCore", root, StringComparison.Ordinal);
        Assert.Contains("SyntheticDpReplace", synthetic, StringComparison.Ordinal);
        Assert.Contains("SyntheticCtrlRamReplace", synthetic, StringComparison.Ordinal);
        Assert.Contains("SyntheticGeneralReplace", synthetic, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", synthetic, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveDpReplaceProfiles", dpPerspective, StringComparison.Ordinal);
        Assert.Contains("CreateDpPerspectiveDpReplaceProfileCore", dpPerspective, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950Family", dpPerspective, StringComparison.Ordinal);
        Assert.DoesNotContain("SyntheticIc", dpPerspective, StringComparison.Ordinal);
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

    /// <summary>Verifies DP Perspective operation and region ids stay owned by the DP Perspective catalog.</summary>
    [Fact]
    public void DpPerspectiveOperationIdsStayCatalogOwned()
    {
        string catalog = ReadText("src/NvtFwCombiner.Profiles/DpPerspectiveCatalog.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.DpPerspective.cs");
        string replace = ReadText("src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.DpPerspective.cs");
        string planning = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Planning.cs");

        Assert.Contains("public const string ContainerRegionId = \"dp-perspective-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string CopyDpContainerOperationId = \"copy-dp-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string OverlayTpOperationId = \"overlay-tp\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceDpContainerOperationId = \"replace-dp-container\";", catalog, StringComparison.Ordinal);
        Assert.Contains("public const string RestoreBaseTpOperationId = \"restore-base-tp\";", catalog, StringComparison.Ordinal);
        Assert.Contains(
            "public const string RestoreBaseCustomerInfoOperationId = \"restore-base-customer-info\";",
            catalog,
            StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ContainerRegionId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.CopyDpContainerOperationId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.OverlayTpOperationId", standardMerge, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ContainerRegionId", replace, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ReplaceDpContainerOperationId", replace, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.RestoreBaseTpOperationId", replace, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.RestoreBaseCustomerInfoOperationId", replace, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.ReplaceDpContainerOperationId", planning, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.RestoreBaseTpOperationId", planning, StringComparison.Ordinal);
        Assert.Contains("DpPerspectiveCatalog.RestoreBaseCustomerInfoOperationId", planning, StringComparison.Ordinal);

        foreach (string literal in new[]
        {
            "\"dp-perspective-container\"",
            "\"copy-dp-container\"",
            "\"overlay-tp\"",
            "\"replace-dp-container\"",
            "\"restore-base-tp\"",
            "\"restore-base-customer-info\"",
        })
        {
            Assert.DoesNotContain(literal, standardMerge, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, replace, StringComparison.Ordinal);
            Assert.DoesNotContain(literal, planning, StringComparison.Ordinal);
        }
    }

}
