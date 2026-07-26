using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests the first canonical route and the remaining one-way migration seam.</summary>
public sealed class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>The trusted source joins policy references to the existing compiler output.</summary>
    [Fact]
    public void SourceMaterializesNt51929WithoutCopyingFirmwareFacts()
    {
        var source = new CanonicalCapabilityCatalogMigrationSource();

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);
        CanonicalCapabilityDefinition definition =
            loaded.Candidate!.Definitions.Single(candidate =>
                candidate.Identity.WorkflowId == "standard-merge");
        CompiledComposition composition = definition.CompiledComposition;

        Assert.True(loaded.Succeeded);
        Assert.Equal("NT51929", definition.Identity.IcId);
        Assert.Equal("nt51929-standard-merge-256k", definition.Identity.MapVariant);
        Assert.Equal(definition.CapabilityFingerprint, composition.CompilationFingerprint);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Equal(0x40000, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.All(
            composition.Plan.OrderedOperations,
            static operation => Assert.Null(operation.ExternalProcessorInvocation));
        Assert.Empty(definition.MetadataPlan.Entries);
    }

    /// <summary>The NT51929 DP Replace route references one canonical DPCMI declaration and selected DP slot.</summary>
    [Fact]
    public void SourceMaterializesNt51929DpReplaceDpcmiPlan()
    {
        var source = new CanonicalCapabilityCatalogMigrationSource();

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);
        CanonicalCapabilityDefinition definition =
            loaded.Candidate!.Definitions.Single(candidate =>
                candidate.Identity.WorkflowId == "dp-replace");
        MetadataPlanEntry entry = Assert.Single(definition.MetadataPlan.Entries);

        Assert.True(loaded.Succeeded);
        Assert.Equal("NT51929", definition.Identity.IcId);
        Assert.Equal("nt51929-standard-merge-256k", definition.Identity.MapVariant);
        Assert.Equal("dpcmi-inspection", entry.BindingId);
        Assert.Equal("dp-replacement", entry.SpaceId);
        Assert.Equal("dp-replacement", entry.SlotId);
        Assert.Equal(DpcmiMetadataContract.StructureId, entry.StructureDefinition.StructureId);
        FirmwareRegionRelativeLocator locator =
            Assert.IsType<FirmwareRegionRelativeLocator>(entry.StructureDefinition.Locator);
        Assert.Equal("initial-code-cmd1-page0-anchor", locator.RegionId);
        Assert.Equal(DpcmiMetadataContract.FirstRegister, locator.Offset);
        Assert.Equal(
            [
                DpcmiMetadataContract.MajorVersionFieldId,
                DpcmiMetadataContract.MinorVersionFieldId,
                DpcmiMetadataContract.JiraHighFieldId,
                DpcmiMetadataContract.JiraLowFieldId,
            ],
            entry.FieldIds);
        Assert.Equal(
            [
                MetadataInspectionPurpose.Validation,
                MetadataInspectionPurpose.OutputNaming,
                MetadataInspectionPurpose.Display,
                MetadataInspectionPurpose.Version,
            ],
            entry.Purposes);
    }

    /// <summary>A stale policy fingerprint rejects the complete candidate.</summary>
    [Fact]
    public void SourceRejectsStaleCapabilityFingerprint()
    {
        CanonicalCapabilityPolicySnapshot current =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute route = current.Routes.Single(candidate =>
            candidate.Identity.WorkflowId == "standard-merge");
        string staleFingerprint = new('0', 64);
        CanonicalCapabilityPolicyRoute staleRoute = route with
        {
            CapabilityFingerprint = staleFingerprint,
            Authoring = Rebind(route.Authoring, staleFingerprint),
            Publication = Rebind(route.Publication, staleFingerprint),
            Evidence = Rebind(route.Evidence, staleFingerprint),
        };
        var source = new CanonicalCapabilityCatalogMigrationSource(
            () => current with { Routes = [staleRoute] });

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.SourceInvalid,
            Assert.Single(loaded.Issues).Code);
    }

    /// <summary>Workbench availability and compilation consume the same published capability and token.</summary>
    [Fact]
    public void Nt51929CompilationUsesPublishedCanonicalSnapshot()
    {
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
                TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");
        bool available = WorkbenchCompositionService.IsStandardMergeSupported(
            "NT51929");
        CapabilityResolutionResult afterAvailability =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");

        bool recognized = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51929",
            dpInputLength: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        CapabilityResolutionResult afterCompile =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51929");

        Assert.True(reload.Succeeded);
        Assert.True(resolution.Succeeded);
        Assert.True(available);
        Assert.True(recognized);
        Assert.Empty(issues);
        Assert.Same(resolution.Capability!.CompiledComposition, composition);
        Assert.Equal(
            resolution.Capability.ResolutionToken,
            afterAvailability.Capability!.ResolutionToken);
        Assert.Equal(
            resolution.Capability.ResolutionToken,
            afterCompile.Capability!.ResolutionToken);
    }

    /// <summary>NT51929 DP Replace compilation and metadata inspection share one published capability snapshot.</summary>
    [Fact]
    public void Nt51929DpReplaceUsesPublishedCanonicalSnapshotAndDpcmiAuthority()
    {
        CapabilityCatalogReloadResult reload =
            WorkbenchCompositionService.ReloadCanonicalCapabilityCatalog(
                TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution =
            WorkbenchCompositionService.ResolveCanonicalDpReplaceCapability(
                "NT51929");
        bool recognized = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51929",
            0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        byte[] dp = new byte[0x6000];
        dp[0] = 0x5A;
        dp[0x67] = 0xFE;
        dp[0x68] = 0xED;
        dp[0x401A] = 0x2E;
        dp[0x401B] = 0x03;
        dp[0x401C] = 0xA4;
        using var workspace = TempWorkspace.Create(
            "nvt-fw-combiner-canonical-dpcmi");
        string dpPath = workspace.Write("replacement-dp.bin", dp);

        WorkbenchDpVersionMetadata? version =
            WorkbenchCompositionService.TryReadDpVersionMetadata(
                "NT51929",
                dpPath);
        WorkbenchCmiDpCodeMetadata? cmi =
            WorkbenchCompositionService.TryReadCmiDpCodeMetadata(
                "NT51929",
                dpPath);
        WorkbenchOutputFileNameSuggestion outputName =
            WorkbenchCompositionService.CreateFlashCodeOutputFileName(
                "NT51929",
                [new WorkbenchOutputNameCandidate(
                    WorkbenchOutputNameCandidateKind.Dp,
                    dpPath)],
                new DateOnly(2026, 7, 26));

        Assert.True(reload.Succeeded);
        Assert.True(resolution.Succeeded);
        Assert.True(recognized);
        Assert.Empty(issues);
        Assert.Same(resolution.Capability!.CompiledComposition, composition);
        Assert.Equal(resolution.Capability.ResolutionToken, resolution.Capability.MetadataPlan.ResolutionToken);
        Assert.Equal("030A", version!.Value.VersionToken);
        Assert.Equal((byte)0x03, cmi!.Value.MajorVersionByte);
        Assert.Equal((byte)0x0A, cmi.Value.MinorVersionNibble);
        Assert.Equal((ushort)0x42E, cmi.Value.JiraNumber);
        Assert.Equal(0x401A, cmi.Value.Register16Offset);
        Assert.Equal("030A", outputName.DpVersionToken);
        Assert.Equal(
            "NT51929_FlashCode_D030ATxxxx_20260726.bin",
            outputName.FileName);
    }

    /// <summary>A declared but truncated DPCMI does not fall back to the competing legacy DP-version bytes.</summary>
    [Fact]
    public void Nt51929DpcmiFailureDoesNotFallBackToLegacyDpVersionReader()
    {
        byte[] truncated = new byte[0x100];
        truncated[0x67] = 0xFE;
        truncated[0x68] = 0xED;
        using var workspace = TempWorkspace.Create(
            "nvt-fw-combiner-canonical-dpcmi-truncated");
        string path = workspace.Write("truncated-dp.bin", truncated);

        Assert.Null(WorkbenchCompositionService.TryReadDpVersionMetadata(
            "NT51929",
            path));
        Assert.Null(WorkbenchCompositionService.TryReadCmiDpCodeMetadata(
            "NT51929",
            path));
    }

    /// <summary>Non-pilot routes remain executable only through the named migration adapter.</summary>
    [Fact]
    public void OtherStandardMergeRoutesRemainBehindMigrationAdapter()
    {
        bool recognized = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51930",
            dpInputLength: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        CapabilityResolutionResult canonical =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51930");

        Assert.True(recognized);
        Assert.NotNull(composition);
        Assert.Empty(issues);
        Assert.False(canonical.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, canonical.Issue!.Code);
    }

    private static PinnedCapabilityDecision<TValue> Rebind<TValue>(
        PinnedCapabilityDecision<TValue> decision,
        string fingerprint)
        where TValue : struct, Enum
    {
        return new PinnedCapabilityDecision<TValue>(
            decision.DecisionId,
            decision.RouteId,
            fingerprint,
            decision.Value,
            decision.SourceReference);
    }
}
