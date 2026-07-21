namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Locks the shared viewport and global Button acknowledgement backlog to v0.9.13.</summary>
    [Fact]
    public void V0913OwnsSharedViewportAndGlobalButtonFeedback()
    {
        string roadmap = ReadText("docs/architecture/0.9.x-completion-roadmap.md");
        string tags = ReadText("docs/governance/development-tags.md");

        Assert.Contains("## v0.9.13: shared Hex viewport, Changes redesign, and interaction feedback", roadmap, StringComparison.Ordinal);
        Assert.Contains("Every interactive\nAvalonia `Button`", roadmap, StringComparison.Ordinal);
        Assert.Contains("Pointer, touch, Space, and Enter", roadmap, StringComparison.Ordinal);
        Assert.Contains("Reduced-motion mode", roadmap, StringComparison.Ordinal);
        Assert.Contains("`v0.9.12` | CtrlRAM routing and interaction stabilization", tags, StringComparison.Ordinal);
        Assert.Contains("`v0.9.13` | Shared Hex viewport, Button feedback, and AB re-admission", tags, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared Hex viewport and Changes redesign work remain deferred to `0.9.12`", tags, StringComparison.Ordinal);
    }

    /// <summary>Locks AB execution out of v0.9.12 and assigns typed re-admission to the separately gated v0.9.13 track.</summary>
    [Fact]
    public void V0913OwnsAbCodeProductionReadmissionWithoutV0912Exposure()
    {
        string roadmap = ReadText("docs/architecture/0.9.x-completion-roadmap.md");
        string decision = ReadText("docs/adr/0032-ab-code-production-readmission.md");
        string specification = ReadText("SPEC.md");

        Assert.Contains("### AB Code architecture re-admission owner amendment", roadmap, StringComparison.Ordinal);
        Assert.Contains("All AB Code execution remains unchanged, hidden, and", roadmap, StringComparison.Ordinal);
        Assert.Contains("Existing AB candidates remain hidden and fail closed", decision, StringComparison.Ordinal);
        Assert.Contains("PID, filenames, complete firmware SHA-256 values", decision, StringComparison.Ordinal);
        Assert.Contains("Existing candidates remain hidden and rejected at the Application run boundary throughout `v0.9.12`", specification, StringComparison.Ordinal);
    }

    /// <summary>Verifies the owner-priority roadmap schedules normal Replace before workflow convergence and deferred AB work.</summary>
    [Fact]
    public void OwnerPriorityTargetsNormalMergeReplaceBeforeAb()
    {
        (int replaceLine, string[] replaceMilestone) = FindMarkdownTableRow(
            "docs/governance/development-tags.md",
            "`0.5.0-dev.N`");
        (int convergenceLine, string[] convergenceMilestone) = FindMarkdownTableRow(
            "docs/governance/development-tags.md",
            "`0.6.0-dev.N`");
        (int abLine, string[] abMilestone) = FindMarkdownTableRow(
            "docs/governance/development-tags.md",
            "`0.7.0-dev.N`");

        Assert.True(replaceLine < convergenceLine, "Normal Replace must land before the workflow data-model refactor.");
        Assert.True(convergenceLine < abLine, "Workflow data-model convergence must happen before deferred AB work resumes.");
        Assert.Equal("Normal Replace priority", replaceMilestone[1]);
        Assert.Contains("DP", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("CtrlRAM", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("IC num", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("combiner", replaceMilestone[2], StringComparison.Ordinal);
        Assert.Equal("Workflow data-model convergence", convergenceMilestone[1]);
        Assert.Contains("unified", convergenceMilestone[2], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Merge/Replace", convergenceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("No new byte behavior", convergenceMilestone[2], StringComparison.Ordinal);
        Assert.Contains("AB merge", abMilestone[1], StringComparison.Ordinal);
        Assert.Contains("owner reactivation", abMilestone[2], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("golden evidence", abMilestone[2], StringComparison.OrdinalIgnoreCase);

        foreach (string ic in new[] { "NT51950", "NT51951" })
        {
            string[] row = FindMarkdownTableRow("docs/architecture/supported-ic-matrix.md", ic).Cells;

            Assert.Contains("canonical V2 DP Perspective Standard Merge route", row[1], StringComparison.Ordinal);
            Assert.Contains("DP Perspective", row[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DP and CtrlRAM priority", row[3], StringComparison.Ordinal);
            Assert.Contains("DP", row[4], StringComparison.Ordinal);
            Assert.Contains("golden", row[5], StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Verifies the per-IC flowchart reference stays synchronized with built-in IC lists.</summary>
    [Fact]
    public void IcWorkflowFlowchartReferenceCoversBuiltInIcLists()
    {
        string reference = ReadText("docs/architecture/ic-workflow-flowcharts.md");
        string[] standardMergeIcIds = ReadStandardMergeIcIds();
        string[] builtInIcIds =
        [
            .. standardMergeIcIds
                .Concat(ReadCtrlRamPostbuildIcIds())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(13, standardMergeIcIds.Length);
        Assert.Equal(standardMergeIcIds.Length, standardMergeIcIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("## Update rule", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.cs", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2Bundle.cs", reference, StringComparison.Ordinal);
        Assert.Contains("explicit production Standard Merge and DP Replace registration lists", reference, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowFlowchartReferenceCoversBuiltInIcLists", reference, StringComparison.Ordinal);
        Assert.Contains("NT51928 NB is not covered", reference, StringComparison.Ordinal);
        Assert.Contains("0x37000-0x37FFF (len 0x1000)", reference, StringComparison.Ordinal);
        Assert.Contains("R-CTRLRAM-927", reference, StringComparison.Ordinal);

        foreach (string icId in builtInIcIds)
        {
            Assert.Contains($"| {icId} |", reference, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies Replace planning exposes IC number and post-replace combiner readiness in the right surfaces.</summary>
    [Fact]
    public void ReplacePlanningRequiresIcNumAndCombinerPostProcessing()
    {
        string[] replaceBullets = ReadMarkdownBullets(
            "docs/ui/0.1.1-demo-interface-plan.md",
            "## Replace content");
        Assert.True(
            replaceBullets.Any(bullet => bullet.StartsWith("Shared Number selector", StringComparison.Ordinal)),
            "Replace content must use the shared Number context before region choices.");
        Assert.Contains(
            replaceBullets,
            bullet => bullet.Contains("single", StringComparison.Ordinal)
                && bullet.Contains("cascade", StringComparison.Ordinal)
                && bullet.Contains("numeric", StringComparison.Ordinal));

        string readinessBullet = Assert.Single(
            replaceBullets,
            bullet => bullet.StartsWith("Processor/tool readiness indicator", StringComparison.Ordinal));
        Assert.Contains("combiner.exe", readinessBullet, StringComparison.Ordinal);
        Assert.Contains("CRC/header", readinessBullet, StringComparison.Ordinal);

        string[] replaceRows = ReadPlanningResourceRows("Replace");
        Assert.Contains(
            replaceRows,
            row => row.StartsWith("Device context:", StringComparison.Ordinal)
                   && row.Contains("Number", StringComparison.Ordinal));
        Assert.Contains(
            replaceRows,
            row => row.StartsWith("CRC/header", StringComparison.Ordinal)
                   && row.Contains("combiner.exe", StringComparison.Ordinal));

        string[] row = FindMarkdownTableRow(
            "docs/architecture/integrity-processing-matrix.md",
            "CtrlRAM Replace priority flows").Cells;

        Assert.Contains("post-replace", row[1], StringComparison.Ordinal);
        Assert.Contains("combiner.exe", row[2], StringComparison.Ordinal);
        Assert.Contains("Combiner 1.13.0", row[3], StringComparison.Ordinal);
        Assert.Contains("NT51927", row[3], StringComparison.Ordinal);
        Assert.DoesNotContain("TPB", string.Join(' ', row), StringComparison.Ordinal);
    }
}
