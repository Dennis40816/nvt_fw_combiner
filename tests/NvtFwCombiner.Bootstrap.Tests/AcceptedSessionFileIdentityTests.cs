using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Accepted fixed-workflow sessions bind paths once and retain immutable inspected bytes.</summary>
public sealed class AcceptedSessionFileIdentityTests
{
    private readonly IsolatedBootstrapTestHost _host = new();

    /// <summary>Standard Merge ignores a client path alias after the session accepted canonical inputs.</summary>
    [Fact]
    public async Task StandardMergeAcceptedSessionIgnoresSwappedClientPath()
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

        CompositionRunResult expected = await ExecuteAsync(accepted, paths);
        CompositionRunResult actual = await ExecuteAsync(accepted, swapped);

        Assert.True(expected.Succeeded, CompositionRunReportJson.Serialize(expected));
        Assert.True(actual.Succeeded, CompositionRunReportJson.Serialize(actual));
        Assert.Equal(expected.OutputSha256, actual.OutputSha256);
    }

    /// <summary>Standard Merge executes retained immutable bytes when the accepted path later changes.</summary>
    [Fact]
    public async Task StandardMergeAcceptedSessionDoesNotReopenSamePathAfterMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-standard-accepted-content");
        Dictionary<string, string> paths = CreateStandardInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptStandardSession(paths);
        CompositionRunResult beforeMutation = await ExecuteAsync(accepted, paths);
        MutateFirstByte(paths[CompositionAddressSpaceIds.TpInput]);

        CompositionRunResult afterMutation = await ExecuteAsync(accepted, paths);

        Assert.True(beforeMutation.Succeeded, CompositionRunReportJson.Serialize(beforeMutation));
        Assert.True(afterMutation.Succeeded, CompositionRunReportJson.Serialize(afterMutation));
        Assert.Equal(beforeMutation.OutputSha256, afterMutation.OutputSha256);
    }

    /// <summary>AB Merge ignores a client path alias after the session accepted canonical inputs.</summary>
    [Fact]
    public async Task AbMergeAcceptedSessionIgnoresSwappedClientPath()
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

        CompositionRunResult expected = await ExecuteAsync(accepted, paths);
        CompositionRunResult actual = await ExecuteAsync(accepted, swapped);

        Assert.True(expected.Succeeded, CompositionRunReportJson.Serialize(expected));
        Assert.True(actual.Succeeded, CompositionRunReportJson.Serialize(actual));
        Assert.Equal(expected.OutputSha256, actual.OutputSha256);
    }

    /// <summary>AB Merge executes retained immutable bytes when the accepted path later changes.</summary>
    [Fact]
    public async Task AbMergeAcceptedSessionDoesNotReopenSamePathAfterMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-content");
        Dictionary<string, string> paths = CreateAbInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptAbSession(paths);
        CompositionRunResult beforeMutation = await ExecuteAsync(accepted, paths);
        MutateFirstByte(paths[CompositionAddressSpaceIds.TpBInput]);

        CompositionRunResult afterMutation = await ExecuteAsync(accepted, paths);

        Assert.True(beforeMutation.Succeeded, CompositionRunReportJson.Serialize(beforeMutation));
        Assert.True(afterMutation.Succeeded, CompositionRunReportJson.Serialize(afterMutation));
        Assert.Equal(beforeMutation.OutputSha256, afterMutation.OutputSha256);
    }

    /// <summary>DP Replace ignores a client path alias after the session accepted canonical inputs.</summary>
    [Fact]
    public async Task DpReplaceAcceptedSessionIgnoresSwappedClientPath()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-dp-replace-accepted-path");
        Dictionary<string, string> paths = CreateDpReplaceInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptDpReplaceSession(paths);
        Dictionary<string, string> swapped = new(paths, StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceDp] = workspace.Write(
                "swapped-replacement.bin",
                File.ReadAllBytes(paths[CompositionSlotIds.ReplaceDp])),
        };

        CompositionRunResult expected = await ExecuteAsync(
            accepted,
            paths);
        CompositionRunResult actual = await ExecuteAsync(
            accepted,
            swapped);

        Assert.True(expected.Succeeded, expected.OutcomeStatus);
        Assert.True(actual.Succeeded, actual.OutcomeStatus);
        Assert.Equal(expected.OutputSha256, actual.OutputSha256);
    }

    /// <summary>DP Replace executes the immutable inspected bytes without reopening a changed path.</summary>
    [Fact]
    public async Task DpReplaceAcceptedSessionUsesInspectedBytesAfterPathMutation()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-dp-replace-accepted-content");
        Dictionary<string, string> paths = CreateDpReplaceInputs(workspace);
        ActiveSessionSnapshot accepted = AcceptDpReplaceSession(paths);
        MutateFirstByte(paths[CompositionSlotIds.ReplaceDp]);

        CompositionRunResult result = await ExecuteAsync(
            accepted,
            paths);

        Assert.True(result.Succeeded, result.OutcomeStatus);
        AuthoringInputSlotStatus replacement = accepted.InputSlotStatuses.Single(status =>
            StringComparer.Ordinal.Equals(
                status.AddressSpaceId,
                CompositionAddressSpaceIds.InitialCodeReplacement));
        CompositionOperation operation = accepted.ExactCapability!.CompiledComposition.Plan
            .OrderedOperations.Single(candidate => StringComparer.Ordinal.Equals(
                candidate.SourceSpaceId,
                CompositionAddressSpaceIds.InitialCodeReplacement));
        Assert.Equal(
            replacement.AcceptedBytes!.Value.Span[0],
            result.OutputBytes.Span[checked((int)operation.TargetRange.Start)]);
    }

    private ActiveSessionSnapshot AcceptStandardSession(
        Dictionary<string, string> paths)
    {
        var stamps = paths.ToDictionary(
            static pair => pair.Key,
            static pair => FileStamp.FromBytes(File.ReadAllBytes(pair.Value)),
            StringComparer.Ordinal);
        CompiledAuthoringSelectionSnapshot projection =
            _host.Services.StandardMergeAuthoring.GetAuthoringSnapshot(
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

    private ActiveSessionSnapshot AcceptAbSession(
        Dictionary<string, string> paths)
    {
        CompiledAuthoringSelectionSnapshot projection =
            _host.Services.AbMergeAuthoring.GetAuthoringSnapshot(
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

    private ActiveSessionSnapshot AcceptDpReplaceSession(
        Dictionary<string, string> paths)
    {
        var inspectionPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.ReferenceBase] = paths[CompositionSlotIds.ReplaceBase],
            [CompositionAddressSpaceIds.InitialCodeReplacement] = paths[CompositionSlotIds.ReplaceDp],
        };
        CompiledAuthoringSelectionSnapshot projection =
            _host.Services.DpReplaceAuthoring.GetAuthoringSnapshot(
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

    private ActiveSessionSnapshot AcceptSession(
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
        FirmwareInspectionSnapshotInput[] inputs =
        [
            .. paths.Select(pair => kind switch
            {
                FixedInspectionKind.StandardMerge => new FirmwareInspectionSnapshotInput(
                    pair.Key,
                    pair.Value,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    StandardMergeAddressSpaceId: pair.Key,
                    ExactCapability: exact),
                FixedInspectionKind.AbMerge => new FirmwareInspectionSnapshotInput(
                    pair.Key,
                    pair.Value,
                    AbMergeAddressSpaceId: pair.Key,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    ExactCapability: exact),
                FixedInspectionKind.DpReplace => new FirmwareInspectionSnapshotInput(
                    pair.Key,
                    pair.Value,
                    DpReplaceAddressSpaceId: pair.Key,
                    AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    ExactCapability: exact),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            }),
        ];
        IReadOnlyList<FirmwareInspectionSnapshotResult> inspected =
            BuiltInFirmwareInspection.InspectFirmwareBatch(_host.Canonical, icId, inputs);
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
            [CompositionSlotIds.ReplaceBase] = workspace.Write(
                "base.bin", CreatePattern(0x40000, 0x71)),
            [CompositionSlotIds.ReplaceDp] = workspace.Write(
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

    private ValueTask<CompositionRunResult> ExecuteAsync(
        ActiveSessionSnapshot session,
        IReadOnlyDictionary<string, string> paths)
    {
        return _host.Services.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                session,
                paths,
                build: false),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);
    }

    private void ReloadCatalog()
    {
        CapabilityCatalogReloadResult reload =
            _host.Catalog.Reload(
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
