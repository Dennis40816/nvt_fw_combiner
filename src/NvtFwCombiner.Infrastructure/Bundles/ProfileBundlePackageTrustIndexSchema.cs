using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Provides the closed package trust-index schema embedded in the host.</summary>
internal static class ProfileBundlePackageTrustIndexSchema
{
    internal const string SchemaId =
        "https://novatek.example/nvt-fw-combiner/profile-bundle-package-trust-index-v1.schema.json";

    private const string ResourceName =
        "NvtFwCombiner.Infrastructure.Bundles.Schemas.profile-bundle-package-trust-index-v1.schema.json";

    private static readonly Lazy<JsonSchema> LazySchema = new(() =>
        ProfileBundleSchemaValidator.LoadEmbeddedSchema(
            typeof(ProfileBundlePackageTrustIndexSchema),
            ResourceName,
            SchemaId,
            $"Embedded package trust-index schema resource '{ResourceName}' is missing."));

    internal static JsonSchema Schema => LazySchema.Value;
}
