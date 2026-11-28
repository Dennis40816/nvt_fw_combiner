// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Tests.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Application.Tests.MemoryLayout.MemoryLayoutProjectorTestSupport;

namespace NvtFwCombiner.Bootstrap.Tests.MemoryLayout;

/// <summary>Real compiler routes publish output writes without executing firmware or changing Golden expectations.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutOutputWriteBuiltInTests
{
    /// <summary>The deployed Standard Merge route keeps exactly its direct copy coordinates and sources.</summary>
    [Fact]
    public void BuiltInStandardMergePublishesExactDirectCopyWrites()
    {
        ProjectionFixture fixture = Fixture(BuiltInCompositionTestProfiles.StandardMerge());

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, Session(fixture), fixture.Composition);

        Assert.Equal(fixture.Composition.Plan.OrderedOperations.Select(static step =>
                (step.OperationId, step.TargetRange, step.SourceSpaceId, step.SourceRange)),
            layout.OutputWrites.Values.Select(static write =>
                (write.OperationId, write.OutputRange.Range, write.SourceSlotId, write.SourceRange)));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>
    /// TPB's cloned input source survives relocation except for the relocated bytes, which are scratch scalar
    /// effects with their own output locations.
    /// </summary>
    [Theory]
    [InlineData("NT51929", "nt51929-ab-merge-512k", 0x80000, 0, "copy-tpb")]
    [InlineData("NT51950", "nt51950-ab-merge-maps", 0x80000, 1, "overlay-tpb-into-output")]
    [InlineData("NT51951", "nt51951-ab-merge-1024k", 0x100000, 0, "overlay-tpb-into-output")]
    public void BuiltInAbMergePublishesSelectedTpbAndRelocationOutputCoordinates(
        string icId, string mapVariantSetId, long capacity, int topologyCount, string copyId)
    {
        ProjectionFixture fixture = Fixture(BuiltInCompositionTestProfiles.AbMerge(icId, mapVariantSetId, capacity, topologyCount));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, Session(fixture), fixture.Composition);

        CompositionOperation copy = fixture.Composition.Plan.OrderedOperations.First(step => step.OperationId == copyId);
        CompositionOperation[] relocations = [.. fixture.Composition.Plan.OrderedOperations.Where(static step =>
            step.Kind == CompositionOperationKind.TransformScalar)];
        Assert.NotEmpty(relocations);
        ByteRange[] relocated = [.. relocations.Select(step => new ByteRange(
            checked(copy.TargetRange.Start + step.TargetRange.Start - copy.SourceRange!.Value.Start), step.TargetRange.Length))];
        MemoryLayoutOutputWrite[] copied = [.. layout.OutputWrites.Values.Where(write => write.OperationId == copyId)];
        Assert.Equal(copy.TargetRange.Subtract(relocated), copied.Select(static write => write.OutputRange.Range));
        Assert.All(copied, write =>
        {
            Assert.Equal("tp-b-input", write.SourceSlotId);
            Assert.Equal(new ByteRange(checked(copy.SourceRange!.Value.Start + write.OutputRange.Range.Start - copy.TargetRange.Start),
                write.OutputRange.Range.Length), write.SourceRange);
        });
        Assert.All(relocations, step =>
        {
            MemoryLayoutOutputWrite write = Assert.Single(layout.OutputWrites.Values, write => write.OperationId == step.OperationId);
            Assert.Equal(new ByteRange(checked(copy.TargetRange.Start + step.TargetRange.Start - copy.SourceRange!.Value.Start),
                step.TargetRange.Length), write.OutputRange.Range);
            Assert.Null(write.SourceSlotId);
            Assert.Null(write.SourceRange);
        });
        Assert.Equal(fixture.Composition.Plan.OrderedOperations, layout.Steps.Values);
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>The A/B processor's three permitted header writes are located through imports, not its entire staging view.</summary>
    [Fact]
    public void BuiltInAbProcessorPublishesOnlyTheThreeDeclaredOutputHeaderRanges()
    {
        ProjectionFixture fixture = Fixture(BuiltInCompositionTestProfiles.AbMerge("NT51950", "nt51950-ab-merge-maps", 0x80000, 1));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, Session(fixture), fixture.Composition);

        CompositionOperation processor = Assert.Single(fixture.Composition.Plan.OrderedOperations, static step =>
            step.Kind == CompositionOperationKind.RunExternalProcessor);
        MemoryLayoutOutputWrite[] writes = [.. layout.OutputWrites.Values.Where(write => write.OperationId == processor.OperationId)];
        Assert.Equal([new ByteRange(0x4A100, 4), new ByteRange(0x4A110, 4), new ByteRange(0x4A130, 4)],
            writes.Select(static write => write.OutputRange.Range));
        Assert.All(writes, static write =>
        {
            Assert.Null(write.SourceSlotId);
            Assert.Null(write.SourceRange);
        });
        Assert.DoesNotContain(layout.OutputWrites.Values, static write =>
            write.OperationId.StartsWith("copy-a-bank-to-", StringComparison.Ordinal) ||
            write.OperationId.StartsWith("copy-b-bank-to-", StringComparison.Ordinal) ||
            write.OperationId.StartsWith("import-postbuild-", StringComparison.Ordinal));
        AssertOutputWriteInvariants(layout, fixture.Composition.Plan);
    }

    /// <summary>Selected banks publish only local content effects; seed and publish remain complete in Steps.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public void BankCtrlRamReplacePublishesSelectedInputAndDeclaredProcessorEffects(AbCtrlRamBankSelection selection)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, new AbCtrlRamDraftState(selection));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot session = prepared.AcceptedSession!;
        CompiledComposition composition = session.ExactCapability!.CompiledComposition;
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            composition.V2Details.Provenance.Context);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(session.ExactCapability, session, composition);

        Assert.All(context.Banks, bank =>
        {
            MemoryLayoutOutputWrite input = Assert.Single(layout.OutputWrites.Values, write =>
                write.BankId == bank.BankId && write.SourceSlotId == "replace-ctrlram-nf");
            Assert.Equal(new ByteRange(checked(bank.OutputRange.Start + 0x1FC00), 1), input.OutputRange.Range);
            Assert.Equal(new ByteRange(0, 1), input.SourceRange);
            CompositionOperation processor = Assert.Single(composition.Plan.OrderedOperations, step =>
                step.TargetSpaceId == bank.WorkspaceId && step.Kind == CompositionOperationKind.RunExternalProcessor);
            Assert.Equal(processor.DeclaredWriteRanges.Select(range => new ByteRange(checked(bank.OutputRange.Start + range.Start), range.Length)),
                layout.OutputWrites.Values.Where(write => write.OperationId == processor.OperationId).Select(static write => write.OutputRange.Range));
            Assert.All(layout.OutputWrites.Values.Where(write => write.OperationId == processor.OperationId), static write =>
            {
                Assert.Null(write.SourceSlotId);
                Assert.Null(write.SourceRange);
            });
            Assert.Contains(layout.Steps.Values, step => step.OperationId == $"{bank.BankId}/seed");
            Assert.Contains(layout.Steps.Values, step => step.OperationId == $"{bank.BankId}/publish");
        });
        Assert.All(layout.OutputWrites.Values, write => Assert.Contains(context.Banks, bank => bank.BankId == write.BankId));
        Assert.DoesNotContain(layout.OutputWrites.Values, static write =>
            write.OperationId.EndsWith("/seed", StringComparison.Ordinal) || write.OperationId.EndsWith("/publish", StringComparison.Ordinal));
        Assert.Equal(composition.Plan.OrderedOperations, layout.Steps.Values);
        AssertOutputWriteInvariants(layout, composition.Plan);
    }

    private static ProjectionFixture Fixture((CompiledComposition Composition, MetadataPlanDefinition MetadataPlan) profile)
    {
        MapBoundV2CompilationContext context = Assert.IsType<MapBoundV2CompilationContext>(profile.Composition.V2Details.Provenance.Context, exactMatch: false);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolved = context.ResolvedMap;
        CapabilityRouteIdentity route = new(resolved.MemberId, profile.Composition.V2Details.ExperienceId,
            profile.Composition.V2Details.IcNumberInputMode is null ? "selector-free" : "1-ic", resolved.ImageMap.MapId);
        ResolvedCapability capability = Capability(route, profile.Composition, profile.MetadataPlan);
        return new ProjectionFixture(route, capability, capability.CompiledComposition, resolved, resolved.ImageMap.Regions[0], resolved.ImageMap.Regions[^1]);
    }

    private static ActiveSessionSnapshot Session(ProjectionFixture fixture)
    {
        return CreateSession(fixture, [.. fixture.Composition.V2Details.InputContract.SpaceBindings.Select(binding =>
            Slot(binding.AddressSpaceId, AuthoringSlotLifecycle.Verified,
                fixture.Composition.Plan.AddressSpaces.First(space => space.AddressSpaceId == binding.AddressSpaceId).Length))]);
    }
}
