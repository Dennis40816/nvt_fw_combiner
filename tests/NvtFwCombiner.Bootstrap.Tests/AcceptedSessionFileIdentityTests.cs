using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Accepted fixed-workflow sessions bind paths once and retain immutable inspected bytes.</summary>
public sealed partial class AcceptedSessionFileIdentityTests
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
        MutateFirstConsumedByte(
            accepted,
            CompositionAddressSpaceIds.TpInput,
            paths[CompositionAddressSpaceIds.TpInput]);

        CompositionRunResult afterMutation = await ExecuteAsync(accepted, paths);
        CompositionRunResult refreshed = await ExecuteAsync(AcceptStandardSession(paths), paths);

        Assert.True(beforeMutation.Succeeded, CompositionRunReportJson.Serialize(beforeMutation));
        Assert.True(afterMutation.Succeeded, CompositionRunReportJson.Serialize(afterMutation));
        Assert.Equal(beforeMutation.OutputSha256, afterMutation.OutputSha256);
        Assert.NotEqual(beforeMutation.OutputSha256, refreshed.OutputSha256);
        AssertAcceptedSourceByteProjected(
            accepted,
            afterMutation,
            CompositionAddressSpaceIds.TpInput);
    }

    /// <summary>One accepted immutable path may supply multiple Standard Merge logical bindings.</summary>
    [Fact]
    public async Task StandardMergeAcceptedSessionSharesOnePathAcrossLogicalSlots()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-standard-accepted-shared-path");
        Dictionary<string, string> paths = CreateStandardInputs(workspace);
        paths[CompositionAddressSpaceIds.TpInput] = paths[CompositionAddressSpaceIds.DpInput];
        ActiveSessionSnapshot accepted = AcceptStandardSession(paths);
        string outputPath = workspace.PathFor("shared-output.bin");

        CompositionRunResult result = await ExecuteAsync(
            accepted,
            paths,
            build: true,
            outputPath: outputPath);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(result.OutputBytes.ToArray(), File.ReadAllBytes(outputPath));
        Assert.Equal(
            [CompositionAddressSpaceIds.DpInput, CompositionAddressSpaceIds.TpInput],
            result.Report.Inputs.Select(static input => input.AddressSpaceId).Order(StringComparer.Ordinal));
        _ = Assert.Single(result.Report.Inputs.Select(static input => input.Sha256).Distinct(StringComparer.Ordinal));
        Assert.Contains(result.Report.Operations, static operation =>
            operation.SourceSpaceId == CompositionAddressSpaceIds.DpInput);
        Assert.Contains(result.Report.Operations, static operation =>
            operation.SourceSpaceId == CompositionAddressSpaceIds.TpInput);
        AssertOperationProjection(accepted, result);
    }

    /// <summary>Sharing one path is byte-for-byte equivalent to two paths containing identical accepted bytes.</summary>
    [Fact]
    public async Task StandardMergeSharedPathMatchesDistinctIdenticalFiles()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-standard-accepted-path-parity");
        Dictionary<string, string> sharedPaths = CreateStandardInputs(workspace);
        sharedPaths[CompositionAddressSpaceIds.TpInput] =
            sharedPaths[CompositionAddressSpaceIds.DpInput];
        byte[] sharedBytes = File.ReadAllBytes(sharedPaths[CompositionAddressSpaceIds.DpInput]);
        var distinctPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = workspace.Write("distinct-dp.bin", sharedBytes),
            [CompositionAddressSpaceIds.TpInput] = workspace.Write("distinct-tp.bin", sharedBytes),
        };

        ActiveSessionSnapshot sharedSession = AcceptStandardSession(sharedPaths);
        ActiveSessionSnapshot distinctSession = AcceptStandardSession(distinctPaths);
        CompositionRunResult shared = await ExecuteAsync(sharedSession, sharedPaths);
        CompositionRunResult distinct = await ExecuteAsync(distinctSession, distinctPaths);

        Assert.True(shared.Succeeded, CompositionRunReportJson.Serialize(shared));
        Assert.True(distinct.Succeeded, CompositionRunReportJson.Serialize(distinct));
        Assert.Equal(distinct.OutputSha256, shared.OutputSha256);
        Assert.Equal(distinct.OutputBytes.ToArray(), shared.OutputBytes.ToArray());
        Assert.Equal(
            distinct.Report.Inputs.Select(static input => input.AddressSpaceId).Order(StringComparer.Ordinal),
            shared.Report.Inputs.Select(static input => input.AddressSpaceId).Order(StringComparer.Ordinal));
        AssertOperationProjection(sharedSession, shared);
        AssertOperationProjection(distinctSession, distinct);
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
        MutateFirstConsumedByte(
            accepted,
            CompositionAddressSpaceIds.TpBInput,
            paths[CompositionAddressSpaceIds.TpBInput]);

        CompositionRunResult afterMutation = await ExecuteAsync(accepted, paths);
        CompositionRunResult refreshed = await ExecuteAsync(AcceptAbSession(paths), paths);

        Assert.True(beforeMutation.Succeeded, CompositionRunReportJson.Serialize(beforeMutation));
        Assert.True(afterMutation.Succeeded, CompositionRunReportJson.Serialize(afterMutation));
        Assert.Equal(beforeMutation.OutputSha256, afterMutation.OutputSha256);
        Assert.NotEqual(beforeMutation.OutputSha256, refreshed.OutputSha256);
        AssertAcceptedSourceByteProjected(
            accepted,
            afterMutation,
            CompositionAddressSpaceIds.TpBInput);
    }

    /// <summary>One accepted immutable TP path may supply independent AB TPA and TPB bindings.</summary>
    [Fact]
    public async Task AbMergeAcceptedSessionSharesOneTpPathAcrossLogicalSlots()
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-shared-tp-path");
        Dictionary<string, string> paths = CreateAbInputs(workspace);
        paths[CompositionAddressSpaceIds.TpBInput] = paths[CompositionAddressSpaceIds.TpAInput];
        ActiveSessionSnapshot accepted = AcceptAbSession(paths);

        CompositionRunResult result = await ExecuteAsync(accepted, paths);

        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        Assert.Equal(3, result.Report.Inputs.Count);
        InputArtifactSummary tpA = Assert.Single(result.Report.Inputs, static input =>
            input.AddressSpaceId == CompositionAddressSpaceIds.TpAInput);
        InputArtifactSummary tpB = Assert.Single(result.Report.Inputs, static input =>
            input.AddressSpaceId == CompositionAddressSpaceIds.TpBInput);
        Assert.Equal(tpA.Sha256, tpB.Sha256);
        Assert.NotEqual(tpA.ArtifactId, tpB.ArtifactId);
        Assert.Contains(
            CompositionAddressSpaceIds.TpAInput,
            accepted.ExactCapability!.CompiledComposition.Plan.RequiredInputAddressSpaceIds);
        Assert.Contains(
            CompositionAddressSpaceIds.TpBInput,
            accepted.ExactCapability.CompiledComposition.Plan.RequiredInputAddressSpaceIds);
        AssertOperationProjection(accepted, result);
    }

    /// <summary>One reader locator must fail closed when retained logical slots disagree on stamp or bytes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedSessionRejectsConflictingSnapshotsForOnePath(bool conflictStamp)
    {
        ReloadCatalog();
        using var workspace = TempWorkspace.Create("nfc-ab-accepted-conflicting-snapshot");
        Dictionary<string, string> paths = CreateAbInputs(workspace);
        paths[CompositionAddressSpaceIds.TpBInput] = paths[CompositionAddressSpaceIds.TpAInput];
        ActiveSessionSnapshot accepted = AcceptAbSession(paths);
        ActiveSessionSnapshot conflicting = CreateConflictingSnapshot(
            accepted,
            CompositionAddressSpaceIds.TpBInput,
            conflictStamp);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AcceptedSessionExecutionInputs.CreateBindings(
                conflicting.ExactCapability!.CompiledComposition,
                conflicting));

        Assert.Contains("conflicting accepted snapshots", exception.Message, StringComparison.Ordinal);
        Assert.Contains(CompositionAddressSpaceIds.TpBInput, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(paths[CompositionAddressSpaceIds.TpAInput], exception.Message, StringComparison.Ordinal);
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

    private static ActiveSessionSnapshot CreateConflictingSnapshot(
        ActiveSessionSnapshot accepted,
        string addressSpaceId,
        bool conflictStamp)
    {
        AuthoringInputSlotStatus originalStatus = accepted.InputSlotStatuses.Single(status =>
            StringComparer.Ordinal.Equals(status.AddressSpaceId, addressSpaceId));
        byte[] conflictingBytes = originalStatus.AcceptedBytes!.Value.ToArray();
        if (!conflictStamp)
        {
            conflictingBytes[0] ^= 0xFF;
        }
        FileStamp conflictingStamp = conflictStamp
            ? new FileStamp(conflictingBytes.LongLength, new string('0', 64))
            : originalStatus.FileStamp!.Value;
        AuthoringInputSlotStatus conflictingStatus = new(
            accepted.ExactCapability!.Identity,
            originalStatus.ResolutionToken,
            originalStatus.AuthoringRevision,
            originalStatus.CapabilityFingerprint,
            originalStatus.CompilationFingerprint,
            originalStatus.SelectionReadiness,
            originalStatus.AddressSpaceId,
            originalStatus.InspectionLifecycle,
            conflictingStamp,
            originalStatus.Inspection,
            originalStatus.SelectedPathHint,
            originalStatus.Observation,
            conflictingBytes);
        AuthoringSlotState[] slots =
        [
            .. accepted.Slots.Select(slot =>
                StringComparer.Ordinal.Equals(slot.DefinitionId, originalStatus.SlotId)
                    ? new AuthoringSlotState(
                        slot.DefinitionId,
                        slot.SelectedPath,
                        conflictingStamp,
                        slot.Lifecycle,
                        slot.BlockingIssue,
                        conflictingBytes)
                    : slot),
        ];
        AuthoringInputSlotStatus[] statuses =
        [
            .. accepted.InputSlotStatuses.Select(status =>
                StringComparer.Ordinal.Equals(status.SlotId, originalStatus.SlotId)
                    ? conflictingStatus
                    : status),
        ];
        return new ActiveSessionSnapshot(
            accepted.WorkflowId,
            accepted.ResolutionToken,
            accepted.AuthoringRevision,
            accepted.SelectedRouteId,
            accepted.CapabilityFingerprint,
            accepted.ExecutionAdmitted,
            accepted.SelectedIc,
            accepted.SelectedIcCount,
            accepted.SelectedMapVariant,
            accepted.IcChoices,
            accepted.IcCountChoices,
            slots,
            accepted.DraftState,
            accepted.DraftCapabilityFingerprint,
            accepted.DerivedPublications,
            accepted.CompilationFingerprint,
            accepted.ExactCapability,
            statuses,
            accepted.InputSelectionReadiness,
            accepted.MetadataInspection);
    }

    private static void AssertOperationProjection(
        ActiveSessionSnapshot accepted,
        CompositionRunResult result)
    {
        (string OperationId, string? SourceSpaceId, ByteRange? SourceRange,
            string TargetSpaceId, ByteRange TargetRange)[] expected =
        [
            .. accepted.ExactCapability!.CompiledComposition.Plan.OrderedOperations.Select(
                static operation => (
                    operation.OperationId,
                    operation.SourceSpaceId,
                    operation.SourceRange,
                    operation.TargetSpaceId,
                    operation.TargetRange)),
        ];
        (string OperationId, string? SourceSpaceId, ByteRange? SourceRange,
            string TargetSpaceId, ByteRange TargetRange)[] actual =
        [
            .. result.Report.Operations.Select(static operation => (
                operation.OperationId,
                operation.SourceSpaceId,
                operation.SourceRange,
                operation.TargetSpaceId,
                operation.TargetRange)),
        ];
        Assert.Equal(expected, actual);
    }

    private static void AssertAcceptedSourceByteProjected(
        ActiveSessionSnapshot accepted,
        CompositionRunResult result,
        string addressSpaceId)
    {
        AuthoringInputSlotStatus status = accepted.InputSlotStatuses.Single(candidate =>
            candidate.AddressSpaceId == addressSpaceId);
        string operationSource = addressSpaceId == CompositionAddressSpaceIds.TpBInput
            ? CompositionAddressSpaceIds.TpBWork
            : addressSpaceId;
        CompositionOperation operation = accepted.ExactCapability!.CompiledComposition.Plan
            .OrderedOperations.Last(candidate => candidate.SourceSpaceId == operationSource);
        Assert.Equal(
            status.AcceptedBytes!.Value.Span[checked((int)operation.SourceRange!.Value.Start)],
            result.OutputBytes.Span[checked((int)operation.TargetRange.Start)]);
    }

    private static void MutateFirstByte(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[0] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private static void MutateFirstConsumedByte(
        ActiveSessionSnapshot accepted,
        string addressSpaceId,
        string path)
    {
        string operationSource = addressSpaceId == CompositionAddressSpaceIds.TpBInput
            ? CompositionAddressSpaceIds.TpBWork
            : addressSpaceId;
        CompositionOperation operation = accepted.ExactCapability!.CompiledComposition.Plan
            .OrderedOperations.Last(candidate => candidate.SourceSpaceId == operationSource);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[checked((int)operation.SourceRange!.Value.Start)] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private ValueTask<CompositionRunResult> ExecuteAsync(
        ActiveSessionSnapshot session,
        IReadOnlyDictionary<string, string> paths,
        bool build = false,
        string? outputPath = null)
    {
        return _host.Services.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                session,
                paths,
                build,
                outputPath),
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

    private sealed class PassThroughProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(
            ExternalProcessorRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
        }
    }
}
