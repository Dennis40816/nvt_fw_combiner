using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Synthetic layout classification never selects a version map or opens authoring.</summary>
public sealed class Nt51925MapClassificationTests
{
    /// <summary>Both version lines and unreadable Common FW retain layout classification and consensus TP SVN.</summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public void TpPrefixesClassifyAndReadConsensusMetadataWithoutVersionSelection(byte major, bool readable)
    {
        byte[] tp = CreateReference(major, 0x30000);
        if (!readable) { tp.AsSpan(0x2FFFC, 4).Clear(); }
        CanonicalTestContext canonical = BootstrapTestHost.Canonical;
        var resolver = new FirmwareArtifactClassificationResolver(canonical.Catalog, canonical.Compiler);
        (CompiledFirmwareArtifactClassification? classification, ResolvedCapability? exactCapability,
            IReadOnlyList<ResolvedCapability>? consensusCapabilities) = resolver.ResolveWithExactStandardCapability("NT51925", null, tp);

        Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, classification?.Kind);
        Assert.Null(exactCapability);
        Assert.Equal(2, Assert.IsType<IReadOnlyList<ResolvedCapability>>(consensusCapabilities, exactMatch: false).Count);
        var adapter = new BuiltInCtrlRamAuthoringAdapter(canonical.Catalog, canonical.Projection);
        CtrlRamBaseInspection inspection = resolver.ResolveCtrlRamBase("NT51925", null, tp, null, adapter);
        Assert.Equal(CtrlRamBaseKind.StandardTp, inspection.Kind);
        Assert.Empty(inspection.Issues);
        TpSvnObservation stamp = Assert.IsType<TpSvnObservation>(inspection.StandardTpSvn);
        Assert.Equal(new ByteRange(0x24, 4), stamp.Range.Range);
        Assert.Equal(new byte[] { 0xC0, 0x12, 0x34, 0x56 }, stamp.RawBytes.ToArray());
        Assert.Equal("123456", stamp.RevisionDigits);
        if (readable)
        {
            Assert.Equal((byte)0x5A, inspection.StandardEventBufferFormatVersion);
            Assert.Equal((byte)0x5A, resolver.ReadCommonEventBufferFormatForTp("NT51925",
                canonical.Catalog.GetCurrentSnapshot().ResolutionToken, tp, 0x2F000));
        }
    }

    /// <summary>Explicit maps require both the published allowlist and a map-selection prerequisite.</summary>
    [Theory]
    [InlineData("NT51925", "nt51925-standard-merge-256k", true)]
    [InlineData("NT51925", "nt51925-standard-merge-fw2-256k", true)]
    [InlineData("NT51925", "undeclared-map", false)]
    [InlineData("NT51928", "nt51928-standard-merge-256k", false)]
    public void ExplicitClassificationMapRequestsStayWithinDeclaredAuthority(string icId, string mapId, bool allowed)
    {
        CanonicalTestContext canonical = BootstrapTestHost.Canonical;
        ResolvedCapabilityRoute route = Assert.Single(canonical.Catalog.GetCurrentSnapshot().DynamicRoutes,
            candidate => candidate.Identity.IcId == icId && candidate.Identity.WorkflowId == ExperienceIds.StandardMerge);
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        adapter.Compile(route.Identity, null, [], [], mapId, out CompiledComposition? composition,
            out MetadataPlanDefinition? metadata, out IReadOnlyList<CompositionIssue> issues);
        Assert.Equal(allowed, canonical.Compiler.TryCompilePublishedClassificationCandidate(route, [],
            out ResolvedCapability? capability, mapId));
        if (allowed)
        {
            Assert.Empty(issues);
            Assert.NotNull(metadata);
            Assert.Equal(mapId, Assert.IsType<CompiledComposition>(composition).V2Details.Provenance.ResolvedMap.ImageMap.MapId);
            Assert.Equal(mapId, Assert.IsType<ResolvedCapability>(capability).CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
            adapter.Compile(route.Identity, null, [], out composition, out metadata, out issues);
            Assert.Null(composition);
            Assert.Null(metadata);
            Assert.Equal("profile.v2.compile.common-fw-version-required", Assert.Single(issues).Code);
        }
        else
        {
            Assert.Null(composition);
            Assert.Null(metadata);
            Assert.NotEmpty(issues);
            Assert.Null(capability);
        }
    }

    /// <summary>Equal-capacity maps preserve agreeing kinds, erase differing signals, and reject differing kinds.</summary>
    [Fact]
    public void EqualCapacityClassificationRequiresConsensusWithoutChoosingAMap()
    {
        CanonicalTestContext canonical = BootstrapTestHost.Canonical;
        var resolver = new FirmwareArtifactClassificationResolver(canonical.Catalog, canonical.Compiler);
        byte[] flash = CreateReference(2, 0x40000, fullFlash: true);
        (CompiledFirmwareArtifactClassification? completeClassification, ResolvedCapability? completeExact,
            IReadOnlyList<ResolvedCapability>? completeConsensus) = resolver.ResolveWithExactStandardCapability("NT51925", null, flash);
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, completeClassification?.Kind);
        Assert.Null(completeExact);
        Assert.Equal(2, Assert.IsType<IReadOnlyList<ResolvedCapability>>(completeConsensus, exactMatch: false).Count);

        var agreeing = new FirmwareArtifactClassificationResolver(canonical.Catalog,
            new CanonicalCapabilityCompilerAdapter(canonical.Catalog, new ChangedPlausibilityAdapter(disagree: false)));
        CompiledFirmwareArtifactClassification consensus = Assert.IsType<CompiledFirmwareArtifactClassification>(
            agreeing.Resolve("NT51925", null, CreateReference(1, 0x40000)));
        Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, consensus.Kind);
        Assert.Equal(CompiledFirmwareArtifactSignalStatus.NotDeclared, Assert.Single(consensus.Signals,
            static signal => signal.Kind == CompiledFirmwareArtifactSignalKind.DpContentPlausibility).Status);

        var conflictingAdapter = new ChangedPlausibilityAdapter(disagree: true);
        ResolvedCapabilityRoute route = Assert.Single(canonical.Catalog.GetCurrentSnapshot().DynamicRoutes,
            static candidate => candidate.Identity.IcId == "NT51925" && candidate.Identity.WorkflowId == ExperienceIds.StandardMerge);
        foreach (string mapId in route.CompilationContract.AllowedMapVariantIds)
        {
            conflictingAdapter.Compile(route.Identity, null, [], [], mapId, out CompiledComposition? composition,
                out _, out IReadOnlyList<CompositionIssue> issues);
            Assert.Empty(issues);
            Assert.Equal(mapId == "nt51925-standard-merge-fw2-256k"
                    ? CompiledFirmwareArtifactKind.Unknown : CompiledFirmwareArtifactKind.FlashCode,
                CompiledFirmwareArtifactClassifier.Classify(Assert.IsType<CompiledComposition>(composition), flash).Kind);
        }
        var conflicting = new FirmwareArtifactClassificationResolver(canonical.Catalog,
            new CanonicalCapabilityCompilerAdapter(canonical.Catalog, conflictingAdapter));
        (CompiledFirmwareArtifactClassification? disagreeClassification, ResolvedCapability? disagreeExact,
            IReadOnlyList<ResolvedCapability>? disagreeConsensus) = conflicting.ResolveWithExactStandardCapability("NT51925", null, flash);
        Assert.Null(disagreeClassification);
        Assert.Null(disagreeExact);
        Assert.Null(disagreeConsensus);
    }

    /// <summary>Replace recognizes synthetic TP-prefix and complete references while policy still blocks authoring.</summary>
    [Theory]
    [InlineData(1, 0x30000)]
    [InlineData(2, 0x30000)]
    [InlineData(1, 0x40000)]
    [InlineData(2, 0x40000)]
    public void ReplacePreparationRecognizesBothReferenceExtentsWithoutOpeningAuthoring(byte major, int capacity)
    {
        byte[] reference = CreateReference(major, capacity, fullFlash: capacity == 0x40000);
        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation preparation = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            session, "NT51925", IcNumberSelectionTokens.SingleChip,
            new Dictionary<string, string> { [CompositionSlotIds.ReplaceBase] = "synthetic-reference.bin" },
            new Dictionary<string, byte[]> { [CompositionSlotIds.ReplaceBase] = reference });

        Assert.False(preparation.Succeeded);
        Assert.Null(preparation.AcceptedSession);
        Assert.Null(session.CurrentSnapshot);
        Assert.NotEmpty(preparation.Issues);
        Assert.DoesNotContain(preparation.Issues, static issue => issue.Code == "input.reference.unrecognized");
        Assert.False(BootstrapTestHost.Canonical.Catalog.HasAuthorableCapability("NT51925", ExperienceIds.CtrlRamReplace));
    }

    private static byte[] CreateReference(byte major, int capacity, bool fullFlash = false)
    {
        byte[] reference = new byte[capacity];
        Nt51925VersionSelectionTests.CreateTp(major, major == 1 ? (byte)4 : (byte)0, major == 1 ? (byte)1 : (byte)0)
            .CopyTo(reference, 0);
        new byte[] { 0xC0, 0x12, 0x34, 0x56 }.CopyTo(reference, 0x24);
        reference[0x2F000 + FirmwareConfigLayout.FirmwareVersionOffset] = 0x42;
        reference[0x2F000 + FirmwareConfigLayout.FirmwareVersionBarOffset] = 0xBD;
        reference[0x2F000 + FirmwareConfigLayout.ChipNumberOffset] = 1;
        reference[0x2F00C] = 0x5A;
        if (fullFlash) { reference[0x3E001] = 0x71; }
        return reference;
    }

    private sealed class ChangedPlausibilityAdapter(bool disagree) : ICanonicalDynamicCompilationAdapter
    {
        private readonly BuiltInV2DynamicCompilationAdapter _inner = new();

        public CanonicalMapSelectionPrerequisite? GetMapSelectionPrerequisite(string icId, string workflowId)
        {
            return _inner.GetMapSelectionPrerequisite(icId, workflowId);
        }

        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
        {
            return _inner.TryGetAbAuthoringDefinition(identity, out definition, out issues);
        }

        public IReadOnlyList<long> GetMapCapacities(string icId, string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            return _inner.GetMapCapacities(icId, workflowId, out issues);
        }

        public void Compile(CapabilityRouteIdentity identity, long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds, out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan, out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            _inner.Compile(identity, requestedMapCapacity, selectedInputSlotIds,
                out composition, out metadataPlan, out issues, requestedTopology);
        }

        public void Compile(CapabilityRouteIdentity identity, long? requestedMapCapacity,
            IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts, IReadOnlyCollection<string>? selectedInputSlotIds,
            string? explicitMapId, out CompiledComposition? composition, out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues, TopologySelection? requestedTopology = null)
        {
            _inner.Compile(identity, requestedMapCapacity, capturedArtifacts, selectedInputSlotIds, explicitMapId,
                out composition, out metadataPlan, out issues, requestedTopology);
            if (composition is null || explicitMapId != "nt51925-standard-merge-fw2-256k") { return; }
            V2CompiledCompositionDetails details = composition.V2Details;
            V2CompilationProvenance original = details.Provenance;
            string changedSpace = disagree ? CompositionAddressSpaceIds.TpInput : CompositionAddressSpaceIds.DpInput;
            var provenance = new V2CompilationProvenance(original.Bundle, original.ProfileEntry, original.Context,
                original.Promotion, original.ProfileEvidenceRefs,
                original.ValidationRequirements.Select(validation =>
                    validation is CompiledUniformInputRangeValidation uniform && uniform.AddressSpaceId == changedSpace
                        ? new CompiledUniformInputRangeValidation(uniform.RuleId, uniform.Severity, uniform.IssueCode,
                            changedSpace, [new ByteRange(disagree ? 0x100 : 0x3E010, 2)]) : validation),
                original.RequiredCapabilities);
            composition = CompiledComposition.CreateV2(composition.Plan,
                new V2CompiledCompositionDetails(details.ProfileId, details.ProfileVersion, details.ExperienceId,
                    details.CompositionKind, provenance, details.InputContract, details.RegionAccessContract,
                    details.OutputNamingRequirement, details.IcNumberInputMode));
        }
    }
}
