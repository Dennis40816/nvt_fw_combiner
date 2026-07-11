using NvtFwCombiner.Contracts.Bundles;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Normalizes one schema-validated bundle manifest without granting trust.</summary>
internal static class ProfileBundleManifestNormalizer
{
    internal static ProfileBundleManifest Normalize(ProfileBundleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0"))
        {
            throw Error("schemaVersion", "Expected profile-bundle schema version '1.0'.");
        }

        if (!StringComparer.Ordinal.Equals(document.HashAlgorithm, "sha256-rfc8785-entry-array-v1"))
        {
            throw Error("hashAlgorithm", "Unknown bundle hash algorithm.");
        }

        ProfileBundleEntryDocument[] entryDocuments =
        [
            .. document.Entries ?? throw Error("entries", "Required array is missing."),
        ];
        var entries = new ProfileBundleEntry[entryDocuments.Length];
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var caseInsensitivePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var schemaIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entryDocuments.Length; index++)
        {
            ProfileBundleEntryDocument entryDocument = entryDocuments[index] ?? throw Error(
                $"entries[{index}]",
                "Bundle entry cannot be null.");
            string path = $"entries[{index}]";
            ProfileBundleEntryKind kind = NormalizeKind(entryDocument.Kind, $"{path}.kind");
            ValidateKindPath(kind, entryDocument.Path, $"{path}.path");
            if (!entryIds.Add(entryDocument.EntryId))
            {
                throw Error($"{path}.entryId", $"Duplicate bundle entry id '{entryDocument.EntryId}'.");
            }

            if (!paths.Add(entryDocument.Path))
            {
                throw Error($"{path}.path", $"Duplicate bundle entry path '{entryDocument.Path}'.");
            }

            if (!caseInsensitivePaths.TryAdd(entryDocument.Path, entryDocument.Path))
            {
                string existing = caseInsensitivePaths[entryDocument.Path];
                throw Error(
                    $"{path}.path",
                    $"Bundle entry path case-collides with '{existing}'.");
            }

            if (kind == ProfileBundleEntryKind.Schema && !schemaIds.Add(entryDocument.SchemaId))
            {
                throw Error($"{path}.schemaId", $"Duplicate schema id '{entryDocument.SchemaId}'.");
            }

            entries[index] = Wrap(path, () => new ProfileBundleEntry(
                entryDocument.EntryId,
                kind,
                entryDocument.Path,
                entryDocument.SchemaId,
                entryDocument.ContentHash));
        }

        foreach ((ProfileBundleEntry entry, int index) in entries.Select(static (entry, index) => (entry, index)))
        {
            if (entry.Kind != ProfileBundleEntryKind.Schema && !schemaIds.Contains(entry.SchemaId))
            {
                throw Error(
                    $"entries[{index}].schemaId",
                    $"Schema id '{entry.SchemaId}' is not listed by a schema entry.");
            }
        }

        string calculatedHash = ProfileBundleEntryArrayHasher.CalculateContentHash(entryDocuments);
        return !StringComparer.Ordinal.Equals(document.ContentHash, calculatedHash)
            ? throw Error("contentHash", "Bundle entry-array content hash does not match the manifest.")
            : Wrap("$", () => new ProfileBundleManifest(
            document.BundleId,
            document.BundleVersion,
            document.ContentHash,
            document.TrustAnchorBindingId,
            entries));
    }

    private static ProfileBundleEntryKind NormalizeKind(string value, string path)
    {
        return value switch
        {
            "schema" => ProfileBundleEntryKind.Schema,
            "firmware-family" => ProfileBundleEntryKind.FirmwareFamily,
            "composition-profile" => ProfileBundleEntryKind.CompositionProfile,
            "evidence-manifest" => ProfileBundleEntryKind.EvidenceManifest,
            "saved-composition-rule" => ProfileBundleEntryKind.SavedCompositionRule,
            _ => throw Error(path, "Unknown bundle entry kind."),
        };
    }

    private static void ValidateKindPath(ProfileBundleEntryKind kind, string value, string path)
    {
        string requiredPrefix = kind switch
        {
            ProfileBundleEntryKind.Schema => "schemas/",
            ProfileBundleEntryKind.FirmwareFamily => "families/",
            ProfileBundleEntryKind.CompositionProfile => "profiles/",
            ProfileBundleEntryKind.EvidenceManifest => "evidence/",
            ProfileBundleEntryKind.SavedCompositionRule => "saved-rules/",
            _ => throw new InvalidOperationException("Unknown normalized bundle entry kind."),
        };
        if (!value.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            throw Error(path, $"Bundle entry kind requires path prefix '{requiredPrefix}'.");
        }
    }

    private static T Wrap<T>(string path, Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch (ProfileBundleManifestNormalizationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static ProfileBundleManifestNormalizationException Error(
        string path,
        string message,
        Exception? innerException = null)
    {
        return new ProfileBundleManifestNormalizationException(path, message, innerException);
    }
}
