using System.Buffers.Binary;
using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Declared bank-local effects projected into the complete Reference address space.</summary>
public sealed class AbCtrlRamMemoryLayoutTests
{
    /// <summary>Both bank overviews retain DP context even when only one bank is replaced.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public void PartialFamilyOverviewIncludesDpInBothBanks(AbCtrlRamBankSelection selection)
    {
        string expectedDirectory = Path.Combine(CanonicalGoldenTestData.Root, "NT51950", "ab-merge",
            "boe-d82t80", "topology-unscoped", "nt51950-ab-boe-d82t80", "expected");
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(Directory.GetFiles(expectedDirectory, "*.bin").Single());
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51950", "single", paths, bytes, new AbCtrlRamDraftState(selection));
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot session = prepared.AcceptedSession!;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            session.ExactCapability!, session, session.ExactCapability!.CompiledComposition);
        BankReplaceRouteBinding binding = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51950", "1-ic")!;
        CanonicalCapabilityCompilationContract original = CanonicalDynamicRouteInventory.Resolve(binding.Identity).CompilationContract;
        MemoryLayoutContextMap wrongCapacity = CanonicalDynamicRouteInventory.FindBankReplaceBinding("NT51950", "2-ic")!
            .Local.Route.MemoryLayoutContext!;
        MemoryLayoutContextMap accepted = session.ExactCapability!.MemoryLayoutContext!;
        FirmwareImageMap wrongMap = FirmwareImageMapTestFactory.CreateDirect("synthetic-wrong-tp", "flash",
            accepted.Map.Applicability, accepted.Map.CoveragePolicy,
            [new FirmwareRegionSet("synthetic", "flash",
                [new FirmwareRegion("tp", null, FirmwareRegionOwner.Tp, FirmwareRegionKind.Code,
                    new ByteRange(0, accepted.Map.CapacityBytes), FirmwareWriteConstraint.ExplicitRange)], ["synthetic"])],
            [], ["synthetic"]);
        var wrongGeometry = new MemoryLayoutContextMap(accepted.IcId, accepted.ProfileId, accepted.ProfileVersion,
            accepted.TrustedDefinitionSha256, wrongMap);
        foreach (MemoryLayoutContextMap invalidContext in new[] { wrongCapacity, wrongGeometry })
        {
            var invalidContract = new CanonicalCapabilityCompilationContract(original.ProfileId, original.ProfileVersion,
                original.TrustedDefinitionSha256, original.AllowedMapVariantIds, original.CompilerSemanticId,
                [.. original.SemanticBindingIds.Where(static value => !value.StartsWith("memory-layout-context-", StringComparison.Ordinal)),
                    .. invalidContext.SemanticBindingIds]);
            ArgumentException rejected = Assert.Throws<ArgumentException>(() => invalidContract.ValidateCompilation(
                binding.Identity, session.ExactCapability.CompiledComposition, session.ExactCapability.MetadataPlan.Definition,
                session.ExactCapability.RuntimeReferenceProof, invalidContext));
            Assert.Contains("bank capacity", rejected.Message, StringComparison.Ordinal);
        }
        RuntimeReferenceBankReplaceV2CompilationContext execution = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            session.ExactCapability.CompiledComposition.V2Details.Provenance.Context);
        Assert.All(layout.CanonicalRegions, region => Assert.Contains(execution.ResolvedMap.ImageMap.Regions,
            originalRegion => ReferenceEquals(originalRegion, region)));
        Assert.Equal(0x80000, layout.Capacity);
        Assert.Equal(2, layout.Banks.Count);
        foreach (MemoryLayoutBankLocator bank in layout.Banks)
        {
            MemoryLayoutSectionLocator[] sections = [.. layout.SectionLocators.Where(section => bank.Range.Contains(section.Range))];
            Assert.All(sections, section =>
            {
                Assert.Equal(bank, section.Bank);
                Assert.Contains(session.ExactCapability!.MemoryLayoutContext!.Map.Regions,
                    region => ReferenceEquals(region, section.CanonicalRegion));
            });
            Assert.Equal([MemoryContentRole.Dp, MemoryContentRole.Tp, MemoryContentRole.Dp],
                sections.Select(static section => section.ContentRole));
            Assert.Equal([new ByteRange(bank.Range.Start, 0xA000),
                new ByteRange(bank.Range.Start + 0xA000, 0x2D000),
                new ByteRange(bank.Range.Start + 0x37000, 0x9000)],
                sections.Select(static section => section.Range));
        }
    }

    /// <summary>Declared large banks retain DP context beyond the local Replace prefix.</summary>
    [Theory]
    [InlineData("NT51950", "cascade", "nt51951-fw200-cascade2-auto-prj-599-20260731", 0x40000)]
    [InlineData("NT51951", "single", "nt51951-fw200-single-auto-prj-695-20260718", 0x80000)]
    [InlineData("NT51951", "cascade", "nt51951-fw200-cascade2-auto-prj-599-20260731", 0x80000)]
    public void LargeBankOverviewUsesDeclaredCompanionIncludingPreservedTail(
        string member, string number, string sourceCase, int localLength)
    {
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", sourceCase);
        JsonElement artifact = golden.GetProperty("artifacts").EnumerateArray().Single(static item =>
            item.GetProperty("artifactId").GetString() == "expected-output");
        byte[] source = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
        byte[] bankBytes = new byte[0x80000];
        source.AsSpan(0, localLength).CopyTo(bankBytes);
        byte[] reference = [.. bankBytes, .. bankBytes];
        foreach (int field in new[] { 0xA100, 0xA110, 0xA120 })
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(reference.AsSpan(0x80000 + field, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(0x80000 + field, 4), checked(address + 0x80000));
        }
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = reference;
        _ = paths.Remove("replace-ctrlram-nf");
        _ = bytes.Remove("replace-ctrlram-nf");
        paths["replace-ctrlram-normal"] = Path.Combine(Path.GetTempPath(), "layout-normal.bin");
        bytes["replace-ctrlram-normal"] = source.AsSpan(0x25610, 0x5C00).ToArray();
        foreach (AbCtrlRamBankSelection selection in new[] { AbCtrlRamBankSelection.A, AbCtrlRamBankSelection.B, AbCtrlRamBankSelection.Both })
        {
            CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new(ExperienceIds.CtrlRamReplace), member, number, paths, bytes, new AbCtrlRamDraftState(selection));
            Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
            ActiveSessionSnapshot session = prepared.AcceptedSession!;
            MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
                session.ExactCapability!, session, session.ExactCapability!.CompiledComposition);
            Assert.Equal(0x100000, layout.Capacity);
            Assert.Equal(2, layout.Banks.Count);
            foreach (MemoryLayoutBankLocator bank in layout.Banks)
            {
                MemoryLayoutSectionLocator[] sections = [.. layout.SectionLocators.Where(section => bank.Range.Contains(section.Range))];
                Assert.Equal([MemoryContentRole.Dp, MemoryContentRole.Tp, MemoryContentRole.Dp],
                    sections.Select(static section => section.ContentRole));
                Assert.Equal(bank.Range.Start, sections[0].Range.Start);
                Assert.Equal(bank.Range.EndExclusive, sections[^1].Range.EndExclusive);
                Assert.All(sections, section => Assert.Equal(bank, section.Bank));
            }
        }
    }

    /// <summary>Only selected local effects are shown; bank transfer operations do not color the whole image.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public void BankCoverageUsesDeclaredRangesAndOriginalOperations(AbCtrlRamBankSelection selection)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        ActiveSessionSnapshot session = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, new AbCtrlRamDraftState(selection)).AcceptedSession!;
        CompiledComposition composition = session.ExactCapability!.CompiledComposition;
        IReadOnlyList<CtrlRamRegion> regions = BootstrapTestHost.Canonical.CtrlRamAuthoring
            .GetDiscoveryDisplay("NT51929", "single").Regions;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(session.ExactCapability, session, composition, regions);
        Assert.Equal(0x80000, layout.Capacity);
        RuntimeReferenceBankReplaceV2CompilationContext context = Assert.IsType<RuntimeReferenceBankReplaceV2CompilationContext>(
            composition.V2Details.Provenance.Context);
        Assert.Collection(layout.Banks,
            bank => Assert.Equal("a-bank", bank.BankId),
            bank => Assert.Equal("b-bank", bank.BankId));
        Assert.All(layout.Banks, bank =>
        {
            Assert.Equal(layout.AddressSpaceId, bank.AddressSpaceId);
            Assert.Equal(context.ResolvedMap.ImageMap.Regions.Single(region => region.RegionId == bank.BankId).Range, bank.Range);
            Assert.True(new ByteRange(0, layout.Capacity).Contains(bank.Range));
        });
        Assert.False(layout.Banks[0].Range.Overlaps(layout.Banks[1].Range));
        Assert.All(context.Banks, bank => Assert.Equal(bank.OutputRange,
            layout.Banks.Single(locator => locator.BankId == bank.BankId).Range));
        IList<MemoryLayoutBankLocator> mutableBanks = Assert.IsType<IList<MemoryLayoutBankLocator>>(layout.Banks, exactMatch: false);
        _ = Assert.Throws<NotSupportedException>(mutableBanks.Clear);
        Assert.All(layout.BeforeSegments, static segment => Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition));
        for (int i = 0; i < 2; i++)
        {
            int start = i * 0x40000;
            bool selected = selection == AbCtrlRamBankSelection.Both || (i == 0 ? selection == AbCtrlRamBankSelection.A : selection == AbCtrlRamBankSelection.B);
            MemoryLayoutSegment[] bank = [.. layout.AfterSegments.Where(segment => segment.Range.Start >= start && segment.Range.EndExclusive <= start + 0x40000)];
            Assert.NotEmpty(bank);
            foreach (CtrlRamRegion region in regions)
            {
                var placed = new ByteRange(start + region.Start, region.Length);
                MemoryLayoutSegment[] parts = [.. bank.Where(segment => placed.Contains(segment.Range))];
                Assert.NotEmpty(parts);
                Assert.Equal(placed.Length, parts.Sum(static segment => segment.Range.Length));
                Assert.All(parts, segment =>
                {
                    Assert.Equal(MemoryContentRole.CtrlRam, segment.ContentRole);
                    Assert.Equal(region.RegionGroup, segment.RegionGroup);
                    Assert.Equal(region.Role, segment.CtrlRamRegionRole);
                    MemoryLayoutBankRegion attribution = Assert.IsType<MemoryLayoutBankRegion>(segment.BankRegion);
                    Assert.Equal(i == 0 ? "a-bank" : "b-bank", attribution.Bank.BankId);
                    Assert.Equal(placed, attribution.Range);
                    Assert.Equal(new ByteRange(region.Start, region.Length), attribution.LocalRegion.Range);
                    Assert.Same(context.Banks[0].LocalComposition.V2Details.Provenance.ResolvedMap, attribution.LocalMap);
                    Assert.Contains(attribution.LocalMap.ImageMap.Regions, candidate => ReferenceEquals(candidate, attribution.LocalRegion));
                    Assert.Contains(layout.CanonicalRegions, candidate => ReferenceEquals(candidate, segment.CanonicalRegion));
                });
            }
            MemoryLayoutSegment nf = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x1FC00, 1)));
            MemoryLayoutSegment retained = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x32000, 1)));
            Assert.Equal(MemoryWorkflowDisposition.Kept, retained.Disposition);
            Assert.Equal("reference-base", retained.ContentSource!.SourceSpaceId);
            if (selected)
            {
                Assert.Equal(MemoryWorkflowDisposition.WillReplace, nf.Disposition);
                Assert.Equal("replace-ctrlram-nf", nf.SourceSlotId);
                Assert.Equal("replace-ctrlram-nf", nf.ContentSource!.SourceSlotId);
                Assert.NotEqual(nf.LogicalCoverageGroupId, retained.LogicalCoverageGroupId);
                MemoryLayoutSegment tail = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x1FC01, 1)));
                Assert.Equal(MemoryWorkflowDisposition.Kept, tail.Disposition);
                Assert.Equal(nf.LogicalCoverageGroupId, tail.LogicalCoverageGroupId);
                Assert.Same(nf.BankRegion, tail.BankRegion);
                Assert.Equal("reference-base", tail.ContentSource!.SourceSpaceId);
                Assert.Empty(tail.ContributingOperations);
                Assert.Contains(bank, static segment => segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite);
                Assert.Equal(MemoryProcessorEffect.DeclaredWrite,
                    Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x2E000, 1))).ProcessorEffect);
                if (i == 1)
                {
                    MemoryLayoutSegment header = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(0x47164, 4)));
                    Assert.Contains(header.ContributingOperations, static operation => operation.OperationId == "b-bank/normalize/relocate-tpb-ilm");
                    Assert.Contains(header.ContributingOperations, static operation => operation.OperationId == "b-bank/restore/relocate-tpb-ilm");
                }
            }
            else
            {
                Assert.All(bank, static segment => Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition));
                Assert.All(bank, static segment => Assert.Empty(segment.ContributingOperations));
            }
        }
        Assert.Contains(layout.SectionLocators, static section => section.ContentRole == MemoryContentRole.Tp);
        long sectionEnd = 0;
        foreach (MemoryLayoutSectionLocator section in layout.SectionLocators)
        {
            Assert.Equal(sectionEnd, section.Range.Start);
            sectionEnd = section.Range.EndExclusive;
        }
        Assert.Equal(layout.Capacity, sectionEnd);
        Assert.All(layout.AfterSegments, segment =>
        {
            Assert.Equal(MemoryObservedChange.NotObserved, segment.ObservedChange);
            Assert.All(segment.ContributingOperations, operation =>
            {
                Assert.DoesNotContain("/seed", operation.OperationId, StringComparison.Ordinal);
                Assert.DoesNotContain("/publish", operation.OperationId, StringComparison.Ordinal);
                Assert.Contains(composition.Plan.OrderedOperations, original => ReferenceEquals(original, operation));
            });
        });
    }

    /// <summary>Optional display declarations cannot introduce unmatched or duplicate local geometry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidLocalDisplayGeometryIsRejected(bool duplicate)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        ActiveSessionSnapshot session = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, new AbCtrlRamDraftState()).AcceptedSession!;
        CtrlRamRegion[] regions = [.. BootstrapTestHost.Canonical.CtrlRamAuthoring.GetDiscoveryDisplay("NT51929", "single").Regions];
        regions = duplicate ? [.. regions, regions[0]] : [regions[0] with { Start = long.MaxValue }];
        _ = Assert.Throws<MemoryLayoutDisplayProjectionException>(() => MemoryLayoutProjector.Project(
            session.ExactCapability!, session, session.ExactCapability!.CompiledComposition, regions));
    }

    /// <summary>A Standard reference retains its complete sections without AB viewport identities.</summary>
    [Fact]
    public void StandardReferenceHasNoBankLocators()
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        bytes[CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "standard-merge", "NT51929", "expected-output"));
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(
            BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
                new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes).AcceptedSession);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            session.ExactCapability!, session, session.ExactCapability!.CompiledComposition);

        Assert.Empty(layout.Banks);
        Assert.NotEmpty(layout.SectionLocators);
    }
}
