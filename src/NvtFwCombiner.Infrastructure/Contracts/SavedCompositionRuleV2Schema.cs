using System.Reflection;
using System.Text.Json;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Contracts;

/// <summary>
/// Evaluates Saved Composition Rule v2 documents against the one canonical
/// checked-in Draft 2020-12 schema embedded at build time.
/// </summary>
internal static class SavedCompositionRuleV2Schema
{
    internal const string SchemaId =
        "https://example.invalid/nfc/schemas/saved-composition-rule-v2.schema.json";

    private const string ResourceName =
        "NvtFwCombiner.Infrastructure.Contracts.Schemas.saved-composition-rule-v2.schema.json";

    private static readonly Lazy<JsonSchema> LazySchema = new(LoadSchema);

    internal static bool IsValid(JsonElement document)
    {
        return ProfileBundleSchemaValidator.IsInstanceValid(
            LazySchema.Value,
            document);
    }

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(SavedCompositionRuleV2Schema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"Embedded Saved Composition Rule schema resource '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        return ProfileBundleSchemaValidator.ParseSchema(
            ResourceName,
            SchemaId,
            document.RootElement);
    }
}
