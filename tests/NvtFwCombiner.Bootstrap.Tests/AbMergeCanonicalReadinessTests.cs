using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>AB inspection publishes one exact Application-owned terminal batch.</summary>
    [Fact]
    public async Task AbInspectionBatchPublishesCanonicalTerminalStatuses()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-canonical-readiness");
        Dictionary<string, string> paths = WriteInputs(workspace);
        const long revision = 7;
        FirmwareInspectionSnapshotInput[] inputs =
        [
            .. paths.Select(pair => new FirmwareInspectionSnapshotInput(
                pair.Key,
                pair.Value,
                AbMergeAddressSpaceId: pair.Key,
                AuthoringRevision: revision)),
        ];

        FirmwareInspectionBatchResult batch = await BootstrapTestHost.Services.FirmwareInspectionExperience
            .InspectFirmwareBatchAsync("NT51929", inputs, TestContext.Current.CancellationToken);
        FirmwareInspectionSnapshot[] results = [.. batch.InspectionsById.Values];

        Assert.Equal(3, results.Length);
        AuthoringCapabilityCatalogSnapshot catalog = Assert.IsType<AuthoringCapabilityCatalogSnapshot>(
            results[0].InputSlotCatalog);
        string compilationFingerprint = Assert.Single(catalog.Routes).CompilationFingerprint!;
        Assert.All(results, result =>
        {
            Assert.Same(catalog, result.InputSlotCatalog);
            AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
                result.InputSlotStatus);
            Assert.Equal(new AuthoringRevision(revision), status.AuthoringRevision);
            Assert.Equal(compilationFingerprint, status.CompilationFingerprint);
            Assert.Equal(AuthoringSlotLifecycle.Verified, status.InspectionLifecycle);
            Assert.True(status.IsTerminal);
            Assert.False(status.BlocksBuild);
        });
    }

    /// <summary>Canonical AB health preserves blocking short input and accepted-tail warning semantics.</summary>
    [Fact]
    public async Task AbInspectionBatchPreservesShortAndTailHealth()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-canonical-health");
        FirmwareInspectionSnapshotInput[] inputs =
        [
            new(
                CompositionAddressSpaceIds.DpAbInput,
                workspace.Write("dp-short.bin", new byte[DpLength - 1]),
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.DpAbInput),
            new(
                CompositionAddressSpaceIds.TpAInput,
                workspace.Write("tp-tail.bin", CreateTpImage(0x81, 0, chipCount: 1, length: TpLength + 1)),
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput),
        ];

        FirmwareInspectionBatchResult batch = await BootstrapTestHost.Services.FirmwareInspectionExperience
            .InspectFirmwareBatchAsync("NT51929", inputs, TestContext.Current.CancellationToken);
        IReadOnlyDictionary<string, FirmwareInspectionSnapshot> results = batch.InspectionsById;

        Assert.Equal(
            AuthoringSlotLifecycle.Error,
            results[CompositionAddressSpaceIds.DpAbInput].InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            AuthoringSlotLifecycle.Verified,
            results[CompositionAddressSpaceIds.TpAInput].InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(new ByteRange(0, TpLength), results[CompositionAddressSpaceIds.TpAInput].InputSlotStatus!.Inspection!.AcceptedSnapshotRange);
    }

    /// <summary>Readiness observations and Build naming share one raw AB version decoder.</summary>
    [Fact]
    public async Task AbVersionObservationsMatchOutputNamingTokensAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-version-decoder-parity");
        Dictionary<string, string> paths = WriteInputs(workspace);
        byte[] tpAWithIgnoredTail = new byte[TpLength + 17];
        CreateTpImage(0x81, 0, chipCount: 1).CopyTo(tpAWithIgnoredTail, 0);
        tpAWithIgnoredTail[^1] = 0xA5;
        paths[CompositionAddressSpaceIds.TpAInput] = workspace.Write(
            "tp-a-with-tail.bin",
            tpAWithIgnoredTail);
        FirmwareInspectionSnapshotInput[] inputs =
        [
            .. paths.Select(pair => new FirmwareInspectionSnapshotInput(
                pair.Key,
                pair.Value,
                AbMergeAddressSpaceId: pair.Key)),
        ];

        FirmwareInspectionBatchResult batch = await BootstrapTestHost.Services.FirmwareInspectionExperience
            .InspectFirmwareBatchAsync("NT51929", inputs, TestContext.Current.CancellationToken);
        CompiledInputVersionObservation[] versions =
        [
            .. batch.InspectionsById.Values.SelectMany(static result => result.InputSlotStatus!.Observation.Versions),
        ];
        CompositionRunResult run = await AbMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51929",
            paths,
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(run.Succeeded, CompositionRunReportJson.Serialize(run));
        var tokens = run.OutputNaming!.Tokens.ToDictionary(static token => token.TokenId);
        var acceptedHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = Sha256(
                File.ReadAllBytes(paths[CompositionAddressSpaceIds.DpAbInput])),
            [CompositionAddressSpaceIds.TpAInput] = Sha256(tpAWithIgnoredTail.AsSpan(0, TpLength)),
            [CompositionAddressSpaceIds.TpBInput] = Sha256(
                File.ReadAllBytes(paths[CompositionAddressSpaceIds.TpBInput]).AsSpan(0, TpLength)),
        };
        Assert.NotEqual(
            Sha256(tpAWithIgnoredTail),
            acceptedHashes[CompositionAddressSpaceIds.TpAInput]);
        foreach (CompiledInputVersionObservation version in versions)
        {
            string tokenId = version.Kind switch
            {
                CompiledInputVersionKind.DpA => "dp-a",
                CompiledInputVersionKind.DpB => "dp-b",
                CompiledInputVersionKind.TpA => "tp-a",
                CompiledInputVersionKind.TpB => "tp-b",
                CompiledInputVersionKind.TpReferenceFirmwareConfig => throw new InvalidOperationException(),
                _ => throw new ArgumentOutOfRangeException(nameof(version), version.Kind, null),
            };
            string expected = version.IsKnown
                ? version.Kind is CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB
                    ? FormattableString.Invariant($"D{version.Major:X2}{version.Minor:X2}")
                    : FormattableString.Invariant($"T{version.Major:X2}{version.Minor:X2}")
                : version.Kind is CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB
                    ? "Dxxxx"
                    : "Txxxx";
            string sourceAddressSpaceId = version.Kind switch
            {
                CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB =>
                    CompositionAddressSpaceIds.DpAbInput,
                CompiledInputVersionKind.TpA => CompositionAddressSpaceIds.TpAInput,
                CompiledInputVersionKind.TpB => CompositionAddressSpaceIds.TpBInput,
                CompiledInputVersionKind.TpReferenceFirmwareConfig => throw new InvalidOperationException(),
                _ => throw new ArgumentOutOfRangeException(nameof(version), version.Kind, null),
            };
            string parserId = version.Kind is CompiledInputVersionKind.DpA or CompiledInputVersionKind.DpB
                ? "profile-cmi-reg16-18;reg16=0xF3;reg17=0x18;reg18=0x3D;jira=3571"
                : "fwconfig-backup";
            OutputNamingTokenSummary token = tokens[tokenId];
            Assert.Equal(version.IsKnown, token.IsKnown);
            Assert.Equal(expected, token.Value);
            Assert.Equal(sourceAddressSpaceId, token.SourceAddressSpaceId);
            Assert.Equal(acceptedHashes[sourceAddressSpaceId], token.AcceptedSnapshotSha256);
            Assert.Equal(parserId, token.ParserId);
        }
    }
}
