using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Locks the candidate evidence range rule in the executable Draft 2020-12 contract.</summary>
public sealed class CandidateEvidenceManifestSchemaTests
{
    private const string SchemaId = "https://example.invalid/nfc/schemas/firmware-evidence-manifest-v1.schema.json";

    /// <summary>Accepts only a typed range or the closed unresolved map-blocking statement exception.</summary>
    [Theory]
    [InlineData("typed-range", true)]
    [InlineData("unresolved-statement", true)]
    [InlineData("accepted-statement", false)]
    [InlineData("unresolved-scalar", false)]
    [InlineData("unresolved-reference", false)]
    public void EvidenceManifestEnforcesRangeValueDispositionAndPromotionRule(string mutation, bool expectedValid)
    {
        JsonSchema schema = LoadSchema();
        JsonObject manifest = CreateManifest();
        JsonObject fact = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(manifest["facts"])[0]);

        switch (mutation)
        {
            case "typed-range":
                fact["value"] = new JsonObject
                {
                    ["kind"] = "range",
                    ["addressSpaceId"] = "flash",
                    ["range"] = new JsonObject { ["start"] = 0, ["length"] = 1 },
                };
                fact["disposition"] = "accepted";
                fact["promotionImpact"] = "none";
                break;
            case "unresolved-statement":
                break;
            case "accepted-statement":
                fact["disposition"] = "accepted";
                fact["promotionImpact"] = "none";
                break;
            case "unresolved-scalar":
                fact["value"] = new JsonObject { ["kind"] = "scalar", ["value"] = 1 };
                break;
            case "unresolved-reference":
                fact["value"] = new JsonObject { ["kind"] = "reference", ["targetId"] = "range-source" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown schema mutation.");
        }

        using var document = JsonDocument.Parse(manifest.ToJsonString());
        bool actualValid = schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
            RequireFormatValidation = true,
        }).IsValid;

        Assert.Equal(expectedValid, actualValid);
    }

    /// <summary>Verifies the Python intake emits a candidate evidence manifest accepted by the pinned schema.</summary>
    [Fact]
    public void CandidateIntakeEmitsEvidenceManifestAcceptedByPinnedSchema()
    {
        string root = Path.Combine(Path.GetTempPath(), "nfc-candidate-intake-" + Guid.NewGuid().ToString("N"));
        string sourceRoot = Path.Combine(root, "source");
        string outputRoot = Path.Combine(root, "candidate");
        _ = Directory.CreateDirectory(sourceRoot);

        try
        {
            byte[] sourceBytes = "candidate evidence\n"u8.ToArray();
            string sourceFileName = "flashmap.txt";
            string sourcePath = Path.Combine(sourceRoot, sourceFileName);
            File.WriteAllBytes(sourcePath, sourceBytes);
            string requestPath = Path.Combine(root, "request.json");
            File.WriteAllText(requestPath, CreateRequest(sourceFileName, sourceBytes));

            using Process process = Process.Start(CreateIntakeProcessStartInfo(requestPath, sourceRoot, outputRoot))
                ?? throw new InvalidOperationException("Could not start the candidate intake process.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"Candidate intake failed with exit code {process.ExitCode}.{Environment.NewLine}{standardOutput}{standardError}");

            using var output = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(outputRoot, "evidence-manifest.json")));
            Assert.True(
                LoadSchema().Evaluate(output.RootElement, new EvaluationOptions
                {
                    OutputFormat = OutputFormat.Flag,
                    RequireFormatValidation = true,
                }).IsValid);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static JsonSchema LoadSchema()
    {
        string path = RepositoryPaths.FromRepositoryRoot(
            "docs",
            "contracts",
            "firmware-evidence-manifest-v1.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ProfileBundleSchemaValidator.ParseSchema(path, SchemaId, document.RootElement);
    }

    private static JsonObject CreateManifest()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["manifestId"] = "candidate-evidence",
            ["manifestVersion"] = "0.1.0",
            ["status"] = "candidate",
            ["intakeProvenance"] = new JsonObject
            {
                ["toolId"] = "ic-reference-intake",
                ["toolVersion"] = "0.9.4",
                ["generatedAt"] = "2026-07-14T00:00:00Z",
                ["candidateOnly"] = true,
            },
            ["sourceArtifacts"] = new JsonArray(new JsonObject
            {
                ["artifactId"] = "flashmap-reference",
                ["sourceKind"] = "document",
                ["logicalName"] = "flashmap.txt",
                ["contentHash"] = new string('a', 64),
                ["sizeBytes"] = 1,
            }),
            ["facts"] = new JsonArray(new JsonObject
            {
                ["factId"] = "candidate-range",
                ["subject"] = new JsonObject
                {
                    ["familyId"] = "candidate-family",
                    ["memberId"] = "NT51951",
                    ["modeId"] = "ab-merge",
                },
                ["factKind"] = "range",
                ["value"] = new JsonObject
                {
                    ["kind"] = "statement",
                    ["text"] = "Map review is pending.",
                },
                ["disposition"] = "unresolved",
                ["promotionImpact"] = "blocks-map-resolution",
                ["citations"] = new JsonArray(new JsonObject
                {
                    ["artifactId"] = "flashmap-reference",
                    ["location"] = "Sheet 1",
                }),
            }),
            ["reviews"] = new JsonArray(),
        };
    }

    private static ProcessStartInfo CreateIntakeProcessStartInfo(string requestPath, string sourceRoot, string outputRoot)
    {
        string scriptPath = RepositoryPaths.FromRepositoryRoot("scripts", "intake_ic_reference.py");
        var startInfo = new ProcessStartInfo("python")
        {
            WorkingDirectory = RepositoryPaths.FromRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--request");
        startInfo.ArgumentList.Add(requestPath);
        startInfo.ArgumentList.Add("--source-root");
        startInfo.ArgumentList.Add(sourceRoot);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(outputRoot);
        return startInfo;
    }

    private static string CreateRequest(string sourceFileName, byte[] sourceBytes)
    {
        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["requestId"] = "candidate-request",
            ["manifestId"] = "candidate-evidence",
            ["manifestVersion"] = "0.1.0",
            ["requestedAtUtc"] = "2026-07-14T00:00:00Z",
            ["owner"] = "firmware-owner",
            ["workflow"] = "reference-only",
            ["candidateScope"] = new JsonObject
            {
                ["memberIds"] = new JsonArray("NT51951"),
                ["modeIds"] = new JsonArray("ab-merge"),
                ["capacityBytes"] = new JsonArray(524288),
                ["topologyChoices"] = new JsonArray("single"),
            },
            ["sourceArtifacts"] = new JsonArray(new JsonObject
            {
                ["artifactId"] = "flashmap-reference",
                ["sourceKind"] = "document",
                ["logicalName"] = sourceFileName,
                ["sourcePath"] = sourceFileName,
                ["contentHash"] = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant(),
                ["sizeBytes"] = sourceBytes.Length,
            }),
            ["facts"] = CreateManifest()["facts"]!.DeepClone(),
            ["reviews"] = new JsonArray(),
        }.ToJsonString();
    }
}
