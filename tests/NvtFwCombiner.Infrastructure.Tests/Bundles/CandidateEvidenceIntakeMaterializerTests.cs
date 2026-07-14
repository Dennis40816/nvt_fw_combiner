using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Tests materialization of candidate-only evidence from a fully declared local request.</summary>
public sealed class CandidateEvidenceIntakeMaterializerTests
{
    /// <summary>Publishes an exact closed root and its report sidecar without leaking the source path.</summary>
    [Fact]
    public void MaterializePublishesClosedCandidateSetFromDeclaredSnapshot()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        string sourcePath = Path.Combine(sourceRoot, "Owner Record.xlsx");
        File.WriteAllBytes(sourcePath, sourceBytes);
        string requestPath = workspace.Write("request.json", CreateRequest("Owner Record.xlsx", sourceBytes));
        string outputDirectory = workspace.PathFor("candidate-set");

        CandidateEvidenceMaterializationResult result = CandidateEvidenceIntakeMaterializer.Materialize(
            new CandidateEvidenceMaterializationRequest(
                requestPath,
                sourceRoot,
                outputDirectory,
                new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(Path.Combine(outputDirectory, "candidate-root"), result.CandidateRootDirectory);
        Assert.Equal(Path.Combine(outputDirectory, "candidate-validation-report.json"), result.ValidationReportPath);
        Assert.Equal(sourceBytes, File.ReadAllBytes(
            Path.Combine(result.CandidateRootDirectory, "artifacts", "owner-record", "Owner Record.xlsx")));

        using var sourceBundle = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(result.CandidateRootDirectory, "source", "candidate-source-bundle.json")));
        Assert.False(sourceBundle.RootElement.GetProperty("sourceArtifacts")[0].TryGetProperty("sourcePath", out _));

        using var rootManifest = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(result.CandidateRootDirectory, "candidate-root-manifest.json")));
        JsonElement entries = rootManifest.RootElement.GetProperty("entries");
        Assert.Equal(result.RootContentHash, rootManifest.RootElement.GetProperty("contentHash").GetString());
        Assert.Contains(entries.EnumerateArray(), static entry =>
            entry.GetProperty("path").GetString() == "artifacts/owner-record/Owner Record.xlsx");
        ClosedContentRootInventoryVerifier.VerifyClosedInventory(
            result.CandidateRootDirectory,
            "candidate-root-manifest.json",
            [.. entries.EnumerateArray().Select(static entry => entry.GetProperty("path").GetString()!)],
            64);

        using var report = JsonDocument.Parse(File.ReadAllBytes(result.ValidationReportPath));
        Assert.Equal(result.RootContentHash, report.RootElement.GetProperty("rootContentHash").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(Path.Combine(result.CandidateRootDirectory, "candidate-root-manifest.json"))),
            report.RootElement.GetProperty("rootManifestSha256").GetString());
        Assert.Equal("passed", report.RootElement.GetProperty("validationOutcome").GetString());
    }

    /// <summary>Rejects a changed declared artifact before publishing a candidate directory.</summary>
    [Fact]
    public void MaterializeRejectsSourceHashMismatchWithoutPublishing()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "owner-record.txt"), sourceBytes);
        JsonObject request = RequestNode("owner-record.txt", sourceBytes);
        JsonObject artifact = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(request["sourceArtifacts"])[0]);
        artifact["contentHash"] = new string('0', 64);
        string requestPath = workspace.Write("request.json", Encoding.UTF8.GetBytes(request.ToJsonString()));
        string outputDirectory = workspace.PathFor("candidate-set");

        _ = Assert.Throws<InvalidDataException>(() => CandidateEvidenceIntakeMaterializer.Materialize(
            new CandidateEvidenceMaterializationRequest(
                requestPath,
                sourceRoot,
                outputDirectory,
                DateTimeOffset.UnixEpoch)));

        Assert.False(Directory.Exists(outputDirectory));
        Assert.Empty(Directory.EnumerateDirectories(workspace.Root, ".candidate-set-*"));
    }

    /// <summary>Rejects a fact citation that names an artifact outside the declared snapshot set.</summary>
    [Fact]
    public void MaterializeRejectsUndeclaredFactCitationWithoutPublishing()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "owner-record.txt"), sourceBytes);
        JsonObject request = RequestNode("owner-record.txt", sourceBytes);
        JsonObject citation = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(request["facts"])[0])["citations"])[0]);
        citation["artifactId"] = "not-declared";
        string requestPath = workspace.Write("request.json", Encoding.UTF8.GetBytes(request.ToJsonString()));
        string outputDirectory = workspace.PathFor("candidate-set");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CandidateEvidenceIntakeMaterializer.Materialize(
                new CandidateEvidenceMaterializationRequest(
                    requestPath,
                    sourceRoot,
                    outputDirectory,
                    DateTimeOffset.UnixEpoch)));

        Assert.Contains("undeclared artifact", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputDirectory));
    }

    /// <summary>Rejects an existing destination without replacing or altering caller-owned content.</summary>
    [Fact]
    public void MaterializeRejectsExistingOutputDirectoryWithoutOverwrite()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "owner-record.txt"), sourceBytes);
        string requestPath = workspace.Write("request.json", CreateRequest("owner-record.txt", sourceBytes));
        string outputDirectory = workspace.PathFor("candidate-set");
        _ = Directory.CreateDirectory(outputDirectory);
        string sentinelPath = Path.Combine(outputDirectory, "sentinel.txt");
        File.WriteAllText(sentinelPath, "preserve");

        _ = Assert.Throws<IOException>(() => CandidateEvidenceIntakeMaterializer.Materialize(
            new CandidateEvidenceMaterializationRequest(
                requestPath,
                sourceRoot,
                outputDirectory,
                DateTimeOffset.UnixEpoch)));

        Assert.Equal("preserve", File.ReadAllText(sentinelPath));
        Assert.False(Directory.Exists(Path.Combine(outputDirectory, "candidate-root")));
    }

    /// <summary>Rejects an output path nested below the declared source root.</summary>
    [Fact]
    public void MaterializeRejectsOutputDirectoryInsideSourceRoot()
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "owner-record.txt"), sourceBytes);
        string requestPath = workspace.Write("request.json", CreateRequest("owner-record.txt", sourceBytes));
        string outputDirectory = Path.Combine(sourceRoot, "candidate-set");

        _ = Assert.Throws<ArgumentException>(() => CandidateEvidenceIntakeMaterializer.Materialize(
            new CandidateEvidenceMaterializationRequest(
                requestPath,
                sourceRoot,
                outputDirectory,
                DateTimeOffset.UnixEpoch)));

        Assert.False(Directory.Exists(outputDirectory));
    }

    /// <summary>Rejects Office and tool lock files before copying a candidate artifact.</summary>
    [Theory]
    [InlineData("~$Owner Record.xlsx")]
    [InlineData("owner-record.lock")]
    public void MaterializeRejectsLockFileWithoutPublishing(string fileName)
    {
        using var workspace = TempWorkspace.Create("nfc-candidate-materializer");
        byte[] sourceBytes = "owner evidence\n"u8.ToArray();
        string sourceRoot = workspace.PathFor("source");
        _ = Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, fileName), sourceBytes);
        string requestPath = workspace.Write("request.json", CreateRequest(fileName, sourceBytes));
        string outputDirectory = workspace.PathFor("candidate-set");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CandidateEvidenceIntakeMaterializer.Materialize(
                new CandidateEvidenceMaterializationRequest(
                    requestPath,
                    sourceRoot,
                    outputDirectory,
                    DateTimeOffset.UnixEpoch)));

        Assert.Contains("lock file", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputDirectory));
    }

    private static byte[] CreateRequest(string fileName, byte[] sourceBytes)
    {
        return Encoding.UTF8.GetBytes(RequestNode(fileName, sourceBytes).ToJsonString());
    }

    private static JsonObject RequestNode(string fileName, byte[] sourceBytes)
    {
        JsonObject request = CandidateEvidenceV1SchemaTests.CreateDocument("intake-request");
        JsonObject artifact = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(request["sourceArtifacts"])[0]);
        artifact["logicalName"] = fileName;
        artifact["sourcePath"] = fileName;
        artifact["contentHash"] = Hash(sourceBytes);
        artifact["sizeBytes"] = sourceBytes.Length;
        return request;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
