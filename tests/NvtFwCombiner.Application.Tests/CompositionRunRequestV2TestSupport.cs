using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

internal static class CompositionRunRequestV2TestSupport
{
    internal static readonly DateTimeOffset FirstTimestamp = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset SecondTimestamp = FirstTimestamp.AddSeconds(1);

    internal static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap CreateResolvedMap(
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

    internal sealed class RecordingOutputWriter : ICompositionOutputWriter
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

    internal static CompiledComposition CreateRuntimeReferenceCandidate(
        bool allowsConditionalProcessor = false,
        bool includeProcessorView = false,
        string modeId = ExperienceIds.CtrlRamReplace,
        string experienceId = ExperienceIds.CtrlRamReplace,
        CompiledInputArtifactClass sourceArtifactClass = CompiledInputArtifactClass.CtrlRamReplacement,
        FirmwareRegionOwner rootOwner = FirmwareRegionOwner.System,
        FirmwareRegionKind rootKind = FirmwareRegionKind.Image,
        string? processorId = null,
        string processorToolBindingId = "synthetic-postbuild-tool",
        ByteRange? processorWriteRange = null,
        string? processorWriteSectionId = null,
        IReadOnlyList<ByteRange>? processorAdditionalWriteRanges = null,
        bool useNestedCtrlRamMap = false,
        TopologyRequirement? topologyRequirement = null,
        TopologySelection? requestedTopology = null,
        bool runtimeExecutable = false)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap = CreateResolvedMap(
            modeId,
            FirmwareWriteConstraint.ExplicitRange,
            topologyRequirement,
            requestedTopology,
            rootOwner: rootOwner,
            rootKind: rootKind,
            includeNestedCtrlRamRegion: useNestedCtrlRamMap);
        FirmwareRegion rootRegion = resolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "root");
        FirmwareRegion[] rootChain =
        [
            rootRegion,
        ];
        FirmwareRegion[] writeChain = useNestedCtrlRamMap
            ?
            [
                rootRegion,
                resolvedMap.ImageMap.Regions.Single(static region => region.RegionId == "ctrlram"),
            ]
            : rootChain;
        string writeRegionId = useNestedCtrlRamMap ? "ctrlram" : "root";
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "runtime-reference-profile",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            new RuntimeReferenceReplaceV2CompilationContext(
                resolvedMap,
                allowsConditionalProcessor || processorId is not null,
                processorId is null ? [] : ["processor-write"]),
            new CompiledProfilePromotion(
                runtimeExecutable
                    ? CompiledProfilePromotionStage.Supported
                    : CompiledProfilePromotionStage.ExecutableCandidate,
                []),
            ["runtime-reference-evidence"],
            [],
            []);
        CompiledInputSlotRequirement referenceSlot = CompiledInputSlotTestFactory.Create(
            "reference-slot",
            "reference",
            CompiledInputArtifactClass.ReferenceImage,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
            new CompiledNoInputNormalization());
        CompiledInputSlotRequirement sourceSlot = CompiledInputSlotTestFactory.Create(
            "source-slot",
            "source",
            sourceArtifactClass,
            required: true,
            CompiledInputSlotCardinality.OneOrMore,
            [".bin"],
            new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
            sourceArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement
                ? new CompiledTruncateCtrlRamInputNormalization(
                    "SYNTHETIC_CTRLRAM_TRUNCATED",
                    "synthetic-ctrlram-normalization")
                : new CompiledNoInputNormalization());
        var referenceBinding = new CompiledInputSpaceBinding(
            "reference",
            "reference-slot",
            CompiledInputInstancePolicy.Singleton);
        var sourceBinding = new CompiledInputSpaceBinding(
            "source-a",
            "source-slot",
            CompiledInputInstancePolicy.PerBinding);
        var inputContract = new CompiledInputContract(
            [referenceSlot, sourceSlot],
            [referenceBinding, sourceBinding]);
        var regionAccess = new CompiledRegionAccessContract(
            [
                new CompiledRegionAccessRequirement(
                    writeRegionId,
                    RegionAccessKind.ExplicitRange,
                    "Synthetic runtime reference-replace target.",
                    [],
                    writeChain),
            ],
            includeProcessorView || processorId is not null
                ?
                [
                    new CompiledResolvedPhysicalView(
                        "processor-image",
                        "output-image",
                        new ByteRange(0, 4),
                        rootChain),
                    new CompiledResolvedPhysicalView(
                        "processor-write",
                        "output-image",
                        new ByteRange(1, 2),
                        writeChain),
                ]
                : []);
        var details = new V2CompiledCompositionDetails(
            "runtime-reference-profile",
            "1.0.0",
            experienceId,
            CompositionKind.Replace,
            provenance,
            inputContract,
            regionAccess,
            new CompiledOutputNamingRequirement(
                "runtime-reference.bin",
                allowOverride: false,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []),
            IcNumberInputMode.SingleSelector);
        AddressSpace[] spaces =
        [
            new AddressSpace("reference", 4, AddressSpaceMutability.Immutable),
            new AddressSpace(
                "source-a",
                2,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy:
                    sourceArtifactClass == CompiledInputArtifactClass.CtrlRamReplacement
                        ? InputOversizePolicy.TruncateWithWarning
                        : InputOversizePolicy.Reject),
            new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        ByteRange effectiveMappingRange =
            processorWriteRange ?? new ByteRange(1, 2);
        CompositionOperation[] mappingOperations =
        [
            CompositionOperation.ReplaceRange(
                "replace-source",
                10,
                "source-a",
                new ByteRange(0, effectiveMappingRange.Length),
                "output-image",
                effectiveMappingRange,
                OverlapPolicy.Reject,
                "Replace synthetic runtime source."),
        ];
        CompositionOperation[] processorOperations = processorId is null
            ? []
            :
            [
                CompositionOperation.RunExternalProcessor(
                    "run-postbuild",
                    int.MaxValue,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        processorId,
                        processorToolBindingId,
                        [new ByteRange(0, 4)],
                        [
                            processorWriteRange ?? new ByteRange(1, 2),
                            .. processorAdditionalWriteRanges ?? [],
                        ],
                        allowedWriteRangeSections:
                            CreateSyntheticPostbuildWriteSections(
                                processorWriteRange ?? new ByteRange(1, 2),
                                processorWriteSectionId)),
                    OverlapPolicy.ReplaceExisting,
                    "Run the synthetic postbuild processor."),
            ];
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference", 4),
            spaces,
            [.. mappingOperations, .. processorOperations]);
        return runtimeExecutable
            ? CompiledComposition.CreateV2RuntimeExecutable(plan, details)
            : CompiledComposition.CreateV2(plan, details);

        static IReadOnlyList<ExternalProcessorWriteRangeSection>
            CreateSyntheticPostbuildWriteSections(
                ByteRange allowedWriteRange,
                string? sectionId)
        {
            LegacyCombinerPostbuildCommandPlan plan =
                CreateSyntheticPostbuildPlan();
            ByteRange[] stagedTargetRanges =
            [
                .. LegacyCombinerPostbuildPlanCompiler.GetStagedFileBlocks(plan)
                    .Select(static block => block.FirmwareRange),
            ];
            return
            [
                .. LegacyCombinerPostbuildPlanCompiler
                    .GetAllowedWriteRangeSectionsForStagedSources(
                        plan,
                        4,
                        stagedTargetRanges,
                        stagedTargetRanges)
                    .Where(section =>
                        allowedWriteRange.Contains(section.Range))
                    .Select(section => sectionId is null
                        ? section
                        : new ExternalProcessorWriteRangeSection(
                            sectionId,
                            section.Range,
                            section.SourceRange)),
            ];
        }
    }

    internal static LegacyCombinerPostbuildCommandPlan
        CreateSyntheticPostbuildPlan()
    {
        var block = new LegacyCombinerBlockArgument(
            "synthetic-block",
            LegacyCombinerBlockSourceKind.StagedFile,
            "source.bin",
            0,
            new ByteRange(1, 2));
        var command = new LegacyCombinerPostbuildCommand(
            "synthetic-command",
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Disable",
            crcArgument: null,
            [block]);
        var profile = new LegacyCombinerPostbuildProfile(
            "nfc.synthetic.postbuild",
            "NT-SYNTHETIC",
            "synthetic-postbuild-tool",
            "firmware.bin",
            [command],
            [command],
            "synthetic-evidence");
        LegacyCombinerPostbuildPlanSelector selector =
            profile.PlanSelectors.Single(static candidate =>
                candidate.Kind ==
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip);
        return profile.ResolvePlan(selector);
    }
}
