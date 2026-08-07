using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Compilable evidence for the fixed-bank NT51950 AB Merge Combiner profile.</summary>
public sealed class Nt51950AbMergeCandidateProfileTests
{
    private const string BundleDirectory = "nt51950-ab-merge";
    private const string BundleContentHash = "775c42fba1fbbf1c4c8869656c83c86ce34d612dda3ceed92a93cb4e82f7cd67";
    private const int Capacity = 0x80000;
    private const int BankLength = 0x40000;
    private const int TpInputLength = 0x37000;
    private const int TpCodeStart = 0xA000;
    private const int TpCodeLength = 0x2D000;

    /// <summary>Verifies the candidate compiles the exact staged Combiner boundary for the audited 512 KiB NT51950 AB image.</summary>
    [Fact]
    public void CandidateProfileDeclaresFullBanksAndCombinerOnlyHeaderMutation()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.True(composition.IsV2AbFunctionOpenCandidate);
        Assert.Equal(
            "b00181e924452c038a629b7d8ff52d12c240bf0bb5d14efed6f77529ec6ae042",
            composition.IntegrityFingerprint);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51950-ab-merge-512k", details.Provenance.ResolvedMap.ImageMap.MapId);
        AssertRegionRange(details, "a-cmi-dp-version", 0x3B016, 3);
        AssertRegionRange(details, "b-cmi-dp-version", 0x7B016, 3);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, details.Provenance.Promotion.Stage);
        Assert.Equal(
            ["firmware-owner-review", "golden-certification-closure"],
            details.Provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            [
                "copy-dp-ab-image",
                "overlay-tpa-into-output",
                "relocate-tpb-diff-for-b-bank",
                "overlay-tpb-into-output",
                "copy-a-bank-to-combiner-work",
                "copy-b-bank-to-combiner-work",
                "run-nt51950-ab-combiner",
                "import-postbuild-b-ilm",
                "import-postbuild-b-dlm",
                "import-postbuild-b-crc",
            ],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        CompositionOperation diffRelocation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "relocate-tpb-diff-for-b-bank"));
        Assert.Equal(CompositionOperationKind.TransformScalar, diffRelocation.Kind);
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), diffRelocation.SourceRange);
        Assert.Equal(new ByteRange(0xA120, sizeof(uint)), diffRelocation.TargetRange);
        Assert.Equal(new BigInteger(0x40000), Assert.IsType<ScalarTransform>(diffRelocation.ScalarTransform).Addend);
        Assert.Equal(
            [new ByteRange(TpCodeStart, TpCodeLength), new ByteRange(TpCodeStart, TpCodeLength)],
            composition.Plan.OrderedOperations
                .Where(static operation => operation.OperationId is
                    "overlay-tpa-into-output" or "overlay-tpb-into-output")
                .Select(static operation => operation.SourceRange));

        CompositionOperation postbuild = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "run-nt51950-ab-combiner"));
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(postbuild.ExternalProcessorInvocation);
        Assert.Equal("ab-combiner-work", postbuild.TargetSpaceId);
        Assert.Equal("nfc-nt51950-ab-merge-combiner-v1", invocation.ProcessorId);
        Assert.Equal("legacy-combiner-1.13.0", invocation.ToolBindingId);
        Assert.Equal(
            [new ByteRange(0x4A100, 4), new ByteRange(0x4A110, 4), new ByteRange(0x4A130, 4)],
            invocation.AllowedWriteRanges);
        Assert.Equal(
            ["a-bank", "b-bank"],
            invocation.StagedArtifactBindings.Select(static binding => binding.ArtifactId));
        Assert.Equal(
            ["output-image", "output-image"],
            invocation.StagedArtifactBindings.Select(static binding => binding.SourceSpaceId));
        Assert.Equal(
            [new ByteRange(0, BankLength), new ByteRange(BankLength, BankLength)],
            invocation.StagedArtifactBindings.Select(static binding => binding.SourceRange));
    }

    /// <summary>Verifies the profile derives TP source geometry and relocation from one placed region template.</summary>
    [Fact]
    public void CandidateProfileDerivesTpGeometryFromRegionInstances()
    {
        string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        using var family = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "profiles",
            "built-in",
            BundleDirectory,
            "families",
            "nt51950-ab-merge.json")));
        Assert.Equal("1.2", family.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement[] regionSets = [.. family.RootElement.GetProperty("regionSets").EnumerateArray()];
        Assert.DoesNotContain(
            regionSets.SelectMany(static set => set.GetProperty("regions").EnumerateArray()),
            static region =>
            {
                string regionId = region.GetProperty("regionId").GetString()!;
                return regionId.Contains("control", StringComparison.Ordinal) ||
                    regionId.Contains("preserved-tail", StringComparison.Ordinal);
            });
        Assert.Contains(
            regionSets,
            static set => set.TryGetProperty("regionTemplates", out JsonElement templates) &&
                templates.EnumerateArray().Any(template =>
                    template.GetProperty("regions").EnumerateArray().Any(region =>
                        region.GetProperty("regionId").GetString() == "tp-code")));

        using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "profiles",
            "built-in",
            BundleDirectory,
            "profiles",
            "nt51950-ab-merge.json")));
        Assert.Equal("2.14", profile.RootElement.GetProperty("schemaVersion").GetString());
        JsonElement[] views = [.. profile.RootElement.GetProperty("views").EnumerateArray()];
        foreach (string viewId in new[] { "tp-a-code-source", "tp-b-code-source" })
        {
            JsonElement view = Assert.Single(
                views,
                candidate => candidate.GetProperty("viewId").GetString() == viewId);
            Assert.Equal("region-template-range", view.GetProperty("selector").GetProperty("kind").GetString());
        }

        JsonElement relocation = Assert.Single(
            profile.RootElement.GetProperty("operations").EnumerateArray(),
            static operation => operation.GetProperty("operationId").GetString() == "relocate-tpb-diff-for-b-bank");
        Assert.Equal(
            "region-instance-delta",
            relocation.GetProperty("addend").GetProperty("kind").GetString());
    }

    /// <summary>Verifies only the seed, named TP placements, and exact postbuild header fields can write the output.</summary>
    [Fact]
    public void CandidatePlanWritesOnlySeedTpPlacementsAndPostbuildFieldsToOutput()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-output-authority");
        CompiledComposition composition = CompileCandidate(workspace);

        Assert.Equal(
            [
                ("copy-dp-ab-image", new ByteRange(0, Capacity)),
                ("overlay-tpa-into-output", new ByteRange(0xA000, TpCodeLength)),
                ("overlay-tpb-into-output", new ByteRange(0x4A000, TpCodeLength)),
                ("import-postbuild-b-ilm", new ByteRange(0x4A100, sizeof(uint))),
                ("import-postbuild-b-dlm", new ByteRange(0x4A110, sizeof(uint))),
                ("import-postbuild-b-crc", new ByteRange(0x4A130, sizeof(uint))),
            ],
            composition.Plan.OrderedOperations
                .Where(operation => operation.TargetSpaceId == composition.Plan.OutputSpaceId)
                .Select(static operation => (operation.OperationId, operation.TargetRange)));
    }

    /// <summary>Verifies the narrowly admitted function-open candidate can create an application run request.</summary>
    [Fact]
    public void FunctionOpenCandidateCanCreateApplicationRunRequest()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);

        var request = new CompositionRunRequest(
            "ab-candidate",
            composition,
            CreateRuntimeBindings(),
            composition.DefaultOutputFileName,
            abMergeTopologySelection: SingleTopology());

        Assert.Same(composition, request.CompiledComposition);
    }

    /// <summary>Verifies the engine stages immutable A/B artifacts, relocates only TPB DIFF, and leaves header CRC to Combiner.</summary>
    [Fact]
    public async Task CandidatePlanStagesRawTpBanksAndLeavesCallerInputsUntouchedAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        byte[] dp = CreatePattern(Capacity, 0x11);
        byte[] tpA = CreatePattern(TpInputLength, 0x44);
        byte[] tpB = CreatePattern(TpInputLength, 0x77);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, sizeof(uint)), 0x12345678);
        byte[] originalTpB = [.. tpB];
        bool invoked = false;

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }),
            (_, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                invoked = true;
                Assert.Empty(stagedSources);
                Assert.Equal(["a-bank", "b-bank"], stagedArtifacts.Select(static artifact => artifact.ArtifactId));
                Assert.Equal(BankLength, stagedArtifacts[0].Bytes.Length);
                Assert.Equal(BankLength, stagedArtifacts[1].Bytes.Length);
                AssertRangeEquals(dp, 0, stagedArtifacts[0].Bytes.Span, 0, TpCodeStart);
                AssertRangeEquals(tpA, TpCodeStart, stagedArtifacts[0].Bytes.Span, TpCodeStart, TpCodeLength);
                AssertRangeEquals(dp, BankLength, stagedArtifacts[1].Bytes.Span, 0, TpCodeStart);
                AssertRangeEquals(tpB, TpCodeStart, stagedArtifacts[1].Bytes.Span, TpCodeStart, 0x120);
                Assert.Equal(
                    0x12385678u,
                    BinaryPrimitives.ReadUInt32LittleEndian(stagedArtifacts[1].Bytes.Span.Slice(0xA120, sizeof(uint))));
                AssertRangeEquals(
                    tpB,
                    TpCodeStart + 0x124,
                    stagedArtifacts[1].Bytes.Span,
                    TpCodeStart + 0x124,
                    TpCodeLength - 0x124);
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(originalTpB, tpB);
        AssertRangeEquals(tpA, TpCodeStart, result.OutputBytes.Span, TpCodeStart, TpCodeLength);
        AssertRangeEquals(tpB, TpCodeStart, result.OutputBytes.Span, BankLength + TpCodeStart, 0x120);
        Assert.Equal(
            0x12385678u,
            BinaryPrimitives.ReadUInt32LittleEndian(result.OutputBytes.Span.Slice(BankLength + 0xA120, sizeof(uint))));
        AssertRangeEquals(
            tpB,
            TpCodeStart + 0x124,
            result.OutputBytes.Span,
            BankLength + TpCodeStart + 0x124,
            TpCodeLength - 0x124);
        AssertRangeEquals(dp, 0, result.OutputBytes.Span, 0, TpCodeStart);
        AssertRangeEquals(dp, 0x37000, result.OutputBytes.Span, 0x37000, 0x13000);
        AssertRangeEquals(dp, 0x77000, result.OutputBytes.Span, 0x77000, 0x9000);
    }

    /// <summary>Verifies no alternate capacity can select the fixed full-bank candidate map.</summary>
    [Fact]
    public void CandidateProfileRejectsNon512KiBMapCapacity()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51950-ab-merge",
            "0.3.0",
            "NT51950",
            ExperienceIds.AbMerge,
            requestedMapCapacity: BankLength);

        Assert.False(compilation.IsCompiled);
        Assert.Contains(
            compilation.Issues,
            static issue => StringComparer.Ordinal.Equals(issue.Code, "profile.v2.compile.map-capacity-unavailable"));
    }

    /// <summary>Verifies the same NT51950 contract selects the declared cascade map at one mebibyte.</summary>
    [Fact]
    public void CandidateProfileSelectsOneMebibyteCascadeMap()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51950-ab-merge",
            "0.3.0",
            "NT51950",
            ExperienceIds.AbMerge,
            requestedMapCapacity: 0x100000,
            requestedTopology: CascadeTopology(),
            resolutionArtifacts: []);

        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(composition.V2Details);
        Assert.Equal("nt51950-ab-merge-1024k", details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(0x100000, composition.Plan.OutputInitialization.Capacity);
        AssertRegionRange(details, "a-cmi-dp-version", 0x5016, 3);
        AssertRegionRange(details, "b-cmi-dp-version", 0x45016, 3);
        Assert.Equal(
            [
                ("copy-dp-ab-image", new ByteRange(0, 0x100000)),
                ("overlay-tpa-into-output", new ByteRange(0xA000, TpCodeLength)),
                ("overlay-tpb-into-output", new ByteRange(0x4A000, TpCodeLength)),
                ("import-postbuild-b-ilm", new ByteRange(0x4A100, sizeof(uint))),
                ("import-postbuild-b-dlm", new ByteRange(0x4A110, sizeof(uint))),
                ("import-postbuild-b-crc", new ByteRange(0x4A130, sizeof(uint))),
            ],
            composition.Plan.OrderedOperations
                .Where(operation => operation.TargetSpaceId == composition.Plan.OutputSpaceId)
                .Select(static operation => (operation.OperationId, operation.TargetRange)));
        Assert.Equal(
            [new ByteRange(TpCodeStart, TpCodeLength), new ByteRange(TpCodeStart, TpCodeLength)],
            composition.Plan.OrderedOperations
                .Where(static operation => operation.OperationId is
                    "overlay-tpa-into-output" or "overlay-tpb-into-output")
                .Select(static operation => operation.SourceRange));
        CompositionOperation relocation = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => operation.OperationId == "relocate-tpb-diff-for-b-bank");
        Assert.Equal(new BigInteger(0x40000), Assert.IsType<ScalarTransform>(relocation.ScalarTransform).Addend);
    }

    /// <summary>Verifies the cascade route never exposes or rewrites the opaque DP container tail during TP postbuild.</summary>
    [Fact]
    public async Task CascadePlanPreservesCompleteDpSeedOutsideTpPlacementsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-cascade-preservation");
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51950-ab-merge",
            "0.3.0",
            "NT51950",
            ExperienceIds.AbMerge,
            requestedMapCapacity: 0x100000,
            requestedTopology: CascadeTopology(),
            resolutionArtifacts: []);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
        byte[] dp = CreatePattern(0x100000, 0x21);
        byte[] tpA = CreatePattern(TpInputLength, 0x43);
        byte[] tpB = CreatePattern(TpInputLength, 0x65);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, sizeof(uint)), 0x12345678);
        byte[] originalTpB = [.. tpB];

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["dp-ab-input"] = dp,
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }),
            (_, inputBytes, stagedSources, stagedArtifacts, _) =>
            {
                Assert.Empty(stagedSources);
                Assert.Equal(Capacity, inputBytes.Length);
                Assert.Equal(["a-bank", "b-bank"], stagedArtifacts.Select(static artifact => artifact.ArtifactId));
                Assert.All(stagedArtifacts, artifact => Assert.Equal(BankLength, artifact.Bytes.Length));
                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(inputBytes));
            },
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(0x100000, result.OutputBytes.Length);
        Assert.Equal(originalTpB, tpB);
        AssertRangeEquals(dp, 0, result.OutputBytes.Span, 0, TpCodeStart);
        AssertRangeEquals(dp, 0x37000, result.OutputBytes.Span, 0x37000, 0x13000);
        AssertRangeEquals(dp, 0x77000, result.OutputBytes.Span, 0x77000, 0x89000);
        AssertRangeEquals(tpA, TpCodeStart, result.OutputBytes.Span, TpCodeStart, TpCodeLength);
        AssertRangeEquals(tpB, TpCodeStart, result.OutputBytes.Span, BankLength + TpCodeStart, 0x120);
        Assert.Equal(
            0x12385678u,
            BinaryPrimitives.ReadUInt32LittleEndian(result.OutputBytes.Span.Slice(BankLength + 0xA120, sizeof(uint))));
        AssertRangeEquals(
            tpB,
            TpCodeStart + 0x124,
            result.OutputBytes.Span,
            BankLength + TpCodeStart + 0x124,
            TpCodeLength - 0x124);
    }

    /// <summary>Verifies the candidate rejects incomplete DP or TP prefixes before staging.</summary>
    [Theory]
    [InlineData("dp-ab-input", Capacity - 1)]
    [InlineData("dp-ab-input", Capacity + 1)]
    [InlineData("tp-a-input", TpInputLength - 1)]
    [InlineData("tp-b-input", TpInputLength - 1)]
    public async Task CandidatePlanRejectsOneByteInputCapacityDeviationAsync(string inputId, int length)
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        Dictionary<string, byte[]> inputs = CreateInputs();
        inputs[inputId] = new byte[length];

        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan,
            new CompositionExecutionInput(inputs),
            externalProcessor: null,
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.Contains(
            result.Issues,
            static issue => StringComparer.Ordinal.Equals(
                issue.Code,
                CompositionIssueCodes.InputAddressSpaceLengthMismatch));
    }

    /// <summary>Verifies declared authority covers every byte of the three external relocation fields.</summary>
    [Fact]
    public void CandidateWriteAuthorityCoversNoCarryAndCarryBytesOfEachRelocationField()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-ab-candidate");
        CompiledComposition composition = CompileCandidate(workspace);
        CompositionOperation postbuild = Assert.Single(
            composition.Plan.OrderedOperations,
            static operation => StringComparer.Ordinal.Equals(operation.OperationId, "run-nt51950-ab-combiner"));
        ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(postbuild.ExternalProcessorInvocation);
        var policy = new ChangedRangePolicy(invocation.AllowedWriteRanges);

        foreach (ByteRange changedByte in new[]
                 {
                     new ByteRange(0x4A102, 1),
                     new ByteRange(0x4A103, 1),
                     new ByteRange(0x4A112, 1),
                     new ByteRange(0x4A113, 1),
                     new ByteRange(0x4A130, 1),
                     new ByteRange(0x4A133, 1),
                 })
        {
            Assert.True(policy.Evaluate([changedByte]).IsAllowed);
        }
    }

    private static CompiledComposition CompileCandidate(TempWorkspace workspace)
    {
        V2CompositionPlanCompileResult compilation = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, BundleDirectory, BundleContentHash).Compile(
            "nt51950-ab-merge",
            "0.3.0",
            "NT51950",
            ExperienceIds.AbMerge,
            Capacity,
            SingleTopology(),
            []);
        Assert.True(compilation.IsCompiled, FormatIssues(compilation.Issues));
        return Assert.IsType<CompiledComposition>(compilation.CompiledComposition);
    }

    private static Dictionary<string, byte[]> CreateInputs()
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["dp-ab-input"] = new byte[Capacity],
            ["tp-a-input"] = new byte[TpInputLength],
            ["tp-b-input"] = new byte[TpInputLength],
        };
    }

    private static void AssertRegionRange(
        V2CompiledCompositionDetails details,
        string regionId,
        long start,
        long length)
    {
        FirmwareRegion region = Assert.Single(
            details.Provenance.ResolvedMap.ImageMap.Regions,
            candidate => StringComparer.Ordinal.Equals(candidate.RegionId, regionId));
        Assert.Equal(new ByteRange(start, length), region.Range);
    }

    private static InputArtifactBinding[] CreateRuntimeBindings()
    {
        return
        [
            new InputArtifactBinding(
                "dp-ab-input",
                "dp-ab-input",
                "dp-ab-input-artifact",
                "dp.bin",
                CompiledInputArtifactClass.DpFirmware),
            new InputArtifactBinding(
                "tp-a-input",
                "tp-a-input",
                "tp-a-input-artifact",
                "tp-a.bin",
                CompiledInputArtifactClass.TpFirmware),
            new InputArtifactBinding(
                "tp-b-input",
                "tp-b-input",
                "tp-b-input-artifact",
                "tp-b.bin",
                CompiledInputArtifactClass.TpFirmware),
        ];
    }

    private static TopologySelection SingleTopology()
    {
        return new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "test");
    }

    private static TopologySelection CascadeTopology()
    {
        return new TopologySelection(2, "Cascade", TopologySelectionSource.Requested, "test");
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 31)));
        }

        return bytes;
    }

    private static void AssertRangeEquals(
        ReadOnlySpan<byte> expected,
        int expectedStart,
        ReadOnlySpan<byte> actual,
        int actualStart,
        int length)
    {
        Assert.Equal(
            expected.Slice(expectedStart, length).ToArray(),
            actual.Slice(actualStart, length).ToArray());
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}
