using System.Buffers.Binary;
using System.Diagnostics;
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

        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase);
        byte[] expected = ReadExpected(goldenCase);
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

    /// <summary>
    /// Verifies the owner-confirmed NT51919/NT51932 fact-scoped aliases preserve the direct NT51929 golden bytes.
    /// This is alias parity and does not present the NT51929 output as a direct alias product golden.
    /// </summary>
    [Theory]
    [InlineData("NT51919", "nt51919-ab-merge-alias")]
    [InlineData("NT51932", "nt51932-ab-merge")]
    public void Nt51919AndNt51932FactScopedAliasesMatchNt51929AbGolden(string icId, string profileId)
    {
        JsonElement goldenCase = ReadGoldenCase("nt51929-ab-t05-d06");
        JsonElement applicability = goldenCase.GetProperty("evidenceApplicability");
        Assert.Equal(
            ["NT51929"],
            applicability.GetProperty("directMemberIds").EnumerateArray().Select(static item => item.GetString()));
        Assert.Equal(
            ["NT51919", "NT51932"],
            applicability.GetProperty("factScopedAliasMemberIds").EnumerateArray().Select(static item => item.GetString()));
        Assert.Empty(applicability.GetProperty("notEstablishedMemberIds").EnumerateArray());
        CanonicalGoldenAlias alias = CanonicalGoldenTestData.LoadWorkflowAliases("ab-merge")
            .Single(item => StringComparer.Ordinal.Equals(item.Ic, icId));
        Assert.Equal("nt51929-ab-t05-d06", alias.SourceCaseId);
        Assert.Equal("NT51929", alias.SourceIc);
        CompiledComposition composition = CompileCandidate(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(Nt51929BundleDirectory, Nt51929BundleContentHash),
            goldenCase,
            icId,
            profileId);
        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase);
        byte[] expected = ReadExpected(goldenCase);

        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(inputs));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal("c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2", Hash(result.OutputBytes.Span));
    }

    /// <summary>
    /// Verifies each directly named 51929/51932 candidate configuration against the immutable Python snapshot.
    /// This synthetic parity is migration evidence and is not a direct owner product golden.
    /// </summary>
    [Theory]
    [InlineData("NT51929", "nt51929-ab-merge", "51929")]
    [InlineData("NT51932", "nt51932-ab-merge", "51932")]
    public async Task Nt51929AndNt51932CandidatesMatchNamedPythonReferenceAsync(
        string icId,
        string profileId,
        string referenceConfiguration)
    {
        const int capacity = 0x80000;
        const int tpLength = 0x40000;
        const string expectedSha256 = "cd54e124b02f2a91a5f43836ab49cc28db811a4a8e1ff407eb98e47437de10ce";

        CompiledComposition composition = CompileCandidate(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(Nt51929BundleDirectory, Nt51929BundleContentHash),
            profileId,
            "0.1.0",
            icId,
            capacity);
        byte[] dp = CreateAddressSensitivePattern(capacity, 0x31);
        byte[] tpA = CreateAddressSensitivePattern(tpLength, 0x57);
        byte[] tpB = CreateAddressSensitivePattern(tpLength, 0x83);
        WriteRelocationPointers(tpB);
        byte[] originalTpB = [.. tpB];
        using var referenceWorkspace = TempWorkspace.Create($"nfc-{icId}-ab-python-reference");
        byte[] pythonReferenceOutput = await RunPythonReferenceAsync(
            referenceWorkspace,
            referenceConfiguration,
            dp,
            tpA,
            tpB,
            TestContext.Current.CancellationToken);

        CompositionExecutionResult result = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expectedSha256, Hash(pythonReferenceOutput));
        Assert.Equal(pythonReferenceOutput, result.OutputBytes.ToArray());
        Assert.Equal(expectedSha256, Hash(result.OutputBytes.Span));
        Assert.Equal(originalTpB, tpB);
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
        JsonElement applicability = goldenCase.GetProperty("evidenceApplicability");
        Assert.Equal(
            ["NT51951"],
            applicability.GetProperty("factScopedAliasMemberIds").EnumerateArray().Select(static item => item.GetString()));
        Assert.Empty(applicability.GetProperty("notEstablishedMemberIds").EnumerateArray());
        CanonicalGoldenAlias alias = CanonicalGoldenTestData.LoadWorkflowAliases("ab-merge")
            .Single(item =>
                StringComparer.Ordinal.Equals(item.Ic, "NT51951") &&
                StringComparer.Ordinal.Equals(item.SourceCaseId, caseId));
        Assert.Equal("NT51950", alias.SourceIc);
        using var workspace = TempWorkspace.Create($"nfc-{caseId}");
        CompiledComposition composition = CompileCandidate(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
                workspace,
                Nt51950BundleDirectory,
                Nt51950BundleContentHash),
            goldenCase,
            "NT51950");
        Dictionary<string, byte[]> inputs = ReadInputs(goldenCase);
        byte[] expected = ReadExpected(goldenCase);
        byte[] originalTpB = [.. inputs["tp-b-input"]];
        using var referenceWorkspace = TempWorkspace.Create($"nfc-{caseId}-python-reference");
        byte[] pythonReferenceOutput = await RunPythonReferenceAsync(
            referenceWorkspace,
            "51950",
            inputs["dp-ab-input"],
            inputs["tp-a-input"],
            inputs["tp-b-input"],
            TestContext.Current.CancellationToken);
        Assert.Equal(expected, pythonReferenceOutput);
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
            Assert.Equal(pythonReferenceOutput, result.OutputBytes.ToArray());
            Assert.Equal(ExpectedArtifact(goldenCase).GetProperty("sha256").GetString(), Hash(result.OutputBytes.Span));
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

    /// <summary>
    /// Verifies the compiled NT51951 candidate and Combiner match the immutable Python snapshot byte-for-byte.
    /// </summary>
    [Fact]
    public async Task Nt51951CandidatePlanWithCombinerMatchesPythonReferenceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const int outputLength = 0x100000;
        const int tpLength = 0x37000;
        const string expectedSha256 = "e1524ba52b41d5a49eb58fcdb75326d5f0c78a6df7af2fcfdaa632a12e628c71";

        using var workspace = TempWorkspace.Create("nfc-nt51951-ab-topology");
        CompiledComposition composition = CompileCandidate(
            AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
                workspace,
                Nt51950BundleDirectory,
                Nt51950BundleContentHash),
            "nt51951-ab-merge",
            "0.1.0",
            "NT51951",
            outputLength);
        byte[] dp = CreatePattern(outputLength, 37, 11);
        byte[] tpA = CreatePattern(tpLength, 19, 23);
        byte[] tpB = CreatePattern(tpLength, 29, 31);
        WriteHeaderPointers(tpA);
        WriteHeaderPointers(tpB);
        BinaryPrimitives.WriteUInt32LittleEndian(tpA.AsSpan(0xA130, sizeof(uint)), 0x1F6CF3EC);
        byte[] originalTpB = [.. tpB];
        using var referenceWorkspace = TempWorkspace.Create("nfc-nt51951-ab-python-reference");
        byte[] pythonReferenceOutput = await RunPythonReferenceAsync(
            referenceWorkspace,
            "51951",
            dp,
            tpA,
            tpB,
            TestContext.Current.CancellationToken);
        Assert.Equal(expectedSha256, Hash(pythonReferenceOutput));

        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"nfc-nt51951-ab-topology-{Guid.NewGuid():N}");
        ExternalProcessorResult? externalResult = null;
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
            CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
                composition.Plan,
                new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["dp-ab-input"] = dp,
                    ["tp-a-input"] = tpA,
                    ["tp-b-input"] = tpB,
                }),
                async (operation, inputBytes, stagedSources, stagedArtifacts, cancellationToken) =>
                {
                    Assert.Empty(stagedSources);
                    ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(
                        operation.ExternalProcessorInvocation);
                    externalResult = await processor.TransformAsync(
                        new ExternalProcessorRequest(
                            "nt51951-synthetic-topology",
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
            Assert.Equal(expectedSha256, Hash(result.OutputBytes.Span));
            Assert.Equal(pythonReferenceOutput, result.OutputBytes.ToArray());
            Assert.Equal(originalTpB, tpB);
            ExternalProcessorResult toolResult = Assert.IsType<ExternalProcessorResult>(externalResult);
            Assert.Equal(
                [new ByteRange(0x8A102, 1), new ByteRange(0x8A112, 1), new ByteRange(0x8A130, 4)],
                toolResult.ChangedRanges);
            ExternalProcessInvocation command = Assert.Single(toolResult.ExecutedCommands);
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
        string icId,
        string? profileId = null)
    {
        return CompileCandidate(
            catalog,
            profileId ?? goldenCase.GetProperty("profileId").GetString()!,
            goldenCase.GetProperty("profileVersion").GetString()!,
            icId,
            goldenCase.GetProperty("mapCapacity").GetInt64());
    }

    private static CompiledComposition CompileCandidate(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string icId,
        long mapCapacity)
    {
        V2CompositionPlanCompileResult compilation = TrustedV2CompositionCompiler.Compile(
            catalog,
            profileId,
            profileVersion,
            icId,
            ExperienceIds.AbMerge,
            mapCapacity);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static JsonElement ReadGoldenCase(string caseId)
    {
        return CanonicalGoldenTestData.LoadDirectCase("ab-merge", caseId);
    }

    private static Dictionary<string, byte[]> ReadInputs(JsonElement goldenCase)
    {
        return goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Where(static artifact => artifact.GetProperty("role").GetString() == "input")
            .ToDictionary(
            static artifact => artifact.GetProperty("artifactId").GetString()!,
            static artifact => ReadFixture(artifact),
            StringComparer.Ordinal);
    }

    private static byte[] ReadExpected(JsonElement goldenCase)
    {
        return ReadFixture(ExpectedArtifact(goldenCase));
    }

    private static JsonElement ExpectedArtifact(JsonElement goldenCase)
    {
        return goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(static artifact => artifact.GetProperty("role").GetString() == "expected");
    }

    private static byte[] ReadFixture(JsonElement entry)
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.ManifestPath(CanonicalGoldenTestData.Root, entry));
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

    private static async Task<byte[]> RunPythonReferenceAsync(
        TempWorkspace workspace,
        string icId,
        byte[] dp,
        byte[] tpA,
        byte[] tpB,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(dp);
        ArgumentNullException.ThrowIfNull(tpA);
        ArgumentNullException.ThrowIfNull(tpB);

        // This test-only process invokes the immutable reference snapshot; production never loads it.
        string dpPath = workspace.Write("DP_AB/input.bin", dp);
        string tpAPath = workspace.Write("TPA/input.bin", tpA);
        string tpBPath = workspace.Write("TPB/input.bin", tpB);
        string outputPath = workspace.PathFor("python-reference-output.bin");
        string referenceRoot = RepositoryPaths.FromRepositoryRoot("refcode", "ab_code_combiner");
        const string script = """
            import pathlib
            import sys

            reference_root = pathlib.Path(sys.argv[1])
            sys.path.insert(0, str(reference_root))

            from combine import combine
            from ic_config import IC_CONFIGS

            dp = bytearray(pathlib.Path(sys.argv[2]).read_bytes())
            tpa = bytearray(pathlib.Path(sys.argv[3]).read_bytes())
            tpb = bytearray(pathlib.Path(sys.argv[4]).read_bytes())
            output = combine(IC_CONFIGS[sys.argv[5]], dp, tpa, tpb, debug=0)
            pathlib.Path(sys.argv[6]).write_bytes(output)
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = workspace.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(referenceRoot);
        startInfo.ArgumentList.Add(dpPath);
        startInfo.ArgumentList.Add(tpAPath);
        startInfo.ArgumentList.Add(tpBPath);
        startInfo.ArgumentList.Add(icId);
        startInfo.ArgumentList.Add(outputPath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Python AB reference snapshot.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        string output = await standardOutput;
        string error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Python AB reference failed for NT{icId}. Exit={process.ExitCode}{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{output}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{error}");
        Assert.True(File.Exists(outputPath), $"Python AB reference did not produce {outputPath}.");
        return File.ReadAllBytes(outputPath);
    }

    private static byte[] CreatePattern(int length, int multiplier, int addend)
    {
        return [.. Enumerable.Range(0, length).Select(index => (byte)(((index * multiplier) + addend) & byte.MaxValue))];
    }

    private static byte[] CreateAddressSensitivePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            int wordOffset = index & ~3;
            uint word = unchecked(
                ((uint)wordOffset * 0x9E3779B9U)
                ^ (salt * 0x01010101U)
                ^ 0xA5A5A5A5U);
            bytes[index] = unchecked((byte)(word >> ((index & 3) * 8)));
        }

        return bytes;
    }

    private static void WriteRelocationPointers(byte[] image)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x7164, sizeof(uint)), 0x00123456);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x7168, sizeof(uint)), 0x00ABCDEF);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x716C, sizeof(uint)), 0x0000C0DE);
    }

    private static void WriteHeaderPointers(byte[] image)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA100, sizeof(uint)), 0x0000C000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA110, sizeof(uint)), 0x00011000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xA120, sizeof(uint)), 0x0001A000);
    }

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
