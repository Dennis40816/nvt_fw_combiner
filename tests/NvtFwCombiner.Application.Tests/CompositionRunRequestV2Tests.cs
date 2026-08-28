using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests the closed Application admission contract for V2 plan and runtime artifacts.</summary>
public sealed partial class CompositionRunRequestV2Tests
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

    /// <summary>Runtime defense-in-depth uses the same case-insensitive compiled extension policy.</summary>
    [Fact]
    public void RequestAcceptsUppercaseCompiledExtension()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable();

        var request = new CompositionRunRequest(
            "v2-runtime-uppercase-extension",
            composition,
            [CreateBinding("input.BIN")],
            "v2-output.bin");

        Assert.Equal("input.BIN", Assert.Single(request.ArtifactBindings.Values).OriginalFileName);
    }

    /// <summary>Verifies V2 Replace artifacts enforce their compiled IC-number selector at the shared Application boundary.</summary>
    [Fact]
    public void RequestAcceptsV2ReplaceRuntimeArtifactWithMatchingIcNumberSelection()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(
            compositionKind: CompositionKind.Replace,
            icNumberInputMode: IcNumberInputMode.SingleSelector);

        var request = new CompositionRunRequest(
            "v2-replace-runtime",
            composition,
            [CreateBinding()],
            "v2-output.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        Assert.Equal(
            IcNumberInputMode.SingleSelector,
            request.CompiledComposition.V2Details.IcNumberInputMode);
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "v2-replace-without-selector",
            composition,
            [CreateBinding()],
            "v2-output.bin"));
    }

    /// <summary>Verifies a supported AB route retains its profile-declared topology admission.</summary>
    [Fact]
    public void SupportedAbRuntimeAcceptsMatchingTopologySelection()
    {
        TopologySelection topology = new(1, "1 IC", TopologySelectionSource.Requested, "test");
        CompiledComposition composition = CreateV2RuntimeExecutable(
            outputTemplate: CompiledOutputNamingRequirement.AbCodeV1Template,
            requiredTokenIds: ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"],
            modeId: ExperienceIds.AbMerge,
            experienceId: ExperienceIds.AbMerge,
            topologyRequirement: TopologyRequirement.RequireSingleChip(),
            requestedTopology: topology);

        var request = new CompositionRunRequest(
            "supported-ab-runtime",
            composition,
            [CreateBinding()],
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            abMergeTopologySelection: topology);

        Assert.Equal(topology, request.AbMergeTopologySelection);
    }

    /// <summary>Automatic rendered naming retains its compiled template until accepted inputs are read.</summary>
    [Fact]
    public void AutomaticOutputNamingRejectsPreRenderedRequestName()
    {
        var topology = new TopologySelection(
            1,
            "1 IC",
            TopologySelectionSource.Requested,
            "test");
        CompiledComposition composition = CreateV2RuntimeExecutable(
            outputTemplate: CompiledOutputNamingRequirement.AbCodeV1Template,
            requiredTokenIds: ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"],
            modeId: ExperienceIds.AbMerge,
            experienceId: ExperienceIds.AbMerge,
            topologyRequirement: TopologyRequirement.RequireSingleChip(),
            requestedTopology: topology);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositionRunRequest(
                "pre-rendered-automatic-name",
                composition,
                [CreateBinding()],
                "caller-rendered.bin",
                abMergeTopologySelection: topology));

        Assert.Equal("outputFileName", exception.ParamName);
    }

    /// <summary>Verifies a token-free V2 artifact can allow a safe plain-file-name output override.</summary>
    [Fact]
    public void RuntimeArtifactAllowsStaticOutputOverridePolicy()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: true);
        var request = new CompositionRunRequest(
            "v2-override",
            composition,
            [CreateBinding()],
            "caller-output.bin");

        Assert.Equal("caller-output.bin", request.OutputFileName);
        _ = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "v2-unsafe-override",
            composition,
            [CreateBinding()],
            "caller?.bin"));
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
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: true);
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]> { ["input-artifact"] = [1, 2, 3, 4] }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var request = new CompositionRunRequest("v2-run", composition, [CreateBinding()], "caller-output.bin");

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
        Assert.Equal("map", preview.Report.MapId);
        Assert.Equal("map", build.Report.MapId);
        Assert.Equal("input.bin", Assert.Single(preview.Report.Inputs).OriginalFileName);
        Assert.True(writer.WasCalled);
        Assert.Equal("caller-output.bin", writer.FileName);
    }

    /// <summary>Verifies an allowed V2 output override remains part of Preview-to-Build approval identity.</summary>
    [Fact]
    public async Task V2OutputOverrideChangeInvalidatesApprovedBuild()
    {
        CompiledComposition composition = CreateV2RuntimeExecutable(allowOutputOverride: true);
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]> { ["input-artifact"] = [1, 2, 3, 4] }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var previewRequest = new CompositionRunRequest(
            "v2-override",
            composition,
            [CreateBinding()],
            "caller-output.bin");
        var changedOutputRequest = new CompositionRunRequest(
            "v2-override",
            composition,
            [CreateBinding()],
            "other-output.bin");

        CompositionRunResult preview = await service.PreviewAsync(previewRequest, CancellationToken.None);
        CompositionRunResult build = await service.BuildAsync(
            changedOutputRequest.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal(CompositionExecutionStatus.Failed, build.Status);
        Assert.Null(build.CommittedOutputId);
        Assert.False(writer.WasCalled);
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

    /// <summary>Verifies V2 Replace uses the shared Application engine to clone the exact reference, pad DP input, and preserve Preview-to-Build output parity.</summary>
    [Fact]
    public async Task V2ReplaceRuntimeRunsCloneAndDpPaddingThroughPreviewAndBuild()
    {
        CompiledComposition composition = CreateV2ReplaceRuntimeExecutable();
        var writer = new RecordingOutputWriter();
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["reference-artifact"] = [0xCC, 0xCC, 0xCC, 0xCC],
                ["dp-artifact"] = [0xA0, 0xB0],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp, ThirdTimestamp, FourthTimestamp]),
            writer);
        var request = new CompositionRunRequest(
            "v2-replace-run",
            composition,
            [
                new InputArtifactBinding(
                    "reference-source",
                    "reference",
                    "reference-artifact",
                    "base.bin",
                    CompiledInputArtifactClass.ReferenceImage),
                new InputArtifactBinding(
                    "dp-source",
                    "dp",
                    "dp-artifact",
                    "dp.bin",
                    CompiledInputArtifactClass.DpFirmware),
            ],
            "v2-output.bin",
            icNumberSelection: new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        CompositionRunResult preview = await service.PreviewAsync(request, CancellationToken.None);
        CompositionRunResult build = await service.BuildAsync(
            request.WithApprovedPreviewToken(Assert.IsType<string>(preview.PreviewToken)),
            CancellationToken.None);

        Assert.Equal(CompositionExecutionStatus.Succeeded, preview.Status);
        Assert.Equal(CompositionExecutionStatus.Succeeded, build.Status);
        Assert.Equal([0xA0, 0xB0, 0x00, 0x00], preview.OutputBytes.ToArray());
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        Assert.Equal(CompositionOperationKind.ReplaceRange, Assert.Single(preview.Report.Mutations).Kind);
        Assert.True(writer.WasCalled);
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

    private static CompiledComposition CreateV2RuntimeExecutable(
        bool allowOutputOverride = false,
        CompositionKind compositionKind = CompositionKind.Merge,
        IcNumberInputMode? icNumberInputMode = null,
        string outputTemplate = "v2-output.bin",
        IReadOnlyList<string>? requiredTokenIds = null,
        string modeId = "standard",
        string experienceId = "standard-merge",
        TopologyRequirement? topologyRequirement = null,
        TopologySelection? requestedTopology = null)
    {
        return CreateV2Artifact(
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            outputTemplate,
            requiredTokenIds ?? [],
            runtimeExecutable: true,
            allowOutputOverride: allowOutputOverride,
            compositionKind: compositionKind,
            icNumberInputMode: icNumberInputMode,
            modeId: modeId,
            experienceId: experienceId,
            topologyRequirement: topologyRequirement,
            requestedTopology: requestedTopology);
    }

    private static CompiledComposition CreateV2ReplaceRuntimeExecutable()
    {
        return CreateV2Artifact(
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            "v2-output.bin",
            [],
            runtimeExecutable: true,
            compositionKind: CompositionKind.Replace,
            icNumberInputMode: IcNumberInputMode.SingleSelector,
            modeId: "dp-replace",
            experienceId: "dp-replace",
            inputContract: new CompiledInputContract(
                [
                    CompiledInputSlotTestFactory.Create(
                        "reference-slot",
                        "reference",
                        CompiledInputArtifactClass.ReferenceImage,
                        required: true,
                        CompiledInputSlotCardinality.ExactlyOne,
                        [".bin"],
                        new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
                        new CompiledNoInputNormalization()),
                    CompiledInputSlotTestFactory.Create(
                        "dp-slot",
                        "dp",
                        CompiledInputArtifactClass.DpFirmware,
                        required: true,
                        CompiledInputSlotCardinality.ExactlyOne,
                        [".bin"],
                        new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
                        new CompiledPadShorterInputNormalization(0, "synthetic-dp-padding")),
                ],
                [
                    new CompiledInputSpaceBinding(
                        "reference-source",
                        "reference-slot",
                        CompiledInputInstancePolicy.Singleton),
                    new CompiledInputSpaceBinding(
                        "dp-source",
                        "dp-slot",
                        CompiledInputInstancePolicy.Singleton),
                ]),
            plan: new CompositionPlan(
                ImageInitialization.Reference("output-image", "reference-source", 4),
                [
                    new AddressSpace("reference-source", 4, AddressSpaceMutability.Immutable),
                    new AddressSpace(
                        "dp-source",
                        4,
                        AddressSpaceMutability.Immutable,
                        inputPaddingByte: 0),
                    new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
                ],
                [CompositionOperation.ReplaceRange(
                    "replace-dp",
                    10,
                    "dp-source",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "replace synthetic DP bytes")]));
    }

    private static CompiledComposition CreateV2Artifact(
        CompiledProfilePromotion promotion,
        string outputTemplate,
        IReadOnlyList<string> requiredTokenIds,
        bool runtimeExecutable,
        bool allowOutputOverride = false,
        CompositionKind compositionKind = CompositionKind.Merge,
        IcNumberInputMode? icNumberInputMode = null,
        string modeId = "standard",
        string experienceId = "standard-merge",
        CompiledInputContract? inputContract = null,
        CompositionPlan? plan = null,
        TopologyRequirement? topologyRequirement = null,
        TopologySelection? requestedTopology = null)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            modeId,
            topologyRequirement: topologyRequirement,
            requestedTopology: requestedTopology);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            new ResolvedMapV2CompilationContext(resolvedMap),
            promotion,
            ["profile-evidence"],
            [],
            []);
        var output = new CompiledOutputNamingRequirement(
            outputTemplate,
            allowOverride: allowOutputOverride,
            CompiledOutputInvalidCharacterPolicy.Reject,
            requiredTokenIds);
        var details = new V2CompiledCompositionDetails(
            "profile-v2",
            "2.0.0",
            experienceId,
            compositionKind,
            provenance,
            inputContract ?? CreateInputContract(),
            new CompiledRegionAccessContract([], []),
            output,
            icNumberInputMode);
        plan ??= new CompositionPlan(
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
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, details)
            : CompiledComposition.CreateV2(plan, details);
    }

    private static CompiledInputContract CreateInputContract()
    {
        return new CompiledInputContract(
            [CompiledInputSlotTestFactory.Create(
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

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap(
        string modeId = "standard",
        FirmwareWriteConstraint rootWriteConstraint = FirmwareWriteConstraint.Forbidden,
        TopologyRequirement? topologyRequirement = null,
        TopologySelection? requestedTopology = null,
        FirmwareRegionOwner rootOwner = FirmwareRegionOwner.System,
        FirmwareRegionKind rootKind = FirmwareRegionKind.Image,
        bool includeNestedCtrlRamRegion = false)
    {
        FirmwareRegion[] regions = includeNestedCtrlRamRegion
            ?
            [
                new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, 4),
                    rootWriteConstraint),
                new FirmwareRegion(
                    "prefix",
                    "root",
                    FirmwareRegionOwner.Unknown,
                    FirmwareRegionKind.Unmapped,
                    new ByteRange(0, 1),
                    FirmwareWriteConstraint.Forbidden),
                new FirmwareRegion(
                    "ctrlram",
                    "root",
                    FirmwareRegionOwner.Tp,
                    FirmwareRegionKind.CtrlRam,
                    new ByteRange(1, 2),
                    FirmwareWriteConstraint.ExplicitRange),
                new FirmwareRegion(
                    "suffix",
                    "root",
                    FirmwareRegionOwner.Unknown,
                    FirmwareRegionKind.Unmapped,
                    new ByteRange(3, 1),
                    FirmwareWriteConstraint.Forbidden),
            ]
            :
            [
                new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    rootOwner,
                    rootKind,
                    new ByteRange(0, 4),
                    rootWriteConstraint),
            ];
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                [modeId],
                topologyRequirement ?? TopologyRequirement.NoTopologyConstraint(),
                4),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                regions,
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
            modeId,
            4,
            requestedTopology,
            []));

        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(result.ResolvedMap);
    }

    private sealed class RecordingOutputWriter : ICompositionOutputWriter
    {
        internal bool WasCalled { get; private set; }

        internal string? FileName { get; private set; }

        public ValueTask<CompositionOutputCommitReceipt> CommitAsync(
            string fileName,
            ReadOnlyMemory<byte> outputBytes,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            FileName = fileName;
            return ValueTask.FromResult(CompositionOutputCommitReceipt.CreateLoose(
                $"committed:{fileName}", fileName, outputBytes.Span));
        }
    }
}
