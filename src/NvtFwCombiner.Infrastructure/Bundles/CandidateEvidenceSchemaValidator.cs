using System.Text.Json;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Validates one bounded candidate-only evidence document without granting runtime authority.</summary>
internal static class CandidateEvidenceSchemaValidator
{
    internal static void ValidateDocument(
        ReadOnlyMemory<byte> utf8Json,
        string documentPath,
        int maximumBytes,
        int maximumDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);

        using JsonDocument document = StrictJsonDocumentReader.Parse(utf8Json, maximumBytes, maximumDepth);
        ClosedContentSchemaValidator.ValidateInstance(
            CandidateEvidenceSchema.Schema,
            document.RootElement,
            documentPath,
            CandidateEvidenceSchema.SchemaId,
            "Candidate evidence");
    }
}
