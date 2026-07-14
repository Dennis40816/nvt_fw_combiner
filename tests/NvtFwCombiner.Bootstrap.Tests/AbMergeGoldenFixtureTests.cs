using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Golden evidence for the owner-approved NT51929 and NT51950 AB fixture package.</summary>
public sealed class AbMergeGoldenFixtureTests
{
    private const string Nt51929BundleDirectory = "nt51919-nt51929-nt51932-ab-merge";
    private const string Nt51929BundleContentHash = "b5035b9c4afa8691adb98632b4ce9a1088d74d04948ea1f20690aade889445fb";
    private const string Nt51950BundleDirectory = "nt51950-ab-merge";
    private const string Nt51950BundleContentHash = "d12b1a686a9c45b901cd5888f71e456b8e3f50fefe4d09da89dad20bfc86e357";

    /// <summary>Verifies the manifest accounts for every tracked AB golden payload with its declared hash.</summary>
    [Fact]
    public void ManifestAccountsForEveryTrackedFixtureAndOwnerApproval()
    {
        using JsonDocument manifest = OpenManifest();
        JsonElement root = manifest.RootElement;
        Assert.Equal("owner-approved-golden-firmware", root.GetProperty("payloadClass").GetString());
        Assert.True(root.GetProperty("binaryPayloadsIncluded").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("source").GetProperty("approval").GetString()));

        var manifestPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement goldenCase in root.GetProperty("cases").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(goldenCase.GetProperty("ownerApproval").GetString()));
            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                AssertManifestFile(input.Value, manifestPaths);
            }

            AssertManifestFile(goldenCase.GetProperty("expectedOutput"), manifestPaths);
        }

        string fixturesRoot = Path.Combine(GoldenRoot, "fixtures");
        string[] trackedFiles = [.. Directory.EnumerateFiles(fixturesRoot, "*.bin", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(GoldenRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];
        Assert.Equal(trackedFiles, manifestPaths.Order(StringComparer.Ordinal));
    }

    /// <summary>Verifies the NT51929 candidate plan reproduces the complete owner-approved AB output without a processor stage.</summary>
    [Fact]
    public void Nt51929CandidatePlanMatchesOwnerApprovedGoldenBytes()
    {
        JsonElement goldenCase = ReadCase("nt51929-ab-512k-20260611");
        using var workspace = TempWorkspace.Create("nfc-nt51929-ab-golden");
        CompiledComposition composition = CompileCandidate(
            workspace,
            Nt51929BundleDirectory,
            Nt51929BundleContentHash,
            goldenCase);
        byte[] expected = ReadManifestFile(goldenCase.GetProperty("expectedOutput"));

        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(ReadInputs(goldenCase.GetProperty("inputs"))));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal(goldenCase.GetProperty("expectedOutput").GetProperty("sha256").GetString(), Hash(result.OutputBytes.Span));
    }

    /// <summary>
    /// Verifies the NT51950 V2 candidate produces the exact owner golden image outside the four header fields owned by Combiner.
    /// The test deliberately passes through the external stage: C# must never calculate or write those header bytes.
    /// </summary>
    [Theory]
    [InlineData("nt51950-ab-512k-boe-20260616")]
    [InlineData("nt51950-ab-512k-hiway-20260616")]
    public async Task Nt51950CandidatePlanLeavesOnlyCombinerOwnedGoldenDifferencesAsync(string caseId)
    {
        JsonElement goldenCase = ReadCase(caseId);
        using var workspace = TempWorkspace.Create($"nfc-{caseId}");
        CompiledComposition composition = CompileCandidate(
            workspace,
            Nt51950BundleDirectory,
            Nt51950BundleContentHash,
            goldenCase);
        byte[] expected = ReadManifestFile(goldenCase.GetProperty("expectedOutput"));
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            composition.Plan.OrderedOperations[^1].ExternalProcessorInvocation);
        Assert.Equal(ReadRequiredCombinerWriteRanges(goldenCase), invocation.AllowedWriteRanges);

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(ReadInputs(goldenCase.GetProperty("inputs"))),
            static (_, inputBytes, _, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        IReadOnlyList<ByteRange> changedRanges = ByteDiff.FindChangedRanges(result.OutputBytes.Span, expected);
        Assert.NotEmpty(changedRanges);
        ChangedRangeVerdict verdict = new ChangedRangePolicy(invocation.AllowedWriteRanges).Evaluate(changedRanges);
        Assert.True(
            verdict.IsAllowed,
            $"Pre-Combiner output differs outside declared Combiner write authority: {string.Join(", ", verdict.ViolatingRanges)}");
    }

    private static string GoldenRoot => Path.Combine(
        RepositoryPaths.FindRepositoryRoot(),
        "testdata",
        "golden",
        "ab-merge");

    private static JsonDocument OpenManifest()
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenRoot, "manifest.json")));
    }

    private static JsonElement ReadCase(string caseId)
    {
        using JsonDocument manifest = OpenManifest();
        return manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(item.GetProperty("caseId").GetString(), caseId))
            .Clone();
    }

    private static CompiledComposition CompileCandidate(
        TempWorkspace workspace,
        string bundleDirectory,
        string bundleContentHash,
        JsonElement goldenCase)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            AbMergeCandidateBundleTestSupport.LoadCatalog(workspace, bundleDirectory, bundleContentHash),
            goldenCase.GetProperty("profileId").GetString()!,
            goldenCase.GetProperty("profileVersion").GetString()!,
            goldenCase.GetProperty("ic").GetString()!,
            ExperienceIds.AbMerge,
            goldenCase.GetProperty("capacityBytes").GetInt64());
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static Dictionary<string, byte[]> ReadInputs(JsonElement inputs)
    {
        return inputs.EnumerateObject().ToDictionary(
            static input => input.Name,
            input => ReadManifestFile(input.Value),
            StringComparer.Ordinal);
    }

    private static byte[] ReadManifestFile(JsonElement manifestFile)
    {
        string path = RepositoryPaths.ManifestPath(GoldenRoot, manifestFile);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(manifestFile.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(manifestFile.GetProperty("sha256").GetString(), Hash(bytes));
        return bytes;
    }

    private static void AssertManifestFile(JsonElement manifestFile, HashSet<string> manifestPaths)
    {
        string relativePath = manifestFile.GetProperty("path").GetString()!;
        bool isSharedInput = manifestFile.TryGetProperty("sharedWith", out _);
        if (isSharedInput)
        {
            Assert.Contains(relativePath, manifestPaths);
        }
        else
        {
            Assert.True(manifestPaths.Add(relativePath), $"Fixture path '{relativePath}' is listed more than once outside an explicit shared input.");
        }

        Assert.False(string.IsNullOrWhiteSpace(manifestFile.GetProperty("originalFileName").GetString()));
        _ = ReadManifestFile(manifestFile);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static ByteRange[] ReadRequiredCombinerWriteRanges(JsonElement goldenCase)
    {
        return [.. goldenCase.GetProperty("verification").GetProperty("requiredCombinerWriteRanges")
            .EnumerateArray()
            .Select(static range => ParseHalfOpenRange(range.GetString()!))];
    }

    private static ByteRange ParseHalfOpenRange(string value)
    {
        string[] bounds = value.Split('-', StringSplitOptions.TrimEntries);
        Assert.Equal(2, bounds.Length);
        return ByteRange.FromStartEndExclusive(ParseHex(bounds[0]), ParseHex(bounds[1]));
    }

    private static long ParseHex(string value)
    {
        Assert.StartsWith("0x", value, StringComparison.OrdinalIgnoreCase);
        return long.Parse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
