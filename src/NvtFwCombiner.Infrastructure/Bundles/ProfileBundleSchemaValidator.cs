using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Validates immutable bundle-entry snapshots against their closed Draft 2020-12 schemas.</summary>
internal static class ProfileBundleSchemaValidator
{
    internal static void ValidateManifest(
        ProfileBundleFileSnapshot manifestSnapshot,
        int maximumJsonDepth)
    {
        ArgumentNullException.ThrowIfNull(manifestSnapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);

        using JsonDocument document = manifestSnapshot.ParseStrictJson(maximumJsonDepth);
        ClosedContentSchemaValidator.ValidateInstance(
            ProfileBundleManifestSchema.Schema,
            document.RootElement,
            manifestSnapshot.ManifestPath,
            ProfileBundleManifestSchema.SchemaId,
            "Bundle");
    }

    internal static void ValidateEntries(
        ProfileBundleEntrySnapshotCollection collection,
        int maximumJsonDepth)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);

        var schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (ProfileBundleEntrySnapshot entry in collection.Entries)
        {
            if (entry.Entry.Kind == ProfileBundleEntryKind.Schema)
            {
                using JsonDocument document = entry.FileSnapshot.ParseStrictJson(maximumJsonDepth);
                schemas.Add(
                    entry.Entry.SchemaId,
                    ParseSchema(entry.Entry.Path, entry.Entry.SchemaId, document.RootElement));
            }
        }

        foreach (ProfileBundleEntrySnapshot entry in collection.Entries)
        {
            if (entry.Entry.Kind == ProfileBundleEntryKind.Schema)
            {
                continue;
            }

            if (!schemas.TryGetValue(entry.Entry.SchemaId, out JsonSchema? schema))
            {
                throw new InvalidDataException(
                    $"Bundle schema validation failed for '{entry.Entry.Path}': " +
                    $"Bundle entry references unavailable schema '{entry.Entry.SchemaId}'.");
            }

            using JsonDocument document = entry.FileSnapshot.ParseStrictJson(maximumJsonDepth);
            ClosedContentSchemaValidator.ValidateInstance(
                schema,
                document.RootElement,
                entry.Entry.Path,
                entry.Entry.SchemaId,
                "Bundle");
        }
    }

    internal static JsonSchema ParseSchema(string schemaPath, string schemaId, JsonElement root)
    {
        return ClosedContentSchemaValidator.ParseSchema(schemaPath, schemaId, root, "Bundle");
    }
}
