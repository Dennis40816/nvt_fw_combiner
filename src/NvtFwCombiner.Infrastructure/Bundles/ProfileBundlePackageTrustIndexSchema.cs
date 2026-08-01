using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Provides the closed package trust-index schema embedded in the host.</summary>
internal static class ProfileBundlePackageTrustIndexSchema
{
    internal const string SchemaId =
        "https://novatek.example/nvt-fw-combiner/profile-bundle-package-trust-index-v1.schema.json";

    private const string ResourceName =
        "NvtFwCombiner.Infrastructure.Bundles.Schemas.profile-bundle-package-trust-index-v1.schema.json";

    private static readonly Lazy<JsonSchema> LazySchema = new(LoadSchema);

    internal static JsonSchema Schema => LazySchema.Value;

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(ProfileBundlePackageTrustIndexSchema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"Embedded package trust-index schema resource '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        return ProfileBundleSchemaValidator.ParseSchema(
            ResourceName,
            SchemaId,
            document.RootElement);
    }
}
