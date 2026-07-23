namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Locks the delivery/family milestone split and keeps the Presentation backlog deferred.</summary>
    [Fact]
    public void V0915ThroughV0918OwnDeliveryFamilyCandidatesAndDeferPresentationBacklog()
    {
        string roadmap = ReadText("docs/architecture/0.9.x-completion-roadmap.md");
        string tags = ReadText("docs/governance/development-tags.md");
        string deliveryRoadmap = ReadText("docs/architecture/v0.9.15-0.9.17-roadmap.md");
        string viewportDecision = ReadText("docs/adr/0028-capability-driven-shared-hex-viewport.md");

        Assert.Contains("## v0.9.15: AB delivery readiness and review automation", roadmap, StringComparison.Ordinal);
        Assert.Contains("No U0-U5 implementation is\nauthorized under `0.9.15`", roadmap, StringComparison.Ordinal);
        Assert.Contains("later\nowner-selected milestone", roadmap, StringComparison.Ordinal);
        Assert.Contains("Every interactive Avalonia `Button`", roadmap, StringComparison.Ordinal);
        Assert.Contains("Pointer, touch, Space, and Enter", roadmap, StringComparison.Ordinal);
        Assert.Contains("Reduced-motion mode", roadmap, StringComparison.Ordinal);
        Assert.Contains("`v0.9.12` | CtrlRAM routing and interaction stabilization", tags, StringComparison.Ordinal);
        Assert.Contains("`v0.9.15` | AB function open, delivery readiness, and review automation", tags, StringComparison.Ordinal);
        Assert.Contains("`v0.9.16` | NT51950 AB golden-certification closure", tags, StringComparison.Ordinal);
        Assert.Contains("`v0.9.17` | NT51951 AB golden-certification closure", tags, StringComparison.Ordinal);
        Assert.Contains("`v0.9.18` | Family/evidence model unification", tags, StringComparison.Ordinal);
        Assert.Contains("peeled commit `9b15d8757ccb44167c471ca4e602036066bcdea9`", tags, StringComparison.Ordinal);
        Assert.DoesNotContain("`v0.9.15` | UI unification, shared Hex viewport, and Button feedback", tags, StringComparison.Ordinal);

        int progressionStart = tags.IndexOf("## Progression", StringComparison.Ordinal);
        int progressionEnd = tags.IndexOf("## Rules", StringComparison.Ordinal);
        string progression = tags[progressionStart..progressionEnd];
        Assert.Contains("v0.9.12         CtrlRAM production routing, interaction stabilization, and release governance", progression, StringComparison.Ordinal);
        Assert.Contains("v0.9.15         AB function open, input/output usability, and review automation", progression, StringComparison.Ordinal);
        Assert.Contains("v0.9.16         NT51950 AB golden-certification closure", progression, StringComparison.Ordinal);
        Assert.Contains("v0.9.17         NT51951 AB golden-certification closure", progression, StringComparison.Ordinal);
        Assert.Contains("v0.9.18         family/evidence model unification", progression, StringComparison.Ordinal);
        Assert.DoesNotContain("code-size/shared Hex", progression, StringComparison.Ordinal);

        Assert.Contains("| `0.9.15` | Open NT51919/NT51929/NT51932 perfect-family AB plus NT51950", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| `0.9.16` | NT51950 AB golden-certification closure", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| `0.9.17` | NT51951 AB golden-certification closure", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("peeled commit\n  `9b15d8757ccb44167c471ca4e602036066bcdea9`", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("owner-supplied single golden covers only `single`", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("selector-free plan remains one declared byte plan", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("## Family evidence-reuse policy", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("**Perfect family:** treat members as the same for a *like-for-like workflow*", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("**LDC family:** for LDC-related families such as NT51927/NT51928", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("NT51950/NT51951, the presence or absence of local dimming divides DP behavior", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("no LDC-family golden may be shared for DP Replace, AB Code", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("Standard Merge, or any other DP-writing/DP-layout workflow", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("TP/CtrlRAM sharing is not evidence for a DP range, header", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("This is the single canonical roadmap for the next four milestones.", deliveryRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("v0.9.18-family-evidence-roadmap.md", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("later owner-selected Presentation milestone", viewportDecision, StringComparison.Ordinal);
        Assert.Contains("## Post-v0.9.18 owner decision queue", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| Evidence-backed code-size reduction and related-function reuse |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("code size is a review metric, never a hard ratchet or release gate", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| Settings support-matrix entry and evidence dashboard |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("function availability, certification/support status, direct versus reusable golden evidence", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("must never select a profile, waive a gate, infer byte behavior", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| Error experience unification |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| IC family and rule authoring UI |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| Customized plan reuse and import |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| Report detail, layout, and functional review |", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("# v0.9.15–v0.9.18 Delivery and Family Roadmap", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| `perfect-family` | NT51919/NT51929/NT51932", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| `ldc-tp-only` | NT51927/NT51928", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("| `ldc-tp-only` | NT51950/NT51951", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("It is not an authorization to change a profile, map, processor, support stage,", deliveryRoadmap, StringComparison.Ordinal);
    }

    /// <summary>Separates reviewed AB function availability from direct-golden and support-certification debt.</summary>
    [Fact]
    public void AbFunctionAvailabilityDoesNotHideGoldenDebt()
    {
        string matrix = ReadText("docs/architecture/supported-ic-matrix.md");

        Assert.Contains("## AB function availability, direct-golden debt, and progress", matrix, StringComparison.Ordinal);
        Assert.Contains("A missing direct golden is an evidence debt, not a", matrix, StringComparison.Ordinal);
        Assert.Contains("**33.3%**; **4 missing**", matrix, StringComparison.Ordinal);
        Assert.Contains("NT51950 `Cascade`, and selector-free NT51951", matrix, StringComparison.Ordinal);
        Assert.Contains("**66.7%**", matrix, StringComparison.Ordinal);
        Assert.Contains("**100.0%**; external golden, firmware-owner, independent-review, packaging, and release-owner gates remain open.", matrix, StringComparison.Ordinal);
        Assert.Contains("**0.0%**; function availability must not be presented as certification.", matrix, StringComparison.Ordinal);
    }

    /// <summary>Locks the historical v0.9.13 AB gate and the current function-open but certification-neutral re-admission state.</summary>
    [Fact]
    public void V0914OwnsAbCodeProductionReadmissionWithoutV0913Exposure()
    {
        string roadmap = ReadText("docs/architecture/0.9.x-completion-roadmap.md");
        string releaseRoadmap = ReadText("docs/architecture/v0.9.14-roadmap-and-release-gates.md");
        string decision = ReadText("docs/adr/0032-ab-code-production-readmission.md");
        string specification = ReadText("SPEC.md");

        Assert.Contains("### AB Code architecture re-admission owner amendment", roadmap, StringComparison.Ordinal);
        Assert.Contains("All AB Code execution remains unchanged, hidden, and", roadmap, StringComparison.Ordinal);
        Assert.Contains("Existing AB candidates remain hidden and fail closed", decision, StringComparison.Ordinal);
        Assert.Contains("PID, filenames, complete firmware SHA-256 values", decision, StringComparison.Ordinal);
        Assert.Contains("NT51919, NT51929, and NT51932 form one perfect family", decision, StringComparison.Ordinal);
        Assert.Contains("DP1 is\n  `[0x00000,0x40000)` and DP2 is `[0x40000,0x80000)`", decision, StringComparison.Ordinal);
        Assert.Contains("three-byte CMI Reg16h-18h layout", decision, StringComparison.Ordinal);
        Assert.Contains("`[0x401A,0x401D)` and `[0x4401A,0x4401D)`", decision, StringComparison.Ordinal);
        Assert.Contains("`0x67/0x68` reader remains output-naming", decision, StringComparison.Ordinal);
        Assert.Contains("Each selected TP BIN is inspected independently", decision, StringComparison.Ordinal);
        Assert.Contains("four explicit values, never accepts a raw\noffset", decision, StringComparison.Ordinal);
        Assert.Contains("cannot select a route from filename, PID, version, hash, or payload metadata", releaseRoadmap, StringComparison.Ordinal);
        Assert.Contains("decoded metadata or Unknown", releaseRoadmap, StringComparison.Ordinal);
        Assert.Contains("equal or `Unknown` values never collapse", releaseRoadmap, StringComparison.Ordinal);
        Assert.Contains("AB uses one DP_AB card with distinct DP1/DP2 subrows", releaseRoadmap, StringComparison.Ordinal);
        Assert.Contains("The first `v0.9.14` AB pilot", specification, StringComparison.Ordinal);
        Assert.Contains("AB follows ADR 0036's `NT519xx_FlashCode_A_DmmmmTvvvv_B_DmmmmTvvvv_yyyyMMdd.bin` form", specification, StringComparison.Ordinal);
        Assert.Contains("AB Code architecture was re-admitted under ADR 0032 in `v0.9.14`", specification, StringComparison.Ordinal);
        Assert.Contains("The `0.9.15` candidate exposes only the declared NT51919/NT51929/NT51932", specification, StringComparison.Ordinal);
        Assert.Contains("still gate support certification and release", specification, StringComparison.Ordinal);
        Assert.Contains("Cross-workflow Merge/Replace header, Evidence, and Memory coverage unification", releaseRoadmap, StringComparison.Ordinal);
        Assert.Contains("slot cards show one highest-severity icon", releaseRoadmap, StringComparison.Ordinal);
    }

    /// <summary>Locks the owner-approved Public-through-v1.0.0 schedule and its disclosure boundary.</summary>
    [Fact]
    public void V10VisibilityDecisionSupersedesPostV0911Schedule()
    {
        string specification = ReadText("SPEC.md");
        string changelog = ReadText("CHANGELOG.md");
        string review = ReadText("docs/governance/v0.9.12-public-visibility-review.md");

        Assert.Contains("維持至 stable `v1.0.0` 完成；其後改為 `Private`", specification, StringComparison.Ordinal);
        Assert.Contains("supersedes the earlier visibility schedule", changelog, StringComparison.Ordinal);
        Assert.Contains("remain Public through stable `v1.0.0`; become Private afterward", review, StringComparison.Ordinal);
        Assert.Contains("cannot retract commit history, source archives,\ncaches, clones, or forks", review, StringComparison.Ordinal);
        Assert.Contains("already tracks owner-approved canonical golden BIN payloads", review, StringComparison.Ordinal);
        Assert.Contains("adds no new BIN, archive,\ncredential, signing key, token, or private payload", review, StringComparison.Ordinal);
        Assert.Contains("including already tracked owner-approved golden payloads", review, StringComparison.Ordinal);
        Assert.Contains("ignored owner-handoff `.7z` remains outside Git", review, StringComparison.Ordinal);
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
        string[] abMergeIcIds = ReadAbMergeIcIds();
        string[] builtInIcIds =
        [
            .. standardMergeIcIds
                .Concat(ReadCtrlRamPostbuildIcIds())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(13, standardMergeIcIds.Length);
        Assert.Equal(standardMergeIcIds.Length, standardMergeIcIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"], abMergeIcIds);
        Assert.Contains("## Update rule", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.cs", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2Bundle.cs", reference, StringComparison.Ordinal);
        Assert.Contains("explicit Standard Merge, AB pilot, and DP Replace registration lists", reference, StringComparison.Ordinal);
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
