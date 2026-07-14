using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI tests for candidate-only evidence staging.</summary>
public sealed class CandidateIntakeCliCommandTests
{
    /// <summary>Stages a declared candidate evidence set without admitting it to runtime composition.</summary>
    [Fact]
    public async Task CandidateIntakeStagePublishesCandidateOnlyEvidenceSet()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-intake-cli");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "Owner Record.txt"), sourceBytes);
        string requestPath = workspace.Write("request.json", CreateRequest(sourceBytes));
        string outputDirectory = workspace.PathFor("candidate-set");

        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "candidate-intake",
                "stage",
                "--request",
                requestPath,
                "--source-root",
                sourceRoot,
                "--output-dir",
                outputDirectory,
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Candidate evidence staged.", result.Output, StringComparison.Ordinal);
        Assert.Contains("Runtime authority: none", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(outputDirectory, "candidate-validation-report.json")));
        Assert.True(File.Exists(Path.Combine(
            outputDirectory,
            "candidate-root",
            "candidate-root-manifest.json")));
        Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Combine(
            outputDirectory,
            "candidate-root",
            "artifacts",
            "owner-record",
            "Owner Record.txt")));
    }

    /// <summary>Rejects incomplete command options before it attempts filesystem staging.</summary>
    [Fact]
    public async Task CandidateIntakeStageRejectsMissingSourceRoot()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["candidate-intake", "stage", "--request", "request.json", "--output-dir", "candidate-set"],
            TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("option '--source-root' is required", result.Error, StringComparison.Ordinal);
    }

    private static byte[] CreateRequest(byte[] sourceBytes)
    {
        string sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        string request = $$"""
            {
              "schemaVersion": "1.0",
              "documentKind": "intake-request",
              "requestId": "candidate-intake-test",
              "manifestId": "candidate-intake-manifest",
              "manifestVersion": "1.0.0",
              "requestedAtUtc": "2026-07-14T00:00:00Z",
              "owner": "test owner",
              "scope": {
                "memberIds": ["NT51950"],
                "modeIds": ["ab-merge"],
                "capacityBytes": [524288],
                "topologyChoices": ["single"]
              },
              "sourceArtifacts": [
                {
                  "artifactId": "owner-record",
                  "sourceKind": "owner-record",
                  "logicalName": "Owner Record.txt",
                  "sourcePath": "Owner Record.txt",
                  "contentHash": "{{sourceHash}}",
                  "sizeBytes": {{sourceBytes.Length}}
                }
              ],
              "facts": [
                {
                  "factId": "candidate-scope",
                  "factKind": "topology",
                  "value": { "kind": "statement", "text": "Candidate only." },
                  "disposition": "observed",
                  "promotionImpact": "blocks-support",
                  "citations": [{ "artifactId": "owner-record", "location": "line 1" }]
                }
              ],
              "missingEvidenceDisposition": "listed",
              "missingEvidence": [
                {
                  "gapId": "golden",
                  "evidenceKind": "golden",
                  "statement": "Expected output required.",
                  "promotionImpact": "blocks-support"
                }
              ]
            }
            """;
        return Encoding.UTF8.GetBytes(request);
    }
}
