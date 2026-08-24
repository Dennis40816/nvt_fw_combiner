using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared runtime and display gates over the canonical capability publication.</summary>
public sealed class CanonicalCapabilityProjectionPolicyTests
{
    /// <summary>Retired NT51931 fails closed before input or processor planning.</summary>
    [Fact]
    public async Task Nt51931GeneralReplaceFailsClosedWithStableIssue()
    {
        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralReplaceAsync(
            BootstrapTestHost.Canonical,
            "NT51931",
            "single",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = "\0must-not-be-resolved.bin",
            },
            new GeneralMappingDraftState([]),
            savedRulePolicy: null,
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        CompositionIssue issue = Assert.Single(prepared.Issues);
        Assert.Equal(CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported, issue.Code);
        Assert.Contains("Not available", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Retired NT51931 projections expose a blocked state without a compatibility route.</summary>
    [Fact]
    public void Nt51931GeneralReplaceIsExplicitlyNotSupported()
    {
        Assert.False(BootstrapTestHost.Canonical.Projection.IsReplaceWorkflowAvailable("NT51931", ExperienceIds.GeneralReplace));
        Assert.False(BootstrapTestHost.Canonical.Projection
            .GetReplaceWorkflowReadiness("NT51931", ExperienceIds.GeneralReplace)
            .HasExactRoute);
    }

    /// <summary>Replace exposure follows only routes declared by the canonical publication.</summary>
    [Fact]
    public void OtherIcReplaceExposureIsCanonicalPublicationDriven()
    {
        Assert.True(BootstrapTestHost.Canonical.Projection.IsReplaceWorkflowAvailable("NT51932", ExperienceIds.CtrlRamReplace));
        Assert.False(BootstrapTestHost.Canonical.Projection.IsReplaceWorkflowAvailable("NT51932", ExperienceIds.GeneralReplace));
        Assert.False(BootstrapTestHost.ProductCanonical.Projection.IsReplaceWorkflowAvailable("NT51932", ExperienceIds.DpReplace));
        Assert.True(BootstrapTestHost.Canonical.Projection.IsReplaceWorkflowAvailable("NT51926", ExperienceIds.GeneralReplace));
        Assert.False(BootstrapTestHost.Canonical.Projection.GetReplaceWorkflowReadiness("NT51932", ExperienceIds.GeneralReplace)
            .HasExactRoute);
        Assert.True(BootstrapTestHost.Canonical.Projection.GetReplaceWorkflowReadiness("NT51926", ExperienceIds.GeneralReplace)
            .HasExactRoute);
    }

    /// <summary>DP evidence remains visible after the independent authoring gate is closed.</summary>
    [Fact]
    public void ReplaceReadinessSeparatesGoldenEvidenceFromAvailability()
    {
        CapabilityWorkflowReadiness verified = BootstrapTestHost.ProductCanonical.Projection.GetReplaceWorkflowReadiness(
            "NT51929",
            ExperienceIds.DpReplace);
        CapabilityWorkflowReadiness gated = BootstrapTestHost.ProductCanonical.Projection.GetReplaceWorkflowReadiness(
            "NT51932",
            ExperienceIds.DpReplace);

        Assert.False(verified.IsAvailable);
        Assert.Equal(CapabilityEvidenceStatus.DirectGolden, verified.EvidenceStatus);
        Assert.True(verified.HasReviewedEvidence);
        Assert.False(gated.IsAvailable);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, gated.EvidenceStatus);
        Assert.False(gated.IsEvidencePending);
        Assert.Contains("1.1.0", gated.OpenCondition, StringComparison.Ordinal);
    }

    /// <summary>Direct DP authoring fails closed with a typed issue before reading any input.</summary>
    [Fact]
    public void DpReplaceDirectAuthoringReturnsTypedUnavailableIssue()
    {
        CompiledAuthoringSelectionSnapshot selection =
            BootstrapTestHost.ProductServices.DpReplaceAuthoring.GetAuthoringSnapshot(
                "NT51950",
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));

        Assert.Empty(selection.Catalog.Routes);
        Assert.Empty(selection.InputBindings);
        CompositionIssue issue = Assert.Single(selection.Issues);
        Assert.Equal(
            CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported,
            issue.Code);
        Assert.Contains("Not available", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Workbench exposes owner-declared symmetric perfect/partial family facts.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PerfectAlias)]
    [InlineData("NT51927", "nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PerfectAlias)]
    [InlineData("NT51928", "nt51917-nt51927-nt51928-canonical-container", CapabilityFamilyRelationship.PartialAlias)]
    [InlineData("NT51929", "nt51929-nt51932", CapabilityFamilyRelationship.PerfectAlias)]
    [InlineData("NT51932", "nt51929-nt51932", CapabilityFamilyRelationship.PerfectAlias)]
    public void IcFamilySummaryComesFromCanonicalMap(
        string icId,
        string familyId,
        CapabilityFamilyRelationship relationship)
    {
        CapabilityFamilySummary summary = BootstrapTestHost.Canonical.Projection.GetIcFamilySummary(icId);

        Assert.Equal(familyId, summary.FamilyId);
        Assert.Equal(relationship, summary.Relationship);
        Assert.False(string.IsNullOrWhiteSpace(summary.Scope));
    }

    /// <summary>Only canonical/perfect members of one declared family qualify for advisory context reuse.</summary>
    [Theory]
    [InlineData("NT51917", "NT51927", true)]
    [InlineData("NT51919", "NT51932", true)]
    [InlineData("NT51928", "NT51927", false)]
    [InlineData("NT51923", "NT51926", false)]
    public void PerfectFamilyPairExcludesPartialAndUnrelatedIcs(
        string firstIcId,
        string secondIcId,
        bool expected)
    {
        Assert.Equal(expected, BootstrapTestHost.Canonical.Projection.ArePerfectFamilyMembers(firstIcId, secondIcId));
        Assert.Equal(expected, BootstrapTestHost.Canonical.Projection.ArePerfectFamilyMembers(secondIcId, firstIcId));
    }

}
