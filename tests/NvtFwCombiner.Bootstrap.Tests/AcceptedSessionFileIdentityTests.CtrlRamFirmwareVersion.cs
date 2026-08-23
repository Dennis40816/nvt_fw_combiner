using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AcceptedSessionFileIdentityTests
{
    /// <summary>CtrlRAM firmware-version confirmation is projected from one exact accepted session without path I/O.</summary>
    [Fact]
    public void CtrlRamFirmwareVersionObservationRetainsAcceptedBytesAfterDiskMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ctrlram-version-accepted-lease");
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement expectedArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "expected-output");
        byte[] acceptedBytes = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(expectedArtifact));
        string sharedPath = workspace.Write("accepted-reference.bin", acceptedBytes);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = sharedPath,
            ["replace-ctrlram-normal"] = sharedPath,
        };
        (ActiveSessionSnapshot? snapshot, IReadOnlyList<CompositionIssue> issues) =
            CtrlRamReplaceTestSupport.Prepare(
                _host.Canonical,
                "NT51926",
                "single",
                paths,
                firmwareVersionEdit: null);
        ActiveSessionSnapshot accepted = Assert.IsType<ActiveSessionSnapshot>(snapshot);
        Assert.Empty(issues);

        AuthoringInputSlotStatus referenceStatus = accepted.InputSlotStatuses.Single(status =>
            status.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase);
        CompiledInputVersionObservation metadata = Assert.Single(
            referenceStatus.Observation.Versions,
            static version => version.Kind == CompiledInputVersionKind.TpReferenceFirmwareConfig);
        Assert.True(referenceStatus.FileStamp.HasValue);
        Assert.True(referenceStatus.AcceptedBytes.HasValue);
        Assert.True(metadata.IsKnown);
        CompiledInputVersionObservation? projected = _host.Canonical.CtrlRamAuthoring
            .ProjectFirmwareVersionConfirmationLease(accepted);
        Assert.Equal(metadata, projected);

        byte[] changed = File.ReadAllBytes(sharedPath);
        changed[^1] ^= 0x01;
        File.WriteAllBytes(sharedPath, changed);

        Assert.Contains(metadata, referenceStatus.Observation.Versions);
        Assert.True(_host.Canonical.CtrlRamAuthoring.IsFirmwareVersionConfirmationLeaseCurrent(
            accepted,
            accepted));
        (ActiveSessionSnapshot? replacementSnapshot, _) = CtrlRamReplaceTestSupport.Prepare(
            _host.Canonical,
            "NT51926",
            "single",
            paths,
            firmwareVersionEdit: null);
        ActiveSessionSnapshot replacement = Assert.IsType<ActiveSessionSnapshot>(replacementSnapshot);
        Assert.NotSame(accepted, replacement);
        Assert.False(_host.Canonical.CtrlRamAuthoring.IsFirmwareVersionConfirmationLeaseCurrent(
            replacement,
            accepted));
    }
}
