using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Checks private evidence lookup with independent synthetic data.</summary>
public sealed class ConfidentialReferenceTestDataTests
{
    /// <summary>Private-held provenance stays readable as public identity metadata.</summary>
    [Fact]
    public void PrivateHeldCanonicalProvenanceRetainsPublicIntegrityIdentity()
    {
        JsonElement artifact = PrivateHeldArtifact();
        using var manifest = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "docs", "references", "confidential-references.json")));
        JsonElement entry = manifest.RootElement.GetProperty("entries").EnumerateArray().Single(
            static item => item.GetProperty("id").GetString() == "golden-provenance-nt51951-ctrlram-fw2.0.0");

        Assert.Equal(entry.GetProperty("sha256").GetString(), artifact.GetProperty("sha256").GetString());
        Assert.Equal(entry.GetProperty("sizeBytes").GetInt64(), artifact.GetProperty("size").GetInt64());
    }

    /// <summary>Private Golden bytes are checked only when their inventory is configured.</summary>
    [Fact]
    public void PrivateHeldCanonicalProvenanceMatchesPinnedBytes()
    {
        if (!ConfidentialReferenceTestData.IsConfigured)
        {
            Assert.Skip("confidential golden not executed");
        }

        JsonElement artifact = PrivateHeldArtifact();
        string path = CanonicalGoldenTestData.ArtifactPath(artifact);
        Assert.Equal(artifact.GetProperty("size").GetInt64(), new FileInfo(path).Length);
        Assert.Equal(artifact.GetProperty("sha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
    }

    /// <summary>Private storage cannot remove an input or expected output from execution.</summary>
    [Theory]
    [InlineData("input", "private-reference")]
    [InlineData("expected", "private-reference")]
    [InlineData("provenance", "unknown")]
    public void PrivateStorageRejectsUnapprovedRolesAndDispositions(string role, string storage)
    {
        using var artifact = JsonDocument.Parse(JsonSerializer.Serialize(new { role, storage }));
        _ = Assert.Throws<InvalidDataException>(() =>
            CanonicalGoldenTestData.IsPrivateReference(artifact.RootElement));
    }

    /// <summary>A matching inventory identity returns verified bytes.</summary>
    [Fact]
    public void MatchingInventoryDigestReturnsVerifiedBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-match");
        byte[] expected = Encoding.UTF8.GetBytes("synthetic reference");
        string digest = Convert.ToHexStringLower(SHA256.HashData(expected));
        _ = workspace.Write("nfc/references/synthetic/reference.dat", expected);
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  synthetic/reference.dat\n"));

        Assert.Equal(expected, ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }

    /// <summary>Changed private bytes fail the pinned digest check.</summary>
    [Fact]
    public void MismatchingInventoryDigestRejectsPrivateBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-drift");
        byte[] expected = Encoding.UTF8.GetBytes("synthetic reference");
        string digest = Convert.ToHexStringLower(SHA256.HashData(expected));
        _ = workspace.Write("nfc/references/synthetic/reference.dat", Encoding.UTF8.GetBytes("drifted"));
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  synthetic/reference.dat\n"));

        _ = Assert.Throws<InvalidDataException>(() =>
            ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }

    /// <summary>An inventory path cannot escape its reference root.</summary>
    [Fact]
    public void InventoryPathCannotEscapePrivateReferenceRoot()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-escape");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic")));
        _ = workspace.Write("nfc/references/SHA256SUMS",
            Encoding.UTF8.GetBytes($"{digest}  ../outside.dat\n"));

        _ = Assert.Throws<InvalidOperationException>(() =>
            ConfidentialReferenceTestData.ReadVerifiedBytes(digest, workspace.Root));
    }

    private static JsonElement PrivateHeldArtifact()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            CanonicalGoldenTestData.Root, "manifest.json")));
        foreach (JsonElement entry in inventory.RootElement.GetProperty("cases").EnumerateArray())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.PathFromRelative(
                CanonicalGoldenTestData.Root, entry.GetProperty("manifestPath").GetString()!)));
            if (!document.RootElement.TryGetProperty("artifacts", out JsonElement artifacts) ||
                !artifacts.EnumerateArray().Any(artifact => CanonicalGoldenTestData.IsPrivateReference(
                    artifact, document.RootElement.GetProperty("caseId").GetString())))
            {
                continue;
            }

            JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
                document.RootElement.GetProperty("workflow").GetString()!,
                document.RootElement.GetProperty("caseId").GetString()!);
            return goldenCase.GetProperty("artifacts").EnumerateArray()
                .Single(artifact => CanonicalGoldenTestData.IsPrivateReference(
                    artifact, document.RootElement.GetProperty("caseId").GetString())).Clone();
        }

        throw new InvalidDataException("Private-held canonical provenance was not declared.");
    }
}
