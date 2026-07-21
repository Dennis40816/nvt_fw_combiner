using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared trusted-V2 Standard Merge migration evidence helpers.</summary>
internal static class V2StandardMergeGoldenTestSupport
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 7, 12, 0, 0, 1, TimeSpan.Zero);

    internal static TrustedProfileBundleCatalog LoadDeployedCatalog(string bundleDirectory, string bundleContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);
        string bundleRoot = Path.Combine(AppContext.BaseDirectory, "profiles", "built-in", bundleDirectory);
        return LoadCatalogRoot(bundleRoot, bundleContentHash);
    }

    internal static void AssertSourceCatalogIsRejected(string bundleDirectory, string bundleContentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleContentHash);
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string bundleRoot = Path.Combine(repositoryRoot, "profiles", "built-in", bundleDirectory);
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => LoadBundle(bundleRoot, bundleContentHash));
        Assert.Contains("schemas/", exception.Message, StringComparison.Ordinal);
    }

    private static TrustedProfileBundleCatalog LoadCatalogRoot(string bundleRoot, string bundleContentHash)
    {
        AssertSchemaSnapshotsMatchManifest(bundleRoot);
        TrustedProfileBundle bundle = LoadBundle(bundleRoot, bundleContentHash);
        return TrustedProfileBundleCatalogProjection.Create(bundle.CreateDocumentProjection());
    }

    private static TrustedProfileBundle LoadBundle(string bundleRoot, string bundleContentHash)
    {
        return ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(bundleContentHash, "built-in-profile-bundle-v2"),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8)));
    }

    internal static CompiledComposition CompileV2(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string icId,
        long? requestedMapCapacity = null)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            catalog,
            profileId,
            profileVersion,
            icId,
            ExperienceIds.StandardMerge,
            requestedMapCapacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        return composition;
    }

    internal static JsonElement ReadGoldenCase(string referenceIc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceIc);
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        return manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(item.GetProperty("ic").GetString(), referenceIc))
            .Clone();
    }

    internal static Dictionary<string, byte[]> ReadInputs(JsonElement inputs)
    {
        return inputs.EnumerateObject().ToDictionary(
            static input => input.Name,
            input => ReadManifestFile(input.Value),
            StringComparer.Ordinal);
    }

    internal static byte[] ReadManifestFile(JsonElement manifestFile)
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.ManifestPath(GoldenRoot, manifestFile));
        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Hash(bytes));
        return bytes;
    }

    internal static async ValueTask<CompositionRunResult> PreviewAsync(
        CompiledComposition compiledComposition,
        IReadOnlyDictionary<string, byte[]> inputs)
    {
        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{compiledComposition.ProfileId}:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings =
        [
            .. inputs.Keys.Order(StringComparer.Ordinal)
                .Select(addressSpaceId => CreateInputBinding(compiledComposition.ProfileId, addressSpaceId)),
        ];
        var service = new CompositionRunService(reader, new FakeClock([StartedAtUtc, CompletedAtUtc]));
        var request = new CompositionRunRequest(
            $"golden-{compiledComposition.IcId.ToLowerInvariant()}",
            compiledComposition,
            bindings,
            compiledComposition.DefaultOutputFileName);
        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    internal static void AssertSuccessfulGoldenOutput(
        CompositionRunResult result,
        CompiledComposition compiledComposition,
        byte[] expectedOutput)
    {
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Report.Issues);
        Assert.Equal(expectedOutput, result.OutputBytes.ToArray());
        Assert.Equal(Hash(expectedOutput), result.Report.Output.Sha256);
        Assert.Equal(compiledComposition.CompilationFingerprint, result.Report.CompilationFingerprint);
    }

    internal static string GoldenRoot => CanonicalGoldenTestData.Root;

    private static void AssertSchemaSnapshotsMatchManifest(string bundleRoot)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "profile-bundle.json")));
        foreach (JsonElement entry in manifest.RootElement.GetProperty("entries").EnumerateArray().Where(static entry =>
                     StringComparer.Ordinal.Equals(entry.GetProperty("kind").GetString(), "schema")))
        {
            string schemaFileName = Path.GetFileName(entry.GetProperty("path").GetString()!);
            string contentHash = entry.GetProperty("contentHash").GetString()!;
            Assert.Equal(
                File.ReadAllBytes(ResolveContractSchemaPath(schemaFileName, contentHash)),
                File.ReadAllBytes(Path.Combine(bundleRoot, "schemas", schemaFileName)));
        }
    }

    internal static string ResolveContractSchemaPath(string schemaPath, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        string contractRoot = RepositoryPaths.FromRepositoryRoot("docs", "contracts");
        string[] matches =
        [
            .. Directory.EnumerateFiles(contractRoot, "*.schema.json", SearchOption.TopDirectoryOnly)
                .Where(path => StringComparer.Ordinal.Equals(Hash(File.ReadAllBytes(path)), contentHash)),
        ];
        return Assert.Single(matches);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }

    private static InputArtifactBinding CreateInputBinding(string profileId, string addressSpaceId)
    {
        (string originalFileName, CompiledInputArtifactClass artifactClass) = addressSpaceId switch
        {
            "dp-input" => ("dp.bin", CompiledInputArtifactClass.DpFirmware),
            "tp-input" => ("tp.bin", CompiledInputArtifactClass.TpFirmware),
            "ld-input" => ("ld.bin", CompiledInputArtifactClass.Auxiliary),
            _ => throw new ArgumentOutOfRangeException(nameof(addressSpaceId), addressSpaceId, "Unsupported Standard Merge input space."),
        };
        return new InputArtifactBinding(
            addressSpaceId,
            addressSpaceId,
            $"{profileId}:{addressSpaceId}",
            originalFileName,
            artifactClass);
    }
}
