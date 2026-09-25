using System.Collections.Frozen;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>
/// Preloads the built-in bundle catalogs a catalog load opens, in fixed dependency layers (ADR 0075).
/// </summary>
/// <remarks>
/// A bundle resolves metadata only from bundles of earlier layers, so a worker never waits for another
/// worker's lazy initialization. The warm-up bundles load alone first so shared type initializers and
/// embedded schemas are ready before any worker starts. Preloading decides nothing: the serial catalog
/// pass still reports every result and failure in plan order.
/// </remarks>
internal static class BuiltInV2BundlePreload
{
    /// <summary>The first layer-0 bundle loads alone before any worker starts (ADR 0075 decision 2).</summary>
    internal const int WarmUpBundleCount = 1;

    /// <summary>The only trusted bundles a catalog load does not open: General Merge logical candidates.</summary>
    internal static IReadOnlyList<string> ExcludedBundles { get; } =
    [
        "nt51917-nt51927-general-merge-logical-candidate",
        "nt51919-nt51929-nt51932-general-merge-logical-candidate",
        "nt51923-nt51926-general-merge-logical-candidate",
        "nt51928-general-merge-logical-candidate",
        "nt51950-nt51951-general-merge-logical-candidate",
    ];

    /// <summary>Bundle directories per dependency layer, pinned against the built reference graph by test.</summary>
    internal static IReadOnlyList<IReadOnlyList<string>> Layers { get; } =
    [
        [
            "nt51919-nt51929-nt51932-shared-facts",
            "nt51923-ctrlram-replace-candidate",
            "nt51917-ctrlram-replace-alias-candidate",
            "nt51926-ctrlram-replace-candidate",
            "nt51927-ctrlram-replace-candidate",
            "nt51928-ctrlram-replace-candidate",
            "nt51929-ctrlram-replace-candidate",
            "nt51932-ctrlram-replace-candidate",
            "nt51950-ctrlram-replace-candidate",
            "nt51951-ctrlram-replace-candidate",
        ],
        ["nt51927-standard-merge"],
        [
            "nt51917-nt51927-shared-facts",
            "nt51923-nt51926-shared-facts",
            "nt51923-standard-merge",
            "nt51928-standard-merge",
            "nt51929-standard-merge",
            "nt51950-ab-merge",
            "nt51950-nt51951-standard-merge",
        ],
        ["nt51919-nt51929-nt51932-ab-merge"],
    ];

    internal static int WorkerCount => Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1));

    /// <summary>Loads every layer, waiting for each layer before the next; only cancellation escapes.</summary>
    internal static void Run(CancellationToken cancellationToken)
    {
        if (!MatchesTrustIndex(BuiltInV2BundleRegistry.TrustIndex.Bundles))
        {
            return;
        }

        FrozenDictionary<string, BuiltInV2Bundle> bundles = BuiltInV2BundleRegistry.All;
        RunLayers(Layers, directory => bundles[directory].Preload(), WorkerCount, cancellationToken);
    }

    /// <summary>
    /// Runs the warm-up entry alone, then every layer on at most <paramref name="workerCount"/> workers with a
    /// barrier between layers. Cancellation stops scheduling; started entries finish before it propagates.
    /// </summary>
    internal static void RunLayers(
        IReadOnlyList<IReadOnlyList<string>> layers,
        Action<string> preload,
        int workerCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(preload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = workerCount,
            CancellationToken = cancellationToken,
        };
        for (int layer = 0; layer < layers.Count; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int warmUp = layer == 0 ? WarmUpBundleCount : 0;
            foreach (string directory in layers[layer].Take(warmUp))
            {
                preload(directory);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _ = Parallel.ForEach(layers[layer].Skip(warmUp), options, preload);
        }
    }

    /// <summary>
    /// True only when the trusted bundles are exactly the table plus the fixed excluded logical candidates, and
    /// each excluded bundle is still a General Merge logical candidate that provides no metadata or disclosure.
    /// Anything else leaves loading serial (ADR 0075 decision 3).
    /// </summary>
    internal static bool MatchesTrustIndex(IReadOnlyList<ProfileBundlePackageTrustEntry> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        HashSet<string> table = Layers.SelectMany(static layer => layer).ToHashSet(StringComparer.Ordinal);
        HashSet<string> expected = [.. table, .. ExcludedBundles];
        HashSet<string> actual = bundles.Select(static entry => entry.BundleDirectory).ToHashSet(StringComparer.Ordinal);
        return actual.Count == bundles.Count &&
            actual.SetEquals(expected) &&
            bundles.Where(entry => !table.Contains(entry.BundleDirectory)).All(static entry =>
                entry.MetadataProviderFamilies.Count == 0 &&
                entry.FamilyDisclosureFamilies.Count == 0 &&
                entry.RuntimeRegistrations.Count != 0 &&
                entry.RuntimeRegistrations.All(static registration =>
                    StringComparer.Ordinal.Equals(registration.WorkflowId, ExperienceIds.GeneralMerge)));
    }
}
