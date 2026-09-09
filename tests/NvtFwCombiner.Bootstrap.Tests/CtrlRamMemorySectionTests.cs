using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Overview context reuses the admitted counterpart without changing CtrlRAM coverage.</summary>
public sealed class CtrlRamMemorySectionTests
{
    /// <summary>Capacity limits visibility, never selects the counterpart; TP-only references do not gain DP.</summary>
    [Theory]
    [InlineData(0x40000, true)]
    [InlineData(0x35000, false)]
    public void ThreeChipContextUsesExactCompanionAndPreservesPrimaryCoverage(int capacity, bool hasDp)
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase("ctrlram-replace", "nt51927-3chip-self-20260705");
        Dictionary<string, string> paths = fixture.GetProperty("artifacts").EnumerateArray()
            .ToDictionary(artifact => artifact.GetProperty("slotId").GetString()!, CanonicalGoldenTestData.ArtifactPath);
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        bytes[CompositionSlotIds.ReplaceBase] = bytes[CompositionSlotIds.ReplaceBase][..capacity];
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51927", "3", paths, bytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(prepared.AcceptedSession);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(session.ExactCapability);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);
        MemoryLayoutSectionLocator tp = Assert.Single(layout.SectionLocators, section => section.ContentRole == MemoryContentRole.Tp);
        Assert.Equal(new ByteRange(0, 0x35000), tp.Range);
        Assert.Equal("nt51927-standard-merge-256k", tp.MapId);
        Assert.Equal("tp-code", tp.CanonicalRegion!.RegionId);
        Assert.Equal(hasDp, layout.SectionLocators.Any(section => section.ContentRole == MemoryContentRole.Dp));
        if (hasDp)
        {
            Assert.Equal(new ByteRange(0x3C000, 0x4000), Assert.Single(layout.SectionLocators,
                section => section.ContentRole == MemoryContentRole.Dp).Range);
            Assert.Equal(new ByteRange(0x35000, 0x7000), Assert.Single(layout.SectionLocators,
                section => section.ContentRole == MemoryContentRole.Unmapped).Range);
        }
        FirmwareImageMap primary = capability.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap;
        Assert.Equal(primary.Regions, layout.CanonicalRegions);
        Assert.DoesNotContain(layout.CanonicalRegions, region => ReferenceEquals(region, tp.CanonicalRegion));
        Assert.Equal(primary.MapId, layout.MapId);
        long cursor = 0;
        foreach (MemoryLayoutSectionLocator section in layout.SectionLocators)
        {
            Assert.Equal(cursor, section.Range.Start);
            Assert.Equal(layout.AddressSpaceId, section.AddressSpaceId);
            cursor = section.Range.EndExclusive;
        }
        Assert.Equal(capacity, cursor);
        Assert.Equal(capacity, layout.AfterSegments.Sum(segment => segment.Range.Length));
    }

    /// <summary>Reportless full images use explicit Standard context without modifying the primary map or Report plan.</summary>
    [Theory]
    [InlineData("NT51919", "nt51929-fw200-single-auto-prj-594-20260717", "postbuild-nf-ctrlram")]
    [InlineData("NT51950", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-nf-ctrlram")]
    [InlineData("NT51951", "nt51951-fw200-single-auto-prj-695-20260718", "nf-ctrlram-input")]
    public void ExplicitContextPreservesPrimaryFacts(string ic, string caseId, string nfArtifact)
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement[] artifacts = [.. fixture.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string nfPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == nfArtifact));
        var paths = new Dictionary<string, string> { [CompositionSlotIds.ReplaceBase] = basePath, ["replace-ctrlram-nf"] = nfPath };
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), ic, "single", paths, bytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(prepared.AcceptedSession);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(session.ExactCapability);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);
        MemoryLayoutContextMap context = Assert.IsType<MemoryLayoutContextMap>(capability.MemoryLayoutContext);
        Assert.Equal(ic, context.IcId);
        Assert.Empty(capability.MetadataPlan.Definition.Entries);
        Assert.Equal(3, layout.SectionLocators.Count);
        Assert.All(layout.SectionLocators, section => Assert.Equal(context.Map.MapId, section.MapId));
        if (ic == "NT51919")
        {
            Assert.Equal(new ByteRange(0, 0x6000), layout.SectionLocators[0].Range);
            Assert.Equal(new ByteRange(0x6000, 0x1000), layout.SectionLocators[1].Range);
            Assert.Equal(new ByteRange(0x7000, 0x39000), layout.SectionLocators[2].Range);
            Assert.Equal([MemoryContentRole.Dp, MemoryContentRole.Unmapped, MemoryContentRole.Tp],
                layout.SectionLocators.Select(section => section.ContentRole));
            Assert.All(layout.SectionLocators, section => Assert.False(section.IsImageContainer));
        }
        else
        {
            Assert.Equal(new ByteRange(0, 0xA000), layout.SectionLocators[0].Range);
            Assert.Equal(new ByteRange(0xA000, 0x2D000), layout.SectionLocators[1].Range);
            Assert.Equal(new ByteRange(0x37000, layout.Capacity - 0x37000), layout.SectionLocators[2].Range);
            Assert.Equal([MemoryContentRole.Dp, MemoryContentRole.Tp, MemoryContentRole.Dp],
                layout.SectionLocators.Select(section => section.ContentRole));
            Assert.True(layout.SectionLocators[0].IsImageContainer);
            Assert.True(layout.SectionLocators[1].IsImageOverlay);
            Assert.True(layout.SectionLocators[2].IsImageContainer);
        }
        Assert.Equal(capability.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.Regions, layout.CanonicalRegions);
        Assert.Equal(layout.Capacity, layout.AfterSegments.Sum(segment => segment.Range.Length));
        Assert.Equal(layout.Capacity, layout.SectionLocators.Sum(section => section.Range.Length));
        ResolvedCapability Rebind(MemoryLayoutContextMap? candidate)
        {
            return new ResolvedCapability(capability.Identity, capability.CapabilityFingerprint,
                capability.CompiledComposition, capability.Authoring, capability.Publication,
                capability.Evidence, capability.MetadataPlan, capability.ResolutionToken,
                capability.CompilationContract, capability.RuntimeReferenceProof, candidate);
        }
        Assert.Same(context, Rebind(context).MemoryLayoutContext);
        _ = Assert.Throws<ArgumentException>(() => Rebind(null));
        var wrongHash = new MemoryLayoutContextMap(context.IcId, context.ProfileId, context.ProfileVersion,
            new string('a', 64), context.Map);
        _ = Assert.Throws<ArgumentException>(() => Rebind(wrongHash));
    }
}
