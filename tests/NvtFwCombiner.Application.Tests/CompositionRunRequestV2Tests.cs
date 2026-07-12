using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests the closed Application admission contract for V2 plan and runtime artifacts.</summary>
public sealed class CompositionRunRequestV2Tests
{
    private static readonly DateTimeOffset FirstTimestamp = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondTimestamp = FirstTimestamp.AddSeconds(1);
    private static readonly DateTimeOffset ThirdTimestamp = FirstTimestamp.AddSeconds(2);
    private static readonly DateTimeOffset FourthTimestamp = FirstTimestamp.AddSeconds(3);

    /// <summary>Verifies V2PlanCompiled cannot reach the current Preview or Build request boundary.</summary>
    [Fact]
    public void RequestRejectsV2PlanCompiledArtifact()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "v2-run",
            CreateV2PlanCompiled(),
            [],
            "output.bin"));

        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the exact trusted V2 authority pair accepts only a contract-matching binding and literal output.</summary>
    [Fact]
    public void RequestAcceptsV2RuntimeArtifactWithExactBindingAndOutput()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable();
        var request = new CompositionRunRequest(
            "v2-runtime",
            composition,
            [CreateBinding()],
            "v2-output.bin");

        Assert.Same(composition, request.CompiledComposition);
        Assert.Equal("input.bin", Assert.Single(request.ArtifactBindings.Values).OriginalFileName);
    }

    /// <summary>Verifies this runtime slice rejects an otherwise safe output override until token rendering owns the policy.</summary>
    [Fact]
    public void RuntimeArtifactRejectsOutputOverridePolicy()
    {
        _ = Assert.Throws<ArgumentException>(() => CreateV2RuntimeExecutable(allowOutputOverride: true));
    }

    /// <summary>Verifies V2 runtime request construction rejects missing provenance, class/extension mismatch, extras, and forbidden output overrides.</summary>
    [Fact]
    public void RequestRejectsV2BindingOrOutputPolicyMismatch()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable();
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "missing-name",
            composition,
            [new InputArtifactBinding("input", "input", "input-artifact")],
            "v2-output.bin"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "wrong-class",
            composition,
            [new InputArtifactBinding(
                "input",
                "input",
                "input-artifact",
                "input.bin",
                CompiledInputArtifactClass.TpFirmware)],
            "v2-output.bin"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "wrong-extension",
            composition,
            [new InputArtifactBinding(
                "input",
                "input",
                "input-artifact",
                "input.hex",
                CompiledInputArtifactClass.ReferenceImage)],
            "v2-output.bin"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "extra-binding",
            composition,
            [
                CreateBinding(),
                new InputArtifactBinding(
                    "extra",
                    "extra",
                    "extra-artifact",
                    "extra.bin",
                    CompiledInputArtifactClass.ReferenceImage),
            ],
            "v2-output.bin"));
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "forbidden-override",
            composition,
            [CreateBinding()],
            "other.bin"));
    }

    /// <summary>Verifies a V2 preview and approved build use the existing engine and retain the compiled fingerprint in reports.</summary>
    [Fact]
    public async Task V2RuntimeArtifactRunsThroughPreviewAndBuildWithFingerprintParity()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable();
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]> { ["input-artifact"] = [1, 2, 3, 4] }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var request = new CompositionRunRequest("v2-run", composition, [CreateBinding()], "v2-output.bin");

        AssertBindingSnapshotCannotBeMutated(request);
        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);
        AssertBindingSnapshotCannotBeMutated(request);
        CompositionRunResult build = await service.BuildAsync(
            request.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, build.Status);
        Assert.Equal([1, 2, 3, 4], preview.OutputBytes.ToArray());
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        Assert.Equal(composition.CompilationFingerprint, preview.Report.CompilationFingerprint);
        Assert.Equal(composition.CompilationFingerprint, build.Report.CompilationFingerprint);
        Assert.Equal("input.bin", Assert.Single(preview.Report.Inputs).OriginalFileName);
        Assert.True(writer.WasCalled);
        Assert.Equal("v2-output.bin", writer.FileName);
    }

    /// <summary>Verifies original filename provenance cannot be changed after preview without invalidating Build approval.</summary>
    [Fact]
    public async Task V2OriginalFileNameChangeInvalidatesApprovedBuild()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable();
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]> { ["input-artifact"] = [1, 2, 3, 4] }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var previewRequest = new CompositionRunRequest("v2-preview", composition, [CreateBinding()], "v2-output.bin");
        var changedNameRequest = new CompositionRunRequest(
            "v2-preview",
            composition,
            [CreateBinding("renamed.bin")],
            "v2-output.bin");

        CompositionRunResult preview = await service.PreviewAsync(previewRequest, CancellationToken.None);
        CompositionRunResult build = await service.BuildAsync(
            changedNameRequest.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal(CompositionExecutionStatus.Failed, build.Status);
        Assert.Null(build.CommittedOutputId);
        Assert.False(writer.WasCalled);
    }

    private static InputArtifactBinding CreateBinding(string originalFileName = "input.bin")
    {
        return new InputArtifactBinding(
            "input",
            "input",
            "input-artifact",
            originalFileName,
            CompiledInputArtifactClass.ReferenceImage);
    }

    private static void AssertBindingSnapshotCannotBeMutated(CompositionRunRequest request)
    {
        IDictionary<string, InputArtifactBinding> bindings = Assert.IsType<IDictionary<string, InputArtifactBinding>>(
            request.ArtifactBindings,
            exactMatch: false);

        Assert.True(bindings.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => bindings["input"] = CreateBinding("changed.bin"));
    }

    private static CompiledComposition CreateV2PlanCompiled()
    {
        return CreateV2Artifact(
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Compilable, []),
            "{original-name}.bin",
            ["original-name"],
            runtimeExecutable: false);
    }

    private static CompiledComposition CreateV2RuntimeExecutable(bool allowOutputOverride = false)
    {
        return CreateV2Artifact(
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            "v2-output.bin",
            [],
            runtimeExecutable: true,
            allowOutputOverride: allowOutputOverride);
    }

    private static CompiledComposition CreateV2Artifact(
        CompiledProfilePromotion promotion,
        string outputTemplate,
        IReadOnlyList<string> requiredTokenIds,
        bool runtimeExecutable,
        bool allowOutputOverride = false)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap();
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            resolvedMap,
            promotion,
            ["profile-evidence"],
            [],
            []);
        var output = new CompiledOutputNamingRequirement(
            outputTemplate,
            allowOverride: allowOutputOverride,
            CompiledOutputInvalidCharacterPolicy.Reject,
            requiredTokenIds);
        var identity = new V2CompiledCompositionIdentity(
            "profile-v2",
            "2.0.0",
            "standard-merge",
            CompositionKind.Merge,
            new V2CompiledCompositionDetails(
                provenance,
                CreateInputContract(),
                new CompiledRegionAccessContract([], []),
                output));
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            [
                new AddressSpace("input", 4, AddressSpaceMutability.Immutable, allowedInputLengths: [4]),
                new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange(
                "copy-input",
                10,
                "input",
                new ByteRange(0, 4),
                "output-image",
                new ByteRange(0, 4),
                OverlapPolicy.Reject,
                "copy synthetic immutable input")]);
        return runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(
                plan,
                identity,
                CompiledIcNumberPolicy.NotApplicable)
            : CompiledComposition.CreateV2(
                plan,
                identity,
                CompiledIcNumberPolicy.NotApplicable);
    }

    private static CompiledInputContract CreateInputContract()
    {
        return new CompiledInputContract(
            [new CompiledInputSlotRequirement(
                "input-slot",
                "input",
                CompiledInputArtifactClass.ReferenceImage,
                required: true,
                CompiledInputSlotCardinality.ExactlyOne,
                [".bin"],
                new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
                new CompiledNoInputNormalization())],
            [new CompiledInputSpaceBinding(
                "input",
                "input-slot",
                CompiledInputInstancePolicy.Singleton)]);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap()
    {
        var map = FirmwareImageMap.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                ["standard"],
                TopologyRequirement.NoTopologyConstraint(),
                4),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 4),
                    FirmwareWriteConstraint.Forbidden)],
                ["map-evidence"])],
            [],
            ["map-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            [map],
            []);
        FirmwareMapResolutionResult result = definition.ResolveMap(new FirmwareMapResolutionInputs(
            "NT-SYNTHETIC",
            "standard",
            4,
            requestedTopology: null,
            []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }

    private sealed class RecordingOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        public ValueTask<string> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            FileName = fileName;
            return ValueTask.FromResult($"committed:{fileName}");
        }
    }
}
