using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Loads canonical embedded schemas through one strict runtime authority.</summary>
internal static class EmbeddedVersionManagementSchema
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    internal static bool IsValid(JsonSchema schema, JsonElement document)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return schema.Evaluate(document, EvaluationOptions).IsValid;
    }

    internal static JsonDocument ParseStrict(ReadOnlyMemory<byte> utf8Json, int maximumDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        if (utf8Json.Span.StartsWith(Utf8Bom))
        {
            throw new JsonException("Canonical JSON must not contain a byte-order mark.");
        }

        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        };
        var reader = new Utf8JsonReader(utf8Json.Span, readerOptions);
        var objectKeys = new Stack<HashSet<string>?>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objectKeys.Push(new(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                objectKeys.Push(null);
            }
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                _ = objectKeys.Pop();
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string property = reader.GetString() ?? throw new JsonException("JSON property name is null.");
                if (objectKeys.Peek() is not { } keys || !keys.Add(property))
                {
                    throw new JsonException($"Duplicate JSON property '{property}'.");
                }
            }
        }

        return JsonDocument.Parse(utf8Json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = maximumDepth,
        });
    }

    internal static JsonSchema Load(
        Type assemblyMarker,
        string resourceName,
        string expectedId,
        string unavailableMessage)
    {
        ArgumentNullException.ThrowIfNull(assemblyMarker);
        Assembly assembly = assemblyMarker.Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException(unavailableMessage);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("$id", out JsonElement id) ||
            !string.Equals(id.GetString(), expectedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Canonical embedded schema identity is invalid.");
        }
        EvaluationResults metaValidation = MetaSchemas.Draft202012.Evaluate(
            document.RootElement,
            EvaluationOptions);
        _ = metaValidation.IsValid
            ? true
            : throw new InvalidOperationException("Canonical embedded schema is invalid.");

        return JsonSchema.FromText(document.RootElement.GetRawText(), new BuildOptions
        {
            SchemaRegistry = new SchemaRegistry(),
        });
    }
}

/// <summary>Canonical release-manifest contract used by managed installation.</summary>
internal static class ReleaseManifestSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(ReleaseManifestSchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.release-manifest-v1.schema.json",
        "https://schemas.example.invalid/nvt_fw_combiner/release-manifest-v1.schema.json",
        "Canonical release-manifest schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}

/// <summary>Canonical update-catalog contract used before DTO projection.</summary>
internal static class UpdateCatalogSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(UpdateCatalogSchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.update-catalog-v1.schema.json",
        "https://schemas.example.invalid/nvt_fw_combiner/update-catalog-v1.schema.json",
        "Canonical update-catalog schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}

/// <summary>Canonical fixed update-source registry contract.</summary>
internal static class UpdateSourceRegistrySchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(UpdateSourceRegistrySchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.update-source-registry-v1.schema.json",
        "https://schemas.example.invalid/nvt_fw_combiner/update-source-registry-v1.schema.json",
        "Canonical update-source registry schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}

/// <summary>Canonical strict launcher-bootstrap state contract.</summary>
internal static class LauncherBootstrapStateSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(LauncherBootstrapStateSchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.launcher-bootstrap-v1.schema.json",
        "https://schemas.example.invalid/nvt_fw_combiner/launcher-bootstrap-v1.schema.json",
        "Canonical launcher-bootstrap state schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}

/// <summary>Canonical distribution Launcher embedded-payload contract.</summary>
internal static class ManagedSetupPayloadAdmissionSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(ManagedSetupPayloadAdmissionSchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.managed-setup-payload-admission-v1.schema.json",
        "https://novatek.example/schema/managed-setup-payload-admission-v1.json",
        "Canonical managed-Setup payload schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}

/// <summary>Canonical first-install whole-root transaction contract.</summary>
internal static class ManagedSetupTransactionSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => EmbeddedVersionManagementSchema.Load(
        typeof(ManagedSetupTransactionSchema),
        "NvtFwCombiner.VersionManagement.Infrastructure.Contracts.managed-setup-transaction-v1.schema.json",
        "https://novatek.example/schema/managed-setup-transaction-v1.json",
        "Canonical managed-Setup transaction schema is unavailable."));

    internal static bool IsValid(JsonElement document)
    {
        return EmbeddedVersionManagementSchema.IsValid(Schema.Value, document);
    }
}
