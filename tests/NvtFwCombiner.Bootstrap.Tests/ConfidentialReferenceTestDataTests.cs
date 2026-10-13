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
        (_, JsonElement artifact) = PrivateHeldArtifact();
        using var manifest = JsonDocument.Parse(GoldenFileIo.ReadAllText(RepositoryPaths.FromRepositoryRoot(
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

        (string caseId, JsonElement artifact) = PrivateHeldArtifact();
        string path = CanonicalGoldenTestData.ArtifactPath(artifact, caseId);
        Assert.Equal(artifact.GetProperty("size").GetInt64(), new FileInfo(path).Length);
        Assert.Equal(artifact.GetProperty("sha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(GoldenFileIo.ReadAllBytes(path))));
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

    /// <summary>A held reference is selected by its SHA-256 file name and returns verified bytes.</summary>
    [Fact]
    public void HeldDirectoryReturnsVerifiedBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-held-match");
        byte[] expected = Encoding.UTF8.GetBytes("synthetic held reference");
        string digest = Convert.ToHexStringLower(SHA256.HashData(expected));
        _ = workspace.Write($"docs/references/ci-held/{digest}", expected);

        Assert.Equal(expected, ConfidentialReferenceTestData.ReadVerifiedHeldBytes(
            digest.ToUpperInvariant(), Path.Combine(workspace.Root, "docs", "references", "ci-held")));
    }

    /// <summary>Changed held bytes fail the digest check, even when the file name matches.</summary>
    [Fact]
    public void HeldDirectoryRejectsDriftedBytes()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-held-drift");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic held reference")));
        _ = workspace.Write($"docs/references/ci-held/{digest}", Encoding.UTF8.GetBytes("drifted"));

        _ = Assert.Throws<InvalidDataException>(() => ConfidentialReferenceTestData.ReadVerifiedHeldBytes(
            digest, Path.Combine(workspace.Root, "docs", "references", "ci-held")));
    }

    /// <summary>A held file name that is not a SHA-256 cannot select a path.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("not-a-digest")]
    [InlineData("")]
    public void HeldDirectoryRejectsNonDigestNames(string name)
    {
        using var workspace = TempWorkspace.Create("confidential-reference-held-name");

        _ = Assert.ThrowsAny<ArgumentException>(() => ConfidentialReferenceTestData.ReadVerifiedHeldBytes(
            name, Path.Combine(workspace.Root, "docs", "references", "ci-held")));
    }

    /// <summary>A missing held file is an error, not an empty result.</summary>
    [Fact]
    public void HeldDirectoryMissingFileThrows()
    {
        using var workspace = TempWorkspace.Create("confidential-reference-held-missing");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("absent")));
        _ = workspace.Write("docs/references/ci-held/placeholder", Encoding.UTF8.GetBytes("other"));

        _ = Assert.ThrowsAny<IOException>(() => ConfidentialReferenceTestData.ReadVerifiedHeldBytes(
            digest, Path.Combine(workspace.Root, "docs", "references", "ci-held")));
    }

    private static (string CaseId, JsonElement Artifact) PrivateHeldArtifact()
    {
        using var inventory = JsonDocument.Parse(GoldenFileIo.ReadAllText(Path.Combine(
            CanonicalGoldenTestData.Root, "manifest.json")));
        foreach (JsonElement entry in inventory.RootElement.GetProperty("cases").EnumerateArray())
        {
            using var document = JsonDocument.Parse(GoldenFileIo.ReadAllText(RepositoryPaths.PathFromRelative(
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
            string caseId = document.RootElement.GetProperty("caseId").GetString()!;
            return (caseId, goldenCase.GetProperty("artifacts").EnumerateArray()
                .Single(artifact => CanonicalGoldenTestData.IsPrivateReference(artifact, caseId)).Clone());
        }

        throw new InvalidDataException("Private-held canonical provenance was not declared.");
    }
}
