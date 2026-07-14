using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Locks the candidate-only v1 evidence document contract.</summary>
public sealed class CandidateEvidenceV1SchemaTests
{
    private const string SchemaId =
        "https://example.invalid/nfc/schemas/candidate-evidence-v1.schema.json";

    /// <summary>Accepts each complete candidate-only document shape.</summary>
    [Theory]
    [InlineData("intake-request")]
    [InlineData("candidate-source-bundle")]
    [InlineData("candidate-root-manifest")]
    [InlineData("candidate-validation-report")]
    public void CandidateEvidenceSchemaAcceptsEachCompleteDocument(string documentKind)
    {
        Assert.True(IsValid(CreateDocument(documentKind)));
    }

    /// <summary>Rejects runtime escalation in every generated candidate document.</summary>
    [Theory]
    [InlineData("intake-request")]
    [InlineData("candidate-source-bundle")]
    [InlineData("candidate-root-manifest")]
    [InlineData("candidate-validation-report")]
    public void CandidateEvidenceSchemaRejectsRuntimeAuthorityEscalation(string documentKind)
    {
        JsonObject document = CreateDocument(documentKind);
        document["runtimeAuthority"] = "runtime";

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects a source path copied into the path-free source bundle.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsSourcePathInCandidateSourceBundle()
    {
        JsonObject document = CreateDocument("candidate-source-bundle");
        JsonObject artifact = Artifact(includeSourcePath: false);
        artifact["sourcePath"] = "private/owner-record.txt";
        document["sourceArtifacts"] = new JsonArray(artifact);

        Assert.False(IsValid(document));
    }

    /// <summary>Requires a non-empty list exactly when missing evidence is listed.</summary>
    [Theory]
    [InlineData("listed", 0, false)]
    [InlineData("owner-declared-none", 1, false)]
    [InlineData("listed", 1, true)]
    [InlineData("owner-declared-none", 0, true)]
    public void CandidateEvidenceSchemaEnforcesMissingEvidenceDisposition(
        string disposition,
        int gapCount,
        bool expectedValid)
    {
        JsonObject document = CreateDocument("candidate-source-bundle");
        document["missingEvidenceDisposition"] = disposition;
        JsonArray missingEvidence = gapCount == 0 ? [] : new JsonArray(MissingEvidence());
        document["missingEvidence"] = missingEvidence;

        Assert.Equal(expectedValid, IsValid(document));
    }

    /// <summary>Requires an explicit typed half-open range or an unresolved map-blocking statement.</summary>
    [Theory]
    [InlineData("typed", true)]
    [InlineData("unresolved-statement", true)]
    [InlineData("accepted-statement", false)]
    [InlineData("scalar", false)]
    public void CandidateEvidenceSchemaEnforcesRangeFactRule(string mutation, bool expectedValid)
    {
        JsonObject document = CreateDocument("intake-request");
        JsonObject fact = Fact();
        switch (mutation)
        {
            case "typed":
                break;
            case "unresolved-statement":
                fact["value"] = new JsonObject
                {
                    ["kind"] = "statement",
                    ["text"] = "Map review is pending.",
                };
                fact["disposition"] = "unresolved";
                fact["promotionImpact"] = "blocks-map-resolution";
                break;
            case "accepted-statement":
                fact["value"] = new JsonObject
                {
                    ["kind"] = "statement",
                    ["text"] = "Map review is pending.",
                };
                break;
            case "scalar":
                fact["value"] = new JsonObject
                {
                    ["kind"] = "scalar",
                    ["value"] = 1,
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown fact mutation.");
        }

        document["facts"] = new JsonArray(fact);
        Assert.Equal(expectedValid, IsValid(document));
    }

    /// <summary>Rejects unsafe copied diagnostics instead of emitting owner local paths.</summary>
    [Theory]
    [InlineData("C:\\owner\\firmware.bin")]
    [InlineData("\\\\owner-host\\share\\firmware.bin")]
    [InlineData("/private/firmware.bin")]
    public void CandidateEvidenceSchemaRejectsLocalPathSyntaxInReportSummary(string summary)
    {
        JsonObject document = CreateDocument("candidate-validation-report");
        JsonObject check = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["checks"])[0]);
        check["summary"] = summary;

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects copied path syntax in every non-source-path candidate field.</summary>
    [Theory]
    [InlineData("logical-name")]
    [InlineData("fact-rationale")]
    [InlineData("missing-evidence")]
    public void CandidateEvidenceSchemaRejectsLocalPathSyntaxInCopiedEvidence(string mutation)
    {
        JsonObject document = CreateDocument("candidate-source-bundle");
        switch (mutation)
        {
            case "logical-name":
                {
                    JsonObject artifact = Assert.IsType<JsonObject>(
                        Assert.IsType<JsonArray>(document["sourceArtifacts"])[0]);
                    artifact["logicalName"] = "C:\\private\\owner-record.txt";
                    break;
                }
            case "fact-rationale":
                {
                    JsonObject fact = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["facts"])[0]);
                    fact["rationale"] = "\\\\owner-host\\share\\evidence.txt";
                    break;
                }
            case "missing-evidence":
                {
                    JsonObject gap = Assert.IsType<JsonObject>(
                        Assert.IsType<JsonArray>(document["missingEvidence"])[0]);
                    gap["statement"] = "/private/evidence.txt";
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown evidence mutation.");
        }

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects materialized-root paths that escape through a parent segment.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsParentSegmentInRootManifestEntry()
    {
        JsonObject document = CreateDocument("candidate-root-manifest");
        JsonObject entry = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["entries"])[0]);
        entry["path"] = "schemas/../outside.json";

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects a root entry whose declared kind does not own its directory.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsRootEntryKindPathMismatch()
    {
        JsonObject document = CreateDocument("candidate-root-manifest");
        JsonObject entry = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["entries"])[0]);
        entry["kind"] = "artifact";

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects a validation report as a closed-root entry to prevent a report/hash cycle.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsValidationReportRootEntry()
    {
        JsonObject document = CreateDocument("candidate-root-manifest");
        JsonObject entry = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["entries"])[0]);
        entry["kind"] = "report";

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects a root manifest that changes the canonical entry-array hash algorithm.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsUnknownRootManifestHashAlgorithm()
    {
        JsonObject document = CreateDocument("candidate-root-manifest");
        document["hashAlgorithm"] = "sha256-unknown";

        Assert.False(IsValid(document));
    }

    /// <summary>Rejects artifact lengths outside the published per-artifact limit.</summary>
    [Fact]
    public void CandidateEvidenceSchemaRejectsOversizedArtifact()
    {
        JsonObject document = CreateDocument("intake-request");
        JsonObject artifact = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(document["sourceArtifacts"])[0]);
        artifact["sizeBytes"] = 16777217;

        Assert.False(IsValid(document));
    }

    private static bool IsValid(JsonObject document)
    {
        using var jsonDocument = JsonDocument.Parse(document.ToJsonString());
        return LoadSchema().Evaluate(jsonDocument.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
            RequireFormatValidation = true,
        }).IsValid;
    }

    private static JsonSchema LoadSchema()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "candidate-evidence-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(path, SchemaId, document.RootElement);
    }

    private static JsonObject CreateDocument(string documentKind)
    {
        return documentKind switch
        {
            "intake-request" => IntakeRequest(),
            "candidate-source-bundle" => SourceBundle(),
            "candidate-root-manifest" => RootManifest(),
            "candidate-validation-report" => ValidationReport(),
            _ => throw new ArgumentOutOfRangeException(nameof(documentKind), documentKind, "Unknown document kind."),
        };
    }

    private static JsonObject IntakeRequest()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "intake-request",
            ["requestId"] = "synthetic-request",
            ["manifestId"] = "synthetic-manifest",
            ["manifestVersion"] = "1.0.0",
            ["requestedAtUtc"] = "2026-07-14T00:00:00Z",
            ["owner"] = "firmware-owner",
            ["scope"] = Scope(),
            ["sourceArtifacts"] = new JsonArray(Artifact(includeSourcePath: true)),
            ["facts"] = new JsonArray(Fact()),
            ["missingEvidenceDisposition"] = "listed",
            ["missingEvidence"] = new JsonArray(MissingEvidence()),
        };
    }

    private static JsonObject SourceBundle()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-source-bundle",
            ["bundleId"] = "synthetic-source-bundle",
            ["requestId"] = "synthetic-request",
            ["requestContentHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["requestContentHash"] = Hash('a'),
            ["manifestId"] = "synthetic-manifest",
            ["manifestVersion"] = "1.0.0",
            ["requestedAtUtc"] = "2026-07-14T00:00:00Z",
            ["owner"] = "firmware-owner",
            ["scope"] = Scope(),
            ["sourceArtifacts"] = new JsonArray(Artifact(includeSourcePath: false)),
            ["facts"] = new JsonArray(Fact()),
            ["missingEvidenceDisposition"] = "listed",
            ["missingEvidence"] = new JsonArray(MissingEvidence()),
            ["intakeProvenance"] = new JsonObject
            {
                ["toolId"] = "candidate-intake",
                ["toolVersion"] = "0.9.4",
                ["generatedAtUtc"] = "2026-07-14T00:00:00Z",
                ["candidateOnly"] = true,
            },
            ["runtimeAuthority"] = "none",
        };
    }

    private static JsonObject RootManifest()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-root-manifest",
            ["rootId"] = "synthetic-root",
            ["sourceBundleEntryId"] = "candidate-source-bundle",
            ["contractSchemaEntryId"] = "candidate-schema",
            ["hashAlgorithm"] = "sha256-rfc8785-candidate-entry-array-v1",
            ["contentHash"] = Hash('a'),
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["entryId"] = "candidate-schema",
                    ["kind"] = "schema",
                    ["path"] = "schemas/candidate-evidence-v1.schema.json",
                    ["contentHash"] = Hash('a'),
                    ["sizeBytes"] = 1,
                },
                new JsonObject
                {
                    ["entryId"] = "candidate-source-bundle",
                    ["kind"] = "source-bundle",
                    ["path"] = "source/candidate-source-bundle.json",
                    ["contentHash"] = Hash('b'),
                    ["sizeBytes"] = 1,
                },
                new JsonObject
                {
                    ["entryId"] = "owner-record",
                    ["kind"] = "artifact",
                    ["path"] = "artifacts/owner-record.txt",
                    ["contentHash"] = Hash('c'),
                    ["sizeBytes"] = 1,
                }),
            ["runtimeAuthority"] = "none",
        };
    }

    private static JsonObject ValidationReport()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["documentKind"] = "candidate-validation-report",
            ["reportId"] = "synthetic-report",
            ["rootId"] = "synthetic-root",
            ["requestContentHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["requestContentHash"] = Hash('a'),
            ["rootContentHash"] = Hash('b'),
            ["rootManifestHashAlgorithm"] = "sha256-raw-utf8-v1",
            ["rootManifestSha256"] = Hash('c'),
            ["validatedEntryIds"] = new JsonArray(
                "candidate-schema",
                "candidate-source-bundle",
                "owner-record"),
            ["checks"] = new JsonArray(new JsonObject
            {
                ["checkId"] = "schema-validation",
                ["outcome"] = "passed",
                ["summary"] = "The candidate schema is valid.",
            }),
            ["validationOutcome"] = "passed",
            ["missingEvidenceDisposition"] = "listed",
            ["missingEvidence"] = new JsonArray(MissingEvidence()),
            ["runtimeAuthority"] = "none",
        };
    }

    private static JsonObject Scope()
    {
        return new JsonObject
        {
            ["memberIds"] = new JsonArray("NT51951"),
            ["modeIds"] = new JsonArray("ab-merge"),
            ["capacityBytes"] = new JsonArray(524288),
            ["topologyChoices"] = new JsonArray("single"),
        };
    }

    private static JsonObject Artifact(bool includeSourcePath)
    {
        var artifact = new JsonObject
        {
            ["artifactId"] = "owner-record",
            ["sourceKind"] = "owner-record",
            ["logicalName"] = "owner-record.txt",
            ["contentHash"] = Hash('a'),
            ["sizeBytes"] = 1,
        };
        if (includeSourcePath)
        {
            artifact["sourcePath"] = "owner-record.txt";
        }

        return artifact;
    }

    private static JsonObject Fact()
    {
        return new JsonObject
        {
            ["factId"] = "candidate-range",
            ["factKind"] = "range",
            ["value"] = new JsonObject
            {
                ["kind"] = "range",
                ["addressSpaceId"] = "flash",
                ["range"] = new JsonObject
                {
                    ["start"] = 0,
                    ["length"] = 1,
                },
            },
            ["disposition"] = "observed",
            ["promotionImpact"] = "blocks-map-resolution",
            ["citations"] = new JsonArray(new JsonObject
            {
                ["artifactId"] = "owner-record",
                ["location"] = "Sheet 1!A1",
            }),
        };
    }

    private static JsonObject MissingEvidence()
    {
        return new JsonObject
        {
            ["gapId"] = "combiner-replay",
            ["evidenceKind"] = "command-trace",
            ["statement"] = "The owner has not supplied a replayable Combiner command trace.",
            ["promotionImpact"] = "blocks-execution",
        };
    }

    private static string Hash(char character)
    {
        return new string(character, 64);
    }
}
