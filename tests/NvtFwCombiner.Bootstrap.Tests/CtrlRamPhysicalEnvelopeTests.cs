using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Physical DiffDLM records include their preserved tails without enlarging input writes.</summary>
public sealed class CtrlRamPhysicalEnvelopeTests
{
    /// <summary>Discovery renders complete active records, not the shorter writable prefix.</summary>
    [Theory]
    [InlineData("NT51950", "cascade", 0x1400)]
    [InlineData("NT51951", "cascade", 0x1400)]
    [InlineData("NT51919", "2", 0x1400)]
    [InlineData("NT51929", "4", 0x3C00)]
    [InlineData("NT51932", "8", 0x8C00)]
    public void DiffDlmDisplayIncludesEveryPreservedRecordTail(string ic, string number, long expectedLength)
    {
        CtrlRamInspectionDisplay display = BootstrapTestHost.Services.CtrlRamAuthoring.GetDiscoveryDisplay(ic, number);
        CtrlRamRegion diff = Assert.Single(display.Regions, region => region.Role == CtrlRamRegionRole.DiffDlm);
        Assert.Equal(expectedLength, diff.Length);
        Assert.Equal(ReplaceRegionGroup.Cascade, diff.RegionGroup);
        Assert.DoesNotContain(display.InputSlots, slot => slot.SlotId == "replace-ctrlram-nf");
    }

    /// <summary>The real 950-family input compiler and display agree on the full physical Diff record.</summary>
    [Theory]
    [InlineData("NT51950", "tp-firmware-input")]
    [InlineData("NT51951", "expected-output")]
    [InlineData("NT51951", "tp-firmware-input")]
    public void CascadeDiscoveryMatchesExactCanonicalMapWithoutExpandingWrites(string ic, string baseArtifact)
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51951-fw200-cascade2-auto-prj-599-20260731");
        string ArtifactPath(string id)
        {
            return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
                .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
        }
        // NT51950 uses the existing NT51951 TP-work geometry alias, not its 512-KiB full image.
        var paths = new Dictionary<string, string>
        {
            ["replace-base"] = ArtifactPath(baseArtifact),
            ["replace-ctrlram-normal"] = ArtifactPath("normal-ctrlram-input"),
            ["replace-ctrlram-vn"] = ArtifactPath("vn-ctrlram-input"),
            ["replace-ctrlram-diff"] = ArtifactPath("diffdlm-input"),
        };
        Dictionary<string, byte[]> bytes = paths.ToDictionary(pair => pair.Key, pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), ic, "cascade", paths, bytes);
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(issue => issue.Message)));
        ActiveSessionSnapshot session = prepared.AcceptedSession!;
        CtrlRamInspectionDisplay display = BootstrapTestHost.Canonical.CtrlRamAuthoring
            .GetDiscoveryDisplayFromAcceptedBase(ic, "cascade", bytes["replace-base"]);
        CtrlRamRegion diff = Assert.Single(display.Regions, region => region.Role == CtrlRamRegionRole.DiffDlm);
        CompiledComposition composition = session.ExactCapability!.CompiledComposition;
        FirmwareRegion canonical = composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions.Single(region => region.RegionId == "diff-ctrlram");
        Assert.Equal(canonical.Range.Start, diff.Start);
        Assert.Equal(canonical.Range.Length, diff.Length);
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(session.ExactCapability, session, composition, display.Regions);
        MemoryLayoutSectionLocator tp = Assert.Single(layout.SectionLocators, section => section.ContentRole == MemoryContentRole.Tp);
        Assert.Equal(new ByteRange(0xA000, 0x2D000), tp.Range);
        Assert.True(tp.IsImageOverlay);
        Assert.Equal(baseArtifact == "expected-output", layout.SectionLocators.Any(section => section.ContentRole == MemoryContentRole.Dp));
        Assert.Equal(baseArtifact == "expected-output" ? 3 : 2, layout.SectionLocators.Count);
        Assert.Equal(layout.Capacity, layout.SectionLocators.Sum(section => section.Range.Length));
        Assert.Equal(0x1400, layout.AfterSegments.Where(segment => ReferenceEquals(segment.CanonicalRegion, canonical)).Sum(segment => segment.Range.Length));
        Assert.Equal(0x5C00, Assert.Single(display.Regions, region => region.Role == CtrlRamRegionRole.Normal).Length);
        Assert.Equal(0x20FC, Assert.Single(display.Regions, region => region.Role == CtrlRamRegionRole.Vn).Length);
        CtrlRamInputDescriptionFacts input = display.InputSlots.Single(slot => slot.SlotId == "replace-ctrlram-diff").CtrlRamDescription!;
        _ = Assert.Single(input.Sections);
        Assert.Equal(0x910, input.Sections[0].MaximumLength);
    }
}
