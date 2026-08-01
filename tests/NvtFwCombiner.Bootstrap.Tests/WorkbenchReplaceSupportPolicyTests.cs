using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared runtime and display gates for IC-specific Replace support policy.</summary>
public sealed class WorkbenchReplaceSupportPolicyTests
{
    /// <summary>Retired NT51931 fails closed before input or processor planning.</summary>
    [Fact]
    public async Task Nt51931GeneralReplaceFailsClosedWithStableIssue()
    {
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
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
        Assert.False(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51931", WorkbenchReplaceModes.General));
        Assert.Empty(WorkbenchCompositionService.GetReplaceInputSlots("NT51931", "single", WorkbenchReplaceModes.General));
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
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

    /// <summary>Retiring NT51931 does not remove established Replace exposure from admitted ICs.</summary>
    [Fact]
    public void OtherIcReplaceExposureRemainsCatalogDriven()
    {
        Assert.True(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.CtrlRam));
        Assert.True(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.General));
        Assert.True(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.Dp));
    }

    /// <summary>Golden readiness reports verification without banning an evidence-gated workflow.</summary>
    [Fact]
    public void ReplaceReadinessSeparatesGoldenEvidenceFromAvailability()
    {
        WorkbenchWorkflowReadiness verified = WorkbenchCompositionService.GetReplaceWorkflowReadiness(
            "NT51950",
            WorkbenchReplaceModes.Dp);
        WorkbenchWorkflowReadiness gated = WorkbenchCompositionService.GetReplaceWorkflowReadiness(
            "NT51932",
            WorkbenchReplaceModes.Dp);

        Assert.True(verified.IsAvailable);
        Assert.Equal(WorkbenchWorkflowEvidenceStatus.GoldenVerified, verified.EvidenceStatus);
        Assert.True(gated.IsAvailable);
        Assert.Equal(WorkbenchWorkflowEvidenceStatus.EvidenceGated, gated.EvidenceStatus);
        Assert.Contains("does not ban authoring", gated.OpenCondition, StringComparison.Ordinal);
    }

    /// <summary>Workbench exposes Reply.md perfect/partial family facts without redefining firmware maps.</summary>
    [Theory]
    [InlineData("NT51917", "nt51927-family", "NT51927", WorkbenchIcFamilyRelationship.PerfectAlias)]
    [InlineData("NT51928", "nt51927-family", "NT51927", WorkbenchIcFamilyRelationship.PartialAlias)]
    [InlineData("NT51932", "nt51929-nt51932-family", "NT51929", WorkbenchIcFamilyRelationship.PerfectAlias)]
    public void IcFamilySummaryComesFromSupportCatalog(
        string icId,
        string familyId,
        string sourceIcId,
        WorkbenchIcFamilyRelationship relationship)
    {
        WorkbenchIcFamilySummary summary = WorkbenchCompositionService.GetIcFamilySummary(icId);

        Assert.Equal(familyId, summary.FamilyId);
        Assert.Equal(sourceIcId, summary.CanonicalIcId);
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
        Assert.Equal(expected, WorkbenchCompositionService.ArePerfectFamilyMembers(firstIcId, secondIcId));
        Assert.Equal(expected, WorkbenchCompositionService.ArePerfectFamilyMembers(secondIcId, firstIcId));
    }

    /// <summary>DP payload guidance follows registered profile identity instead of a Presentation IC branch.</summary>
    [Theory]
    [InlineData("NT51951", WorkbenchSlotIds.MergeDp, WorkbenchFirmwareSlotHint.InitialCodeAndLdc)]
    [InlineData("NT51951", WorkbenchSlotIds.ReplaceDp, WorkbenchFirmwareSlotHint.InitialCodeAndLdc)]
    [InlineData("NT51950", WorkbenchSlotIds.MergeDp, WorkbenchFirmwareSlotHint.None)]
    [InlineData("NT51951", WorkbenchSlotIds.MergeTp, WorkbenchFirmwareSlotHint.None)]
    public void FirmwareSlotGuidanceComesFromRegisteredProfile(
        string icId,
        string slotId,
        WorkbenchFirmwareSlotHint expected)
    {
        Assert.Equal(expected, WorkbenchCompositionService.GetFirmwareSlotHint(icId, slotId));
    }
}
