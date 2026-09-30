# WS-MEMLAYOUT — decision 189

## Dispatch and admission

- Outcome: implement R09-02 first batch and declared DP sub-field folding,
  then passive legend / passive-close recovery / keyboard access, then tiny
  markers and fixed legend emphasis. Design A, the complete four-route matrix,
  card caching, firmware execution and native High Contrast are out of scope.
- Authority: owner-authorized edits and local commits only; R2. No push, PR,
  GitHub writes, fetch, rebase, release, profile or CI changes.
- Branch: `feature/1.1.15/memory-layout`; worktree `<worktrees>/f115-ml`;
  base `99e3efd7e06545a927f26a2f9da39fad65642ebf` from the records branch.
- Single writer: Codex `gpt-6-astra`, requested xhigh; no delegation. Commander
  owns integration and the planned independent Claude architecture/UI review.
- Mutable scope: Application MemoryLayout contracts/projector, their existing
  Presentation consumers and shared MemoryCoverage control/resources, affected
  tests, the interaction handoff, this log and bug ledger. Evidence remains
  outside Git under `evidence/1.1.15/`.
- Read first: decision 189; the interaction handoff's Passive legend section;
  revised analysis and review under `evidence/1.1.15/`; R09-01 from `origin/1.2.x`;
  development-execution-workflow and handoff protocol.
- Stop: required firmware/profile/release/CI changes; an unanswered material
  product decision; the same test still failing after two corrections; over
  four hours in a stage or ten hours total. Record the blocker before stopping.

### Capability-reuse gate

Owner search: `MemoryLayoutProjector` and its Sections, ContentSource,
LogicalCoverage and InputBindings partials publish accepted map/plan/session
facts through `MemoryLayoutSegment` and `MemoryLayoutSnapshot`.
`UiCompositionRunner.Common`, `ShellTextResources.MemoryLayout/DynamicText` and
`ReplaceRegionGroupBuilder` are the consumers with the second classifications
identified by R09-01. The bounded `rg`/source inspection confirmed these paths
on the base above; there is no CodeGraph index in this checkout.

Disposition: **extend-owner** for the six missing Application contracts and DP
section details; **reuse** their consumers and the shared control; **reject-duplicate**
for any Presentation classifier or separate projector. Canonical map role,
source attribution and workflow effect remain separate dimensions. No byte,
range, order, CRC/header, padding, naming, support or profile decision changes.

| Item | Deletion condition in this stage | Preserved invariant |
| --- | --- | --- |
| A-06 | Remove source-space-to-fill classification from Presentation; consume typed attribution. | Map content role remains source-neutral; color mapping remains UI. |
| A-07 | Three artifact-label consumers use one Application artifact identity. | Context-specific localized wording may differ. |
| A-08 | Action/detail decisions are published by Application. | Localized/Technical wrapper kinds stay in Presentation; postprocessing does not imply CRC. |
| A-09 | Remove initializer inference from text resources. | Protected blank regions are initialized; unadmitted reference, source-less writes and unknown initializer are not. |
| A-11 | Remove IsReferenceKept and UI group decisions. | Logical coverage remains; restored reference and kept bytes retain their distinction. |
| C-06 | Unresolved callers consume typed Application pending/readiness facts. | DP, General mapping, reference and CtrlRAM prerequisites remain distinct; UI flags do not decide readiness. |

Checks: affected Application MemoryLayout, UiSmoke MemoryCoverage/source/pending
and route tests; scoped architecture guards when their consumers change;
`python scripts/verify.py --structure-only`. Each test process loads the fixed
user test-area setting and uses its existing `temp` child. No `--all` run.
Red/green evidence, screenshots, measured opening cost and code-size changes
will be recorded per stage. Independent fixed-head review and owner visual
acceptance remain commander/owner gates; local verification is not integration.

### 2026-09-29 — stage 1 stopped at the retry limit

State: local; incomplete. This checkpoint commit contains only admission, the
checkpoint and bug records. Production and test edits remain an uncommitted
patch over `99e3efd7e`; no completed stage implementation commit exists.
Stages 2 and 3 have not started. No push, PR, fetch, rebase or GitHub write.

Evidence:

- Application `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj
  --no-restore --filter FullyQualifiedName~MemoryLayout`: **62 passed, 0 failed**;
  `evidence/1.1.15/test-results/stage1-app.trx`. This is the locally edited source,
  not a fixed-head or full-suite pass.
- Bootstrap filter
  `FullyQualifiedName~AbCtrlRamMemoryLayoutTests.BankCoverageUsesDeclaredRangesAndOriginalOperations`:
  red **0 passed, 3 failed**, then first correction **0 passed, 3 failed**.
  The next run added `|FullyQualifiedName~AbCtrlRamMemoryLayoutTests.StandardReferenceHasNoBankLocators`:
  **1 passed, 3 failed**. TRX files are `stage1-dp-red.trx`,
  `stage1-dp-green.trx` (filename only; it failed), and
  `stage1-dp-corrected-range.trx` under the same evidence directory.
- The final failure is the test's template ID expectation versus the canonical
  resolved `a-cmi-dp-version` identity. The preceding failure was its incorrect
  AB gap assumption: AB DP is `[0,0x7000)`; Standard has the separately tested
  `Unmapped` at `flash [0x6000,0x7000)`. No range/profile change was made.
- UiSmoke filter `FullyQualifiedName~MemoryCoverage|FullyQualifiedName~MemorySourcePresentation|FullyQualifiedName~MemoryPostprocessing`:
  **not executed**, build blocked by IDE0055 in the new DP-card regression.
- Before captures at the base: `ReleaseExampleScreenshots` selected by
  `LoadedInputsHaveClosedAndOpenDetailsEvidence` and DisplayName containing
  `nt51929-ab-t05-d06` or `51950-dp-256k`: **2 passed, 0 failed**;
  `before.trx`. Four PNGs under `evidence/1.1.15/screens/before/`.
- No after captures, opening-time measurement, structure-only run, architecture
  gate or independent review completed before the stop. No `--all` invocation.
- Restore-modified `packages.lock.json` files were restored. Every .NET test
  process loaded the user test-area root and set TEMP/TMP/TMPDIR to its existing
  `temp` child; no tests/verifiers were running at the checkpoint commit.
- Policy nonblank count of the unfinished patch: production **+150**, comprising
  non-UI runtime **+240** and Presentation **-90**; tests **+168**.
  `MergePresentationViewModel` **2158 -> 2160 (+2)** for the accepted-session
  argument; `ShellTextResources` **3226 -> 3227 (+1)** for typed localization.
  These exceed the ADR 0080 item 17 hotspot no-growth baseline and remain an
  integration gate; no baseline/approval policy was changed.
  Details: `evidence/1.1.15/stage1-code-size.json`.
- Complete retained source/test text: `evidence/1.1.15/stage1-working-source.json`,
  SHA-256 `692a3db8230491726a21fce05071d993a2a7f256020dc0fefc9f1ee81140e69b`.
  `git diff --check` passed. The source archive includes new untracked test and
  contract files; it is evidence for resumption, not a review verdict.

Open: **owner stop condition triggered: the same route test remained failing
after two corrections**. See
`BUG-20260929-memory-dp-regression-identity` and
`BUG-20260929-memory-dp-card-test-format`. The R09 migration and DP section patch
are unreviewed and incomplete; App green does not validate the UI adapters.
The remaining M1/M2/O5 bugs are still open. Required stage checks, interaction
contract updates, code-size disposition, independent Claude R2 review and owner
visual acceptance remain outstanding. Scoped Polytail: **FAIL / incomplete**,
because checks and mandatory review have not passed.

Next: commander/owner decides whether to resume this retained local patch;
first inspect the canonical resolved field identities and finish the new test's
formatting, then run affected gates before proceeding to stages 2 or 3.

### 2026-09-29 — stage 1 completed after authorized resumption

State: locally verified; this coherent commit contains the retained migration,
DP fields, corrected tests, bug resolutions and checkpoint. Base remains
99e3efd7e; prior stop checkpoint is 37c6c9586. Commander authorized both test
corrections and reset their retry counters. This turn's total limit is eight
hours; each stage retains its four-hour limit. Single writer, no delegation.

Decision 192 is supplied by the current owner instruction. Its named remote ref
is absent locally; no fetch was attempted. Projection uses declared DP owners,
including command fields, without IC-specific rules. NT51950/951 profile
declarations remain the separate R3 workstream's responsibility.

Evidence (all with user test-area TEMP/TMP/TMPDIR; no full-suite claim):

- Application `--filter FullyQualifiedName~MemoryLayout`: 62 passed, 0 failed;
  `evidence/1.1.15/test-results/stage1-resume-app.trx`.
- Bootstrap `--filter FullyQualifiedName~AbCtrlRamMemoryLayoutTests`: 12 passed,
  0 failed; `stage1-resume-bootstrap.trx` in that directory. Both resolved bank
  IDs and exact flash ranges are inside DP, not standalone slices. Standard
  independently retains `Unmapped [0x6000,0x7000)`.
- UiSmoke `--filter FullyQualifiedName~MemoryCoverage|FullyQualifiedName~MemorySourcePresentation|FullyQualifiedName~MemoryPostprocessing|FullyQualifiedName~DpPerspective`:
  final 191 passed, 0 failed; `stage1-final-ui.trx`. The first resumed run was
  190/1 because the style fixture omitted typed DisplayGroup.Base; its bug is
  resolved. Two exploratory size cleanups failed analyzers and were corrected.
- Architecture `--filter FullyQualifiedName~Memory|FullyQualifiedName~ReplaceRegionGroupsUseTypedGroupsAndStableSourceIdentity|FullyQualifiedName~ProjectDependencyTests`:
  15 passed, 0 failed; `stage1-resume-architecture-restored.trx`. Initial
  no-restore invocation lacked assets; locked restore supplied them unchanged.
- `python scripts/verify.py --structure-only`: PASS;
  `evidence/1.1.15/stage1-final-structure.log`. Earlier hotspot failures remain
  in the resumption evidence; no analyzer, baseline or policy was disabled.
- Nonblank code-size versus base: production +147, non-UI runtime +240,
  Presentation -93, tests +177. New typed contracts and invariant tests explain
  the growth. Existing hotspot aggregates are unchanged. Details:
  `evidence/1.1.15/stage1-final-code-size.json`.
- Scoped author review traced all six migration items to Application facts and
  their consumers; no firmware bytes/ranges/order/integrity/support, profile,
  release or CI change. DP projection retains exact declared field references.
  Unknown attribution remains explicit; source-less writes are not initialized.
  `git diff --check` passed. Tests are complete before commit.

Open: independent fixed-head Claude architecture/UI review and owner visual
acceptance remain with commander/owner. Scoped author Polytail has no open
correctness finding; overall R2 review remains incomplete pending that reviewer.
Before screenshots are retained; after screenshots and latency remain for
stages 2/3. No integration or publication performed.

Next: stage 2 red/green for passive legend, transparent-surface removal,
passive-close recovery and real keyboard traversal under decision 189.

Stage 2 admission: extend-owner `MemoryCoverageBar` for lifecycle and input,
`MemoryCoverageInteractionBehavior` / `MemoryCoverageInteractionState` for
rail-only emphasis, and its existing Legend partial for passive consumers.
Reuse shared templates and controlled close scheduler; reject workflow-specific
handlers, a selected-ID service or firmware projection on pointer movement.
The four-state reference remains the card/local hierarchy reference, superseded
only by the accepted legend changes. Decision 189 Q2 overrides the analysis's
earlier suggestion to retain legend emphasis while a card is open.
Red: `stage2-red.trx` has 5 failures (passive legend Light/Dark, same-slice reopen
Plain/non-Plain, extent-only close) and 1 pass (actual three-byte Tab/Shift+Tab
and Escape). M3 needs no production correction at this boundary.

### 2026-09-29 — stage 2 completed

State: locally verified; stage 1 is ce680fec536fea876c87991997985b6ef0b5478a.
This commit completes passive legend, transparent-surface removal, rail-only
reverse emphasis and one-shot passive-close recovery. Keyboard three-byte
navigation was already valid and has actual Tab/Shift+Tab/Enter/Escape coverage.
The canonical interaction handoff now names superseded historical behavior.

Evidence:

- UiSmoke `--filter FullyQualifiedName~MemoryCoverage`: **189 passed, 0 failed**;
  `evidence/1.1.15/test-results/stage2-final-ui.trx`. Includes shared Plain and
  non-Plain controls, Light/Dark, existing bilingual/geometry/teardown/wheel
  coverage, controlled 319/320 ms tall-legend transit, identical-label identity,
  group-to-leaf emphasis, and 200 same-target moves without card rebuilding.
- `stage2-red.trx`: 5 failed / 1 passed. `stage2-corridor-red.trx`: 1 failed.
  `stage2-green-initial.trx`: 7 passed. Wider regression's local-leaf emphasis
  defect and obsolete fixture assertions were corrected; all failures retained.
- `python scripts/verify.py --structure-only`: PASS;
  `evidence/1.1.15/stage2-structure.log`. No profile/release/CI change or `--all`.
- Opening measurement: English Light; Plain/non-Plain x 420/620 DIP x Reduced
  Motion on/off, 30 warm samples each plus first sample. Handler P50 26.88-85.42
  ms; P95 48.13-105.66 ms. Render-pump P95 51.40-109.33 ms. Raw samples under
  `evidence/1.1.15/timings-stage2/`. This does not measure native first-readable
  latency or claim improvement against a different source/environment. O4
  caching/reveal optimization remains excluded.
- Stage delta nonblank: production +44 (all Presentation), tests +355.
  `evidence/1.1.15/stage2-code-size.json`; existing hotspot baselines unchanged.
- Author review: activity uses display references, not labels/ranges; group
  membership feeds rail-only state while cards retain ordinary terminal state.
  Invalidations discard stale targets; the only rearm is valid passive closure.
  Popup spacers have no background/hit testing. Diff and affected links checked.

Open (corrected by the independent review): the selected filter was green;
affected consumers were not covered, and five P1 failures were later reproduced
at 8010d7770. This was not evidence of no local failing gate. Independent Claude fixed-head R2 review, owner
visual acceptance and native first-readable timing remain external. Scoped
Polytail author check has no open correctness finding; R2 review is incomplete
until commander's independent reviewer records it. Stage 3 marker and styling
acceptance is not claimed here; no integration/publication.

Next: O5 non-overlapping 24-DIP markers/collision lists and Q2 fixed border with
resource-backed background, with DPI/theme/language and screenshot evidence.

Stage 3 admission: extend-owner `MemoryCoverageBar` for display geometry and
its existing local-view/card lifecycle; reuse `WireSlice`, shared templates,
interaction leases and theme resources. Marker clustering is display-only:
each entry retains its original typed slice, with no range or ownership
inference. Reject minimum-width distortion of the proportional rail and a
second popup/input controller. `stage3-red.trx` reproduces six missing-marker
cases and four missing fixed-border cases (10 failed, 0 passed). A collection
expression formatting issue in the new fixture was corrected before this run.

### 2026-09-29 — stage 3 completed

State: locally verified; stage 2 is 20a66dd3b02aad8e178066c860936d0027534e8e.
This coherent commit completes visible 24-DIP tiny markers, collision lists
and fixed passive-legend borders. Product source is frozen before final gates;
this checkpoint is included with the implementation. No integration or publish.

Evidence:

- Final UiSmoke affected filter (`MemoryCoverage|MemorySourcePresentation|MemoryPostprocessing|DpPerspective`,
  each with `FullyQualifiedName~`) plus three canonical screenshot cases:
  **243 passed, 0 failed, 0 skipped**, 2m09s;
  `evidence/1.1.15/test-results/stage3-final-ui-captures.trx`.
  This includes all 24 combinations of 240/420 DIP, actual 100/150/200% headless
  render scaling, Light/Dark and English/Traditional Chinese; isolated and
  adjacent three-byte selections preserve exact slice identity/ranges.
- Red/green: `stage3-red.trx` 10 failed; initial implementation 7 passed / 3
  fixture failures, corrected to 10/0. Wider `stage3-ui.trx` 228 passed / 10
  obsolete geometry assertions, corrected in `stage3-geometry-green.trx` 10/0.
  Dense-list review reproduced card overlap in `stage3-dense.trx` (2 failed);
  `stage3-dense-final.trx` passes 27 cases after bounded height and input-origin
  fixes. No test remains failed after two corrections; no stop rule triggered.
- Dense lists reserve card space and scroll. Pointer scrolling discards stale
  card identity; keyboard bring-into-view retains its focused card. Collision
  entries reuse the shared pointer, focus, Escape, close and teardown owners.
  Proportional rail widths/weights are unchanged; the marker row is separate.
- Architecture affected guards: 15/0, `stage3-architecture.trx`; final
  structure-only result is recorded in `evidence/1.1.15/stage3-final-structure.log`.
  Application 62/0 and Bootstrap 12/0 from stage 1 are reused for their unchanged
  source. No full-suite, firmware execution or Golden parity claim.
- Canonical before/after: `screens/before/` and `screens/after/` under
  `evidence/1.1.15/`. Both Details states for NT51929 AB and NT51950 Standard;
  after also includes NT51929 AB CtrlRAM Candidate. Primary inspected canonical
  before/after, both Details states, passive borders, narrow 150/200% bilingual
  Light/Dark markers and dense-list captures. The AB CtrlRAM Evidence open
  status is preserved. The screenshot application version remains the branch's
  existing version; this task does not change release/version policy.
- Final opening measurement: 8 groups (Plain/non-Plain x 420/620 DIP x Reduced
  Motion on/off), English Light, one cold plus 30 warm samples each. Handler
  P50 **23.14-72.38 ms**, P95 **44.93-207.13 ms**; render-pump P95
  **46.12-230.79 ms**. The largest P95 is Plain/420/non-reduced. Raw samples:
  `evidence/1.1.15/timings-final/`. These are headless handler/render-pump costs,
  not native first-readable latency or proof of a performance change; O4 is
  still excluded. Stage-2 measurements remain separately retained.
- Stage delta nonblank: production **+138** (Presentation only), tests **+222**.
  Cumulative versus 99e3efd7e: production **+329**, non-UI runtime **+240**,
  Presentation **+89**, tests **+754**. Existing >=2000-line hotspots are
  unchanged. Growth implements the authorized typed contracts, shared marker
  geometry and independent input/geometry regressions; no baseline is raised.
  `stage3-code-size.json` and `final-code-size.json` under the evidence root.
- Scoped author review checked the admitted owners, no inferred firmware
  ownership, no second classification/popup lifecycle, unchanged ranges and
  fixture bytes, dynamic border resources, cleanup and observed input behavior.
  No open local correctness finding. All discovered bugs are recorded with
  resolutions. Diff/links checked; no profile, release, CI, lockfile or private
  payload change. No verifier/test runs during the commit.

Open: independent fixed-head Claude R2 architecture/UI review and owner visual
acceptance remain with commander/owner. Local author Polytail:
**PASS-WITH-HUMAN-GATE**; mandatory independent review is not claimed complete.
Native first-readable timing is unmeasured; High Contrast remains the separate
1.2.10 acceptance. Decision 192's NT51950/951 AB profile declarations remain the
separate R3 branch; this branch follows declared owners without special cases.
No push, PR, GitHub write, fetch, merge or rebase was performed. No explicit
prune/gc command was issued, but the first stage-3 commit (40966aa32) printed
Git's automatic-packing message and completed. This is an execution deviation
from the no-housekeeping constraint, not an authorized maintenance action.
Subsequent Git writes suppress it with command-local `gc.auto=0` and
`maintenance.auto=false`; persistent configuration is unchanged. See
`BUG-20260929-memory-commit-auto-maintenance` in the bug ledger. This amendment
changes only the execution record; the verified product/test source is intact.

Next: commander reviews this fixed diff and screenshot evidence, arranges the
independent review and integration. The authorized local three-stage scope is
complete; O1/O2/O4 and unrelated backlog work are not started.

### 2026-09-29 — independent-review correction admission

State: local. Starting head `8010d7770282a70f9f58bfc44cf87d22523be971`,
integration base `aba286bae`; clean checkout on the existing task branch.
Owner authorizes the review corrections, full Release project tests and one
local commit. Codex gpt-6-astra, requested xhigh, remains the sole writer.
No delegation, push, PR, fetch, rebase, profile, firmware, release or CI edits.
This turn has a four-hour limit and a two-correction retry limit per test.

Owner search: `MemoryLayoutProjector.ProjectPending` owns C-06 decisions;
`AuthoringInputSlotStatus`, session lifecycle, unstable-file batch results and
`CompositionIssue` own the missing facts. Extend that typed input/output, retain
original inspection/issue facts in `FirmwareSlotViewModel`, and localize the
result only. Standard Merge required address spaces remain Application-owned.
`MemoryLayoutSnapshot` owns A-11 group publication; move group membership there
and reject inconsistent consumer facts explicitly. `ProjectMapSections` owns
section identity through declared parent/owner; reuse its checked bank placement.
Reject a second Presentation classifier. Existing shared rail/legend controls
and style contracts own the remaining corrections. No execution facts change.

Risk: R2, existing authorized interaction/Application contract. Mutable scope
extends the prior admission to the typed slot fact carrier and its inspection
projection. Final gate: complete UiSmoke, Application, Bootstrap, Architecture
projects in Release, structure-only, affected screenshots, diff/Polytail review.
Independent exact-head review and visual acceptance remain commander/owner gates.

Evidence: `review-p1-red.trx` in `evidence/1.1.15/test-results/` records Release
**0 passed / 5 failed**, one existing regression per P1. This also supersedes the
stage-3 claim of no open local correctness finding; its filter did not include
all affected consumers. All five failures must be corrected before completion.

Open: corrections and full gates in progress. P3-6 remains visual acceptance,
not authority to redesign labels or address wrapping. The joint NT51950/951
profile-branch scenario cannot be certified before commander integration.
Next: typed C-06 correction, then A-11, rail/style, declared-parent and lifecycle
regressions; record base comparisons for any failing gate.

### 2026-09-29 — independent-review corrections completed locally

State: locally implemented and verified, with source/tests frozen before the
final complete Release project gates. This checkpoint belongs to the local
correction commit following `8010d7770`; no integration or publication is
claimed. No push, PR, GitHub write, fetch, prune/gc or rebase was performed.

Review dispositions:

- P1-1 / C-06: Application publishes typed blocking diagnostics from original
  inspection status, availability failure, authoring issues and session
  lifecycle. A session-only error receives a localized diagnostic fallback;
  toggling linked AB TP no longer throws on an empty UI diagnostic string.
- P1-2 / C-06: stale content, rejected publication, incomplete inspection,
  authoring issues and non-terminal blocked readiness retain their typed
  reasons. Application selects the blocked DP before another missing required
  input; localization restores `DP BIN needs attention` and its original
  detail. Path changes/reset clear the carried facts. A retained non-terminal
  status uses the existing prerequisite error card, not terminal formatting.
- P1-3 / A-11: missing display groups are explicitly absent and render in the
  neutral Common group. Conflicting or partly missing facts for one logical
  identity fail with an explicit inconsistency message. Fixtures publish their
  declared Common/Slave groups; Application tests cover mixed-to-Common.
- P1-4: the CtrlRAM window regression rejects legend focus, focuses the actual
  corresponding rail and checks its exact-range card.
- P1-5: marker/collision outlines use the required 2-DIP width and existing
  dynamic resources; the complete style-contract tests run in UiSmoke.
- P2-1: the stage-2/3 assertion of no local failing gate is corrected above.
  The earlier selected filters missed affected consumers; this correction runs
  all four requested test projects, without filters.
- P2-2: field folding follows a declared DP code/image ancestor or DP code
  sibling with the same declared parent. Canonical identity remains code/image;
  adjacent distinct parents remain distinct. Only typed metadata field kinds
  are listed, excluding large Data partitions. Tests cover field-before-code,
  DP parent beside TP, nested field ownership, distinct adjacent parents and
  orphan-command neutral context. Existing NT51929 AB tests retain both exact
  bank ranges and Standard Unmapped coverage. No profile or byte contract changes.
- P3-1: Standard Merge uses the already applied Application required-address-
  space publication, including a query-safe empty catalog. P3-2 adds six
  passive-close reset/detach/disable cases across Plain/non-Plain controls.
- P3-3: Application snapshot publishes one immutable display-group lookup per
  logical identity; Presentation consumes it without per-segment classification.
  P3-4 explicitly supersedes connector pointer retention in the interaction note.
- P3-5: added the actual base CtrlRAM route screenshot and an after DP card
  with expanded `a-cmi-dp-version` / `flash [0x401A,0x401D)`.
- P3-6: unchanged, as the review assigns raw region labels and narrow address
  wrapping to owner visual acceptance. No approved alternative is available;
  this is not permission to introduce a new labeling/layout contract.

Red/green and base comparison (all artifacts under
`evidence/1.1.15/test-results/`):

- `review-p1-red.trx`: **0/5**, one existing regression per P1 at `8010d7770`.
  `review-p1-corrected.trx`: **13/0**, including group/lifecycle regressions.
  The first correction left stale-publication failure (6/1); adding typed facts
  to both publication/finally paths made the second correction pass.
- Parent regressions: `review-parent-red.trx` **4/4** contains three intended
  failures and one invalid equal-bound child fixture. Correcting only that
  fixture produces `review-parent-fixture-red.trx` **0/1** for distinct parents.
  A later nested-owner regression fails in `review-nested-fixture-red.trx`
  **0/1**, after correcting its missing child partitions. The final complete
  MemoryLayout filter is **72/0** in `review-parent-final-green.trx`.
- Retaining non-terminal status revealed a terminal-formatter exception in
  both languages: `review-nonterminal-red.trx` **0/2**, corrected in
  `review-followup-green.trx`. Build-only analyzer/import mistakes executed no
  tests; their logs and the invalid fixtures are retained in the bug ledger.
- First complete correction run: Application **1653/0**, UiSmoke **1915/6**,
  Bootstrap **2142/0**, Architecture **277/0** (`review-full-*.trx`). Five UI
  failures came from querying requirements during empty-catalog publication;
  one fixture omitted typed Slave groups. Reusing the existing applied
  requirements and declaring the fixture groups corrected all six.
- Detached base `aba286bae`: complete Release UiSmoke **1861/0/0** in
  `review-base-ui.trx`; the original five P1 tests and those six existing
  consumer tests pass there. These failures are change-related, not pre-existing.
  `review-base-comparison.json` matches all 11 failures to base passes by name.
  New regression/fixture failures have no identical test at base and are
  recorded separately above, not claimed as base passes. No known failing
  base gate is being excused. The temporary worktree was removed after comparison,
  after restoring its restore-generated lockfile changes and verifying the
  exact authorized target and clean status. The task worktree has no lockfile delta.
- `review-followup-green.trx` **15/1** passed the consumer fixes but exposed
  the new screenshot fixture selecting the overview container as a coverage
  control. Selecting its descendant yields `review-card-final.trx` **1/0**.
  No test remains failed after two corrections; the stopping rule did not fire.

Final complete project gates: Application **1654 passed / 0 failed / 0 skipped**;
UiSmoke **1923 / 0 / 0**; Bootstrap **2142 / 0 / 0**; Architecture **277 / 0 / 0**.
Each project uses
`dotnet test tests/NvtFwCombiner.<Project>.Tests/NvtFwCombiner.<Project>.Tests.csproj -c Release --no-restore`,
with a TRX logger/results directory and no filter (`review-final-*.trx` / `.log`).
Every test shell loads user-level `NFC_TEST_AREA_ROOT`, assigns its existing
`temp` child to `TEMP`, `TMP`, `TMPDIR`, and sets `DOTNET_CLI_UI_LANGUAGE=en`.
`python scripts/verify.py --structure-only` **PASS** (31.6 s), recorded in
`evidence/1.1.15/review-final-structure.log`, uses the same environment setup.
This is a structure lane and four complete projects, not `verify.py --all`,
CI, release approval or a new Golden parity claim. Final TRX counters are also
retained in `review-final-gates.json`; all 5996 tests passed without skips.

Screenshot evidence under `evidence/1.1.15/screens/`:

- `review-before/nt51929-ab-ctrlram-candidate-details-open.png` comes from the
  detached base; `review-after/` contains fresh canonical Details-closed/open,
  CtrlRAM DP card and marker/collision captures from the correction source.
- The primary visually compared the base Context slice with the after DP
  section and inspected the readable DP field card, 150% tiny markers and
  200% dark collision list. The initial translucent card image is retained as
  `nt51929-ab-ctrlram-candidate-dp-card-initial-transition.png`; it is obsolete
  evidence and must not be used for acceptance. Final capture settles the
  existing ReducedMotion/details reveal before saving. Owner acceptance remains
  open; screenshots do not establish Golden byte parity or native timing.

Size and author review:

- Relative to `8010d7770`, nonblank production **+81** (non-UI runtime **+53**,
  Presentation **+28**), tests **+288**; `review-code-size.json`. The projector
  grows 1777 to 1823 lines. All existing >=2000-line aggregates stay unchanged,
  including ShellTextResources 3226, MergePresentationViewModel 2158 and
  ReplacePresentationViewModel 2208; no size baseline or approval is altered.
- `review-final-source-sha256.json` records the frozen changed source/test
  bytes. Scoped author review checks typed diagnostic precedence and freshness,
  immutable group publication, explicit inconsistency failure, declared parent
  identity, both-bank placement, neutral gaps, passive invalidation, style
  resources and card/rail input. One Application classification path remains.
- Local authority classification records 16 R0 and 23 R1 paths, no required
  path roles and no unclassified paths (`review-authority-classification.json`).
  The semantic risk stays R2; path floors do not replace the contract review.
- R2 local author Polytail is limited to this correction diff; the independent
  report reviewed `8010d7770`, not the corrected head. A final `PASS` or an
  integration-ready claim is withheld until independent exact-head review and
  owner visual acceptance. This is not a permanent gate exemption.
- Local author verdict: **PASS-WITH-HUMAN-GATE**. No open local P0/P1 or failed
  requested gate remains. Diff/affected links and added-content privacy checks
  pass; no profile, firmware, release, CI, generated payload or lockfile is
  included. Frozen source/test hashes were rechecked after the complete tests.
  Git writes suppress automatic maintenance with command-local `gc.auto=0`
  and `maintenance.auto=false`; no test/verifier runs during the commit.

Open: independent exact-head R2 review and owner visual acceptance, including
P3-6. The separate NT51950/951 profile branch is not part of this worktree;
synthetic declared-parent/Data regressions cover its shape, but the real joint
AB CtrlRAM scenario remains commander's integration check. During transition,
an orphan command without a declared DP code/image owner remains neutral
Context. Native first-readable timing and High Contrast remain their existing
separate acceptance work; no new work is started here.

### 2026-09-30 — owner visual review corrections (uncommitted)

Owner review of the running build found four display problems; all four are
corrected locally in Presentation and UiSmoke tests only. No profile, Domain,
Application, firmware, release or CI file changed.

- Legend highlight is background only (decision 196, amending decision 189 Q2):
  `Border.memoryPassiveLegend` keeps a transparent 2 DIP border for layout
  size, and `.railActive` sets only `NfcMemoryInteractionSurfaceBrush`. The
  popup card is now a rail owner, so its legend row stays lit while the card is
  hovered or focused, and only that row. The non-colour cue is lost until the
  High Contrast work (1.2.10).
- Connectors were missing after `20a66dd3b` removed the `crossesOverview`
  block (earlier `0e6e6f6be` had hidden stems across the interactive legend).
  `OpenCard` again computes `crossesOverview` and `OverviewObstacles()`; the
  open local view (`OpenLocal`) uses `CardConnector` (`MemoryLocalConnector`)
  when the legend is shown and the view opens downward. Stems cross the legend
  and break behind labels; `LegendMarkers()` and `FooterObstacles()` supply
  the obstacle rectangles.
- Start address and legend title are left aligned.
- The "•" lane beside CtrlRAM on NT51950 1 IC is the DIFF CtrlRAM region
  (`diff-ctrlram`, role DiffDLM, `DiffDLM.bin`) at `0x33200` (0x1400 bytes,
  `explicit-range`, flash-map visibility `multi-chip-only`) with a neutral
  position label. Correction of the earlier "it is not a Slave" note, which
  reasoned only from the Master/Slave group names (those exist only outside the
  cascade branch): NT51950 has no separate Slave `nf/normal/vn` regions (the
  cascade branch groups them as Common); the only multi-chip-specific region is
  this DiffDLM one. Its policy `nt51950-nt51951-preserve-active-diffnf`
  (`profiles/built-in/ctrlram-postbuild-v2/catalog.json`) is exact-2-IC, active
  records = IC count - 1 (one record for the one Slave; inferred from the count
  offset, not declared as "Slave"), record `[0x33200,0x34600)`: `DiffDLM.bin`
  writes `[0x33200,0x33B10)` (2320 B) and `[0x33B10,0x34600)` (2800 B) keeps the
  reference bytes. Labelling it as Slave data or hiding it was owner question Q4;
  decided by decision 197 (single IC hides it, only 2 IC cascade says "Slave DIFF
  CtrlRAM").

Verification (local, uncommitted, source = this worktree at `9c946e457` plus
the changes above): narrow memory-popup/CtrlRAM/AB tests 163/163; wider
`NvtFwCombiner.UiSmoke.Tests` 253/253. Screenshots `review-after2` in the test
area; gallery `acceptance-1115.html`.

Findings recorded, not fixed here:

- AB Merge DP card shows no CMI row. `MemoryLayoutProjector.ProjectSections`
  returns no sections outside `CtrlRamReplace`, so `GetMemoryDisplay` facts
  carry only Region ID and Operation. Listing the CMI needs a declared
  AB Merge layout context (route/profile contract, R3) - proposed for 1.2.x.
- AB Merge rail legend has two rows both named "DP AB" (0x00000-0x06FFF and
  0x40000-0x46FFF); decided by decision 197 (ordinals "#1"/"#2", no bank suffix).
- P3-6 (raw `a-cmi-dp-version` label versus a readable name) still open.

Open: owner visual acceptance, the decisions above, independent exact-head R2
review, commit/push/PR (not authorized yet).

### 2026-09-30 — owner answers (decision 197) and true-range connectors (uncommitted)

Owner answers after the `review-after2` build, recorded in decision 197
(`docs/handoff/1.1.12.md`). All work below is local and uncommitted; no commit, push or PR
is authorized yet.

- Connector origin (implemented). The owner observed that the black stem always started at the
  24x24 minimum marker (or the position row) under the rail, even for a tiny slice. It now
  starts at the slice's true range on the flash bar. `MemoryCoverageBar.Markers.cs` stores the
  true range centre of each tiny marker in the attached property `RangeCenter` (a collision
  group uses the midpoint of its first and last slice); `RangeCenterX` reads it, so a handle
  clamped inside the rail still points at its real address. `OpenCard` and `OpenLocal` start
  the stem at the bottom edge of the flash track and cross the overview rows behind text
  obstacles (`OverviewObstacles`, `FooterObstacles`). A popup that opens above rests above the
  track top, not above the marker row. The 24 DIP handle is unchanged as the pointer target
  and minimum visible mark.
  Tests: `MemoryCoveragePopupTests.TrueRange.cs`, 8 cases (a focus-lane stem, a single tiny
  marker, a collision list and clamped edge markers, at 240 and 420 or 620 DIP). A first
  mutation check (reverting the range centre) was too weak; the edge-marker case was added and
  then failed under the same mutation. The narrow `Memory|CtrlRam` UiSmoke run passes 504/504
  (local, source = this worktree at `9c946e457` plus the uncommitted changes).
- Single IC has no "•" lane (implemented, local). The lane was an unbound Base-group CtrlRAM
  range, the NT51950/NT51951 `diff-ctrlram` region, which single IC never binds.
  `MemoryFocusLaneViewModel.Create` now skips Base-group CtrlRAM ranges when `isSingleIc`; the
  slice stays on the flash bar as context. Tests: `MemoryFocusLaneTopologyTests` (theory:
  single IC yields `["Master"]`, cascade yields `["Common", "•"]`) and
  `CtrlRamOverviewCompletionTests` (the 950/951 single-IC golden has no "•" lane and still keeps
  the Base CtrlRAM slice in `ReplaceCoverageSegments`). Both failed before the change (RED).
  The per-topology region-set split is R3 and belongs to `1.2.x`.
- "Slave DIFF CtrlRAM" is the label for 2 IC cascade only; nothing new to implement beyond the
  lane filter.
- Legend ordinals (implemented, local). `MemoryCoverageBarProjection.CoalesceContent` first
  resets every input slice to ordinal 0 (numbering is display state on shared slice
  view-models, so a rebuild must be idempotent), coalesces adjacent same-artifact ranges, then
  `NumberRepeatedTitles` gives rows that share one title "#1", "#2" in rail order.
  `MemoryCoverageSegmentViewModel.CanNumberRepeatedTitle` limits it to primary content that is
  not CtrlRAM, not a kept pattern and whose title still equals its source label (a captioned
  row such as "Unmapped" or "Reserved" is never numbered); `SetDisplayOrdinal` rewrites
  `DisplayTitle` and `AccessibleDetail` together. A coalesced run counts as one row.
  Tests: three facts in `MemoryCoverageContentGroupingTests` (physical order with singletons
  plain, runs counted once, exclusions and rerun reset).
- Consequence to disclose: the rule is uniform, so the CtrlRAM overview bar of NT51950/51
  single IC, which lists two DP sections, now also reads "DP #1" and "DP #2" (NT51919 has one
  DP section and stays "DP"). The existing assertion in
  `CtrlRamOverviewCompletionTests` (`Assert.Equal("DP", section.DisplayTitle)`) was changed to
  expect the ordinals; nothing else in the suite changed its expectation.

Verification (local, uncommitted): targeted 18/18 (`MemoryFocusLaneTopologyTests`,
`MemoryCoverageContentGroupingTests`, `FullFlashInputsShowTpAndDpContext`); wider narrow run
`Memory|CtrlRam|Legend|AbMerge|AbDp` on UiSmoke 593/593. Source = this worktree at `9c946e457`
plus the uncommitted changes.

Wider run (local, same source): the whole UiSmoke project 1936/1937; the one failure is the
known navigation focus flake `NavigationFocusIndicatorTests.UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling`
(`BUG-20260929-nav-focus-underline-gap-flake`), unrelated to this work.

## 2026-09-30 — owner review round 2 (legend hover, outer addresses, CtrlRAM stem origin)

The owner reviewed the rebuilt examples over a remote-desktop session and reported four items.

- Not every region lit its legend row on hover (fixed, local). Evidence: a scratch headless
  probe hovered every rail target of four Golden screens and read the legend rows. On AB
  Merge only coalesced runs lit (NT51929: 0 of 4 slices and 0 of 2 tiny markers; NT51950: 2
  of 5); CtrlRAM Replace lit every slice. The hovered slice's state became rail-active and
  raised `PropertyChanged`, yet the row stayed dark with a direct binding and with a strong
  subscription alike, so the row was watching a different state object. Cause:
  `MergePresentationViewModel.Memory.cs` published `MergeCoverageSegments` first, which
  rebuilds the bar and binds each legend row to the slice's current `Interaction`, and only
  then called `ReplaceRegionGroupBuilder.CreateLogicalItems`, whose
  `MemoryCoverageLogicalItemViewModel` constructor replaces every slice's `Interaction`.
  The rail resolves the state at hover time, so it wrote the new object while the row
  watched the discarded one. A coalesced run is created inside the bar and never passes
  through a logical item, which is why only runs worked. Fix: create the logical items
  before publishing the slices. Test first: `MemoryLegendRailHighlightTests` hovers every AB
  rail slice of the NT51929 and NT51950 Golden and requires exactly its own row lit; both
  cases failed before the change and pass after. Record:
  `BUG-20260930-merge-legend-highlight-stale-state`. The same publish-then-reassign order
  exists in `ReplacePresentationViewModel.ApplyReplaceMemoryDisplay` (and the bank view
  builds logical items a second time); it is not user visible today because that bar is
  hidden whenever CtrlRAM lanes exist, so it is recorded separately and left open:
  `BUG-20260930-replace-coverage-state-reassigned-after-publish`.
- Hovering a CtrlRAM lane label ("Master", "Common", "Cascade") lights no legend row: the
  overview legend has rows only for DP and TP FW, and the lane's CtrlRAM ranges live inside
  TP FW. Whether the containing row should light is an owner decision (open).
- Outer start/end addresses appear only on the CtrlRAM flash overview. Cause: only
  `MainWindowWorkflowTemplates.axaml` line 380 binds `StartAddress`/`EndAddress`
  (`CtrlRamStartAddress`/`CtrlRamEndAddress`); the Replace flash bar (line 386) and the Merge
  bar (`MainWindowSharedTemplates.axaml` line 507) bind neither. Merge shows the whole range in
  a text box above the bar instead (`MergeMemoryRangeLabel`, for example
  "0x00000-0x7FFFF (len 0x80000)"). Record: `BUG-20260930-memory-outer-addresses-only-ctrlram`
  (open; the owner decides whether every bar shows them).
- A seam at the DP #1 / TP FW boundary seen over remote desktop is a transport artefact, not
  the app: a local capture of the same window shows every one of the 34 rail rows switching
  from (37,99,235) to (22,163,74) with no intermediate pixel, while the remote image is
  compressed and scaled (text blurred the same way).
- CtrlRAM lane stems (owner amendment to decision 197, implemented, local): a lane's local
  view again leaves from its own label row. `MemoryCoverageBar.OpenLocal` uses the position
  row as the stem origin for a lane, places the popup against it, starts the stem at the
  label centre (`LabelCenterX`) and interrupts it behind every overview glyph
  (`OverviewObstacles`; the footer-only helper was removed as unused). Tiny markers,
  collision lists and the "⋮" group keep the true-range origin. Test first:
  `LaneStemStartsBelowItsPositionLabel` (240 and 620 DIP) replaced
  `LaneStemStartsAtTheTrueRangeOnTheFlashBar`; it failed before the change. A Golden render
  of NT51950 single and NT51951 cascade shows the stem leaving below "Master" and "Common".

Verification (local, uncommitted): `MemoryLegendRailHighlightTests` 2/2 and the whole
`MemoryCoveragePopupTests` class 162/162.

Owner answers (2026-09-30, all recommendations accepted): a lane label lights the legend
row of its containing section; every bar shows outer start/end addresses and Merge keeps its
range box; the Replace publish-then-reassign path is fixed in `1.1.15`; after these, sync
`1.1.x`, commit, push and open the R2 pull request (authorized). Visual acceptance of the
round-2 fixes was given with that authorization.

Implementation of the answers (local, uncommitted):

- Lane label lights its containing section. A new attached property
  `MemoryCoverageInteractionBehavior.RailContext` lists states that take rail emphasis only;
  `ResolveStates` appends them. `MemoryCoverageBar` sets it on each lane position to the
  states of the primary display slices the lane overlaps in its address space
  (`ContainingStates`), and on the local view (now rail-enabled like the card) while a lane's
  view is open, so the row stays lit inside the view. Test first:
  `HoveringACtrlRamLaneLightsItsContainingSectionRow` (NT51950 single Golden: hovering
  "Master" lights exactly the TP FW row, it stays lit inside the local view and clears on
  exit); RED before, GREEN after.
- Outer addresses on every bar. `MemoryCoverageBarProjection.OuterAddresses` is the one
  owner: the lowest start and highest end of the ranged slices of a single address space,
  empty otherwise. `CtrlRamStartAddress`/`CtrlRamEndAddress` now use it, and the new
  `ReplaceStartAddress`/`ReplaceEndAddress` and `MergeStartAddress`/`MergeEndAddress` bind the
  Replace flash bar and the Merge bar; the Merge range box stays. Tests:
  `MemoryOuterAddressTests` (NT51929 AB bar shows 0x00000 and 0x7FFFF with its range box;
  NT51950 single Replace flash bar carries the CtrlRAM overview's addresses). A stub-first RED
  run was not possible because the analyzers reject constant properties (CA1822); the
  pre-change evidence is the missing template bindings.
- Replace publish order and idempotent logical items. `ApplyReplaceMemoryDisplay` now builds
  the coverage groups (`RefreshReplaceCoverageGroups(segments)`) before publishing
  `ReplaceCoverageSegments` and `CtrlRamOverview`, and the `MemoryCoverageLogicalItemViewModel`
  constructor keeps a run head's existing state instead of creating one, so building items
  again (the bank view) never strands a bound observer. Tests:
  `MemoryCoverageStatePublicationTests` (logical-item idempotence; Replace and Merge Golden
  launches keep every published slice's state). The Replace case (15 of 19 slices changed
  state after publication) and the idempotence case were RED before; Merge was already GREEN
  after the round-2 fix.

Verification (local, before the `1.1.x` sync): narrow UiSmoke selection
`Memory|CtrlRam|Legend|Merge|Replace|AbDp|AbDummy` 798/798.

After merging `1.1.x` (#489, #490, #491; no conflicts): `verify.py --structure-only` PASS once the two
measured hotspot baselines were raised (`MergePresentationViewModel` 2158 to 2162,
`ReplacePresentationViewModel` 2208 to 2211; owner approval in the pull request). The narrow selection
plus `NavigationFocusIndicatorTests` ran 807/809: both failures are the open navigation focus flake
(`BUG-20260929-nav-focus-underline-gap-flake`, missing-ring frame, class alone 3x 13/13), not this work.

## 2026-09-30 — pull request #492 and its independent review

Pull request #492 (`feature/1.1.15/memory-layout` into `1.1.x`, R2, no roles) opened at head `ff99c5940`.
Independent review by a fresh Claude Opus 5.5 session (same runtime as part of the implementation) at that
exact head: `accept-with-changes`, no P0/P1/P2, four P3 findings, `state=complete`; its own narrow runs:
UiSmoke memory classes 188/188 and `MemoryLayoutProjectorTests` 72/72; it measured the two hotspot baselines
exactly (2158 to 2162, 2208 to 2211).

- P3-1 fixed: a cleared Replace display kept its outer addresses because only
  `ApplyReplaceMemoryDisplay` raised them. Their notifications now sit with the CtrlRAM ones in
  `NotifyCoverageGroupingChanged`, which every publish (including the clear path) runs; the
  extended `ReplaceFlashBarCarriesOuterAddresses` failed before and passes after.
- P3-2 fixed: primary Unmapped and Reserved overview sections whose title equals their source label
  could take ordinals; `CanNumberRepeatedTitle` now excludes them by role
  (`OrdinalsSkipPrimaryUnmappedAndReservedSections`, RED before). Neutral "Context" sections stay
  numberable, as decision 197 does not exclude them. Known limit: numbering is display state on
  shared slices, so a row bound before the bar's first rebuild keeps the plain title until rebind.
- P3-3 fixed: records said "local, uncommitted"; decision 197, the interaction contract (the lane's
  local view keeps the containing row lit) and the bug records now name pull request #492.
- P3-4 accepted as follow-ups (no production risk found): an integration test for the bank-view
  second construction, a Merge-reorder test independent of the run-head rule, above-placement stem
  tests for markers and lanes, and keyboard focus lighting a legend row.

Delta review of `ff99c5940..468f24842` (fresh Claude Sonnet 5 session): accept, openP0P1 0; exact-head
record posted as review 5361717605.

CI on `468f24842` failed `python / repository policy (repository-scripts-s-z)` (and the aggregate
`python-worker / verify`): `test_uismoke_writers_outside_the_session_are_isolated_per_compiled_type` found the
unlisted variable `NFC_MEMORY_TIMING_OUTPUT_DIR`, added by stage commit `20a66dd3b` in
`MemoryCoveragePopupTests.Measurements.cs`. The opening-time measurement now writes to the listed caller
override `NFC_VISUAL_OUTPUT_DIR` (same opt-in evidence directory semantics); `verify.py` is unchanged. The
policy test passes locally and the popup class stays 162/162.

CI on `60843aaec` then passed every shard, but the aggregate `dotnet / build-test` failed the decision 193
gate: two tests passed only on the retry. `NavigationFocusIndicatorTests.UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling`
has its open record and a hardening in pull request #493. `MemoryCoveragePopupTests.CardWheelDoesNotScrollTheAncestorPage`
(`direction: 0`) lost its card after shrinking it under the pointer: the passive close grace elapsed on the loaded
runner. An amplified local run (500 ms wait after the shrink) reproduced the CI assertion; moving the pointer back
onto the card fixes the test (`BUG-20260930-memory-card-wheel-shrink-close-flake`). No production change.

Open: exact-head record for the new head, CI, owner approval (including the code-size baseline raise).
