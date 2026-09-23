using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class CanonicalCapabilityCatalogMigrationTests
{
    /// <summary>A trusted source-envelope Standard route publishes as a definition without reading or compiling DP bytes.</summary>
    [Fact]
    public void SourceEnvelopeInventoryPublishesTrustedDeclarationWithoutBin()
    {
        using var workspace = TempWorkspace.Create("b1-source-envelope-inventory");
        string originalRoot = Path.Combine(AppContext.BaseDirectory,
            "profiles", "built-in", "nt51950-nt51951-standard-merge");
        JsonObject manifest = Assert.IsType<JsonObject>(JsonNode.Parse(
            File.ReadAllText(Path.Combine(originalRoot, "profile-bundle.json"))));
        JsonArray entries = Assert.IsType<JsonArray>(manifest["entries"]);
        var packageEntries = new List<ProfileBundleEntryDocument>();
        foreach (JsonNode? item in entries)
        {
            JsonObject entry = Assert.IsType<JsonObject>(item);
            string relativePath = entry["path"]!.GetValue<string>();
            byte[] content = relativePath == "schemas/composition-profile-v2.schema.json"
                ? File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
                    "docs", "contracts", "composition-profile-v2.16.schema.json"))
                : File.ReadAllBytes(Path.Combine(originalRoot, relativePath));
            if (entry["kind"]?.GetValue<string>() == "composition-profile")
            {
                JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(content));
                profile["schemaVersion"] = "2.16";
                if (relativePath == "profiles/nt51950-standard-merge.json")
                {
                    profile["sourceEnvelopeBinding"] = new JsonObject
                    {
                        ["sourceSlotId"] = "dp-input",
                        ["layoutTemplateMapId"] = "nt51950-standard-merge-256k",
                        ["rootRegionId"] = "dp-container",
                        ["whenSourceAbsent"] = "reject",
                        ["expectedOuterLengths"] = new JsonArray(0x40000, 0x80000, 0x100000),
                        ["unexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
                    };
                    JsonObject output = Assert.Single(
                        Assert.IsType<JsonArray>(profile["spaces"])
                            .Select(static space => Assert.IsType<JsonObject>(space)),
                        static space => space["kind"]?.GetValue<string>() == "output-image");
                    output["capacity"] = new JsonObject
                    {
                        ["kind"] = "source-slot",
                        ["sourceSlotId"] = "dp-input",
                    };
                }
                content = Encoding.UTF8.GetBytes(profile.ToJsonString());
            }
            string contentHash = Convert.ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            entry["contentHash"] = contentHash;
            packageEntries.Add(new ProfileBundleEntryDocument(
                entry["entryId"]!.GetValue<string>(),
                entry["kind"]!.GetValue<string>(),
                relativePath,
                entry["schemaId"]!.GetValue<string>(),
                contentHash));
            _ = workspace.Write(relativePath, content);
        }
        string bundleHash = ProfileBundleEntryArrayHasher.CalculateContentHash(packageEntries);
        manifest["contentHash"] = bundleHash;
        _ = workspace.Write("profile-bundle.json",
            Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        var bundle = new BuiltInV2Bundle(
            workspace.Root,
            manifest["bundleVersion"]!.GetValue<string>(),
            bundleHash,
            manifest["trustAnchorBindingId"]!.GetValue<string>());
        var registration = new BuiltInV2Registration(
            "NT51950", "nt51950-standard-merge-dp-perspective", "0.8.0", null,
            bundle, CompositionKind.Merge, ExperienceIds.StandardMerge);
        CapabilityRouteIdentity identity = new(
            "NT51950", ExperienceIds.StandardMerge, "selector-free",
            "nt51950-standard-merge-256k");
        BuiltInV2Registration? Find(CapabilityRouteIdentity candidate)
        {
            return candidate.RouteId == identity.RouteId ? registration : null;
        }

        Assert.True(CanonicalDynamicRouteInventory.IsDynamic(identity, Find));
        CanonicalDynamicRoute declared = CanonicalDynamicRouteInventory
            .CreateResolver(static () => [], Find)(identity);
        Assert.Equal([identity.MapVariant],
            declared.CompilationContract.AllowedMapVariantIds);
        CapabilityProfileSummary summary = registration.CreateProfileSummary();
        Assert.False(summary.CompileSucceeded);
        Assert.True(summary.DeclarationReady);
        Assert.NotNull(registration.CreateExactMapMetadataPlan(identity.MapVariant));
        Assert.True(registration.TryGetContainerPolicy(out _));

        var routePolicy = new CanonicalCapabilityPolicyRoute(
            identity, declared.CapabilityFingerprint,
            new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                "b1-synthetic-authoring", identity.RouteId, declared.CapabilityFingerprint,
                CapabilityAuthoringAvailability.Available, "b1-synthetic"),
            new PinnedCapabilityDecision<CapabilityPublicationStatus>(
                "b1-synthetic-publication", identity.RouteId, declared.CapabilityFingerprint,
                CapabilityPublicationStatus.TestOnly, "b1-synthetic"),
            new PinnedCapabilityDecision<CapabilityEvidenceStatus>(
                "b1-synthetic-evidence", identity.RouteId, declared.CapabilityFingerprint,
                CapabilityEvidenceStatus.SyntheticOracle, "b1-synthetic"));
        var policy = new CanonicalCapabilityPolicySnapshot(
            "b1-synthetic", "1.0.0", new string('a', 64), [routePolicy]);
        var source = new CanonicalCapabilityCatalogSource(
            () => policy,
            candidate => CanonicalDynamicRouteInventory.IsDynamic(candidate, Find),
            static _ => throw new InvalidOperationException(
                "Source-envelope declaration must not enter the static compiler."),
            () => CanonicalDynamicRouteInventory.CreateResolver(static () => [], Find),
            static (_, _) => CanonicalCapabilityDisclosure.Empty,
            static () => []);
        var catalog = new CanonicalCapabilityCatalog(source);

        CapabilityCatalogReloadResult first = catalog.Reload(
            TestContext.Current.CancellationToken);
        CapabilityCatalogReloadResult second = catalog.Reload(
            TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded, string.Join(" | ", first.Issues.Select(static issue => issue.Message)));
        Assert.True(second.Succeeded);
        Assert.Empty(first.Snapshot!.Capabilities);
        Assert.Equal(identity.RouteId, Assert.Single(first.Snapshot.DynamicRoutes).Identity.RouteId);
        Assert.Equal([identity.MapVariant],
            Assert.Single(first.Snapshot.DynamicRoutes).CompilationContract.AllowedMapVariantIds);
        Assert.Equal(declared.CapabilityFingerprint,
            Assert.Single(second.Snapshot!.DynamicRoutes).CapabilityFingerprint);
        Assert.NotEqual(first.Snapshot.ResolutionToken, second.Snapshot.ResolutionToken);

        byte[] dp = [.. Enumerable.Range(0, 0x40001).Select(index => (byte)(index % 251))];
        byte[] tp = [.. Enumerable.Repeat((byte)0xA5, 0x37000)];
        FirmwareArtifactPayload[] captured =
        [
            new FirmwareArtifactPayload("dp-input", dp),
            new FirmwareArtifactPayload("tp-input", tp),
        ];
        string[] selected = ["dp-input", "tp-input"];
        registration.TryCompile(dp.Length, requestedTopology: null, selected, captured,
            out CompiledComposition? direct, out IReadOnlyList<CompositionIssue> directIssues);
        Assert.True(directIssues.Count == 0,
            string.Join(" | ", directIssues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.NotNull(direct);
        Assert.NotNull(registration.CreateMetadataPlan(direct));

        var adapter = new CapturedBundleRegistrationAdapter(registration, identity);
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);
        bool routed = compiler.TryCompilePublishedDynamicCapability(
            identity, dp.Length, captured, selected,
            out CompiledComposition? composition, out ResolvedCapability? capability,
            out IReadOnlyList<CompositionIssue> compileIssues);

        Assert.True(routed);
        Assert.Empty(compileIssues);
        Assert.Equal(1, adapter.CapturedCalls);
        Assert.NotNull(composition);
        ResolvedCapability bound = Assert.IsType<ResolvedCapability>(capability);
        Assert.Equal(identity.RouteId, bound.Identity.RouteId);
        Assert.Equal(second.Snapshot.ResolutionToken, bound.ResolutionToken);
        Assert.Same(composition, bound.CompiledComposition);
        SourceEnvelopeExtent extent = Assert.IsType<SourceEnvelopeExtent>(
            Assert.IsType<ResolvedMapV2CompilationContext>(
                composition.V2Details.Provenance.Context).SourceEnvelope);
        Assert.Equal(identity.MapVariant, extent.LayoutTemplateMapId);
        Assert.Equal(0x40000, extent.LayoutTemplateCapacity);
        Assert.Equal(dp.Length, extent.ActualOutputLength);
        Assert.Equal(dp.Length, composition.Plan.OutputInitialization.Capacity);
        CompiledInputSlotRequirement dpSlot = Assert.Single(
            composition.V2Details.InputContract.Slots,
            static slot => slot.SlotId == "dp-input");
        Assert.Equal(dp.Length,
            Assert.IsType<CompiledExactBytesInputLengthRequirement>(dpSlot.LengthRequirement).Bytes);
        CompositionExecutionResult execution = CompositionEngine.Execute(
            composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["dp-input"] = dp,
                ["tp-input"] = tp,
            }));
        Assert.Equal(CompositionExecutionStatus.Succeeded, execution.Status);
        byte[] expected = [.. dp];
        Array.Copy(tp, 0xA000, expected, 0xA000, 0x2D000);
        Assert.Equal(expected, execution.OutputBytes.ToArray());
    }

    private sealed class CapturedBundleRegistrationAdapter(
        BuiltInV2Registration registration,
        CapabilityRouteIdentity expectedIdentity) : ICanonicalDynamicCompilationAdapter
    {
        internal int CapturedCalls { get; private set; }

        public bool TryGetAbAuthoringDefinition(
            CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition,
            out IReadOnlyList<CompositionIssue> issues)
        {
            definition = null;
            issues = [];
            return false;
        }

        public IReadOnlyList<long> GetMapCapacities(
            string icId, string workflowId, out IReadOnlyList<CompositionIssue> issues)
        {
            return registration.GetMapCapacities(out issues);
        }

        public void Compile(
            CapabilityRouteIdentity identity,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            throw new InvalidOperationException("Captured compilation must not use length-only dispatch.");
        }

        public void Compile(
            CapabilityRouteIdentity identity,
            long? requestedMapCapacity,
            IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            Assert.Equal(expectedIdentity.RouteId, identity.RouteId);
            CapturedCalls++;
            registration.TryCompile(requestedMapCapacity, requestedTopology,
                selectedInputSlotIds, capturedArtifacts, out composition, out issues);
            metadataPlan = composition is not null && issues.Count == 0
                ? registration.CreateMetadataPlan(composition)
                : null;
        }
    }

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
        const string bankRouteId =
            "route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k";
        ResolvedCapabilityRoute bankRoute = Assert.Single(ctrlRamRoutes,
            route => route.Identity.RouteId == bankRouteId);
        ResolvedCapabilityRoute[] legacyRoutes =
        [
            .. ctrlRamRoutes.Where(route => route.Identity.RouteId != bankRouteId),
        ];
        ResolvedCapabilityRoute[] reportless =
        [
            .. legacyRoutes.Where(route =>
                route.Identity.IcId is "NT51919" or "NT51950" or "NT51951"),
        ];
        ResolvedCapabilityRoute[] reportful =
        [
            .. legacyRoutes.Except(reportless),
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
        Assert.Equal(45, ctrlRamRoutes.Length);
        Assert.Equal(44, legacyRoutes.Length);
        Assert.Equal(10, reportless.Length);
        Assert.Equal(34, reportful.Length);
        Assert.Equal(
            ctrlRamRoutes.Select(static route => route.Identity.RouteId)
                .Order(StringComparer.Ordinal),
            policyRoutes.Select(static route => route.Identity.RouteId)
                .Order(StringComparer.Ordinal));
        CanonicalCapabilityPolicyRoute bankPolicy = Assert.Single(policyRoutes,
            route => route.Identity.RouteId == bankRouteId);
        Assert.Equal(CapabilityPublicationStatus.Candidate, bankRoute.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, bankRoute.Evidence.Value);
        Assert.EndsWith("-authoring-v1", bankPolicy.Authoring.DecisionId, StringComparison.Ordinal);
        Assert.EndsWith("-publication-v1", bankPolicy.Publication.DecisionId, StringComparison.Ordinal);
        Assert.EndsWith("-evidence-v1", bankPolicy.Evidence.DecisionId, StringComparison.Ordinal);
        Assert.Equal(3, bankRoute.CompilationContract.SemanticBindingIds.Count);
        Assert.Contains(bankRoute.CompilationContract.SemanticBindingIds,
            static binding => binding.StartsWith("bank-definition:", StringComparison.Ordinal));
        Assert.Contains(bankRoute.CompilationContract.SemanticBindingIds,
            static binding => binding.StartsWith("postbuild-selector:", StringComparison.Ordinal));
        Assert.Contains(bankRoute.CompilationContract.SemanticBindingIds,
            static binding => binding.StartsWith("postbuild-plan:", StringComparison.Ordinal));
        Assert.DoesNotContain(bankRoute.CompilationContract.SemanticBindingIds,
            static binding => binding.StartsWith("report-metadata-", StringComparison.Ordinal));
        Assert.All(policyRoutes.Where(route => route.Identity.RouteId != bankRouteId), route =>
        {
            // Catalog 1.12.0 adds display-only context to these ten routes.
            bool hasAddedContext = route.Identity.IcId is "NT51919" or "NT51950" or "NT51951";
            Assert.EndsWith(hasAddedContext ? "-authoring-v4" : "-authoring-v3", route.Authoring.DecisionId, StringComparison.Ordinal);
            Assert.EndsWith(hasAddedContext ? "-publication-v6" : "-publication-v5", route.Publication.DecisionId, StringComparison.Ordinal);
            string expectedEvidenceRevision = tpRoutesAwaitingIndependentExpectedOutput.Contains(
                route.Identity.RouteId,
                StringComparer.Ordinal)
                ? "-evidence-v5"
                : hasAddedContext ? "-evidence-v4" : "-evidence-v3";
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
                candidate.Identity.WorkflowId == ExperienceIds.StandardMerge);
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51928"];
        registration.TryCompile(
            0x40000,
            requestedTopology: null,
            [],
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
        MetadataPlanDefinition metadataPlan = registration.CreateMetadataPlan(compiled!);
        _ = route.BindCompilation(compiled!, metadataPlan);
        ResolvedCapabilityRoute mapRoute = PublishWithContract(route, wrongMap);
        ResolvedCapabilityRoute compilerRoute = PublishWithContract(route, wrongCompiler);
        ResolvedCapabilityRoute selectionRoute = PublishWithContract(route, wrongSelectionGroup);
        ArgumentException mapFailure = Assert.Throws<ArgumentException>(() =>
            mapRoute.BindCompilation(compiled!, metadataPlan));
        ArgumentException compilerFailure = Assert.Throws<ArgumentException>(() =>
            compilerRoute.BindCompilation(compiled!, metadataPlan));
        ArgumentException selectionFailure = Assert.Throws<ArgumentException>(() =>
            selectionRoute.BindCompilation(compiled!, metadataPlan));
        Assert.Equal("composition", mapFailure.ParamName);
        Assert.Contains("selected a map outside", mapFailure.Message, StringComparison.Ordinal);
        Assert.Equal("composition", compilerFailure.ParamName);
        Assert.Contains("reviewed compiler semantics", compilerFailure.Message, StringComparison.Ordinal);
        Assert.Equal("composition", selectionFailure.ParamName);
        Assert.Contains("Compiled semantic bindings do not match", selectionFailure.Message, StringComparison.Ordinal);
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
                candidate.Identity.WorkflowId == ExperienceIds.StandardMerge);
        BuiltInV2Registration registration =
            BuiltInV2RegistrationRegistry.StandardMergeByIc["NT51928"];
        registration.TryCompile(
            0x40000,
            requestedTopology: null,
            [],
            out CompiledComposition? compiled,
            out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition withoutSelectionGroup = WithoutSelectionGroups(
            compiled!);

        Assert.True(reload.Succeeded);
        Assert.Empty(issues);
        MetadataPlanDefinition metadataPlan = registration.CreateMetadataPlan(compiled!);
        _ = route.BindCompilation(compiled!, metadataPlan);
        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            route.BindCompilation(withoutSelectionGroup, metadataPlan));
        Assert.Equal("composition", failure.ParamName);
        Assert.Contains("Compiled semantic bindings do not match", failure.Message, StringComparison.Ordinal);
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
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            source.Identity,
            contract.ProfileId,
            contract.ProfileVersion,
            contract.TrustedDefinitionSha256,
            contract.AllowedMapVariantIds,
            contract.CompilerSemanticId,
            contract.SemanticBindingIds);
        var definition = new CanonicalDynamicCapabilityDefinition(
            source.Identity,
            fingerprint,
            contract,
            Repin(source.Authoring, fingerprint),
            Repin(source.Publication, fingerprint),
            Repin(source.Evidence, fingerprint));
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

    private static PinnedCapabilityDecision<TValue> Repin<TValue>(
        PinnedCapabilityDecision<TValue> decision,
        string fingerprint)
        where TValue : struct, Enum
    {
        return new(
            decision.DecisionId,
            decision.RouteId,
            fingerprint,
            decision.Value,
            decision.SourceReference);
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
