namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Pure byte transport for ADR 0077. Encoding never admits or normalizes a bundle.</summary>
internal static class PrebuiltProfileCatalogCodec
{
    /// <summary>
    /// Encodes canonical-header bundle order, each bundle's documents ordinal by entryId, then its manifest.
    /// The complete pack encoder must additionally budget its actual header against MaximumFileBytes.
    /// </summary>
    internal static PrebuiltProfileCatalogBody EncodeBody(IReadOnlyList<PrebuiltProfileCatalogBodyInput> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        if (bundles.Count is 0 or > PrebuiltProfileCatalogFormat.MaximumBodyBytes)
        {
            throw new InvalidDataException("Prebuilt catalog requires a bounded nonempty bundle set.");
        }

        // Bound the carried bytes before sorting inputs or allocating the output buffer/range table.
        int length = 0;
        foreach (PrebuiltProfileCatalogBodyInput bundle in bundles)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentException.ThrowIfNullOrWhiteSpace(bundle.BundleDirectory);
            ArgumentNullException.ThrowIfNull(bundle.Documents);
            length = AddLength(length, bundle.Manifest);
            foreach (ProfileBundleEntrySnapshot document in bundle.Documents)
            {
                ArgumentNullException.ThrowIfNull(document);
                if (document.Entry.Kind is not (ProfileBundleEntryKind.FirmwareFamily or ProfileBundleEntryKind.CompositionProfile))
                {
                    throw new InvalidDataException("Prebuilt catalog carries only family and profile documents.");
                }

                length = AddLength(length, document.FileSnapshot);
            }
        }

        var ordered = new List<(PrebuiltProfileCatalogBodyRange Range, ProfileBundleFileSnapshot Snapshot)>();
        int offset = 0;
        string? previousBundle = null;
        foreach (PrebuiltProfileCatalogBodyInput bundle in bundles.OrderBy(
                     static bundle => bundle.BundleDirectory, StringComparer.Ordinal))
        {
            if (StringComparer.Ordinal.Equals(previousBundle, bundle.BundleDirectory))
            {
                throw new InvalidDataException("Prebuilt catalog bundle directories must be unique.");
            }

            previousBundle = bundle.BundleDirectory;
            string? previousEntry = null;
            foreach (ProfileBundleEntrySnapshot document in bundle.Documents.OrderBy(
                         static document => document.Entry.EntryId, StringComparer.Ordinal))
            {
                if (StringComparer.Ordinal.Equals(previousEntry, document.Entry.EntryId))
                {
                    throw new InvalidDataException("Prebuilt catalog document entryIds must be unique within a bundle.");
                }

                previousEntry = document.Entry.EntryId;
                Append(bundle.BundleDirectory, document.Entry.EntryId, document.FileSnapshot);
            }

            Append(bundle.BundleDirectory, null, bundle.Manifest);
        }

        byte[] content = new byte[length];
        foreach ((PrebuiltProfileCatalogBodyRange range, ProfileBundleFileSnapshot snapshot) in ordered)
        {
            snapshot.Content.CopyTo(content.AsSpan(range.Offset, range.Length));
        }

        return new PrebuiltProfileCatalogBody(content, ordered.Select(static item => item.Range));

        void Append(string directory, string? entryId, ProfileBundleFileSnapshot snapshot)
        {
            ordered.Add((new PrebuiltProfileCatalogBodyRange(directory, entryId, offset, snapshot.Length), snapshot));
            offset = checked(offset + snapshot.Length);
        }
    }

    private static int AddLength(int current, ProfileBundleFileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Length == 0 || snapshot.Length > PrebuiltProfileCatalogFormat.MaximumBodyBytes - current
            ? throw new InvalidDataException("Prebuilt catalog body is empty or exceeds the file byte budget.")
            : checked(current + snapshot.Length);
    }
}
