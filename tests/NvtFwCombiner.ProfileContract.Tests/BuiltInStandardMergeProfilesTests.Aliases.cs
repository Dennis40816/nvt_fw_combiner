using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class BuiltInStandardMergeProfilesTests
{
    /// <summary>Verifies owner-confirmed Standard Merge aliases copy the same ranges as their reference ICs.</summary>
    [Theory]
    [InlineData("NT51917", "NT51927")]
    [InlineData("NT51919", "NT51929")]
    public void OwnerConfirmedAliasStandardMergeProfilesFollowReferenceRanges(string aliasIcId, string referenceIcId)
    {
        CompositionProfileDefinition alias = BuiltInStandardMergeProfiles.OwnerConfirmedAliasStandardMergeProfiles
            .Single(profile => profile.IcId == aliasIcId);
        CompositionProfileDefinition reference = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(profile => profile.IcId == referenceIcId);

        ProfileCompileResult aliasCompile = CompositionProfileCompiler.Compile(alias, []);
        ProfileCompileResult referenceCompile = CompositionProfileCompiler.Compile(reference, []);

        Assert.True(aliasCompile.IsSuccess, FormatIssues(aliasCompile.Issues));
        Assert.True(referenceCompile.IsSuccess, FormatIssues(referenceCompile.Issues));
        Assert.EndsWith("-gen-flash-alias", alias.ProfileId, StringComparison.Ordinal);
        Assert.Equal(reference.Initialization.Capacity, alias.Initialization.Capacity);
        Assert.Equal(
            reference.AddressSpaces
                .Where(space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(space => (space.AddressSpaceId, space.Length)),
            alias.AddressSpaces
                .Where(space => space.Mutability == AddressSpaceMutability.Immutable)
                .Select(space => (space.AddressSpaceId, space.Length)));
        Assert.Equal(
            referenceCompile.Plan!.OrderedOperations.Select(operation => (
                operation.OperationId,
                operation.Sequence,
                operation.SourceSpaceId,
                operation.SourceRange,
                operation.TargetRange)),
            aliasCompile.Plan!.OrderedOperations.Select(operation => (
                operation.OperationId,
                operation.Sequence,
                operation.SourceSpaceId,
                operation.SourceRange,
                operation.TargetRange)));
    }

    /// <summary>Verifies the executable Standard Merge catalog exposes only profiles with current release evidence gates.</summary>
    [Fact]
    public void ExecutableStandardMergeProfilesIncludeOwnerConfirmedAliases()
    {
        string[] expectedIcIds =
        [
            "NT51917",
            "NT51919",
            "NT51920",
            "NT51923",
            "NT51926",
            "NT51927",
            "NT51928",
            "NT51929",
            "NT51930",
            "NT51931",
            "NT51932",
            "NT51950",
            "NT51951",
        ];

        Assert.Equal(
            expectedIcIds,
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                .Select(profile => profile.IcId)
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT-SYNTHETIC");
        Assert.Contains(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT51917" && profile.ProfileId == "nt51917-standard-merge-gen-flash-alias");
        Assert.Contains(
            BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles,
            profile => profile.IcId == "NT51919" && profile.ProfileId == "nt51919-standard-merge-gen-flash-alias");
    }
}
