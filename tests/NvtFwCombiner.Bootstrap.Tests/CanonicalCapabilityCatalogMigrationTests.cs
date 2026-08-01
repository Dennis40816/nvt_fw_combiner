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
    /// <summary>
    /// The trusted source joins policy references and exact canonical TP Header
    /// references to the existing compiler output without copying geometry.
    /// </summary>
    [Fact]
    public void SourceMaterializesNt51929WithCanonicalMetadataReferences()
    {
        var source = new CanonicalCapabilityCatalogMigrationSource();

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);
        Assert.True(
            loaded.Succeeded,
            string.Join(
                Environment.NewLine,
                loaded.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        CanonicalCapabilityDefinition definition =
            loaded.Candidate!.Definitions.Single(candidate =>
                candidate.Identity.IcId == "NT51929" &&
                candidate.Identity.WorkflowId == "standard-merge");
        CompiledComposition composition = definition.CompiledComposition;

        Assert.Equal("NT51929", definition.Identity.IcId);
        Assert.Equal("nt51929-standard-merge-256k", definition.Identity.MapVariant);
        Assert.Equal(
            "c8f1268a871cfd571ff41694a71c85e6364bbe1fca6f3a7264cce77103b214a9",
            definition.CapabilityFingerprint);
        Assert.NotEqual(
            definition.CapabilityFingerprint,
            composition.CompilationFingerprint);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, composition.Eligibility);
        Assert.Equal(0x40000, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            composition.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.All(
            composition.Plan.OrderedOperations,
            static operation => Assert.Null(operation.ExternalProcessorInvocation));
        MetadataPlanEntry readModel = Assert.Single(
            definition.MetadataPlan.Entries,
            static entry => StringComparer.Ordinal.Equals(
                entry.BindingId,
                "type-ab-tp-flash-header-read-model"));
        MetadataPlanEntry copyReference = Assert.Single(
            definition.MetadataPlan.Entries,
            static entry => StringComparer.Ordinal.Equals(
                entry.BindingId,
                "type-ab-tp-flash-header-copy-reference"));
        Assert.Same(
            readModel.StructureDefinition,
            copyReference.StructureDefinition);
        Assert.Equal(
            FirmwareMetadataStructureKind.TpFlashHeader,
            readModel.StructureDefinition.Definition.StructureKind);
        Assert.Equal("tp-input", readModel.SpaceId);
        Assert.Equal("tp-input", readModel.SlotId);
        Assert.Equal(
            [
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Span,
                    "complete-header"),
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Series,
                    "dlm-crc-series"),
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Group,
                    "header-integrity-values"),
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Group,
                    "tp-bank-relative-start-addresses"),
            ],
            readModel.TargetReferences);
        Assert.Equal(
            [
                MetadataReferencePurpose.Inspection,
                MetadataReferencePurpose.Formatting,
                MetadataReferencePurpose.MemoryProjection,
                MetadataReferencePurpose.ReportClassification,
            ],
            readModel.Purposes);
        Assert.Equal(
            [
                new FirmwareMetadataReferenceTarget(
                    FirmwareMetadataReferenceTargetKind.Span,
                    "complete-header"),
            ],
            copyReference.TargetReferences);
        Assert.Equal(
            [MetadataReferencePurpose.Copy],
            copyReference.Purposes);
    }

    /// <summary>The NT51929 DP Replace route references one canonical DPCMI declaration and selected DP slot.</summary>
    [Fact]
    public void SourceMaterializesNt51929DpReplaceDpcmiPlan()
    {
        var source = new CanonicalCapabilityCatalogMigrationSource();

        CapabilityCatalogLoadResult loaded =
            source.Load(TestContext.Current.CancellationToken);
        Assert.True(
            loaded.Succeeded,
            string.Join(
                Environment.NewLine,
                loaded.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        CanonicalCapabilityDefinition definition =
            loaded.Candidate!.Definitions.Single(candidate =>
                candidate.Identity.IcId == "NT51929" &&
                candidate.Identity.WorkflowId == "dp-replace");
        MetadataPlanEntry entry = Assert.Single(definition.MetadataPlan.Entries);

        Assert.Equal("NT51929", definition.Identity.IcId);
        Assert.Equal(
            "nt51919-nt51929-nt51932-perfect-map-256k",
            definition.Identity.MapVariant);
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
                MetadataReferencePurpose.Validation,
                MetadataReferencePurpose.OutputNaming,
                MetadataReferencePurpose.Display,
                MetadataReferencePurpose.Version,
            ],
            entry.Purposes);
    }

    /// <summary>One reviewed NT51928 capability binds both admitted maps to distinct exact compilations.</summary>
    [Fact]
    public void Nt51928DualCapacityKeepsDefinitionIdentityAndChangesCompilationIdentity()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        ResolvedCapabilityRoute route = reload.Snapshot!.DynamicRoutes.Single(
            candidate =>
                candidate.Identity.IcId == "NT51928" &&
                candidate.Identity.WorkflowId == "dp-replace");
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value["NT51928"];

        registration.TryCompile(
            0x40000,
            requestedTopology: null,
            [.. registration.InputSelectionGroupMemberSlotIds.Take(1)],
            out CompiledComposition? compact,
            out IReadOnlyList<CompositionIssue> compactIssues);
        registration.TryCompile(
            0x80000,
            requestedTopology: null,
            registration.InputSelectionGroupMemberSlotIds,
            out CompiledComposition? extended,
            out IReadOnlyList<CompositionIssue> extendedIssues);
        ResolvedCapability compactCapability = route.BindCompilation(compact!);
        ResolvedCapability extendedCapability = route.BindCompilation(extended!);

        Assert.True(reload.Succeeded);
        Assert.Empty(compactIssues);
        Assert.Empty(extendedIssues);
        Assert.Equal(
            CanonicalDynamicRouteInventory.Nt51928DualCapacityMapVariantSetId,
            route.Identity.MapVariant);
        Assert.Equal(
            ["nt51928-standard-merge-256k", "nt51928-standard-merge-512k"],
            route.CompilationContract.AllowedMapVariantIds);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, route.Evidence.Value);
        Assert.Equal(
            route.CapabilityFingerprint,
            compactCapability.CompiledComposition.CapabilityFingerprint);
        Assert.Equal(
            route.CapabilityFingerprint,
            extendedCapability.CompiledComposition.CapabilityFingerprint);
        Assert.NotEqual(
            compactCapability.CompiledComposition.CompilationFingerprint,
            extendedCapability.CompiledComposition.CompilationFingerprint);
    }

    /// <summary>A stale policy fingerprint rejects the complete candidate.</summary>
    [Fact]
    public void SourceRejectsStaleCapabilityFingerprint()
    {
        CanonicalCapabilityPolicySnapshot current =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute route = current.Routes.Single(candidate =>
            candidate.Identity.IcId == "NT51929" &&
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

    /// <summary>Every static Standard Merge route resolves and compiles through one canonical snapshot.</summary>
    [Fact]
    public void OtherStandardMergeRoutesUsePublishedCanonicalSnapshot()
    {
        bool recognized = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51923",
            dpInputLength: 0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        CapabilityResolutionResult canonical =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(
                "NT51923");

        Assert.True(recognized);
        Assert.Empty(issues);
        Assert.True(canonical.Succeeded);
        Assert.Same(canonical.Capability!.CompiledComposition, composition);
    }

    /// <summary>Retired ICs resolve to the same stable unsupported result and cannot compile through migration adapters.</summary>
    [Theory]
    [InlineData("NT51920")]
    [InlineData("NT51925")]
    [InlineData("NT51930")]
    [InlineData("NT51931")]
    public void RetiredIcRoutesFailClosed(string icId)
    {
        CapabilityResolutionResult standard =
            WorkbenchCompositionService.ResolveCanonicalStandardMergeCapability(icId);
        CapabilityResolutionResult dp =
            WorkbenchCompositionService.ResolveCanonicalDpReplaceCapability(icId);
        bool standardRecognized = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength: 0x40000,
            out CompiledComposition? standardComposition,
            out IReadOnlyList<CompositionIssue> standardIssues);
        bool dpRecognized = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            icId,
            baseCapacity: 0x40000,
            out CompiledComposition? dpComposition,
            out IReadOnlyList<CompositionIssue> dpIssues);

        Assert.False(standard.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, standard.Issue!.Code);
        Assert.False(dp.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, dp.Issue!.Code);
        Assert.False(standardRecognized);
        Assert.Null(standardComposition);
        Assert.Empty(standardIssues);
        Assert.False(dpRecognized);
        Assert.Null(dpComposition);
        Assert.Empty(dpIssues);
    }

    /// <summary>The policy-facing DP route compiles the perfect-like family map directly.</summary>
    [Fact]
    public void Nt51929DpReplacePolicyPinsPerfectLikeCompiledIdentity()
    {
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value["NT51929"];

        registration.TryCompile(
            0x40000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.Equal(
            "nt51919-nt51929-nt51932-perfect-map-256k",
            composition.V2Details!.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(
            "3d937f93a0cf0714b8d13ab5480d7f65a27da04a5c78aaab7a53ba25fb8a200c",
            composition.CompilationFingerprint);
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
