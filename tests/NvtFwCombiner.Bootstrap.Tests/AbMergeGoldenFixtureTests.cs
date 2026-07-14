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

    /// <summary>Keeps candidate-only AB evidence and shared input provenance from being silently weakened.</summary>
    [Fact]
    public void ManifestRetainsCandidateEvidenceGatesAndSharedInputIdentity()
    {
        using JsonDocument manifest = OpenManifest();
        JsonElement[] goldenCases = [.. manifest.RootElement.GetProperty("cases").EnumerateArray()];
        var expectedVerificationKinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nt51929-ab-512k-20260611"] = "direct-v2-exact",
            ["nt51950-ab-512k-boe-20260616"] = "v2-pre-combiner-exact-outside-declared-writes",
            ["nt51950-ab-512k-hiway-20260616"] = "v2-pre-combiner-exact-outside-declared-writes",
        };
        var expectedSupportStatuses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nt51929-ab-512k-20260611"] = "candidate-only; firmware-owner review and the NT51919/NT51932 member evidence remain separate gates.",
            ["nt51950-ab-512k-boe-20260616"] = "candidate-only; C# does not calculate header CRC and product execution remains blocked until the declared Combiner evidence is supplied.",
            ["nt51950-ab-512k-hiway-20260616"] = "candidate-only; C# does not calculate header CRC and product execution remains blocked until the declared Combiner evidence is supplied.",
        };
        var expectedSharedInputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nt51929-ab-512k-20260611:tp-b-input"] = "nt51929-ab-512k-20260611:tp-a-input",
            ["nt51950-ab-512k-boe-20260616:tp-b-input"] = "nt51950-ab-512k-boe-20260616:tp-a-input",
            ["nt51950-ab-512k-hiway-20260616:tp-a-input"] = "nt51950-ab-512k-boe-20260616:tp-a-input",
            ["nt51950-ab-512k-hiway-20260616:tp-b-input"] = "nt51950-ab-512k-hiway-20260616:tp-a-input",
        };
        var inputsByReference = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var sharedInputsByReference = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (JsonElement goldenCase in goldenCases)
        {
            string caseId = goldenCase.GetProperty("caseId").GetString()!;
            Assert.Equal("owner-approved-golden-firmware", goldenCase.GetProperty("sourceClassification").GetString());
            Assert.Equal(expectedSupportStatuses[caseId], goldenCase.GetProperty("supportStatus").GetString());

            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                Assert.True(
                    inputsByReference.TryAdd($"{caseId}:{input.Name}", input.Value),
                    $"AB fixture input '{caseId}:{input.Name}' is declared more than once.");

                if (input.Value.TryGetProperty("sharedWith", out JsonElement sharedWith))
                {
                    Assert.True(
                        sharedInputsByReference.TryAdd(
                            $"{caseId}:{input.Name}",
                            NormalizeSharedInputReference(caseId, sharedWith.GetString()!)),
                        $"AB fixture input '{caseId}:{input.Name}' declares shared provenance more than once.");
                }
            }
        }

        Assert.Equal(
            expectedVerificationKinds.Keys.Order(StringComparer.Ordinal),
            goldenCases.Select(static goldenCase => goldenCase.GetProperty("caseId").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedSupportStatuses.Keys.Order(StringComparer.Ordinal),
            goldenCases.Select(static goldenCase => goldenCase.GetProperty("caseId").GetString()).Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedSharedInputs.Keys.Order(StringComparer.Ordinal),
            sharedInputsByReference.Keys.Order(StringComparer.Ordinal));

        foreach (JsonElement goldenCase in goldenCases)
        {
            string caseId = goldenCase.GetProperty("caseId").GetString()!;
            foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
            {
                string inputReference = $"{caseId}:{input.Name}";
                if (!sharedInputsByReference.TryGetValue(inputReference, out string? sharedReference))
                {
                    continue;
                }

                Assert.Equal(expectedSharedInputs[inputReference], sharedReference);
                Assert.True(
                    inputsByReference.TryGetValue(sharedReference, out JsonElement sharedInput),
                    $"AB fixture input '{caseId}:{input.Name}' references missing shared input '{sharedReference}'.");
                AssertSameArtifactIdentity(sharedInput, input.Value);
            }

            string verificationKind = goldenCase.GetProperty("verification").GetProperty("kind").GetString()!;
            Assert.Equal(expectedVerificationKinds[caseId], verificationKind);
            switch (verificationKind)
            {
                case "direct-v2-exact":
                    Assert.False(goldenCase.TryGetProperty("missingProcessorEvidence", out _));
                    Assert.False(goldenCase.GetProperty("verification").TryGetProperty("requiredCombinerWriteRanges", out _));
                    break;
                case "v2-pre-combiner-exact-outside-declared-writes":
                    Assert.Equal(
                        ["0x4A100-0x4A104", "0x4A110-0x4A114", "0x4A120-0x4A124", "0x4A130-0x4A134"],
                        goldenCase.GetProperty("verification").GetProperty("requiredCombinerWriteRanges")
                            .EnumerateArray()
                            .Select(static range => range.GetString()));
                    Assert.Equal(
                        ["exact map.txt", "replayable Combiner command trace", "Combiner tool manifest for this case"],
                        goldenCase.GetProperty("missingProcessorEvidence")
                            .EnumerateArray()
                            .Select(static evidence => evidence.GetString()));
                    break;
                default:
                    Assert.Fail($"AB fixture '{caseId}' declares unsupported verification kind '{verificationKind}'.");
                    break;
            }
        }
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

    private static string NormalizeSharedInputReference(string caseId, string sharedWith)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedWith);

        int separator = sharedWith.IndexOf(':');
        if (separator < 0)
        {
            return $"{caseId}:{sharedWith}";
        }

        Assert.Equal(separator, sharedWith.LastIndexOf(':'));
        Assert.InRange(separator, 1, sharedWith.Length - 2);
        return sharedWith;
    }

    private static void AssertSameArtifactIdentity(JsonElement expected, JsonElement actual)
    {
        foreach (string propertyName in new[] { "path", "originalFileName", "size", "sha256" })
        {
            Assert.Equal(
                expected.GetProperty(propertyName).GetRawText(),
                actual.GetProperty(propertyName).GetRawText());
        }
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
