using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Locks the proposed candidate-workspace manifest to one closed Draft 2020-12 shape.</summary>
public sealed class CandidateWorkspaceSchemaContractTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/candidate-workspace-v1.schema.json";

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

    /// <summary>Proves each generated artifact binds its kind, path, and approved schema.</summary>
    [Fact]
    public void GeneratedArtifactSatisfiesClosedKindPathAndSchemaBinding()
    {
        JsonObject workspace = ParseExample();
        workspace["generatedArtifacts"] = new JsonArray
        {
            new JsonObject
            {
                ["artifactId"] = "nt51950-candidate-family",
                ["kind"] = "firmware-family",
                ["path"] = "generated/families/nt51950-candidate-family.json",
                ["schemaId"] =
                    "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json",
                ["schemaContentHash"] = ZeroHash,
                ["sizeBytes"] = 2,
                ["contentHash"] = ZeroHash,
                ["sourceFactIds"] = new JsonArray("nt51950-map-observation"),
            },
        };

        AssertValid(workspace.ToJsonString());
    }

    /// <summary>Proves profile artifacts use the canonical schema id rather than a source filename.</summary>
    [Fact]
    public void GeneratedProfileRequiresCanonicalCompositionProfileSchemaId()
    {
        JsonObject workspace = ParseExample();
        workspace["generatedArtifacts"] = GeneratedProfile(
            "https://example.invalid/nfc/schemas/composition-profile-v2.schema.json");
        AssertValid(workspace.ToJsonString());

        workspace["generatedArtifacts"]![0]!["schemaId"] =
            "https://example.invalid/nfc/schemas/composition-profile-v2.7.schema.json";
        AssertInvalid(workspace.ToJsonString());
    }

    /// <summary>Proves parity diagnostics pin the installed Legacy Combiner binding exactly.</summary>
    [Fact]
    public void ParityReferenceRequiresExactLegacyCombinerBinding()
    {
        JsonObject workspace = ParseExample();
        workspace["diagnostics"]!["parity"] = new JsonObject
        {
            ["status"] = "failed",
            ["referenceKind"] = "legacy-combiner-1.13.0",
            ["expectedOutputHash"] = ZeroHash,
            ["actualOutputHash"] = ZeroHash,
            ["issueCodes"] = new JsonArray("parity-mismatch"),
        };
        AssertValid(workspace.ToJsonString());

        workspace["diagnostics"]!["parity"]!["referenceKind"] = "legacy-combiner-1.13";
        AssertInvalid(workspace.ToJsonString());
    }

    /// <summary>Proves authority expansion, path drift, and missing review gates fail closed.</summary>
    [Theory]
    [InlineData("support-promotion")]
    [InlineData("unknown-root-property")]
    [InlineData("compatibility-name-drift")]
    [InlineData("empty-residual-gates")]
    [InlineData("compiler-pass-without-fingerprint")]
    [InlineData("parity-pass-without-hashes")]
    [InlineData("artifact-path-escape")]
    [InlineData("artifact-kind-schema-mismatch")]
    public void SchemaRejectsUnsafeOrAmbiguousWorkspaceShape(string mutation)
    {
        JsonObject workspace = ParseExample();
        ApplyMutation(workspace, mutation);

        AssertInvalid(workspace.ToJsonString());
    }

    private static void ApplyMutation(JsonObject workspace, string mutation)
    {
        JsonObject diagnostics = workspace["diagnostics"]!.AsObject();
        switch (mutation)
        {
            case "support-promotion":
                workspace["capabilities"]!["supportPromotion"] = true;
                return;
            case "unknown-root-property":
                workspace["unexpected"] = true;
                return;
            case "compatibility-name-drift":
                workspace["compatibilityExport"]!["records"]![3]!["path"] =
                    "compatibility-export/extra.json";
                return;
            case "empty-residual-gates":
                diagnostics["residualGates"] = new JsonArray();
                return;
            case "compiler-pass-without-fingerprint":
                diagnostics["compiler"]!["status"] = "passed";
                return;
            case "parity-pass-without-hashes":
                diagnostics["parity"]!["status"] = "passed";
                return;
            case "artifact-path-escape":
                workspace["generatedArtifacts"] = GeneratedFamily("generated/families/../escape.json");
                return;
            case "artifact-kind-schema-mismatch":
                JsonArray artifacts = GeneratedFamily("generated/families/candidate.json");
                artifacts[0]!["kind"] = "profile-bundle";
                workspace["generatedArtifacts"] = artifacts;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }
    }

    private static JsonArray GeneratedFamily(string path)
    {
        return
        [
            new JsonObject
            {
                ["artifactId"] = "candidate-family",
                ["kind"] = "firmware-family",
                ["path"] = path,
                ["schemaId"] =
                    "https://example.invalid/nfc/schemas/firmware-family-v1.schema.json",
                ["schemaContentHash"] = ZeroHash,
                ["sizeBytes"] = 2,
                ["contentHash"] = ZeroHash,
                ["sourceFactIds"] = new JsonArray("declared-fact"),
            },
        ];
    }

    private static JsonArray GeneratedProfile(string schemaId)
    {
        return
        [
            new JsonObject
            {
                ["artifactId"] = "candidate-profile",
                ["kind"] = "composition-profile",
                ["path"] = "generated/profiles/candidate-profile.json",
                ["schemaId"] = schemaId,
                ["schemaContentHash"] = ZeroHash,
                ["sizeBytes"] = 2,
                ["contentHash"] = ZeroHash,
                ["sourceFactIds"] = new JsonArray("declared-fact"),
            },
        ];
    }

    private static JsonSchema LoadSchema()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "candidate-workspace-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(path, SchemaId, document.RootElement);
    }

    private static string DocumentationExample()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "candidate-workspace-v1.md");
        string markdown = File.ReadAllText(path).ReplaceLineEndings("\n");
        const string opening = "```json\n";
        const string closing = "\n```";
        int start = markdown.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, "Candidate workspace documentation must contain one JSON example.");
        start += opening.Length;
        int end = markdown.IndexOf(closing, start, StringComparison.Ordinal);
        Assert.True(end > start, "Candidate workspace JSON example must have a closing fence.");
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

    private static string ZeroHash => new('0', 64);
}
