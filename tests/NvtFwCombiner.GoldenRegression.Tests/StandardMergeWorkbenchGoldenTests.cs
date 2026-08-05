using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.GoldenRegression.Tests;

/// <summary>Golden tests that exercise the workbench command path for Standard Merge.</summary>
public sealed class StandardMergeWorkbenchGoldenTests
{
    /// <summary>Verifies the workbench facade can build every Standard Merge golden case byte-for-byte.</summary>
    [Fact]
    public async Task WorkbenchBuildStandardMergeMatchesGoldenBytes()
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-golden");

        var admittedIcIds = CanonicalCapabilityProjection.GetIcIds()
            .ToHashSet(StringComparer.Ordinal);
        JsonElement[] goldenCases =
        [
            .. manifestDocument.RootElement.GetProperty("cases")
                .EnumerateArray()
                // Retired fixtures remain immutable historical evidence, not runtime golden claims.
                .Where(goldenCase =>
                    admittedIcIds.Contains($"NT{goldenCase.GetProperty("ic").GetString()}")),
        ];

        foreach (JsonElement goldenCase in goldenCases)
        {
            await VerifyGoldenCaseAsync(goldenRoot, workspace.Root, goldenCase);
        }
    }

    /// <summary>Verifies the workbench command path can build owner-confirmed Standard Merge aliases.</summary>
    [Theory]
    [MemberData(nameof(StandardMergeAliases))]
    public async Task WorkbenchBuildStandardMergeAliasMatchesReferenceGoldenBytes(string aliasIc, string referenceIc)
    {
        string goldenRoot = CanonicalGoldenTestData.Root;
        using JsonDocument manifestDocument = CanonicalGoldenTestData.LoadDirectWorkflowManifest("standard-merge");
        JsonElement goldenCase = manifestDocument.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Single(item => item.GetProperty("ic").GetString() == referenceIc)
            .Clone();
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-alias-{aliasIc}");

        Dictionary<string, string> slotPaths = [];
        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string originalPath = RepositoryPaths.ManifestPath(goldenRoot, input.Value);
            string copiedPath = workspace.PathFor($"{input.Name}.bin");
            File.Copy(originalPath, copiedPath);
            slotPaths[input.Name] = copiedPath;
        }

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunStandardMergeAsync(
                $"NT{aliasIc}",
                slotPaths,
                build: true,
                CancellationToken.None);

        string outputPath = result.CommittedOutputId ?? result.OutputFileName;
        Assert.True(result.Succeeded, result.Status);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(
            File.ReadAllBytes(RepositoryPaths.ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(outputPath));
    }

    /// <inheritdoc/>
    public static TheoryData<string, string> StandardMergeAliases()
    {
        TheoryData<string, string> cases = [];
        foreach (CanonicalGoldenAlias alias in CanonicalGoldenTestData.LoadWorkflowAliases("standard-merge"))
        {
            cases.Add(alias.Ic[2..], alias.SourceIc[2..]);
        }

        return cases;
    }

    private static async ValueTask VerifyGoldenCaseAsync(
        string goldenRoot,
        string tempRoot,
        JsonElement goldenCase)
    {
        string ic = goldenCase.GetProperty("ic").GetString()!;
        string caseRoot = Path.Combine(tempRoot, ic);
        _ = Directory.CreateDirectory(caseRoot);

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal);
        foreach (JsonProperty input in goldenCase.GetProperty("inputs").EnumerateObject())
        {
            string originalPath = RepositoryPaths.ManifestPath(goldenRoot, input.Value);
            string copiedPath = Path.Combine(caseRoot, $"{input.Name}.bin");
            File.Copy(originalPath, copiedPath);
            slotPaths[input.Name] = copiedPath;
        }

        WorkbenchRunResult result = await CompositionExecutionAdapter.RunStandardMergeAsync(
                $"NT{ic}",
                slotPaths,
                build: true,
                CancellationToken.None)
            .ConfigureAwait(false);

        string outputPath = result.CommittedOutputId ?? result.OutputFileName;
        Assert.True(result.Succeeded, result.Status);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(
            File.ReadAllBytes(RepositoryPaths.ManifestPath(goldenRoot, goldenCase.GetProperty("expectedOutput"))),
            File.ReadAllBytes(outputPath));
        if (StringComparer.Ordinal.Equals(ic, "51929"))
        {
            VerifyNt51929CanonicalTrace(result);
        }
    }

    private static void VerifyNt51929CanonicalTrace(WorkbenchRunResult result)
    {
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement root = report.RootElement;
        JsonElement[] inputs = [.. root.GetProperty("Inputs").EnumerateArray()];
        JsonElement[] operations = [.. root.GetProperty("Operations").EnumerateArray()];

        Assert.Equal("nt51929-standard-merge-gen-flash.bin", result.OutputFileName);
        Assert.Equal(
            "377b6b9ad0d3f1b9c0c413b622e95f80b58a5e502a5fca5da90c5abf8e96d39d",
            root.GetProperty("CompilationFingerprint").GetString());
        Assert.Equal(
            ["dp-input", "tp-input"],
            inputs.Select(input => input.GetProperty("AddressSpaceId").GetString()));
        Assert.Equal(
            [
                "e9626d1230bb117a91e7cd365b060991b48bf555bda065a84f8a62edd8c95095",
                "a5631c8c05dd1e29b216b3cc00a84ef416db41e04c9d1e7c3d211a44bf7bc458",
            ],
            inputs.Select(input => input.GetProperty("Sha256").GetString()));
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            operations.Select(operation => operation.GetProperty("OperationId").GetString()));
        AssertOperation(operations[0], start: 0x7000, length: 0x39000);
        AssertOperation(operations[1], start: 0, length: 0x6000);
        Assert.Equal(
            "fdba72984d662c4d652c56d909cf27756193f3766875b42d55207bccfca6867e",
            root.GetProperty("Output").GetProperty("Sha256").GetString());
    }

    private static void AssertOperation(
        JsonElement operation,
        long start,
        long length)
    {
        JsonElement range = operation.GetProperty("TargetRange");
        Assert.Equal(start, range.GetProperty("Start").GetInt64());
        Assert.Equal(length, range.GetProperty("Length").GetInt64());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("ProcessorId").ValueKind);
        Assert.Empty(operation.GetProperty("ExecutedCommands").EnumerateArray());
    }
}
