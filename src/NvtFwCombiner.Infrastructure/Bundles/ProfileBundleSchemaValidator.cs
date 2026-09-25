using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Validates immutable bundle-entry snapshots against their closed Draft 2020-12 schemas.</summary>
internal static class ProfileBundleSchemaValidator
{
    private const string Draft202012SchemaId = "https://json-schema.org/draft/2020-12/schema";
    private const int MaximumCachedEntrySchemas = 64;

    // Built-in bundles repeat the same schema bytes; identical bytes always give the same verdict,
    // so only schemas that passed every check are reused. Failures are never cached.
    private static readonly ConcurrentDictionary<EntrySchemaKey, JsonSchema> ValidatedEntrySchemas = new();

    internal static void ValidateManifest(
        ProfileBundleFileSnapshot manifestSnapshot,
        int maximumJsonDepth)
    {
        ArgumentNullException.ThrowIfNull(manifestSnapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumJsonDepth);

        using JsonDocument document = manifestSnapshot.ParseStrictJson(maximumJsonDepth);
        ValidateInstance(
            ProfileBundleManifestSchema.Schema,
            document.RootElement,
            manifestSnapshot.ManifestPath,
            ProfileBundleManifestSchema.SchemaId);
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
                schemas.Add(entry.Entry.SchemaId, GetOrParseEntrySchema(entry, maximumJsonDepth));
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
                throw Error(
                    entry.Entry.Path,
                    $"Bundle entry references unavailable schema '{entry.Entry.SchemaId}'.");
            }

            using JsonDocument document = entry.FileSnapshot.ParseStrictJson(maximumJsonDepth);
            ValidateInstance(schema, document.RootElement, entry.Entry.Path, entry.Entry.SchemaId);
        }
    }

    private static JsonSchema GetOrParseEntrySchema(ProfileBundleEntrySnapshot entry, int maximumJsonDepth)
    {
        var key = new EntrySchemaKey(entry.Entry.SchemaId, entry.FileSnapshot.ActualSha256, maximumJsonDepth);
        if (ValidatedEntrySchemas.TryGetValue(key, out JsonSchema? cached))
        {
            return cached;
        }

        using JsonDocument document = entry.FileSnapshot.ParseStrictJson(maximumJsonDepth);
        JsonSchema schema = ParseSchema(entry.Entry.Path, entry.Entry.SchemaId, document.RootElement);
        return ValidatedEntrySchemas.Count < MaximumCachedEntrySchemas
            ? ValidatedEntrySchemas.GetOrAdd(key, schema)
            : schema;
    }

    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    internal static JsonSchema LoadEmbeddedSchema(
        Type assemblyMarker,
        string resourceName,
        string schemaId,
        string missingResourceMessage)
    {
        Assembly assembly = assemblyMarker.Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException(missingResourceMessage);
        using var document = JsonDocument.Parse(stream);
        return ParseSchema(resourceName, schemaId, document.RootElement);
    }

    internal static JsonSchema ParseSchema(string schemaPath, string schemaId, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Error(schemaPath, "Bundle schema root must be an object.");
        }

        ValidateRequiredRootString(root, "$schema", Draft202012SchemaId, schemaPath);
        ValidateRequiredRootString(root, "$id", schemaId, schemaPath);
        ValidateSchemaReferences(root, isRoot: true, schemaPath);

        EvaluationResults metaValidation = MetaSchemas.Draft202012.Evaluate(root, EvaluationOptions);
        if (!metaValidation.IsValid)
        {
            throw Error(schemaPath, "Bundle schema does not satisfy Draft 2020-12.");
        }

        try
        {
            return JsonSchema.FromText(root.GetRawText(), new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry(),
            });
        }
        catch (JsonSchemaException exception)
        {
            throw Error(schemaPath, "Bundle schema could not be parsed.", exception);
        }
        catch (JsonException exception)
        {
            throw Error(schemaPath, "Bundle schema could not be parsed.", exception);
        }
    }

    internal static bool IsInstanceValid(
        JsonSchema schema,
        JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return schema.Evaluate(document, EvaluationOptions).IsValid;
    }

    private static void ValidateInstance(
        JsonSchema schema,
        JsonElement document,
        string documentPath,
        string schemaId)
    {
        if (!IsInstanceValid(schema, document))
        {
            throw Error(documentPath, $"Bundle document does not satisfy schema '{schemaId}'.");
        }
    }

    private static void ValidateRequiredRootString(
        JsonElement root,
        string propertyName,
        string expectedValue,
        string entryPath)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            !StringComparer.Ordinal.Equals(property.GetString(), expectedValue))
        {
            throw Error(entryPath, $"Bundle schema requires {propertyName} '{expectedValue}'.");
        }
    }

    private static void ValidateSchemaReferences(JsonElement element, bool isRoot, string entryPath)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateSchemaReferences(item, isRoot: false, entryPath);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name is "$ref" or "$dynamicRef" or "$recursiveRef")
                {
                    if (property.Value.ValueKind != JsonValueKind.String ||
                        !property.Value.GetString()!.StartsWith('#'))
                    {
                        throw Error(entryPath, $"Bundle schema {property.Name} must be a local fragment reference.");
                    }
                }
                else if (!isRoot && property.Name is "$id" or "$schema")
                {
                    throw Error(entryPath, $"Bundle schema cannot declare nested {property.Name}.");
                }

                ValidateSchemaReferences(property.Value, isRoot: false, entryPath);
            }
        }
    }

    private static InvalidDataException Error(string entryPath, string message, Exception? innerException = null)
    {
        return new InvalidDataException($"Bundle schema validation failed for '{entryPath}': {message}", innerException);
    }

    private readonly record struct EntrySchemaKey(string SchemaId, string ContentSha256, int MaximumJsonDepth);
}
