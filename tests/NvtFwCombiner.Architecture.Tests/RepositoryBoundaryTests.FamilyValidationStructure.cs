namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ProfileBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Profiles resolves family references without repeating canonical Domain invariants.</summary>
    [Fact]
    public void FamilyNormalizationLeavesCanonicalInvariantsInDomain()
    {
        string relationships = ReadText(
            "src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Relationships.cs");
        string metadata = ReadText(
            "src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Metadata.cs");

        Assert.DoesNotContain(
            "Shared-fact applicability does not cover",
            relationships,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Duplicate shared-fact reference",
            relationships,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Family relationship cannot be null", relationships, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateGlobalStructureIds", metadata, StringComparison.Ordinal);
    }
}
