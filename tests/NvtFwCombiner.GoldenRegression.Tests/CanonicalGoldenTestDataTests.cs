using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Contract tests for canonical direct-input evidence and fact-scoped aliases.</summary>
public sealed class CanonicalGoldenTestDataTests
{
    private static readonly string[] LegacyPaths = ["testdata/golden/ctrlram-replace/fixtures/base.bin"];
    private static readonly string[] AliasFactScope = ["exact catalog and process facts"];
    private static readonly string[] AliasEvidenceRefs = ["focused test"];

    /// <summary>Direct input evidence resolves aliases but never enters executable golden projections.</summary>
    [Fact]
    public void DirectInputEvidenceResolvesAliasWithoutBecomingExecutableGolden()
    {
        using var workspace = TempWorkspace.Create("canonical-direct-evidence");
        string root = workspace.PathFor("canonical");
        const string sourceCaseId = "nt51927-ctrlram-fw132-twochip-evidence";
        const string aliasCaseId = "nt51917-ctrlram-fw132-twochip-alias";
        string sourceManifestPath = CaseManifestPath("NT51927", sourceCaseId);
        string aliasManifestPath = CaseManifestPath("NT51917", aliasCaseId);
        string inputRelativePath = string.Join(
            '/',
            "NT51927",
            "ctrlram-replace",
            "1.3.2",
            "cascade-2",
            sourceCaseId,
            "inputs",
            "base.bin");
        byte[] inputBytes = Encoding.UTF8.GetBytes("owner-approved input evidence");
        string inputPath = Path.Combine(root, RepositoryPaths.NormalizeRelativePath(inputRelativePath));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
        File.WriteAllBytes(inputPath, inputBytes);
        WriteJson(
            Path.Combine(root, sourceManifestPath),
            new
            {
                schemaVersion = "1.0",
                caseId = sourceCaseId,
                ic = "NT51927",
                workflow = "ctrlram-replace",
                variantOrVersion = "1.3.2",
                topology = "cascade-2",
                directGolden = false,
                directEvidence = true,
                sourceClassification = "owner-approved-input-evidence",
                ownerApproval = "focused test",
                artifacts = new[]
                {
                    new
                    {
                        artifactId = "base-input",
                        role = "input",
                        path = inputRelativePath,
                        size = inputBytes.Length,
                        sha256 = Convert.ToHexStringLower(SHA256.HashData(inputBytes)),
                        legacyPaths = LegacyPaths,
                    },
                },
            });
        WriteJson(
            Path.Combine(root, aliasManifestPath),
            new
            {
                schemaVersion = "1.0",
                caseId = aliasCaseId,
                ic = "NT51917",
                workflow = "ctrlram-replace",
                variantOrVersion = "1.3.2",
                topology = "cascade-2",
                directGolden = false,
                sourceClassification = "owner-approved-fact-alias",
                ownerApproval = "focused test",
                alias = new
                {
                    sourceCaseId,
                    factScope = AliasFactScope,
                    evidenceRefs = AliasEvidenceRefs,
                },
            });
        WriteJson(
            Path.Combine(root, "manifest.json"),
            new
            {
                schemaVersion = "1.0",
                payloadClass = "owner-approved-golden",
                binaryPayloadsIncluded = true,
                diagnosticsRoot = "testdata/diagnostics/golden-evidence",
                cases = new[]
                {
                    new { caseId = sourceCaseId, manifestPath = sourceManifestPath },
                    new { caseId = aliasCaseId, manifestPath = aliasManifestPath },
                },
            });

        CanonicalGoldenAlias alias = Assert.Single(
            CanonicalGoldenTestData.LoadWorkflowAliases("ctrlram-replace", root));

        Assert.Equal(aliasCaseId, alias.CaseId);
        Assert.Equal("NT51917", alias.Ic);
        Assert.Equal(sourceCaseId, alias.SourceCaseId);
        Assert.Equal("NT51927", alias.SourceIc);
        using JsonDocument directGoldens = CanonicalGoldenTestData.LoadDirectWorkflowManifest(
            "ctrlram-replace",
            root);
        Assert.Empty(directGoldens.RootElement.GetProperty("cases").EnumerateArray());
        JsonElement directEvidence = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            sourceCaseId,
            root);
        Assert.Equal(sourceCaseId, directEvidence.GetProperty("caseId").GetString());
        _ = Assert.Throws<InvalidDataException>(
            () => CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", sourceCaseId, root));
    }

    /// <summary>Explicit roots validate and project their own direct-golden payloads.</summary>
    [Fact]
    public void ExplicitRootProjectsAndValidatesDirectGoldenArtifacts()
    {
        using var workspace = TempWorkspace.Create("canonical-explicit-root-golden");
        string root = workspace.PathFor("canonical");
        const string caseId = "nt51923-explicit-root-direct-golden";
        string caseDirectory = string.Join(
            '/',
            "NT51923",
            "standard-merge",
            "test",
            "topology-unscoped",
            caseId);
        string manifestPath = $"{caseDirectory}/provenance/case.json";
        string inputRelativePath = $"{caseDirectory}/inputs/dp.bin";
        string expectedRelativePath = $"{caseDirectory}/expected/flash.bin";
        byte[] inputBytes = Encoding.UTF8.GetBytes("temporary direct input");
        byte[] expectedBytes = Encoding.UTF8.GetBytes("temporary expected output");
        WriteBytes(root, inputRelativePath, inputBytes);
        WriteBytes(root, expectedRelativePath, expectedBytes);
        WriteJson(
            Path.Combine(root, RepositoryPaths.NormalizeRelativePath(manifestPath)),
            new
            {
                schemaVersion = "1.0",
                caseId,
                ic = "NT51923",
                workflow = "standard-merge",
                variantOrVersion = "test",
                topology = "topology-unscoped",
                profileId = "nt51923-explicit-root-test",
                directGolden = true,
                sourceClassification = "owner-approved",
                ownerApproval = "focused test",
                artifacts = new[]
                {
                    Artifact("dp-input", "input", inputRelativePath, inputBytes),
                    Artifact("expected-output", "expected", expectedRelativePath, expectedBytes),
                },
            });
        WriteJson(
            Path.Combine(root, "manifest.json"),
            new
            {
                cases = new[] { new { caseId, manifestPath } },
            });

        using JsonDocument projection = CanonicalGoldenTestData.LoadDirectWorkflowManifest(
            "standard-merge",
            root);
        JsonElement goldenCase = Assert.Single(
            projection.RootElement.GetProperty("cases").EnumerateArray());
        JsonElement rawCase = CanonicalGoldenTestData.LoadDirectCase(
            "standard-merge",
            caseId,
            root);

        Assert.Equal("51923", goldenCase.GetProperty("ic").GetString());
        Assert.Equal(inputBytes.Length, goldenCase.GetProperty("inputs").GetProperty("dp-input").GetProperty("size").GetInt64());
        Assert.Equal(expectedBytes.Length, goldenCase.GetProperty("expectedOutput").GetProperty("size").GetInt64());
        Assert.Equal(caseId, rawCase.GetProperty("caseId").GetString());
    }

    private static string CaseManifestPath(string ic, string caseId)
    {
        return string.Join(
            '/',
            ic,
            "ctrlram-replace",
            "1.3.2",
            "cascade-2",
            caseId,
            "provenance",
            "case.json");
    }

    private static void WriteJson(string path, object value)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value));
    }

    private static void WriteBytes(string root, string relativePath, byte[] bytes)
    {
        string path = Path.Combine(root, RepositoryPaths.NormalizeRelativePath(relativePath));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static object Artifact(string artifactId, string role, string path, byte[] bytes)
    {
        return new
        {
            artifactId,
            role,
            path,
            size = bytes.Length,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            legacyPaths = LegacyPaths,
        };
    }
}
