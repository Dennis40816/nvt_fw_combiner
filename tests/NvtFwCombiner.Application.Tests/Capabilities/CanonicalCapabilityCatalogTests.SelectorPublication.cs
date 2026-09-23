using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.Capabilities;

public sealed partial class CanonicalCapabilityCatalogTests
{
    /// <summary>The selector child is eagerly bound to the exact catalog publication.</summary>
    [Fact]
    public void SelectorPublicationSharesSnapshotTokenAndBacksLegacyGetters()
    {
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate().WithDisclosure(
            CreateDisclosure(
                new Dictionary<string, IReadOnlyList<CapabilityNumberChoice>>(
                    StringComparer.Ordinal)
                {
                    ["NT51929"] =
                    [
                        new CapabilityNumberChoice("single", "1 IC"),
                    ],
                }));
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        var experience = new CanonicalCapabilityExperience(catalog, catalog);

        CapabilitySelectorPublication selector = experience.GetSelectorPublication();

        Assert.Same(reload.Snapshot!.SelectorPublication, selector);
        Assert.Equal(reload.Snapshot.ResolutionToken, selector.ResolutionToken);
        Assert.Equal("NT51929", selector.DefaultIcId);
        Assert.Equal(["NT51929"], selector.IcIds);
        Assert.Equal(selector.DefaultIcId, experience.DefaultIcId);
        Assert.Equal(selector.IcIds, experience.GetIcIds());
        Assert.Equal(
            selector.GetNumberSelectionChoices("NT51929"),
            experience.GetNumberSelectionChoices("NT51929"));
    }

    /// <summary>A later reload cannot mutate selector facts retained from an older publication.</summary>
    [Fact]
    public void ReloadPublishesFreshSelectorWithoutMutatingRetainedPublication()
    {
        CapabilityRouteIdentity replacementRoute = new(
            "NT51950",
            ExperienceIds.StandardMerge,
            "selector-free",
            "nt51950-standard-merge");
        CanonicalCapabilityCatalogCandidate firstCandidate = CreateCandidate();
        CanonicalCapabilityCatalogCandidate secondCandidate = CreateCandidate(
            CreateDefinition(
                CreateCompiledComposition(route: replacementRoute),
                replacementRoute));
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(firstCandidate),
                CapabilityCatalogLoadResult.Success(secondCandidate)));

        CapabilitySelectorPublication first = catalog
            .Reload(TestContext.Current.CancellationToken)
            .Snapshot!
            .SelectorPublication;
        CapabilitySelectorPublication second = catalog
            .Reload(TestContext.Current.CancellationToken)
            .Snapshot!
            .SelectorPublication;

        Assert.NotEqual(first.ResolutionToken, second.ResolutionToken);
        Assert.Equal(["NT51929"], first.IcIds);
        Assert.Equal("NT51929", first.DefaultIcId);
        Assert.Equal(["NT51950"], second.IcIds);
        Assert.Equal("NT51950", second.DefaultIcId);
    }

    /// <summary>Every nested selector collection is copied, read-only, and retained across reload.</summary>
    [Fact]
    public void SelectorPublicationDeeplyFreezesEveryNestedSelectorFact()
    {
        CapabilityRouteIdentity standardRoute = new(
            "NT51950",
            ExperienceIds.StandardMerge,
            "selector-free",
            "nt51950-standard-selector-test");
        CapabilityRouteIdentity singleRoute = CreateAbRoute(
            "NT51950",
            "single",
            "nt51950-ab-single-freeze-test");
        CapabilityRouteIdentity cascadeRoute = CreateAbRoute(
            "NT51950",
            "cascade",
            "nt51950-ab-cascade-freeze-test");
        CanonicalCapabilityCatalogCandidate firstCandidate = CreateCandidate(
                CreateDefinition(
                    CreateCompiledComposition(route: standardRoute),
                    standardRoute),
                CreateDefinition(
                    CreateAbCompiledComposition(singleRoute, chipCount: 1),
                    singleRoute),
                CreateDefinition(
                    CreateAbCompiledComposition(cascadeRoute, chipCount: 2),
                    cascadeRoute))
            .WithDisclosure(CreateDisclosure(
                new Dictionary<string, IReadOnlyList<CapabilityNumberChoice>>(
                    StringComparer.Ordinal)
                {
                    ["NT51950"] =
                    [
                        new CapabilityNumberChoice("single", "1 IC"),
                        new CapabilityNumberChoice("cascade", "2 IC"),
                    ],
                }));
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(firstCandidate),
                CapabilityCatalogLoadResult.Success(CreateCandidate())));

        CapabilitySelectorPublication first = catalog
            .Reload(TestContext.Current.CancellationToken)
            .Snapshot!
            .SelectorPublication;
        ResolutionToken firstToken = first.ResolutionToken;
        IReadOnlyList<string> workflows = first.GetAuthorableWorkflowIds("NT51950");
        IReadOnlyList<CapabilityNumberChoice> numbers =
            first.GetNumberSelectionChoices("NT51950");
        IReadOnlyList<CapabilityTopologyChoice> topologies =
            first.GetAbMergeTopologyChoices("NT51950");

        AssertRejectsMutation(first.IcIds);
        AssertRejectsMutation(first.AbMergeIcIds);
        AssertRejectsMutation(workflows);
        AssertRejectsMutation(numbers);
        AssertRejectsMutation(topologies);
        Assert.Equal([ExperienceIds.AbMerge, ExperienceIds.StandardMerge], workflows);
        Assert.Equal(["single", "cascade"], numbers.Select(static choice => choice.Token));
        Assert.Equal([1, 2], topologies.Select(static choice => choice.Selection.ChipCount));
        Assert.Equal(["1 IC", "2 IC"], topologies.Select(static choice => choice.DisplayLabel));

        CapabilitySelectorPublication second = catalog
            .Reload(TestContext.Current.CancellationToken)
            .Snapshot!
            .SelectorPublication;

        Assert.NotEqual(firstToken, second.ResolutionToken);
        Assert.Equal(firstToken, first.ResolutionToken);
        Assert.Equal("NT51950", first.DefaultIcId);
        Assert.Equal(["NT51950"], first.IcIds);
        Assert.Equal([ExperienceIds.AbMerge, ExperienceIds.StandardMerge],
            first.GetAuthorableWorkflowIds("NT51950"));
        Assert.Equal(["single", "cascade"], first.GetNumberSelectionChoices("NT51950")
            .Select(static choice => choice.Token));
        Assert.Equal([1, 2], first.GetAbMergeTopologyChoices("NT51950")
            .Select(static choice => choice.Selection.ChipCount));
        Assert.Equal(["NT51929"], second.IcIds);
    }

    /// <summary>A valid publication with no authorable route has no invented selector default.</summary>
    [Fact]
    public void ZeroAuthorablePublicationUsesNullDefaultAndFailsClosed()
    {
        CanonicalCapabilityDefinition unavailable = CreateDefinition(
            CreateCompiledComposition(),
            authoringAvailability: CapabilityAuthoringAvailability.Unavailable);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
                CreateCandidate(unavailable))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        var experience = new CanonicalCapabilityExperience(catalog, catalog);

        CapabilitySelectorPublication selector = experience.GetSelectorPublication();

        Assert.True(reload.Succeeded);
        Assert.Null(selector.DefaultIcId);
        Assert.Empty(selector.IcIds);
        Assert.Empty(selector.AbMergeIcIds);
        Assert.False(selector.IsWorkflowAuthorable(
            "NT51929",
            ExperienceIds.StandardMerge));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => experience.DefaultIcId);
        Assert.Contains("no authorable IC route", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The compiler and selector read the same topology projection.</summary>
    [Fact]
    public void SelectorAndCompilerShareAbMergeTopologyProjection()
    {
        CapabilityRouteIdentity singleRoute = CreateAbRoute(
            "NT51950",
            "single",
            "nt51950-ab-single");
        CapabilityRouteIdentity cascadeRoute = CreateAbRoute(
            "NT51950",
            "cascade",
            "nt51950-ab-cascade");
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate(
            CreateDefinition(
                CreateAbCompiledComposition(singleRoute, chipCount: 1),
                singleRoute),
            CreateDefinition(
                CreateAbCompiledComposition(cascadeRoute, chipCount: 2),
                cascadeRoute));
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));
        _ = catalog.Reload(TestContext.Current.CancellationToken);
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new UnusedDynamicCompiler());

        IReadOnlyList<CapabilityTopologyChoice> selector = catalog
            .GetCurrentSnapshot()
            .SelectorPublication
            .GetAbMergeTopologyChoices("NT51950");
        IReadOnlyList<CapabilityTopologyChoice> compiled =
            compiler.GetAbMergeTopologyChoices("NT51950");

        Assert.Equal(selector, compiled);
        Assert.Equal([1, 2], selector.Select(static choice => choice.Selection.ChipCount));
    }

    /// <summary>Selector-free AB remains authorable without inventing a topology choice.</summary>
    [Fact]
    public void SelectorFreeAbRouteIsDistinctFromMissingAbAuthoring()
    {
        CapabilityRouteIdentity selectorFreeRoute = CreateAbRoute(
            "NT51951",
            "selector-free",
            "nt51951-ab-selector-free");
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate(
            CreateDefinition(
                CreateCompiledComposition(route: selectorFreeRoute),
                selectorFreeRoute));
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));
        _ = catalog.Reload(TestContext.Current.CancellationToken);

        CapabilitySelectorPublication selector = catalog
            .GetCurrentSnapshot()
            .SelectorPublication;

        Assert.Equal(["NT51951"], selector.AbMergeIcIds);
        Assert.True(selector.IsWorkflowAuthorable(
            "NT51951",
            ExperienceIds.AbMerge));
        Assert.Empty(selector.GetAbMergeTopologyChoices("NT51951"));
        Assert.False(selector.IsWorkflowAuthorable(
            "NT51929",
            ExperienceIds.AbMerge));
    }

    /// <summary>Two formats sharing an IC/count require explicit identity, never array-order selection.</summary>
    [Fact]
    public void AbFormatAmbiguityRequiresExactPublishedIdentity()
    {
        var first = new CapabilityRouteIdentity("NT51951", ExperienceIds.AbMerge, "selector-free", "common-maps");
        var second = new CapabilityRouteIdentity("NT51951", ExperienceIds.AbMerge, "selector-free", "desay-maps");
        var candidate = new CanonicalCapabilityCatalogCandidate("exact-ab-test", "1.0.0", new string('a', 64), [],
            [CreateDynamicAbDefinition(first), CreateDynamicAbDefinition(second)]);
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var adapter = new UnusedDynamicCompiler();
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);

        Assert.False(compiler.TryCompileAbMergeCapability("NT51951", null, [], out _, out _, out IReadOnlyList<CompositionIssue> issues));
        Assert.Equal(CapabilityCatalogIssueCodes.RouteAmbiguous, Assert.Single(issues).Code);
        Assert.Null(adapter.CapturedIdentity);
        Assert.False(compiler.TryCompilePublishedDynamicCapability("NT51951", ExperienceIds.AbMerge,
            "selector-free", null, [], out _, out _, out issues));
        Assert.Equal(CapabilityCatalogIssueCodes.RouteAmbiguous, Assert.Single(issues).Code);
        Assert.Null(adapter.CapturedIdentity);

        Assert.True(compiler.TryCompilePublishedDynamicCapability(second, null, [], out CompiledComposition? composition,
            out ResolvedCapability? capability, out issues));
        Assert.Same(second, adapter.CapturedIdentity);
        Assert.Empty(issues);
        Assert.Null(composition); // This fake only observes dispatch; actual binding is covered by Bootstrap tests.
        Assert.Null(capability);
    }

    /// <summary>A legacy adapter cannot silently compile a captured source through its length-only method.</summary>
    [Fact]
    public void CapturedCompilationDefaultsToTypedUnsupportedWithoutLengthOnlyDispatch()
    {
        var legacy = new UnusedDynamicCompiler();
        ICanonicalDynamicCompilationAdapter adapter = legacy;
        CapabilityRouteIdentity identity = CreateAbRoute(
            "NT51951", "selector-free", "desay-maps");

        adapter.Compile(
            identity,
            0x40001,
            [new FirmwareArtifactPayload("dp-input", new byte[0x40001])],
            [],
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Null(composition);
        Assert.Null(metadataPlan);
        Assert.Null(legacy.CapturedIdentity);
        Assert.Equal("capability.dynamic.captured-compilation-unsupported", Assert.Single(issues).Code);
    }

    /// <summary>A publication replaced inside captured compilation discards the adapter result.</summary>
    [Fact]
    public void CapturedCompilationRejectsCatalogReloadDuringAdapterCall()
    {
        CapabilityRouteIdentity identity = CreateAbRoute(
            "NT51951", "selector-free", "desay-maps");
        CanonicalCapabilityCatalogCandidate candidate = new(
            "captured-reload-test", "1.0.0", new string('a', 64), [],
            [CreateDynamicAbDefinition(identity)]);
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(
            CapabilityCatalogLoadResult.Success(candidate),
            CapabilityCatalogLoadResult.Success(candidate)));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolutionToken before = catalog.GetCurrentSnapshot().ResolutionToken;
        var adapter = new ReloadingCapturedCompiler
        {
            BeforeCapturedResult = () =>
                Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded),
        };
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);

        bool routed = compiler.TryCompilePublishedDynamicCapability(
            identity, 4,
            [new FirmwareArtifactPayload("dp-input", new byte[4])],
            [], out CompiledComposition? composition,
            out ResolvedCapability? capability,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(routed);
        Assert.Equal(1, adapter.CapturedCalls);
        Assert.NotEqual(before, catalog.GetCurrentSnapshot().ResolutionToken);
        Assert.Null(composition);
        Assert.Null(capability);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication,
            Assert.Single(issues).Code);
    }

    /// <summary>Every identity axis must resolve in the current publication before the adapter is called.</summary>
    [Theory]
    [InlineData("NT51950", "ab-merge", "selector-free", "desay-maps")]
    [InlineData("NT51951", "standard-merge", "selector-free", "desay-maps")]
    [InlineData("NT51951", "ab-merge", "1-ic", "desay-maps")]
    [InlineData("NT51951", "ab-merge", "selector-free", "missing-maps")]
    public void ExactDynamicDispatchRejectsUnpublishedIdentityAxes(string icId, string workflow, string count, string mapSet)
    {
        var identity = new CapabilityRouteIdentity("NT51951", ExperienceIds.AbMerge, "selector-free", "desay-maps");
        var candidate = new CanonicalCapabilityCatalogCandidate("exact-ab-test", "1.0.0", new string('a', 64), [],
            [CreateDynamicAbDefinition(identity)]);
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var adapter = new UnusedDynamicCompiler();
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);

        _ = compiler.TryCompilePublishedDynamicCapability(new CapabilityRouteIdentity(icId, workflow, count, mapSet),
            null, [], out CompiledComposition? composition, out ResolvedCapability? capability, out IReadOnlyList<CompositionIssue> issues);

        Assert.NotEmpty(issues);
        Assert.Null(composition);
        Assert.Null(capability);
        Assert.Null(adapter.CapturedIdentity);
    }

    private static CapabilityRouteIdentity CreateAbRoute(
        string icId,
        string countVariant,
        string mapVariant)
    {
        return new CapabilityRouteIdentity(
            icId,
            ExperienceIds.AbMerge,
            countVariant,
            mapVariant);
    }

    private static void AssertRejectsMutation<T>(IReadOnlyList<T> values)
    {
        IList<T> mutableView = Assert.IsType<IList<T>>(values, exactMatch: false);
        T retained = Assert.Single(values.Take(1));
        _ = Assert.Throws<NotSupportedException>(() => mutableView.Add(retained));
        _ = Assert.Throws<NotSupportedException>(() => mutableView[0] = retained);
        _ = Assert.Throws<NotSupportedException>(() => mutableView.RemoveAt(0));
    }

    private static CompiledComposition CreateAbCompiledComposition(
        CapabilityRouteIdentity route,
        int chipCount)
    {
        var topology = new TopologySelection(
            chipCount,
            $"{chipCount} IC",
            TopologySelectionSource.Requested,
            "application-selector-test");
        TopologyRequirement requirement = chipCount == 1
            ? TopologyRequirement.RequireSingleChip()
            : TopologyRequirement.RequireCascade();
        const long capacity = 8;
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            route.MapVariant,
            "flash",
            new FirmwareMapApplicability(
                [route.IcId],
                [route.WorkflowId],
                requirement,
                capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet(
                "physical",
                "flash",
                [new FirmwareRegion(
                    "root",
                    parentRegionId: null,
                    FirmwareRegionOwner.System,
                    FirmwareRegionKind.Image,
                    new ByteRange(0, capacity),
                    FirmwareWriteConstraint.Forbidden)],
                ["application-selector-test"])],
            [],
            ["application-selector-test"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "application-selector-family",
            "1.0.0",
            new string('c', 64),
            [map],
            []);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            definition.ResolveMap(new FirmwareMapResolutionInputs(
                route.IcId,
                route.WorkflowId,
                capacity,
                topology,
                [])).ResolvedMap ?? throw new InvalidOperationException(
                    "Synthetic AB map did not resolve.");
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", capacity, 0),
            [
                new AddressSpace(
                    "ab-input",
                    capacity,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "output-image",
                    capacity,
                    AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange(
                "copy-ab",
                100,
                "ab-input",
                new ByteRange(0, capacity),
                "output-image",
                new ByteRange(0, capacity),
                OverlapPolicy.Reject,
                "Copy synthetic AB input.")]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "application-selector-bundle",
                "1.0.0",
                new string('c', 64),
                "application-selector-trust"),
            new ProfileBundleEntryIdentity(
                "application-selector-profile",
                new string('c', 64)),
            new ResolvedMapV2CompilationContext(resolvedMap),
            new CompiledProfilePromotion(
                CompiledProfilePromotionStage.Supported,
                []),
            ["application-selector-test"],
            [],
            []);
        var details = new V2CompiledCompositionDetails(
            $"synthetic-{route.IcId.ToLowerInvariant()}-{route.IcCountVariant}",
            "1.0.0",
            ExperienceIds.AbMerge,
            CompositionKind.Merge,
            provenance,
            new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                    "ab-input-slot",
                    "ab-input",
                    CompiledInputArtifactClass.Auxiliary,
                    required: true,
                    CompiledInputSlotCardinality.ExactlyOne,
                    [".bin"],
                    new CompiledExactBytesInputLengthRequirement(capacity),
                    new CompiledNoInputNormalization())],
                [new CompiledInputSpaceBinding(
                    "ab-input",
                    "ab-input-slot",
                    CompiledInputInstancePolicy.Singleton)]),
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                $"synthetic-{route.IcId.ToLowerInvariant()}-ab.bin",
                allowOverride: false,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []),
            null);
        return CompiledComposition.CreateV2RuntimeExecutable(plan, details);
    }

    private class UnusedDynamicCompiler : ICanonicalDynamicCompilationAdapter
    {
        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
        {
            definition = null;
            issues = [];
            return false;
        }

        internal CapabilityRouteIdentity? CapturedIdentity { get; private set; }

        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            issues = [];
            return [];
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
            CapturedIdentity = identity;
            composition = null;
            metadataPlan = null;
            issues = [];
        }
    }

    private sealed class ReloadingCapturedCompiler : UnusedDynamicCompiler,
        ICanonicalDynamicCompilationAdapter
    {
        internal Action? BeforeCapturedResult { get; init; }

        internal int CapturedCalls { get; private set; }

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
            _ = identity;
            _ = requestedMapCapacity;
            _ = capturedArtifacts;
            _ = selectedInputSlotIds;
            _ = requestedTopology;
            CapturedCalls++;
            BeforeCapturedResult?.Invoke();
            composition = null;
            metadataPlan = null;
            issues = [];
        }

    }
}
