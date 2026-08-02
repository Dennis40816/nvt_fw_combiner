using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves deployed Standard Merge and DP Replace profiles consume the shared headless contract.</summary>
[Collection(CanonicalCapabilityCatalogPublicationGroup.Name)]
public sealed class HeadlessInputSlotInspectionContractTests
{
    /// <summary>NT51928 dependent slots remain pending without inventing a discovery compilation.</summary>
    [Fact]
    public void DpReplaceMissingReferencePublishesPreCompilationReadiness()
    {
        ReloadCatalog();
        bool resolved = WorkbenchCompositionService.TryResolveBuiltInV2DpReplaceInputSelection(
            "NT51928",
            baseCapacity: null,
            ["initial-code-replacement"],
            out InputSelectionReadinessSnapshot? readiness,
            out IReadOnlyList<CompositionIssue> readinessIssues);
        Assert.True(resolved, FormatIssues(readinessIssues));
        InputSelectionMemberReadiness member = readiness!.Groups
            .SelectMany(static group => group.Members)
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.SlotId,
                "initial-code-replacement"));

        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value["NT51928"];
        registration.TryCompile(
            0x40000,
            out CompiledComposition? discovery,
            out IReadOnlyList<CompositionIssue> discoveryIssues);
        Assert.Empty(discoveryIssues);
        CompiledInputSpaceBinding binding = discovery!.V2Details.InputContract.SpaceBindings
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.SlotId,
                member.SlotId));

        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        Assert.True(reload.Succeeded, string.Join("; ", reload.Issues.Select(static issue => issue.Message)));
        ResolvedCapabilityRoute route = catalog.CurrentSnapshot!.DynamicRoutes.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.Identity.IcId, "NT51928") &&
            StringComparer.Ordinal.Equals(candidate.Identity.WorkflowId, ExperienceIds.DpReplace));

        AuthoringInputSlotStatus status = AuthoringInputSlotInspectionService.ProjectReadiness(
            route,
            new AuthoringRevision(3),
            member,
            binding);

        Assert.Equal(ResolvedChildReadiness.PendingInput, status.Readiness);
        Assert.False(status.CanSelect);
        Assert.Equal(
            InputSelectionNextActionKind.LoadArtifactFirst,
            status.ReadinessNextAction!.Kind);
        Assert.Equal(CompositionAddressSpaceIds.ReferenceBase, status.ReadinessNextAction.SubjectId);
        Assert.Equal(route.CapabilityFingerprint, status.CapabilityFingerprint);
        Assert.Null(status.CompilationFingerprint);
        Assert.Null(status.InspectionLifecycle);
    }

    /// <summary>The existing compiler owns atomic dependent-selection order normalization.</summary>
    [Fact]
    public void DpReplaceCompiledSelectionIsOrderIndependent()
    {
        ReloadCatalog();
        string[] forward =
        [
            CompositionAddressSpaceIds.InitialCodeReplacement,
            CompositionAddressSpaceIds.LdcReplacement,
        ];
        string[] reverse = [.. forward.Reverse()];

        bool firstCompiled = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            0x80000,
            forward,
            out CompiledComposition? first,
            out IReadOnlyList<CompositionIssue> firstIssues);
        bool secondCompiled = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            0x80000,
            reverse,
            out CompiledComposition? second,
            out IReadOnlyList<CompositionIssue> secondIssues);

        Assert.True(firstCompiled, FormatIssues(firstIssues));
        Assert.True(secondCompiled, FormatIssues(secondIssues));
        Assert.Equal(first!.CompilationFingerprint, second!.CompilationFingerprint);
        Assert.Equal(
            first.Plan.OrderedOperations.Select(static operation => operation.OperationId),
            second.Plan.OrderedOperations.Select(static operation => operation.OperationId));
    }

    /// <summary>A deployed Standard Merge section source reaches terminal health without Avalonia.</summary>
    [Fact]
    public void StandardMergeProfilePublishesTerminalSlotHealth()
    {
        ReloadCatalog();
        bool compiled = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51929",
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(compiled, FormatIssues(issues));

        ResolvedCapability capability = WorkbenchCompositionService
            .ResolveCanonicalCapabilityForRun(composition!)!;
        (CompiledInputSpaceBinding binding, _, AddressSpace space) =
            SelectSource(capability, static candidate =>
                candidate.ArtifactClass != CompiledInputArtifactClass.ReferenceImage);
        AuthoringInputSlotStatus status = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(1),
            Ready(binding.SlotId),
            binding.AddressSpaceId,
            new byte[checked((int)space.Length)]);

        Assert.True(status.IsTerminal);
        Assert.NotEqual(AuthoringSlotLifecycle.Error, status.InspectionLifecycle);
        Assert.Equal(ExperienceIds.StandardMerge, status.WorkflowId);
    }

    /// <summary>A deployed DP Replace source reaches terminal health under its exact compilation.</summary>
    [Fact]
    public void DpReplaceProfilePublishesTerminalSlotHealth()
    {
        ReloadCatalog();
        bool compiled = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51929",
            baseCapacity: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(compiled && composition is not null, FormatIssues(issues));
        ResolvedCapability? capability =
            WorkbenchCompositionService.ResolveCanonicalCapabilityForRun(composition);
        Assert.NotNull(capability);

        (CompiledInputSpaceBinding binding, _, AddressSpace space) =
            SelectSource(capability, static candidate =>
                candidate.ArtifactClass != CompiledInputArtifactClass.ReferenceImage);
        AuthoringInputSlotStatus status = AuthoringInputSlotInspectionService.Inspect(
            capability,
            new AuthoringRevision(2),
            Ready(binding.SlotId),
            binding.AddressSpaceId,
            new byte[checked((int)space.Length)]);

        Assert.Equal(AuthoringSlotLifecycle.Verified, status.InspectionLifecycle);
        Assert.Equal(ExperienceIds.DpReplace, status.WorkflowId);
        Assert.Equal(composition.CompilationFingerprint, status.CompilationFingerprint);
    }

    /// <summary>The desktop batch reads once and returns the same DP terminal warning contract.</summary>
    [Fact]
    public void DpReplaceBatchPublishesProfileWarningFromSingleRead()
    {
        ReloadCatalog();
        byte[] uniformSource = new byte[0x40000];
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        string slotAddressSpaceId = CompositionAddressSpaceIds.InitialCodeReplacement;

        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51928",
                [
                    new WorkbenchFirmwareInspectionInput(
                        "reference",
                        "reference.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new WorkbenchFirmwareInspectionInput(
                        "dp-input",
                        "dp.bin",
                        DpReplaceAddressSpaceId: slotAddressSpaceId),
                ],
                path =>
                {
                    reads[path] = reads.GetValueOrDefault(path) + 1;
                    return path == "reference.bin" ? new byte[0x40000] : uniformSource;
                });

        Assert.All(reads.Values, static count => Assert.Equal(1, count));
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(static result => result.InspectionId == "dp-input").Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Warning, status.InspectionLifecycle);
        Assert.Equal("DP_UNIFORM_CONTENT_WARNING", status.InspectionIssueCode);
        Assert.False(status.BlocksBuild);
    }

    /// <summary>The reference can complete against the compiler-owned default minimum selection.</summary>
    [Fact]
    public void DpReplaceReferencePublishesTerminalDefaultCompilation()
    {
        ReloadCatalog();
        byte[] reference = new byte[0x40000];
        WorkbenchFirmwareInspectionResult result = Assert.Single(
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51928",
                [new WorkbenchFirmwareInspectionInput(
                    "reference",
                    "reference.bin",
                    DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => reference));

        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            result.Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Verified, status.InspectionLifecycle);
        Assert.NotNull(status.CompilationFingerprint);
        Assert.False(status.BlocksBuild);
    }

    /// <summary>One DP selection change republishes every selected file under one compilation identity.</summary>
    [Fact]
    public void DpReplaceBatchPublishesOneCompilationFingerprint()
    {
        ReloadCatalog();
        byte[] reference = new byte[0x40000];
        byte[] initialCode = [.. Enumerable.Range(0, 0x40000).Select(static index => (byte)index)];
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        WorkbenchFirmwareInspectionInput[] inputs =
        [
            new("reference", "reference.bin", DpReplaceAddressSpaceId:
                CompositionAddressSpaceIds.ReferenceBase, AuthoringRevision: 6),
            new("initial", "initial.bin", DpReplaceAddressSpaceId:
                CompositionAddressSpaceIds.InitialCodeReplacement, AuthoringRevision: 6),
        ];

        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51928",
                inputs,
                path =>
                {
                    reads[path] = reads.GetValueOrDefault(path) + 1;
                    return path == "reference.bin" ? reference : initialCode;
                });

        AuthoringInputSlotStatus referenceStatus = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(static result => result.InspectionId == "reference").Inspection.InputSlotStatus);
        AuthoringInputSlotStatus initialStatus = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(static result => result.InspectionId == "initial").Inspection.InputSlotStatus);
        Assert.Equal(referenceStatus.CompilationFingerprint, initialStatus.CompilationFingerprint);
        Assert.Equal(new AuthoringRevision(6), referenceStatus.AuthoringRevision);
        Assert.Equal(referenceStatus.AuthoringRevision, initialStatus.AuthoringRevision);
        Assert.Equal(AuthoringSlotLifecycle.Verified, referenceStatus.InspectionLifecycle);
        Assert.Equal(AuthoringSlotLifecycle.Verified, initialStatus.InspectionLifecycle);
        Assert.All(reads.Values, static count => Assert.Equal(1, count));
    }

    /// <summary>Unreadable selected input is terminal Error without a fabricated content identity.</summary>
    [Fact]
    public void DpReplaceUnreadableSourcePublishesTypedBlockingError()
    {
        ReloadCatalog();
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51928",
                [
                    new WorkbenchFirmwareInspectionInput(
                        "reference",
                        "reference.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new WorkbenchFirmwareInspectionInput(
                        "dp-input",
                        "missing.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.InitialCodeReplacement),
                ],
                path =>
                {
                    reads[path] = reads.GetValueOrDefault(path) + 1;
                    return path == "reference.bin" ? new byte[0x40000] : null;
                });

        Assert.All(reads.Values, static count => Assert.Equal(1, count));
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(static result => result.InspectionId == "dp-input").Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Error, status.InspectionLifecycle);
        Assert.Equal("input.inspection.source-unreadable", status.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.SelectReadableInput,
            status.InspectionNextAction);
        Assert.True(status.BlocksBuild);
        Assert.Null(status.FileStamp);
        Assert.Null(status.Inspection);
    }

    /// <summary>An unreadable Reference blocks every selected slot before compilation without inventing health.</summary>
    [Fact]
    public void DpReplaceUnreadableReferencePublishesBlockedPreCompilationReadiness()
    {
        ReloadCatalog();
        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51928",
                [
                    new WorkbenchFirmwareInspectionInput(
                        "reference",
                        "missing-reference.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new WorkbenchFirmwareInspectionInput(
                        "initial",
                        "initial.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.InitialCodeReplacement),
                ],
                path => path == "initial.bin" ? new byte[0x40000] : null);

        Assert.All(results, result =>
        {
            AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
                result.Inspection.InputSlotStatus);
            Assert.Equal(ResolvedChildReadiness.Blocked, status.Readiness);
            Assert.False(status.CanSelect);
            Assert.Equal(
                InputArtifactInspectionIssueCodes.SourceUnreadable,
                status.SelectionReadiness.IssueCode);
            Assert.Equal(
                InputSelectionNextActionKind.CorrectSelection,
                status.ReadinessNextAction!.Kind);
            Assert.Null(status.CompilationFingerprint);
            Assert.Null(status.InspectionLifecycle);
            Assert.Null(status.Inspection);
        });
    }

    /// <summary>An unsupported Reference capacity remains typed readiness rather than an empty inspection result.</summary>
    [Fact]
    public void DpReplaceUnsupportedReferenceCapacityPublishesBlockedPreCompilationReadiness()
    {
        ReloadCatalog();
        IReadOnlyList<WorkbenchFirmwareInspectionResult> results =
            WorkbenchCompositionService.InspectFirmwareBatch(
                "NT51950",
                [
                    new WorkbenchFirmwareInspectionInput(
                        "reference",
                        "reference.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new WorkbenchFirmwareInspectionInput(
                        "dp",
                        "dp.bin",
                        DpReplaceAddressSpaceId: CompositionAddressSpaceIds.DpReplacement),
                ],
                path => path == "reference.bin" ? new byte[0x60000] : new byte[0x40000]);

        Assert.All(results, result =>
        {
            AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
                result.Inspection.InputSlotStatus);
            Assert.Equal(ResolvedChildReadiness.Blocked, status.Readiness);
            Assert.Equal(
                CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                status.SelectionReadiness.IssueCode);
            Assert.Null(status.CompilationFingerprint);
            Assert.Null(status.InspectionLifecycle);
        });
    }

    private static void ReloadCatalog()
    {
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
                TestContext.Current.CancellationToken);
        Assert.True(reload.Succeeded, string.Join("; ", reload.Issues.Select(static issue => issue.Message)));
    }

    private static (CompiledInputSpaceBinding Binding, CompiledInputSlotRequirement Slot, AddressSpace Space)
        SelectSource(
            ResolvedCapability capability,
            Func<CompiledInputSlotRequirement, bool> predicate)
    {
        CompiledInputContract contract = capability.CompiledComposition.V2Details.InputContract;
        CompiledInputSlotRequirement slot = contract.Slots.First(predicate);
        CompiledInputSpaceBinding binding = contract.SpaceBindings.First(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, slot.SlotId));
        AddressSpace space = capability.CompiledComposition.Plan.AddressSpaces.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, binding.AddressSpaceId));
        return (binding, slot, space);
    }

    private static InputSelectionMemberReadiness Ready(string slotId)
    {
        return new InputSelectionMemberReadiness(
            slotId,
            IsSelected: true,
            ResolvedChildReadiness.Ready,
            CanSelect: true,
            Reason: null,
            NextAction: null);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(static issue => issue.Message));
    }
}
