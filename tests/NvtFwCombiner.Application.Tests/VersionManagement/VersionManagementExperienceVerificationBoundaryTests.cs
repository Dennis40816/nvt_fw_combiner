using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>A candidate attached to a typed verification failure is never published.</summary>
    [Fact]
    public async Task FailedVerificationWithCandidateNeverPublishesVerifiedCandidate()
    {
        UpdateCatalogSnapshot catalog = Catalog("0.10.6");
        UpdateCatalogVersionSnapshot package = Assert.Single(catalog.Versions);
        var candidate = new VerifiedUpdateCandidate(
            package.Version,
            package.Identity,
            package.ReleaseNotes);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(State(
                [Admission("0.10.5")],
                active: "0.10.5",
                lastKnownGood: "0.10.5")),
            new FixedCatalogSource(catalog),
            new HealthyRepository(verificationResult: new(
                candidate,
                ManagedVersionInstallIssue.PackageMismatch)));

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Invalid, snapshot.SourceStatus);
        Assert.Null(snapshot.VerifiedCandidate);
        Assert.False(snapshot.ShouldPromptForUpdate);
    }

    /// <summary>A verified result must retain the exact catalog admission identity.</summary>
    [Fact]
    public async Task VerifiedCandidateMustMatchExactCatalogAdmission()
    {
        UpdateCatalogSnapshot catalog = Catalog("0.10.6");
        UpdateCatalogVersionSnapshot package = Assert.Single(catalog.Versions);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(State(
                [Admission("0.10.5")],
                active: "0.10.5",
                lastKnownGood: "0.10.5")),
            new FixedCatalogSource(catalog),
            new HealthyRepository(verificationResult: new(
                new(package.Version, "different-admission", package.ReleaseNotes),
                ManagedVersionInstallIssue.None)));

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Invalid, snapshot.SourceStatus);
        Assert.Null(snapshot.VerifiedCandidate);
        Assert.False(snapshot.ShouldPromptForUpdate);
    }

    /// <summary>A verified result for another version cannot consume the current prompt.</summary>
    [Fact]
    public async Task MismatchedVerifiedVersionNeverPrompts()
    {
        UpdateCatalogSnapshot catalog = Catalog("0.10.6");
        UpdateCatalogVersionSnapshot package = Assert.Single(catalog.Versions);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.5"),
            "managed-root",
            new MemoryStateStore(State(
                [Admission("0.10.5")],
                active: "0.10.5",
                lastKnownGood: "0.10.5")),
            new FixedCatalogSource(catalog),
            new HealthyRepository(verificationResult: new(
                new(ManagedAppVersion.Parse("0.10.7"), package.Identity, package.ReleaseNotes),
                ManagedVersionInstallIssue.None)));

        VersionManagementSnapshot snapshot = await experience.CheckAsync(
            isAutomatic: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(VersionSourceStatus.Invalid, snapshot.SourceStatus);
        Assert.Null(snapshot.VerifiedCandidate);
        Assert.False(snapshot.ShouldPromptForUpdate);
    }
}
