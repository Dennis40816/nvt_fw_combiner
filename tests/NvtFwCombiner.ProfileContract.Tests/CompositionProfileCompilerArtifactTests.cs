using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests legacy profile compilation into one atomic composition artifact.</summary>
public sealed class CompositionProfileCompilerArtifactTests
{
    /// <summary>Verifies successful compilation stores one artifact and exposes its plan only by projection.</summary>
    [Fact]
    public void CompileReturnsAtomicLegacyArtifact()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(result.CompiledComposition);
        Assert.Same(composition.Plan, result.Plan);
        Assert.Equal(profile.ProfileId, composition.ProfileId);
        Assert.Equal(profile.ProfileVersion, composition.ProfileVersion);
        Assert.Equal(profile.IcId, composition.IcId);
        Assert.Equal(profile.ModeId, composition.ModeId);
        Assert.Equal(profile.ExperienceId, composition.ExperienceId);
        Assert.Equal(profile.CompositionKind, composition.CompositionKind);
        Assert.Equal(profile.DefaultOutputFileName, composition.DefaultOutputFileName);
        Assert.Equal(CompiledIcNumberPolicy.NotApplicable, composition.IcNumberPolicy);
        Assert.Equal(CompiledCompositionEligibility.LegacyRuntimeExecutable, composition.Eligibility);
        Assert.Equal("0.2", Assert.IsType<LegacyProfileCompilationAuthority>(composition.Authority).ModelVersion);
    }

    /// <summary>Verifies all legacy Replace selector modes map to a closed compiled policy.</summary>
    [Theory]
    [InlineData(IcNumberInputMode.SingleSelector, CompiledIcNumberPolicy.SingleSelector)]
    [InlineData(IcNumberInputMode.CascadeSelector, CompiledIcNumberPolicy.CascadeSelector)]
    [InlineData(IcNumberInputMode.NumericSelector, CompiledIcNumberPolicy.NumericSelector)]
    public void CompileMapsReplaceIcNumberPolicy(
        IcNumberInputMode inputMode,
        CompiledIcNumberPolicy expectedPolicy)
    {
        CompositionProfileDefinition profile = CloneProfile(
            BuiltInReplaceProfiles.SyntheticGeneralReplace,
            inputMode);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(expectedPolicy, result.CompiledComposition!.IcNumberPolicy);
    }

    /// <summary>Verifies invalid Merge and Replace selector policies fail before artifact creation.</summary>
    [Fact]
    public void CompileRejectsInvalidIcNumberPolicyBeforeArtifactCreation()
    {
        CompositionProfileDefinition mergeWithSelector = CloneProfile(
            BuiltInStandardMergeProfiles.SyntheticStandardMerge,
            IcNumberInputMode.SingleSelector);
        CompositionProfileDefinition replaceWithoutSelector = CloneProfile(
            BuiltInReplaceProfiles.SyntheticGeneralReplace,
            inputMode: null);
        CompositionProfileDefinition replaceWithUnknownSelector = CloneProfile(
            BuiltInReplaceProfiles.SyntheticGeneralReplace,
            (IcNumberInputMode)int.MaxValue);

        AssertFailure(
            CompositionProfileCompiler.Compile(mergeWithSelector, []),
            "profile.ic-number-mode.not-applicable");
        AssertFailure(
            CompositionProfileCompiler.Compile(replaceWithoutSelector, []),
            "profile.ic-number-mode.required");
        AssertFailure(
            CompositionProfileCompiler.Compile(replaceWithUnknownSelector, []),
            "profile.ic-number-mode.unknown");
    }

    /// <summary>Verifies recompilation is stable while identity or default output changes alter the fingerprint.</summary>
    [Fact]
    public void CompileFingerprintBindsLegacyProfileIdentityAndOutputPolicy()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;
        CompiledComposition first = CompositionProfileCompiler.Compile(profile, []).CompiledComposition!;
        CompiledComposition second = CompositionProfileCompiler.Compile(profile, []).CompiledComposition!;
        CompiledComposition changedIdentity = CompositionProfileCompiler.Compile(
            CloneProfile(profile, inputMode: null, profileId: "changed-profile"),
            []).CompiledComposition!;
        CompiledComposition changedOutput = CompositionProfileCompiler.Compile(
            CloneProfile(profile, inputMode: null, defaultOutputFileName: "changed.bin"),
            []).CompiledComposition!;

        Assert.Equal(first.CompilationFingerprint, second.CompilationFingerprint);
        Assert.NotEqual(first.CompilationFingerprint, changedIdentity.CompilationFingerprint);
        Assert.NotEqual(first.CompilationFingerprint, changedOutput.CompilationFingerprint);
    }

    private static CompositionProfileDefinition CloneProfile(
        CompositionProfileDefinition source,
        IcNumberInputMode? inputMode,
        string? profileId = null,
        string? defaultOutputFileName = null)
    {
        return new CompositionProfileDefinition(
            profileId ?? source.ProfileId,
            source.ProfileVersion,
            source.IcId,
            source.ModeId,
            source.CompositionKind,
            source.ExperienceId,
            defaultOutputFileName ?? source.DefaultOutputFileName,
            source.Initialization,
            source.AddressSpaces,
            source.Operations,
            source.Regions,
            source.RegionAccessRules,
            inputMode);
    }

    private static void AssertFailure(ProfileCompileResult result, string issueCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.CompiledComposition);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
