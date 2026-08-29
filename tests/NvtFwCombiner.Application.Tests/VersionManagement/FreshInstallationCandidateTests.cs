using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

public sealed partial class VersionManagementExperienceTests
{
    /// <summary>Fresh admission accepts a current-version package without touching durable state.</summary>
    [Fact]
    public async Task FreshAdmissionAcceptsEqualVersionAndIsStateFree()
    {
        string latest = SourcePath("fresh-equal");
        SequenceRegistrySource registry = new(Registry(
            revision: 10,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest)));
        CountingRepository repository = new();
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            repository,
            registry);

        FreshInstallationCandidateResult result = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal(ManagedAppVersion.Parse("0.10.6"), result.Candidate!.Package.Version);
        Assert.Equal(latest, result.Candidate.Identity.SourceRoot);
        Assert.Equal(10, result.Candidate.Identity.RegistryRevision);
        Assert.Equal(CatalogContentDigest, result.Candidate.Identity.CatalogDigest);
        Assert.Equal([latest], repository.VerifiedRoots);
        Assert.Equal(1, registry.LoadCount);
    }

    /// <summary>Fresh admission follows Registry ordering and accepts a verified fallback only.</summary>
    [Fact]
    public async Task FreshAdmissionUsesFirstCompletelyVerifiedAutomaticCandidate()
    {
        string latest = SourcePath("fresh-invalid-latest");
        string available = SourcePath("fresh-valid-fallback");
        CountingRepository repository = new(ManagedAppVersion.Parse("0.10.6"));
        SelectiveVerificationRepository healthyRepository = new(repository, available);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource(
                (latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)),
                (available, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            healthyRepository,
            new SequenceRegistrySource(Registry(
                revision: 11,
                SecondRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest),
                (available, UpdateSourceRegistryEntryStatus.Available))));

        FreshInstallationCandidateResult result = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Issue.ToString());
        Assert.Equal(available, result.Candidate!.Identity.SourceRoot);
        Assert.Equal([latest, available], healthyRepository.VerifiedRoots);
    }

    /// <summary>First install never downgrades below the distribution Launcher version.</summary>
    [Fact]
    public async Task FreshAdmissionRejectsPackageOlderThanDistributionLauncher()
    {
        string latest = SourcePath("fresh-older");
        CountingRepository repository = new();
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(Catalog("0.10.5"), UpdateCatalogLoadIssue.None))),
            repository,
            new SequenceRegistrySource(RegistryWithPublication(
                "0.10.5",
                1,
                CatalogContentDigest,
                12,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        FreshInstallationCandidateResult result = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(FreshInstallationCandidateIssue.CandidateUnavailable, result.Issue);
    }

    /// <summary>First install rejects a verified legacy package that has no launchable Launcher.</summary>
    [Fact]
    public async Task FreshAdmissionRejectsVerifiedPackageWithoutSupportedManagedLauncher()
    {
        string latest = SourcePath("fresh-schema-1-1");
        UpdateCatalogVersionSnapshot package = Catalog("0.10.6").Versions[0];
        var legacyVerification = new ManagedPackageVerificationResult(
            new(package.Version, package.Identity, package.ReleaseNotes),
            ManagedVersionInstallIssue.None);
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new HealthyRepository(verificationResult: legacyVerification),
            new SequenceRegistrySource(Registry(
                13,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        FreshInstallationCandidateResult result = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(FreshInstallationCandidateIssue.SourceRejected, result.Issue);
    }

    /// <summary>An unobservable automatic source stays unavailable rather than becoming no-candidate.</summary>
    [Fact]
    public async Task FreshAdmissionPreservesSourceUnavailableWhenNoCandidateCanBeObserved()
    {
        string latest = SourcePath("fresh-unavailable");
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(null, UpdateCatalogLoadIssue.SourceUnavailable))),
            new CountingRepository(),
            new SequenceRegistrySource(Registry(
                13,
                SecondRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));

        FreshInstallationCandidateResult result = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(FreshInstallationCandidateIssue.SourceUnavailable, result.Issue);
    }

    /// <summary>Exact revalidation verifies the captured Registry entry instead of selecting latest again.</summary>
    [Fact]
    public async Task ExactRevalidationRejectsPublicationChangeBeforeCatalogOrPackageRead()
    {
        string latest = SourcePath("fresh-drift");
        SequenceRegistrySource registry = new(
            Registry(10, FirstRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)),
            Registry(11, SecondRegistryDigest, (latest, UpdateSourceRegistryEntryStatus.Latest)));
        PathCatalogSource catalog = new((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None)));
        CountingRepository repository = new();
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            catalog,
            repository,
            registry);
        FreshInstallationCandidateResult captured = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        FreshInstallationCandidateResult reverified = await experience
            .ReverifyFreshInstallationAsync(captured.Candidate!, TestContext.Current.CancellationToken);

        Assert.True(captured.IsSuccess);
        Assert.Equal(FreshInstallationCandidateIssue.SourceChanged, reverified.Issue);
        Assert.Equal(latest, Assert.Single(catalog.LoadedRoots));
        Assert.Equal(latest, Assert.Single(repository.VerifiedRoots));
        Assert.Equal(2, registry.LoadCount);
    }

    /// <summary>Unchanged exact authority produces a value-identical immutable token.</summary>
    [Fact]
    public async Task ExactRevalidationReturnsSameClosedIdentity()
    {
        string latest = SourcePath("fresh-stable");
        UpdateSourceRegistryLoadResult publication = Registry(
            12,
            FirstRegistryDigest,
            (latest, UpdateSourceRegistryEntryStatus.Latest));
        CountingRepository repository = new();
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            repository,
            new SequenceRegistrySource(publication, publication));
        FreshInstallationCandidateResult captured = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);

        FreshInstallationCandidateResult reverified = await experience
            .ReverifyFreshInstallationAsync(captured.Candidate!, TestContext.Current.CancellationToken);

        Assert.True(reverified.IsSuccess, reverified.Issue.ToString());
        Assert.Equal(captured.Candidate!.Identity, reverified.Candidate!.Identity);
        Assert.Equal(2, repository.VerifiedRoots.Count);
    }

    /// <summary>The public fresh token cannot combine a verified package with another admission.</summary>
    [Fact]
    public async Task FreshCandidateRejectsMismatchedVerifiedAdmission()
    {
        string latest = SourcePath("fresh-closed-token");
        using VersionManagementExperience experience = VersionManagementExperienceTestFactory.Create(
            ManagedAppVersion.Parse("0.10.6"),
            "managed-root",
            new ForbiddenStateStore(),
            new PathCatalogSource((latest, new(Catalog("0.10.6"), UpdateCatalogLoadIssue.None))),
            new CountingRepository(),
            new SequenceRegistrySource(Registry(
                14,
                FirstRegistryDigest,
                (latest, UpdateSourceRegistryEntryStatus.Latest))));
        FreshInstallationCandidateResult captured = await experience.InspectFreshInstallationAsync(
            TestContext.Current.CancellationToken);
        FreshInstallationCandidate candidate = Assert.IsType<FreshInstallationCandidate>(captured.Candidate);

        _ = Assert.Throws<ArgumentException>(() => new FreshInstallationCandidate(
            candidate.Identity,
            candidate.Package,
            new VerifiedUpdateCandidate(
                candidate.Package.Version,
                "another-admission",
                candidate.Package.ReleaseNotes)));
    }

    /// <summary>The public fresh identity rejects noncanonical authority digests.</summary>
    [Fact]
    public void FreshIdentityRejectsInvalidAuthorityDigest()
    {
        string sourceRoot = SourcePath("fresh-invalid-identity");
        UpdateCatalogVersionSnapshot package = Catalog("0.10.6").Versions[0];

        _ = Assert.Throws<ArgumentException>(() => new FreshInstallationCandidateIdentity(
            "nvt-fw-combiner-production",
            15,
            "invalid",
            1,
            package.Version,
            CatalogContentDigest,
            Path.Combine(sourceRoot, "update-catalog.v1.json"),
            sourceRoot,
            UpdateSourceRegistryEntryStatus.Latest,
            package.PackagePath.Value,
            package.PackageSize,
            package.PackageSha256,
            package.ReleaseManifestSha256));
    }

    private sealed class ForbiddenStateStore : IVersionManagerStateStore
    {
        public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
            TimeSpan waitTimeout,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fresh admission must not acquire durable state authority.");
        }

        public ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fresh admission must not read durable state.");
        }

        public ValueTask SaveAsync(VersionManagerState state, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fresh admission must not write durable state.");
        }
    }

    private sealed class SelectiveVerificationRepository(
        IManagedVersionRepository inner,
        string admittedRoot) : IManagedVersionRepository
    {
        internal List<string> VerifiedRoots { get; } = [];

        public ValueTask<ManagedPackageVerificationResult> VerifyPackageAsync(
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            VerifiedRoots.Add(sourceRoot);
            return string.Equals(sourceRoot, admittedRoot, StringComparison.Ordinal)
                ? new HealthyRepository().VerifyPackageAsync(sourceRoot, package, cancellationToken)
                : inner.VerifyPackageAsync(sourceRoot, package, cancellationToken);
        }

        public ValueTask<ManagedVersionInstallResult> InstallAsync(
            string managedRoot,
            string sourceRoot,
            UpdateCatalogVersionSnapshot package,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionInventoryReadResult> InventoryAsync(
            string managedRoot,
            IReadOnlyList<ManagedVersionAdmission> admissions,
            ManagedAppVersion? activeVersion,
            ManagedAppVersion? lastKnownGoodVersion,
            ManagedAppVersion? failedActivationVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ManagedVersionDeleteIssue> DeleteAsync(
            string managedRoot,
            ManagedVersionAdmission admission,
            ManagedAppVersion? activeVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
