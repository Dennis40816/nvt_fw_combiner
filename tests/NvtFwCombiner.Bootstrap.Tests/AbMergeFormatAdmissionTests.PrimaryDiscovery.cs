using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeFormatAdmissionTests
{
    /// <summary>Trusted family declarations and immutable inputs suffice; no compiled map is required.</summary>
    [Theory]
    [InlineData("NT51950", 1, 1, 1, 0x97, 0xA6, "nt51950-ab-desay-single-1024k")]
    [InlineData("NT51950", 2, 2, 3, 0x97, 0xA6, "nt51950-ab-desay-cascade-1024k")]
    [InlineData("NT51950", 2, 2, 2, 0x84, 0x85, "nt51950-ab-common-exact2-1024k")]
    [InlineData("NT51950", 2, 2, 3, 0x84, 0x85, "nt51950-ab-merge-1024k")]
    [InlineData("NT51951", 0, 0, 0, 0x97, 0xA6, "nt51951-ab-desay-1024k")]
    [InlineData("NT51951", 0, 0, 0, 0x84, 0x84, "nt51951-ab-merge-1024k")]
    public void PrimaryDiscoverySelectsFormatWithoutCompiledSnapshot(
        string ic, int count, byte countA, byte countB, byte rawA, byte rawB, string expectedMap)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.AbMergeByIc[ic].GetFirmwareFamily();
        byte[] a = Tp(rawA, countA);
        byte[] b = Tp(rawB, countB);
        if (count == 0)
        {
            a = a[..0x22229];
            b = b[..0x22229];
        }

        FirmwareBinInspectionArtifact[] artifacts = Artifacts(a, b);
        AbMergeFormatAdmissionResult result = AbMergeFormatAdmission.Assess(family, ic, Configuration(family), Topology(count), artifacts);
        Assert.True(result.Succeeded, string.Join(" | ", result.Issues.Select(issue => issue.Code)));
        AbMergeFormatSelection selection = Assert.IsType<AbMergeFormatSelection>(result.Selection);
        Assert.Equal(expectedMap, selection.MapId);
        Assert.Equal(rawA, selection.TpAFormatByte);
        Assert.Equal(rawB, selection.TpBFormatByte);
        Assert.Null(selection.PrimaryInspection);
        Assert.Equal(artifacts[0].Sha256, selection.TpAPrimary.Resolved!.ArtifactIdentity.Sha256);
        Assert.Equal(artifacts[1].Sha256, selection.TpBPrimary.Resolved!.ArtifactIdentity.Sha256);
        Assert.Equal(new ByteRange(0x22200, 41), selection.TpAPrimary.Resolved.LocatorOutcome.ResolvedRange.Range);

        // The migration adapter must select exactly the same semantic result.
        MetadataPlanDefinition plan = Plan(ic, count);
        AbMergeFormatAdmissionResult legacy = AbMergeFormatAdmission.Assess(family, ic, Configuration(family),
            Inspect(plan, a, b), Topology(count), artifacts);
        Assert.True(legacy.Succeeded);
        Assert.Equal((selection.MapId, selection.FormatId, selection.DisplayName),
            (legacy.Selection!.MapId, legacy.Selection.FormatId, legacy.Selection.DisplayName));
    }

    /// <summary>Discovery is not a fallback around invalid configuration, inputs or primary relations.</summary>
    [Theory]
    [InlineData("complement", "AB_FORMAT_PRIMARY_INVALID")]
    [InlineData("short", "AB_FORMAT_PRIMARY_INVALID")]
    [InlineData("missing", "AB_FORMAT_PRIMARY_INVALID")]
    [InlineData("duplicate", "AB_FORMAT_PRIMARY_INVALID")]
    [InlineData("config", "AB_FORMAT_CONFIGURATION_INVALID")]
    [InlineData("mismatch", "AB_FORMAT_MISMATCH")]
    public void PrimaryDiscoveryFailsClosed(string mutation, string code)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.AbMergeByIc["NT51951"].GetFirmwareFamily();
        byte[] a = Tp(0x97);
        if (mutation == "complement")
        {
            a[0x22201] = 0;
        }
        else if (mutation == "short")
        {
            a = a[..0x22228];
        }

        FirmwareBinInspectionArtifact[] artifacts = Artifacts(a, Tp(mutation == "mismatch" ? (byte)0x84 : (byte)0xA6));
        artifacts = mutation switch
        {
            "missing" => [artifacts[0]],
            "duplicate" => [.. artifacts, artifacts[0]],
            _ => artifacts,
        };
        EventBufferFormatConfigurationState? state = mutation == "config" ? null : Configuration(family);
        AssertBlocked(AbMergeFormatAdmission.Assess(family, "NT51951", state, null, artifacts), code);
        if (mutation == "complement")
        {
            Assert.True(family.TryResolveAbPrimaryFirmwareConfig("NT51951",
                [new FirmwareArtifactPayload("tp-a-input", a)], null,
                out FirmwareMetadataStructureResolution? primaryA, out FirmwareMetadataStructureResolution? primaryB));
            Assert.Equal(FirmwareMetadataStructureResolutionStatus.Resolved, primaryA.Status);
            Assert.False(Assert.Single(primaryA.Resolved!.DecodedStructure.Relations,
                relation => relation.RelationId == "firmware-version-complement").IsSatisfied);
            Assert.Equal(FirmwareMetadataStructureResolutionStatus.Pending, primaryB.Status);
        }
    }

    /// <summary>Captured inputs remain immutable, while a newly captured primary value changes the decision.</summary>
    [Fact]
    public void PrimaryDiscoveryUsesCurrentCapturedBytesWithoutRetainingPreviousDecision()
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.AbMergeByIc["NT51951"].GetFirmwareFamily();
        byte[] a = Tp(0x97);
        byte[] b = Tp(0xA6);
        FirmwareBinInspectionArtifact[] captured = Artifacts(a, b);
        a[0x2220C] = 0x84;
        b[0x2220C] = 0x84;
        AbMergeFormatAdmissionResult old = AbMergeFormatAdmission.Assess(family, "NT51951", Configuration(family), null, captured);
        AbMergeFormatAdmissionResult current = AbMergeFormatAdmission.Assess(family, "NT51951", Configuration(family), null, Artifacts(a, b));
        Assert.True(old.Succeeded);
        Assert.True(current.Succeeded);
        Assert.Equal("desay", old.Selection!.FormatId);
        Assert.Equal("common", current.Selection!.FormatId);
        Assert.NotEqual(old.Selection.TpAPrimary.Resolved!.ArtifactIdentity, current.Selection.TpAPrimary.Resolved!.ArtifactIdentity);
    }
}
