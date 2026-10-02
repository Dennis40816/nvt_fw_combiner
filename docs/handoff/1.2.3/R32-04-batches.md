# R32-04 named document-assertion batches

Status: 2026-10-02; decision 258 approved Batch 1 (implemented with this document); decision 263 approved
Batches 2 and 3, which are not implemented yet; Batch 4 waits for the owner at its merge.
Batches 2 to 4 wait for the owner. The Batch 2 rewrite exists as a proposal on the branch
`feature/1.2.3/r32-04-batch-2` and does not merge before the owner approves it.

Authority: [board decision 258](../1.2.x.md), [R32-04 and O22](../1.1.14/1.2.x-inventory.md),
and the [disposition tables, four questions, and limits](R32-04-disposition.md).
Source: `1f1be9bc7e0238a2cc7bc2e39f9026b051093855`; branch `feature/1.2.3/r32-04-batches`, from `1.2.x`.
Approval applies to the named assertion groups, never to a whole mixed method or an unlisted check.
Test paths below are relative to `tests/`; Architecture filenames are identified by topic and current file.

## Batch 1: changelog heading

In plain words: three checks that only pinned the exact words of one changelog heading are deleted. After this,
rewording that heading no longer fails a test, and a heading pasted twice would no longer be caught either.

| Test file (topic) and method | Assertion group | What it protects | Action | State |
| --- | --- | --- | --- | --- |
| `NvtFwCombiner.Architecture.Tests/HostInfrastructureBoundaryTests.StartupDiagnostics.cs`; `V0105PreloadBaselineAndLifecycleLedgerStayFrozen` via `AssertV0105PreloadBaselineIsFrozen` | `Assert.Equal(1, CountOccurrences(changelog, "#### Message Center, report, and System Information readability"));` | Wording only, including this heading's exactly-once guard | Deleted only this assertion | approved by decision 258 |
| Same file and method | `Assert.DoesNotContain("#### Message Center report and System Information readability", changelog, StringComparison.Ordinal);` | Wording only | Deleted only this assertion | approved by decision 258 |
| Same file and method | `Assert.DoesNotContain("#### Message Center and report readability", changelog, StringComparison.Ordinal);` | Wording only | Deleted only this assertion | approved by decision 258 |
| Same file and method; all other assertions in the helper | Five predecessor rows; 13 identities; six size rows and slice sum; gross/add/net amendments; eight ticket rows, anchors, cells and arithmetic; lifecycle/release strings and source checks | Frozen source/package identities and accounting; non-transferable amendments; bounded preload lifecycle; retained release-note fields and human gates; source ownership, cancellation, bounded I/O, worker/generation limits and metadata confirmation | Retain every other assertion byte-for-byte; no local or constant became unused | approved by decision 258 |

## Batch 2: label rewrites

In plain words: the CI test would watch the real flaky-test report being written, instead of looking for the word
"aggregate"; it expects exactly one recovered test, the probe test that failed once and passed on retry. For the
review instructions no rewrite is proposed: the review skill gives no fixed name to each duty, so a new check would
freeze wording again. The owner chose the rewrite for the first check and to keep the second as it is
(decision 263).

| Test file and method | Present assertion group | Duty and protected checks | Proposed assertion or exception | Action | State |
| --- | --- | --- | --- | --- | --- |
| `scripts/test_ci_dotnet_retry.py`; `CiDotnetRetryTests.test_aggregate_recomputes_and_reports_flaky_list` | `self.assertIn("aggregate", (root / "summary.md").read_text())` | Recompute the flaky list from per-attempt evidence, report every recovered identity with its failure evidence, and reject a manifest that erases a recovered failure | `report.assert_called_once()`; `self.assertEqual(expected_flaky, report.call_args[0][1])`; for each expected identity: `self.assertIn(f"{item['project']}: {identity}", summary)`, `self.assertIn(f"{item['project']} {identity} (attempt-1)", summary)`, `self.assertIn(f"attempt-1 failure for {identity}", summary)`; `self.assertIn("at Probe.Tests.Run()", summary)` | Proposed on `feature/1.2.3/r32-04-batch-2`, replacing only the label assertion and its setup | approved by decision 263; not yet implemented |
| Same file and method; other assertions | `self.assertIsNone(error)`; `coverage.assert_called_once()`; console checks for `Probe.Tests.Case1`, `bug record`, `GoldenRegression 3/3`, original failure message and stack, failed-test notice, exactly one warning and flaky-test warning; `self.assertRaisesRegex(RuntimeError, "flaky")` after erasing `manifest["flakyTests"]`; `coverage.assert_not_called()` | Successful evidence/coverage flow, Golden counters, original failure visibility, release zero-flaky annotations, recomputation and refusal to publish coverage after evidence disagreement | All existing protected assertions stay unchanged | Retain | retained |
| `scripts/test_skill_inventory_validation.py`; `RepositorySkillRoutingContractTests.test_code_review_uses_three_lenses_without_forced_subagents` | `self.assertIn("**Spec correctness**", text)`; `self.assertIn("**Runtime, safety, and architecture**", text)`; `self.assertIn("**Tests and evidence**", text)`; `self.assertIn("Spawn read-only subagents only when", text)` | All three duties: missing/incorrect/unrequested behavior; dependency direction, firmware authority, safety, compatibility/security/release impact; coverage, independent Golden evidence and remaining human evidence. Delegation is conditional on independent, read-heavy areas; ordinary reviews stay in one pass | Exception: leave all four assertions unchanged; there is no existing stable duty identifier, anchor or key in `.agents/skills/nfc-review/SKILL.md` or its `agents/openai.yaml`. Its ordinal list and bold headings are prose, not semantic duty keys | No rewrite implemented; retain the test and delegation condition | kept as it is by decision 263 |

The CI proposal uses `report_ci_flaky_tests` with `wraps`, so the real reporter still runs.
`expected_flaky` is the exact list containing `project.relative_path` and `Probe.Tests.Case1`.
The finalizer writes to a fresh `aggregate-summary.md`, so producer output cannot satisfy its summary checks.
The first attempt fails and the second passes; the retained tampering case requires recomputation from those attempts.
Only failed-attempt details belong in the failure report; passing retry details are not newly required.
No production reporting code, review instructions or stable duty key is changed or invented.

## Batch 3: historical wording

In plain words: delete three checks on old milestone names and five on old demo-plan bullet wording. Milestone
order, scope, the evidence and approval rules, and the IC rows stay checked.

| Test file (topic) and method | Assertion group, quoted | What it protects | Action | State |
| --- | --- | --- | --- | --- |
| `NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.Roadmap.cs`; `OwnerPriorityTargetsNormalMergeReplaceBeforeAb`; the milestone-title subgroup (`R32-04-disposition.md:68`) | `Assert.Equal("Normal Replace priority", replaceMilestone[1]);` | Wording only | Proposed deletion only; unchanged on this branch | approved by decision 263; not yet implemented |
| Same file and method | `Assert.Equal("Workflow data-model convergence", convergenceMilestone[1]);` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method | `Assert.Contains("AB merge", abMilestone[1], StringComparison.Ordinal);` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method; retained facts | `replaceLine < convergenceLine`; `convergenceLine < abLine`; scope checks `DP`, `CtrlRAM`, `IC num`, `combiner`, `unified` (ignore case), `Merge/Replace`, `No new byte behavior`, `owner reactivation` and `golden evidence` (ignore case); for each of `NT51950` and `NT51951`: `canonical V2 DP Perspective Standard Merge route`, `DP Perspective` (ignore case), `DP and CtrlRAM priority`, `DP`, `golden` (ignore case) | Historical sequencing/scope, evidence before new bytes, owner reactivation/Golden conditions, both IC route/evidence rows | Retain all checks and row lookups | retained |
| Same Roadmap file; `ReplacePlanningRequiresIcNumAndCombinerPostProcessing`; the demo-plan wording subgroup (`R32-04-disposition.md:71`) | `Assert.True(replaceBullets.Any(bullet => bullet.StartsWith("Shared Number selector", StringComparison.Ordinal)), "Replace content must use the shared Number context before region choices.");` | Wording only in the historical demo plan | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method | `Assert.Contains(replaceBullets, bullet => bullet.Contains("single", StringComparison.Ordinal) && bullet.Contains("cascade", StringComparison.Ordinal) && bullet.Contains("numeric", StringComparison.Ordinal));` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method | `string readinessBullet = Assert.Single(replaceBullets, bullet => bullet.StartsWith("Processor/tool readiness indicator", StringComparison.Ordinal));` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method | `Assert.Contains("combiner.exe", readinessBullet, StringComparison.Ordinal);` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method | `Assert.Contains("CRC/header", readinessBullet, StringComparison.Ordinal);` | Wording only | Proposed deletion only; unchanged | approved by decision 263; not yet implemented |
| Same file and method; retained facts | Planning-resource predicates: `Device context:` with `Number`, and `CRC/header` with `combiner.exe`; integrity-matrix checks `post-replace`, `combiner.exe`, `Combiner 1.13.0`, `NT51927`, and absence of `TPB` | Shared Number resource context and recorded post-replace CRC/header/tool facts; historical support-neutral evidence cannot authorize current admission | Retain both resource assertions, all five matrix assertions and their lookups | retained |

Neither historical subgroup is deleted here, and neither whole method is proposed for removal.
If the five demo-plan checks are deleted, the `replaceBullets` read in that method and the helper
`ReadMarkdownBullets` in `RepositoryBoundaryTestSupport.cs` (it has no other caller) become unused and go with them.

## Batch 4: moves into the structure lane

| Test file (topic) and method | Assertion group | What the structure check must protect before removal | Action | State |
| --- | --- | --- | --- | --- |
| `NvtFwCombiner.Architecture.Tests/RepositoryDocumentTests.RepositoryShape.cs`; `RepositoryTextFilesStayBelowEmergencyCeiling` | Complete scan and `Assert.Empty(oversizedFiles)` | Recursively scan existing `src`, `tests`, `docs`, `eng`; extensions `.cs`, `.axaml`, `.md`, `.targets` case-insensitively, including non-Markdown; exclude any `bin`/`obj` path segment case-insensitively; count physical lines; accept 2,500 and reject above 2,500; preserve relative-path diagnostics ordered by descending line count then ordinal path; skip absent roots | Describe a move into always-run structure checks in 1.2.3; current test, threshold and helpers stay until equivalent coverage and failure behavior are verified | waits for the owner at merge, R3 |
| `NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.Roadmap.cs`; `NfcRoadmapHasOneOrderedVersionAllocationEntryPoint` | All six checks together: README link; nonempty versions; unique versions; ascending order; VERSION membership; owner-unallocated heading | `README.md` contains the link `](docs/architecture/nfc_roadmap.md)` to the canonical roadmap; parse allocation rows only under `## Current release sequence` until the next level-two heading; retain version parsing and invalid-version failure; nonempty, unique, ascending versions containing trimmed `VERSION`; retain `## Explicit owner-unallocated queue` | Describe a move into always-run structure checks in 1.2.3; all current checks and semantic-input mappings stay until the complete move is verified | waits for the owner at merge, R3 |

Before removal, migration evidence must show both groups execute in the always-run structure lane,
including probes of the threshold, exclusions, missing/duplicate/out-of-order versions and VERSION membership.
The owner must review the verifier change at merge as governance-owner; coverage is required before prose skipping.
`scripts/validate_repository.py` currently checks local Markdown link targets and the exact generated skill inventory.
Those checks do not prove the line ceiling, roadmap section parsing, allocation order or current-version membership.
No verifier, validator, workflow, test selection, mapping or MOVE implementation is changed here.

## Not in any batch

Everything else in the disposition list stays as it is, including every KEEP and KEEP (unconfirmed) group.
Unlisted assertions in the named mixed methods remain untouched; the Batch 2 review exception remains unchanged.
This includes firmware facts/ranges, authority and permission rules, release rules, contracts, evidence integrity,
the ADR string-token sentence, implementation phase labels, and the existing generated-inventory comparison.
The disposition's exclusions and limits still apply. No prose skip or support/Golden promotion is authorized.
