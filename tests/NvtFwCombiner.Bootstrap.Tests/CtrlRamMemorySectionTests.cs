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

    /// <summary>A reportless route displays neutral context, never guesses a Standard map from capacity.</summary>
    [Fact]
    public void NoCompanionHasOneNeutralContextWithoutCanonicalClaims()
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement[] artifacts = [.. fixture.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string nfPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == "postbuild-nf-ctrlram"));
        var paths = new Dictionary<string, string> { [CompositionSlotIds.ReplaceBase] = basePath, ["replace-ctrlram-nf"] = nfPath };
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51950", "single", paths, bytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(prepared.AcceptedSession);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(session.ExactCapability);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);
        MemoryLayoutSectionLocator section = Assert.Single(layout.SectionLocators);
        Assert.Equal(MemoryContentRole.General, section.ContentRole);
        Assert.Null(section.MapId);
        Assert.Null(section.CanonicalRegion);
        Assert.Equal(new ByteRange(0, layout.Capacity), section.Range);
    }
}
