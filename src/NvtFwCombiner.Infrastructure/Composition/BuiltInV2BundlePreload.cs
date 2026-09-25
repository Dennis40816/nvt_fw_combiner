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
    private const int WarmUpBundleCount = 2;

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
        if (!MatchesTrustIndex(BuiltInV2BundleRegistry.TrustIndex))
        {
            return;
        }

        FrozenDictionary<string, BuiltInV2Bundle> bundles = BuiltInV2BundleRegistry.All;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = WorkerCount,
            CancellationToken = cancellationToken,
        };
        for (int layer = 0; layer < Layers.Count; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int warmUp = layer == 0 ? WarmUpBundleCount : 0;
            foreach (string directory in Layers[layer].Take(warmUp))
            {
                bundles[directory].Preload();
                cancellationToken.ThrowIfCancellationRequested();
            }

            _ = Parallel.ForEach(Layers[layer].Skip(warmUp), options, directory => bundles[directory].Preload());
        }
    }

    /// <summary>
    /// True when the table names exactly the trusted bundles a load opens: every trusted bundle is in the
    /// table unless it is a General Merge logical candidate that provides no metadata or disclosure family.
    /// </summary>
    internal static bool MatchesTrustIndex(ProfileBundlePackageTrustIndex trustIndex)
    {
        ArgumentNullException.ThrowIfNull(trustIndex);
        HashSet<string> table = Layers.SelectMany(static layer => layer).ToHashSet(StringComparer.Ordinal);
        int matched = 0;
        foreach (ProfileBundlePackageTrustEntry entry in trustIndex.Bundles)
        {
            bool excluded = entry.MetadataProviderFamilies.Count == 0 &&
                entry.FamilyDisclosureFamilies.Count == 0 &&
                entry.RuntimeRegistrations.Count != 0 &&
                entry.RuntimeRegistrations.All(static registration =>
                    StringComparer.Ordinal.Equals(registration.WorkflowId, ExperienceIds.GeneralMerge));
            if (table.Contains(entry.BundleDirectory) == excluded)
            {
                return false;
            }

            matched += excluded ? 0 : 1;
        }

        return matched == table.Count;
    }
}
