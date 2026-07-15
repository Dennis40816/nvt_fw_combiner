using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Owner-approved AB fixture evidence for V2 candidate profiles.</summary>
public sealed class AbMergeGoldenRegressionTests
{
    private const string Nt51929BundleDirectory = "nt51919-nt51929-nt51932-ab-merge";
    private const string Nt51929BundleContentHash = "b5035b9c4afa8691adb98632b4ce9a1088d74d04948ea1f20690aade889445fb";
    private const string Nt51950BundleDirectory = "nt51950-ab-merge";
    private const string Nt51950BundleContentHash = "06a671a3a6a6cb16e5cef7ed356a61626fdbd4395cd47299b95f60bb645885af";

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

    /// <summary>Verifies the approved 51950 Combiner command reproduces each owner-approved AB output byte-for-byte.</summary>
    [Theory]
    [InlineData("nt51950-ab-boe-d82t80")]
    [InlineData("nt51950-ab-hiway-d82t80")]
    public async Task Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync(string caseId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-{caseId}-{Guid.NewGuid():N}");
        ExternalProcessorResult? externalResult = null;

        try
        {
            ExternalCombinerToolManifest manifest = LoadManifest(
                Path.Combine(repositoryRoot, "external-tools", "legacy-combiner", "1.13.0", "manifest.json"));
            Assert.Equal(
                manifest.Sha256,
                Hash(File.ReadAllBytes(Path.Combine(
                    repositoryRoot,
                    "external-tools",
                    manifest.ToolId,
                    manifest.ToolVersion,
                    manifest.ExecutableName))));
            var processor = new ExternalCombinerProcessor(
                new ExternalCombinerToolRegistry([manifest]),
                Path.Combine(repositoryRoot, "external-tools"),
                stagingRoot,
                new SystemExternalProcessRunner(),
                ExternalCombinerInvocationCatalog.All);

            CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
                composition.Plan,
                new CompositionExecutionInput(inputs),
                async (operation, inputBytes, stagedSources, stagedArtifacts, cancellationToken) =>
                {
                    Assert.Empty(stagedSources);
                    ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
                        operation.ExternalProcessorInvocation);
                    externalResult = await processor.TransformAsync(
                        new ExternalProcessorRequest(
                            $"{caseId}-combiner",
                            invocation.ProcessorId,
                            invocation.ToolBindingId,
                            inputBytes,
                            invocation.AllowedWriteRanges,
                            stagedArtifacts: stagedArtifacts),
                        cancellationToken);
                    return externalResult.Succeeded
                        ? CompositionExternalProcessorResult.Success(externalResult.OutputBytes)
                        : CompositionExternalProcessorResult.Failed(externalResult.Issues);
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
            Assert.Empty(result.Issues);
            Assert.Equal(expected, result.OutputBytes.ToArray());
            Assert.Equal(goldenCase.GetProperty("expectedOutput").GetProperty("sha256").GetString(), Hash(result.OutputBytes.Span));
            Assert.Equal(originalTpB, inputs["tp-b-input"]);

            ExternalProcessorResult toolResult = Assert.IsType<ExternalProcessorResult>(externalResult);
            Assert.Equal(
                [new ByteRange(0x4A102, 1), new ByteRange(0x4A112, 1), new ByteRange(0x4A130, 4)],
                toolResult.ChangedRanges);
            ExternalProcessInvocation command = Assert.Single(toolResult.ExecutedCommands);
            Assert.Equal(
                [
                    "NT51950BASED_MERGE_AB_MODE",
                    "CRC8",
                    Path.Combine(command.WorkingDirectory, "artifact-a-bank.bin"),
                    Path.Combine(command.WorkingDirectory, "artifact-b-bank.bin"),
                    Path.Combine(command.WorkingDirectory, "output.bin"),
                    "0x40000",
                ],
                command.Arguments);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    /// <summary>Verifies the NT51951 one-mebibyte topology against a fixed synthetic Python-reference vector.</summary>
    [Fact]
    public async Task Nt51951CombinerTopologyMatchesPythonReferenceVectorAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const int outputLength = 0x100000;
        const int bankLength = 0x80000;
        const int tpLength = 0x37000;
        const int tpCodeStart = 0xA000;
        const int tpCodeEnd = 0x37000;
        const int bBankOffset = 0x80000;
        const string expectedSha256 = "e1524ba52b41d5a49eb58fcdb75326d5f0c78a6df7af2fcfdaa632a12e628c71";

        byte[] dp = CreatePattern(outputLength, 37, 11);
        byte[] tpA = CreatePattern(tpLength, 19, 23);
        byte[] tpB = CreatePattern(tpLength, 29, 31);
        WriteHeaderPointers(tpA);
        WriteHeaderPointers(tpB);
        BinaryPrimitives.WriteUInt32LittleEndian(tpA.AsSpan(0xA130, sizeof(uint)), 0x1F6CF3EC);
        byte[] originalTpB = [.. tpB];

        byte[] aBank = [.. dp.AsSpan(0, bankLength)];
        tpA.AsSpan(tpCodeStart, tpCodeEnd - tpCodeStart).CopyTo(aBank.AsSpan(tpCodeStart));
        byte[] relocatedTpB = [.. tpB];
        uint rawDiff = BinaryPrimitives.ReadUInt32LittleEndian(relocatedTpB.AsSpan(0xA120, sizeof(uint)));
        BinaryPrimitives.WriteUInt32LittleEndian(relocatedTpB.AsSpan(0xA120, sizeof(uint)), rawDiff + bBankOffset);
        byte[] bBank = [.. dp.AsSpan(bankLength, bankLength)];
        relocatedTpB.AsSpan(tpCodeStart, tpCodeEnd - tpCodeStart).CopyTo(bBank.AsSpan(tpCodeStart));
        byte[] preCombiner = [.. dp];
        aBank.CopyTo(preCombiner, 0);
        bBank.CopyTo(preCombiner, bankLength);

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-nt51951-ab-topology-{Guid.NewGuid():N}");
        try
        {
            ExternalCombinerToolManifest manifest = LoadManifest(
                Path.Combine(repositoryRoot, "external-tools", "legacy-combiner", "1.13.0", "manifest.json"));
            var processor = new ExternalCombinerProcessor(
                new ExternalCombinerToolRegistry([manifest]),
                Path.Combine(repositoryRoot, "external-tools"),
                stagingRoot,
                new SystemExternalProcessRunner(),
                ExternalCombinerInvocationCatalog.All);
            ExternalProcessorResult result = await processor.TransformAsync(
                new ExternalProcessorRequest(
                    "nt51951-synthetic-topology",
                    ExternalCombinerInvocationCatalog.Nt51951AbMerge.ProcessorId,
                    manifest.ToolBindingId,
                    preCombiner,
                    [new ByteRange(0x8A100, 4), new ByteRange(0x8A110, 4), new ByteRange(0x8A130, 4)],
                    stagedArtifacts:
                    [
                        new ExternalProcessorStagedArtifact("a-bank", aBank),
                        new ExternalProcessorStagedArtifact("b-bank", bBank),
                    ]),
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, FormatIssues(result.Issues));
            Assert.Equal(expectedSha256, Hash(result.OutputBytes.Span));
            Assert.Equal(originalTpB, tpB);
            Assert.Equal(
                [new ByteRange(0x8A102, 1), new ByteRange(0x8A112, 1), new ByteRange(0x8A130, 4)],
                result.ChangedRanges);
            ExternalProcessInvocation command = Assert.Single(result.ExecutedCommands);
            Assert.Equal(
                [
                    "NT51950BASED_MERGE_AB_MODE",
                    "CRC8",
                    Path.Combine(command.WorkingDirectory, "artifact-a-bank.bin"),
                    Path.Combine(command.WorkingDirectory, "artifact-b-bank.bin"),
                    Path.Combine(command.WorkingDirectory, "output.bin"),
                    "0x80000",
                ],
                command.Arguments);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
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

    private static ExternalCombinerToolManifest LoadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new ExternalCombinerToolManifest(
            root.GetProperty("schemaVersion").GetString()!,
            root.GetProperty("toolBindingId").GetString()!,
            root.GetProperty("toolId").GetString()!,
            root.GetProperty("toolVersion").GetString()!,
            root.GetProperty("displayName").GetString()!,
            root.GetProperty("platform").GetString()!,
            root.GetProperty("executableName").GetString()!,
            root.GetProperty("sha256").GetString()!,
            root.GetProperty("adapterId").GetString()!,
            root.GetProperty("inputMode").GetString()!,
            [.. root.GetProperty("argumentTemplate").EnumerateArray().Select(static item => item.GetString()!)],
            root.GetProperty("workingDirectoryPolicy").GetString()!,
            root.GetProperty("timeoutSeconds").GetInt32(),
            [.. root.GetProperty("allowedExtraOutputFiles").EnumerateArray().Select(static item => item.GetString()!)]);
    }

    private static byte[] CreatePattern(int length, int multiplier, int addend)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(((index * multiplier) + addend) & byte.MaxValue))];
    }

    private static void WriteHeaderPointers(byte[] image)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA100, sizeof(uint)), 0x0000C000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA110, sizeof(uint)), 0x00011000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA120, sizeof(uint)), 0x0001A000);
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
