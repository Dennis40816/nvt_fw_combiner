namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Keeps future scope in the single NFC roadmap while preserving the 0.9.x hot-fix record.</summary>
    [Fact]
    public void NfcRoadmapRecordsPublishedV110AndCurrentAllocations()
    {
        string roadmap = ReadText("docs/architecture/0.9.x-completion-roadmap.md");
        string tags = ReadText("docs/governance/development-tags.md");
        string deliveryRoadmap = ReadText("docs/architecture/v0.9.15-0.9.17-roadmap.md");
        string nfcRoadmap = ReadText("docs/architecture/nfc_roadmap.md");
        string readme = ReadText("README.md");
        string normalizedNfcRoadmap = string.Join(
            ' ',
            nfcRoadmap.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string spec = ReadText("SPEC.md");
        string informationArchitecture = ReadText("docs/ui/information-architecture.md");
        string supportedIcMatrix = ReadText("docs/architecture/supported-ic-matrix.md");
        string replacementRecord = ReadText(
            "docs/governance/change-records/ARCH-ROADMAP-111-REBASELINE-01.json");
        string navigationHandoff = ReadText(
            "docs/ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md");
        string bundleRenameHandoff = ReadText(
            "docs/ui/v1.1.x-bundle-primary-output-rename-handoff.md");
        string normalizedBundleRenameHandoff = string.Join(
            ' ',
            bundleRenameHandoff.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string reportHistoryUsabilityHandoff = ReadText(
            "docs/ui/v1.1.x-report-history-usability-handoff.md");
        string normalizedReportHistoryUsabilityHandoff = string.Join(
            ' ',
            reportHistoryUsabilityHandoff.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string abDpMetadataLayoutHandoff = ReadText(
            "docs/ui/v1.1.x-ab-dp-metadata-layout-handoff.md");
        string normalizedAbDpMetadataLayoutHandoff = string.Join(
            ' ',
            abDpMetadataLayoutHandoff.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string abIcSelectorScopeHandoff = ReadText(
            "docs/ui/v1.1.x-ab-ic-selector-scope-handoff.md");
        string normalizedAbIcSelectorScopeHandoff = string.Join(
            ' ',
            abIcSelectorScopeHandoff.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string reportChangesCompareHandoff = ReadText(
            "docs/ui/v1.1.x-report-changes-compare-handoff.md");
        string normalizedReportChangesCompareHandoff = string.Join(
            ' ',
            reportChangesCompareHandoff.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string standardMergeVerificationFeedbackHandoff = ReadText(
            "docs/ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md");
        string normalizedStandardMergeVerificationFeedbackHandoff = string.Join(
            ' ',
            standardMergeVerificationFeedbackHandoff.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
        string selectorContract = ReadText("docs/ui/v1.1.5-ctrlram-selector-visual-contract.md");
        string selectorReference = ReadText("docs/ui/references/v1.1.5-ctrlram-selector-reference.svg");
        string documentConvergenceHandoff = ReadText(
            "docs/architecture/v1.1.2-repository-document-convergence-handoff.md");
        string documentConvergenceManifest = ReadText(
            "docs/architecture/v1.1.2-repository-document-convergence-manifest.md");
        string sizeAdr = ReadText("docs/adr/0021-code-size-ratchet-and-convergence.md");
        string dependencyPlan = ReadText("docs/governance/0.10.x-ticket-dependency-plan.md");
        string[] coreAuthorities = [spec, sizeAdr, dependencyPlan];

        Assert.Contains("Status: historical execution and release evidence", roadmap, StringComparison.Ordinal);
        Assert.Contains("Status: historical release-planning evidence", deliveryRoadmap, StringComparison.Ordinal);
        Assert.Contains("Future\nmilestone scope, sequencing, and dates are maintained only", tags, StringComparison.Ordinal);

        Assert.Contains("# NFC Roadmap", nfcRoadmap, StringComparison.Ordinal);
        foreach (string route in new[]
        {
            "docs/architecture/nfc_roadmap.md",
            "SPEC.md",
            "docs/adr/README.md",
            "AGENTS.md",
            "docs/governance/development-execution-workflow.md",
            "docs/governance/branch-version-and-release-governance.md",
            "docs/governance/agent-skill-routing.md",
            "docs/governance/agent-skill-inventory.md",
            "CHANGELOG.md",
            "docs/references/verification-report.md",
            "docs/ci/release-package.md",
        })
        {
            Assert.Contains($"]({route})", readme, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("0.10.x-ticket-dependency-plan.md", readme, StringComparison.Ordinal);
        Assert.Contains("This file owns future milestone order and release boundaries only.", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("`0.10.0` reconciles its original `v0.9.15` planning baseline", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("reviewed `v0.9.16` hot-fix", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("## `0.10.0`: planning and governance baseline", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("## `0.10.1` through `0.10.6`: owner-allocated implementation", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("`v0.10.2` publishes the reviewed desktop adoption", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("`v0.10.3` completes the remaining approved refactoring graph", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("`v0.10.6` reserves a configured-path update screen", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("It does not allocate or implement a production Support Matrix", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains(
            "The approved GitHub issues named in the",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "numbers are not assumed to be a contiguous range",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("#207, #214, #219, and #221", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("Dependency depth is not a release version.", normalizedNfcRoadmap, StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.0`: published manual-only Windows baseline",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "`v1.1.0` was published on 2026-09-01 as the bounded direct-run Windows x64 distribution.",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.1`: verification, test, CI, and release architecture only",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "`v1.1.1` owns only the verification, test, CI, and release architecture review",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "then reusing accepted component evidence, improving narrow-test selection, and removing duplicated setup",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.2`: repository document convergence planning",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "physical whole-repository document inventory, topology, retention",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "v1.1.2-repository-document-convergence-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "v1.1.2-repository-document-convergence-manifest.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.3`: infrastructure determinism and isolation",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "four-seed infrastructure determinism/isolation",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "It must not reduce a gate, verifier, or trust boundary.",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.4`: session diagnostics and Version-page review",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "privacy-filtered current-session diagnostics/history",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "## `1.1.5`: CtrlRAM selector visual contract",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "../ui/v1.1.5-ctrlram-selector-visual-contract.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        string[] expectedV11Allocations =
        [
            "## `1.1.2`: repository document convergence planning",
            "## `1.1.3`: infrastructure determinism and isolation",
            "## `1.1.4`: session diagnostics and Version-page review",
            "## `1.1.5`: CtrlRAM selector visual contract",
            "## `1.1.6`: first-entry selection and CtrlRAM Replace diagnosis",
            "## `1.1.7`: Memory Layout review",
            "## `1.1.8`: AB selector and DP metadata layout",
            "## `1.1.9`: Standard Merge verification feedback",
            "## `1.1.10`: Report History usability",
            "## `1.1.11`: Report Changes compare",
            "## `1.1.12`: bundle primary-output rename",
            "## `1.1.13`: agent execution workflow and AI-skill pilot",
            "## `1.1.14`: evidence-preserving semantic convergence and minimality",
        ];
        string[] actualV11Allocations =
        [.. nfcRoadmap
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("## `1.1.", StringComparison.Ordinal)
                && !line.StartsWith("## `1.1.0`", StringComparison.Ordinal)
                && !line.StartsWith("## `1.1.1`", StringComparison.Ordinal))];
        Assert.Equal(expectedV11Allocations, actualV11Allocations);

        int v1114SectionStart = nfcRoadmap.IndexOf(expectedV11Allocations[^1], StringComparison.Ordinal);
        int v1114SectionEnd = nfcRoadmap.IndexOf("\n## ", v1114SectionStart + expectedV11Allocations[^1].Length, StringComparison.Ordinal);
        string v1114Section = nfcRoadmap[v1114SectionStart..v1114SectionEnd];
        string normalizedV1114Section = string.Join(
            ' ',
            v1114Section.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("stale SPEC/release evidence", normalizedV1114Section, StringComparison.Ordinal);
        Assert.Contains("current-versus-historical headings", normalizedV1114Section, StringComparison.Ordinal);
        Assert.Contains("active handoff's open TODO, owner, blocker, and next-action summary", normalizedV1114Section, StringComparison.Ordinal);
        Assert.Contains("must not add a parallel documentation framework", normalizedV1114Section, StringComparison.Ordinal);
        Assert.Contains(
            "../governance/change-records/VERIFY-111-XUNIT-SEED-01.json",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("immutable historical evidence", normalizedNfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("former `v1.1.2` allocation is superseded to `v1.1.3`", normalizedNfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("D1-D7 deletion batch as complete with final frozen review and final verifier evidence", normalizedNfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting independent review/final gate", normalizedNfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("Repository document convergence", readme, StringComparison.Ordinal);
        Assert.Contains("no second inventory or TODO owner", readme, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Root.FullName, "docs", "README.md")));
        Assert.Contains("](nfc_roadmap.md)", documentConvergenceHandoff, StringComparison.Ordinal);
        Assert.Contains("](v1.1.2-repository-document-convergence-manifest.md)", documentConvergenceHandoff, StringComparison.Ordinal);
        Assert.Contains("DELETE_APPROVED", documentConvergenceManifest, StringComparison.Ordinal);
        foreach (string authorityPath in new[]
        {
            "docs/adr/0021-code-size-ratchet-and-convergence.md",
            "docs/governance/change-records/DOCS-112-RETENTION-CLEANUP-01.json",
            "testdata/golden/canonical/manifest.json",
            "docs/governance/v0.9.9-final-owner-golden-gap-matrix-20260718.md",
            "docs/ci/release-package.md",
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(ReadText(authorityPath)));
        }

        foreach ((string releaseHeading, string handoffPath) in new[]
        {
            ("## `1.1.2`: repository document convergence planning", "v1.1.2-repository-document-convergence-handoff.md"),
            ("## `1.1.5`: CtrlRAM selector visual contract", "../ui/v1.1.5-ctrlram-selector-visual-contract.md"),
            ("## `1.1.6`: first-entry selection and CtrlRAM Replace diagnosis", "../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md"),
            ("## `1.1.8`: AB selector and DP metadata layout", "../ui/v1.1.x-ab-ic-selector-scope-handoff.md"),
            ("## `1.1.8`: AB selector and DP metadata layout", "../ui/v1.1.x-ab-dp-metadata-layout-handoff.md"),
            ("## `1.1.9`: Standard Merge verification feedback", "../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md"),
            ("## `1.1.10`: Report History usability", "../ui/v1.1.x-report-history-usability-handoff.md"),
            ("## `1.1.11`: Report Changes compare", "../ui/v1.1.x-report-changes-compare-handoff.md"),
            ("## `1.1.12`: bundle primary-output rename", "../ui/v1.1.x-bundle-primary-output-rename-handoff.md"),
            ("## `1.1.13`: agent execution workflow and AI-skill pilot", "../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md"),
        })
        {
            int sectionStart = nfcRoadmap.IndexOf(releaseHeading, StringComparison.Ordinal);
            int sectionEnd = nfcRoadmap.IndexOf("\n## ", sectionStart + releaseHeading.Length, StringComparison.Ordinal);
            string section = nfcRoadmap[sectionStart..(sectionEnd < 0 ? nfcRoadmap.Length : sectionEnd)];

            Assert.Contains(handoffPath, section, StringComparison.Ordinal);
        }
        const string v1120Heading = "## `1.2.0`: bounded Launcher hardening and development";
        const string v1130Heading = "## `1.3.0`: Launcher and publication-system extraction review";
        int v1114Index = nfcRoadmap.IndexOf(expectedV11Allocations[^1], StringComparison.Ordinal);
        int v1120Index = nfcRoadmap.IndexOf(v1120Heading, StringComparison.Ordinal);
        int v1130Index = nfcRoadmap.IndexOf(v1130Heading, StringComparison.Ordinal);
        Assert.Equal(v1120Index, nfcRoadmap.LastIndexOf(v1120Heading, StringComparison.Ordinal));
        Assert.True(v1114Index < v1120Index && v1120Index < v1130Index);

        string v1120Section = nfcRoadmap[v1120Index..v1130Index];
        int unallocatedQueueIndex = nfcRoadmap.IndexOf("\n## Explicit owner-unallocated queue", v1130Index + v1130Heading.Length, StringComparison.Ordinal);
        string v1130Section = nfcRoadmap[v1130Index..unallocatedQueueIndex];
        string unallocatedQueueSection = nfcRoadmap[unallocatedQueueIndex..];
        string normalizedV1130Section = string.Join(
            ' ',
            v1130Section.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string normalizedUnallocatedQueueSection = string.Join(
            ' ',
            unallocatedQueueSection.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("Launcher work remains secondary to every `v1.1.x` UI and performance priority", v1120Section, StringComparison.Ordinal);
        Assert.Contains("begins real bounded Launcher hardening/development", v1120Section, StringComparison.Ordinal);
        Assert.Contains("comprehensive current defect, security, and evidence inventory", v1120Section, StringComparison.Ordinal);
        Assert.Contains("owner-approved, reviewable remediation tranche", v1120Section, StringComparison.Ordinal);
        Assert.Contains("does not claim Catalog/Registry production activation or release readiness", v1120Section, StringComparison.Ordinal);
        Assert.Contains("activation remains **NO-GO** until separate R3 security/evidence closure", v1120Section, StringComparison.Ordinal);
        Assert.Contains("subsequent architecture/repository extraction review", v1130Section, StringComparison.Ordinal);
        Assert.Contains("not the first Launcher delivery", v1130Section, StringComparison.Ordinal);
        Assert.Contains("remains secondary to the `v1.1.x` plan", v1130Section, StringComparison.Ordinal);
        Assert.Contains("not allocated to a current `v1.1.x` release", normalizedV1130Section, StringComparison.Ordinal);
        Assert.Contains("after the first `v1.2.0` remediation tranche", unallocatedQueueSection, StringComparison.Ordinal);
        Assert.Contains("Catalog/Registry production activation", unallocatedQueueSection, StringComparison.Ordinal);
        Assert.Contains("remaining publisher trust/signing/security closure", normalizedUnallocatedQueueSection, StringComparison.Ordinal);
        Assert.Contains("## Explicit owner-unallocated queue", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains(
            "Launcher activation is **NO-GO** until separately approved R3 security/evidence closure",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "Catalog/Registry production activation",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Former planned `v1.1.0` product-expansion/analyzer bundle", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("Former generic `v1.1.x` Golden-evidence item", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains(
            "The published `v1.1.0` made no ordinary DP Replace retirement-or-reopening decision, so that decision is also owner-unallocated.",
            normalizedNfcRoadmap,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "## `1.0.8` through `1.0.13`: update delivery and release-flow closure",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "## `1.1.0`: deferred product expansion and analyzer cleanup",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.DoesNotContain("v1.0.9-ctrlram-selector-visual-contract.md", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Former `v1.0.10`", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Former `v1.0.13`", nfcRoadmap, StringComparison.Ordinal);

        Assert.Contains("published `v1.1.0` made no retirement-or-reopening decision", informationArchitecture, StringComparison.Ordinal);
        Assert.Contains("owner-unallocated", informationArchitecture, StringComparison.Ordinal);
        Assert.DoesNotContain("without the `1.1.0` owner decision", informationArchitecture, StringComparison.Ordinal);
        Assert.Contains("Published `v1.1.0` made no retirement-or-reopening decision", supportedIcMatrix, StringComparison.Ordinal);
        Assert.Contains("owner-unallocated", supportedIcMatrix, StringComparison.Ordinal);
        Assert.DoesNotContain("Owner decision at `1.1.0`", supportedIcMatrix, StringComparison.Ordinal);
        Assert.DoesNotContain("retirement or reopening is decided at", supportedIcMatrix, StringComparison.Ordinal);
        Assert.DoesNotContain("owner will decide the feature at `1.1.0`", supportedIcMatrix, StringComparison.Ordinal);

        Assert.Contains(
            "\"taskId\": \"ARCH-ROADMAP-111-REBASELINE-01\"",
            replacementRecord,
            StringComparison.Ordinal);
        Assert.Contains("ARCH-ROADMAP-108-130-01", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("v1.1.1 verification, test, CI, and release architecture only", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("measured CI-flow optimization", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("v1.1.2 complete former v1.0.9 bundle", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("v1.1.3 Memory Layout review", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("fail-closed pre-tag release preflight", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("post-publish verification", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("is not claimed fully green here", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("owner-unallocated", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("proposed and pending product-owner approval", replacementRecord, StringComparison.Ordinal);
        Assert.Contains("grants no product, firmware, support, or implementation authority", replacementRecord, StringComparison.Ordinal);

        foreach (string handoff in new[]
        {
            navigationHandoff,
            bundleRenameHandoff,
            reportHistoryUsabilityHandoff,
            abDpMetadataLayoutHandoff,
            abIcSelectorScopeHandoff,
            reportChangesCompareHandoff,
            standardMergeVerificationFeedbackHandoff,
        })
        {
            Assert.DoesNotContain("Status: allocated", handoff, StringComparison.Ordinal);
        }

        Assert.Contains(
            "../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", navigationHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.6` milestone](../architecture/nfc_roadmap.md#116-first-entry-selection-and-ctrlram-replace-diagnosis)",
            navigationHandoff,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-bundle-primary-output-rename-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", bundleRenameHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.12` milestone](../architecture/nfc_roadmap.md#1112-bundle-primary-output-rename)",
            bundleRenameHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompositionOutputNamingExperience.ResolveAcceptedOutput",
            bundleRenameHandoff,
            StringComparison.Ordinal);
        Assert.Contains("OutputNamingSummary.IsExplicitOverride", bundleRenameHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "Bundle folder name** and **Output filename** are independent fields",
            bundleRenameHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "excluded from the verification-only `v1.1.1` release",
            normalizedBundleRenameHandoff,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-bundle-primary-output-rename-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-report-history-usability-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.10` milestone](../architecture/nfc_roadmap.md#1110-report-history-usability)",
            reportHistoryUsabilityHandoff,
            StringComparison.Ordinal);
        Assert.Contains("OpenReportHistoryEntryAsyncCommand", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("RemoveReportHistoryEntryCommand", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("leading source hypothesis", normalizedReportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("real control-click regression", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("Segoe Fluent", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("LoadedReportJson", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("failing `SetTextAsync`", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("LoadReportJsonButton_OnClick", reportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains("first import impossible", normalizedReportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "returns keyboard focus to the invoking Load report action",
            normalizedReportHistoryUsabilityHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "moves focus to the opened report heading",
            normalizedReportHistoryUsabilityHandoff,
            StringComparison.Ordinal);
        Assert.Contains("not part of the verification-only `v1.1.1`", normalizedReportHistoryUsabilityHandoff, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-report-history-usability-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-ab-dp-metadata-layout-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", abDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.8` milestone](../architecture/nfc_roadmap.md#118-ab-selector-and-dp-metadata-layout)",
            abDpMetadataLayoutHandoff,
            StringComparison.Ordinal);
        Assert.Contains("CompiledInputVersionObservation", abDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("FormatAbVersion", abDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("FirmwareSlotInformationFactTemplate", abDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("four-column desktop `UniformGrid`", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("DP1 Version**, optional **DP1 Jira Index**, **DP2 Version**, optional **DP2 Jira Index", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("no value containing ` · `", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("omits that bank's Jira Index fact", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("`FormatAbVersion` is removed", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("Compact height may grow only by the existing second fact row", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("all four values, including `AUTO_PRJ-4095`, render without ellipsis", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.Contains("not part of the verification-only `v1.1.1`", normalizedAbDpMetadataLayoutHandoff, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-ab-dp-metadata-layout-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-ab-ic-selector-scope-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", abIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.8` milestone](../architecture/nfc_roadmap.md#118-ab-selector-and-dp-metadata-layout)",
            abIcSelectorScopeHandoff,
            StringComparison.Ordinal);
        Assert.Contains("CapabilitySelectorPublication.Create", abIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("WorkflowSelectorProjection.WorkflowIcChoices", abIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("PublishAcceptedMergeSharedContext", abIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("PublishCanonicalCatalogIcChoices", abIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("retains the collection obtained while Standard Merge was active", normalizedAbIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("synchronously republishes `IcChoices` exactly once", normalizedAbIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("must not add a dispatcher", normalizedAbIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("must not hard-code IC identifiers in XAML", normalizedAbIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.Contains("not part of the verification-only `v1.1.1`", normalizedAbIcSelectorScopeHandoff, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-ab-ic-selector-scope-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-report-changes-compare-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.11` milestone](../architecture/nfc_roadmap.md#1111-report-changes-compare)",
            reportChangesCompareHandoff,
            StringComparison.Ordinal);
        Assert.Contains("ListBox.reportHexDiffRanges", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("contentScrollSurface", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("ByteDiff.FindChangedRanges", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("physical `SectionLabel`", reportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("A nested ScrollViewer is forbidden", normalizedReportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("must not change `ByteDiff`", normalizedReportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("must not alias error, caution, controller-input, or reference-slot tokens", normalizedReportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains("fixed English-only width guesses are forbidden", normalizedReportChangesCompareHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "not part of the verification-only `v1.1.1`",
            normalizedReportChangesCompareHandoff,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-report-changes-compare-handoff.md")));
        Assert.Contains(
            "../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("Status: handoff-only scope, acceptance, diagnosis, and evidence.", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "[NFC roadmap `v1.1.9` milestone](../architecture/nfc_roadmap.md#119-standard-merge-verification-feedback)",
            standardMergeVerificationFeedbackHandoff,
            StringComparison.Ordinal);
        Assert.Contains("FileContentSnapshotInspector", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains("CompiledInputArtifactInspectionService", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains("CompiledInputLoadValidationEvaluator", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains("DP_UNIFORM_CONTENT_WARNING", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains("TP_UNIFORM_CONTENT_WARNING", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains("`BlocksBuild=false`", standardMergeVerificationFeedbackHandoff, StringComparison.Ordinal);
        Assert.Contains(
            "The **Verified** badge is deliberately narrower than “this firmware is known good.”",
            normalizedStandardMergeVerificationFeedbackHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "Presentation must not reread the file",
            normalizedStandardMergeVerificationFeedbackHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not block Build",
            normalizedStandardMergeVerificationFeedbackHandoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "not part of the verification-only `v1.1.1`",
            normalizedStandardMergeVerificationFeedbackHandoff,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.x-standard-merge-input-verification-feedback-handoff.md")));
        Assert.Contains("# v1.1.5 CtrlRAM selector visual contract", selectorContract, StringComparison.Ordinal);
        Assert.Contains("Status: proposed reference for product-owner approval.", selectorContract, StringComparison.Ordinal);
        Assert.Contains("pending product-owner approval", selectorContract, StringComparison.Ordinal);
        Assert.Contains("v1.1.5 CtrlRAM selector — annotated geometry board", selectorReference, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.5-ctrlram-selector-visual-contract.md")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.2-ctrlram-selector-visual-contract.md")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "v1.1.4-ctrlram-selector-visual-contract.md")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "references",
            "v1.1.2-ctrlram-selector-reference.svg")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "docs",
            "ui",
            "references",
            "v1.1.4-ctrlram-selector-reference.svg")));
        Assert.Contains(
            "IC family/rule authoring UI after the trusted-bundle and evidence models are",
            nfcRoadmap,
            StringComparison.Ordinal);
        Assert.Contains("agent-skill-inventory.md", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("0.10.x-ticket-dependency-plan.md", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("verification-report.md", nfcRoadmap, StringComparison.Ordinal);

        Assert.DoesNotContain("Historical 0.9.x release index", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("`0.9.15` | AB function opening", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("`0.9.15` | Final 0.9.x release", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("| `0.10.1` |", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("| `0.10.2` |", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("The reviewed upstream active inventory contains 22 skills", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("| Engineering |", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("| Phase | Scope | Exit gate |", nfcRoadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("Canonicalize only transport-level line endings", nfcRoadmap, StringComparison.Ordinal);
        Assert.Contains("Status: owner-approved dependency graph.", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains(
            "GitHub issues and PRs are the sole live\nexecution-state records.",
            dependencyPlan,
            StringComparison.Ordinal);
        Assert.Contains(
            "## Canonical Core completion amendment — 2026-08-08",
            dependencyPlan,
            StringComparison.Ordinal);
        foreach (string authority in coreAuthorities)
        {
            string normalizedAuthority = string.Join(
                ' ',
                authority.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(
                "with a proportionate implementation, verification, and evidence cost and a net-negative result is completed",
                normalizedAuthority,
                StringComparison.Ordinal);
            Assert.Contains(
                "total implementation, verification, and evidence cost is disproportionate to its maintenance benefit",
                normalizedAuthority,
                StringComparison.Ordinal);
            Assert.Contains(
                "only as a #230-owned same-PR exception",
                normalizedAuthority,
                StringComparison.Ordinal);
        }
        foreach (string authority in new[] { spec, sizeAdr })
        {
            string normalizedAuthority = string.Join(
                ' ',
                authority.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(
                "broad #231 implementation begins only after #230 closes and the owner separately approves its intake",
                normalizedAuthority,
                StringComparison.Ordinal);
        }
        string normalizedDependencyPlan = string.Join(
            ' ',
            dependencyPlan.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains(
            "#231 and #232 may execute in parallel only after the owner separately approves implementation intake for each",
            normalizedDependencyPlan,
            StringComparison.Ordinal);
        Assert.Contains("| 0 | Canonical pilot | #173 | Deliver the NT51929 Standard Merge canonical capability tracer | — |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 0 | Headless retirement | #221 | Retire NT51920/NT51925/NT51930/NT51931 production capabilities | — |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 4 | Headless data | #177 | Migrate remaining admitted metadata family bindings | #174, #175, #176, #221 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 5 | Headless firmware | #259 | Canonicalize source projections and FlashCode admission | #219, #239 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 6 | Headless firmware | #187 | Migrate admitted legacy TP Header families | #186, #221, #259, and matching #177 family slice |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 16 | Deferred UI | #214 | Deliver Message Center and System Information diagnostics | #173, #185, #208 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 18 | Core convergence | #230 | Converge Domain + Profiles to one canonical firmware model | #195, #196, #259 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 19 | Core convergence | #231 | Converge Application on capability-centered use cases | #195, #196, #259, #230 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 19 | Core convergence | #232 | Converge Infrastructure, Contracts, and CRC worker protocol ownership | #195, #196, #230 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 20 | Core convergence | #233 | Converge Bootstrap + CLI to wiring-only composition | #195, #196, #230, #231, #232 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 21 | Core convergence | #229 | Complete evidence-backed Canonical Core Convergence | #230, #231, #232, #233 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains("| 22 | Integration | #197 | Close the 0.10.x integration gate and allocate releases | #171, #172, #229 |", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains(
            "## Pre-Core production-growth allocation (historical; superseded 2026-08-08)",
            dependencyPlan,
            StringComparison.Ordinal);
        Assert.DoesNotContain("four-slice hard gate", dependencyPlan, StringComparison.Ordinal);
        Assert.DoesNotContain("#197's hard 44,000-line integration gate", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains(
            "Live completion and dependency-ready frontier state are queried from GitHub;",
            dependencyPlan,
            StringComparison.Ordinal);
        Assert.DoesNotContain("| done |", dependencyPlan, StringComparison.Ordinal);
        Assert.DoesNotContain("open dependency-ready frontier", dependencyPlan, StringComparison.Ordinal);
        Assert.Contains(
            "Dependency depth is a topological planning aid, not a release number.",
            dependencyPlan,
            StringComparison.Ordinal);
    }

    /// <summary>Separates reviewed AB function availability from direct-golden and support-certification debt.</summary>
    [Fact]
    public void AbFunctionAvailabilityDoesNotHideGoldenDebt()
    {
        string matrix = ReadText("docs/architecture/supported-ic-matrix.md");

        Assert.Contains("## AB support, direct-golden debt, and release progress", matrix, StringComparison.Ordinal);
        Assert.Contains("A missing direct Golden remains an", matrix, StringComparison.Ordinal);
        Assert.Contains("**33.3%**; **4 missing**", matrix, StringComparison.Ordinal);
        Assert.Contains("NT51950 `Cascade`, and selector-free NT51951", matrix, StringComparison.Ordinal);
        Assert.Contains("**66.7%**", matrix, StringComparison.Ordinal);
        Assert.Contains("**100.0%**; external golden, firmware-owner, independent-review, packaging, and release-owner gates remain open.", matrix, StringComparison.Ordinal);
        Assert.Contains("**0.0%**; local support policy does not self-approve firmware, package, signing, or release-owner gates.", matrix, StringComparison.Ordinal);
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
        Assert.Contains("The `0.9.15` release scope exposes only the declared NT51919/NT51929/NT51932", specification, StringComparison.Ordinal);
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

        Assert.Equal(10, standardMergeIcIds.Length);
        Assert.Equal(standardMergeIcIds.Length, standardMergeIcIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"], abMergeIcIds);
        Assert.Contains("## Update rule", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2RegistrationRegistry.cs", reference, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2Bundle.cs", reference, StringComparison.Ordinal);
        Assert.Contains("package-trust-index.json", reference, StringComparison.Ordinal);
        Assert.Contains("runtime admission for Standard Merge", reference, StringComparison.Ordinal);
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

    /// <summary>Preserves historical planning evidence while routing current ownership to canonical documents.</summary>
    [Fact]
    public void HistoricalPlanningDocumentsPointToCanonicalRoadmapAndCurrentUiArchitecture()
    {
        string historicalPlan = ReadText("docs/architecture/0.7.0-refactor-and-evidence-plan.md");
        string onboardingRunbook = ReadText("docs/architecture/adding-ic-merge-replace-workflow.md");
        string demoPlan = ReadText("docs/ui/0.1.1-demo-interface-plan.md");

        Assert.Contains("Status: Historical", historicalPlan, StringComparison.Ordinal);
        Assert.Contains(
            "Active version allocation and completion status are maintained only in [`NFC Roadmap`](nfc_roadmap.md).",
            historicalPlan,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "That document is the active checklist",
            onboardingRunbook,
            StringComparison.Ordinal);
        Assert.Contains("retained historical checklist", onboardingRunbook, StringComparison.Ordinal);
        Assert.Contains("[`NFC Roadmap`](nfc_roadmap.md)", onboardingRunbook, StringComparison.Ordinal);

        Assert.Contains(
            "For the current information architecture, see [`information-architecture.md`](information-architecture.md).",
            demoPlan,
            StringComparison.Ordinal);
    }

}
