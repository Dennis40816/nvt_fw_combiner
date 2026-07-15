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
    private const string Nt51950BundleContentHash = "d0bd4c4df98b256426b98cfc14be9f79b92e69524b575aca814d59073598738e";

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
