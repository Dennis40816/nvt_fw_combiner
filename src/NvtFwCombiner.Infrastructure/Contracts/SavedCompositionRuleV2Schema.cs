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

    private static readonly Lazy<JsonSchema> LazySchema = new(() =>
        ProfileBundleSchemaValidator.LoadEmbeddedSchema(
            typeof(SavedCompositionRuleV2Schema),
            ResourceName,
            SchemaId,
            $"Embedded Saved Composition Rule schema resource '{ResourceName}' is missing."));

    internal static bool IsValid(JsonElement document)
    {
        return ProfileBundleSchemaValidator.IsInstanceValid(
            LazySchema.Value,
            document);
    }

}
