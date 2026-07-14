using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Provides the immutable candidate-only schema used before candidate materialization.</summary>
internal static class CandidateEvidenceSchema
{
    internal const string SchemaId =
        "https://example.invalid/nfc/schemas/candidate-evidence-v1.schema.json";

    private const string ResourceName =
        "NvtFwCombiner.Infrastructure.Bundles.Schemas.candidate-evidence-v1.schema.json";

    private static readonly Lazy<JsonSchema> LazySchema = new(LoadSchema);

    internal static JsonSchema Schema => LazySchema.Value;

    private static JsonSchema LoadSchema()
    {
        Assembly assembly = typeof(CandidateEvidenceSchema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException(
            $"Embedded candidate evidence schema resource '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        return ClosedContentSchemaValidator.ParseSchema(
            ResourceName,
            SchemaId,
            document.RootElement,
            "Candidate evidence");
    }
}
