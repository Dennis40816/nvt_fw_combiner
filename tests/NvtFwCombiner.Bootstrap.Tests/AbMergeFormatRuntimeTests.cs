using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using System.Text.Json;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Infrastructure.Time;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>The real host must apply its saved configuration to AB preparation, not only to a pure helper.</summary>
public sealed class AbMergeFormatRuntimeTests
{
    /// <summary>Fresh installations and deletion of a custom override build with defaults without Save.</summary>
    [Theory]
    [InlineData(false, 0x97, "desay")]
    [InlineData(false, 0x84, "common")]
    [InlineData(true, 0x97, "desay")]
    public async Task MissingConfigurationBuildsAbInputsWithoutSavingAsync(bool deleteOverride, byte format, string expected)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-first-use-defaults");
        string path = workspace.PathFor("format.json");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((NvtFwCombiner.Application.ExternalTools.IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: path);
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        if (deleteOverride)
        {
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [], AliasName = "Custom" })],
                TestContext.Current.CancellationToken)).Succeeded);
            File.Delete(path);
        }
        string a = workspace.Write("a.bin", CreateTp(format, 1));
        string b = workspace.Write("b.bin", CreateTp(format, 1));
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("tp-a-input", a, File.ReadAllBytes(a)), new("tp-b-input", b, File.ReadAllBytes(b))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Message)));
        NvtFwCombiner.Application.Composition.CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.Equal(expected, proposal.Confirmation!.Format!.FormatId);
        CapabilityActionReadinessSnapshot? readiness = await host.AbMergeAuthoring.GetActionReadinessAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        string output = workspace.PathFor("output.bin");
        CompositionRunResult run = await host.CompositionExecution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(prepared.Snapshot!,
                new Dictionary<string, string> { ["tp-a-input"] = a, ["tp-b-input"] = b },
                build: true, outputPath: output, actionReadiness: readiness),
            new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
        Assert.True(run.Succeeded, CompositionRunReportJson.Serialize(run));
        Assert.True(File.Exists(output));
        Assert.Equal(expected, run.Report.AbMergeFormat!.FormatId);
        Assert.True(configuration.Current.UsesBuiltInDefaults);
        Assert.False(File.Exists(path));
    }

    /// <summary>Cascade's selected lower bound is not presented as an observed exact count for independent A/B banks.</summary>
    [Theory]
    [InlineData(0x97, 3, TopologyRequirementKind.Cascade)]
    [InlineData(0x84, 2, TopologyRequirementKind.ExactCount)]
    public async Task OutputConfirmationRetainsCompiledTopologyConstraintAsync(byte format, byte bCount, TopologyRequirementKind expectedKind)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-topology");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "cascade",
            [new("tp-a-input", workspace.PathFor("a.bin"), CreateTp(format, 2)), new("tp-b-input", workspace.PathFor("b.bin"), CreateTp(format, bCount))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        TopologyRequirement requirement = Assert.IsType<TopologyRequirement>(proposal.Confirmation!.TopologyRequirement);
        Assert.Equal(expectedKind, requirement.Kind);
        Assert.Equal(expectedKind == TopologyRequirementKind.ExactCount ? 2 : null, requirement.ExactChipCount);
        Assert.Same(prepared.Snapshot!.ExactCapability!.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.Applicability.TopologyRequirement,
            requirement);
    }

    /// <summary>Confirmation retains real output capacity and per-binding primary evidence without reopening or double-counting delivery sources.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputConfirmationKeepsPerBindingFactsAndExactOutputSizeAsync(bool sameFile)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-confirmation-facts");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        string a = workspace.PathFor("never-created-a.bin");
        string b = sameFile ? a : workspace.PathFor("never-created-b.bin");
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(session, "NT51950", "single",
            [new("tp-a-input", a, CreateTp(0x97, 1)), new("tp-b-input", b, CreateTp(sameFile ? (byte)0x97 : (byte)0xA6, 1))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(
            prepared.Snapshot!, TestContext.Current.CancellationToken);
        CompositionOutputConfirmationSummary summary = Assert.IsType<CompositionOutputConfirmationSummary>(proposal.Confirmation);
        Assert.Equal("NT51950", summary.IcId);
        Assert.Equal(ExperienceIds.AbMerge, summary.WorkflowId);
        Assert.Equal(1, summary.Topology!.ChipCount);
        Assert.Equal(0x100000, summary.OutputLengthBytes);
        Assert.Equal("desay", summary.Format!.FormatId);
        Assert.Equal("Desay", summary.Format.DisplayName);
        Assert.True(summary.HasGeneratedInputs);
        Assert.Equal(sameFile ? 1 : 2, proposal.Sources.Count);
        Assert.Equal(2, summary.Inputs.Count);
        Assert.Equal<string>(["tp-a-input", "tp-b-input"], summary.Inputs.Select(static input => input.BindingId));
        Assert.Equal<byte>([0x97, sameFile ? (byte)0x97 : (byte)0xA6], summary.Inputs.Select(static input => input.EventBufferFormat!.RawByte));
        Assert.All(summary.Inputs, input =>
        {
            AuthoringInputSlotStatus acceptedStatus = prepared.Snapshot!.InputSlotStatuses.Single(status => status.AddressSpaceId == input.BindingId);
            Assert.Equal(0x37000, input.SizeBytes);
            Assert.Equal(acceptedStatus.Inspection!.ExpectedOuterLengths, input.ExpectedLengths);
            Assert.Same(acceptedStatus.Inspection, input.Inspection);
            Assert.Equal(acceptedStatus.FileStamp!.Value.Sha256, input.Sha256);
            Assert.Equal(acceptedStatus.InspectionAdvisories, input.InspectionAdvisories);
            Assert.Equal(input.BindingId, input.EventBufferFormat!.ArtifactIdentity.ArtifactId);
            Assert.Equal(0x22200, input.EventBufferFormat.PrimaryRange.Range.Start);
            Assert.Equal(summary.Format.ConfigurationSourceSha256, input.EventBufferFormat.ConfigurationSourceSha256);
        });
        Assert.Equal(host.CompositionOutputNaming.ResolveAcceptedOutput(prepared.Snapshot!).OutputName.FileName,
            proposal.OutputPreparation.OutputName.FileName);
        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
        Assert.True(await host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, TestContext.Current.CancellationToken));
        Assert.True(await host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, TestContext.Current.CancellationToken));
    }

    /// <summary>Alias-only and rule changes both invalidate an open confirmation; unchanged reload generations do not.</summary>
    [Theory]
    [InlineData("alias")]
    [InlineData("rules")]
    public async Task OutputConfirmationRejectsChangedConfigurationAsync(string change)
    {
        using TempWorkspace workspace = TempWorkspace.Create("output-confirmation-current");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("tp-a-input", workspace.PathFor("a.bin"), CreateTp(0x97, 1)), new("tp-b-input", workspace.PathFor("b.bin"), CreateTp(0xA6, 1))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.True(await host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, TestContext.Current.CancellationToken));
        Assert.True((await configuration.SaveAsync([.. configuration.CreateDefaultsDraft().Select(entry => change == "alias"
            ? entry! with { AliasName = "Customer alias" } : entry! with { RecognitionValues = [] })],
            TestContext.Current.CancellationToken)).Succeeded);
        Assert.False(await host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, TestContext.Current.CancellationToken));
        Assert.Equal("Desay", proposal.Confirmation!.Format!.DisplayName);
        if (change == "alias")
        {
            CompositionOutputBundleProposal refreshed = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
            Assert.Equal("Customer alias", refreshed.Confirmation!.Format!.DisplayName);
        }
    }

    /// <summary>Legacy AB does not invent a format or include the optional A output in Flash output size.</summary>
    [Fact]
    public async Task LegacyOutputConfirmationKeepsFormatAbsentAndAdditionalDeliverySeparateAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("legacy-confirmation");
        CompositionHostServices host = CompositionHostServices.Create();
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", workspace.PathFor("a.bin"), new byte[0x40000]), new("tp-b-input", workspace.PathFor("b.bin"), new byte[0x40000])], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.Null(proposal.Confirmation!.Format);
        Assert.Equal(0x80000, proposal.Confirmation.OutputLengthBytes);
        Assert.All(proposal.Confirmation.Inputs, static input => Assert.Null(input.EventBufferFormat));
        Assert.NotEmpty(proposal.OutputPreparation.AdditionalDeliveries);
        Assert.True(await host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, TestContext.Current.CancellationToken));
    }

    /// <summary>DP actual/expected source lengths and warnings stay in the input facts, never replacing Flash output length.</summary>
    [Theory]
    [InlineData(0x100000)]
    [InlineData(0x100010)]
    public async Task OutputConfirmationPreservesDpInspectionExpectationsAsync(int dpLength)
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-dp-expectation");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("dp-ab-input", workspace.PathFor("dp.bin"), new byte[dpLength]),
             new("tp-a-input", workspace.PathFor("a.bin"), CreateTp(0x97, 1)), new("tp-b-input", workspace.PathFor("b.bin"), CreateTp(0xA6, 1))],
            AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        CompositionOutputInputSummary dp = Assert.Single(proposal.Confirmation!.Inputs, static input => input.SlotId == "dp-ab-input");
        Assert.Equal(dpLength, dp.SizeBytes);
        Assert.Equal<long>([0x100000], dp.ExpectedLengths);
        Assert.Equal(0x100000, proposal.Confirmation.OutputLengthBytes);
        Assert.Null(dp.EventBufferFormat);
        Assert.False(proposal.Confirmation.HasGeneratedInputs);
        Assert.Equal(3, proposal.Sources.Count);
        if (dpLength != 0x100000) { Assert.Equal(AuthoringSlotLifecycle.Warning, dp.InspectionLifecycle); }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, cancellation.Token).AsTask());
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            host.CompositionOutputNaming.IsProposalCurrentAsync(proposal, cancellation.Token).AsTask());
    }

    /// <summary>Only a successfully admitted primary format is projected, with independent A/B input provenance.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("unsaved")]
    [InlineData("invalid")]
    [InlineData("short-primary")]
    [InlineData("mismatch")]
    public async Task InputFormatFactsRequireSuccessfulAdmissionAsync(string state)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-input-format-facts");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        if (state == "invalid") { _ = workspace.Write("format.json", "invalid"u8.ToArray()); }
        if (state is not ("unsaved" or "invalid"))
        {
            Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        }
        byte[] a = state == "short-primary" ? new byte[0x22200] : CreateTp(0x97, 1);
        byte[] b = CreateTp(state == "mismatch" ? (byte)0x84 : (byte)0xA6, 1);
        AbMergeInspectionBatch result = await ((AbMergeAuthoringExperience)host.AbMergeAuthoring).InspectInputSlotsAsync("NT51950",
            [new("a", "a.bin", AbMergeAddressSpaceId: "tp-a-input", AbMergeTopologyToken: "single", AbMergeDpMode: AbMergeDpMode.Dummy),
             new("b", "b.bin", AbMergeAddressSpaceId: "tp-b-input", AbMergeTopologyToken: "single", AbMergeDpMode: AbMergeDpMode.Dummy)],
            path => path == "a.bin" ? a : b, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Facts.Count);
        if (state is not ("valid" or "unsaved"))
        {
            Assert.NotEmpty(result.Issues);
            Assert.All(result.Facts.Values, static facts => Assert.Null(facts.EventBufferFormat));
            return;
        }
        Assert.Empty(result.Issues);
        foreach ((string id, string space, byte raw, byte[] bytes) in new[]
        {
            ("a", "tp-a-input", (byte)0x97, a), ("b", "tp-b-input", (byte)0xA6, b),
        })
        {
            EventBufferFormatObservation format = Assert.IsType<EventBufferFormatObservation>(result.Facts[id].EventBufferFormat);
            Assert.Equal(raw, format.RawByte);
            Assert.Equal("desay", format.FormatId);
            Assert.Equal("Desay", format.DisplayName);
            Assert.Equal(space, format.ArtifactIdentity.ArtifactId);
            Assert.Equal(FileStamp.FromBytes(bytes).Sha256, format.ArtifactIdentity.Sha256);
            Assert.Equal(0x22200, format.PrimaryRange.Range.Start);
            Assert.Contains("primary", format.PrimaryStructureId, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(configuration.Current.SourceSha256, format.ConfigurationSourceSha256);
        }
    }

    /// <summary>Retained source bytes are not admission: saving Config must still require every declared input.</summary>
    [Theory]
    [InlineData("complete")]
    [InlineData("missing-tp")]
    [InlineData("missing-dp")]
    [InlineData("unreadable")]
    public async Task FirstConfigurationSavePreservesInputRequirementsAsync(string inputState)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-source-retention");
        _ = workspace.Write("format.json", "invalid"u8.ToArray());
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        AbMergeDpMode mode = inputState == "missing-dp" ? AbMergeDpMode.Normal : AbMergeDpMode.Dummy;
        CompiledAuthoringSelectionSnapshot declaration = host.AbMergeAuthoring.GetAuthoringSnapshot("NT51950", "single",
            [], new Dictionary<string, FileStamp>(), new AuthoringRevision(1), dpMode: mode);
        Assert.True(session.Activate(declaration.Catalog).Succeeded);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tp-a-input"] = workspace.Write("a.bin", CreateTp(0x97, 1)),
        };
        if (inputState != "missing-tp")
        {
            paths.Add("tp-b-input", inputState == "unreadable"
                ? workspace.PathFor("absent-b.bin") : workspace.Write("b.bin", CreateTp(0x97, 1)));
        }
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(paths);
        Assert.True(started.Succeeded);
        FirmwareInspectionBatchResult inspected = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51950",
            [.. paths.Select(pair => new FirmwareInspectionSnapshotInput(pair.Key, pair.Value,
                AbMergeAddressSpaceId: pair.Key, AbMergeTopologyToken: "single", AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                AbMergeDpMode: mode))], TestContext.Current.CancellationToken);
        AuthoringCapabilityCatalogSnapshot catalog = inspected.InspectionsById["tp-a-input"].InputSlotCatalog!;
        Dictionary<string, AuthoringInputSlotStatus> statuses = inspected.InspectionsById.ToDictionary(
            static pair => pair.Key, static pair => pair.Value.InputSlotStatus!, StringComparer.Ordinal);
        Assert.All(statuses.Values, static status =>
        {
            Assert.Null(status.AcceptedBytes);
            Assert.Null(status.CompilationFingerprint);
        });
        Assert.NotNull(statuses["tp-a-input"].CapturedSource);
        if (inputState == "unreadable") { Assert.Null(statuses["tp-b-input"].CapturedSource); }
        Assert.True(host.AbMergeAuthoring.AdoptInspectedBatch(session, catalog, started.Leases, statuses).Succeeded);
        Assert.False(session.CurrentSnapshot!.HasCurrentInputInspection);
        Assert.Null(session.CurrentSnapshot.ExactCapability);
        if (inputState == "complete")
        {
            CapabilityActionBlocker blocker = Assert.Single(ActiveSessionBuildBlockerResolver.ResolveBuildAvailability(
                session.CurrentSnapshot, ExperienceIds.AbMerge).Blockers);
            Assert.Equal(CapabilityReadinessDimension.Configuration, blocker.Dimension);
            Assert.Equal(CapabilityReadinessNextAction.ReviewConfiguration, blocker.NextAction);
            Assert.Equal("event-buffer-format", blocker.SubjectId);
        }
        foreach (string path in paths.Values) { File.Delete(path); }
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation result = Assert.IsType<CompiledAuthoringSessionPreparation>(
            await host.AbMergeAuthoring.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken));
        Assert.Equal(inputState == "complete", result.Succeeded);
        if (inputState == "complete")
        {
            Assert.True(session.CurrentSnapshot!.HasCurrentInputInspection);
            Assert.All(session.CurrentSnapshot.InputSlotStatuses, static status =>
            {
                _ = Assert.NotNull(status.AcceptedBytes);
                Assert.Null(status.CapturedSource);
            });
        }
        else
        {
            Assert.False(session.CurrentSnapshot!.HasCurrentInputInspection);
            Assert.Null(session.CurrentSnapshot.GetAcceptedCapability(AuthoringDerivedResultKind.Preview));
            if (inputState == "missing-dp")
            {
                Assert.Contains(session.CurrentSnapshot.Slots, static slot => slot.DefinitionId == "dp-ab-input" && slot.SelectedPath is null);
            }
        }
    }

    /// <summary>Pre-compilation source retention owns a copy, never a mutable caller buffer or executable bytes.</summary>
    [Fact]
    public async Task PreCompilationSourceCaptureIsImmutableAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-source-copy");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        byte[] source = CreateTp(0x97, 1);
        byte[] expected = [.. source];
        AbMergeInspectionBatch inspected = await ((AbMergeAuthoringExperience)host.AbMergeAuthoring).InspectInputSlotsAsync("NT51950",
            [new("tp-a-input", "a.bin", AbMergeAddressSpaceId: "tp-a-input", AbMergeTopologyToken: "single", AbMergeDpMode: AbMergeDpMode.Dummy)],
            _ => source, TestContext.Current.CancellationToken);
        Array.Fill(source, (byte)0xFF);
        AuthoringInputSlotStatus status = inspected.Statuses["tp-a-input"];
        Assert.Equal(expected, status.CapturedSource!.AcceptedBytes!.Value.ToArray());
        Assert.Equal(FileStamp.FromBytes(expected), status.CapturedSource.FileStamp);
        Assert.Null(status.AcceptedBytes);
        Assert.Null(status.CompilationFingerprint);
        Assert.Null(Assert.Single(inspected.Catalog!.Routes).ExactCapability);
    }

    /// <summary>A run past format capture retains its original bytes and exact map; a later run rejects the obsolete request.</summary>
    [Fact]
    public async Task ConfigurationSaveDuringExecutionKeepsCapturedOutputAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-running-capture");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((NvtFwCombiner.Application.ExternalTools.IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(
            [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { AliasName = "Before Save" })],
            TestContext.Current.CancellationToken)).Succeeded);
        string a = workspace.Write("a.bin", CreateTp(0x97, 2));
        string b = workspace.Write("b.bin", CreateTp(0xA6, 2));
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(session, "NT51950", "cascade",
            [new("tp-a-input", a, File.ReadAllBytes(a)), new("tp-b-input", b, File.ReadAllBytes(b))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CapabilityActionReadinessSnapshot? readiness = await host.AbMergeAuthoring.GetActionReadinessAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        var request = new AcceptedCompositionExecutionRequest(prepared.Snapshot!,
            new Dictionary<string, string> { ["tp-a-input"] = a, ["tp-b-input"] = b }, build: false,
            previewOutputFileName: "captured.bin", actionReadiness: readiness);
        CompositionRunResult baseline = await host.CompositionExecution.ExecuteAsync(request,
            new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
        Assert.True(baseline.Succeeded, CompositionRunReportJson.Serialize(baseline));
        using (JsonDocument json = JsonDocument.Parse(CompositionRunReportJson.Serialize(baseline)))
        {
            Assert.True(json.RootElement.TryGetProperty("AbMergeFormat", out _),
                "A format-admitted run must retain its execution-time format evidence.");
        }
        AbMergeFormatRunSummary before = Assert.IsType<AbMergeFormatRunSummary>(baseline.Report.AbMergeFormat);
        Assert.Equal("desay", before.FormatId);
        Assert.Equal("Before Save", before.DisplayName);
        Assert.Equal(0x97, before.TpA.FormatByte);
        Assert.Equal(0xA6, before.TpB.FormatByte);
        using var destination = new HeldFormatDestination(TestContext.Current.CancellationToken);
        var executor = new CompositionExecutionExperience(host.Catalog, destination, () =>
        {
            ExternalProcessorEnvironmentLease lease = environment.AcquireCurrent();
            return new CompositionExternalProcessorLease(lease.Generation, lease.Processor);
        }, environment.IsCurrent, new SystemClock(), (AbMergeAuthoringExperience)host.AbMergeAuthoring);
        Task<CompositionRunResult> running = Task.Run(async () => await executor.ExecuteAsync(request,
            new CompositionRunProgressFeed(), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        try
        {
            await destination.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            EventBufferFormatConfigurationState captured = configuration.Current;
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [], AliasName = "After Save" })],
                TestContext.Current.CancellationToken)).Succeeded);
            CompiledAuthoringSessionPreparation updated = Assert.IsType<CompiledAuthoringSessionPreparation>(
                await host.AbMergeAuthoring.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken));
            Assert.True(updated.Succeeded);
            Assert.Equal("nt51950-ab-common-2ic-maps", updated.Snapshot!.ExactCapability!.Identity.MapVariant);
            destination.Release.Set();
            CompositionRunResult completed = await running;
            Assert.True(completed.Succeeded, CompositionRunReportJson.Serialize(completed));
            Assert.Equal(baseline.OutputBytes.ToArray(), completed.OutputBytes.ToArray());
            Assert.Same(prepared.Snapshot!.ExactCapability, completed.ResolvedCapability);
            Assert.NotSame(updated.Snapshot.ExactCapability, completed.ResolvedCapability);
            AbMergeFormatRunSummary summary = Assert.IsType<AbMergeFormatRunSummary>(completed.Report.AbMergeFormat);
            Assert.Equal(captured.Generation, summary.ConfigurationGeneration);
            Assert.Equal(captured.SourceSha256, summary.ConfigurationSourceSha256);
            Assert.Equal("Before Save", summary.DisplayName);
            Assert.Equal("desay", summary.FormatId);
            Assert.Equal(0x97, summary.TpA.FormatByte);
            Assert.Equal(0xA6, summary.TpB.FormatByte);
            Assert.NotEqual(configuration.Current.SourceSha256, summary.ConfigurationSourceSha256);
            using JsonDocument completedJson = JsonDocument.Parse(CompositionRunReportJson.Serialize(completed));
            JsonElement formatJson = completedJson.RootElement.GetProperty("AbMergeFormat");
            Assert.Equal(JsonSerializer.Serialize(summary), JsonSerializer.Serialize(formatJson));
            Assert.DoesNotContain(workspace.PathFor(""), formatJson.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ArtifactId", formatJson.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Bytes", formatJson.ToString(), StringComparison.Ordinal);
            CompositionPreRunRefusalException next = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(() =>
                host.CompositionExecution.ExecuteAsync(request, new CompositionRunProgressFeed(), TestContext.Current.CancellationToken).AsTask());
            Assert.Contains(next.Issues, static issue => issue.Code == "AB_FORMAT_CHANGED");
            Assert.Contains("AB_FORMAT_CHANGED", next.Message, StringComparison.Ordinal);
            CapabilityActionReadinessSnapshot? nextReadiness = await host.AbMergeAuthoring.GetActionReadinessAsync(
                updated.Snapshot, TestContext.Current.CancellationToken);
            var nextRequest = new AcceptedCompositionExecutionRequest(updated.Snapshot,
                new Dictionary<string, string> { ["tp-a-input"] = a, ["tp-b-input"] = b }, build: false,
                previewOutputFileName: "next.bin", actionReadiness: nextReadiness);
            CompositionRunResult nextRun = await host.CompositionExecution.ExecuteAsync(nextRequest,
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.True(nextRun.Succeeded, CompositionRunReportJson.Serialize(nextRun));
            AbMergeFormatRunSummary nextSummary = Assert.IsType<AbMergeFormatRunSummary>(nextRun.Report.AbMergeFormat);
            Assert.Equal("common", nextSummary.FormatId);
            Assert.True(nextSummary.ConfigurationGeneration > summary.ConfigurationGeneration);
            Assert.NotEqual(summary.ConfigurationSourceSha256, nextSummary.ConfigurationSourceSha256);
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [], AliasName = "Later Alias" })],
                TestContext.Current.CancellationToken)).Succeeded);
            CompositionRunResult aliasRun = await host.CompositionExecution.ExecuteAsync(nextRequest,
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.True(aliasRun.Succeeded, CompositionRunReportJson.Serialize(aliasRun));
            Assert.Equal(nextRun.OutputBytes.ToArray(), aliasRun.OutputBytes.ToArray());
            Assert.Equal(nextRun.PreviewToken, aliasRun.PreviewToken);
            Assert.NotEqual(nextSummary.ConfigurationSourceSha256, aliasRun.Report.AbMergeFormat!.ConfigurationSourceSha256);

            // Fail the external processor after the shared execution admission, not the preflight gate.
            var failing = new CompositionExecutionExperience(host.Catalog, new ProtectedCompositionDestinationProvider(),
                () => new CompositionExternalProcessorLease(environment.AcquireCurrent().Generation, new FailedFormatProcessor()),
                environment.IsCurrent, new SystemClock(), (AbMergeAuthoringExperience)host.AbMergeAuthoring);
            CompositionRunResult failed = await failing.ExecuteAsync(nextRequest,
                new CompositionRunProgressFeed(), TestContext.Current.CancellationToken);
            Assert.False(failed.Succeeded);
            Assert.Contains(failed.Report.Issues, issue => issue.Code == "TEST_FORMAT_PROCESSOR_FAILED");
            Assert.Equal("common", failed.Report.AbMergeFormat!.FormatId);
            Assert.Equal(aliasRun.Report.AbMergeFormat.ConfigurationSourceSha256, failed.Report.AbMergeFormat.ConfigurationSourceSha256);
            using JsonDocument failedJson = JsonDocument.Parse(CompositionRunReportJson.Serialize(failed));
            Assert.Equal("common", failedJson.RootElement.GetProperty("AbMergeFormat").GetProperty("FormatId").GetString());
        }
        finally
        {
            destination.Release.Set();
            _ = await running;
        }
    }

    private sealed class FailedFormatProcessor : IExternalProcessor
    {
        public ValueTask<ExternalProcessorResult> TransformAsync(ExternalProcessorRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ExternalProcessorResult.Failed(
                [new CompositionIssue("TEST_FORMAT_PROCESSOR_FAILED", "Intentional processor failure after format capture.")]));
        }
    }

    /// <summary>Report projection cannot cross map/family/input authority and does not persist file locators.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("map")]
    [InlineData("family")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("input-sha")]
    [InlineData("input-length")]
    [InlineData("input-space")]
    [InlineData("missing-input")]
    [InlineData("swapped-primary")]
    [InlineData("copied-b-primary")]
    public async Task FormatReportRequiresExactCompiledAndInputIdentityAsync(string scenario)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-report-identity");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        byte[] tp = CreateTp(0x97, 1);
        string path = workspace.PathFor("same.bin");
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("tp-a-input", path, tp), new("tp-b-input", path, tp)], AbMergeDpMode.Dummy,
            TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        (AbMergeFormatSelection? selection, IReadOnlyList<CompositionIssue> issues) =
            await ((AbMergeAuthoringExperience)host.AbMergeAuthoring).AssessAcceptedFormatAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.Empty(issues);
        AbMergeFormatSelection captured = Assert.IsType<AbMergeFormatSelection>(selection);
        CompiledComposition composition = prepared.Snapshot!.ExactCapability!.CompiledComposition;
        (InputArtifactBinding[] bindings, _) = AcceptedSessionExecutionInputs.CreateBindings(composition, prepared.Snapshot);
        captured = scenario switch
        {
            "map" => captured with { MapId = "wrong-map" },
            "family" => captured with { FamilyId = "wrong-family" },
            "version" => captured with { FamilyVersion = "0.0.0" },
            "hash" => captured with { FamilyContentHash = new string('0', 64) },
            "swapped-primary" => captured with { TpAPrimary = captured.TpBPrimary, TpBPrimary = captured.TpAPrimary },
            "copied-b-primary" => captured with { TpAPrimary = captured.TpBPrimary },
            _ => captured,
        };
        if (scenario is "input-sha" or "input-length" or "input-space")
        {
            InputArtifactBinding original = bindings[0];
            FileStamp stamp = original.AcceptedContentStamp!.Value;
            bindings[0] = new(scenario == "input-space" ? "wrong-space" : original.AddressSpaceId,
                original.BindingId, original.ArtifactId, original.OriginalFileName, original.ArtifactClass,
                new FileStamp(stamp.AcceptedLength + (scenario == "input-length" ? 1 : 0),
                    scenario == "input-sha" ? new string('0', 64) : stamp.Sha256));
        }
        if (scenario == "missing-input") { bindings = []; }
        if (scenario != "valid")
        {
            _ = Assert.Throws<InvalidOperationException>(() => AbMergeFormatRunSummary.Create(captured, composition, bindings));
            return;
        }

        AbMergeFormatRunSummary summary = AbMergeFormatRunSummary.Create(captured, composition, bindings);
        Assert.Equal("tp-a-input", summary.TpA.InputBindingId);
        Assert.Equal("tp-b-input", summary.TpB.InputBindingId);
        Assert.Equal("tp-a-primary-firmware-config", summary.TpA.StructureId);
        Assert.Equal("tp-b-primary-firmware-config", summary.TpB.StructureId);
        Assert.Equal("flash", summary.TpA.AddressSpaceId);
        Assert.Equal(0x22200, summary.TpA.Start);
        Assert.Equal(0x22229, summary.TpA.EndExclusive);
        Assert.Equal(summary.TpA.Start, summary.TpB.Start);
        Assert.Equal(summary.TpA.EndExclusive, summary.TpB.EndExclusive);
        Assert.Equal(captured.FamilyId, summary.FamilyId);
        Assert.Equal(captured.FamilyVersion, summary.FamilyVersion);
        Assert.Equal(captured.FamilyContentHash, summary.FamilyContentHash);
        Assert.Equal(captured.ConfigurationGeneration, summary.ConfigurationGeneration);
        Assert.Equal(captured.ConfigurationSourceSha256, summary.ConfigurationSourceSha256);
        Assert.DoesNotContain(path, JsonSerializer.Serialize(summary), StringComparison.OrdinalIgnoreCase);
        var runRequest = new CompositionRunRequest("capture-copy", composition, bindings,
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
            abMergeTopologySelection: CapabilityPublicationCoherence.GetAcceptedAbMergeTopologySelection(prepared.Snapshot.ExactCapability),
            resolvedCapability: prepared.Snapshot.ExactCapability)
        {
            AbMergeFormat = summary,
        };
        Assert.Same(summary, runRequest.WithApprovedPreviewToken("unchanged-preview-token").AbMergeFormat);
        Assert.Equal("unchanged-preview-token", runRequest.WithApprovedPreviewToken("unchanged-preview-token").ApprovedPreviewToken);
    }

    private sealed class HeldFormatDestination(CancellationToken cancellationToken) : ICompositionExecutionDestinationProvider, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);

        public CompositionExecutionDestination Prepare(CompositionExecutionDestinationRequest request)
        {
            Entered.SetResult();
            Release.Wait(cancellationToken);
            return new ProtectedCompositionDestinationProvider().Prepare(request);
        }

        public void Dispose()
        {
            Release.Dispose();
        }
    }

    /// <summary>A delayed reapplication never replaces later selections or another completed reapplication.</summary>
    [Theory]
    [InlineData("new-input", false, false)]
    [InlineData("new-reapply", false, false)]
    [InlineData("cancel", false, false)]
    [InlineData("publication", false, false)]
    [InlineData("new-input", true, false)]
    [InlineData("new-reapply", true, false)]
    [InlineData("cancel", true, false)]
    [InlineData("publication", true, false)]
    [InlineData("new-input", true, true)]
    [InlineData("new-reapply", true, true)]
    [InlineData("cancel", true, true)]
    [InlineData("publication", true, true)]
    public async Task ReapplicationRetainsSnapshotOwnershipAcrossWaitAsync(string mutation, bool preCompilation, bool blockedResolution)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-reapply-race");
        var environment = new ExternalProcessorEnvironmentLoader();
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        if (preCompilation)
        {
            _ = workspace.Write("format.json", "invalid"u8.ToArray());
            CompiledAuthoringSelectionSnapshot declaration = host.AbMergeAuthoring.GetAuthoringSnapshot("NT51950", "single",
                [], new Dictionary<string, FileStamp>(), new AuthoringRevision(1), dpMode: AbMergeDpMode.Dummy);
            Assert.True(session.Activate(declaration.Catalog).Succeeded);
            var paths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tp-a-input"] = workspace.Write("a.bin", CreateTp(0x97, 1)),
                ["tp-b-input"] = workspace.Write("b.bin", CreateTp(0x97, 1)),
            };
            AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(paths);
            FirmwareInspectionBatchResult inspected = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51950",
                [.. paths.Select(pair => new FirmwareInspectionSnapshotInput(pair.Key, pair.Value,
                    AbMergeAddressSpaceId: pair.Key, AbMergeTopologyToken: "single", AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                    AbMergeDpMode: AbMergeDpMode.Dummy))], TestContext.Current.CancellationToken);
            Assert.True(host.AbMergeAuthoring.AdoptInspectedBatch(session, inspected.InspectionsById["tp-a-input"].InputSlotCatalog!, started.Leases,
                inspected.InspectionsById.ToDictionary(static pair => pair.Key, static pair => pair.Value.InputSlotStatus!, StringComparer.Ordinal)).Succeeded);
            Assert.All(session.CurrentSnapshot!.InputSlotStatuses, static status => Assert.Null(status.AcceptedBytes));
        }
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        if (!preCompilation)
        {
            Assert.True((await host.AbMergeAuthoring.PrepareSessionAsync(session, "NT51950", "single",
                [new("tp-a-input", "a.bin", CreateTp(0x97, 1)), new("tp-b-input", "b.bin", CreateTp(0x97, 1))],
                AbMergeDpMode.Dummy, TestContext.Current.CancellationToken)).Succeeded);
        }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<IEventBufferFormatConfigurationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new AbMergeAuthoringExperience(host.Compiler, host.Catalog, environment, _ =>
        {
            entered.SetResult();
            return resume.Task;
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<CompiledAuthoringSessionPreparation?> pending = owner.ReapplyAcceptedInputsAsync(session, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        if (mutation == "new-input") { Assert.True(session.BeginSlotFileInspection("tp-a-input", "new-a.bin").Succeeded); }
        if (blockedResolution) { _ = workspace.Write("format.json", "invalid"u8.ToArray()); }
        if (mutation == "new-reapply")
        {
            Assert.Equal(!blockedResolution, (await host.AbMergeAuthoring.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken))!.Succeeded);
        }
        if (mutation == "cancel") { await cancellation.CancelAsync(); }
        if (mutation == "publication") { Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded); }
        ActiveSessionSnapshot current = session.CurrentSnapshot!;
        resume.SetResult(configuration);
        if (mutation == "cancel")
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.All(current.InputSlotStatuses, status =>
            {
                Assert.Equal(!preCompilation, status.AcceptedBytes is not null);
                Assert.Equal(preCompilation, status.CapturedSource is not null);
            });
        }
        else
        {
            CompiledAuthoringSessionPreparation result = Assert.IsType<CompiledAuthoringSessionPreparation>(await pending);
            Assert.False(result.Succeeded);
            Assert.Empty(result.AbMergeFacts);
            if (mutation == "publication") { Assert.Contains(result.Issues, static issue => issue.Code == "AB_FORMAT_PUBLICATION_STALE"); }
            else { Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, result.SessionIssue?.Code); }
            if (blockedResolution && mutation is "new-input" or "new-reapply")
            {
                // The Presentation reference guard must reject the old batch, even when paths are unchanged.
                Assert.NotSame(current, result.Snapshot);
            }
        }
        Assert.Same(current, session.CurrentSnapshot);
    }

    /// <summary>Failed Config reapplication retains accepted bytes for a later successful retry without firmware IO.</summary>
    [Fact]
    public async Task ReapplicationRetriesRetainedInputsAfterInvalidConfigurationAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-reapply");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(session, "NT51950", "cascade",
            [new("tp-a-input", workspace.PathFor("absent-a.bin"), CreateTp(0x97, 2)),
             new("tp-b-input", workspace.PathFor("absent-b.bin"), CreateTp(0x97, 2))], AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        ActiveSessionSnapshot original = prepared.Snapshot!;
        _ = workspace.Write("format.json", "invalid"u8.ToArray());
        CompiledAuthoringSessionPreparation failed = Assert.IsType<CompiledAuthoringSessionPreparation>(
            await host.AbMergeAuthoring.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken));
        Assert.False(failed.Succeeded);
        Assert.Contains(failed.Issues, static issue => issue.Code == "AB_FORMAT_CONFIGURATION_INVALID");
        Assert.NotSame(original, session.CurrentSnapshot);
        Assert.Same(original.ExactCapability, session.CurrentSnapshot!.ExactCapability);
        Assert.All(session.CurrentSnapshot.InputSlotStatuses, static status => Assert.NotNull(status.AcceptedBytes));
        Assert.True((await configuration.SaveAsync(
            [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [] })],
            TestContext.Current.CancellationToken)).Succeeded);
        CompiledAuthoringSessionPreparation recovered = Assert.IsType<CompiledAuthoringSessionPreparation>(
            await host.AbMergeAuthoring.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken));
        Assert.True(recovered.Succeeded, string.Join(" | ", recovered.Issues.Select(static issue => issue.Message)));
        Assert.Equal("nt51950-ab-common-2ic-maps", recovered.Snapshot!.ExactCapability!.Identity.MapVariant);
        Assert.All(recovered.Inspection!.Statuses.Values, status =>
            Assert.Equal(recovered.Snapshot.AuthoringRevision, status.AuthoringRevision));
        Assert.Same(recovered.Snapshot.ExactCapability, Assert.Single(recovered.Inspection.Catalog.Routes).ExactCapability);
        Assert.Same(recovered.Snapshot.MetadataInspection, recovered.Inspection.MetadataInspection);
        Assert.Same(recovered.Inspection.Catalog, recovered.Selection.Catalog);
        Assert.All(recovered.Snapshot.InputSlotStatuses, status =>
            Assert.Equal(original.InputSlotStatuses.Single(source => source.SlotId == status.SlotId).AcceptedBytes!.Value.ToArray(), status.AcceptedBytes!.Value.ToArray()));
    }

    /// <summary>Reload generations and display-only aliases do not alter accepted bytes or firmware authority.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EquivalentConfigurationReassessesAcceptedBytesWithoutReopeningPathsAsync(bool aliasChange)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-equivalent-run");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            session, "NT51950", "single",
            [new("tp-a-input", workspace.PathFor("never-created-a.bin"), CreateTp(0x97, 1)),
             new("tp-b-input", workspace.PathFor("never-created-b.bin"), CreateTp(0xA6, 1))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        var owner = (AbMergeAuthoringExperience)host.AbMergeAuthoring;
        (AbMergeFormatSelection? first, IReadOnlyList<CompositionIssue> initialIssues) =
            await owner.AssessAcceptedFormatAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.Empty(initialIssues);
        Assert.NotNull(first);
        if (aliasChange)
        {
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { AliasName = "My vendor" })],
                TestContext.Current.CancellationToken)).Succeeded);
        }
        (AbMergeFormatSelection? second, IReadOnlyList<CompositionIssue> reloadedIssues) =
            await owner.AssessAcceptedFormatAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        Assert.Empty(reloadedIssues);
        Assert.NotNull(second);
        Assert.True(second.ConfigurationGeneration > first.ConfigurationGeneration);
        Assert.Equal(first.MapId, second.MapId);
        Assert.Equal(aliasChange ? "My vendor" : first.DisplayName, second.DisplayName);
        CompiledAuthoringSessionPreparation reapplied = Assert.IsType<CompiledAuthoringSessionPreparation>(
            await owner.ReapplyAcceptedInputsAsync(session, TestContext.Current.CancellationToken));
        Assert.True(reapplied.Succeeded);
        foreach ((string slotId, byte raw) in new[] { ("tp-a-input", (byte)0x97), ("tp-b-input", (byte)0xA6) })
        {
            AbMergeInputFacts facts = reapplied.AbMergeFacts[slotId];
            EventBufferFormatObservation format = Assert.IsType<EventBufferFormatObservation>(facts.EventBufferFormat);
            Assert.Equal(raw, format.RawByte);
            Assert.Equal("desay", format.FormatId);
            Assert.Equal(aliasChange ? "My vendor" : first.DisplayName, format.DisplayName);
            Assert.True(format.ConfigurationGeneration > first.ConfigurationGeneration);
            Assert.Equal(configuration.Current.SourceSha256, format.ConfigurationSourceSha256);
            Assert.Equal(slotId, format.ArtifactIdentity.ArtifactId);
            Assert.Equal(FileStamp.FromBytes(CreateTp(raw, 1)).Sha256, format.ArtifactIdentity.Sha256);
            Assert.Equal(0x22200, format.PrimaryRange.Range.Start);
            Assert.Equal(prepared.Inspection!.Statuses[slotId].Observation.Versions, facts.Versions);
        }
    }

    /// <summary>Policy-absent legacy AB performs no Config IO and still retains its accepted readiness.</summary>
    [Fact]
    public async Task LegacyReadinessNeverAcquiresFormatConfigurationAsync()
    {
        CompositionHostServices host = CompositionHostServices.Create();
        var owner = new AbMergeAuthoringExperience(host.Compiler, host.Catalog, new ExternalProcessorEnvironmentLoader(),
            _ => throw new InvalidOperationException("Legacy policy must not request Config."));
        CompiledAuthoringSessionPreparation prepared = owner.PrepareSession(new AuthoringSessionState(ExperienceIds.AbMerge), "NT51929", null,
            [new("tp-a-input", "a.bin", new byte[0x40000]), new("tp-b-input", "b.bin", new byte[0x40000])], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
            await owner.GetActionReadinessAsync(prepared.Snapshot!, TestContext.Current.CancellationToken));
        Assert.True(readiness.Build.IsAvailable);
        Assert.True(readiness.Preview.IsAvailable);
    }

    /// <summary>Neither action readiness nor direct execution can retain a previously accepted format after configuration changes.</summary>
    [Theory]
    [InlineData("missing", false)]
    [InlineData("invalid", false)]
    [InlineData("changed", false)]
    [InlineData("missing", true)]
    [InlineData("invalid", true)]
    [InlineData("changed", true)]
    public async Task ChangedConfigurationBlocksAcceptedAbRunAsync(string change, bool execution)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-fresh-run");
        string configurationPath = workspace.PathFor("format.json");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: configurationPath);
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(change == "missing"
            ? [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [] })]
            : configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        string a = workspace.Write("a.bin", CreateTp(0x97, 1));
        string b = workspace.Write("b.bin", CreateTp(0x97, 1));
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("tp-a-input", a, File.ReadAllBytes(a)), new("tp-b-input", b, File.ReadAllBytes(b))],
            AbMergeDpMode.Dummy, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        if (change == "missing") { File.Delete(configurationPath); }
        if (change == "invalid") { _ = workspace.Write("format.json", "invalid json"u8.ToArray()); }
        if (change == "changed")
        {
            Assert.True((await configuration.SaveAsync(
                [.. configuration.CreateDefaultsDraft().Select(static entry => entry! with { RecognitionValues = [] })],
                TestContext.Current.CancellationToken)).Succeeded);
        }
        string expectedCode = change == "invalid" ? "AB_FORMAT_CONFIGURATION_INVALID" : "AB_FORMAT_CHANGED";
        string output = workspace.PathFor("must-not-exist.bin");
        if (execution)
        {
            int acquisitions = 0;
            var destinations = new RejectedFormatDestination();
            var executor = new CompositionExecutionExperience(host.Catalog, destinations, () =>
            {
                acquisitions++;
                throw new InvalidOperationException("Rejected format must not acquire a processor.");
            }, static _ => false, new FakeClock([]), (AbMergeAuthoringExperience)host.AbMergeAuthoring);
            CompositionPreRunRefusalException failure = await Assert.ThrowsAsync<CompositionPreRunRefusalException>(() => executor.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(prepared.Snapshot!, new Dictionary<string, string> { ["tp-a-input"] = a, ["tp-b-input"] = b },
                    build: true, outputPath: output), new CompositionRunProgressFeed(), TestContext.Current.CancellationToken).AsTask());
            Assert.Contains(expectedCode, failure.Message, StringComparison.Ordinal);
            Assert.Contains(failure.Issues, issue => issue.Code == expectedCode);
            Assert.Equal(0, acquisitions);
            Assert.Equal(0, destinations.Calls);
        }
        else
        {
            CapabilityActionReadinessSnapshot readiness = Assert.IsType<CapabilityActionReadinessSnapshot>(
                await host.AbMergeAuthoring.GetActionReadinessAsync(prepared.Snapshot!, TestContext.Current.CancellationToken));
            Assert.Contains(readiness.Build.Blockers, blocker => blocker.Code == expectedCode);
            Assert.Contains(readiness.Preview.Blockers, blocker => blocker.Code == expectedCode);
            if (change == "invalid")
            {
                CapabilityActionBlocker blocker = Assert.Single(readiness.Build.Blockers);
                Assert.Equal(CapabilityReadinessDimension.Configuration, blocker.Dimension);
                Assert.Equal(CapabilityReadinessNextAction.ReviewConfiguration, blocker.NextAction);
            }
        }
        Assert.False(File.Exists(output));
    }

    /// <summary>A publication or cancellation change during configuration acquisition cannot publish new exact authority.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConfigurationWaitCannotCrossPublicationOrCancellationAsync(bool inspection, bool cancel)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-capture-boundary");
        var environment = new ExternalProcessorEnvironmentLoader();
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource<IEventBufferFormatConfigurationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new AbMergeAuthoringExperience(host.Compiler, host.Catalog, environment, _ =>
        {
            entered.SetResult();
            return resume.Task;
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSessionPreparation? prepared = null;
        AbMergeInspectionBatch? inspected = null;
        async Task CaptureAsync()
        {
            if (inspection)
            {
                inspected = await owner.InspectInputSlotsAsync("NT51950",
                    [new("a", "a.bin", AbMergeAddressSpaceId: "tp-a-input", AbMergeTopologyToken: "single", AbMergeDpMode: AbMergeDpMode.Dummy),
                     new("b", "b.bin", AbMergeAddressSpaceId: "tp-b-input", AbMergeTopologyToken: "single", AbMergeDpMode: AbMergeDpMode.Dummy)],
                    _ => CreateTp(0x97, 1), cancellation.Token);
            }
            else
            {
                prepared = await owner.PrepareSessionAsync(session, "NT51950", "single",
                    [new("tp-a-input", "a.bin", CreateTp(0x97, 1)), new("tp-b-input", "b.bin", CreateTp(0x97, 1))],
                    AbMergeDpMode.Dummy, cancellation.Token);
            }
        }
        Task capture = CaptureAsync();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        if (cancel) { await cancellation.CancelAsync(); }
        else { Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded); }
        resume.SetResult(configuration);
        if (cancel)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
            Assert.Null(session.CurrentSnapshot);
            Assert.Null(inspected);
        }
        else
        {
            await capture;
            IReadOnlyList<CompositionIssue> issues = inspection ? inspected!.Issues : prepared!.Issues;
            Assert.Contains(issues, static issue => issue.Code == "AB_FORMAT_PUBLICATION_STALE");
            Assert.Null(session.CurrentSnapshot?.ExactCapability);
            if (inspection)
            {
                Assert.All(inspected!.Statuses.Values, static status => Assert.Null(status.AcceptedBytes));
                Assert.All(inspected.Facts.Values, static facts => Assert.Null(facts.EventBufferFormat));
            }
        }
    }

    /// <summary>Legacy preparation observes cancellation even though no format configuration needs to be awaited.</summary>
    [Fact]
    public async Task CancelledLegacyPreparationCannotMutateSessionAsync()
    {
        CompositionHostServices host = CompositionHostServices.Create();
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await host.AbMergeAuthoring.PrepareSessionAsync(session, "NT51929", null,
                [new("tp-a-input", "a.bin", new byte[0x40000])], AbMergeDpMode.Dummy, cancellation.Token));
        Assert.Null(session.CurrentSnapshot);
    }

    /// <summary>Malformed input membership remains a typed batch failure, with no guessed address space or accepted bytes.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("tp-a-input")]
    public async Task InvalidInspectionMembershipReturnsTypedIssueAsync(string secondSlot)
    {
        var owner = (AbMergeAuthoringExperience)CompositionHostServices.Create().AbMergeAuthoring;
        AbMergeInspectionBatch result = await owner.InspectInputSlotsAsync("NT51929",
            [new("a", "a.bin", AbMergeAddressSpaceId: "tp-a-input"), new("b", "b.bin", AbMergeAddressSpaceId: secondSlot)],
            _ => new byte[0x40000], TestContext.Current.CancellationToken);
        Assert.Contains(result.Issues, static issue => issue.Code == "AB_FORMAT_INPUT_INVALID");
        Assert.Empty(result.Statuses);
        Assert.Empty(result.Facts);
        Assert.Null(Assert.Single(result.Catalog!.Routes).ExactCapability);
    }

    /// <summary>Legacy partial selections retain their exact accepted contract rather than becoming a format migration.</summary>
    [Fact]
    public async Task LegacySingleSlotInspectionRetainsOriginalExactAuthorityAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-legacy-partial");
        CompositionHostServices host = CompositionHostServices.Create();
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSelectionSnapshot initial = host.AbMergeAuthoring.GetAuthoringSnapshot("NT51929", null,
            [], new Dictionary<string, FileStamp>(), new AuthoringRevision(1));
        Assert.True(session.Activate(initial.Catalog).Succeeded);
        string path = workspace.Write("a.bin", new byte[0x40000]);
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(new Dictionary<string, string> { ["tp-a-input"] = path });
        FirmwareInspectionBatchResult result = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51929",
            [new("tp-a-input", path, AbMergeAddressSpaceId: "tp-a-input", AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                ExactCapability: started.Snapshot.ExactCapability)], TestContext.Current.CancellationToken);
        AuthoringCapabilityCatalogSnapshot target = result.InspectionsById["tp-a-input"].InputSlotCatalog!;
        Assert.Null(result.InspectionsById["tp-a-input"].AbMergeFacts!.EventBufferFormat);
        TestContext.Current.TestOutputHelper!.WriteLine($"source={started.Snapshot.CompilationFingerprint}; target={Assert.Single(target.Routes).CompilationFingerprint}");
        AuthoringSessionTransitionResult adopted = host.AbMergeAuthoring.AdoptInspectedBatch(session, target, started.Leases,
            result.InspectionsById.ToDictionary(static pair => pair.Key, static pair => pair.Value.InputSlotStatus!, StringComparer.Ordinal));
        Assert.True(adopted.Succeeded, adopted.Issue?.Message);
    }

    /// <summary>Format discovery may adopt a complete exact batch only for the original current source leases.</summary>
    [Theory]
    [InlineData("valid", 0x84)]
    [InlineData("valid", 0x97)]
    [InlineData("foreign-session", 0x84)]
    [InlineData("changed-path", 0x84)]
    [InlineData("old-revision", 0x84)]
    [InlineData("missing-member", 0x84)]
    [InlineData("duplicate-member", 0x84)]
    [InlineData("publication-rollover", 0x84)]
    public async Task FormatBatchAdoptionRetainsSourceLeaseOwnershipAsync(string mutation, byte format)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-adoption");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        var session = new AuthoringSessionState(ExperienceIds.AbMerge);
        CompiledAuthoringSelectionSnapshot declaration = host.AbMergeAuthoring.GetAuthoringSnapshot("NT51950", "cascade",
            [], new Dictionary<string, FileStamp>(), new AuthoringRevision(1), dpMode: AbMergeDpMode.Dummy);
        Assert.True(session.Activate(declaration.Catalog).Succeeded);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tp-a-input"] = workspace.Write("a.bin", CreateTp(format, 2)),
            ["tp-b-input"] = workspace.Write("b.bin", CreateTp(format, 2)),
        };
        AuthoringSlotInspectionBatchStartResult started = session.BeginSlotFileInspections(paths);
        Assert.True(started.Succeeded);
        FirmwareInspectionBatchResult inspected = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51950",
            [.. paths.Select(pair => new FirmwareInspectionSnapshotInput(pair.Key, pair.Value,
                AbMergeAddressSpaceId: pair.Key, AbMergeTopologyToken: "cascade", AuthoringRevision: started.Snapshot!.AuthoringRevision.Value,
                AbMergeDpMode: AbMergeDpMode.Dummy))], TestContext.Current.CancellationToken);
        AuthoringCapabilityCatalogSnapshot exact = inspected.InspectionsById["tp-a-input"].InputSlotCatalog!;
        IReadOnlyList<AuthoringSlotInspectionLease> leases = started.Leases;
        if (mutation == "foreign-session")
        {
            var foreign = new AuthoringSessionState(ExperienceIds.AbMerge);
            Assert.True(foreign.Activate(declaration.Catalog).Succeeded);
            leases = foreign.BeginSlotFileInspections(paths).Leases;
        }
        if (mutation is "changed-path" or "old-revision")
        {
            if (mutation == "changed-path") { paths["tp-a-input"] = workspace.PathFor("other.bin"); }
            Assert.True(session.BeginSlotFileInspections(paths).Succeeded);
        }
        if (mutation == "missing-member") { leases = [leases[0]]; }
        if (mutation == "duplicate-member") { leases = [leases[0], leases[0]]; }
        if (mutation == "publication-rollover") { Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded); }
        ActiveSessionSnapshot before = session.CurrentSnapshot!;
        AuthoringSessionTransitionResult adopted = host.AbMergeAuthoring.AdoptInspectedBatch(session, exact, leases,
            inspected.InspectionsById.ToDictionary(static pair => pair.Key, static pair => pair.Value.InputSlotStatus!, StringComparer.Ordinal));
        Assert.Equal(mutation == "valid", adopted.Succeeded);
        if (mutation == "valid")
        {
            Assert.True(adopted.Snapshot!.HasCurrentInputInspection);
            Assert.Equal(format == 0x84 ? "2-ic" : "2-plus-ic", adopted.Snapshot.ExactCapability!.Identity.IcCountVariant);
        }
        else { Assert.Same(before, session.CurrentSnapshot); }
    }

    /// <summary>Discovery may expose input declarations but cannot select a format or use the legacy synchronous entry point.</summary>
    [Fact]
    public void FormatDiscoveryAndLegacyEntryPointCannotCompileAnAssumedFormat()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-discovery");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        CompiledAuthoringSelectionSnapshot selection = host.AbMergeAuthoring.GetAuthoringSnapshot("NT51950", "cascade",
            [], new Dictionary<string, FileStamp>(StringComparer.Ordinal), new AuthoringRevision(1));
        Assert.Equal(3, selection.InputBindings.Count);
        Assert.Null(Assert.Single(selection.Catalog.Routes).ExactCapability);
        CompiledAuthoringSessionPreparation legacy = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "cascade",
            [new("tp-a-input", "a.bin", CreateTp(0x97, 2)), new("tp-b-input", "b.bin", CreateTp(0xA6, 2))], AbMergeDpMode.Dummy);
        Assert.False(legacy.Succeeded);
        Assert.Contains(legacy.Issues, static issue => issue.Code == "AB_FORMAT_CAPTURE_REQUIRED");
    }

    /// <summary>Actual asynchronous file loading uses primary format instead of compiling an IC-only default.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncFileInspectionUsesCapturedFormatAsync(bool invalidPrimary)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-file-inspection");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        byte[] tpA = CreateTp(0x97, 1);
        if (invalidPrimary) { tpA[0x22201] = 0; }
        string a = workspace.Write("a.bin", tpA);
        string b = workspace.Write("b.bin", CreateTp(0xA6, 1));
        FirmwareInspectionBatchResult result = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51950",
            [new("a", a, AbMergeAddressSpaceId: "tp-a-input", AbMergeTopologyToken: "single"),
             new("b", b, AbMergeAddressSpaceId: "tp-b-input", AbMergeTopologyToken: "single")], TestContext.Current.CancellationToken);
        AuthoringInputSlotStatus status = result.InspectionsById["a"].InputSlotStatus!;
        Assert.NotNull(status);
        Assert.Equal(a, status.SelectedPathHint);
        if (invalidPrimary)
        {
            Assert.Equal("AB_FORMAT_PRIMARY_INVALID", status.SelectionReadiness.IssueCode);
            Assert.Null(status.AcceptedBytes);
            Assert.Null(status.CompilationFingerprint);
        }
        else
        {
            Assert.Equal(tpA, status.AcceptedBytes!.Value.ToArray());
            Assert.Equal("nt51950-ab-desay-maps", Assert.Single(result.InspectionsById["a"].InputSlotCatalog!.Routes).ExactCapability!.Identity.MapVariant);
        }
    }

    /// <summary>Primary format and actual TP counts select the declared output profile in the host workflow.</summary>
    [Theory]
    [InlineData("NT51950", "single", 1, 0x97, "nt51950-ab-merge-desay", "nt51950-ab-desay-single-1024k")]
    [InlineData("NT51950", "cascade", 2, 0xA6, "nt51950-ab-merge-desay", "nt51950-ab-desay-cascade-1024k")]
    [InlineData("NT51951", null, 2, 0x97, "nt51951-ab-merge-desay", "nt51951-ab-desay-1024k")]
    [InlineData("NT51950", "cascade", 2, 0x84, "nt51950-ab-merge-common-2ic", "nt51950-ab-common-exact2-1024k")]
    public async Task SavedFormatSelectsActualAbProfileAsync(
        string icId, string? topology, byte actualCount, byte format, string expectedProfile, string expectedMap)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-runtime");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(),
            TestContext.Current.CancellationToken)).Succeeded);
        byte[] tpA = CreateTp(format, actualCount);
        byte[] tpB = CreateTp(format == 0x97 ? (byte)0xA6 : format, actualCount);

        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), icId, topology,
            [new("tp-a-input", "tp-a.bin", tpA), new("tp-b-input", "tp-b.bin", tpB)], AbMergeDpMode.Dummy,
            TestContext.Current.CancellationToken);

        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Code)));
        Assert.NotNull(prepared.Snapshot?.ExactCapability);
        CompiledComposition composition = prepared.Snapshot.ExactCapability.CompiledComposition;
        Assert.Equal(expectedProfile, composition.V2Details.ProfileId);
        Assert.Equal(expectedMap, composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        (AbMergeFormatSelection? selection, IReadOnlyList<CompositionIssue> issues) =
            await ((AbMergeAuthoringExperience)host.AbMergeAuthoring).AssessAcceptedFormatAsync(prepared.Snapshot,
                TestContext.Current.CancellationToken);
        Assert.Empty(issues);
        (InputArtifactBinding[] bindings, _) = AcceptedSessionExecutionInputs.CreateBindings(composition, prepared.Snapshot);
        AbMergeFormatRunSummary summary = AbMergeFormatRunSummary.Create(
            Assert.IsType<AbMergeFormatSelection>(selection), composition, bindings);
        Assert.Equal(format == 0x84 ? "common" : "desay", summary.FormatId);
        Assert.Equal(format, summary.TpA.FormatByte);
        Assert.Equal(tpB[0x2220C], summary.TpB.FormatByte);
    }

    /// <summary>The host must not preserve a legacy executable session when current format admission fails.</summary>
    [Theory]
    [InlineData("invalid-config", "AB_FORMAT_CONFIGURATION_INVALID")]
    [InlineData("mismatched-format", "AB_FORMAT_MISMATCH")]
    [InlineData("invalid-primary", "AB_FORMAT_PRIMARY_INVALID")]
    public async Task InvalidCurrentFormatCannotPrepareLegacyOutputAsync(string failure, string expectedCode)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ab-format-runtime-invalid");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        if (failure == "invalid-config") { _ = workspace.Write("format.json", "invalid"u8.ToArray()); }
        if (failure != "invalid-config")
        {
            IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
                TestContext.Current.CancellationToken);
            Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(),
                TestContext.Current.CancellationToken)).Succeeded);
        }
        byte[] tpA = CreateTp(0x97, 1);
        byte[] tpB = CreateTp(failure == "mismatched-format" ? (byte)0x84 : (byte)0xA6, 1);
        if (failure == "invalid-primary")
        {
            tpA[0x22201] = 0; // A valid Backup cannot repair the invalid primary complement.
        }
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [new("tp-a-input", "tp-a.bin", tpA), new("tp-b-input", "tp-b.bin", tpB)], AbMergeDpMode.Dummy,
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Contains(prepared.Issues, issue => issue.Code == expectedCode);
    }

    private sealed class RejectedFormatDestination : ICompositionExecutionDestinationProvider
    {
        internal int Calls { get; private set; }

        public CompositionExecutionDestination Prepare(CompositionExecutionDestinationRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Rejected format must not prepare an output destination.");
        }
    }

    private static byte[] CreateTp(byte format, byte count)
    {
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = format;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x3600C] = 0x84; // Backup must not replace primary format selection.
        tp[0x36017] = count;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        return tp;
    }
}
