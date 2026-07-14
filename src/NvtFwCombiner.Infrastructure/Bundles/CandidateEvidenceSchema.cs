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

    private static readonly Lazy<byte[]> LazyUtf8Content = new(LoadUtf8Content);
    private static readonly Lazy<JsonSchema> LazySchema = new(LoadSchema);

    internal static JsonSchema Schema => LazySchema.Value;

    internal static ReadOnlyMemory<byte> Utf8Content => LazyUtf8Content.Value;

    private static JsonSchema LoadSchema()
    {
        using JsonDocument document = StrictJsonDocumentReader.Parse(LazyUtf8Content.Value, 262144, 64);
        return ClosedContentSchemaValidator.ParseSchema(
            ResourceName,
            SchemaId,
            document.RootElement,
            "Candidate evidence");
    }

    private static byte[] LoadUtf8Content()
    {
        Assembly assembly = typeof(CandidateEvidenceSchema).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException(
            $"Embedded candidate evidence schema resource '{ResourceName}' is missing.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }
}
