using System.Text.Json;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Shared runtime and display gates for IC-specific Replace support policy.</summary>
public sealed class WorkbenchReplaceSupportPolicyTests
{
    /// <summary>Every NT51931 Replace route fails before input or processor planning.</summary>
    [Theory]
    [InlineData(WorkbenchReplaceModes.Dp)]
    [InlineData(WorkbenchReplaceModes.CtrlRam)]
    [InlineData(WorkbenchReplaceModes.General)]
    public async Task Nt51931ReplaceFailsClosedWithStableIssue(string replaceMode)
    {
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51931",
            "single",
            replaceMode,
            new Dictionary<string, string>(StringComparer.Ordinal),
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Blocked", result.Status);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(WorkbenchIssueCodes.ReplaceWorkflowNotSupported, issue.GetProperty("Code").GetString());
        Assert.Contains("Not Supported", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
        Assert.Empty(document.RootElement.GetProperty("Operations").EnumerateArray());
    }

    /// <summary>Workbench projections expose an explicit blocked state instead of executable inputs.</summary>
    [Theory]
    [InlineData(WorkbenchReplaceModes.Dp)]
    [InlineData(WorkbenchReplaceModes.CtrlRam)]
    [InlineData(WorkbenchReplaceModes.General)]
    public void Nt51931ReplaceDisplayIsExplicitlyNotSupported(string replaceMode)
    {
        Assert.False(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51931", replaceMode));
        Assert.Empty(WorkbenchCompositionService.GetReplaceInputSlots("NT51931", "single", replaceMode));
        Assert.Equal(
            "Not Supported",
            WorkbenchCompositionService.GetReplaceMemoryRangeLabel("NT51931", "single", replaceMode));

    }

    /// <summary>The NT51931 gate does not remove established Replace exposure from other ICs.</summary>
    [Fact]
    public void OtherIcReplaceExposureRemainsCatalogDriven()
    {
        Assert.True(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.CtrlRam));
        Assert.True(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.General));
        Assert.False(WorkbenchCompositionService.IsReplaceWorkflowSupported("NT51932", WorkbenchReplaceModes.Dp));
    }
}
