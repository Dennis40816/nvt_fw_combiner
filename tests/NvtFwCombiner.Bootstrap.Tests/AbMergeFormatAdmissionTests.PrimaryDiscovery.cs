using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeFormatAdmissionTests
{
    /// <summary>Blocking pair assessment retains each observed count rather than erasing valid metadata.</summary>
    [Theory]
    [InlineData(false, 2, 0, 2, 1)]
    [InlineData(true, 2, null, 2, 1)]
    [InlineData(true, 0, null, 0, 2)]
    public void CommonPrefixCountFailuresPreserveBothObservations(bool unreadableA, byte countB, int? expectedA, int expectedB, int issueCount)
    {
        byte[] a = Tp(0x84, 0);
        if (unreadableA) { a[0x36FFC] = 0xFF; }
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.AssessCommonAcceptedPair(InputCandidates("NT51951", null), a, Tp(0x84, countB), null);
        Assert.Equal<int?>(expectedA, result.TpAChipCount);
        Assert.Equal<int?>(expectedB, result.TpBChipCount);
        Assert.Equal(issueCount, result.Issues.Count);
        Assert.Equal(CompositionAddressSpaceIds.TpAInput, result.Issues[0].OperationId);
        Assert.All(result.Issues, static issue => Assert.Equal(CompositionIssueSeverity.Error, issue.Severity));
    }

    /// <summary>Format discovery and execution use the same prefix; ignored tail markers never supply count.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountAdmissionIgnoresTrailingBackup(bool backupOnlyInTail)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51951", "nt51951-ab-merge-1024k")!.GetFirmwareFamily();
        byte[] original = Tp(0x84, 2);
        byte[] tailed = new byte[original.Length + 0x1000];
        original.CopyTo(tailed, 0);
        original.AsSpan(0x36000, 0x1000).CopyTo(tailed.AsSpan(original.Length));
        if (backupOnlyInTail) { tailed[0x36FFC] = 0xFF; }
        CompiledComposition[] candidates = InputCandidates("NT51951", null);
        AbMergeFormatAdmissionResult discovery = AbMergeFormatAdmission.Assess(family, "NT51951", Configuration(family),
            null, Artifacts(tailed, tailed), candidates);
        AbMergeTopologyAdmissionResult runtime = AbMergeTopologyAdmission.AssessAcceptedPair(candidates[0], tailed, tailed, null)!;
        Assert.Equal(!backupOnlyInTail, discovery.Succeeded);
        Assert.Equal(discovery.Succeeded, runtime.Succeeded);
        if (backupOnlyInTail)
        {
            Assert.All(discovery.Issues, issue => Assert.Equal("firmware-config.chip-count-unreadable", issue.Code));
        }
        else
        {
            Assert.Equal("nt51951-ab-merge-1024k", discovery.Selection!.MapId);
            Assert.Equal(Artifacts(tailed, tailed)[0].Sha256, discovery.Selection.TpAPrimary.Resolved!.ArtifactIdentity.Sha256);
        }
    }

    /// <summary>Missing or conflicting candidate geometry never becomes a default map.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountAdmissionRejectsUnknownCommonGeometry(bool conflicting)
    {
        byte[] bytes = Tp(0x84);
        Array.Resize(ref bytes, 0x40000);
        CompiledComposition[] candidates = conflicting
            ? [InputCandidates("NT51951", null)[0], InputCandidates("NT51929", null)[0]] : [];
        AbMergeTopologyAdmissionResult result = AbMergeTopologyAdmission.AssessCommonAcceptedPair(candidates, bytes, bytes, null);
        Assert.Equal("AB_TP_SOURCE_GEOMETRY_INVALID", Assert.Single(result.Issues).Code);
        Assert.False(result.Succeeded);
    }

    /// <summary>Trusted family declarations and immutable inputs suffice; no compiled map is required.</summary>
    [Theory]
    [InlineData("NT51950", 1, 1, 1, 0x97, 0xA6, "nt51950-ab-merge-512k")]
    [InlineData("NT51950", 2, 3, 3, 0x97, 0xA6, "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 2, 2, 0x84, 0x85, "nt51950-ab-merge-1024k")]
    [InlineData("NT51950", 2, 3, 3, 0x84, 0x85, "nt51950-ab-merge-1024k")]
    [InlineData("NT51951", 0, 1, 1, 0x97, 0xA6, "nt51951-ab-merge-1024k")]
    [InlineData("NT51951", 0, 2, 2, 0x84, 0x84, "nt51951-ab-merge-1024k")]
    public void PrimaryDiscoverySelectsFormatWithoutCompiledSnapshot(
        string ic, int count, byte countA, byte countB, byte rawA, byte rawB, string expectedMap)
    {
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.FindAbMergeRegistration(ic, ic == "NT51950" ? "nt51950-ab-merge-maps" : "nt51951-ab-merge-1024k")!.GetFirmwareFamily();
        byte[] a = Tp(rawA, countA);
        byte[] b = Tp(rawB, countB);

        FirmwareBinInspectionArtifact[] artifacts = Artifacts(a, b);
        AbMergeFormatAdmissionResult result = AssessFormat(family, ic, Configuration(family), Topology(count), artifacts);
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
        AbMergeFormatAdmissionResult legacy = AssessFormat(family, ic, Configuration(family),
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
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51951", "nt51951-ab-merge-1024k")!.GetFirmwareFamily();
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
        AssertBlocked(AssessFormat(family, "NT51951", state, null, artifacts), code);
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
        FirmwareFamilyResolutionDefinition family = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51951", "nt51951-ab-merge-1024k")!.GetFirmwareFamily();
        byte[] a = Tp(0x97);
        byte[] b = Tp(0xA6);
        FirmwareBinInspectionArtifact[] captured = Artifacts(a, b);
        a[0x2220C] = 0x84;
        b[0x2220C] = 0x84;
        AbMergeFormatAdmissionResult old = AssessFormat(family, "NT51951", Configuration(family), null, captured);
        AbMergeFormatAdmissionResult current = AssessFormat(family, "NT51951", Configuration(family), null, Artifacts(a, b));
        Assert.True(old.Succeeded);
        Assert.True(current.Succeeded);
        Assert.Equal("desay", old.Selection!.FormatId);
        Assert.Equal("common", current.Selection!.FormatId);
        Assert.NotEqual(old.Selection.TpAPrimary.Resolved!.ArtifactIdentity, current.Selection.TpAPrimary.Resolved!.ArtifactIdentity);
    }
}
