using System.Globalization;
using System.Text;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Pins the complete published built-in catalog snapshot, so a nondeterministic
/// load or an unreviewed behavior change in any published section is caught.
/// </summary>
public sealed class CanonicalCatalogSnapshotDigestTests
{
    // Pinned from the published snapshot of base 186036f8e (v1.1.11 product
    // behavior plus the Startup A unit 1/2 load changes, which keep results).
    // Re-pinned by NVT-END-FLAG-1113-01 (ADR 0076): the policy source hash,
    // the NT51950/NT51951 capability fingerprints and the dp-perspective
    // family version/hash changed; every section length is unchanged.
    // Re-pinned by TP-SVN-MODEL-1113-01: the policy source hash, every route
    // capability fingerprint and the re-pinned family versions/hashes changed;
    // static routes gain their display-only TP SVN metadata entries (length
    // 98164 -> 108496); the other section lengths are unchanged.
    // Decision 192 (WS-DPREGIONS): DP declaration identities re-pin the policy
    // source and seven AB Merge/AB CtrlRAM dynamic routes. Section lengths,
    // publication/evidence decisions and all other sections remain unchanged.
    // Decision 195 moves only the two 1024k B CMI ranges; its shared bundle
    // identity re-pins the same sections and seven routes without length changes.
    // Proposed ADR 0084 isolates nine NT51925 Candidate/ContractOnly declarations
    // in their own maps, families and bundles. Re-pinned from the production
    // catalog probe; existing NT51923/NT51926 identities and Golden expectations
    // are restored to v1.2.1. All nine NT51925 routes are unavailable, and the
    // selector excludes them from both workflows until owner evidence arrives.
    // A change here is a published catalog change and needs its own review.
    private const string PinnedSha256 =
        "ba14f4147b5286061c822e95c536cb75e1e9f628ee6bde03f87880fe2fb9e7d4";
    private const int PinnedLength = 537_918;
    private const int SequentialReloads = 5;
    private const int ConcurrentLoads = 4;

    private static readonly (string Name, int Length, string Sha256)[] PinnedSections =
    [
        ("catalog", 341, "72d959b58f8be5188eb8d6a0c92531a034d2e1c194fb5984956fde9763506d22"),
        ("static-routes", 125_051, "6917943ccaf4ecea6531dfeef304a18604882e3c2eaf2dfad5798f39175e181c"),
        ("dynamic-routes", 275_179, "bc261288a64ed276729627c3f453271131567a7f79e53a6ea3e5a04ad2922c20"),
        ("full-image-plans", 106_364, "3052ae411884f7c44713a18286360b2d4f1d410bf0c4bc97ab2c3f3c9dea2df9"),
        ("disclosure", 20_002, "dcd5a7655b7ea02a8ee601093ac02f449a3b7509828ef5dd8f94ae76f7bba8de"),
        ("selector", 10_957, "260184aa3b48b10e6e3bf7883ddab6d7a25a5659930b27fe00a6c77f0a28aafe"),
        ("certification", 24, "eb0edc192f3394a161de752c7d53cef86dd32bf94e352929b9f715db1efd5353"),
    ];

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(1);

    /// <summary>The production source publishes exactly the pinned catalog snapshot.</summary>
    [Fact]
    public void ProductionSourcePublishesThePinnedSnapshot()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());

        CanonicalCapabilityCatalogSnapshot snapshot =
            ReloadSucceeded(catalog, TestContext.Current.CancellationToken);

        AssertPinned(CanonicalCatalogSnapshotDigest.Create(snapshot), "production source");
    }

    /// <summary>Sequential and concurrent reloads of one catalog republish the same snapshot content.</summary>
    [Fact]
    public async Task SequentialAndConcurrentReloadsRepublishThePinnedSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        List<CanonicalCapabilityCatalogSnapshot> published = [];
        for (int reload = 0; reload < SequentialReloads; reload++)
        {
            published.Add(ReloadSucceeded(catalog, cancellationToken));
        }

        published.AddRange(await RunTogetherAsync(
            ConcurrentLoads,
            _ => ReloadSucceeded(catalog, cancellationToken),
            cancellationToken));

        Assert.Equal(
            published.Count,
            published.Select(static snapshot => snapshot.ResolutionToken).Distinct().Count());
        for (int index = 0; index < published.Count; index++)
        {
            AssertPinned(
                CanonicalCatalogSnapshotDigest.Create(published[index]),
                FormattableString.Invariant($"reload {index + 1} of {published.Count}"));
        }
    }

    /// <summary>Independent catalogs loading at the same time in one process publish the same content.</summary>
    [Fact]
    public async Task IndependentCatalogsLoadedInParallelPublishThePinnedSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CanonicalCapabilityCatalog[] catalogs =
        [
            .. Enumerable.Range(0, ConcurrentLoads).Select(static _ => new CanonicalCapabilityCatalog(
                CompositionHostServices.CreateCanonicalCapabilityCatalogSource())),
        ];

        CanonicalCapabilityCatalogSnapshot[] snapshots = await RunTogetherAsync(
            ConcurrentLoads,
            participant => ReloadSucceeded(catalogs[participant], cancellationToken),
            cancellationToken);

        for (int index = 0; index < snapshots.Length; index++)
        {
            AssertPinned(
                CanonicalCatalogSnapshotDigest.Create(snapshots[index]),
                FormattableString.Invariant($"parallel catalog {index + 1} of {snapshots.Length}"));
        }
    }

    /// <summary>A fresh product host graph loads through its startup loader to the same content.</summary>
    [Fact]
    public async Task FreshHostGraphPublishesThePinnedSnapshot()
    {
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        CapabilityCatalogReloadResult? result = null;
        await foreach (CanonicalCapabilityCatalogLoadUpdate update in
                       host.CanonicalCatalogLoader.LoadAsync(TestContext.Current.CancellationToken))
        {
            result = update.Result ?? result;
        }

        Assert.NotNull(result);
        Assert.True(result.Succeeded, DescribeIssues(result.Issues));
        CanonicalCapabilityCatalogSnapshot snapshot =
            Assert.IsType<CanonicalCapabilityCatalogSnapshot>(result.Snapshot);
        Assert.Same(snapshot, host.Catalog.GetCurrentSnapshot());
        AssertPinned(CanonicalCatalogSnapshotDigest.Create(snapshot), "fresh host graph");
    }

    private static CanonicalCapabilityCatalogSnapshot ReloadSucceeded(
        CanonicalCapabilityCatalog catalog,
        CancellationToken cancellationToken)
    {
        CapabilityCatalogReloadResult result = catalog.Reload(cancellationToken);
        Assert.True(result.Succeeded, DescribeIssues(result.Issues));
        Assert.False(result.RetainedLastKnownGood);
        return Assert.IsType<CanonicalCapabilityCatalogSnapshot>(result.Snapshot);
    }

    private static async Task<TResult[]> RunTogetherAsync<TResult>(
        int participants,
        Func<int, TResult> work,
        CancellationToken cancellationToken)
    {
        using var start = new Barrier(participants);
        Task<TResult>[] tasks =
        [
            .. Enumerable.Range(0, participants).Select(participant => Task.Factory.StartNew(
                () => start.SignalAndWait(StartTimeout, cancellationToken)
                    ? work(participant)
                    : throw new TimeoutException("Concurrent catalog loads did not start together."),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)),
        ];
        return await Task.WhenAll(tasks);
    }

    private static void AssertPinned(CanonicalCatalogSnapshotDigest digest, string context)
    {
        if (StringComparer.Ordinal.Equals(digest.Sha256, PinnedSha256))
        {
            return;
        }

        var message = new StringBuilder();
        _ = message
            .AppendLine(CultureInfo.InvariantCulture, $"The published catalog snapshot changed ({context}).")
            .AppendLine(CultureInfo.InvariantCulture, $"pinned sha256 {PinnedSha256}, length {PinnedLength}")
            .AppendLine(CultureInfo.InvariantCulture, $"actual sha256 {digest.Sha256}, length {digest.Text.Length}");
        int offset = 0;
        foreach (CanonicalCatalogDigestSection actual in digest.Sections)
        {
            (string Name, int Length, string Sha256)[] pinned =
            [
                .. PinnedSections.Where(section => StringComparer.Ordinal.Equals(section.Name, actual.Name)),
            ];
            bool changed = pinned.Length != 1 ||
                pinned[0].Length != actual.Length ||
                !StringComparer.Ordinal.Equals(pinned[0].Sha256, actual.Sha256);
            string expected = pinned.Length == 1
                ? FormattableString.Invariant($"length {pinned[0].Length}, sha256 {pinned[0].Sha256}")
                : "not pinned";
            string marker = changed ? " CHANGED" : string.Empty;
            _ = message.AppendLine(
                CultureInfo.InvariantCulture,
                $"section {actual.Name}: length {actual.Length}, sha256 {actual.Sha256} (pinned {expected}){marker}");
            int end = offset + actual.Length;
            if (changed)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(digest.Text[offset..end]);
            }

            offset = end;
        }

        _ = message.AppendLine("The canonical text of each changed section is in the test output.");
        Assert.Fail(message.ToString());
    }

    private static string DescribeIssues(IReadOnlyList<CapabilityCatalogIssue> issues)
    {
        return string.Join(
            Environment.NewLine,
            issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
