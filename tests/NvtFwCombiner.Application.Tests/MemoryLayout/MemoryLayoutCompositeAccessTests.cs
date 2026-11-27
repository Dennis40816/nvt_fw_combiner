// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Layout-only AB authority cannot supply the active composite Replace access policy.</summary>
public sealed class MemoryLayoutCompositeAccessTests
{
    /// <summary>Selected obligations never turn AB Merge access into a complete Replace authoring policy.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BankReplaceKeepsAccessUnavailableAndFullLayoutVersionCarriersForSingleAndBothSelections(int selectedBankCount)
    {
        CompiledComposition layout = Parent(16, runtime: false);
        CompiledComposition local = Parent(8, runtime: true);
        var definition = new BankReferenceReplaceDefinition(layout, local);
        var reference = new FirmwareArtifactPayload("reference-base", new byte[16]);
        var plan = new CompositionPlan(ImageInitialization.Reference("output", "reference-base", 16),
            [new AddressSpace("reference-base", 16, AddressSpaceMutability.Immutable),
             new AddressSpace("output", 16, AddressSpaceMutability.Mutable)], []);
        CompiledReferenceBank[] banks = [.. Enumerable.Range(0, selectedBankCount).Select(index =>
            new CompiledReferenceBank(index == 0 ? "a-bank" : "b-bank", $"bank-{index}-work",
                new ByteRange(index * 8, 8),
                new FirmwareArtifactPayload($"ab-replace/bank-{index}/reference", new byte[8]).Identity, local))];
        var context = new RuntimeReferenceBankReplaceV2CompilationContext(layout, reference.Identity, definition, plan, banks);
        V2CompiledCompositionDetails source = layout.V2Details;
        var provenance = new V2CompilationProvenance(source.Provenance.Bundle, source.Provenance.ProfileEntry,
            context, source.Provenance.Promotion, ["synthetic-evidence"], [], []);
        var composition = CompiledComposition.CreateV2(plan, new V2CompiledCompositionDetails(
            definition.DefinitionId, definition.Version, ExperienceIds.CtrlRamReplace, CompositionKind.Replace,
            provenance, Inputs(16), source.RegionAccessContract, source.OutputNamingRequirement));
        FirmwareImageMap map = context.ResolvedMap.ImageMap;
        Assert.Empty(source.RegionAccessContract.GetReadOnlyRanges(map));

        MemoryLayoutFactSet<MemoryLayoutReadOnlyRange> facts = MemoryLayoutFactProjection.ProjectReadOnlyRanges(
            composition, map, map.AddressSpaceId, map.CapacityBytes);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, facts.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.ActiveAuthoringPolicyNotResolved, facts.UnavailableReason);
        Assert.Empty(facts.Values);
        MemoryLayoutFactSet<MemoryLayoutVersionLocation> versions = MemoryLayoutFactProjection.ProjectDpVersionCarrierLocations(
            composition, map.AddressSpaceId, map.CapacityBytes);
        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, versions.Status);
        Assert.Equal([("a-cmi-dp-version", new ByteRange(1, 3)), ("b-cmi-dp-version", new ByteRange(9, 3))],
            versions.Values.Select(static location => (location.FieldId, location.OutputRange.Range)));
        Assert.Same(plan, composition.Plan);
        Assert.Same(source.RegionAccessContract, composition.V2Details.RegionAccessContract);
        Assert.Equal(selectedBankCount, context.Banks.Count);
    }

    /// <summary>Malformed declarations and a carrier beyond the actual output extent are unavailable.</summary>
    [Theory]
    [InlineData(2, 16)]
    [InlineData(3, 10)]
    public void UnresolvedCarrierShapeOrExtentDoesNotPublishGuessedLocations(int carrierLength, long actualCapacity)
    {
        CompiledComposition layout = Parent(16, runtime: false, carrierLength);

        MemoryLayoutFactSet<MemoryLayoutVersionLocation> facts = MemoryLayoutFactProjection.ProjectDpVersionCarrierLocations(
            layout, "flash", actualCapacity);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, facts.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, facts.UnavailableReason);
        Assert.Empty(facts.Values);
    }

    private static CompiledInputContract Inputs(long capacity)
    {
        return new(
            [CompiledInputSlotTestFactory.Create("reference-base", "reference-base", CompiledInputArtifactClass.ReferenceImage,
                true, CompiledInputSlotCardinality.ExactlyOne, [".bin"],
                new CompiledExactResolvedMapCapacityInputLengthRequirement(capacity), new CompiledNoInputNormalization())],
            [new CompiledInputSpaceBinding("reference-base", "reference-base", CompiledInputInstancePolicy.Singleton)]);
    }

    private static CompiledComposition Parent(long capacity, bool runtime, int carrierLength = 3)
    {
        string mode = runtime ? ExperienceIds.CtrlRamReplace : ExperienceIds.AbMerge;
        FirmwareRegion root = new("root", null, FirmwareRegionOwner.System, FirmwareRegionKind.Image,
            new ByteRange(0, capacity), FirmwareWriteConstraint.ExplicitRange);
        FirmwareRegion[] regions = runtime ? [root] :
        [
            root,
            new("a-bank", "root", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(0, 8), FirmwareWriteConstraint.ExplicitRange),
            new("a-before", "a-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(0, 1), FirmwareWriteConstraint.ExplicitRange),
            new("a-cmi-dp-version", "a-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, new ByteRange(1, carrierLength), FirmwareWriteConstraint.ExplicitRange),
            new("a-after", "a-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(1 + carrierLength, 7 - carrierLength), FirmwareWriteConstraint.ExplicitRange),
            new("b-bank", "root", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(8, 8), FirmwareWriteConstraint.ExplicitRange),
            new("b-before", "b-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(8, 1), FirmwareWriteConstraint.ExplicitRange),
            new("b-cmi-dp-version", "b-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Command, new ByteRange(9, carrierLength), FirmwareWriteConstraint.ExplicitRange),
            new("b-after", "b-bank", FirmwareRegionOwner.Dp, FirmwareRegionKind.Code, new ByteRange(9 + carrierLength, 7 - carrierLength), FirmwareWriteConstraint.ExplicitRange),
        ];
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect($"synthetic-{mode}", "flash",
            new FirmwareMapApplicability(["NT-SYNTHETIC"], [mode], TopologyRequirement.NoTopologyConstraint(), capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet("physical", "flash", regions, ["synthetic-evidence"])], [], ["synthetic-evidence"]);
        var family = new FirmwareFamilyResolutionDefinition("synthetic-family", "1.0.0", new string('c', 64), [map], []);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolved = Assert.IsType<
            FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(family.ResolveMap(
                new FirmwareMapResolutionInputs("NT-SYNTHETIC", mode, capacity, null, [])).ResolvedMap);
        V2CompilationContext context = runtime
            ? new RuntimeReferenceReplaceV2CompilationContext(resolved, allowsConditionalProcessor: false)
            : new ResolvedMapV2CompilationContext(resolved);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity("synthetic-bundle", "1.0.0", new string('a', 64), "test-binding"),
            new ProfileBundleEntryIdentity("synthetic-profile", new string('b', 64)), context,
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []), ["synthetic-evidence"], [], []);
        var details = new V2CompiledCompositionDetails(
            runtime ? "synthetic-ctrlram-replace-local" : "synthetic-ab-layout", "1.0.0", mode,
            runtime ? CompositionKind.Replace : CompositionKind.Merge, provenance, Inputs(capacity),
            new CompiledRegionAccessContract(
                [new CompiledRegionAccessRequirement("root", RegionAccessKind.ExplicitRange, "Synthetic writable layout", [], [root])], []),
            runtime ? new CompiledOutputNamingRequirement("synthetic.bin", true, CompiledOutputInvalidCharacterPolicy.Reject, [])
                : new CompiledOutputNamingRequirement(CompiledOutputNamingRequirement.AbCodeV1Template,
                    true, CompiledOutputInvalidCharacterPolicy.Reject, ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"]));
        return CompiledComposition.CreateV2RuntimeExecutable(new CompositionPlan(
            ImageInitialization.Blank("output", capacity, 0),
            [new AddressSpace("reference-base", capacity, AddressSpaceMutability.Immutable),
             new AddressSpace("output", capacity, AddressSpaceMutability.Mutable)], []), details);
    }
}
