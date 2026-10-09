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

    /// <summary>Standard declarations publish for inspection while Application execution remains closed.</summary>
    [Fact]
    public void StandardCandidatePublishesItsOwnPlanWithoutExecutionPromotion()
    {
        CapabilityResolutionResult prepared = BootstrapTestHost.Canonical.Catalog.ResolveUniqueRoute(
            "NT51925", ExperienceIds.StandardMerge, "selector-free");
        CapabilityResolutionResult existing = BootstrapTestHost.Canonical.Catalog.ResolveUniqueRoute(
            "NT51926", ExperienceIds.StandardMerge, "selector-free");

        Assert.False(prepared.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.AuthoringUnavailable, prepared.Issue!.Code);
        Assert.True(existing.Succeeded);
        ResolvedCapability declaration = Assert.Single(BootstrapTestHost.Canonical.Catalog.GetCurrentSnapshot().Capabilities,
            static row => row.Identity.IcId == "NT51925" && row.Identity.WorkflowId == ExperienceIds.StandardMerge);
        Assert.False(declaration.ExecutionAdmitted);
        MetadataPlanResolutionResult metadata = BootstrapTestHost.Canonical.Catalog.ResolveUniqueMetadataPlan(
            "NT51925", ExperienceIds.StandardMerge, "selector-free");
        Assert.True(metadata.Succeeded);
        Assert.NotNull(metadata.MetadataPlan);
        Assert.True(existing.Capability!.ExecutionAdmitted);
        Assert.False(BootstrapTestHost.Canonical.Catalog.HasAuthorableCapability("NT51925", ExperienceIds.StandardMerge));
        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, declaration.CompiledComposition.Eligibility);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, existing.Capability.CompiledComposition.Eligibility);
        Assert.Equal(CompiledProfilePromotionStage.Compilable,
            declaration.CompiledComposition.V2Details.Provenance.Promotion.Stage);
        AssertSameBytePlan(existing.Capability.CompiledComposition.Plan, declaration.CompiledComposition.Plan);
        ArgumentException rejected = Assert.Throws<ArgumentException>(() => new CompositionRunRequest(
            "prepared-standard", declaration.CompiledComposition, [], "prepared.bin"));
        Assert.Equal("compiledComposition", rejected.ParamName);
    }

    /// <summary>Each declared capacity, Common FW version and topology retains the provisional copied values.</summary>
    [Theory]
    [InlineData("141", 1, 0x3C000)]
    [InlineData("141", 1, 0x40000)]
    [InlineData("141", 2, 0x3C000)]
    [InlineData("141", 2, 0x40000)]
    [InlineData("200", 1, 0x3C000)]
    [InlineData("200", 1, 0x40000)]
    [InlineData("200", 2, 0x3C000)]
    [InlineData("200", 2, 0x40000)]
    public void RuntimeDeclarationsKeepCopiedValuesWithoutExecutionAdmission(string fw, int chipCount, int capacity)
    {
        CompiledComposition prepared = CompileRuntime("NT51925", "0.1.0", fw, chipCount, capacity);
        CompiledComposition existing = CompileRuntime("NT51926", "0.4.0", fw, chipCount, capacity);

        Assert.Equal(CompiledCompositionEligibility.V2PlanCompiled, prepared.Eligibility);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, existing.Eligibility);
        Assert.Equal(CompiledProfilePromotionStage.Compilable, prepared.V2Details.Provenance.Promotion.Stage);
        Assert.Equal(CompiledProfilePromotionStage.Supported, existing.V2Details.Provenance.Promotion.Stage);
        Assert.NotEmpty(prepared.V2Details.Provenance.Promotion.Blockers);
        AssertSameBytePlan(existing.Plan, prepared.Plan);
        ArgumentException rejected = Assert.Throws<ArgumentException>(() => new CompositionRunRequest("prepared-runtime", prepared,
            [new InputArtifactBinding("reference-base", "reference-base", "reference", "reference.bin", CompiledInputArtifactClass.ReferenceImage),
             new InputArtifactBinding("vn-source", "vn-source", "replacement", "replacement.bin", CompiledInputArtifactClass.CtrlRamReplacement)],
            "prepared.bin", new IcNumberSelection(chipCount == 1 ? IcNumberInputMode.SingleSelector : IcNumberInputMode.CascadeSelector,
                [chipCount == 1 ? IcNumberSelectionTokens.SingleChip : IcNumberSelectionTokens.Cascade]), outputFileNameIsOverride: true));
        Assert.Equal("compiledComposition", rejected.ParamName);

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
            $"nt51925-ctrlram-fw{fw}-{(capacity == 0x3C000 ? "tp-work-240k" : "full-flash-256k")}");
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
                        new ByteRange(0x315D0, 0x120), OverlapPolicy.Reject, alignment: 1,
                        reason: "Synthetic prefix replacement for shared byte-contract comparison.")]));
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

    private static void AssertSameBytePlan(CompositionPlan expected, CompositionPlan actual)
    {
        Assert.Equal(expected.OutputSpaceId, actual.OutputSpaceId);
        Assert.Equal(expected.Initializations.Select(static item =>
            (item.Kind, item.TargetSpaceId, item.Capacity, item.FillByte, item.ReferenceSpaceId)),
            actual.Initializations.Select(static item =>
            (item.Kind, item.TargetSpaceId, item.Capacity, item.FillByte, item.ReferenceSpaceId)));
        Assert.Equal(expected.OrderedOperations.Count, actual.OrderedOperations.Count);
        for (int index = 0; index < expected.OrderedOperations.Count; index++)
        {
            CompositionOperation first = expected.OrderedOperations[index];
            CompositionOperation second = actual.OrderedOperations[index];
            Assert.Equal((first.Sequence, first.Kind, first.SourceSpaceId, first.SourceRange,
                first.TargetSpaceId, first.TargetRange, first.OverlapPolicy, first.FillByte),
                (second.Sequence, second.Kind, second.SourceSpaceId, second.SourceRange,
                second.TargetSpaceId, second.TargetRange, second.OverlapPolicy, second.FillByte));
            Assert.Equal(first.PatchBytes.ToArray(), second.PatchBytes.ToArray());
            Assert.Equal(first.DeclaredWriteRanges, second.DeclaredWriteRanges);
            Assert.Equal(first.ExternalProcessorInvocation?.AllowedReadRanges,
                second.ExternalProcessorInvocation?.AllowedReadRanges);
            Assert.Equal(first.ExternalProcessorInvocation?.AllowedWriteRanges,
                second.ExternalProcessorInvocation?.AllowedWriteRanges);
        }
    }
}
