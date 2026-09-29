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

Open: no local failing gate. Independent Claude fixed-head R2 review, owner
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
