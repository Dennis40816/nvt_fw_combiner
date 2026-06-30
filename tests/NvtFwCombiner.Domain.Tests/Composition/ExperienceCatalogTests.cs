using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

/// <summary>Tests for the approved experience catalog.</summary>
public sealed class ExperienceCatalogTests
{
    /// <summary>Verifies that the catalog exposes the six approved experiences in stable order.</summary>
    [Fact]
    public void CatalogContainsTheSixApprovedExperiences()
    {
        string[] ids = [.. ExperienceCatalog.All.Select(experience => experience.ExperienceId)];

        string[] expected =
        [
            "standard-merge",
            "ab-merge",
            "general-merge",
            "dp-replace",
            "ctrlram-replace",
            "general-replace",
        ];

        Assert.Equal(expected, ids);
    }

    /// <summary>Verifies that initialization depends only on merge versus replace.</summary>
    [Theory]
    [InlineData("standard-merge", ImageInitializationKind.Blank)]
    [InlineData("ab-merge", ImageInitializationKind.Blank)]
    [InlineData("general-merge", ImageInitializationKind.Blank)]
    [InlineData("dp-replace", ImageInitializationKind.Reference)]
    [InlineData("ctrlram-replace", ImageInitializationKind.Reference)]
    [InlineData("general-replace", ImageInitializationKind.Reference)]
    public void RequiredInitializationIsDerivedOnlyFromCompositionKind(
        string experienceId,
        ImageInitializationKind expected)
    {
        ExperienceDescriptor experience = ExperienceCatalog.All.Single(
            item => item.ExperienceId == experienceId);

        Assert.Equal(expected, experience.RequiredInitialization);
    }
}
