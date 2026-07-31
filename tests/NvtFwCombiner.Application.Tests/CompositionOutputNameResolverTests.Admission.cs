using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    /// <summary>The public capability constructor rejects an incoherent executable identity.</summary>
    [Fact]
    public void ResolvedCapabilityRejectsIncoherentPublication()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionCapability(
                fixture,
                composition,
                CapabilityFingerprint,
                fixture.Plan.ResolutionToken));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionCapability(
                fixture,
                composition,
                composition.CompilationFingerprint,
                new ResolutionToken("different-capability-publication")));
    }

    /// <summary>Capability route IC, workflow, and map are all compiled facts.</summary>
    [Theory]
    [InlineData("ic")]
    [InlineData("workflow")]
    [InlineData("map")]
    public void ResolvedCapabilityRejectsRouteDrift(string change)
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        var route = new CapabilityRouteIdentity(
            change == "ic" ? "NT51928" : "NT51929",
            change == "workflow" ? "dp-replace" : "standard-merge",
            "none",
            change == "map" ? "other-map" : "map");

        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionCapability(
                fixture,
                composition,
                composition.CompilationFingerprint,
                fixture.Plan.ResolutionToken,
                route: route));
    }

    /// <summary>Internal test construction still requires a valid route, fingerprint, and exact plan entries.</summary>
    [Fact]
    public void AcceptedInspectionConstructorRejectsInvalidIdentityOrPlanEntries()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        _ = Assert.Throws<ArgumentException>(() =>
            new OutputNamingAdmissionIdentity(
                OutputNamingRouteId,
                "not-a-sha256",
                fixture.Plan.ResolutionToken,
                fixture.Snapshot.AuthoringRevision));

        ResolvedMetadataPlan independentlyResolvedPlan =
            fixture.Plan.Definition.Resolve(fixture.Plan.ResolutionToken);
        _ = Assert.Throws<ArgumentException>(() =>
            new AcceptedOutputNamingInspection(
                OutputNamingRouteId,
                CapabilityFingerprint,
                independentlyResolvedPlan,
                fixture.Snapshot));
    }

    /// <summary>The default value-type token cannot become publication or report identity.</summary>
    [Fact]
    public void DefaultResolutionTokenIsRejectedAtEveryAdmissionBoundary()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);

        _ = Assert.Throws<ArgumentException>(() =>
            fixture.Plan.Definition.Resolve(default));
        _ = Assert.Throws<ArgumentException>(() =>
            new ResolvedMetadataPlan(
                fixture.Plan.Definition,
                default));
        _ = Assert.Throws<ArgumentException>(() =>
            default(ResolutionToken).ToString());
        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionCapability(
                fixture,
                composition,
                composition.CompilationFingerprint,
                default));
        _ = Assert.Throws<ArgumentException>(() =>
            new OutputNamingAdmissionIdentity(
                OutputNamingRouteId,
                composition.CompilationFingerprint,
                default,
                fixture.Snapshot.AuthoringRevision));
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            new OutputNamingAdmissionSummary(
                OutputNamingRouteId,
                composition.CompilationFingerprint,
                resolutionToken: null!,
                fixture.Snapshot.AuthoringRevision));
    }

    /// <summary>Catalog and resolved publication reject missing, repurposed, or rebound naming metadata.</summary>
    [Fact]
    public void CapabilityPublicationRejectsMetadataPlanAuthorityDrift()
    {
        InspectionFixture fixture = CreateInspectionFixture(includeDpcmi: true);
        CompiledComposition composition = CreateRuntimeComposition(fixture);
        ResolvedMetadataPlan emptyPlan = MetadataPlanDefinition.Empty.Resolve(
            fixture.Plan.ResolutionToken);
        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionDefinition(
                composition,
                MetadataPlanDefinition.Empty));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateAdmissionCapability(
                fixture,
                composition,
                composition.CompilationFingerprint,
                fixture.Plan.ResolutionToken,
                emptyPlan));

        MetadataPlanEntry dpcmi = fixture.Plan.Definition.Entries.Single(entry =>
            entry.BindingId == "dpcmi-naming");
        MetadataPlanEntry firmwareConfig =
            fixture.Plan.Definition.Entries.Single(entry =>
                entry.BindingId == "firmware-config-general-parameters-naming");
        var repurposedDpcmi = new MetadataPlanEntry(
            dpcmi.BindingId,
            dpcmi.SpaceId,
            dpcmi.SlotId,
            dpcmi.FamilyDefinition,
            dpcmi.ResolvedMap,
            dpcmi.MetadataSetBinding,
            dpcmi.StructureDefinition,
            dpcmi.TargetReferences,
            [MetadataReferencePurpose.Inspection],
            dpcmi.EvidenceRefs);
        var reboundDpcmi = new MetadataPlanEntry(
            dpcmi.BindingId,
            firmwareConfig.SpaceId,
            firmwareConfig.SlotId,
            firmwareConfig.FamilyDefinition,
            firmwareConfig.ResolvedMap,
            firmwareConfig.MetadataSetBinding,
            firmwareConfig.StructureDefinition,
            firmwareConfig.TargetReferences,
            [MetadataReferencePurpose.OutputNaming],
            firmwareConfig.EvidenceRefs);

        foreach (MetadataPlanEntry invalidDpcmi in
                 new[] { repurposedDpcmi, reboundDpcmi })
        {
            var definition = new MetadataPlanDefinition(
                [invalidDpcmi, firmwareConfig]);
            _ = Assert.Throws<ArgumentException>(() =>
                CreateAdmissionDefinition(
                    composition,
                    definition));
            _ = Assert.Throws<ArgumentException>(() =>
                CreateAdmissionCapability(
                    fixture,
                    composition,
                    composition.CompilationFingerprint,
                    fixture.Plan.ResolutionToken,
                    definition.Resolve(fixture.Plan.ResolutionToken)));
        }
    }

    private static ResolvedCapability CreateAdmissionCapability(
        InspectionFixture fixture,
        CompiledComposition composition,
        string capabilityFingerprint,
        ResolutionToken capabilityResolutionToken,
        ResolvedMetadataPlan? metadataPlan = null,
        CapabilityRouteIdentity? route = null)
    {
        route ??= new CapabilityRouteIdentity(
                "NT51929",
                "standard-merge",
                "none",
                "map");
        return new ResolvedCapability(
            route,
            capabilityFingerprint,
            composition,
            Decision(
                "authoring",
                CapabilityAuthoringAvailability.Available),
            Decision(
                "publication",
                CapabilityPublicationStatus.Supported),
            Decision(
                "evidence",
                CapabilityEvidenceStatus.ContractOnly),
            metadataPlan ?? fixture.Plan,
            capabilityResolutionToken);

        PinnedCapabilityDecision<TValue> Decision<TValue>(
            string decisionId,
            TValue value)
            where TValue : struct, Enum
        {
            return new PinnedCapabilityDecision<TValue>(
                decisionId,
                route.RouteId,
                capabilityFingerprint,
                value,
                "synthetic-output-naming");
        }
    }

    private static CanonicalCapabilityDefinition CreateAdmissionDefinition(
        CompiledComposition composition,
        MetadataPlanDefinition metadataPlan)
    {
        var route = new CapabilityRouteIdentity(
            "NT51929",
            "standard-merge",
            "none",
            "map");
        string fingerprint = composition.CompilationFingerprint;
        return new CanonicalCapabilityDefinition(
            route,
            composition,
            Decision(
                "authoring",
                CapabilityAuthoringAvailability.Available),
            Decision(
                "publication",
                CapabilityPublicationStatus.Supported),
            Decision(
                "evidence",
                CapabilityEvidenceStatus.ContractOnly),
            metadataPlan);

        PinnedCapabilityDecision<TValue> Decision<TValue>(
            string decisionId,
            TValue value)
            where TValue : struct, Enum
        {
            return new PinnedCapabilityDecision<TValue>(
                decisionId,
                route.RouteId,
                fingerprint,
                value,
                "synthetic-output-naming");
        }
    }
}
