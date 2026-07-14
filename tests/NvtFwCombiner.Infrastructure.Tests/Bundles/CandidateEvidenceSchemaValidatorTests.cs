using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests the pinned C# validator for the candidate-only evidence contract.</summary>
public sealed class CandidateEvidenceSchemaValidatorTests
{
    /// <summary>Accepts every complete candidate evidence document shape through the embedded schema.</summary>
    [Theory]
    [InlineData("intake-request")]
    [InlineData("candidate-source-bundle")]
    [InlineData("candidate-root-manifest")]
    [InlineData("candidate-validation-report")]
    public void ValidateDocumentAcceptsEveryCompleteCandidateDocument(string documentKind)
    {
        JsonObject document = CandidateEvidenceV1SchemaTests.CreateDocument(documentKind);

        CandidateEvidenceSchemaValidator.ValidateDocument(
            Encoding.UTF8.GetBytes(document.ToJsonString()),
            $"{documentKind}.json",
            262144,
            64);
    }

    /// <summary>Rejects documents that attempt to change candidate-only runtime authority.</summary>
    [Fact]
    public void ValidateDocumentRejectsRuntimeAuthorityEscalation()
    {
        JsonObject document = CandidateEvidenceV1SchemaTests.CreateDocument("candidate-source-bundle");
        document["runtimeAuthority"] = "runtime";

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CandidateEvidenceSchemaValidator.ValidateDocument(
                Encoding.UTF8.GetBytes(document.ToJsonString()),
                "source.json",
                262144,
                64));

        Assert.Contains(CandidateEvidenceSchema.SchemaId, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects duplicate keys before the document reaches the schema authority.</summary>
    [Fact]
    public void ValidateDocumentRejectsDuplicateKeys()
    {
        byte[] document = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"documentKind\":\"intake-request\",\"documentKind\":\"intake-request\"}");

        _ = Assert.Throws<JsonException>(() => CandidateEvidenceSchemaValidator.ValidateDocument(
            document,
            "request.json",
            262144,
            64));
    }

    /// <summary>Rejects oversized or over-nested candidate inputs before schema evaluation.</summary>
    [Fact]
    public void ValidateDocumentEnforcesPublishedParserLimits()
    {
        byte[] document = Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"nested\":{}}");

        _ = Assert.Throws<JsonException>(() => CandidateEvidenceSchemaValidator.ValidateDocument(
            document,
            "request.json",
            document.Length - 1,
            64));
        _ = Assert.ThrowsAny<JsonException>(() => CandidateEvidenceSchemaValidator.ValidateDocument(
            document,
            "request.json",
            262144,
            1));
    }
}
