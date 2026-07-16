using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Locks the proposed candidate IC workbook projection to one closed Draft 2020-12 shape.</summary>
public sealed class CandidateIcWorkbookSchemaContractTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/candidate-ic-workbook-v1.schema.json";

    private static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.Flag,
        RequireFormatValidation = true,
    };

    /// <summary>Proves the committed schema is a valid closed Draft 2020-12 document.</summary>
    [Fact]
    public void RepositorySchemaSatisfiesDraft202012()
    {
        _ = LoadSchema();
    }

    /// <summary>Proves the synthetic prose example stays executable and schema-valid.</summary>
    [Fact]
    public void DocumentationExampleSatisfiesSchema()
    {
        AssertValid(DocumentationExample());
    }

    /// <summary>Proves authority expansion, embedded paths, and ambiguous fact shapes fail closed.</summary>
    [Theory]
    [InlineData("support-promotion")]
    [InlineData("command-execution")]
    [InlineData("unknown-root-property")]
    [InlineData("embedded-artifact-path")]
    [InlineData("path-shaped-logical-name")]
    [InlineData("leading-logical-name-whitespace")]
    [InlineData("trailing-rationale-whitespace")]
    [InlineData("leading-citation-whitespace")]
    [InlineData("leading-statement-whitespace")]
    [InlineData("trailing-scalar-whitespace")]
    [InlineData("c1-control-statement")]
    [InlineData("artifact-role-source-mismatch")]
    [InlineData("empty-facts")]
    [InlineData("zero-length-range")]
    [InlineData("invalid-member-id")]
    [InlineData("zero-citation-ordinal")]
    [InlineData("scalar-type-mismatch")]
    [InlineData("embedded-review")]
    public void SchemaRejectsUnsafeOrAmbiguousProjectionShape(string mutation)
    {
        JsonObject workbook = ParseExample();
        ApplyMutation(workbook, mutation);

        AssertInvalid(workbook.ToJsonString());
    }

    private static void ApplyMutation(JsonObject workbook, string mutation)
    {
        switch (mutation)
        {
            case "support-promotion":
                workbook["capabilities"]!["supportPromotion"] = true;
                return;
            case "command-execution":
                workbook["capabilities"]!["commandExecution"] = true;
                return;
            case "unknown-root-property":
                workbook["unexpected"] = true;
                return;
            case "embedded-artifact-path":
                workbook["artifacts"]![0]!["relativePath"] = "private/input.bin";
                return;
            case "path-shaped-logical-name":
                workbook["artifacts"]![0]!["logicalName"] = "C:private.bin";
                return;
            case "leading-logical-name-whitespace":
                workbook["artifacts"]![0]!["logicalName"] = " map.txt";
                return;
            case "trailing-rationale-whitespace":
                workbook["rangeFacts"]![0]!["rationale"] = "Synthetic shape only. ";
                return;
            case "leading-citation-whitespace":
                workbook["citations"]![0]!["location"] = " Synthetic range row";
                return;
            case "leading-statement-whitespace":
                workbook["statementFacts"] = StatementFact(" unresolved statement");
                return;
            case "trailing-scalar-whitespace":
                workbook["scalarFacts"] = ScalarStringFact("declared value ");
                return;
            case "c1-control-statement":
                workbook["statementFacts"] = StatementFact("hidden\u0085text");
                return;
            case "artifact-role-source-mismatch":
                workbook["artifacts"]![0]!["sourceKind"] = "firmware";
                return;
            case "empty-facts":
                workbook["rangeFacts"] = new JsonArray();
                return;
            case "zero-length-range":
                workbook["rangeFacts"]![0]!["length"] = 0;
                return;
            case "invalid-member-id":
                workbook["rangeFacts"]![0]!["memberId"] = "nt00000";
                return;
            case "zero-citation-ordinal":
                workbook["citations"]![0]!["ordinal"] = 0;
                return;
            case "scalar-type-mismatch":
                workbook["scalarFacts"] = MismatchedScalarFact();
                return;
            case "embedded-review":
                workbook["reviews"] = new JsonArray();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }
    }

    private static JsonArray MismatchedScalarFact()
    {
        return
        [
            new JsonObject
            {
                ["factId"] = "synthetic-scalar",
                ["familyId"] = "nt-example",
                ["factKind"] = "capability",
                ["disposition"] = "unresolved",
                ["promotionImpact"] = "blocks-support",
                ["valueType"] = "integer",
                ["value"] = "1",
            },
        ];
    }

    private static JsonArray StatementFact(string text)
    {
        return
        [
            new JsonObject
            {
                ["factId"] = "synthetic-statement",
                ["familyId"] = "nt-example",
                ["factKind"] = "capability",
                ["disposition"] = "unresolved",
                ["promotionImpact"] = "blocks-support",
                ["text"] = text,
            },
        ];
    }

    private static JsonArray ScalarStringFact(string value)
    {
        return
        [
            new JsonObject
            {
                ["factId"] = "synthetic-scalar",
                ["familyId"] = "nt-example",
                ["factKind"] = "capability",
                ["disposition"] = "unresolved",
                ["promotionImpact"] = "blocks-support",
                ["valueType"] = "string",
                ["value"] = value,
            },
        ];
    }

    private static JsonSchema LoadSchema()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "candidate-ic-workbook-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(path, SchemaId, document.RootElement);
    }

    private static string DocumentationExample()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "candidate-ic-workbook-v1.md");
        string markdown = File.ReadAllText(path).ReplaceLineEndings("\n");
        const string opening = "```json\n";
        const string closing = "\n```";
        int start = markdown.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, "Candidate IC workbook documentation must contain one JSON example.");
        start += opening.Length;
        int end = markdown.IndexOf(closing, start, StringComparison.Ordinal);
        Assert.True(end > start, "Candidate IC workbook JSON example must have a closing fence.");
        Assert.Equal(-1, markdown.IndexOf(opening, end, StringComparison.Ordinal));
        return markdown[start..end];
    }

    private static JsonObject ParseExample()
    {
        return JsonNode.Parse(DocumentationExample())!.AsObject();
    }

    private static void AssertValid(string json)
    {
        Assert.True(Evaluate(json).IsValid);
    }

    private static void AssertInvalid(string json)
    {
        Assert.False(Evaluate(json).IsValid);
    }

    private static EvaluationResults Evaluate(string json)
    {
        using var document = JsonDocument.Parse(json);
        return LoadSchema().Evaluate(document.RootElement, Options);
    }
}
