using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
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
