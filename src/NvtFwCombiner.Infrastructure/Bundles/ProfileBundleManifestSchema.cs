using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Provides the immutable bootstrap schema used before a bundle can trust its own entries.</summary>
internal static class ProfileBundleManifestSchema
{
    internal const string SchemaId = "https://example.invalid/nfc/schemas/profile-bundle-v1.schema.json";

    private const string ResourceName =
        "NvtFwCombiner.Infrastructure.Bundles.Schemas.profile-bundle-v1.schema.json";

    private static readonly Lazy<JsonSchema> LazySchema = new(LoadSchema);

    internal static JsonSchema Schema => LazySchema.Value;

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(ProfileBundleManifestSchema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException(
            $"Embedded profile-bundle schema resource '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        return ProfileBundleSchemaValidator.ParseSchema(ResourceName, SchemaId, document.RootElement);
    }
}
