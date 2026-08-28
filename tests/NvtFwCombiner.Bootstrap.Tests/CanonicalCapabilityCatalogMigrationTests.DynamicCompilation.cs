using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>Dynamic compilation contracts reject malformed definition bounds at construction.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsMalformedDefinitionBounds()
    {
        const string profileId = "profile";
        const string profileVersion = "1.0.0";
        const string semanticId = "compiler-semantic";
        string hash = new('a', 64);

        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                "not-a-sha256",
                ["map"],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                [""],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                [],
                semanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                ["map"],
                semanticId,
                [" "]));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                ["map"],
                CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId));
        _ = Assert.Throws<ArgumentException>(() =>
            new CanonicalCapabilityCompilationContract(
                profileId,
                profileVersion,
                hash,
                ["map"],
                CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId,
                allowsLogicalOutput: true));
    }

    /// <summary>CtrlRAM report semantics exist only when the reviewed Standard Merge profile declares them.</summary>
    [Fact]
    public void DynamicCtrlRamReportBindingsRequireProfileDeclaration()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        ResolvedCapabilityRoute[] ctrlRamRoutes =
        [
            .. reload.Snapshot!.DynamicRoutes.Where(static route =>
                route.Identity.WorkflowId == ExperienceIds.CtrlRamReplace),
        ];
        ResolvedCapabilityRoute[] reportless =
        [
            .. ctrlRamRoutes.Where(route =>
                route.Identity.IcId is "NT51919" or "NT51950" or "NT51951"),
        ];
        ResolvedCapabilityRoute[] reportful =
        [
            .. ctrlRamRoutes.Except(reportless),
        ];
        CanonicalCapabilityPolicySnapshot policy =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute[] policyRoutes =
        [
            .. policy.Routes.Where(static route =>
                route.Identity.WorkflowId == ExperienceIds.CtrlRamReplace),
        ];
        string[] tpRoutesAwaitingIndependentExpectedOutput =
        [
            "route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work",
            "route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work",
            "route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work",
        ];
        var expectedMaps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NT51917"] = "nt51927-standard-merge-256k",
            ["NT51923"] = "nt51923-standard-merge-256k",
            ["NT51926"] = "nt51926-standard-merge-256k",
            ["NT51927"] = "nt51927-standard-merge-256k",
            ["NT51928"] = "nt51928-standard-merge-512k",
            ["NT51929"] = "nt51929-standard-merge-256k",
            ["NT51932"] = "nt51932-standard-merge-256k",
        };

        Assert.True(reload.Succeeded);
        Assert.Equal(44, ctrlRamRoutes.Length);
        Assert.Equal(10, reportless.Length);
        Assert.Equal(34, reportful.Length);
        Assert.Equal(
            ctrlRamRoutes.Select(static route => route.Identity.RouteId)
                .Order(StringComparer.Ordinal),
            policyRoutes.Select(static route => route.Identity.RouteId)
                .Order(StringComparer.Ordinal));
        Assert.All(policyRoutes, route =>
        {
            Assert.EndsWith("-authoring-v3", route.Authoring.DecisionId, StringComparison.Ordinal);
            Assert.EndsWith("-publication-v5", route.Publication.DecisionId, StringComparison.Ordinal);
            string expectedEvidenceRevision = tpRoutesAwaitingIndependentExpectedOutput.Contains(
                route.Identity.RouteId,
                StringComparer.Ordinal)
                ? "-evidence-v4"
                : "-evidence-v3";
            Assert.EndsWith(
                expectedEvidenceRevision,
                route.Evidence.DecisionId,
                StringComparison.Ordinal);
        });
        Assert.All(reportless, route => Assert.DoesNotContain(
            route.CompilationContract.SemanticBindingIds,
            static binding => binding.StartsWith(
                "report-metadata-",
                StringComparison.Ordinal)));
        Assert.All(reportful, route =>
        {
            string[] reportBindings =
            [
                .. route.CompilationContract.SemanticBindingIds.Where(
                    static binding => binding.StartsWith(
                        "report-metadata-",
                        StringComparison.Ordinal)),
            ];
            Assert.Equal(4, reportBindings.Length);
            Assert.Contains(
                $"report-metadata-map:{expectedMaps[route.Identity.IcId]}",
                reportBindings,
                StringComparer.Ordinal);
            Assert.Equal(
                CtrlRamV2RouteRegistry.All
                    .Where(candidate => candidate.Key.IcId == route.Identity.IcId)
                    .SelectMany(static candidate =>
                        candidate.ReportMetadataPlan.ReportProjections)
                    .Select(static projection =>
                        $"report-metadata-slot:{projection.SpaceId}<-{projection.SlotId}")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
                reportBindings.Where(static binding => binding.StartsWith(
                        "report-metadata-slot:",
                        StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal));
        });
    }

    /// <summary>Map, compiler, and selection-group drift are independent admission failures.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsExactSemanticDrift()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
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
            out CompiledComposition? compiled,
            out IReadOnlyList<CompositionIssue> issues);
        CanonicalCapabilityCompilationContract expected =
            route.CompilationContract;

        var wrongMap = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            ["different-map"],
            expected.CompilerSemanticId,
            expected.SemanticBindingIds);
        var wrongCompiler = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            expected.AllowedMapVariantIds,
            "different-compiler-semantic",
            expected.SemanticBindingIds);
        var wrongSelectionGroup = new CanonicalCapabilityCompilationContract(
            expected.ProfileId,
            expected.ProfileVersion,
            expected.TrustedDefinitionSha256,
            expected.AllowedMapVariantIds,
            expected.CompilerSemanticId,
            ["different-selection-member"]);

        Assert.True(reload.Succeeded);
        Assert.Empty(issues);
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongMap).BindCompilation(compiled!));
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongCompiler).BindCompilation(compiled!));
        _ = Assert.Throws<ArgumentException>(() =>
            PublishWithContract(route, wrongSelectionGroup).BindCompilation(compiled!));
    }

    /// <summary>A compiler regression cannot erase a reviewed selection group.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsMissingCompiledSelectionGroup()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
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
            out CompiledComposition? compiled,
            out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition withoutSelectionGroup = WithoutSelectionGroups(
            compiled!);

        Assert.True(reload.Succeeded);
        Assert.Empty(issues);
        _ = Assert.Throws<ArgumentException>(() =>
            route.BindCompilation(withoutSelectionGroup));
    }

    /// <summary>A logical-output compiler cannot drift from the reviewed firmware family.</summary>
    [Fact]
    public void DynamicCompilationContractRejectsLogicalFamilyDrift()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        ResolvedCapabilityRoute route = reload.Snapshot!.DynamicRoutes.Single(
            candidate =>
                candidate.Identity.IcId == "NT51928" &&
                candidate.Identity.WorkflowId == "general-merge");
        GeneralMergeV2CandidateRegistration registration =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc["NT51928"];
        V2CompositionPlanCompileResult compile = registration.Bundle.CompileLogicalOutput(
            registration.ProfileId,
            registration.ProfileVersion,
            "NT51928",
            new V2LogicalOutputCompileRequest(
                new GeneralMergeOutputInitializer(16),
                [new V2ExplicitMappingInputBinding("source-a", "source", 4)],
                [new ExplicitMapping(
                    "copy-source",
                    1,
                    ExplicitMappingOperationKind.CopyRange,
                    "source-a",
                    new ByteRange(1, 3),
                    CompositionAddressSpaceIds.OutputImage,
                    new ByteRange(0, 3),
                    OverlapPolicy.Reject,
                    1,
                    "dynamic capability family drift test")]));
        CompiledComposition composition = Assert.IsType<CompiledComposition>(
            compile.CompiledComposition);
        CompiledComposition drifted = WithLogicalFamilyId(
            composition,
            "different-logical-family");

        Assert.True(reload.Succeeded);
        Assert.True(compile.IsCompiled);
        _ = route.BindCompilation(composition);
        _ = Assert.Throws<ArgumentException>(() =>
            route.BindCompilation(drifted));
    }

    private static ResolvedCapabilityRoute PublishWithContract(
        ResolvedCapabilityRoute source,
        CanonicalCapabilityCompilationContract contract)
    {
        var definition = new CanonicalDynamicCapabilityDefinition(
            source.Identity,
            source.CapabilityFingerprint,
            contract,
            source.Authoring,
            source.Publication,
            source.Evidence);
        var catalog = new CanonicalCapabilityCatalog(
            new SingleCandidateSource(new CanonicalCapabilityCatalogCandidate(
                "semantic-drift-test",
                "1.0.0",
                new string('a', 64),
                [],
                [definition])));
        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);

        Assert.True(reload.Succeeded);
        return Assert.Single(reload.Snapshot!.DynamicRoutes);
    }

    private static CompiledComposition WithoutSelectionGroups(
        CompiledComposition composition)
    {
        V2CompiledCompositionDetails source = composition.V2Details;
        var inputContract = new CompiledInputContract(
            source.InputContract.Slots,
            source.InputContract.SpaceBindings,
            selectionGroups: []);
        var details = new V2CompiledCompositionDetails(
            composition.V2Details.ProfileId,
            composition.V2Details.ProfileVersion,
            composition.V2Details.ExperienceId,
            composition.V2Details.CompositionKind,
            source.Provenance,
            inputContract,
            source.RegionAccessContract,
            source.OutputNamingRequirement,
            source.IcNumberInputMode);
        return CompiledComposition.CreateV2RuntimeExecutable(composition.Plan, details);
    }

    private static CompiledComposition WithLogicalFamilyId(
        CompiledComposition composition,
        string familyId)
    {
        V2CompiledCompositionDetails source = composition.V2Details;
        V2CompilationProvenance provenance = source.Provenance;
        LogicalOutputV2CompilationContext context =
            Assert.IsType<LogicalOutputV2CompilationContext>(provenance.Context);
        var changedProvenance = new V2CompilationProvenance(
            provenance.Bundle,
            provenance.ProfileEntry,
            new LogicalOutputV2CompilationContext(
                familyId,
                context.FamilyVersion,
                context.FamilyContentHash,
                context.MemberId),
            provenance.Promotion,
            provenance.ProfileEvidenceRefs,
            provenance.ValidationRequirements,
            provenance.RequiredCapabilities);
        var details = new V2CompiledCompositionDetails(
            composition.V2Details.ProfileId,
            composition.V2Details.ProfileVersion,
            composition.V2Details.ExperienceId,
            composition.V2Details.CompositionKind,
            changedProvenance,
            source.InputContract,
            source.RegionAccessContract,
            source.OutputNamingRequirement,
            source.IcNumberInputMode);
        return CompiledComposition.CreateV2(composition.Plan, details);
    }
}
