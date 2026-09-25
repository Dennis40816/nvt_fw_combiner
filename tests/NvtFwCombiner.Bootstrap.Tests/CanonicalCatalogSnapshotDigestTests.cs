using System.Globalization;
using System.Text;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Pins the complete published built-in catalog snapshot, so a nondeterministic
/// load or an unreviewed behavior change in any published section is caught.
/// </summary>
public sealed class CanonicalCatalogSnapshotDigestTests
{
    // Pinned from the published snapshot of base 186036f8e (v1.1.11 product
    // behavior plus the Startup A unit 1/2 load changes, which keep results).
    // A change here is a published catalog change and needs its own review.
    private const string PinnedSha256 =
        "37ef0e538b60207086022067cf8aa6330a269d599242e5c4146af0d0cd92bf13";
    private const int PinnedLength = 483_532;
    private const int SequentialReloads = 5;
    private const int ConcurrentLoads = 4;

    private static readonly (string Name, int Length, string Sha256)[] PinnedSections =
    [
        ("catalog", 341, "3761dd12c7d885df046442f8524120c52ae9df5be2420af665785767917aadc5"),
        ("static-routes", 98_164, "8b89f41a7ffa923a93fbd5e7311987dab7533b67f636d2d8d748b80f61e609c8"),
        ("dynamic-routes", 248_943, "c8d036f838a38624434215e57934ad2cb1813b08abf0f78381d3bad42c1b81af"),
        ("full-image-plans", 106_364, "fa2db5740e4b3de6cdf84a3edc3b387c98ffc7cdc710be469587b16a6dbe7947"),
        ("disclosure", 18_861, "c5f8148381d38b36373194f60de8a6450df680f910394a9dc257ad32ac32fe2f"),
        ("selector", 10_835, "41e00a86903342faa9c1a739234d06a3bf9f1423cba74b24ad83a0da1ac89d55"),
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
        CompositionHostServices host = CompositionHostServices.Create();
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
