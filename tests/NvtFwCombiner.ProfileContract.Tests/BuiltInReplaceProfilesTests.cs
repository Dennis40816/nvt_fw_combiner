using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests built-in synthetic Replace profiles used by CLI and UI wiring.</summary>
public sealed class BuiltInReplaceProfilesTests
{
    /// <summary>Verifies the built-in Replace catalog exposes the approved three-way taxonomy.</summary>
    [Fact]
    public void BuiltInReplaceProfilesExposeThreeWayTaxonomy()
    {
        Assert.Equal(
            ["dp-replace", "ctrlram-replace", "general-replace"],
            BuiltInReplaceProfiles.All
                .Select(profile => profile.ExperienceId)
                .Distinct(StringComparer.Ordinal));
    }

    /// <summary>Verifies fixed DP Replace compiles with separate DP and LD payloads.</summary>
    [Fact]
    public void SyntheticDpReplaceCompiles()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticDpReplace;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        Assert.Contains(profile.AddressSpaces, space => space.AddressSpaceId == "dp-replacement" && space.InputPaddingByte == 0xFF);
        Assert.Contains(profile.AddressSpaces, space => space.AddressSpaceId == "ld-replacement" && space.InputPaddingByte == 0xFF);
        Assert.Equal(["replace-dp", "replace-ld"], result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(profile.Regions, region => region.RegionId == "ld" && region.ClassificationTags.Contains("ld", StringComparer.Ordinal));
    }

    /// <summary>Verifies NT51950/NT51951 DP Replace is catalog-owned instead of workbench-owned.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-dp-replace-dp-perspective")]
    [InlineData("NT51951", "nt51951-dp-replace-dp-perspective")]
    public void Nt51950FamilyDpReplaceCompilesFromBuiltInCatalog(string icId, string profileId)
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.All.Single(item => item.ProfileId == profileId);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(icId, profile.IcId);
        Assert.Equal(CompositionKind.Replace, profile.CompositionKind);
        Assert.Equal("dp-replace", profile.ExperienceId);
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "reference-base" && space.Length == 0x100000);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "dp-replacement" &&
            space.Length == 0x100000 &&
            space.InputPaddingByte == 0x00 &&
            space.AllowedInputLengths.SequenceEqual([0x40000, 0x80000, 0x100000]));

        CompositionOperation[] operations = [.. result.Plan!.OrderedOperations];
        Assert.Equal(["replace-dp-container", "restore-base-tp", "restore-base-customer-info"], operations.Select(operation => operation.OperationId));
        Assert.Equal(new ByteRange(0, 0x100000), operations[0].TargetRange);
        Assert.Equal(ByteRange.FromStartEndExclusive(0x0A000, 0x37000), operations[1].TargetRange);
        Assert.Equal(new ByteRange(0x37000, 0x1000), operations[2].TargetRange);
        Assert.Contains(profile.Regions, region =>
            region.RegionId == "dp-perspective-container" &&
            region.ClassificationTags.Contains("customer-info-preserve", StringComparer.Ordinal));
    }

    /// <summary>Verifies the NT51950/NT51951 workbench profile uses the selected base length without duplicating profile semantics.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public void Nt51950FamilyDpReplaceCreatesSelectedBaseLengthProfile(string icId, long capacity)
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.CreateNt51950FamilyDpReplaceProfile(icId, capacity);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "reference-base" && space.Length == capacity);
        Assert.Contains(profile.AddressSpaces, space =>
            space.AddressSpaceId == "dp-replacement" &&
            space.Length == capacity &&
            space.InputPaddingByte == 0x00 &&
            space.AllowedInputLengths.Count == 0);
        Assert.Equal(
            ["replace-dp-container", "restore-base-tp", "restore-base-customer-info"],
            result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Equal(new ByteRange(0, capacity), result.Plan.OrderedOperations[0].TargetRange);
        Assert.Equal(BuiltInReplaceProfiles.Nt51950FamilyTpRestoreRange, result.Plan.OrderedOperations[1].TargetRange);
        Assert.Equal(BuiltInReplaceProfiles.Nt51950FamilyCustomerInfoPreserveRange, result.Plan.OrderedOperations[2].TargetRange);
    }

    /// <summary>Verifies fixed CtrlRAM Replace compiles with oversized-input truncation policy.</summary>
    [Fact]
    public void SyntheticCtrlRamReplaceCompiles()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticCtrlRamReplace;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.CascadeSelector, profile.IcNumberInputMode);
        Assert.Contains(
            profile.AddressSpaces,
            space => space.AddressSpaceId == "ctrlram-replacement" &&
                space.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning);
        Assert.Contains(
            profile.Regions,
            region => region.ClassificationTags.Contains("tp-ctrlram", StringComparer.Ordinal));
    }

    /// <summary>Verifies General Replace compiles runtime explicit mappings into replace operations.</summary>
    [Fact]
    public void SyntheticGeneralReplaceCompilesExplicitMapping()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticGeneralReplace;
        var mapping = new ExplicitMapping(
            "replace-general",
            100,
            ExplicitMappingOperationKind.ReplaceRange,
            "replacement-input",
            new ByteRange(0, 2),
            "output-image",
            new ByteRange(1, 2),
            OverlapPolicy.Reject,
            alignment: 1,
            "replace payload",
            targetRegionId: null);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("replacement-input", 2, AddressSpaceMutability.Immutable)]);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
