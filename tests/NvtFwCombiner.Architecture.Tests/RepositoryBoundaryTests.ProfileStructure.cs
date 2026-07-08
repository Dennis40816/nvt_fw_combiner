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

    /// <summary>Verifies alias-heavy postbuild profile rows stay grouped by IC family.</summary>
    [Fact]
    public void LegacyPostbuildProfileRowsStaySplitByFamily()
    {
        string sharedRows = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.cs");
        string nt51927Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51927Family.cs");
        string nt51930Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51930Family.cs");
        string nt51932Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51932Family.cs");
        string nt51950Family = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.Profiles.Nt51950Family.cs");

        Assert.DoesNotContain("NT51927 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51930 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51932 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950 CtrlRAM postbuild profile", sharedRows, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51917", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51928", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51917 follows NT51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51928 follows NT51927", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51930", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51930CommonFw1x", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51931", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51932", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51929", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51919", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51929 follows NT51932", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51919 follows NT51929", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51950", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("public static LegacyCombinerPostbuildProfile Nt51951", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("owner confirmation: NT51951 follows NT51950", nt51950Family, StringComparison.Ordinal);
    }

    /// <summary>Verifies legacy postbuild contract types stay split by responsibility.</summary>
    [Fact]
    public void LegacyPostbuildContractTypesStaySplit()
    {
        string profile = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildProfile.cs");
        string enums = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildEnums.cs");
        string commonFwRule = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerCommonFwVersionRule.cs");
        string branchRule = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildBranchRule.cs");
        string blockArgument = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerBlockArgument.cs");
        string command = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCommand.cs");
        string commandPlan = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCommandPlan.cs");

        Assert.Contains("public sealed class LegacyCombinerPostbuildProfile", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum LegacyCombinerCommandFamily", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class LegacyCombinerPostbuildCommand", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("public sealed class LegacyCombinerPostbuildCommandPlan", profile, StringComparison.Ordinal);
        Assert.Contains("public enum LegacyCombinerCommandFamily", enums, StringComparison.Ordinal);
        Assert.Contains("public enum LegacyCombinerBlockSourceKind", enums, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerCommonFwVersionRule", commonFwRule, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildBranchRule", branchRule, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerBlockArgument", blockArgument, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommand", command, StringComparison.Ordinal);
        Assert.Contains("public sealed class LegacyCombinerPostbuildCommandPlan", commandPlan, StringComparison.Ordinal);
    }

    /// <summary>Verifies legacy postbuild planning stays split from write-range and integrity helpers.</summary>
    [Fact]
    public void LegacyPostbuildPlannerConcernsStaySplit()
    {
        string root = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.cs");
        string writeRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.WriteRanges.cs");
        string integrityRanges = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.IntegrityRanges.cs");
        string normalize = ReadText(
            "src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildPlanner.Normalize.cs");

        Assert.Contains("public static partial class LegacyCombinerPostbuildPlanner", root, StringComparison.Ordinal);
        Assert.Contains("CreatePlan", root, StringComparison.Ordinal);
        Assert.Contains("GetStagedFileBlocks", root, StringComparison.Ordinal);
        Assert.Contains("CalculateRequiredCapacity", root, StringComparison.Ordinal);
        Assert.Contains("ResolveBranch", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAllowedWriteRangeSectionsForStagedSources", root, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizeCandidateWriteRangeSections", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNt51927BasedCrcOnlyIntegrityRanges", root, StringComparison.Ordinal);

        Assert.Contains("GetKnownIntegrityWriteRanges", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetKnownIntegrityWriteRangeSections", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForStagedSources", writeRanges, StringComparison.Ordinal);
        Assert.Contains("GetAllowedWriteRangeSectionsForInPlaceRefresh", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", writeRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string SelectWriteRangeSectionId", writeRanges, StringComparison.Ordinal);

        Assert.Contains("AddNtBasedHeaderIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("AddNt51927BasedCrcOnlyIntegrityRanges", integrityRanges, StringComparison.Ordinal);
        Assert.Contains("GetPostbuildBlockSectionId", integrityRanges, StringComparison.Ordinal);
        Assert.DoesNotContain("private static IReadOnlyList<LegacyCombinerPostbuildWriteRange> NormalizeCandidateWriteRangeSections", integrityRanges, StringComparison.Ordinal);

        Assert.Contains("NormalizeCandidateWriteRangeSections", normalize, StringComparison.Ordinal);
        Assert.Contains("SelectWriteRangeSectionId", normalize, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void AddNtBasedHeaderIntegrityRanges", normalize, StringComparison.Ordinal);
    }
}
