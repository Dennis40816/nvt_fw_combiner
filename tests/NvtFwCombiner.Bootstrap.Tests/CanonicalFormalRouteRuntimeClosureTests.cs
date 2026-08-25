using System.Security.Cryptography;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Executes the complete reviewed Standard, AB, and CtrlRAM route denominator.</summary>
public sealed class CanonicalFormalRouteRuntimeClosureTests
{
    /// <summary>The policy denominator and honest witness classes remain exact.</summary>
    [Fact]
    public void FormalRouteFixtureCatalogCoversTheExactReviewedDenominator()
    {
        IReadOnlyList<CanonicalFormalRouteRuntimeFixture> fixtures =
            CanonicalFormalRouteRuntimeFixtureCatalog.Create();

        Assert.Equal(64, fixtures.Count);
        Assert.Equal(64, fixtures.Select(static fixture => fixture.RouteId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(14, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.StandardMerge));
        Assert.Equal(6, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.AbMerge));
        Assert.Equal(44, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.CtrlRamReplace));
        Assert.Equal(28, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.DirectGolden));
        Assert.Equal(9, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.ApprovedAlias));
        Assert.Equal(5, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.SyntheticOracle));
        Assert.Equal(22, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.ContractOnly));
    }

    /// <summary>
    /// Product preparation and the shared executor close over every formal route. Extra cases lock
    /// both NT51928 Standard capacities and bounded/generic CtrlRAM topology boundaries.
    /// </summary>
    [Theory(Timeout = 180_000)]
    [InlineData(ExperienceIds.StandardMerge, 14, 15)]
    [InlineData(ExperienceIds.AbMerge, 6, 7)]
    [InlineData(ExperienceIds.CtrlRamReplace, 44, 59)]
    public async Task FormalRoutesPreparePreviewAndBuildWithExactRuntimeIdentityAsync(
        string workflowId,
        int expectedRouteCount,
        int expectedCaseCount)
    {
        CompositionHostServices host = BootstrapTestHost.ProductServices;
        CanonicalTestContext canonical = BootstrapTestHost.ProductCanonical;
        CanonicalFormalRouteRuntimeFixture[] fixtures =
        [
            .. CanonicalFormalRouteRuntimeFixtureCatalog.Create().Where(fixture =>
                fixture.Policy.Identity.WorkflowId == workflowId),
        ];
        int executedCases = 0;
        foreach (CanonicalFormalRouteRuntimeFixture fixture in fixtures)
        {
            using var workspace = TempWorkspace.Create(
                $"nfc-formal-route-{fixture.Policy.Identity.IcId}-{workflowId}");
            IReadOnlyList<CanonicalFormalRouteRuntimeCase> cases =
                CanonicalFormalRouteRuntimeFixtureCatalog.Materialize(
                    fixture,
                    workspace,
                    host);
            Assert.NotEmpty(cases);
            foreach (CanonicalFormalRouteRuntimeCase runtimeCase in cases)
            {
                try
                {
                    AssertWitnessProvenance(runtimeCase);
                    await ExecuteCaseAsync(host, canonical, runtimeCase, workspace);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Formal runtime case '{runtimeCase.CaseId}' failed.",
                        exception);
                }
                executedCases++;
            }
        }

        Assert.Equal(expectedRouteCount, fixtures.Length);
        Assert.Equal(expectedCaseCount, executedCases);
    }

    private static async Task ExecuteCaseAsync(
        CompositionHostServices host,
        CanonicalTestContext canonical,
        CanonicalFormalRouteRuntimeCase runtimeCase,
        TempWorkspace workspace)
    {
        var originalInputHashes = runtimeCase.SlotPaths.Values
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(static path => path, HashFile, StringComparer.Ordinal);
        AssertExpectedFirmwareConfigChipCount(runtimeCase);
        ActiveSessionSnapshot accepted = Prepare(host, canonical, runtimeCase);
        ResolvedCapability capability = Assert.IsType<ResolvedCapability>(
            accepted.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection));
        AssertExactIdentity(runtimeCase, accepted, capability);
        CapabilityActionReadinessSnapshot? readiness = await ResolveReadinessAsync(
            accepted,
            capability,
            TestContext.Current.CancellationToken);
        var processor = new CanonicalFormalRuntimePassThroughProcessor();
        ICompositionExecution execution = CompositionExecutionTestSupport.Create(
            canonical,
            () => new CompositionExternalProcessorLease(1, processor),
            static generation => generation == 1);

        CompositionRunResult preview = await execution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                accepted,
                runtimeCase.SlotPaths,
                build: false,
                actionReadiness: readiness),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);
        string outputPath = workspace.PathFor(
            $"outputs/{Sanitize(runtimeCase.CaseId)}.bin");
        CompositionRunResult build = await execution.ExecuteAsync(
            new AcceptedCompositionExecutionRequest(
                accepted,
                runtimeCase.SlotPaths,
                build: true,
                outputPath: outputPath,
                actionReadiness: readiness),
            new CompositionRunProgressFeed(),
            TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, Failure(runtimeCase, preview));
        Assert.True(build.Succeeded, Failure(runtimeCase, build));
        Assert.Same(capability, preview.ResolvedCapability);
        Assert.Same(capability, build.ResolvedCapability);
        Assert.Equal(preview.OutputBytes.ToArray(), build.OutputBytes.ToArray());
        string computedOutputSha256 = Convert.ToHexStringLower(
            SHA256.HashData(preview.OutputBytes.Span));
        Assert.Equal(computedOutputSha256, preview.OutputSha256);
        Assert.Equal(computedOutputSha256, build.OutputSha256);
        Assert.Equal(preview.OutputBytes.Length, preview.OutputSize);
        Assert.Equal(build.OutputBytes.Length, build.OutputSize);
        Assert.Equal(preview.OutputSha256, build.OutputSha256);
        Assert.Equal(preview.OutputSize, build.OutputSize);
        Assert.Equal(preview.OutputBytes.ToArray(), File.ReadAllBytes(outputPath));
        Assert.Equal(capability.CompiledComposition.CompilationFingerprint,
            preview.Report.CompilationFingerprint);
        Assert.Equal(preview.Report.CompilationFingerprint, build.Report.CompilationFingerprint);
        AssertOperationsSucceededAndConstrained(preview);
        AssertOperationsSucceededAndConstrained(build);
        int processorOperationCount = capability.CompiledComposition.Plan.OrderedOperations.Count(
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor);
        Assert.Equal(processorOperationCount * 2, processor.Requests.Count);
        if (runtimeCase.ExpectedResolvedIcCount is { } expectedIcCount)
        {
            Assert.All(processor.Requests, request =>
                Assert.Equal(expectedIcCount, request.ResolvedIcCount));
            if (runtimeCase.Fixture.Policy.Identity.WorkflowId == ExperienceIds.CtrlRamReplace)
            {
                long baseLength = new FileInfo(
                    runtimeCase.SlotPaths[CompositionSlotIds.ReplaceBase]).Length;
                Assert.Equal(baseLength, preview.OutputSize);
                Assert.Equal(baseLength, build.OutputSize);
            }
        }
        Assert.All(originalInputHashes, pair => Assert.Equal(pair.Value, HashFile(pair.Key)));
    }

    private static void AssertExpectedFirmwareConfigChipCount(
        CanonicalFormalRouteRuntimeCase runtimeCase)
    {
        if (runtimeCase.ExpectedFirmwareConfigChipCount is not { } expectedChipCount)
        {
            return;
        }

        string workflowId = runtimeCase.Fixture.Policy.Identity.WorkflowId;
        string[] metadataSlots = workflowId switch
        {
            ExperienceIds.AbMerge =>
            [
                CompositionAddressSpaceIds.TpAInput,
                CompositionAddressSpaceIds.TpBInput,
            ],
            ExperienceIds.CtrlRamReplace => [CompositionSlotIds.ReplaceBase],
            _ => throw new InvalidOperationException(
                $"Runtime chip-count witness is unsupported for '{workflowId}'."),
        };
        Assert.All(metadataSlots, slotId =>
        {
            byte[] bytes = File.ReadAllBytes(runtimeCase.SlotPaths[slotId]);
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(
                bytes,
                out FirmwareConfigMetadata metadata));
            Assert.Equal(expectedChipCount, metadata.ChipNumber);
        });
    }

    private static void AssertWitnessProvenance(CanonicalFormalRouteRuntimeCase runtimeCase)
    {
        Assert.NotEmpty(runtimeCase.WitnessProvenance);
        Assert.Equal(
            runtimeCase.SlotPaths.Keys.Order(StringComparer.Ordinal),
            runtimeCase.WitnessProvenance.Select(static witness => witness.SlotId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            runtimeCase.WitnessProvenance,
            static witness =>
                (witness.Kind is CanonicalFormalRuntimeWitnessKind.CanonicalDerived or
                    CanonicalFormalRuntimeWitnessKind.Synthetic) &&
                witness.ParityClaim == CanonicalFormalRuntimeParityClaim.DirectGoldenParity);
        Assert.All(runtimeCase.WitnessProvenance, witness =>
        {
            Assert.Equal(
                CanonicalFormalRuntimeParityClaim.RuntimeContractOnly,
                witness.ParityClaim);
            if (witness.Kind == CanonicalFormalRuntimeWitnessKind.Synthetic)
            {
                Assert.Null(witness.SourceWorkflowId);
                Assert.Null(witness.SourceIcId);
                Assert.Null(witness.SourceCaseId);
                return;
            }

            Assert.False(string.IsNullOrWhiteSpace(witness.SourceWorkflowId));
            Assert.False(string.IsNullOrWhiteSpace(witness.SourceIcId));
            Assert.False(string.IsNullOrWhiteSpace(witness.SourceCaseId));
        });
    }

    private static ActiveSessionSnapshot Prepare(
        CompositionHostServices host,
        CanonicalTestContext canonical,
        CanonicalFormalRouteRuntimeCase runtimeCase)
    {
        CapabilityRouteIdentity identity = runtimeCase.Fixture.Policy.Identity;
        if (identity.WorkflowId == ExperienceIds.StandardMerge)
        {
            CompiledAuthoringSessionPreparation prepared = host.StandardMergeAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.StandardMerge),
                identity.IcId,
                ReadSelectedInputs(runtimeCase.SlotPaths));
            Assert.True(prepared.Succeeded, FormatPreparationFailure(runtimeCase, prepared.Issues));
            return Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
        }
        if (identity.WorkflowId == ExperienceIds.AbMerge)
        {
            string? topologyToken = ResolveAbTopologyToken(host, identity.IcId, runtimeCase.SelectionToken);
            CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
                new AuthoringSessionState(ExperienceIds.AbMerge),
                identity.IcId,
                topologyToken,
                ReadSelectedInputs(runtimeCase.SlotPaths));
            Assert.True(prepared.Succeeded, FormatPreparationFailure(runtimeCase, prepared.Issues));
            return Assert.IsType<ActiveSessionSnapshot>(prepared.Snapshot);
        }

        var bytes = runtimeCase.SlotPaths.ToDictionary(
            static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value),
            StringComparer.Ordinal);
        CtrlRamAuthoringSessionPreparation ctrlRam = canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace),
            identity.IcId,
            runtimeCase.SelectionToken!,
            runtimeCase.SlotPaths,
            bytes);
        Assert.True(ctrlRam.Succeeded, FormatPreparationFailure(runtimeCase, ctrlRam.Issues));
        return Assert.IsType<ActiveSessionSnapshot>(ctrlRam.AcceptedSession);
    }

    private static CompiledAuthoringSelectedInput[] ReadSelectedInputs(
        IReadOnlyDictionary<string, string> slotPaths)
    {
        return
        [
            .. slotPaths.Select(static pair => new CompiledAuthoringSelectedInput(
                pair.Key,
                pair.Value,
                File.ReadAllBytes(pair.Value))),
        ];
    }

    private static string? ResolveAbTopologyToken(
        CompositionHostServices host,
        string icId,
        string? requestedCount)
    {
        if (requestedCount is null)
        {
            return null;
        }
        int count = int.Parse(
            requestedCount,
            System.Globalization.CultureInfo.InvariantCulture);
        return host.AbMergeAuthoring.GetTopologyChoices(icId)
            .Single(choice => choice.Selection.ChipCount == 1 ? count == 1 : count > 1)
            .Token;
    }

    private static void AssertExactIdentity(
        CanonicalFormalRouteRuntimeCase runtimeCase,
        ActiveSessionSnapshot accepted,
        ResolvedCapability capability)
    {
        CanonicalCapabilityPolicyRoute policy = runtimeCase.Fixture.Policy;
        Assert.Equal(policy.Identity.RouteId, accepted.SelectedRouteId);
        Assert.Equal(policy.Identity.RouteId, capability.Identity.RouteId);
        Assert.Equal(policy.CapabilityFingerprint, accepted.CapabilityFingerprint);
        Assert.Equal(policy.CapabilityFingerprint, capability.CapabilityFingerprint);
        Assert.Equal(policy.Evidence.Value, capability.Evidence.Value);
        Assert.Equal(policy.Identity.RouteId, capability.Evidence.RouteId);
        Assert.Equal(policy.CapabilityFingerprint, capability.Evidence.CapabilityFingerprint);
        Assert.Equal(runtimeCase.ExpectedMapId,
            capability.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.False(string.IsNullOrWhiteSpace(
            capability.CompiledComposition.CompilationFingerprint));
        Assert.Equal(capability.CompiledComposition.CompilationFingerprint,
            accepted.CompilationFingerprint);
        Assert.True(accepted.ExecutionAdmitted);
        Assert.True(accepted.HasCurrentInputInspection);
    }

    private static async ValueTask<CapabilityActionReadinessSnapshot?> ResolveReadinessAsync(
        ActiveSessionSnapshot accepted,
        ResolvedCapability capability,
        CancellationToken cancellationToken)
    {
        var request =
            RuntimeDependencyReadinessRequest.FromResolvedCapability(
                capability,
                accepted.AuthoringRevision);
        if (request.Dependencies.Count == 0)
        {
            return null;
        }
        CapabilityActionReadinessSnapshot readiness =
            await CapabilityActionReadinessResolver.RefreshAndResolveAsync(
                CapabilityAdmissionSnapshot.FromResolvedCapability(
                    capability,
                    accepted.AuthoringRevision),
                accepted.InputSlotStatuses.Select(static status =>
                    new CapabilityChildReadiness(
                        status.SlotId,
                        ResolvedChildReadiness.Ready)),
                request,
                ReadyRuntimeDependencyProvider.Instance,
                runtimeDependencyGeneration: 1,
                static generation => generation == 1,
                cancellationToken);
        return CapabilityActionReadinessResolver.RequireRuntimeDependenciesForPreview(readiness);
    }

    private static void AssertOperationsSucceededAndConstrained(CompositionRunResult result)
    {
        Assert.Equal(result.ResolvedCapability!.CompiledComposition.Plan.OrderedOperations.Count,
            result.Report.Operations.Count);
        Assert.All(result.Report.Operations, operation =>
        {
            Assert.Equal(OperationRunStatus.Succeeded, operation.Status);
            if (operation.Kind != CompositionOperationKind.RunExternalProcessor)
            {
                return;
            }
            Assert.NotNull(operation.ProcessorId);
            Assert.NotNull(operation.ToolBindingId);
            Assert.NotEmpty(operation.ProcessorAllowedReadRanges);
            Assert.NotEmpty(operation.ProcessorAllowedWriteRanges);
            Assert.All(operation.ProcessorAllowedReadRanges, range =>
            {
                Assert.InRange(range.Start, 0, result.OutputSize);
                Assert.InRange(range.EndExclusive, 1, result.OutputSize);
            });
            Assert.All(operation.ProcessorAllowedWriteRanges, range =>
            {
                Assert.InRange(range.Start, 0, result.OutputSize);
                Assert.InRange(range.EndExclusive, 1, result.OutputSize);
            });
        });
    }

    private static string FormatPreparationFailure(
        CanonicalFormalRouteRuntimeCase runtimeCase,
        IEnumerable<CompositionIssue> issues)
    {
        return $"{runtimeCase.CaseId}{Environment.NewLine}" +
            string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}"));
    }

    private static string Failure(
        CanonicalFormalRouteRuntimeCase runtimeCase,
        CompositionRunResult result)
    {
        return $"{runtimeCase.CaseId}{Environment.NewLine}" +
            CompositionRunReportJson.Serialize(result);
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_'));
    }

    private sealed class ReadyRuntimeDependencyProvider : IRuntimeDependencyReadinessProvider
    {
        internal static ReadyRuntimeDependencyProvider Instance { get; } = new();

        public ValueTask<RuntimeDependencyReadinessSnapshot> RefreshAsync(
            RuntimeDependencyReadinessRequest request,
            long generation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new RuntimeDependencyReadinessSnapshot(
                request.RouteId,
                request.CapabilityFingerprint,
                request.CompilationFingerprint,
                request.ResolutionToken,
                request.AuthoringRevision,
                generation,
                DateTimeOffset.UnixEpoch,
                request.Dependencies.Select(static dependency =>
                    RuntimeDependencyEntry.Ready(
                        dependency.ProcessorId,
                        dependency.ToolBindingId))));
        }
    }
}
