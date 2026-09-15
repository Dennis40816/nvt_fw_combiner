using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Dummy naming is compiled-mode provenance, not a fabricated DP version.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputNamingDistinguishesDummyFromRealDp(bool dummy)
    {
        using var workspace = TempWorkspace.Create("ab-dummy-naming");
        Dictionary<string, string> paths = WriteInputs(workspace);
        CompiledAuthoringSessionPreparation prepared = BootstrapTestHost.Services.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51929", null,
            [.. paths.Where(pair => !dummy || pair.Key != CompositionAddressSpaceIds.DpAbInput)
                .Select(static pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))],
            dpMode: dummy ? AbMergeDpMode.Dummy : AbMergeDpMode.Normal);
        Assert.True(prepared.Succeeded);
        CompositionOutputPreparation output = await BootstrapTestHost.Services.CompositionOutputNaming
            .PrepareAutomaticOutputAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.True(output.OutputName.CanUseAutomaticName);
        OutputNamingSummary summary = Assert.IsType<OutputNamingSummary>(output.OutputName.OutputNaming);
        Assert.Matches(dummy
            ? "^NT51929_FlashCode_A_DummyT8100_B_DummyT8203_[0-9]{8}\\.bin$"
            : "^NT51929_FlashCode_A_D[0-9A-F]{4}T8100_B_D[0-9A-F]{4}T8203_[0-9]{8}\\.bin$",
            summary.AutomaticFileName);
        if (dummy)
        {
            Assert.All(summary.Tokens.Where(static token => token.TokenId is "dp-a" or "dp-b"), token =>
            {
                Assert.True(token.IsKnown);
                Assert.Null(token.SourceAddressSpaceId);
                Assert.Null(token.AcceptedSnapshotSha256);
                Assert.Equal("compiled-profile-dummy-dp;fill=0xFF", token.ParserId);
            });
            Assert.DoesNotContain(output.OutputName.Issues, static issue => issue.Code == "output-naming.metadata-unknown");
            _ = Assert.Single(output.OutputName.Issues, static issue => issue.Code == "output-naming.dummy-dp" &&
                issue.Severity == CompositionIssueSeverity.Info && issue.Message.Contains("0xFF", StringComparison.Ordinal));
            _ = paths.Remove(CompositionAddressSpaceIds.DpAbInput);
            CapabilityActionReadinessSnapshot? readiness = await BootstrapTestHost.Services.AbMergeAuthoring
                .GetActionReadinessAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
            CompositionRunResult result = await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(prepared.Snapshot!, paths, false,
                    previewOutputFileName: "customer.bin", actionReadiness: readiness),
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
            Assert.Equal("customer.bin", result.OutputNaming!.ActualFileName);
            Assert.True(result.OutputNaming.IsExplicitOverride);
            Assert.Equal(summary.Tokens.Where(static token => token.TokenId is "dp-a" or "dp-b"),
                result.OutputNaming.Tokens.Where(static token => token.TokenId is "dp-a" or "dp-b"));
            using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
            Assert.Contains("compiled-profile-dummy-dp;fill=0xFF", report.RootElement.GetRawText(), StringComparison.Ordinal);
            _ = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray(),
                static issue => issue.GetProperty("Code").GetString() == "output-naming.dummy-dp");
            string outputPath = workspace.PathFor("output/customer.bin");
            CompositionRunResult build = await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(prepared.Snapshot!, paths, true,
                    outputPath: outputPath, actionReadiness: readiness),
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.True(build.Succeeded, CompositionRunReportJson.Serialize(build));
            Assert.True(File.Exists(outputPath));
            Assert.Equal("customer.bin", build.OutputNaming!.ActualFileName);
            Assert.True(build.OutputNaming.IsExplicitOverride);
        }
    }

    /// <summary>Catalog rollover invalidates old AB inspection authority in both DP modes.</summary>
    [Theory]
    [InlineData(AbMergeDpMode.Normal)]
    [InlineData(AbMergeDpMode.Dummy)]
    public void DpModeInspectionRejectsPreviousCatalogPublication(AbMergeDpMode mode)
    {
        CompositionHostServices host = CompositionHostServices.Create();
        var owner = (AbMergeAuthoringExperience)host.AbMergeAuthoring;
        CompiledAuthoringSelectionSnapshot initial = owner.GetAuthoringSnapshot("NT51929", null, [],
            new Dictionary<string, FileStamp>(), new AuthoringRevision(1), dpMode: mode);
        ResolvedCapability? oldCapability = Assert.Single(initial.Catalog.Routes).ExactCapability;
        Assert.NotNull(oldCapability);
        CapabilityCatalogReloadResult reloaded = host.Catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(reloaded.Succeeded);
        Assert.NotEqual(oldCapability.ResolutionToken, reloaded.Snapshot!.ResolutionToken);
        int reads = 0;
        _ = Assert.Throws<InvalidOperationException>(() => owner.InspectInputSlotsCaptured("NT51929",
            [new FirmwareInspectionSnapshotInput("tp", "tp.bin",
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput, ExactCapability: oldCapability)],
            _ => { reads++; return CreateTpImage(0x81, 0); }, configuration: null));
        Assert.Equal(0, reads);
        CompiledAuthoringSelectionSnapshot current = owner.GetAuthoringSnapshot("NT51929", null, [],
            new Dictionary<string, FileStamp>(), new AuthoringRevision(1), dpMode: mode);
        Assert.Equal(reloaded.Snapshot.ResolutionToken, Assert.Single(current.Catalog.Routes).ExactCapability!.ResolutionToken);
    }

    /// <summary>Inspection started before a mode change cannot publish into the new contract.</summary>
    [Fact]
    public void DummyModeActivationRejectsEarlierNormalInspectionCompletion()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dummy-stale-inspection");
        Dictionary<string, string> paths = WriteInputs(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        var owner = (AbMergeAuthoringExperience)BootstrapTestHost.Services.AbMergeAuthoring;
        CompiledAuthoringSessionPreparation prepared = owner.PrepareSession(session, "NT51929", null,
            [.. paths.Select(static pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))]);
        Assert.True(prepared.Succeeded);
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(paths);
        Assert.True(started.Succeeded);
        AbMergeInspectionBatch oldInspection = owner.InspectInputSlotsCaptured("NT51929",
            [.. paths.Select(pair => new FirmwareInspectionSnapshotInput(pair.Key, pair.Value,
                AbMergeAddressSpaceId: pair.Key, AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                ExactCapability: started.Snapshot.ExactCapability))], File.ReadAllBytes, configuration: null);
        Dictionary<string, FileStamp> tpStamps = paths.Where(static pair => pair.Key != CompositionAddressSpaceIds.DpAbInput)
            .ToDictionary(static pair => pair.Key, static pair => FileStamp.FromBytes(File.ReadAllBytes(pair.Value)));
        CompiledAuthoringSelectionSnapshot dummy = owner.GetAuthoringSnapshot("NT51929", null,
            tpStamps.Keys, tpStamps, started.Snapshot!.AuthoringRevision,
            started.Snapshot, AbMergeDpMode.Dummy);
        Assert.True(session.Activate(dummy).Succeeded);
        ActiveSessionSnapshot after = session.CurrentSnapshot!;
        AuthoringSessionTransitionResult completed = session.TryCompleteSlotFileInspectionBatch(
            oldInspection.Catalog!, started.Leases, oldInspection.Statuses);
        Assert.False(completed.Succeeded);
        Assert.Same(after, session.CurrentSnapshot);
        Assert.DoesNotContain(after.ExactCapability!.CompiledComposition.V2Details.InputContract.Slots,
            static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.True(after.AuthoringRevision.Value > started.Snapshot.AuthoringRevision.Value);
    }

    /// <summary>A retained session cannot hide a mode change, even at the same authoring revision.</summary>
    [Theory]
    [InlineData(AbMergeDpMode.Normal, AbMergeDpMode.Dummy)]
    [InlineData(AbMergeDpMode.Dummy, AbMergeDpMode.Normal)]
    public void DpModeChangeDoesNotReusePreviousCompilation(AbMergeDpMode beforeMode, AbMergeDpMode afterMode)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dummy-mode-transition");
        Dictionary<string, string> paths = WriteInputs(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSelectedInput[] inputs = [.. paths
            .Where(pair => beforeMode == AbMergeDpMode.Normal || pair.Key != CompositionAddressSpaceIds.DpAbInput)
            .Select(static pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))];
        CompiledAuthoringSessionPreparation prepared = BootstrapTestHost.Services.AbMergeAuthoring.PrepareSession(
            session, "NT51929", null, inputs, beforeMode);
        Assert.True(prepared.Succeeded);
        ActiveSessionSnapshot before = prepared.Snapshot!;
        CompiledAuthoringSelectedInput[] retainedTp = [.. inputs.Where(static input =>
            input.SlotId != CompositionAddressSpaceIds.DpAbInput)];
        CompiledAuthoringSelectionSnapshot changed = BootstrapTestHost.Services.AbMergeAuthoring.GetAuthoringSnapshot(
            "NT51929", null, [.. retainedTp.Select(static input => input.SlotId)],
            retainedTp.ToDictionary(static input => input.SlotId, static input => FileStamp.FromBytes(input.Bytes!.Value.Span)),
            before.AuthoringRevision, before, afterMode);
        ResolvedCapability? after = Assert.Single(changed.Catalog.Routes).ExactCapability;
        Assert.NotNull(after);
        Assert.NotSame(before.ExactCapability, after);
        Assert.NotEqual(before.ExactCapability!.CompiledComposition.CompilationFingerprint,
            after.CompiledComposition.CompilationFingerprint);
        Assert.Equal(afterMode == AbMergeDpMode.Normal,
            after.CompiledComposition.V2Details.InputContract.Slots.Any(static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput));
    }

    /// <summary>A current publication does not authorize inspecting a different operator topology.</summary>
    [Theory]
    [InlineData("cascade")]
    [InlineData(null)]
    public void DummyInspectionRejectsMismatchedCurrentTopology(string? requestedToken)
    {
        var host = new IsolatedBootstrapTestHost();
        var owner = (AbMergeAuthoringExperience)host.Services.AbMergeAuthoring;
        ResolvedCapabilityRoute route = Assert.Single(host.Catalog.GetCurrentSnapshot().DynamicRoutes,
            static route => route.Identity.IcId == "NT51950" && route.Identity.WorkflowId == ExperienceIds.AbMerge &&
                route.Identity.MapVariant == "nt51950-ab-merge-maps" && route.Identity.IcCountVariant == "1-ic");
        Assert.True(host.Canonical.Compiler.TryCompilePublishedDynamicCapability(route.Identity, null, [],
            out _, out ResolvedCapability? capability, out _, route.AbMergeTopologyChoice!.Selection));
        Assert.NotNull(capability);
        int reads = 0;
        _ = Assert.Throws<InvalidOperationException>(() => owner.InspectInputSlotsCaptured("NT51950",
            [new FirmwareInspectionSnapshotInput("tp", "tp.bin",
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput,
                AbMergeTopologyToken: requestedToken, ExactCapability: capability)],
            _ => { reads++; return CreateTpImage(0x81, 0, length: 0x37000); }, configuration: null));
        Assert.Equal(0, reads);
    }

    /// <summary>Partial inspection must retain the accepted Dummy contract instead of recreating Normal.</summary>
    [Fact]
    public void DummyPartialInspectionKeepsAcceptedDpFreeCapability()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dummy-partial-inspection");
        Dictionary<string, string> paths = WriteInputs(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = BootstrapTestHost.Services.AbMergeAuthoring.PrepareSession(
            session, "NT51929", null,
            [.. paths.Where(static pair => pair.Key != CompositionAddressSpaceIds.DpAbInput)
                .Select(static pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))],
            AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        ActiveSessionSnapshot accepted = prepared.Snapshot!;
        var owner = (AbMergeAuthoringExperience)BootstrapTestHost.Services.AbMergeAuthoring;
        AbMergeInspectionBatch result = owner.InspectInputSlotsCaptured("NT51929",
            [new FirmwareInspectionSnapshotInput("tp", paths[CompositionAddressSpaceIds.TpAInput],
                AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput,
                ExactCapability: accepted.ExactCapability, AbMergeDpMode: AbMergeDpMode.Dummy)], File.ReadAllBytes, configuration: null);
        Assert.NotNull(result.Catalog);
        Assert.DoesNotContain(Assert.Single(result.Catalog.Routes).ExactCapability!.CompiledComposition
            .V2Details.InputContract.Slots, static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
    }

    /// <summary>Missing DP is accepted only by explicit Dummy mode, never by file absence alone.</summary>
    [Theory]
    [InlineData(AbMergeDpMode.Normal, false, false)]
    [InlineData(AbMergeDpMode.Normal, true, true)]
    [InlineData(AbMergeDpMode.Dummy, false, true)]
    [InlineData(AbMergeDpMode.Dummy, true, false)]
    public void ExplicitDpModeEnforcesSessionMembership(AbMergeDpMode mode, bool includeDp, bool accepted)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dummy-session");
        Dictionary<string, string> paths = WriteInputs(workspace);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation result = BootstrapTestHost.Services.AbMergeAuthoring.PrepareSession(
            session, "NT51929", null,
            [.. paths.Where(pair => includeDp || pair.Key != CompositionAddressSpaceIds.DpAbInput)
                .Select(static pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))],
            mode);

        Assert.Equal(accepted, result.Succeeded);
        if (accepted)
        {
            Assert.NotNull(result.Snapshot);
            Assert.Equal(includeDp ? 3 : 2,
                result.Snapshot.ExactCapability!.CompiledComposition.V2Details.InputContract.Slots.Count);
        }
        Assert.True(File.Exists(paths[CompositionAddressSpaceIds.DpAbInput]));
    }

    /// <summary>Undefined mode values cannot silently become a valid input policy.</summary>
    [Fact]
    public void UndefinedDpModeIsRejectedBeforeAuthoring()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BootstrapTestHost.Services.AbMergeAuthoring
            .GetAuthoringSnapshot("NT51929", null, [], new Dictionary<string, FileStamp>(),
                new AuthoringRevision(1), dpMode: (AbMergeDpMode)42));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BootstrapTestHost.Services.AbMergeAuthoring
            .PrepareSession(new AuthoringSessionState(ExperienceIds.AbMerge), "NT51929", null, [],
                (AbMergeDpMode)42));
    }
}
