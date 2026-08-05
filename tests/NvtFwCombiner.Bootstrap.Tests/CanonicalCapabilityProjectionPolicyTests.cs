using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared runtime and display gates over the canonical capability publication.</summary>
public sealed class CanonicalCapabilityProjectionPolicyTests
{
    /// <summary>Retired NT51931 fails closed before input or processor planning.</summary>
    [Fact]
    public async Task Nt51931GeneralReplaceFailsClosedWithStableIssue()
    {
        WorkbenchRunResult result = await CompositionExecutionAdapter.RunReplaceAsync(
            "NT51931",
            "single",
            WorkbenchReplaceModes.General,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = "\0must-not-be-resolved.bin",
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Blocked", result.Status);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(WorkbenchIssueCodes.ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.Contains("Not available", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
        Assert.Empty(document.RootElement.GetProperty("Operations").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("Inputs").EnumerateArray());
    }

    /// <summary>Retired NT51931 projections expose a blocked state without a compatibility route.</summary>
    [Fact]
    public void Nt51931GeneralReplaceDisplayIsExplicitlyNotSupported()
    {
        Assert.False(CanonicalCapabilityProjection.IsReplaceWorkflowAvailable("NT51931", WorkbenchReplaceModes.General));
        Assert.Empty(CompositionMemoryProjection.GetReplaceInputSlots("NT51931", "single", WorkbenchReplaceModes.General));
        WorkbenchMemoryDisplay display = CompositionMemoryProjection.GetReplaceMemoryDisplay(
            "NT51931",
            "single",
            WorkbenchReplaceModes.General);
        Assert.Equal("Not available", display.RangeLabel);
        WorkbenchMemoryMapRow row = Assert.Single(display.MemoryMapRows);
        Assert.Equal("Blocked", row.ActionLabel);
        Assert.Equal("No target", row.AfterSource);
        Assert.Contains("Not available", row.Detail, StringComparison.Ordinal);
        Assert.Empty(display.CoverageSegments);
    }

    /// <summary>Replace exposure follows only routes declared by the canonical publication.</summary>
    [Fact]
    public void OtherIcReplaceExposureIsCanonicalPublicationDriven()
    {
        Assert.True(CanonicalCapabilityProjection.IsReplaceWorkflowAvailable("NT51932", WorkbenchReplaceModes.CtrlRam));
        Assert.False(CanonicalCapabilityProjection.IsReplaceWorkflowAvailable("NT51932", WorkbenchReplaceModes.General));
        Assert.True(CanonicalCapabilityProjection.IsReplaceWorkflowAvailable("NT51932", WorkbenchReplaceModes.Dp));
        Assert.True(CanonicalCapabilityProjection.IsReplaceWorkflowAvailable("NT51926", WorkbenchReplaceModes.General));
        Assert.False(CanonicalCapabilityProjection
            .GetReplaceWorkflowReadiness("NT51932", WorkbenchReplaceModes.General)
            .HasExactRoute);
        Assert.True(CanonicalCapabilityProjection
            .GetReplaceWorkflowReadiness("NT51926", WorkbenchReplaceModes.General)
            .HasExactRoute);
    }

    /// <summary>Golden readiness reports verification without banning an evidence-gated workflow.</summary>
    [Fact]
    public void ReplaceReadinessSeparatesGoldenEvidenceFromAvailability()
    {
        CapabilityWorkflowReadiness verified = CanonicalCapabilityProjection.GetReplaceWorkflowReadiness(
            "NT51929",
            WorkbenchReplaceModes.Dp);
        CapabilityWorkflowReadiness gated = CanonicalCapabilityProjection.GetReplaceWorkflowReadiness(
            "NT51932",
            WorkbenchReplaceModes.Dp);

        Assert.True(verified.IsAvailable);
        Assert.Equal(CapabilityEvidenceStatus.DirectGolden, verified.EvidenceStatus);
        Assert.True(verified.HasReviewedEvidence);
        Assert.True(gated.IsAvailable);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, gated.EvidenceStatus);
        Assert.True(gated.IsEvidencePending);
        Assert.Contains("does not ban authoring", gated.OpenCondition, StringComparison.Ordinal);
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
        CapabilityFamilySummary summary = CanonicalCapabilityProjection.GetIcFamilySummary(icId);

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
        Assert.Equal(expected, CanonicalCapabilityProjection.ArePerfectFamilyMembers(firstIcId, secondIcId));
        Assert.Equal(expected, CanonicalCapabilityProjection.ArePerfectFamilyMembers(secondIcId, firstIcId));
    }

}
