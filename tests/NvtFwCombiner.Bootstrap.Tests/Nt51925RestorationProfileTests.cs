using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Prepared NT51925 routes remain candidates until independent owner evidence is supplied.</summary>
public sealed class Nt51925RestorationProfileTests
{
    /// <summary>Only the requested workflows have blocked policy declarations, without support or Golden promotion.</summary>
    [Fact]
    public void OnlyStandardMergeAndCtrlRamReplaceAreDeclaredAsCandidates()
    {
        CanonicalCapabilityPolicyRoute[] routes =
            [.. BuiltInCanonicalCapabilityPolicy.Load().Routes.Where(static route => route.Identity.IcId == "NT51925")];

        Assert.Equal(9, routes.Length);
        Assert.Equal(
            [ExperienceIds.CtrlRamReplace, ExperienceIds.StandardMerge],
            routes.Select(static route => route.Identity.WorkflowId).Distinct().Order(StringComparer.Ordinal));
        Assert.All(routes, static route =>
        {
            Assert.Equal(CapabilityAuthoringAvailability.Unavailable, route.Authoring.Value);
            Assert.Equal(CapabilityPublicationStatus.Candidate, route.Publication.Value);
            Assert.Equal(CapabilityEvidenceStatus.ContractOnly, route.Evidence.Value);
        });
        Assert.DoesNotContain("NT51925", BootstrapTestHost.Canonical.Projection.GetIcIds());
        Assert.DoesNotContain(BuiltInV2RegistrationRegistry.AbMerge, static row => row.IcId == "NT51925");
        Assert.False(BuiltInV2RegistrationRegistry.GeneralMergeByIc.ContainsKey("NT51925"));
        Assert.False(BuiltInV2RegistrationRegistry.GeneralReplaceByIc.ContainsKey("NT51925"));
    }

    /// <summary>Declared effective intervals and typed topology plans select the prepared profiles.</summary>
    [Theory]
    [InlineData("1.4.1", IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, "nt51925-ctrlram-replace-fw141-runtime-single")]
    [InlineData("1.4.1", IcNumberInputMode.CascadeSelector, IcNumberSelectionTokens.Cascade, "nt51925-ctrlram-replace-fw141-runtime-cascade")]
    [InlineData("2.0.0", IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, "nt51925-ctrlram-replace-fw200-runtime-single")]
    [InlineData("2.0.0", IcNumberInputMode.CascadeSelector, IcNumberSelectionTokens.Cascade, "nt51925-ctrlram-replace-fw200-runtime-cascade")]
    public void CommonFwAndTopologyResolveTheExpectedProfile(
        string version, IcNumberInputMode mode, string token, string expectedProfileId)
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51925", version, out LegacyCombinerPostbuildProfile? profile, out string? issue), issue);
        LegacyCombinerPostbuildCommandPlan plan = profile!.ResolvePlan(new IcNumberSelection(mode, [token]));

        Assert.True(CtrlRamV2RouteRegistry.TryResolve(plan, out CtrlRamV2Route? route));
        Assert.Equal(expectedProfileId, route!.ProfileId);
        Assert.Equal("NT51925", route.Key.IcId);
        Assert.Equal(2, BuiltInV2BundleRegistry.All[route.BundleId].GetMapVariants(
            route.ProfileId, route.ProfileVersion, "NT51925", ExperienceIds.CtrlRamReplace,
            out IReadOnlyList<CompositionIssue> issues).Count);
        Assert.Empty(issues);
    }

    /// <summary>The Standard declaration is execution-admitted as an unverified candidate while authoring stays unavailable.</summary>
    [Fact]
    public void StandardCandidateIsExecutionAdmittedWhileAuthoringRemainsUnavailable()
    {
        CapabilityResolutionResult prepared = BootstrapTestHost.Canonical.Catalog.ResolveUniqueRoute(
            "NT51925", ExperienceIds.StandardMerge, "selector-free");
        CapabilityResolutionResult existing = BootstrapTestHost.Canonical.Catalog.ResolveUniqueRoute(
            "NT51926", ExperienceIds.StandardMerge, "selector-free");

        Assert.False(prepared.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, prepared.Issue!.Code);
        Assert.True(existing.Succeeded);
        ResolvedCapabilityRoute declaration = Assert.Single(BootstrapTestHost.Canonical.Catalog.GetCurrentSnapshot().DynamicRoutes,
            static row => row.Identity.IcId == "NT51925" && row.Identity.WorkflowId == ExperienceIds.StandardMerge);
        Assert.Equal(CapabilityAuthoringAvailability.Unavailable, declaration.Authoring.Value);
        CompiledComposition composition = Nt51925VersionSelectionTests.CompileStandard(Nt51925VersionSelectionTests.CreateTp(1, 4, 1));
        Assert.True(composition.IsV2StandardMergeOpenCandidate);
        Assert.Equal(2, declaration.CompilationContract.AllowedMapVariantIds.Count);
        MetadataPlanResolutionResult metadata = Assert.IsType<MetadataPlanResolutionResult>(
            ((IStandardMergeMetadataPlanQuery)BootstrapTestHost.Canonical.Compiler)
                .ResolveSourceEnvelopeMetadataPlan("NT51925", 0x40000));
        Assert.True(metadata.Succeeded);
        Assert.NotNull(metadata.MetadataPlan);
        Assert.Equal(declaration.ResolutionToken, metadata.MetadataPlan.ResolutionToken);
        MetadataPlanResolutionResult wrongLength = Assert.IsType<MetadataPlanResolutionResult>(
            ((IStandardMergeMetadataPlanQuery)BootstrapTestHost.Canonical.Compiler)
                .ResolveSourceEnvelopeMetadataPlan("NT51925", 0x30000));
        Assert.False(wrongLength.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, wrongLength.Issue?.Code);
        Assert.True(existing.Capability!.ExecutionAdmitted);
        Assert.False(BootstrapTestHost.Canonical.Catalog.HasAuthorableCapability("NT51925", ExperienceIds.StandardMerge));
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, composition.Eligibility);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, composition.V2Details.Provenance.Promotion.Stage);
        AssertUnchangedOperationContract(existing.Capability.CompiledComposition.Plan, composition.Plan);
        Assert.True(CapabilityPublicationCoherence.IsExecutionAdmitted(composition));
    }

    /// <summary>A Standard route without a version prerequisite retains its static metadata lookup.</summary>
    [Fact]
    public void StandardMetadataWithoutMapSelectionPrerequisiteKeepsItsExistingPlan()
    {
        Assert.Null(((IStandardMergeMetadataPlanQuery)BootstrapTestHost.Canonical.Compiler)
            .ResolveSourceEnvelopeMetadataPlan("NT51926", 0x40000));
        MetadataPlanResolutionResult metadata = BootstrapTestHost.Canonical.Catalog.ResolveUniqueMetadataPlan(
            "NT51926", ExperienceIds.StandardMerge, "selector-free");
        Assert.True(metadata.Succeeded);
        Assert.NotNull(metadata.MetadataPlan);
    }

    /// <summary>Only the explicitly declared Standard candidate satisfies the unchanged execution rule.</summary>
    [Fact]
    public void StandardMergeOpenCandidateRuleAdmitsOnlyTheNt51925Route()
    {
        Assert.DoesNotContain(BootstrapTestHost.Canonical.Catalog.GetCurrentSnapshot().Capabilities,
            static row => row.CompiledComposition.IsV2StandardMergeOpenCandidate);
        Assert.True(Nt51925VersionSelectionTests.CompileStandard(Nt51925VersionSelectionTests.CreateTp(2, 0, 0))
            .IsV2StandardMergeOpenCandidate);
    }

    /// <summary>Each Replace combination admits its candidate plan while authoring stays unavailable and all blockers remain.</summary>
    [Theory]
    [InlineData("141", 1, 0x30000)]
    [InlineData("141", 1, 0x40000)]
    [InlineData("141", 2, 0x30000)]
    [InlineData("141", 3, 0x30000)]
    [InlineData("141", 2, 0x40000)]
    [InlineData("141", 3, 0x40000)]
    [InlineData("200", 1, 0x30000)]
    [InlineData("200", 1, 0x40000)]
    [InlineData("200", 2, 0x30000)]
    [InlineData("200", 3, 0x30000)]
    [InlineData("200", 2, 0x40000)]
    [InlineData("200", 3, 0x40000)]
    public void RuntimeReplaceCandidatesAreExecutionAdmittedWhileAuthoringRemainsUnavailable(string fw, int chipCount, int capacity)
    {
        CompiledComposition prepared = CompileRuntime("NT51925", "0.1.0", fw, chipCount, capacity);
        CompiledComposition existing = CompileRuntime("NT51926", "0.4.0", fw, chipCount,
            capacity == 0x30000 ? 0x3C000 : capacity);

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, prepared.Eligibility);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, existing.Eligibility);
        Assert.Equal(CompiledProfilePromotionStage.ExecutableCandidate, prepared.V2Details.Provenance.Promotion.Stage);
        Assert.Equal(CompiledProfilePromotionStage.Supported, existing.V2Details.Provenance.Promotion.Stage);
        Assert.True(CapabilityPublicationCoherence.IsExecutionAdmitted(prepared));
        Assert.Equal(
            [
                ("nt51925-independent-golden", CompiledProfilePromotionBlockerKind.Golden),
                ("nt51925-official-map-and-postbuild", CompiledProfilePromotionBlockerKind.Map),
                ("nt51925-r3-owner-approval", CompiledProfilePromotionBlockerKind.HumanReview),
            ],
            prepared.V2Details.Provenance.Promotion.Blockers.Select(static blocker => (blocker.BlockerId, blocker.Kind)));
        AssertUnchangedOperationContract(existing.Plan, prepared.Plan);
        Assert.Equal(existing.Plan.OrderedOperations.Select(static item => item.SourceRange),
            prepared.Plan.OrderedOperations.Select(static item => item.SourceRange));
        Assert.Equal(capacity, prepared.Plan.Initializations.Single().Capacity);
        long vnStart = fw == "200" ? 0x2D000 : chipCount == 1 ? 0x26F80 : 0x2B780;
        Assert.Equal(new ByteRange(vnStart, 0x120), prepared.Plan.OrderedOperations[0].TargetRange);
        CompositionOperation postbuild = prepared.Plan.OrderedOperations[1];
        ByteRange[] expectedWrites = [new(0x18, 4), new(0x1C, 4), new(0x3C, 4), new(0x4C, 4), new(0x5C, 4), new(0xFC, 4),
            new(vnStart, 0x120), new(0x2F000, fw == "141" ? 0x780 : 0x800),
            .. fw == "200" ? new ByteRange[] { new(0x2E980, 0x100) } : []];
        Assert.Equal(expectedWrites.OrderBy(static range => range.Start), postbuild.DeclaredWriteRanges);
        Assert.Equal(expectedWrites.OrderBy(static range => range.Start), postbuild.ExternalProcessorInvocation!.AllowedWriteRanges);
        Assert.Equal([new ByteRange(0, 0x30000)], postbuild.ExternalProcessorInvocation.AllowedReadRanges);
        Assert.Equal(0x30000, Assert.Single(prepared.Plan.OrderedOperations,
            static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor).TargetRange.Length);
        var admitted = new CompositionRunRequest("prepared-runtime", prepared,
            [new InputArtifactBinding("reference-base", "reference-base", "reference", "reference.bin", CompiledInputArtifactClass.ReferenceImage),
             new InputArtifactBinding("vn-source", "vn-source", "replacement", "replacement.bin", CompiledInputArtifactClass.CtrlRamReplacement)],
            "nt51925-ctrlram-replace_UNVERIFIED.bin", new IcNumberSelection(chipCount == 1 ? IcNumberInputMode.SingleSelector : IcNumberInputMode.CascadeSelector,
                [chipCount == 1 ? IcNumberSelectionTokens.SingleChip : IcNumberSelectionTokens.Cascade]), outputFileNameIsOverride: false);
        Assert.Same(prepared, admitted.CompiledComposition);
        Assert.Equal("nt51925-ctrlram-replace_UNVERIFIED.bin", prepared.V2Details.OutputNamingRequirement.FileNameTemplate);
        Assert.False(prepared.V2Details.OutputNamingRequirement.AllowOverride);

        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = "reference.bin",
        };
        var inputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = CreateRuntimeReference(fw, capacity),
        };
        CtrlRamAuthoringSessionPreparation authoring = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            session, "NT51925", chipCount == 1 ? IcNumberSelectionTokens.SingleChip : IcNumberSelectionTokens.Cascade,
            paths, inputs);
        Assert.False(authoring.Succeeded);
        Assert.Null(authoring.AcceptedSession);
        Assert.Null(session.CurrentSnapshot);
        Assert.NotEmpty(authoring.Issues);

        var identity = new CapabilityRouteIdentity("NT51925", ExperienceIds.CtrlRamReplace,
            chipCount == 1 ? "1-ic" : "2-plus-ic",
            $"nt51925-ctrlram-fw{fw}-{(chipCount == 1 ? "single" : "cascade")}-{(capacity == 0x30000 ? "tp-work-192k" : "full-flash-256k")}");
        CapabilityRouteResolutionResult resolution = BootstrapTestHost.Canonical.Catalog.ResolveDynamicRoute(identity.RouteId);
        Assert.False(resolution.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.AuthoringUnavailable, resolution.Issue!.Code);
    }

    /// <summary>The restored declaration cannot resolve any unrequested workflow.</summary>
    [Theory]
    [InlineData(ExperienceIds.AbMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    [InlineData(ExperienceIds.GeneralReplace)]
    [InlineData(ExperienceIds.DpReplace)]
    public void UnrequestedWorkflowsRemainUnavailable(string workflow)
    {
        CapabilityResolutionResult result = BootstrapTestHost.Canonical.Catalog.ResolveUniqueRoute(
            "NT51925", workflow, "1-ic", 0x40000);
        Assert.False(result.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, result.Issue!.Code);
    }

    private static CompiledComposition CompileRuntime(string ic, string profileVersion, string fw, int chipCount, int capacity)
    {
        byte[] reference = CreateRuntimeReference(fw, capacity, ic);
        string topology = chipCount == 1 ? "single" : "cascade";
        V2CompositionPlanCompileResult result = BuiltInV2BundleRegistry.All[$"{ic.ToLowerInvariant()}-ctrlram-replace-candidate"]
            .CompileRuntimeReferenceReplace(
                $"{ic.ToLowerInvariant()}-ctrlram-replace-fw{fw}-runtime-{topology}", profileVersion, ic,
                ExperienceIds.CtrlRamReplace,
                new TopologySelection(chipCount, topology, TopologySelectionSource.Requested, "ic-number"),
                [new FirmwareArtifactPayload("reference-base", reference)],
                new V2RuntimeReferenceReplaceCompileRequest(
                    [new V2ExplicitMappingInputBinding("reference-base", "reference-base", capacity),
                     new V2ExplicitMappingInputBinding("vn-source", "ctrlram-source", 0x120)],
                    [new ExplicitMapping("replace-vn", 100, ExplicitMappingOperationKind.ReplaceRange,
                        "vn-source", new ByteRange(0, 0x120), CompositionAddressSpaceIds.OutputImage,
                        new ByteRange(ic == "NT51926" ? 0x315D0 : fw == "200" ? 0x2D000 : chipCount == 1 ? 0x26F80 : 0x2B780, 0x120), OverlapPolicy.Reject, alignment: 1,
                        reason: "Synthetic prefix replacement inside the owner-declared NT51925 VN region.")]));
        Assert.True(result.IsCompiled, string.Join(Environment.NewLine, result.Issues.Select(static issue => issue.Message)));
        return Assert.IsType<CompiledComposition>(result.CompiledComposition);
    }

    private static byte[] CreateRuntimeReference(string fw, int capacity, string ic = "NT51925")
    {
        byte[] reference = new byte[capacity];
        int backupStart = ic == "NT51925" ? 0x2F000 : 0x3B000;
        reference[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = fw == "141" ? (byte)1 : (byte)2;
        reference[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = fw == "141" ? (byte)4 : (byte)0;
        reference[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = fw == "141" ? (byte)1 : (byte)0;
        new byte[] { 0x00, 0x4E, 0x56, 0x54 }.CopyTo(reference, backupStart + 0xFFC);
        return reference;
    }

    private static void AssertUnchangedOperationContract(CompositionPlan expected, CompositionPlan actual)
    {
        Assert.Equal(expected.OutputSpaceId, actual.OutputSpaceId);
        Assert.Equal(expected.Initializations.Select(static item =>
            (item.Kind, item.TargetSpaceId, item.FillByte, item.ReferenceSpaceId)),
            actual.Initializations.Select(static item =>
            (item.Kind, item.TargetSpaceId, item.FillByte, item.ReferenceSpaceId)));
        Assert.Equal(expected.OrderedOperations.Select(static item =>
            (item.Sequence, item.Kind, item.SourceSpaceId, item.TargetSpaceId, item.OverlapPolicy, item.FillByte)),
            actual.OrderedOperations.Select(static item =>
            (item.Sequence, item.Kind, item.SourceSpaceId, item.TargetSpaceId, item.OverlapPolicy, item.FillByte)));
        Assert.Equal(expected.OrderedOperations.Select(static item => Convert.ToHexString(item.PatchBytes.Span)),
            actual.OrderedOperations.Select(static item => Convert.ToHexString(item.PatchBytes.Span)));
    }
}
