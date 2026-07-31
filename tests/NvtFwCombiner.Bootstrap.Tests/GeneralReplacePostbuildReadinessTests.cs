using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.Tests.BootstrapTestData;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Workbench evidence for pre-run General Replace POSTBUILD readiness.</summary>
public sealed class GeneralReplacePostbuildReadinessTests
{
    /// <summary>A declared stage with a missing tool yields only a plan-only diagnostic.</summary>
    [Fact]
    public async Task MissingRuntimeToolCreatesDiagnosticWithoutEngineOutputOrMutation()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-diagnostic");
        byte[] referenceBytes = CreatePattern(0x40000, 0x31);
        byte[] sourceBytes = [0xA5, 0x5A];
        string referencePath = workspace.Write("reference.bin", referenceBytes);
        string sourcePath = workspace.Write("source.bin", sourceBytes);
        GeneralMappingDraftState draft = Draft(sourcePath);
        var provider = new TestReadinessProvider(isReady: false);
        WorkbenchCompositionService.GeneralReplacePostbuildReadinessOverride
            runtime = RuntimeOverride(provider);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                draft,
                build: false,
                runtime,
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("DiagnosticPlanOnly", result.Status);
        Assert.True(result.HasRunReport);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(referenceBytes, await File.ReadAllBytesAsync(
            referencePath,
            TestContext.Current.CancellationToken));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(
            sourcePath,
            TestContext.Current.CancellationToken));
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement root = report.RootElement;
        JsonElement diagnostic = root.GetProperty("DiagnosticPreview");
        Assert.Equal(
            "required-general-postbuild",
            diagnostic.GetProperty("RequiredStageId").GetString());
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            diagnostic.GetProperty("Blocker").GetProperty("Code").GetString());
        Assert.False(diagnostic.GetProperty("OutputProduced").GetBoolean());
        Assert.False(diagnostic.GetProperty("ClaimsFinalIntegrity").GetBoolean());
        Assert.Empty(root.GetProperty("Mutations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Output").ValueKind);
        Assert.Empty(result.OutputFileName);
        Assert.Empty(result.OutputSha256);
    }

    /// <summary>The same blocker disables Build before a report or BIN exists.</summary>
    [Fact]
    public async Task MissingRuntimeToolDisablesBuildWithoutCreatingRunReport()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-build");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x32));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        var provider = new TestReadinessProvider(isReady: false);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: true,
                RuntimeOverride(provider),
                outputPath,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("BuildUnavailable", result.Status);
        Assert.False(result.HasRunReport);
        Assert.Empty(result.ReportJson);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
            result.ActionReadiness!.Build.PrimaryBlocker!.Code);
    }

    /// <summary>A refreshed tool result from a superseded generation remains diagnostic-only.</summary>
    [Fact]
    public async Task StaleRuntimeGenerationCannotReachEngineExecution()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-runtime-stale");
        string referencePath = workspace.Write(
            "reference.bin",
            CreatePattern(0x40000, 0x33));
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        var provider = new TestReadinessProvider(isReady: true);

        WorkbenchRunResult result = await WorkbenchCompositionService
            .RunGeneralReplaceEphemeralDraftWithPostbuildReadinessAsync(
                "NT51926",
                "single",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [WorkbenchSlotIds.ReplaceBase] = referencePath,
                },
                Draft(sourcePath),
                build: false,
                RuntimeOverride(provider, generationIsCurrent: false),
                outputPath: null,
                TestContext.Current.CancellationToken);

        Assert.Equal("DiagnosticPlanOnly", result.Status);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.ActionReadiness!.Build.PrimaryBlocker!.Code);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            report.RootElement.GetProperty("DiagnosticPreview")
                .GetProperty("Blocker")
                .GetProperty("Code")
                .GetString());
        Assert.Equal(
            JsonValueKind.Null,
            report.RootElement.GetProperty("Output").ValueKind);
    }

    private static WorkbenchCompositionService
        .GeneralReplacePostbuildReadinessOverride RuntimeOverride(
            IRuntimeDependencyReadinessProvider provider,
            bool generationIsCurrent = true)
    {
        SavedRuleParentIdentity parent =
            WorkbenchCompositionService
                .GetNt51926GeneralReplaceSavedRuleAdmissionContext()
                .ParentBinding;
        var authority = new SavedRuleV2GeneralReplaceRuntimeAuthority(
            parent,
            ["required-general-postbuild"],
            [
                new ExternalProcessorDependencyReference(
                    "general-postbuild",
                    "legacy-combiner-1.13.0"),
            ]);
        return new WorkbenchCompositionService
            .GeneralReplacePostbuildReadinessOverride(
                authority,
                provider,
                Generation: 1,
                generation => generationIsCurrent && generation == 1);
    }

    private static GeneralMappingDraftState Draft(string sourcePath)
    {
        return new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "tp-map",
                ExplicitMappingOperationKind.ReplaceRange,
                GeneralMappingSource.File(sourcePath),
                new ByteRange(0, 2),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0x22800, 2),
                OverlapPolicy.Reject,
                alignment: 1,
                "Runtime readiness regression."),
        ]);
    }

    private sealed class TestReadinessProvider :
        IRuntimeDependencyReadinessProvider
    {
        private readonly bool _isReady;

        internal TestReadinessProvider(bool isReady)
        {
            _isReady = isReady;
        }

        internal int CallCount { get; private set; }

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            ExternalProcessorDependencyReference dependency =
                Assert.Single(request.Dependencies);
            RuntimeDependencyEntry entry = _isReady
                ? RuntimeDependencyEntry.Ready(
                    dependency.ProcessorId,
                    dependency.ToolBindingId)
                : RuntimeDependencyEntry.Blocked(
                    dependency.ProcessorId,
                    dependency.ToolBindingId,
                    "external-tool.executable.missing",
                    "The required runtime tool is unavailable.");
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                [entry]));
        }
    }
}
