using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

internal sealed record TestCompiledCompositionIdentity(
    string ProfileId,
    string ProfileVersion,
    string IcId,
    string ModeId,
    string ExperienceId,
    CompositionKind CompositionKind);

/// <summary>Creates fully V2-owned synthetic artifacts for Application behavior tests.</summary>
internal static class CompiledCompositionTestFactory
{
    private const string SyntheticSha256 =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    internal static CompiledComposition Create(
        CompositionPlan plan,
        TestCompiledCompositionIdentity identity,
        string defaultOutputFileName,
        IcNumberInputMode? icNumberInputMode = null,
        IReadOnlyList<CompiledValidationRequirement>? validationRequirements = null,
        string mapId = "application-test-map",
        bool allowOutputOverride = false,
        IReadOnlyDictionary<string, string>? inputRolesByAddressSpace = null,
        IReadOnlyList<string>? outputRequiredTokenIds = null,
        CompiledInputLengthRequirement? inputLengthRequirement = null,
        CompiledInputArtifactClass? nonReferenceArtifactClass = null,
        long? nvtEndFlagStart = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(identity);
        long capacity = plan.OutputInitialization.Capacity;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            CreateResolvedMap(identity, capacity, mapId, nvtEndFlagStart);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "application-test-bundle",
                "1.0.0",
                SyntheticSha256,
                "application-test-trust"),
            new ProfileBundleEntryIdentity(
                "application-test-profile",
                SyntheticSha256),
            new ResolvedMapV2CompilationContext(resolvedMap),
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            ["application-test-evidence"],
            validationRequirements ?? [],
            []);
        var details = new V2CompiledCompositionDetails(
            identity.ProfileId,
            identity.ProfileVersion,
            identity.ExperienceId,
            identity.CompositionKind,
            provenance,
            CreateInputContract(plan, identity, inputRolesByAddressSpace, inputLengthRequirement,
                nonReferenceArtifactClass),
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                defaultOutputFileName,
                allowOverride: allowOutputOverride,
                CompiledOutputInvalidCharacterPolicy.Reject,
                outputRequiredTokenIds ?? []),
            icNumberInputMode);
        return CompiledComposition.CreateV2RuntimeExecutable(plan, details);
    }

    private static CompiledInputContract CreateInputContract(
        CompositionPlan plan,
        TestCompiledCompositionIdentity identity,
        IReadOnlyDictionary<string, string>? inputRolesByAddressSpace,
        CompiledInputLengthRequirement? inputLengthRequirement,
        CompiledInputArtifactClass? nonReferenceArtifactClass)
    {
        var slots = new List<CompiledInputSlotRequirement>();
        var bindings = new List<CompiledInputSpaceBinding>();
        foreach (AddressSpace space in plan.AddressSpaces.Where(static space =>
                     space.Mutability == AddressSpaceMutability.Immutable))
        {
            string slotId = $"{space.AddressSpaceId}-slot";
            bool isReference = plan.Initializations.Any(initialization =>
                initialization.Kind == ImageInitializationKind.Reference &&
                StringComparer.Ordinal.Equals(
                    initialization.ReferenceSpaceId,
                    space.AddressSpaceId));
            (CompiledInputArtifactClass artifactClass,
                CompiledInputLengthRequirement lengthRequirement,
                CompiledInputNormalization normalization) = CreateInputPolicy(
                    space,
                    identity,
                    isReference,
                     inputLengthRequirement,
                     nonReferenceArtifactClass);
            slots.Add(CompiledInputSlotTestFactory.Create(
                slotId,
                inputRolesByAddressSpace?.GetValueOrDefault(space.AddressSpaceId) ??
                    space.AddressSpaceId,
                artifactClass,
                required: true,
                CompiledInputSlotCardinality.ExactlyOne,
                [".bin"],
                lengthRequirement,
                normalization));
            bindings.Add(new CompiledInputSpaceBinding(
                space.AddressSpaceId,
                slotId,
                CompiledInputInstancePolicy.Singleton));
        }

        return new CompiledInputContract(slots, bindings);
    }

    private static (CompiledInputArtifactClass ArtifactClass,
        CompiledInputLengthRequirement LengthRequirement,
        CompiledInputNormalization Normalization) CreateInputPolicy(
        AddressSpace space,
        TestCompiledCompositionIdentity identity,
        bool isReference,
        CompiledInputLengthRequirement? inputLengthRequirement,
        CompiledInputArtifactClass? nonReferenceArtifactClass)
    {
        if (isReference)
        {
            return (
                CompiledInputArtifactClass.ReferenceImage,
                new CompiledExactResolvedMapCapacityInputLengthRequirement(space.Length),
                new CompiledNoInputNormalization());
        }

        if (inputLengthRequirement is not null)
        {
            return (
                nonReferenceArtifactClass ?? (inputLengthRequirement is
                    CompiledSourceViewCoverageInputLengthRequirement { MaximumBytes: not null }
                    ? CompiledInputArtifactClass.TpFirmware
                    : CompiledInputArtifactClass.Auxiliary),
                inputLengthRequirement,
                new CompiledNoInputNormalization());
        }

        if (space.InputPaddingByte is byte fillByte)
        {
            return (
                CompiledInputArtifactClass.DpFirmware,
                new CompiledExactResolvedMapCapacityInputLengthRequirement(space.Length),
                new CompiledPadShorterInputNormalization(
                    fillByte,
                    "application-test-evidence"));
        }

        if (space.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning &&
            identity.CompositionKind == CompositionKind.Replace &&
            StringComparer.Ordinal.Equals(identity.ExperienceId, ExperienceIds.CtrlRamReplace))
        {
            return (
                CompiledInputArtifactClass.CtrlRamReplacement,
                new CompiledExactBytesInputLengthRequirement(space.Length),
                new CompiledTruncateCtrlRamInputNormalization(
                    "APPLICATION_TEST_CTRLRAM_TRUNCATED",
                    "application-test-evidence"));
        }

        if (space.InputOversizePolicy == InputOversizePolicy.ExtractDeclaredRange)
        {
            return (
                CompiledInputArtifactClass.Auxiliary,
                new CompiledSourceViewCoverageInputLengthRequirement(
                    space.ExpectedInputLengths.Count == 0
                        ? null
                        : space.ExpectedInputLengths,
                    space.UnexpectedInputLengthIssueCode),
                new CompiledNoInputNormalization());
        }

        return (
            nonReferenceArtifactClass ?? CompiledInputArtifactClass.TpFirmware,
            new CompiledExactBytesInputLengthRequirement(space.Length),
            new CompiledNoInputNormalization());
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap
        CreateResolvedMap(
            TestCompiledCompositionIdentity identity,
            long capacity,
            string mapId,
            long? nvtEndFlagStart)
    {
        // NVT-END-FLAG-1113-01: the synthetic family is outside the migration inventory, so FWConfig Backup reads
        // succeed only at an NVT end flag the map declares; without one they are unreadable.
        FirmwareMetadataSet[] metadataSets = nvtEndFlagStart is long endFlagStart
            ? [new FirmwareMetadataSet(
                "application-test-fwconfig-backup",
                [new FirmwareMetadataStructure(
                    "application-test-fwconfig-backup",
                    "application-test-artifact",
                    0x80,
                    new FirmwareMarkerRelativeLocator(
                        new FirmwareAddressedRange("flash", new ByteRange(endFlagStart, 4)),
                        [0x00, 0x4E, 0x56, 0x54],
                        new FirmwareUniqueMarkerSelection(),
                        FirmwareNvtEndFlag.BackupStartOffsetFromMarker,
                        "root"),
                    [],
                    [])],
                ["application-test-evidence"])]
            : [];
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            mapId,
            "flash",
            new FirmwareMapApplicability(
                [identity.IcId],
                [identity.ModeId],
                TopologyRequirement.NoTopologyConstraint(),
                capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, capacity),
                    FirmwareWriteConstraint.Forbidden)],
                ["application-test-evidence"])],
            metadataSets,
            ["application-test-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "application-test-family",
            "1.0.0",
            SyntheticSha256,
            [map],
            metadataSets);
        var inputs = new FirmwareMapResolutionInputs(
            identity.IcId,
            identity.ModeId,
            capacity,
            requestedTopology: null,
            []);
        // A declaration adds no required structure: the map resolves from its selection alone, as a trusted profile.
        FirmwareMapResolutionResult result = metadataSets.Length == 0
            ? definition.ResolveMap(inputs)
            : definition.ResolveMapWithinForProfile(
                inputs,
                new HashSet<string>(StringComparer.Ordinal) { mapId },
                new HashSet<string>(StringComparer.Ordinal));
        return result.ResolvedMap ?? throw new InvalidOperationException(
            "Synthetic Application map did not resolve.");
    }
}
