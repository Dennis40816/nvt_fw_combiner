using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies V2 details retain the complete input slot policy instead of only plan-space geometry.</summary>
    [Fact]
    public void V2PlanArtifactRetainsCompleteInputSlotContract()
    {
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(CreateV2().V2Details);

        CompiledInputSlotRequirement slot = Assert.Single(details.InputContract.Slots);
        Assert.Equal("input-slot", slot.SlotId);
        Assert.Equal("input", slot.Role);
        Assert.Equal(CompiledInputArtifactClass.ReferenceImage, slot.ArtifactClass);
        Assert.True(slot.Required);
        Assert.Equal(CompiledInputSlotCardinality.ExactlyOne, slot.Cardinality);
        Assert.Equal([".bin"], slot.AcceptedExtensions);
        Assert.Equal(4, Assert.IsType<CompiledExactResolvedMapCapacityInputLengthRequirement>(slot.LengthRequirement).Bytes);
        _ = Assert.IsType<CompiledNoInputNormalization>(slot.Normalization);
        CompiledInputSpaceBinding binding = Assert.Single(details.InputContract.SpaceBindings);
        Assert.Equal("input", binding.AddressSpaceId);
        Assert.Equal("input-slot", binding.SlotId);
        Assert.Equal(CompiledInputInstancePolicy.Singleton, binding.InstancePolicy);
    }

    /// <summary>Verifies input contract snapshots canonical declaration order and rejects ambiguous slot-to-space ownership.</summary>
    [Fact]
    public void V2InputContractCanonicalizesAndRejectsAmbiguousBindings()
    {
        CompiledInputSlotRequirement[] slots =
        [
            Slot("z-slot", "z"),
            Slot("a-slot", "a"),
        ];
        CompiledInputSpaceBinding[] bindings =
        [
            new CompiledInputSpaceBinding("z-space", "z-slot", CompiledInputInstancePolicy.Singleton),
            new CompiledInputSpaceBinding("a-space", "a-slot", CompiledInputInstancePolicy.Singleton),
        ];
        var contract = new CompiledInputContract(slots, bindings);
        slots[0] = Slot("changed", "changed");
        bindings[0] = new CompiledInputSpaceBinding("changed-space", "a-slot", CompiledInputInstancePolicy.Singleton);

        Assert.Equal(["a-slot", "z-slot"], contract.Slots.Select(static slot => slot.SlotId));
        Assert.Equal(["a-space", "z-space"], contract.SpaceBindings.Select(static binding => binding.AddressSpaceId));
        _ = Assert.Throws<ArgumentException>(() => new CompiledInputContract(
            [Slot("input-slot", "input"), Slot("input-slot", "duplicate")],
            [new CompiledInputSpaceBinding("input", "input-slot", CompiledInputInstancePolicy.Singleton)]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledInputContract(
            [Slot("input-slot", "input")],
            [new CompiledInputSpaceBinding("input", "unknown", CompiledInputInstancePolicy.Singleton)]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledInputSlotRequirement(
            "input-slot",
            "input",
            CompiledInputArtifactClass.ReferenceImage,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            ["bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
            new CompiledNoInputNormalization()));
    }

    /// <summary>Verifies the current V2 plan subset rejects a contract that no longer matches immutable plan input geometry.</summary>
    [Fact]
    public void V2PlanArtifactRejectsInputContractThatDoesNotOwnItsImmutablePlanSpace()
    {
        var contract = new CompiledInputContract(
            [Slot("input-slot", "input")],
            [new CompiledInputSpaceBinding("missing", "input-slot", CompiledInputInstancePolicy.Singleton)]);

        _ = Assert.Throws<ArgumentException>(() => CreateV2(inputContract: contract));
    }

    /// <summary>Verifies every closed input length and normalization shape retains its typed payload.</summary>
    [Fact]
    public void V2InputSlotRequirementsRetainClosedLengthAndNormalizationPayloads()
    {
        CompiledInputSlotRequirement exact = new(
            "aux-exact", "aux", CompiledInputArtifactClass.Auxiliary, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledExactBytesInputLengthRequirement(12), new CompiledNoInputNormalization());
        CompiledInputSlotRequirement bounded = new(
            "aux-bounded", "aux", CompiledInputArtifactClass.Auxiliary, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledBoundedInputLengthRequirement(1, 12), new CompiledNoInputNormalization());
        CompiledInputSlotRequirement normalDp = new(
            "dp", "dp", CompiledInputArtifactClass.DpFirmware, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledNormalDpExtractWithWarningInputLengthRequirement("DP_SIZE"), new CompiledNoInputNormalization());
        CompiledInputSlotRequirement tp = new(
            "tp", "tp", CompiledInputArtifactClass.TpFirmware, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledTpMaximum256KInputLengthRequirement(), new CompiledNoInputNormalization());
        CompiledInputSlotRequirement padded = new(
            "dp-padded", "dp", CompiledInputArtifactClass.DpFirmware, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledExactResolvedMapCapacityInputLengthRequirement(16),
            new CompiledPadShorterInputNormalization(0xFF, "pad-evidence"));
        CompiledInputSlotRequirement truncated = new(
            "ctrlram", "ctrlram", CompiledInputArtifactClass.CtrlRamReplacement, true,
            CompiledInputSlotCardinality.ExactlyOne, [".bin"], new CompiledExactBytesInputLengthRequirement(16),
            new CompiledTruncateCtrlRamInputNormalization("CTRLRAM_TRUNCATED", "ctrlram-evidence"));

        Assert.Equal(12, Assert.IsType<CompiledExactBytesInputLengthRequirement>(exact.LengthRequirement).Bytes);
        Assert.Equal((1L, 12L), (
            Assert.IsType<CompiledBoundedInputLengthRequirement>(bounded.LengthRequirement).MinimumBytes,
            Assert.IsType<CompiledBoundedInputLengthRequirement>(bounded.LengthRequirement).MaximumBytes));
        Assert.Equal("DP_SIZE", Assert.IsType<CompiledNormalDpExtractWithWarningInputLengthRequirement>(normalDp.LengthRequirement).IssueCode);
        _ = Assert.IsType<CompiledTpMaximum256KInputLengthRequirement>(tp.LengthRequirement);
        Assert.Equal(262144, CompiledTpMaximum256KInputLengthRequirement.MaximumBytes);
        Assert.Equal((byte)0xFF, Assert.IsType<CompiledPadShorterInputNormalization>(padded.Normalization).FillByte);
        Assert.Equal(
            "CTRLRAM_TRUNCATED",
            Assert.IsType<CompiledTruncateCtrlRamInputNormalization>(truncated.Normalization).WarningIssueCode);
        _ = Assert.Throws<ArgumentException>(() => new CompiledInputSlotRequirement(
            "bad-tp", "tp", CompiledInputArtifactClass.TpFirmware, true, CompiledInputSlotCardinality.ExactlyOne,
            [".bin"], new CompiledExactBytesInputLengthRequirement(16), new CompiledNoInputNormalization()));
        _ = Assert.Throws<ArgumentException>(() => new CompiledInputSlotRequirement(
            "bad-pad", "reference", CompiledInputArtifactClass.ReferenceImage, true,
            CompiledInputSlotCardinality.ExactlyOne, [".bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(16),
            new CompiledPadShorterInputNormalization(0xFF, "evidence")));
    }

    /// <summary>Verifies complete typed input and capability admission policy participates in V2 compilation identity.</summary>
    [Fact]
    public void V2FingerprintBindsInputAndCapabilityAdmissionPolicy()
    {
        CompiledComposition baseline = CreateV2(requiredCapabilities: [DirectCapabilityAdmission("direct reason")]);
        CompiledComposition roleVariant = CreateV2(
            requiredCapabilities: [DirectCapabilityAdmission("direct reason")],
            inputContract: InputContract("different-role"));
        CompiledComposition capabilityVariant = CreateV2(requiredCapabilities: [DirectCapabilityAdmission("different reason")]);
        CompiledComposition aliasVariant = CreateV2(requiredCapabilities: [AliasedCapabilityAdmission()]);

        Assert.NotEqual(baseline.CompilationFingerprint, roleVariant.CompilationFingerprint);
        Assert.NotEqual(baseline.CompilationFingerprint, capabilityVariant.CompilationFingerprint);
        Assert.NotEqual(baseline.CompilationFingerprint, aliasVariant.CompilationFingerprint);
        CompiledCapabilityAdmission aliased = Assert.Single(aliasVariant.V2Details!.Provenance.RequiredCapabilities);
        Assert.Equal("source-member", aliased.Binding.DirectSourceKey.MemberId);
        Assert.Equal(["capability-alias"], aliased.Binding.Provenance.AliasChain.Select(static alias => alias.AliasId));
    }

    /// <summary>Verifies capability admission cannot be forged with a mismatched id, state, or resolved-map target.</summary>
    [Fact]
    public void V2CapabilityAdmissionFailsClosedForInvalidEvidence()
    {
        CompiledCapabilityAdmission direct = DirectCapabilityAdmission("direct reason");
        _ = Assert.Throws<ArgumentException>(() => new CompiledCapabilityAdmission("different", direct.Binding));

        FirmwareMapFactBinding<FirmwareCapabilityFact> wrongTarget = Binding(
            "different-member",
            "map",
            "capability-fact",
            new FirmwareCapabilityFact(
                "capability-fact",
                "ab-code",
                FirmwareCapabilityState.ConfirmedPresent,
                "reason",
                ["evidence"]));
        _ = Assert.Throws<ArgumentException>(() => CreateV2(
            requiredCapabilities: [new CompiledCapabilityAdmission("ab-code", wrongTarget)]));
        FirmwareMapFactBinding<FirmwareCapabilityFact> absent = Binding(
            "NT-SYNTHETIC",
            "map",
            "capability-fact",
            new FirmwareCapabilityFact(
                "capability-fact",
                "ab-code",
                FirmwareCapabilityState.ConfirmedAbsent,
                "reason",
                ["evidence"]));
        _ = Assert.Throws<ArgumentException>(() => new CompiledCapabilityAdmission("ab-code", absent));
    }

    private static CompiledInputSlotRequirement Slot(string slotId, string role)
    {
        return new CompiledInputSlotRequirement(
            slotId,
            role,
            CompiledInputArtifactClass.ReferenceImage,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(4),
            new CompiledNoInputNormalization());
    }

    private static CompiledInputContract InputContract(string role)
    {
        return new CompiledInputContract(
            [Slot("input-slot", role)],
            [new CompiledInputSpaceBinding("input", "input-slot", CompiledInputInstancePolicy.Singleton)]);
    }

    private static CompiledCapabilityAdmission DirectCapabilityAdmission(string reason)
    {
        var capability = new FirmwareCapabilityFact(
            "capability-fact",
            "ab-code",
            FirmwareCapabilityState.ConfirmedPresent,
            reason,
            ["capability-evidence"]);
        return new CompiledCapabilityAdmission(
            "ab-code",
            Binding("NT-SYNTHETIC", "map", "capability-fact", capability));
    }

    private static CompiledCapabilityAdmission AliasedCapabilityAdmission()
    {
        var target = new FirmwareMapFactKey("NT-SYNTHETIC", "map", FirmwareFactKind.Capability, "target-fact");
        var source = new FirmwareMapFactKey("source-member", "source-map", FirmwareFactKind.Capability, "source-fact");
        var applicability = new FirmwareFactApplicability(
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            4);
        var capability = new FirmwareCapabilityFact(
            "source-fact",
            "ab-code",
            FirmwareCapabilityState.ConfirmedPresent,
            "source reason",
            ["source-capability-evidence"]);
        var alias = new FirmwareFactAliasHop(
            "capability-alias",
            target,
            source,
            applicability,
            "target inherits source",
            ["alias-evidence"]);
        return new CompiledCapabilityAdmission(
            "ab-code",
            new FirmwareMapFactBinding<FirmwareCapabilityFact>(
                target,
                source,
                "source-fact",
                capability,
                applicability,
                new FirmwareFactProvenance(target, source, [alias], capability.EvidenceRefs)));
    }

    private static FirmwareMapFactBinding<FirmwareCapabilityFact> Binding(
        string memberId,
        string mapId,
        string capabilityFactId,
        FirmwareCapabilityFact capability)
    {
        var key = new FirmwareMapFactKey(memberId, mapId, FirmwareFactKind.Capability, capabilityFactId);
        var applicability = new FirmwareFactApplicability(
            ["standard"],
            TopologyRequirement.NoTopologyConstraint(),
            4);
        return new FirmwareMapFactBinding<FirmwareCapabilityFact>(
            key,
            key,
            capabilityFactId,
            capability,
            applicability,
            new FirmwareFactProvenance(key, key, [], capability.EvidenceRefs));
    }
}
