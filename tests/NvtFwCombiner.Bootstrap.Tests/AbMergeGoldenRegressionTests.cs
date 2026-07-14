using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved AB fixture evidence for V2 candidate profiles.</summary>
public sealed class AbMergeGoldenRegressionTests
{
    private const string Nt51929BundleDirectory = "nt51919-nt51929-nt51932-ab-merge";
    private const string Nt51929BundleContentHash = "b5035b9c4afa8691adb98632b4ce9a1088d74d04948ea1f20690aade889445fb";
    private const string Nt51950BundleDirectory = "nt51950-ab-merge";
    private const string Nt51950BundleContentHash = "d12b1a686a9c45b901cd5888f71e456b8e3f50fefe4d09da89dad20bfc86e357";

    /// <summary>Verifies the NT51929 V2 candidate reproduces the supplied AB output byte-for-byte.</summary>
    [Fact]
    public void Nt51929CandidateMatchesOwnerApprovedAbGolden()
    {
        JsonElement goldenCase = ReadGoldenCase("nt51929-ab-t05-d06");
        CompiledComposition composition = CompileCandidate(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(Nt51929BundleDirectory, Nt51929BundleContentHash),
            goldenCase,
            "NT51929");

        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expected = ReadFixture(goldenCase.GetProperty("expectedOutput"));
        byte[] originalTpB = [.. inputs["tp-b-input"]];
        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(inputs));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal("c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2", Hash(result.OutputBytes.Span));
        Assert.Equal(originalTpB, inputs["tp-b-input"]);
    }

    /// <summary>Verifies NT51950 staging changes no golden byte outside the declared Combiner authority.</summary>
    [Theory]
    [InlineData("nt51950-ab-boe-d82t80")]
    [InlineData("nt51950-ab-hiway-d82t80")]
    public async Task Nt51950CandidateLeavesGoldenDeltaToDeclaredCombinerWritesAsync(string caseId)
    {
        JsonElement goldenCase = ReadGoldenCase(caseId);
        using var workspace = TempWorkspace.Create($"nfc-{caseId}");
        CompiledComposition composition = CompileCandidate(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
                workspace,
                Nt51950BundleDirectory,
                Nt51950BundleContentHash),
            goldenCase,
            "NT51950");
        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase.GetProperty("inputs"));
        byte[] expected = ReadFixture(goldenCase.GetProperty("expectedOutput"));
        byte[] originalTpB = [.. inputs["tp-b-input"]];

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            static (_, inputBytes, _, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes)),
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(originalTpB, inputs["tp-b-input"]);

        List<ByteRange> observed = FindChangedRanges(result.OutputBytes.Span, expected);
        Assert.Equal(
            [new ByteRange(0x4A102, 1), new ByteRange(0x4A112, 1), new ByteRange(0x4A122, 1), new ByteRange(0x4A130, 4)],
            observed);
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
            composition.Plan.OrderedOperations[^1].ExternalProcessorInvocation);
        ChangedRangeVerdict verdict = new ChangedRangePolicy(invocation.AllowedWriteRanges).Evaluate(observed);
        Assert.True(verdict.IsAllowed);
        Assert.Empty(verdict.ViolatingRanges);
    }

    private static CompiledComposition CompileCandidate(
        TrustedProfileBundleCatalog catalog,
        JsonElement goldenCase,
        string icId)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            catalog,
            goldenCase.GetProperty("profileId").GetString()!,
            goldenCase.GetProperty("profileVersion").GetString()!,
            icId,
            ExperienceIds.AbMerge,
            goldenCase.GetProperty("mapCapacity").GetInt64());
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static JsonElement ReadGoldenCase(string caseId)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json")));
        return manifest.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(item.GetProperty("caseId").GetString(), caseId))
            .Clone();
    }

    private static Dictionary<string, byte[]> ReadInputs(JsonElement inputs)
    {
        return inputs.EnumerateObject().ToDictionary(
            static item => item.Name,
            static item => ReadFixture(item.Value),
            StringComparer.Ordinal);
    }

    private static byte[] ReadFixture(JsonElement entry)
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.ManifestPath(FixtureRoot, entry));
        Assert.Equal(entry.GetProperty("size").GetInt64(), bytes.LongLength);
        Assert.Equal(entry.GetProperty("sha256").GetString(), Hash(bytes));
        return bytes;
    }

    private static List<ByteRange> FindChangedRanges(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        var ranges = new List<ByteRange>();
        int index = 0;
        while (index < actual.Length)
        {
            if (actual[index] == expected[index])
            {
                index++;
                continue;
            }

            int start = index;
            do
            {
                index++;
            }
            while (index < actual.Length && actual[index] != expected[index]);
            ranges.Add(new ByteRange(start, index - start));
        }

        return ranges;
    }

    private static string FixtureRoot => RepositoryPaths.FromRepositoryRoot("testdata", "golden", "ab-merge");

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(
        Environment.NewLine,
        issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
