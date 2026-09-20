using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Proves deployed Standard Merge profiles consume the shared headless contract.</summary>
public sealed partial class HeadlessInputSlotInspectionContractTests
{
    private readonly IsolatedBootstrapTestHost _host = new();

    /// <summary>NT51928 optional LDC selection resolves the exact 512-KiB route and slot set.</summary>
    [Fact]
    public void StandardMergeAuthoringSnapshotTracksSelectedMapVariant()
    {
        ReloadCatalog();
        CompiledAuthoringSelectionSnapshot withoutLdc =
            _host.Services.StandardMergeAuthoring.GetAuthoringSnapshot(
                "NT51928",
                [CompositionAddressSpaceIds.DpInput, CompositionAddressSpaceIds.TpInput],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.DpInput] =
                        new FileStamp(0x80000, new string('a', 64)),
                },
                new AuthoringRevision(1));
        CompiledAuthoringSelectionSnapshot withLdc =
            _host.Services.StandardMergeAuthoring.GetAuthoringSnapshot(
                "NT51928",
                [
                    CompositionAddressSpaceIds.DpInput,
                    CompositionAddressSpaceIds.TpInput,
                    CompositionAddressSpaceIds.LdcInput,
                ],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.DpInput] =
                        new FileStamp(0x80000, new string('a', 64)),
                },
                new AuthoringRevision(1));

        AuthoringCapabilityRoute withoutRoute = Assert.Single(withoutLdc.Catalog.Routes);
        AuthoringCapabilityRoute withRoute = Assert.Single(withLdc.Catalog.Routes);
        Assert.Equal(withoutRoute.Identity.RouteId, withRoute.Identity.RouteId);
        Assert.Equal(withoutRoute.CapabilityFingerprint, withRoute.CapabilityFingerprint);
        Assert.NotEqual(withoutRoute.CompilationFingerprint, withRoute.CompilationFingerprint);
        Assert.DoesNotContain(withoutRoute.SlotDefinitions, static slot =>
            slot.DefinitionId == CompositionAddressSpaceIds.LdcInput);
        Assert.True(withoutLdc.Slots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.LdcInput).CanSelect);
        Assert.Contains(withRoute.SlotDefinitions, static slot =>
            slot.DefinitionId == CompositionAddressSpaceIds.LdcInput);
    }

    /// <summary>NT51928 LDC selection keeps its exact 512-KiB map instead of falling back to no-LDC.</summary>
    [Fact]
    public void StandardMergeAuthoringSnapshotProjectsSelectionDrivenMapWithoutFallback()
    {
        ReloadCatalog();
        CompiledAuthoringSelectionSnapshot snapshot =
            _host.Services.StandardMergeAuthoring.GetAuthoringSnapshot(
                "NT51928",
                [
                    CompositionAddressSpaceIds.DpInput,
                    CompositionAddressSpaceIds.TpInput,
                    CompositionAddressSpaceIds.LdcInput,
                ],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.DpInput] =
                        new FileStamp(0x40000, new string('a', 64)),
                },
                new AuthoringRevision(2));

        AuthoringCapabilityRoute route = Assert.Single(snapshot.Catalog.Routes);
        InputSelectionMemberReadiness ldc = snapshot.Slots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.LdcInput);
        Assert.NotNull(route.CompilationFingerprint);
        Assert.Contains(route.SlotDefinitions, static slot =>
            slot.DefinitionId == CompositionAddressSpaceIds.LdcInput);
        Assert.Equal(ResolvedChildReadiness.Ready, ldc.Readiness);
        Assert.True(ldc.CanSelect);
        Assert.Null(ldc.NextAction);
        Assert.Empty(snapshot.Issues);
    }

    /// <summary>NT51928 short DP retains its reviewed pad-short warning under the selected exact LDC map.</summary>
    [Fact]
    public void StandardMergeBatchRetainsPadShortWarningUnderSelectedExactMap()
    {
        ReloadCatalog();
        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(_host.Canonical,
                "NT51928",
                [
                    new FirmwareInspectionSnapshotInput(
                        "dp",
                        "dp.bin",
                        AuthoringRevision: 3,
                        StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
                    new FirmwareInspectionSnapshotInput(
                        "tp",
                        "tp.bin",
                        AuthoringRevision: 3,
                        StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
                    new FirmwareInspectionSnapshotInput(
                        "ldc",
                        "ldc.bin",
                        AuthoringRevision: 3,
                        StandardMergeAddressSpaceId: CompositionAddressSpaceIds.LdcInput),
                ],
                path => path switch
                {
                    "dp.bin" => new byte[0x40000],
                    "tp.bin" => new byte[0x35000],
                    _ => new byte[0x80000],
                });

        Assert.Equal(3, results.Count);
        AuthoringInputSlotStatus[] statuses =
        [
            .. results.Select(result => Assert.IsType<AuthoringInputSlotStatus>(
                result.Inspection.InputSlotStatus)),
        ];
        Assert.All(statuses, static status =>
        {
            Assert.Equal(ResolvedChildReadiness.Ready, status.Readiness);
            Assert.NotNull(status.CompilationFingerprint);
            Assert.True(status.IsTerminal);
        });
        AuthoringInputSlotStatus dp = results
            .Single(static result => result.InspectionId == "dp")
            .Inspection.InputSlotStatus!;
        Assert.Equal(AuthoringSlotLifecycle.Warning, dp.InspectionLifecycle);
        Assert.NotNull(dp.InspectionIssueCode);
        Assert.False(dp.BlocksBuild);
    }

    /// <summary>A deployed Standard Merge section source reaches terminal health without Avalonia.</summary>
    [Fact]
    public void StandardMergeProfilePublishesTerminalSlotHealth()
    {
        ReloadCatalog();
        bool compiled = _host.Canonical.Compiler.TryCompileStandardMerge(
            "NT51929",
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(compiled, FormatIssues(issues));

        ResolvedCapability capability = _host.Canonical.Catalog
            .ResolveCurrentCompilation(composition!)!;
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

    /// <summary>The desktop headless batch returns one exact Standard Merge compilation identity.</summary>
    [Fact]
    public void StandardMergeBatchPublishesOneTerminalCompilation()
    {
        ReloadCatalog();
        byte[] source = [.. Enumerable.Range(0, 0x40000).Select(static index => (byte)index)];
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        FirmwareInspectionSnapshotInput[] inputs =
        [
            new(
                "dp",
                "dp.bin",
                AuthoringRevision: 7,
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
            new(
                "tp",
                "tp.bin",
                AuthoringRevision: 7,
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
        ];

        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(_host.Canonical,
                "NT51926",
                inputs,
                path =>
                {
                    reads[path] = reads.GetValueOrDefault(path) + 1;
                    return source;
                });

        AuthoringInputSlotStatus[] statuses =
        [
            .. results.Select(result => Assert.IsType<AuthoringInputSlotStatus>(
                result.Inspection.InputSlotStatus)),
        ];
        Assert.All(reads.Values, static count => Assert.Equal(1, count));
        Assert.All(statuses, static status =>
        {
            Assert.True(status.IsTerminal);
            Assert.Equal(new AuthoringRevision(7), status.AuthoringRevision);
        });
        _ = Assert.Single(statuses.Select(static status => status.CompilationFingerprint).Distinct());
    }

    private FirmwareInspectionSnapshot InspectStandardMergeInput(
        string icId,
        string path,
        AuthoringRevision revision,
        int length)
    {
        return Assert.Single(BuiltInFirmwareInspection.InspectFirmwareBatch(_host.Canonical,
            icId,
            [new FirmwareInspectionSnapshotInput(
                "dp",
                path,
                AuthoringRevision: revision.Value,
                StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput)],
            _ => new byte[length])).Inspection;
    }

    private static AuthoringInputSlotStatus StatusForRoute(
        AuthoringCapabilityCatalogSnapshot catalog,
        AuthoringCapabilityRoute route,
        AuthoringRevision revision,
        string selectedPath)
    {
        return new AuthoringInputSlotStatus(
            route.Identity,
            catalog.ResolutionToken,
            revision,
            route.CapabilityFingerprint,
            route.CompilationFingerprint,
            Ready(CompositionAddressSpaceIds.DpInput),
            CompositionAddressSpaceIds.DpInput,
            AuthoringSlotLifecycle.Verified,
            FileStamp.FromBytes([1]),
            inspection: null,
            selectedPath);
    }

    private static void AssertStaleWithoutPublication(
        AuthoringSessionState session,
        ActiveSessionSnapshot beforeCompletion,
        AuthoringSessionTransitionResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, result.Issue!.Code);
        Assert.Same(beforeCompletion, session.CurrentSnapshot);
        Assert.Empty(session.CurrentSnapshot!.InputSlotStatuses);
        Assert.Empty(session.CurrentSnapshot.DerivedPublications);
    }

    private void ReloadCatalog()
    {
        CapabilityCatalogReloadResult reload =
            _host.Catalog.Reload(
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
