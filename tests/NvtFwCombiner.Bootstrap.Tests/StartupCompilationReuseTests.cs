using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using ResolvedFirmwareImageMap =
    NvtFwCombiner.Domain.Firmware.FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Reused startup compilations must equal the compilations they replace, without widening their lifetime.</summary>
public sealed class StartupCompilationReuseTests
{
    private const string Nt51928Scope =
        "NT51917 inherits NT51927 Initial Code geometry through their perfect-like relationship, and the firmware " +
        "owner confirmed NT51927/NT51928 share that geometry while NT51928 retains its distinct LDC and complete " +
        "container map. The all-IC DPCMI definition is canonical independently of this partial family relationship. " +
        "NT51917 inherits NT51927 TP geometry through their perfect-like relationship, and the firmware owner " +
        "confirmed NT51927/NT51928 share that geometry without importing LDC or another workflow. The all-IC " +
        "FirmwareConfig definition is canonical independently of this partial family relationship. " +
        "NT51917 inherits NT51927 complete Header facts through their perfect-like relationship, and both NT51928 " +
        "capacity maps reference only the owner-approved non-NB 927 TP Flash Header definition while retaining " +
        "distinct LDC, DP, and container facts.";

    private const string Nt51927PerfectScope =
        "The firmware owner confirmed NT51917 and NT51927 share one complete modeled firmware definition while " +
        "requested-member evidence and publication remain separate.";

    private const string Nt51929PerfectScope =
        "The firmware owner confirmed NT51919, NT51929, and NT51932 share one complete modeled firmware definition " +
        "while requested-member evidence and publication remain separate.";

    /// <summary>Every published report plan equals an uncached exact-map compilation, entry by entry.</summary>
    [Fact]
    public void ReportMetadataPlanEqualsUncachedCompilationForEveryCtrlRamRoute()
    {
        Assert.NotEmpty(CtrlRamV2RouteRegistry.All);
        foreach (CtrlRamV2Route route in CtrlRamV2RouteRegistry.All)
        {
            ProfileBundleRuntimeRegistration registration = BuiltInV2BundleRegistry.TrustIndex.Bundles
                .Where(bundle => bundle.BundleDirectory == route.BundleId)
                .SelectMany(static bundle => bundle.RuntimeRegistrations)
                .Single(candidate =>
                    candidate.WorkflowId == ExperienceIds.CtrlRamReplace &&
                    candidate.IcId == route.Key.IcId &&
                    candidate.PostbuildProcessorId == route.Key.PostbuildProcessorId &&
                    candidate.ProfileId == route.ProfileId &&
                    candidate.ProfileVersion == route.ProfileVersion);
            MetadataPlanDefinition uncached = CtrlRamV2RouteRegistry.ValidateReportMetadataCounterpart(
                registration,
                BuiltInV2RegistrationRegistry.StandardMergeByIc[route.Key.IcId]);

            AssertEquivalentPlans(uncached, route.ReportMetadataPlan);
        }
    }

    /// <summary>Routes of one Standard counterpart map share one compilation; every route keeps its own plan.</summary>
    [Fact]
    public void RoutesOfOneExactStandardMapShareOneReportCompilation()
    {
        CtrlRamV2Route[] reportful =
            [.. CtrlRamV2RouteRegistry.All.Where(static route => route.ReportMetadataMapId is not null)];
        IGrouping<(BuiltInV2Registration Standard, string MapId), CtrlRamV2Route>[] groups =
        [
            .. reportful.GroupBy(static route => (
                BuiltInV2RegistrationRegistry.StandardMergeByIc[route.Key.IcId],
                route.ReportMetadataMapId!)),
        ];

        Assert.True(groups.Length < reportful.Length);
        Assert.All(groups, group =>
        {
            ResolvedFirmwareImageMap shared = group.First().ReportMetadataPlan.Entries[0].ResolvedMap;
            Assert.All(group, route => Assert.All(
                route.ReportMetadataPlan.Entries,
                entry => Assert.Same(shared, entry.ResolvedMap)));
        });
        Assert.Equal(
            groups.Length,
            reportful.Select(static route => route.ReportMetadataPlan.Entries[0].ResolvedMap)
                .Distinct(ReferenceEqualityComparer.Instance)
                .Count());
        Assert.Equal(
            reportful.Length,
            reportful.Select(static route => route.ReportMetadataPlan)
                .Distinct(ReferenceEqualityComparer.Instance)
                .Count());
    }

    /// <summary>Each disclosure candidate yields the plan a fresh single compilation produces.</summary>
    [Fact]
    public void DisclosureCompilationEqualsFreshCompilationForEveryCandidate()
    {
        BuiltInV2Registration[] registrations = DisclosureRegistrations();
        Assert.NotEmpty(registrations);
        foreach (BuiltInV2Registration registration in registrations)
        {
            long?[] candidates = Candidates(registration);
            (long? InputLength, CompiledComposition? Composition, IReadOnlyList<CompositionIssue> Issues)[] reused =
                [.. registration.TryCompileEach(candidates)];

            Assert.Equal(candidates, reused.Select(static result => result.InputLength));
            for (int index = 0; index < candidates.Length; index++)
            {
                registration.TryCompile(
                    candidates[index],
                    out CompiledComposition? fresh,
                    out IReadOnlyList<CompositionIssue> freshIssues);
                Assert.Empty(freshIssues);
                Assert.Empty(reused[index].Issues);
                AssertEquivalentCompilations(
                    Assert.IsType<CompiledComposition>(fresh),
                    Assert.IsType<CompiledComposition>(reused[index].Composition));
            }
        }
    }

    /// <summary>Capacities that the compiler ignores share one compilation inside a call, never across calls.</summary>
    [Fact]
    public void CapacityIndependentCandidatesShareOneCompilationOnlyWithinOneCall()
    {
        BuiltInV2Registration[] selectionGroups =
        [
            .. DisclosureRegistrations().Where(static registration =>
                registration.InputSelectionGroupMemberSlotIds.Count != 0 &&
                Candidates(registration).Length > 1),
        ];
        Assert.NotEmpty(selectionGroups);
        foreach (BuiltInV2Registration registration in selectionGroups)
        {
            CompiledComposition[] first = CompileAll(registration);
            CompiledComposition[] second = CompileAll(registration);

            Assert.All(first, composition => Assert.Same(first[0], composition));
            Assert.All(second, composition => Assert.Same(second[0], composition));
            Assert.NotSame(first[0], second[0]);
            AssertEquivalentCompilations(first[0], second[0]);
        }
    }

    /// <summary>A single declared capacity reuses the registration's existing summary compilation.</summary>
    [Fact]
    public void SingleCapacityCandidateReusesTheExistingSummaryCompilation()
    {
        BuiltInV2Registration[] singleCapacity =
        [
            .. DisclosureRegistrations().Where(static registration => Candidates(registration).Length == 1),
        ];
        Assert.NotEmpty(singleCapacity);
        foreach (BuiltInV2Registration registration in singleCapacity)
        {
            Assert.True(registration.CreateProfileSummary().CompileSucceeded);
            CompiledComposition first = Assert.Single(CompileAll(registration));
            CompiledComposition second = Assert.Single(CompileAll(registration));
            registration.TryCompile(
                Candidates(registration)[0],
                out CompiledComposition? fresh,
                out IReadOnlyList<CompositionIssue> freshIssues);

            Assert.Same(first, second);
            Assert.Empty(freshIssues);
            AssertEquivalentCompilations(Assert.IsType<CompiledComposition>(fresh), first);
        }
    }

    /// <summary>Rejected lengths and topology-bearing requests keep their exact single-compilation outcome.</summary>
    [Fact]
    public void RejectedAndTopologyBearingRequestsMatchSingleCompilation()
    {
        Assert.NotEmpty(BuiltInV2RegistrationRegistry.AbMerge);
        foreach (BuiltInV2Registration registration in BuiltInV2RegistrationRegistry.AbMerge)
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            Assert.Empty(issues);
            long?[] candidates = [null, .. capacities.Select(static capacity => (long?)capacity), capacities.Max() + 1];
            (long? InputLength, CompiledComposition? Composition, IReadOnlyList<CompositionIssue> Issues)[] results =
                [.. registration.TryCompileEach(candidates)];

            Assert.Equal(candidates, results.Select(static result => result.InputLength));
            for (int index = 0; index < candidates.Length; index++)
            {
                registration.TryCompile(
                    candidates[index],
                    out CompiledComposition? fresh,
                    out IReadOnlyList<CompositionIssue> freshIssues);
                Assert.Equal(
                    freshIssues.Select(static issue => (issue.Code, issue.Message, issue.OperationId)),
                    results[index].Issues.Select(static issue => (issue.Code, issue.Message, issue.OperationId)));
                Assert.Equal(fresh is null, results[index].Composition is null);
                if (fresh is not null)
                {
                    AssertEquivalentCompilations(fresh, results[index].Composition!);
                }
            }
        }
    }

    /// <summary>The family summaries derived from those compilations keep their exact published values.</summary>
    [Fact]
    public void CompiledFamilyDisclosureKeepsItsPublishedSummaries()
    {
        CapabilityCatalogLoadResult loaded = CompositionHostServices.CreateCanonicalCapabilityCatalogSource()
            .Load(TestContext.Current.CancellationToken);
        Assert.True(loaded.Succeeded);
        CanonicalCapabilityDisclosure disclosure = loaded.Candidate!.Disclosure;
        var expected = new Dictionary<string, CapabilityFamilySummary>(StringComparer.Ordinal)
        {
            ["NT51917"] = new("nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PerfectAlias, Nt51927PerfectScope),
            ["NT51919"] = new("nt51929-nt51932", CapabilityFamilyRelationship.PerfectAlias, Nt51929PerfectScope),
            ["NT51923"] = new(null, CapabilityFamilyRelationship.Standalone, null),
            ["NT51926"] = new(null, CapabilityFamilyRelationship.Standalone, null),
            ["NT51927"] = new("nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PerfectAlias, Nt51927PerfectScope),
            ["NT51928"] = new("nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PartialAlias, Nt51928Scope),
            ["NT51929"] = new("nt51929-nt51932", CapabilityFamilyRelationship.PerfectAlias, Nt51929PerfectScope),
            ["NT51932"] = new("nt51929-nt51932", CapabilityFamilyRelationship.PerfectAlias, Nt51929PerfectScope),
        };

        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            DisclosureRegistrations().Select(static registration => registration.IcId).Order(StringComparer.Ordinal));
        Assert.All(expected, pair => Assert.Equal(pair.Value, disclosure.GetFamilySummary(pair.Key)));
    }

    private static BuiltInV2Registration[] DisclosureRegistrations()
    {
        return
        [
            .. BuiltInV2RegistrationRegistry.StandardMergeByIc.Values
                .Where(static registration => registration.SourceEnvelopeBinding is null)
                .OrderBy(static registration => registration.IcId, StringComparer.Ordinal),
        ];
    }

    private static long?[] Candidates(BuiltInV2Registration registration)
    {
        IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        Assert.Empty(issues);
        return capacities.Count == 0 ? [null] : [.. capacities.Select(static capacity => (long?)capacity)];
    }

    private static CompiledComposition[] CompileAll(BuiltInV2Registration registration)
    {
        return
        [
            .. registration.TryCompileEach(Candidates(registration)).Select(static result =>
            {
                Assert.Empty(result.Issues);
                return Assert.IsType<CompiledComposition>(result.Composition);
            }),
        ];
    }

    private static void AssertEquivalentCompilations(CompiledComposition expected, CompiledComposition actual)
    {
        ResolvedFirmwareImageMap expectedMap = expected.V2Details.Provenance.ResolvedMap;
        ResolvedFirmwareImageMap actualMap = actual.V2Details.Provenance.ResolvedMap;
        Assert.Equal(expected.CompilationFingerprint, actual.CompilationFingerprint);
        Assert.Equal(expectedMap.ResolutionFingerprint, actualMap.ResolutionFingerprint);
        Assert.Equal(
            (expectedMap.FamilyId, expectedMap.FamilyVersion, expectedMap.FamilyContentHash, expectedMap.ImageMap.MapId),
            (actualMap.FamilyId, actualMap.FamilyVersion, actualMap.FamilyContentHash, actualMap.ImageMap.MapId));
        Assert.Equal(
            expectedMap.FamilyRelationships.Select(DescribeRelationship),
            actualMap.FamilyRelationships.Select(DescribeRelationship));
    }

    private static string DescribeRelationship(FirmwareFamilyRelationship relationship)
    {
        return string.Join(
            "|",
            relationship.GetType().Name,
            relationship.RelationshipId,
            relationship.Reason,
            string.Join(",", relationship.MemberIds),
            string.Join(",", relationship.EvidenceRefs));
    }

    private static void AssertEquivalentPlans(MetadataPlanDefinition expected, MetadataPlanDefinition actual)
    {
        if (ReferenceEquals(expected, MetadataPlanDefinition.Empty))
        {
            Assert.Same(MetadataPlanDefinition.Empty, actual);
            return;
        }

        Assert.Equal(expected.SourceIdentity, actual.SourceIdentity);
        Assert.Null(actual.FullImageContext);
        Assert.Equal(expected.ReportProjections, actual.ReportProjections);
        Assert.Equal(expected.Entries.Count, actual.Entries.Count);
        Assert.NotEmpty(actual.Entries);
        for (int index = 0; index < expected.Entries.Count; index++)
        {
            MetadataPlanEntry left = expected.Entries[index];
            MetadataPlanEntry right = actual.Entries[index];
            Assert.Equal(
                (left.BindingId, left.SpaceId, left.SlotId, left.MemberId),
                (right.BindingId, right.SpaceId, right.SlotId, right.MemberId));
            Assert.Same(left.FamilyDefinition, right.FamilyDefinition);
            Assert.Same(left.ImageMap, right.ImageMap);
            Assert.Same(left.MetadataSetBinding, right.MetadataSetBinding);
            Assert.Same(left.StructureDefinition, right.StructureDefinition);
            Assert.Equal(left.ResolvedMap.ResolutionFingerprint, right.ResolvedMap.ResolutionFingerprint);
            Assert.Equal(left.TargetReferences, right.TargetReferences);
            Assert.Equal(left.FieldIds, right.FieldIds);
            Assert.Equal(left.Purposes, right.Purposes);
            Assert.Equal(left.EvidenceRefs, right.EvidenceRefs);
        }
    }
}
