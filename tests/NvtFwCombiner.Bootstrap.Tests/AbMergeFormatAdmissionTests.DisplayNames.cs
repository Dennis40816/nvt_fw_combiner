using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeFormatAdmissionTests
{
    /// <summary>Raw names remain per input while configured aliases and map admission keep their authority.</summary>
    [Theory]
    [InlineData(0x97, 0xA6, "Auto Desay", "Auto Desay Palminfo", false)]
    [InlineData(0xA3, 0xA4, "Auto STLA v1", "Auto INX v7", false)]
    [InlineData(0x86, 0x8F, null, null, false)]
    [InlineData(0xA3, 0xA4, "Auto STLA v1", "Auto INX v7", true)]
    [InlineData(0xF1, 0xF2, null, null, true)]
    public void ObservedByteNamesDoNotReplaceConfiguredFormatAuthority(
        byte rawA, byte rawB, string? nameA, string? nameB, bool customRecognition)
    {
        MetadataPlanDefinition plan = Plan("NT51950", 1);
        FirmwareFamilyResolutionDefinition family = plan.Entries[0].FamilyDefinition;
        byte[] a = Tp(rawA, count: 1);
        byte[] b = Tp(rawB, count: 1);
        EventBufferFormatConfigurationState configuration = Configuration(family,
            values: customRecognition ? [rawA, rawB] : null, alias: "My_vendor");
        AbMergeFormatAdmissionResult result = AssessFormat(family, "NT51950", configuration,
            Inspect(plan, a, b), Topology(1), Artifacts(a, b));
        Assert.True(result.Succeeded, string.Join(" | ", result.Issues.Select(issue => issue.Code)));
        AbMergeFormatSelection selection = Assert.IsType<AbMergeFormatSelection>(result.Selection);
        bool desay = customRecognition || rawA == 0x97;
        Assert.Equal("nt51950-ab-merge-512k", selection.MapId);
        Assert.Equal(desay ? "desay" : "common", selection.FormatId);
        Assert.Equal(desay ? "My_vendor" : "Common", selection.DisplayName);
        foreach ((string space, byte raw, string? name, FirmwareMetadataStructureResolution primary) in new[]
        {
            (CompositionAddressSpaceIds.TpAInput, rawA, nameA, selection.TpAPrimary),
            (CompositionAddressSpaceIds.TpBInput, rawB, nameB, selection.TpBPrimary),
        })
        {
            EventBufferFormatObservation observation = Assert.IsType<EventBufferFormatObservation>(
                AbMergeAuthoringExperience.ProjectEventBufferFormat(space, selection));
            Assert.Equal(raw, observation.RawByte);
            Assert.Equal(name, observation.DetectedDisplayName);
            Assert.Equal(selection.DisplayName, observation.DisplayName);
            Assert.Equal(configuration.Generation, observation.ConfigurationGeneration);
            Assert.Equal(ConfigHash, observation.ConfigurationSourceSha256);
            Assert.Equal(primary.Resolved!.ArtifactIdentity, observation.ArtifactIdentity);
            Assert.Equal(primary.Resolved.LocatorOutcome.ResolvedRange, observation.PrimaryRange);
        }
    }
}
