using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Accepted fixed-workflow sessions bind execution to inspected paths and bytes.</summary>
[Collection(CanonicalCapabilityCatalogPublicationGroup.Name)]
public sealed class AcceptedSessionFileIdentityTests
{
    /// <summary>Standard Merge rejects a different path even when its bytes are structurally valid.</summary>
    [Fact]
    public async Task StandardMergeAcceptedSessionRejectsSwappedPath()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-standard-accepted-path");
        Dictionary<string, string> paths = CreateStandardInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptStandardSession(paths);
        Dictionary<string, string> swapped = new(paths, StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = workspace.Write(
                "swapped-dp.bin",
                File.ReadAllBytes(paths[CompositionAddressSpaceIds.DpInput])),
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await WorkbenchCompositionService.RunStandardMergeAcceptedSessionWithProgressAsync(
                "NT51926",
                swapped,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken));
    }

    /// <summary>Standard Merge rejects bytes changed at the accepted path.</summary>
    [Fact]
    public async Task StandardMergeAcceptedSessionRejectsSamePathMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-standard-accepted-content");
        Dictionary<string, string> paths = CreateStandardInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptStandardSession(paths);
        MutateFirstByte(paths[CompositionAddressSpaceIds.TpInput]);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunStandardMergeAcceptedSessionWithProgressAsync(
                "NT51926",
                paths,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        AssertSnapshotMismatch(result);
    }

    /// <summary>AB Merge rejects a different path even when its bytes are structurally valid.</summary>
    [Fact]
    public async Task AbMergeAcceptedSessionRejectsSwappedPath()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-path");
        Dictionary<string, string> paths = CreateAbInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptAbSession(paths);
        Dictionary<string, string> swapped = new(paths, StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "swapped-tp-a.bin",
                File.ReadAllBytes(paths[CompositionAddressSpaceIds.TpAInput])),
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AbMergeWorkbenchCompositionService.RunAbMergeAcceptedSessionWithProgressAsync(
                "NT51929",
                swapped,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken));
    }

    /// <summary>AB Merge rejects bytes changed at the accepted path.</summary>
    [Fact]
    public async Task AbMergeAcceptedSessionRejectsSamePathMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-content");
        Dictionary<string, string> paths = CreateAbInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptAbSession(paths);
        MutateFirstByte(paths[CompositionAddressSpaceIds.TpBInput]);

        WorkbenchRunResult result = await AbMergeWorkbenchCompositionService
            .RunAbMergeAcceptedSessionWithProgressAsync(
                "NT51929",
                paths,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        AssertSnapshotMismatch(result);
    }

    /// <summary>DP Replace rejects a different path even when its capacity is valid.</summary>
    [Fact]
    public async Task DpReplaceAcceptedSessionRejectsSwappedPath()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-dp-replace-accepted-path");
        Dictionary<string, string> paths = CreateDpReplaceInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptDpReplaceSession(paths);
        Dictionary<string, string> swapped = new(paths, StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceDp] = workspace.Write(
                "swapped-replacement.bin",
                File.ReadAllBytes(paths[WorkbenchSlotIds.ReplaceDp])),
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await WorkbenchCompositionService.RunReplaceAcceptedSessionWithProgressAsync(
                "NT51928",
                WorkbenchIcNumberTokens.SingleChip,
                WorkbenchReplaceModes.Dp,
                swapped,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken));
    }

    /// <summary>DP Replace rejects bytes changed at the accepted path.</summary>
    [Fact]
    public async Task DpReplaceAcceptedSessionRejectsSamePathMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-dp-replace-accepted-content");
        Dictionary<string, string> paths = CreateDpReplaceInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptDpReplaceSession(paths);
        MutateFirstByte(paths[WorkbenchSlotIds.ReplaceDp]);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunReplaceAcceptedSessionWithProgressAsync(
                "NT51928",
                WorkbenchIcNumberTokens.SingleChip,
                WorkbenchReplaceModes.Dp,
                paths,
                accepted,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        AssertSnapshotMismatch(result);
    }

    private static ActiveSessionSnapshot AcceptStandardSession(
        Dictionary<string, string> paths)
    {
        var stamps = paths.ToDictionary(
            static pair => pair.Key,
            static pair => FileStamp.FromBytes(File.ReadAllBytes(pair.Value)),
            StringComparer.Ordinal);
        WorkbenchStandardMergeAuthoringSnapshot projection =
            WorkbenchCompositionService.GetStandardMergeAuthoringSnapshot(
                "NT51926",
                [.. paths.Keys],
                stamps,
                new AuthoringRevision(1));
        return AcceptSession(
            ExperienceIds.StandardMerge,
            "NT51926",
            projection.Catalog,
            paths,
            FixedInspectionKind.StandardMerge);
    }

    private static ActiveSessionSnapshot AcceptAbSession(
        Dictionary<string, string> paths)
    {
        WorkbenchAbMergeAuthoringSnapshot projection =
            WorkbenchCompositionService.GetAbMergeAuthoringSnapshot(
                "NT51929",
                topologyToken: null,
                [.. paths.Keys],
                paths.ToDictionary(
                    static pair => pair.Key,
                    static pair => FileStamp.FromBytes(File.ReadAllBytes(pair.Value)),
                    StringComparer.Ordinal),
                new AuthoringRevision(1));
        return AcceptSession(
            ExperienceIds.AbMerge,
            "NT51929",
            projection.Catalog,
            paths,
            FixedInspectionKind.AbMerge);
    }

    private static ActiveSessionSnapshot AcceptDpReplaceSession(
        Dictionary<string, string> paths)
    {
        var inspectionPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.ReferenceBase] = paths[WorkbenchSlotIds.ReplaceBase],
            [CompositionAddressSpaceIds.InitialCodeReplacement] = paths[WorkbenchSlotIds.ReplaceDp],
        };
        CompiledAuthoringSelectionSnapshot projection =
            WorkbenchCompositionService.GetDpReplaceAuthoringSnapshot(
                "NT51928",
                [.. inspectionPaths.Keys],
                inspectionPaths.ToDictionary(
                    static pair => pair.Key,
                    static pair => FileStamp.FromBytes(File.ReadAllBytes(pair.Value)),
                    StringComparer.Ordinal),
                new AuthoringRevision(1));
        return AcceptSession(
            ExperienceIds.DpReplace,
            "NT51928",
            projection.Catalog,
            inspectionPaths,
            FixedInspectionKind.DpReplace);
    }

    private static ActiveSessionSnapshot AcceptSession(
        string workflowId,
        string icId,
        AuthoringCapabilityCatalogSnapshot catalog,
        IReadOnlyDictionary<string, string> paths,
        FixedInspectionKind kind)
    {
        var session = new AuthoringSessionState(workflowId);
        Assert.True(session.Activate(catalog).Succeeded);
        AuthoringSlotInspectionBatchStartResult started =
            session.BeginSlotFileInspections(paths);
        Assert.True(started.Succeeded, started.Issue?.Message);
        ResolvedCapability exact = Assert.IsType<ResolvedCapability>(
            started.Leases[0].ExactCapability);
        WorkbenchFirmwareInspectionInput[] inputs =
        [
            .. paths.Select(pair => kind switch
            {
                FixedInspectionKind.StandardMerge => new WorkbenchFirmwareInspectionInput(
                    pair.Key,
                    pair.Value,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    StandardMergeAddressSpaceId: pair.Key,
                    ExactCapability: exact),
                FixedInspectionKind.AbMerge => new WorkbenchFirmwareInspectionInput(
                    pair.Key,
                    pair.Value,
                    AbMergeAddressSpaceId: pair.Key,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    ExactCapability: exact),
                FixedInspectionKind.DpReplace => new WorkbenchFirmwareInspectionInput(
                    pair.Key,
                    pair.Value,
                    DpReplaceAddressSpaceId: pair.Key,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    ExactCapability: exact),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            }),
        ];
        IReadOnlyList<WorkbenchFirmwareInspectionResult> inspected =
            WorkbenchCompositionService.InspectFirmwareBatch(icId, inputs);
        AuthoringCapabilityCatalogSnapshot exactCatalog = Assert.IsType<AuthoringCapabilityCatalogSnapshot>(
            inspected[0].Inspection.InputSlotCatalog);
        var statuses = inspected.ToDictionary(
            static result => result.Inspection.InputSlotStatus!.SlotId,
            static result => result.Inspection.InputSlotStatus!,
            StringComparer.Ordinal);
        AuthoringSessionTransitionResult completed =
            session.TryCompleteSlotFileInspectionBatch(
                exactCatalog,
                started.Leases,
                statuses);
        Assert.True(completed.Succeeded, completed.Issue?.Message);
        Assert.NotNull(completed.Snapshot!.GetAcceptedCapability(
            AuthoringDerivedResultKind.Inspection));
        return completed.Snapshot;
    }

    private static Dictionary<string, string> CreateStandardInputs(TempWorkspace workspace)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = workspace.Write(
                "dp.bin", CreatePattern(0x40000, 0x21)),
            [CompositionAddressSpaceIds.TpInput] = workspace.Write(
                "tp.bin", CreatePattern(0x40000, 0x31)),
        };
    }

    private static Dictionary<string, string> CreateAbInputs(TempWorkspace workspace)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write(
                "dp-ab.bin", CreatePattern(0x80000, 0x41)),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "tp-a.bin", CreatePattern(0x40000, 0x51)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "tp-b.bin", CreatePattern(0x40000, 0x61)),
        };
    }

    private static Dictionary<string, string> CreateDpReplaceInputs(TempWorkspace workspace)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = workspace.Write(
                "base.bin", CreatePattern(0x40000, 0x71)),
            [WorkbenchSlotIds.ReplaceDp] = workspace.Write(
                "replacement.bin", CreatePattern(0x40000, 0x81)),
        };
    }

    private static byte[] CreatePattern(int length, byte seed)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(seed + index));
        }
        return bytes;
    }

    private static void MutateFirstByte(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertSnapshotMismatch(WorkbenchRunResult result)
    {
        Assert.False(result.Succeeded);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            static issue => issue.GetProperty("Code").GetString() ==
                CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch);
    }

    private static void ReloadCatalog()
    {
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
                TestContext.Current.CancellationToken);
        Assert.True(reload.Succeeded, string.Join(
            "; ", reload.Issues.Select(static issue => issue.Message)));
    }

    private enum FixedInspectionKind
    {
        StandardMerge,
        AbMerge,
        DpReplace,
    }
}
