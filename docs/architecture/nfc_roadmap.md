# NFC Roadmap

Status: active owner roadmap; release-closure checkpoint 2026-09-01; subsequent owner allocation amendments are recorded below.

Current evidence checkpoint, 2026-09-26: **`v1.1.12` is published** at
`30b17e699bde29fc3ec33b3730e7408600ddc5c3`; see the
[release closure](../handoff/1.1.12.md#released-v1112--2026-09-26). `1.1.13`
is in progress: the
[2026-09-26 owner allocation](#owner-allocation-after-the-1112-release--2026-09-26)
and the table below own its allocation, the [1.1.13 board](../handoff/1.1.13.md)
its detail.

History: on 2026-09-26 the published-release sections `0.10.0` through `1.1.5`
and the superseded 2026-09-19 evidence checkpoint moved verbatim to the
[roadmap history](nfc_roadmap-history.md);
[Moved release history](#moved-release-history) keeps their linked headings.
Earlier dated checkpoints below remain history, not open release gates.

## Current release sequence — 2026-09-14

### Owner allocation after the 1.1.12 release — 2026-09-26

`v1.1.12` was published on 2026-09-26 from `main` `30b17e699` (release pull
request #456, release run `36217032510`). `1.1.13` is in progress on the
`1.1.x` trunk. This amendment records the owner decisions numbered in the
[1.1.12 board](../handoff/1.1.12.md#owner-decisions-2026-09-25); the
[1.1.13 board](../handoff/1.1.13.md) owns waves, order, state and evidence.

- **1.1.13** keeps the items moved by the
  [1.1.12 release-scope disposition](#owner-release-scope-for-1112--2026-09-26)
  first, including TP SVN modeling. It also carries the 1.1.12 follow-ups
  merged into `1.1.x` by pull request #457 (CLI report receipt after a failed
  `--report` write, F07 residuals); the CI core-shard follow-ups (decision
  28); the NT51950/NT51951 Display OSD NVT marker rule (decisions 19, 30, 35
  and 37; R3); the navigation focus underline (decision 32); the release
  workflow cleanup with the release re-run conflict (decisions 21 and 29; its
  R3 design returns to the owner before implementation); the VersionManagement
  local-reference rule and duplicate JSON contexts (decision 13); and the
  pre-built profile catalog (decision 38). F03/F06, then F01/F02/F25, follow.
- **Pre-built profile catalog**: built-in profiles stay reviewed JSON under the
  hash-pinned trust index; the build derives a catalog snapshot with the same
  catalog code and ships it, and a profile update regenerates the snapshot. A
  local database is not adopted for built-in profiles; online updates stay
  possible through the trust index. As an R2-R3 item it needs an ADR and
  independent design review before implementation; acceptance is measured
  startup time and snapshot-versus-JSON catalog equivalence.
- **1.1.14** adds a repository-wide inventory and improvement of display
  conventions (decision 39). Examples from the 1.1.13 mockup reviews: field icon
  shape, size and spacing; tooltip styles, placement and content order;
  keyboard focus versus selection indicators; status strips; icon-only versus
  labelled buttons; A/B naming; and bilingual string consistency. Until then
  `1.1.13` work follows the existing conventions. F17/F23/F26 and demonstrated
  F16 defects stay in `1.1.14`.
- **TP SVN**: the SVN is the 4-byte field at TP start + `0x24`, not a TP Header
  field (owner facts of 2026-09-25 and decision 36), so current allocation text
  says "TP SVN"; dated amendments below keep their original "TP Header SVN"
  wording.

This records allocation, not admitted implementation. Each item keeps its own
admission, review, Golden and owner gates; scheduling approves no firmware
behavior, support promotion or release.

### Owner-approved 1.1.x repair allocation — 2026-09-25

This amendment supersedes the affected F01–F26 allocations below. The owner accepted prioritizing input identity, committed-output truth and file preservation before lifecycle repairs and bounded diagnostics. Validate each finding against the current implementation before changing it; old findings are not proof of a current defect. F18 behavioral/interleaving evidence accompanies every repair.

- **1.1.10**: finish the current release. The owner subsequently retained AB
  CtrlRAM Candidate/ContractOnly and explicitly deferred its missing independent
  complete-output Golden, Header/CRC and exact-write-range confirmation and
  remaining manual acceptance to **1.1.11**, under a one-release exception.
  Missing evidence remains missing; this does not promote support or change
  protected GitHub checks. See CHANGELOG and the release-owner attestations.
- **1.1.11**: AB CtrlRAM/OSD evidence and current-head triage; fix reproduced
  F04/F05 stale input/Hex acceptance first. Retain the shared Info/Details,
  Event Buffer, NT51950 DP AB Memory Layout, Flash BIN input and actual firmware
  screenshot/owner-review work already assigned here; cosmetic work follows
  correctness. Temporarily hide **Customized Merge and Customized Replace**
  from ordinary UI entry points while preserving implementation, canonical IDs,
  drafts and historical reports. Reopen only in the release that delivers and
  accepts the corresponding functionality, not automatically at a version bump.
  CLI/direct-launch availability is an explicit implementation-scope question;
  this UI-hide decision does not silently remove command compatibility or delete
  functionality. General Merge/Replace milestones currently remain 1.3.0/1.3.1.
- **1.1.12**: F07/F08 and residual F20/F21: committed BIN receipts, visible and
  retryable persistence failures, remaining picker/I/O consumers and local
  atomic Report replacement. Preserve the already shipped Report Save fixes.
  Add Home startup/catalog-readiness optimization under the measurement-first
  amendment below; retain all output/persistence repairs. TP Header SVN common
  modeling is deferred here pending owner confirmation of Header contents;
  see the bounded amendment below. The NT51950 Normal AB Replace Header backup
  CRC diagnostic also moves here under its separate bounded amendment.
- **1.1.13**: F03/F06 bounded process cancellation/termination/pipe drain first,
  then F01/F02/F25 close, READY cancellation, recovery and lifetime containment.
- **1.1.14**: F17/F23/F26 controlled CLI/workflow/JSON errors, demonstrated F16
  capacity/obsolete-message defects, and integrated regression of these repairs.
- **Remain later**: F09/F10/F12 in 1.2.5; F11/F13 in 1.2.6; F22 in 1.2.7,
  coordinated with 1.2.1 Customized large-file work; F14/F15 in 1.2.8.
  A demonstrated OOM needs a bounded repair proposal, not an invented new limit.
  F19 shipped in 1.1.8; F24 implementation is included in the 1.1.10 candidate.

This is approved scheduling and UI-hide intent, not an implementation completion claim or wholesale acceptance of external APIs, numeric budgets or fault severity. The audit handoff retains acceptance details; this roadmap owns allocation.

### Owner-approved 1.1.12 startup optimization — 2026-09-25

After reviewing current local warm-launch measurements, the owner requested startup optimization in `1.1.12`. Bring Home startup/catalog-ready work forward from the conditional `1.2.8` follow-up, alongside the existing F07/F08 and residual F20/F21 repairs. Keep CtrlRAM cold first-open and F14/F15 in `1.2.8`.

The [handoff](post-v1.1.8-audit-handoff.md#startup-optimization-assigned-to-1112--2026-09-25) records the baseline and acceptance detail: separate first-window, catalog-ready and complete-preload timings; break down the post-window wait; optimize through existing startup/catalog owners; compare equivalent builds and verify first navigation does not merely inherit the delay.

Owner hard-target amendment, 2026-09-25: from process launch, the first main window must be visibly presented within **500 ms**, and **all startup loading must complete within 2,000 ms**, including catalog readiness and deferred startup views. Nonessential work runs in the background without blocking the UI. Background execution does not exempt startup loading from the 2-second limit; unrelated maintenance must be identified separately. These are per-launch limits, not median-only targets. Measure cold and warm launches separately with the actual package on the controlled owner machine; a window handle alone does not prove visible presentation. The first-window limit does not require every page to be ready at 500 ms; full readiness has the 2-second limit. Preserve validation, honest loading/error feedback and first-navigation regression checks.

This replaces target-TBD planning and supersedes the historical 700 ms target for current work; its unachieved historical evidence remains unchanged. No improvement or packaged/cold-launch pass has been demonstrated by this amendment. Broad profile reference convergence stays in `1.2.1`.


### Owner release scope for 1.1.12 — 2026-09-26

The owner set a 2026-09-28 release for `1.1.12` through the `1.1.11` release path
(decisions of 2026-09-25 and 2026-09-26 in the [1.1.12 handoff board](../handoff/1.1.12.md)).
`1.1.12` ships:

- measured startup optimization: deduplicated schema validation, parsing and compilation, and
  bounded parallel bundle preloading under [ADR
  0075](../adr/0075-bounded-catalog-bundle-preload.md). The compressed composite ReadyToRun
  package shape and its 80,000,000-byte EXE ceiling are kept. Measured on the final product source
  and the `1.1.11` package in the same quiet session (package shape, five scored launches each):
  complete loading median 2.20 s against 3.70 s, missing the 2,000 ms target by about 0.2 s; the
  first window stays at about 0.73 s against the 500 ms target, blocked by the compressed
  single-file start-up that the EXE ceiling keeps. GC heap after warm-up (at most 34.9 MB) passes;
  peak working set stays within the same-session `1.1.11` maximum plus 1 MB (handoff decision 18);
  peak private bytes are about 4 MB above `1.1.11` at the median, within the owner's 6 MB
  allowance (handoff decision 16). The WS-IO time box closed with F07 only;
- F07: a committed output keeps its receipt when its delivery or report is
  interrupted;
- removal of the hard NT51950/NT51951 CtrlRAM size limits for Display OSD
  inputs (scoped R3 change; the owner required it in `1.1.12` on 2026-09-26),
  with AB Bases decided by two NVT markers. A Base longer than the IC's
  full-flash map is accepted at any length, a nonstandard length with a
  warning (handoff decision 17);
- a non-certifying local alignment with v0.9.16 for the routes that have
  canonical inputs; the formal comparator for 1.x candidates is `1.1.13` work.

Moved to `1.1.13`, ahead of its existing scope: F08 and residual F20/F21; TP
Header SVN modeling (owner flag definitions are recorded in the handoff board);
the Header backup CRC investigation; the development-process reset,
dual-runtime agent documents, test architecture and the WS-FLOW corrections;
rolling v0.9.16 parity; and first-window work within the EXE ceiling.

### Owner-deferred TP Header SVN modeling — 2026-09-25

The owner deferred shared TP Header SVN modeling to **1.1.12** to first confirm the Header contents. The [existing handoff](post-v1.1.8-audit-handoff.md#tp-header-svn-modeling-deferred-to-1112--2026-09-25) retains read-only findings, the common-definition proposal and pending Header questions. Implementation requires that confirmation and a subsequent scoped admission; this allocation does not accept unconfirmed field semantics or claim a product change. Preserve the existing output/persistence/startup work, the broader 1.2.1 profile convergence, and all 1.1.11 release/evidence gates. SVN modeling is neither a completed 1.1.11 feature nor a release blocker.

### Owner-deferred Header backup CRC investigation — 2026-09-25

The owner assigned the observed **32-byte** Header backup/CRC difference in the NT51950 single-IC, 1 MiB OSD, Normal CtrlRAM **Both** candidate to **1.1.12**. Continue 1.1.11 release preparation with this uncertified case recorded as a known difference. The [existing handoff](post-v1.1.8-audit-handoff.md#header-backup-crc-investigation-deferred-to-1112--2026-09-25) retains the sequence hypothesis, exact byte ranges and firmware-impact questions.

This is a bounded diagnostic deferral, not a claim of complete byte parity or firmware certification. Keep the original Golden and current processing intact; do not mask the CRC words, replace expected bytes or promote other routes. All applicable owner-certified Direct Golden cases, exact-source CI, structure and release gates remain required. Preserve existing 1.1.12 work.

Owner release-identity amendment, 2026-09-23: publish the current work as
**1.1.10**, replacing the earlier `1.2.0` label. GitHub read-only inspection on
this date confirms [v1.1.9](https://github.com/Dennis40816/nvt_fw_combiner/releases/tag/v1.1.9)
is the latest non-draft stable release; its local annotated tag peels to
`b56b4eee5914f1651ed40747ea0ba48015a52105`. This observation does not reconstruct
or certify missing predecessor closure evidence. The dated `1.1.8` checkpoint
above remains historical. Current unfinished work and release evidence are
tracked in the existing [delivery checklist](../ui/v1.1.10-delivery.md#11x-未完成項目彙整--2026-09-25).

Owner allocation, 2026-09-24: investigate and repair in `1.1.11` the failure observed in the concurrent all-lane `test_verify_orchestration` run. The exact `1.1.10` candidate run failed this lane (214/217), while its three failed tests and then the complete module passed in isolated reruns (3/3 and 217/217). The [1.1.10 delivery checkpoint](../ui/v1.1.10-delivery.md#1110-verifier-orchestration-repair--2026-09-24) retains logs and acceptance criteria. This allocation does not mark the failed run as passed or waive any applicable `1.1.10` release gate.

Owner amendment later on 2026-09-24: move that verifier repair forward into
**1.1.10**. The preceding `1.1.11` allocation remains dated history. The
[current repair checkpoint](../ui/v1.1.10-delivery.md#1110-verifier-orchestration-repair--2026-09-24)
records the reproduced pytest collection boundary, bounded correction and
remaining all-lane evidence. No earlier failed run becomes a pass; the full
verifier, Golden, R3 owner and release gates remain required.

Owner 1.1.11 directions, 2026-09-25 (confirmed by the later implementation goal): use one shared slot-role/input-type mechanism for Standard and AB info. DP AB slots emphasize DP CMI A/B; TP A/B slots show their own bank; ordinary TP shows TP A; AB CtrlRAM Base emphasizes TP A/B. Owner's latest 2026-09-25 correction puts Event Buffer in the always-visible primary information area for Standard TP, AB TP A/B and CtrlRAM Base as `Name (0x??)`, with `Common` kept concise. This supersedes the earlier Details placement; preserve bank labels and missing-value disclosure. Retain A-bank range disclosure and hide FWConfig range in the requested info presentation; actual addresses/ranges remain profile-owned. Diagnose NT51950 AB Memory Layout's missing DP AB and verify Flash BIN combination eligibility. Shared-mechanism coverage, root causes, feasibility and final owner visual acceptance still need evidence; these display directions do not authorize invented firmware facts. The owner-provided NT51950 AB Code OSD upload enters the 1.1.11 validation intake only after provenance, hashes, applicable route and complete expected- output contract are checked; it is not yet an owner-certified Golden. The separate one-release exception above owns the 1.1.10 validation deferral; this UI/OSD intake does not alter it, the 1.2.1 Customized/Launcher allocation or the 1.4.1 NT51950/51 AB evidence work.

Owner amendment, 2026-09-23: schedule Customized / General Merge large-file support and the Launcher delta-update, self-update and intranet-migration development plan in `1.2.1`, after the current `1.1.10` release. The [1.2.1 handoff](v1.2.1-handoff.md) carries the scoped review, sequence, unresolved decisions and evidence gates. Retain the existing Python Combiner replacement and ownership/recovery work in that slot. Scheduling does not approve every proposed contract or move the full Launcher release from `2.0.0`; neither new workstream is part of the current `1.1.10` closure.

Owner amendment, 2026-09-22: the next release includes only fixes and changes
allocated before `1.2.0` (through the current `1.1.10` delivery). The previously
planned `1.2.0` release label for that delivery does not pull the separate
Launcher development tranche into the package. Launcher work is excluded from
this release; its next development slot remains to be scheduled. The full
Launcher `2.0.0` target and its acceptance gates remain unchanged.

Owner amendment, 2026-09-21: the work tracked under `1.1.10` is planned for
release as `1.2.0`. Within that work, complete independent Event Buffer display
updates, Partial-family bank cleanup and test closure before AB CtrlRAM Replace;
AB Replace is the final implementation item. The
[1.1.10 delivery checklist](../ui/v1.1.10-delivery.md) retains the detailed
status and evidence. This changes the accepted order and planned release label,
not firmware support, the remaining release gates or unrelated backlog scope.

Owner intake, 2026-09-20: preserve a [local user-data inventory](local-user-data-inventory.md)
for future uninstall implementation, including settings, report history,
version-manager state and transient coordination files. This records cleanup
scope for planning only; it does not enable deletion or assign a new release slot.

The owner requests redistribution of unfinished work across `1.1.6` through
`1.5.x`, with Desay/public AB address corrections completed **before `1.2.0`**,
and sets the **full Launcher release to `2.0.0`**. The sequence below is the
recommended allocation for owner adjustment. It supersedes earlier version
allocations in this document and linked dated handoffs, not their retained
acceptance criteria, historical evidence or permissions. Detailed work packages
below retain their boundaries; they are not a second schedule.

Owner amendment, 2026-09-16: swap the previous `1.1.8` and `1.1.9`
allocations, and tentatively add CtrlRAM Replace of AB Code Flash inputs for
the NT51932 Perfect family and NT51950 Partial family in `1.1.10`.
Finish `1.1.7` first. This schedules the feature; it does not establish bank
write semantics, certify family support or waive existing release gates.

Further owner intake, 2026-09-16: record the linked CtrlRAM/AB discussion
during `1.1.9` and complete the scoped support **before `1.2.0`**. Retain
`1.1.10` as the tentative implementation slot; the `1.1.9` intake is not a
claim that support already exists. The
[AB support handoff](../ui/v1.1.x-custom-options-layout-handoff.md#ctrlram-replace-ab-code-intake--2026-09-16)
records the reference and unresolved contract/evidence boundaries. Also admit
the reported silent long Bundle-folder/output-name failure to `1.1.9` ahead
of optional Settings conveniences; see [issue #434](https://github.com/Dennis40816/nvt_fw_combiner/issues/434)
and the [naming handoff](../ui/v1.1.x-bundle-primary-output-rename-handoff.md#119-long-name-validation-and-recovery-intake--2026-09-16).

Additional owner intake, 2026-09-16: add **Settings > Config > Toolchain**
in `1.1.9` to detect available user-installed VC++ Runtime candidates and let
the user select a compatible runtime, including newer installed versions.
The owner confirms the tool should also bundle the repo-approved runtime and
use it by default; user-installed selection is an optional explicit override.
See the [Toolchain handoff](../ui/v1.1.x-custom-options-layout-handoff.md#toolchain-runtime-intake--2026-09-16).
This is a future feature allocation, not a change to the frozen `1.1.8` scope.

1.1.7 checkpoint (2026-09-16): Information ordering/spacing, default-Off Details,
Config-reapplied Event Buffer facts and the approved Build settings/C source
panel are implemented. Remaining work is integration and release verification,
not another UI redesign. Owner-authorized record recovery preserves original
`1.1.7` at `41fa8484` and reconstructs identical product content on
`codex/1.1.7-record-recovery` at `63983648`; record closure is `b89062c6`.
The owner separately approved the exact code-size allowance on 2026-09-16
(full production +491, runtime +175; Application +174, Bootstrap +1).
Its focused policy tests pass 19/19. The subsequent full verifier passes
structure/code-size and seven product test projects, but remains blocked by
the separate MainWindow aggregate test (1006 actual versus998 limit).
The owner subsequently approved the exact1006 test bound, retaining all
navigation ownership assertions and the985 review warning. The full local
verifier passed at `c081b834` (6628 .NET tests, all script lanes and Python).
The owner explicitly approved publication and main-based reconstruction.
`codex/1.1.7-release-main` starts from published1.1.6/main32f9f398 and preserves
both existing branches. Its product/tests/policy match the verified recovery
source65c0227e exactly; VERSION and release-note metadata now identify1.1.7.
New-record finalization, exact-source CI/package and release-owner gates remain
separate; no tag or publication has occurred yet.
See the [current UI closure evidence](../ui/v1.1.x-custom-options-layout-handoff.md#117-integration-recovery-and-validation--2026-09-16).

Earlier 1.1.6 checkpoint: `v1.1.5` is published (see the
[release evidence](../references/verification-report.md)). Current `1.1.6`
Memory Layout and selection-slot corrections, bold group headers and FW Info
disclosure alignment are locally committed with recorded targeted evidence;
they are not new TODOs or a published `1.1.6` claim. As of 2026-09-15, Desay
detection/new AB geometry, the Settings Config editor, captured Report format
provenance and the DP-size advisory are implemented with scoped test evidence
in [ADR 0072](../adr/0072-event-buffer-format-configuration.md). The latest
Information redesign remains separate. On `1465f11f`, the fresh
`python scripts/verify.py --release-golden` run passed: Bootstrap **1454/1454**,
GoldenRegression **25/25**, zero skips, and all **25 owner-certified complete-output
cases** confirmed by the unchanged per-case gate. The prior 12 Bootstrap failures
were resolved by test/fixture migration, without production or Golden expected
changes. The added real-file inspection case proves complete 1 MiB Normal DP
capture past a previous Common map ceiling; the formal route fixtures retain
generic count 9 and mixed A=2/B=3 coverage. Original and final TRX evidence is
retained under `D:/NvtFwCombiner-TestArea/evidence/v116-ab-format`, with the fresh
Golden run in `unit15-golden-1465f11f`.
The owner's 2026-09-15 request to finish and publish authorizes release
preparation after the reported UI corrections; no further Config redesign is
scheduled for this candidate. Aggregate governance/owner evidence and
`verify.py --all` remain open; the earlier Golden pass is not final-source
integration evidence. New format routes retain candidate/contract-only status.
The TP overlay-length
question is resolved by retaining existing bytes and recording an Excel erratum.
Release preparation is now authorized. Exact-head firmware-owner evidence,
protected review/CI and candidate publication gates remain required.

| Target | Bounded outcome and dependency |
| --- | --- |
| `1.1.6` | **Urgent NT51950/NT51951 partial-family AB correction + editable Desay Settings**: auto-detect the approved Desay markers, Desay TP B at `0x4A000`, public NT51950 partial-family **2 IC** TP B at `0x8A000`; update all coupled offsets, processors and allowed writes. Add required FWConfig/TPA-TPB format admission, the non-blocking DP AB size warning (`0x100000` bytes), and minimal effective-format/output disclosure. Settings supports adding/removing/editing supported-format marker values and separate ID/name lookup through one validated file-backed owner, with safe persistence, invalid-edit feedback and apply/reload invalidation. Include impacted Dummy DP, Memory Layout, Report and family regressions, exact write-range audit and required independent Golden evidence. Carry already-committed UI fixes forward without redesign. |
| `1.1.7` | **Information and output confirmation UI**: TP Version → PID → Common FW Version → Event Buffer Version (`0x97 - Desay`, plain text); responsive action alignment, bounded Details and its default-Off appearance preference; clear IC/Mode/effective-format output confirmation. Final PID-inclusive preview/acceptance precedes implementation. |
| `1.1.8` | **Release/agent workflow proportionality**: gate inventory and evidence-backed simplification, AI Skill/documentation/routing consistency and a bounded reversible pilot. Assess code-size/count gates, repeated confirmations/tests and derived-data automation; do not weaken Golden, signing or publication boundaries. |
| `1.1.9` | **Long-name failure/recovery first, then Toolchain Runtime selection, remaining Settings conveniences and CtrlRAM AB intake**: fix issue #434 with edit-time visible errors, recovery to the last accepted valid name on invalid commit, and safe handling of BIN/Bundle/staging names. Assess removal of the product's total-path cap separately from the filesystem component limit. Add **Settings > Config > Toolchain** for detection and user selection of available compatible VC++ runtimes, including newer installed versions; see the linked Toolchain handoff for admission/readiness and verification questions. Record/reconcile the owner-provided CtrlRAM AB reference and prepare its contract/evidence questions for completion before `1.2.0`. Inventory other user-adjustable preferences and justified reset/import/export conveniences through the `1.1.6` Settings owner. Arbitrary maps, CRC/ranges, safety overrides and support promotion are outside ordinary Settings. |
| `1.1.10` (required before `1.2.0`; owner scope update 2026-09-24) | **CtrlRAM AB Replace, affected shared-contract extraction, DP Replace retirement and verifier orchestration repair**. Retain the NT51932 Perfect family／NT51950 Partial family intake and explicit Common/Desay/topology evidence. Share canonical AB format/layout ownership, preserve independent page instances, and detach shared DPCMI/Perfect-family dependencies before retiring DP Replace. Keep repository-script pytest collection inside the selected test root while preserving exact selection, scratch isolation, cancellation and complete execution; pass the applicable fixed-source verifier. Bank/source/version and fixed-Reference decisions, unresolved firmware rules, implementation and verification evidence are tracked in the [1.1.10 delivery checklist](../ui/v1.1.10-delivery.md). No filename-based AB detection or blanket family promotion. |
| `1.1.11` | **Input correctness, deferred AB/OSD verification and shared information UI**: reproduce/fix F04/F05; complete the current-source AB CtrlRAM and OSD evidence/owner review, retained Info/Details/Event Buffer/DP AB layout/Flash BIN assessment and loaded-FW screenshots. Temporarily hide Customized Merge/Replace UI entry points until their corresponding functionality is released and accepted; preserve implementation and data. See the owner-approved allocation above. |
| `1.1.12` | Published 2026-09-26. **Output/persistence correctness and measured Home startup optimization**: F07/F08, residual F20/F21; preserve committed receipts and existing destinations, expose retryable errors. Hard targets from process launch: visible first window within 500 ms, all startup loading within 2,000 ms; nonessential work in background. Use comparable per-launch timing and first-navigation regression evidence; see the 2026-09-25 startup amendment. TP SVN modeling uses the owner-confirmed 4-byte field at TP start + `0x24` (decision 36) and proceeds only under the `1.1.13` C-7 R3 admission; the scoped NT51950 Normal/Both Header backup CRC investigation follows its linked deferral amendment. 2026-09-26 release scope: see [Owner release scope for 1.1.12](#owner-release-scope-for-1112--2026-09-26); F08, residual F20/F21, TP SVN modeling and the Header backup CRC investigation move to `1.1.13`. |
| `1.1.13` | In progress. **Items moved from 1.1.12, 1.1.12 follow-ups and the pre-built profile catalog, then process cancellation and window lifetime**: the items moved by the 2026-09-26 [1.1.12 release-scope disposition](#owner-release-scope-for-1112--2026-09-26) first (F08 and residual F20/F21, TP SVN modeling, the Header backup CRC investigation and the process, agent-document, test-architecture, parity and first-window work); the follow-ups and additions of the [2026-09-26 allocation](#owner-allocation-after-the-1112-release--2026-09-26), including the NT51950/NT51951 Display OSD NVT marker rule, release workflow cleanup and pre-built profile catalog; then F03/F06 before F01/F02/F25; bounded termination and recovery. Waves, order and state: [1.1.13 board](../handoff/1.1.13.md). |
| `1.1.14` | **Controlled diagnostics, repair regression and display-convention consistency**: F17/F23/F26, demonstrated F16 defects; retain F18 evidence throughout. Repository-wide inventory and improvement of display conventions per the [2026-09-26 allocation](#owner-allocation-after-the-1112-release--2026-09-26) (decision 39); `1.1.13` follows the existing conventions until then. |
| `1.2.0` | **No longer the release label for `1.1.10`**: the owner explicitly chose `1.1.10` on 2026-09-23. No additional scope is assigned by that numbering correction. The former Launcher development tranche remains in `1.2.1`; reference refresh and current evidence remain in the `1.1.10` delivery. |
| `1.2.1` | **Customized large-file support, Launcher update development, Python Combiner replacement and residual ownership convergence**. Follow the [1.2.1 handoff](v1.2.1-handoff.md) for large-source/small-slice then large-output work, delta transfer, Launcher self-update and intranet migration. Preserve the [Python Combiner intake](#121-python-combiner-intake--2026-09-21), including B-bank CRC/postbuild mode assessment, and existing identity/family/topology/input-snapshot/page-draft and recovery work. Progressively consolidate repeated family profile declarations through an explicit, validated shared-definition reference; retain each member's map, topology, identity and evidence. Shared DPCMI/Perfect-family and page-contract work required by DP retirement remains in `1.1.10`; avoid a wholesale profile rewrite. |
| `1.2.2` | Former residual F20/F21/F26 work moves to 1.1.12/1.1.14; F24 already moved to 1.1.10. No replacement scope is assigned. |
| `1.2.3` | **CLI and deterministic Desktop automation** through existing Application/startup owners: workflow coverage, load-report/tab/state/capture/exit, actionable argument errors. Prioritize this before remaining broad UI acceptance. |
| `1.2.4` | **First-entry and page flow**: IC/context lifetime, invalidation, Cancel/Back and remaining per-page custom-option density, using approved previews. Do not reopen completed slot/Memory Layout styling. |
| `1.2.5` | **Report completion**: physical-section grouping and historical replay compatibility; preserve completed Changes cards/navigation. |
| `1.2.6` | **Shared visual/native acceptance**: evidence-driven theme fixes, DPI/high contrast/screen reader and remaining System activity native checks. |
| `1.2.7` | **Documentation and proven-unused code cleanup**: current-versus-history/SPEC/issue reconciliation, remeasured analyzer baseline and reviewable cleanup; preserve evidence and canonical ownership. |
| `1.2.8` | **Conditional performance follow-up**: F14/F15 and residual CtrlRAM cold first-open, only with demonstrated value. Home startup/catalog-ready optimization moved to `1.1.12` by the 2026-09-25 owner amendment; preserve completed `1.1.5` evidence and do not claim a new ten-minute result. |
| `1.3.0` | **General Merge authoring** through existing typed mappings/compiler/executor. |
| `1.3.1` | **General Replace authoring**, immutable reference and the same shared operation model. |
| `1.3.2` | **Saved/custom rules**: edit, persistence, import and validation after the General contracts settle. Distinct from the narrow Settings marker editor. |
| `1.3.3` | **Maintainer IC/family rule-authoring UI**: validate/export untrusted candidates; no live-catalog self-promotion. |
| `1.4.0` | **Independent evidence completion** for retained input-only cases and fact-scoped aliases. This does not defer evidence required for an earlier changed route. |
| `1.4.1` | **IC/capability evidence intake**: remaining NT51950/NT51951 AB, Perfect-family and `ldc-tp-only` gaps; NT51928BT awaits owner-confirmed facts and remains unavailable until separately admitted. Inventory existing support first, split into additional patch releases if the actual intake is large. |
| `1.4.2` | **Launcher/publication extraction decision**: review a concrete need, contracts, trust boundaries and migration/deletion/rollback before any separately approved repository split. Extraction is optional, not a prerequisite for `2.0.0`. |
| `1.5.0` | **Launcher publisher trust/signing/security closure** with independent evidence and actual key/service permissions. |
| `1.5.1` | **Catalog/Registry controlled preproduction validation**: package identity, verification and update readiness; no implied production activation. |
| `1.5.2` | **Installer/download/recovery refinements**: measured transfer savings, exact installed bytes, failure recovery and rollback. |
| `1.5.3` | **Full Launcher candidate acceptance**: clean Windows install/update/failure/recovery/rollback and end-to-end evidence across the integrated candidate. |
| `2.0.0` | **Full Launcher release**. Actual Catalog/Registry production activation requires explicit owner GO and closed security/evidence/publication gates; the scheduled version is not approval to deploy. |

## Shared audit allocation — 2026-09-17

Owner direction: distribute the [shared review](https://chatgpt.com/share/6aaa7f89-1a70-83e9-bcac-badc1fdface2)
across `1.2.0`–`1.2.x` by difficulty and comparable work volume, then resume
`1.1.8` verification/publication. This table adds explicit acceptance to the
existing version outcomes above; it does not merge future changes into the
frozen `1.1.8` candidate. Full Launcher delivery remains `2.0.0`.

Source read on 2026-09-17: the page contains overlapping A01–A14 and F01–F18
reviews of `c580476f`, followed by the fuller `1.1.7`/`e08bb4f4` review with
25 findings (F01–F26 excluding F12), then the owner's Customized naming
decision. Use the final F identifiers below; do not count the earlier lists
again. F12 is a naming requirement, not a confirmed defect. These are review
claims to revalidate against each implementation head, not fresh local tests
or a certification that all current versions reproduce them. Linked report
attachments have not been independently inspected.

Owner amendment, 2026-09-18: bring **F19 only** forward into unpublished
`1.1.8`. Implementation `8375dcb1` and final evidence `8d2f33dc` close the
bounded staging correction locally (54 focused and 1127 Infrastructure tests;
independent R1 review and capability validator pass), not release publication.
Do not implement F19 again in `1.2.0`; retain its regression in the runner
acceptance. The remaining four external P1 claims were reviewed together at
that source: Report Save's F20 I/O boundary remains a P1 release blocker;
F01/F02/F03 are conditional P2 findings, not proof of ordinary-use crashes.
F20's separate picker-only gaps are also P2. Bringing the Report Save blocker
forward requires the owner's next scope decision; it has not been silently
implemented or waived. Existing future allocations remain pending that choice.

Subsequent owner approval, 2026-09-18: also bring the **Report Save portion of
F20** forward into `1.1.8`. Reviewed implementation `8332ef59` and final
evidence `35421663` cover provider/write/flush/disposal failure containment,
snapshot capture, duplicate-save protection and retry; F21 atomic replacement
is not included. Integration follow-up `38c75f0e` passes the full local
verifier and exact-head CI. PR435 merged into the version branch; publication
still requires the release pipeline. Do not reimplement these completed F19/F20
corrections in `1.2.x`. Retain their regressions and keep general picker gaps
in `1.2.2`, alongside F21/F26/F24. The table below preserves the original
relative sizing; subtract brought-forward work at admission rather than filling
the freed capacity with unapproved scope.

Published-scope amendment, 2026-09-19: F19 and the Report Save subset of F20
are now released in `v1.1.8`. F21 snapshot/reentry/post-disposal-success
requirements are also satisfied by that Save correction; **atomic local
replacement remains open**. General picker boundaries remain open. The dated
paragraphs above retain their original pre-publication claims. Do not count
those shipped portions as future implementation work. F01/F02/F03 retain
their recorded conditional assessment; the external report's P1 labels are
not a new local reproduction or an automatic severity upgrade.

Relative sizing includes implementation, fault-injection/contract tests and
scoped review: S=1, M=2, L=3 units. Units are comparative estimates, not days
or measured duration. Aim for 6–8 audit units per release; count shared owners
once. Existing feature work also consumes capacity: at admission split an
oversized existing feature into a separately bounded later patch, rather than
silently dropping tests or claiming equal total release duration. Preserve
the declared Family-before-retirement dependency and earlier firmware gates.

| Version | Audit work / relative size | Required observable acceptance |
| --- | --- | --- |
| `1.1.11` | **Input request identity**: F04/F05 plus AB/OSD verification and temporary Customized UI hiding. | Cancel/reopen cannot apply old picker results; a slow old Hex load cannot replace a newer accepted document; failed loading retains prior bytes/path. |
| `1.1.13` | **Process and window lifetime**: F03/F06 precede F01/F02/F25. | Bound cancellation, termination confirmation and held-pipe drain; failed handoff resumes saving and allows a second Close; READY cancellation and stale callbacks remain contained. |
| `1.1.12` | **Output and persistence truth**: F07/F08 and remaining F20/F21; Home startup optimization, deferred TP SVN modeling and the scoped Header backup CRC investigation follow the 2026-09-25 amendments. | Preserve committed output receipt after cancellation; show retryable save failures; local Report replacement preserves the original destination on precommit failure. Keep shipped snapshot/reentry/disposal fixes. Startup acceptance follows the linked handoff's comparable stage timings and navigation checks. TP SVN modeling uses the owner-confirmed field at TP start + `0x24` (decision 36); implement it only under the `1.1.13` C-7 R3 admission. |
| `1.1.14` | **Bounded diagnostics**: F17/F23/F26 and demonstrated F16 defects. | Invalid arguments fail controllably before host construction; None differs from unknown workflow; oversized JSON integer returns a structured error; text follows actual typed limits. |
| Cross-release | **F18 evidence; F19/F24 completion tracking**. | F18 behavioral/interleaving tests accompany each affected change. F19 shipped in 1.1.8; F24 is implemented in the 1.1.10 candidate, with publication tracked separately. |
| `1.2.5` | **Typed results and language projection**: F09 (L), F10 (M), F12 naming requirement (M); 7 units. | Re-language completed/blocked/partial outcomes from typed state. General rows retain identity/drafts/mappings. Use Customized / Customized Merge / Customized Replace for relevant visible labels, CLI help and new Report labels; retain canonical IDs, command compatibility and unrelated Settings General text. Preserve historical Report interpretation. |
| `1.2.6` | **Shared presentation owners**: F11 (L), F13 (L); 6 units. | Semantic typography controls actual effective style, with explicit legitimate variants and DPI/theme checks. Converge the proven duplicate accepted-output helper through its existing owner; retain AB/extra-A-output/Replace naming contracts, not a giant base ViewModel. |
| `1.2.7` | **Bounded intake**: F22, coordinated with 1.2.1 large-file work. | Validate bounded reads and cancellation; assess aggregate materialization only after measured resource/compatibility decisions. No new numeric budget is approved. |
| `1.2.8` | **Measured notification/allocation improvements**: F14 (L), F15 (L); 6 units. | Measure notification/inspection/compile/measure counts before and after; publish accepted state once and avoid unnecessary focus scans. Remove only the redundant after-range copy with byte/diff/hash/Golden equivalence and measured allocation evidence. Do not promise a speedup; Home startup is separately scheduled in `1.1.12`, while CtrlRAM cold first-open remains conditional here. |

The 2026-09-25 owner amendment above supersedes the original 1.2.0–1.2.4
repair allocations and their old size estimates; no new duration estimate is
implied. F19 is already shipped. Re-estimate only the remaining F20/F21 work.
F22 retains AUD-11A bounded-intake and AUD-11B aggregate-budget assessment;
no new numeric limit is approved. Duplicate-key policy AUD-16B/N01 remains
unallocated/pending decision and must not delay F26 in 1.1.14.
AUD-01 spans F08 in 1.1.12 and lifetime work in 1.1.13. Other shared AUD labels
retain their per-finding allocations rather than moving every subitem together.
The external B0–B4 batches are advisory grouping only, not new release slots.

**F18 is required in every affected release**, included in those estimates,
not a final catch-up milestone: controllable interleaving and failure tests
must demonstrate each acceptance sequence. Source-string checks alone cannot
close a behavior finding. Keep staging, runner, UI I/O and window lifetime
policies with existing owners; no second execution engine or global store.
Earlier A07 picker localization is included with F20/F09; A08 duplicate
publication with F14; A12 acceptance-pattern convergence is limited to proven
F13 duplication, not an additional speculative framework.

The earlier owner deadline for scoped CtrlRAM AB support remains before
`1.2.0` (`1.1.10` tentative); the source's suggestion to do all refactors first
does not override that explicit instruction. Do not mix AB bank/CRC changes
into these audit corrections. The source supplies no approved new bank map.

Release boundary: scheduling a reported issue for `1.2.x` is not a waiver for
a confirmed current P0/P1. Revalidate the high-priority claims against the
`1.1.8` candidate before publication; report any surviving blocker under the
existing release policy rather than treating successful CI as proof of safety.

### Immediate prerequisites for 1.1.6

Use the [Desay intake](../ui/v1.1.x-custom-options-layout-handoff.md#desay-nt51950--nt51951-rule-intake--2026-09-14)
for the consolidated specification. Owner correction, 2026-09-14: `0x2200C`
was a typo; the primary FWConfig starts at `0x22200` and its `+0x0C` field is
`0x2220C`. Owner follow-up explicitly selects the **primary** for Desay
detection because its location is documented. Both the typo and source-choice
questions are closed. Read the profile-bound primary field for each scoped
TP input; do not silently fall back to Backup or migrate other existing
FWConfig readers. This decision is now locally implemented and narrowly verified;
the remaining integration gates are listed in current progress above.
Confirm coupled bank extents,
source bounds and processor/write ranges; a TP B start alone does not define
them. Close required owner/golden evidence before firmware implementation and
release at the affected gates. Limit this version's owner reuse work to what
the urgent change actually needs; it must not wait for the broader `1.2.1`
refactor or the `1.3.x` editors. Owner follow-up explicitly brings the bounded
Desay Settings editor into this urgent version. Implement the validated shared
rule owner first, connect detection/admission and writes next, then Settings
and reload/regression coverage as separate reviewable commits. Changing a
display name must not select a different layout; adding a marker may bind only
to an already admitted format in the approved IC/workflow scope. Keep effective
rule identity with accepted run state and invalidate stale readiness/preview
when a behavior-affecting rule changes. The owner has approved the
[invalid-configuration behavior](../ui/v1.1.x-custom-options-layout-handoff.md#desay-settings-invalid-configuration-decision--2026-09-14):
reject invalid Settings drafts, block affected AB Builds on invalid external
detection configuration without silent fallback, allow explicit default
recovery, and do not block solely for missing display names. The owner also
accepts explicit save/apply re-evaluation, external reload checks at startup,
manual reload and pre-Build, and immutable rules for an in-progress Build.
Keep ordinary Settings immediate-apply. The owner accepts a large
[Settings > Config page](../ui/v1.1.x-custom-options-layout-handoff.md#desay-rule-apply-lifecycle-and-config-page--2026-09-14)
within the existing modal, superseding a separate editor modal. Its fixed footer
has Restore defaults, Discard changes and Save and apply; section switches keep
the draft and dirty close/Escape asks before discarding. Detailed visual-reference
geometry still needs review; the
[consolidated handoff and Config preview](../ui/v1.1.x-custom-options-layout-handoff.md#current-decision-checkpoint--config-preview-2026-09-14)
record the accepted conclusions and generated illustration separately from
production evidence. This record does not implement the page.

Request any source material needed for this urgent correction now; the separate
pre-`1.2.0` refresh reminder is not permission to defer a missing `1.1.6` fact.
If evidence is unavailable, report the exact blocker instead of silently
shipping guessed geometry or moving the correction past `1.2.0`. Every actual
release still executes all applicable certified Golden output cases against
its candidate; `1.4.0` is only the residual evidence-completion backlog.

The sections below include dated previous allocations. Interpret their version
references against this current table; completed releases retain their original
versions, evidence and acceptance residuals.

2026-09-10 post-release reconciliation: immutable `v1.1.4` is published from
`02fc70c8c25a5885be5e0e6db7eb108a37b4a131` through PR #427 and release run
`34484902901`. Earlier dated local/pending checkpoints below remain history,
not a request to redo shipped work. Clean-Windows visible acceptance remains
an explicit residual, not a passed headless-smoke claim. The owner approved
starting `1.1.5` after this planning reconciliation: local full-verifier
parallelization first, Home startup second, CtrlRAM cold first-open third.
Other version allocations remain unchanged; CLI automation has not been
approved for acceleration from `1.2.7`.

2026-08-09 planning amendment: the owner approved complete removal of the
remaining legacy architecture, one production path per module, the
consolidated specification, and the LAR-00 through LAR-12 dependency graph.
The graph is part of the `v0.10.3` complete-refactoring milestone. Its `LAR-*`
planning ids receive GitHub issue numbers only after separate publication
authorization. PR #352 remains the stable predecessor; this amendment does not
tag, release, or reopen it.

2026-08-13 completion record: LAR-01 through LAR-12, LAR-00, and #197 closed
their verifier, Golden, review, package, merge, CI, and release evidence before
the stable `v0.10.3` tag. The following stable `v0.10.4` release preserved that
architecture and recorded its unachieved 700 ms target as an explicit residual.

This file owns future milestone order and release boundaries only. It does not
repeat product requirements, architecture decisions, firmware facts, skill
inventories, issue acceptance criteria, or historical release notes.

Canonical detail lives in:

- [`SPEC.md`](../../SPEC.md) for product scope and accepted requirements;
- [`CHANGELOG.md`](../../CHANGELOG.md) and dated release records for completed
  history;
- [`verification-report.md`](../references/verification-report.md) for the
  current package/release evidence state and open gates;
- the [`0.10.x maintainability design`](0.10.x-maintainability-working-design.md)
  and its linked continuations for architecture reasoning;
- [`ADR 0021`](../adr/0021-code-size-ratchet-and-convergence.md) for the
  production-code measurement, exact descending ratchets, and reviewed
  candidate-ledger completion contract;
- the [`agent skill inventory`](../governance/agent-skill-inventory.md) and
  [`routing contract`](../governance/agent-skill-routing.md) for adopted
  workflow skills; and
- the [`0.10.x ticket dependency plan`](../governance/0.10.x-ticket-dependency-plan.md)
  plus GitHub issue bodies for implementation order and acceptance criteria.

2026-08-11 milestone amendment: after the official `v0.10.3` complete-refactor
tag, the owner added a `v0.10.4` package acceptance target: the controlled
compressed single-file, self-contained `win-x64` Home launch must reach a
nonzero main-window handle at or below a 700 ms median after one unscored
warm-up and across five measured launches. The cold launch remains recorded.
This bounded first-window correction belongs beside the simplification audit;
the broader observable, cancellable, bounded, and user-controllable preload
lifecycle remains `v0.10.5` scope.

2026-08-13 milestone amendment: after the official `v0.10.4` release, the owner
approved [ADR 0049](../adr/0049-unified-preload-lifecycle.md) and the
[`v0.10.5` specification](../specs/v0.10.5-unified-preload-lifecycle.md).
`PL-01` through `PL-07` implement the bounded lifecycle and `PL-00` owns its
terminal evidence/release gate. The lifecycle owner controls scheduling and
operator actions only; catalog, report, inspection, diagnostics, and external-
runtime semantics remain with their typed owners.

## Moved release history

These headings keep links from dated records working. Their sections moved
verbatim to the [roadmap history](nfc_roadmap-history.md) on 2026-09-26.

### `1.1.4`: UI corrections and AB Dummy DP

Moved to the [roadmap history](nfc_roadmap-history.md#114-ui-corrections-and-ab-dummy-dp).

### Current remaining queue: small-impact work first

Moved to the [roadmap history](nfc_roadmap-history.md#current-remaining-queue-small-impact-work-first).

### Hover-only CtrlRAM endpoint hierarchy — 2026-09-09

Moved to the [roadmap history](nfc_roadmap-history.md#hover-only-ctrlram-endpoint-hierarchy--2026-09-09).

### Small-region grouping: accepted information model — 2026-09-08

Moved to the [roadmap history](nfc_roadmap-history.md#small-region-grouping-accepted-information-model--2026-09-08).

### Current Home and Settings inventory follow-up

Moved to the [roadmap history](nfc_roadmap-history.md#current-home-and-settings-inventory-follow-up).

### Local full-verifier parallelization

Moved to the [roadmap history](nfc_roadmap-history.md#local-full-verifier-parallelization).

## Work package: agent workflows, documentation and minimality

Historical allocation: the owner merged the previous `1.1.6` and `1.1.7`
milestones on 2026-09-05. The current sequence separates the workflow/gate
audit (`1.1.8`), shared ownership (`1.2.1`) and documentation/minimality cleanup
(`1.2.7`), retaining the work-package boundaries below.
Use capability, task difficulty, risk and coordination cost to select models
and reasoning effort from all available models; disclose actual known model
configuration, without permanent model-name roles. Audit skill inventory and
routing consistency and forward-test the resulting guidance.

The [Tool-development retrospective handoff](../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md)
owns the evidence/confidence scorecard, separated bottleneck timeline,
lightweight versus R2/R3 workflow, reusable checklist/automation priorities,
lead-time/rework/flakiness/recovery measurements, reversible pilot and
conductor/architect playbook. The resulting workflow still requires separate
owner approval. Do not migrate every repository, create a new tool repository
or impose enterprise ceremony through this allocation.

### Release gate proportionality assessment

Owner allocation, 2026-09-13 (now `1.1.8`, resequenced 2026-09-16): assess unreasonable release gates,
including the rigid code-size accounting exposed during `1.1.5` preparation.
This is an assessment and proposed simplification, not authorization to remove
current checks or change the frozen `1.1.5` release procedure.

Inventory each local, PR, main and release gate with its actual owner,
protected risk, triggering condition, evidence/output, measured execution and
approval-wait cost, overlap, and the consequence of removing it. Classify each
as retain, narrow, automate, combine/reuse exact-source evidence, downgrade to
review warning, or remove; justify the choice from observed coverage and risk.
Prioritize hard total-source line counts and exact-count test assertions,
repeated verification or human confirmation, derived-data synchronization,
and governance-record requirements disproportionate to the affected behavior.
Do not replace them with another speculative scoring or approval framework.

All applicable owner-certified Golden output cases still execute under their
approved complete-output contracts. Firmware/write-range safety, immutable
input handling, source/asset identity, credentials, release permissions and
protected publication remain explicit boundaries. A proposed alternative must
identify preserved failure detection, responsible ownership and suitable
regression evidence before any separately authorized policy change. Record
recommendations in the existing workflow retrospective, with policy/CI changes
reviewed at their actual risk rather than silently waived for this release.

### Public baseline and vendor-specific UI/workflow discussion

Owner intake, 2026-09-14: the selection-slot alignment preview is approved;
the flattened groups, shared card edges and Input/Output top-edge alignment are
locally implemented and verified in the
[UI handoff](../ui/v1.1.x-custom-options-layout-handoff.md#local-completion--2026-09-14).
Return next to the vendor workflow discussion. The
[Desay NT51950/NT51951 intake](../ui/v1.1.x-custom-options-layout-handoff.md#desay-nt51950--nt51951-rule-intake--2026-09-14)
records FWConfig-relative `0x0C` detection (owner-corrected primary field `0x2220C`, values
`0xA6`/`0x97`), an Info indication, user-editable detection values and a Settings
rule inventory, a DP AB Code input-size warning against exactly 1,048,576 bytes
(1 MiB / `0x100000`, owner-confirmed 8 Mbit), Desay AB TP B output start `0x4A000`,
reusable public/vendor flow separation, and public NT51950 partial-family
AB Code / 2 IC TP B output start `0x8A000` with coupled offset/write-range
updates. The owner requires complete Q&A and consolidation before development.
The owner confirms a single byte matching either `0x97` or `0xA6` and selects
the documented primary FWConfig field (`0x2220C`) for this detector. Existing
Backup-based version/topology/inspection contracts stay unchanged. Settings is the requested
common entry point for candidate user-editable rules, with editability and
safety boundaries to be assessed before implementation. The accompanying
Profile/Family/IC Count review is an assessment, not an approved rewrite.
Further owner direction: public and vendor inputs show the actual marker value,
e.g. `0x97 - Desay` under the latest plain-text **Event Buffer Version** label
(superseding the earlier Format badge proposal). TP Version comes first; PID
is primary and outranks IC Count. Add a Settings preference for input Details
default expansion, initially Off. Responsive action alignment, primary-field
layout and the PID-inclusive preview remain pending; these are recorded
requirements, not completed UI work. The current sequence puts the urgent
firmware/admission change and bounded Desay Settings editor in `1.1.6`,
Information UI in `1.1.7`, and broader Settings conveniences in `1.1.9`.
Required FWConfig unreadability/invalidity blocks the scoped
format-dependent workflow, separately from the non-blocking DP-size warning.
The owner also confirms that TPA/TPB Common-versus-Desay disagreement blocks
Build; compare resolved formats, not merely equality of their raw marker bytes.
The handoff records the existing topology-dependent AB gate and the need to
verify shared 950/951 coverage; no new UI or runtime gate is implemented yet.
This is recorded direction, not runtime changes. Saved-rule/IC-authoring work
follows the current `1.3.x` allocations.

Owner intake, 2026-09-09: add this discussion to `1.1.x`; allocate it to
`1.1.6` alongside semantic-consistency and minimality work. The owner observes
that specialized features appear to have accumulated independently. This is
an assessment/design TODO, not authorization to ship a generic form engine,
create vendor-specific executors or alter existing customer firmware routes.
The [custom-options handoff](../ui/v1.1.x-custom-options-layout-handoff.md#public-baseline-and-vendor-variation-discussion--2026-09-09)
owns the questions and inventory boundary.

Owner follow-up, 2026-09-13: after the `1.1.5` Release completes, prioritize
the concrete execution approach for customized flashmaps within this existing
discussion. Compare public/family reuse, explicit customer/layout selection,
map authoring/import/versioning, validation/Golden evidence and report identity.
This is a post-release discussion, not additional `1.1.5` implementation or an
advance of the separately allocated rule-authoring UI.

After the published `v1.1.5`, the
[concrete flashmap proposal](../ui/v1.1.x-custom-options-layout-handoff.md#customized-flashmap-execution-proposal--after-v115-2026-09-13)
records the recommended first slice, existing contract gaps, import/versioning,
validation/Golden and report traceability. It is a discussion draft, not an
implementation-ready specification or an approved Desay/NT51928BT map.

Recommended direction for discussion: one public baseline and versioned vendor
variants that reuse existing profile/compiler,
capability, session, validation, confirmation and report owners. Separate
firmware facts from feature applicability, authoring sequence and visual
grouping. The approved 950/951 Desay slice automatically selects its effective
format from the validated marker; the earlier explicit-selection proposal
does not override this decision. Other vendor selection remains scoped for
discussion. Do not infer a vendor from BIN filenames or scatter new vendor
conditionals through pages. Compare whether existing profile and capability
contracts suffice before proposing any new abstraction.

Completion of this TODO means an evidence-backed current-feature inventory,
owner-approved boundary/selection/lifecycle decisions, a representative public
versus Desay preview, a migration order with deletion criteria, and a test and
report-traceability matrix. Implementation is separately scoped and allocated
after those decisions; the unresolved Desay map remains a distinct prerequisite
for firmware changes, not a reason to block the architectural discussion.

### Documentation, semantic consistency and minimality

Retain the complete documentation scope: evidence-preserving semantic
architecture/version-rule convergence, stale SPEC evidence and
current-versus-historical headings, and each active handoff's open TODO,
owner, blocker and next action. CI/release instruction and evidence convergence
remains in `1.1.3`; completed `1.1.2` cleanup is history, not reopened work.
Preserve historical evidence and do not add a parallel documentation framework.

Family reuse/convergence follow-up (owner request, 2026-09-09): use the
[current inventory](nfc_roadmap-history.md#family-reuse-inventory--2026-09-09) to trace Perfect and
partial shared facts across Standard, AB, DP/CtrlRAM Replace and General
consumers. Preserve one canonical definition per genuinely shared fact;
per-member registration, capability/evidence and publication identity are not
duplicate firmware facts. Identify version/purpose/reference divergence,
propose explicit migrations and deletion milestones for proven duplication,
and verify family members together. Do not create a member-specific map to
compensate for a missing binding, erase legitimate customer/topology/version
differences, or treat a filename/common container as family proof. The current
audit is read-only; any production migration retains its affected authority
and byte-evidence gates.

The owner's follow-up records DP CMI as DP-artifact metadata owned by canonical
IC/family profile data, not DP Replace. The
[IC/profile and state ownership inventory](../ui/v1.1.x-custom-options-layout-handoff.md#ic-profile-and-state-ownership-inventory--2026-09-09)
distinguishes immutable definitions, map/artifact bindings, decoded input
snapshots, accepted workflow/run state and presentation-only drafts. Perfect
and Partial relationships stay with the canonical family; support/evidence
remain independently declared. Use that inventory to prioritize the proven
DP-provider/family dependencies and assess Standard-rooted IC discovery and
Report-bound memory context. Do not rewrite correctly owned UI drafts, add a
global state framework, or treat this inventory as a reproduced runtime bug.

Combine the related dead-code/minimality review with the deferred analyzer
cleanup. Re-measure the recorded baseline before editing: 169 style diagnostics
(`IDE0007` 142, `IDE0002` 10, `IDE0001` 7, `JSON002` 5, `IDE0008` 4,
`IDE0003` 1). Remove only proven-unused code and make reviewable mechanical
changes with affected tests; no authority rewrite, suppression or evidence
deletion is implied.

Reconcile the historical tracker records below against actual implementations
and retained acceptance criteria. This is provenance/status reconciliation,
not permission to auto-close issues or reopen immutable final records.

### DP Replace retirement — owner decision, 2026-09-09

The owner has decided to remove DP Replace. On 2026-09-20 the owner moved
retirement and its prerequisite shared-fact extraction into `1.1.10`, together
with CtrlRAM AB Replace; see the [delivery checklist](../ui/v1.1.10-delivery.md).
This supersedes the `1.2.2` retirement / `1.2.1` prerequisite allocation and
the earlier `1.1.6` allocation; retirement itself is settled.
The local retirement unit is implemented and its scoped review and regression
gates are closed. Canonical full-image metadata ownership and its consumers
were decoupled from DP runtime in `3379ca87`, following the shared DPCMI and
explicit Perfect-disclosure migrations. The retirement removes dedicated
runtime, profiles and policy rows while preserving shared tests and historical
evidence. Actual commands, source boundaries and open integration/R3 gates are
recorded in the delivery checklist; local completion is not a verified release.
Earlier "owner-unallocated" wording describes the preceding decision state,
not a remaining choice to reopen the feature.

Target: retire the DP Replace experience without changing the behavior or
output bytes of Standard Merge, AB Merge (including Dummy DP), CtrlRAM Replace
or the retained General workflows. This is an acceptance target, not an
already-verified zero-impact claim. Removing DP Replace does not remove DP
inputs, DP metadata/CMI, DP/TP map facts, or the common Replace operation model.

The prerequisite shared metadata/family facts now have neutral canonical
owners; their mutable page/session instances remain independent. This does
not authorize removing the shared Replace engine or weakening exact-reference,
package-admission and historical Report checks.

Current impact and required migration boundary:

| Surface | Assessment / retirement TODO |
| --- | --- |
| Ordinary UI and CLI authoring | The retirement removes all 14 DP routes from the [capability policy](../contracts/canonical-capability-policy-v1.json), obsolete selectors, command/help and service wiring. The existing compiler terminal rejects valid retired-DP declarations before creating any artifact; earlier admission failures keep their typed issue. The [CLI handler](../../src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs) must reject the retired command before input reads, execution or output/report side effects, without General fallback. |
| Shared DPCMI definition — completed prerequisite | The [trust index](../../profiles/built-in/package-trust-index.json) registers neutral canonical providers. `nt51929-nt51932@1.3.1` adds declared full-image views while retaining its DPCMI facts; Standard families for 17/27, 23/26, 28, 19/29/32 and 50/51 retain their exact references. CtrlRAM uses the common full-image inspector without a DP runtime fallback. Migration evidence is recorded in the [delivery checklist](../ui/v1.1.10-delivery.md). |
| Perfect family disclosure — completed prerequisite | The [NT51919/29/32 Perfect relationship](../../profiles/built-in/nt51919-nt51929-nt51932-shared-facts/families/nt51929-nt51932.json) remains canonical. [Global disclosure](../../src/NvtFwCombiner.Infrastructure/Composition/CanonicalCapabilityDisclosureInventory.cs) now consumes an explicit admitted family binding independently of DP runtime maps. Existing badge and filename-only mismatch-suppression behavior is covered by the committed disclosure regression unit. |
| Shared execution and inspection | Preserve the [shared operation model and profile-owned access rules](../adr/0005-replace-personas-and-general-mapping.md). Remove only proven DP Replace-specific branches; keep reference initialization, range/CRC safety, metadata inspection and session behavior needed by the other workflows. |
| Historical Report / History | Existing report [history labels](../../src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHistoryEntryViewModel.cs) read stored experience/mode identity. Retain read-only interpretation of old DP Replace records and their input/mutation details; do not erase user history or keep an executable workflow solely to display it. |
| Tests, evidence and package catalogs | Retarget shared-engine/family/UI safety tests to surviving workflows and retain historical reports/oracles. Migrate trust indexes, policy and packages together: 24 bundles, 54 runtime registrations and 79 policy routes survive. Survivor bundle bytes, route fingerprints and decision identities remain exact; removing DP rows does not require repinning them. Preserve all 40 canonical Golden cases, output contracts and the approved allowlist. |

Implementation order: preserve shared facts and references first, remove the
experience-specific runtime/authoring surfaces second, then reconcile active
SPEC/ADR, support/help, policy and release-evidence applicability. In particular,
replace the stale runtime wording that awaits a `1.1.0` retirement decision.

Completion requires scoped catalog-load/family, selector/CLI, metadata/CMI,
session and old-report regression checks, plus byte-equivalence evidence for
affected surviving workflows. At release, execute **all applicable remaining
owner-certified Golden output cases** against the actual candidate. Retiring a
case requires the explicit feature-retirement/evidence decision; hiding the UI
or editing this TODO never makes a required case optional. Verify package
startup without removed registrations and disclose the intentional loss of the
DP Replace command/capability separately from the no-regression target for
surviving functionality.

## Work package: independent Golden evidence completion

### Deferred UI completion from `1.1.4` — 2026-09-10

The 2026-09-10 allocation moved remaining non-Memory-Layout work to `1.1.7`.
The current sequence distributes these retained boundaries across `1.2.4`
(first-entry/options), `1.2.5` (Report) and `1.2.6` (visual/native acceptance):

- First-entry IC selection: context lifetime, invalidation and Cancel/Back.
- Report physical-section grouping and historical replay compatibility.
- Per-page custom-options preview, approval and bounded implementation.
- Remaining broad screen inventory and evidence-driven shared visual/theme fixes.
- Native DPI, high-contrast and screen-reader coverage, including remaining
  System activity native acceptance. Already completed filtering/focus and
  compact-layout corrections are not reopened.

Keep the detailed boundaries in the linked `1.1.4` remaining-queue table and
existing UI handoffs. This defers feature work, not any mandatory safety,
Golden or packaging validation for an earlier actual release. The independent
Golden evidence work below is now allocated to `1.4.0`, separately from UI.

Supply independent expected output for the retained input-only canonical
cases, then re-review the fact-scoped aliases that depend on them. Reconcile
the exact intake against the current manifests rather than the historical
two-case/three-alias count. The owner-approved `v1.1.4` reference allowlist
contains 25 Direct Golden cases, three direct input-evidence cases and twelve
fact-scoped aliases. Its redistribution authorization supersedes the earlier
repository-only packaging restriction for that exact selection; it does not
grant output-parity certification or runtime-support promotion. Subsequent
release packaging continues to follow its own approved allowlist.
One topology, IC, workflow or fact-scoped alias never certifies another beyond
its approved evidence scope. Missing external evidence blocks this milestone;
it does not authorize generating expectations from the implementation.

## Work package: IC and firmware-evidence intake

Allocate the retained new-IC/firmware-evidence/capability intake to `1.4.1`, including
NT51950 AB `1 IC`/`Cascade`, selector-free NT51951 AB, Perfect-family and
`ldc-tp-only` evidence tracks. Inventory each track's existing implementation,
actual evidence gap and profile-owned contract before choosing its bounded
change. These are evidence/intake tracks, not claims that existing functions
are absent or that support is newly certified.

Each affected route retains profile, independent Golden and firmware-owner
gates; use `1.4.0` evidence only within its independently admitted scope.
Earlier firmware-affecting milestones still require their own applicable
evidence and cannot wait until this version to execute mandatory Golden cases.

## Work package: bounded Launcher hardening and development

2026-09-22 estimate (read-only assessment, not an implementation commitment):
the first tranche is approximately **6–12 single-person focused workdays**,
assuming inventory (1–2), two or three bounded corrections (3–6), and scoped
regression/package/review closure (2–4). The exact defect set is not yet chosen.
The complete Launcher scope through trust/security, controlled activation,
installer refinements and clean-Windows recovery/rollback acceptance is roughly
**28–50 focused workdays including that tranche**; external approvals and
environment/key access waiting time are excluded. Optional repository extraction
is excluded. Existing `ManagedDistributionLauncherHostServices`,
`ManagedDistributionLauncherRuntime`, `ManagedLauncherEntry` and
`VersionManagementExperience` already provide entry/setup/recovery, payload
verification, READY/rollback and version-management foundations; this is not a
from-zero estimate. No fresh test run was performed for this assessment.
Per the current release-sequence amendment, this work is outside the next
pre-`1.2.0`-scope delivery even if its release label is `1.2.0`.

Historical allocation: first actual development under the `1.2.0` planning
label; the 2026-09-22 amendment excludes that work from the next release and
leaves its next development slot unscheduled. Complete Launcher release remains
`2.0.0`. The data-refresh deadline below still applies to the next release,
not the later Launcher release.

Owner reminder, 2026-09-14: **before this release**, request/confirm the latest
public NT51950 DP Perspective, Desay DP Perspective and Event Buffer ID/name
table. The [data-refresh checklist](../ui/v1.1.x-custom-options-layout-handoff.md#owner-data-refresh-before-120-release--2026-09-14)
owns received/revision/applicability details. As of 2026-09-23, the latest public
NT51950/NT51951 Perspective is consolidated in the reference workbook and the
latest Event Buffer names are already applied; separate Desay source questions
remain unresolved. This reminder does not itself allocate an unknown firmware
implementation, reopen DP Replace or add a new automated release gate.

Launcher remains secondary to the `1.1.x` UI/performance priorities, but this
version begins real development: a comprehensive current defect, security and
evidence inventory, followed by one owner-approved, reviewable remediation
tranche. This is not merely an architecture or extraction review.

Inventory remaining Launcher/Installer refinements for `1.5.x` acceptance
and the `2.0.0` full release.
Preserve package identity, installed bytes, managed-version, verification,
recovery and rollback authority. Production activation remains NO-GO until the
separate security/evidence and activation gates below close.

## Work package: publisher trust, signing and security closure

The owner's 2026-09-23 request for differential updates, Launcher self-update
and future intranet migration is captured in the
[complete proposal](launcher-update-proposal-20260923.md). It is pending
discussion and does not admit implementation or alter this release allocation.
The expanded draft estimates 35–60 focused workdays, including a 10–18-day first
vertical slice; these supersede neither an approved schedule nor completed evidence.

Current allocation: `1.5.0`.

Address the publisher trust, signing and security/evidence gaps identified by
the `1.2.0` inventory as bounded reviewed changes. Key custody, signing
identity, external service configuration and permissions require their actual
owner approvals; no credential access, production deployment or trust-policy
relaxation is implied. Produce the independent R3 evidence required before
Catalog/Registry activation.

## Work package: conditional Catalog and Registry activation

Current allocation: `1.5.1` controlled preproduction validation, followed by
the separately approved production activation at the `2.0.0` release boundary.

Make the production GO/NO-GO decision only after the independent R3 security
and evidence closure passes and the owner approves the actual activation.
The planned version is not itself a GO decision. Revalidate package identity,
verification, recovery and rollback readiness; do not activate with missing
evidence or replace protected publication with an agent-side path.

## Work package: download minimization and Installer refinements

Current allocation: `1.5.2`, followed by integrated candidate acceptance in
`1.5.3`; neither is the full Launcher production release.

Address delta-download minimization and the remaining bounded Launcher/
Installer refinements from the `1.2.0` inventory. Preserve exact installed
bytes, package identity, verification, recovery and rollback semantics.
Measure transfer savings and failure/recovery behavior before making an
improvement claim; later-discovered unrelated work needs a new allocation.

## Work package: General Merge authoring

Current allocation: `1.3.0`.

Complete the retained General Merge authoring scope through the existing
typed mappings, profile compiler and shared planner/executor. Define the
bounded authoring gaps and acceptance from the current implementation before
coding; do not rebuild already-complete execution infrastructure.
Preserve profile-owned access, overlap, range, validation and integrity rules.
Saved/custom rule persistence is a separate `1.3.2` outcome.

## Work package: General Replace authoring

Current allocation: `1.3.1`.

Complete retained General Replace authoring through the same typed operation
model, immutable required reference and canonical access/postbuild policies.
UI/CLI cannot bypass TP, range, integrity or processor authority.

DP Replace is no longer an undecided item in this version: the owner decided
to retire it on 2026-09-09 and allocated the
[retirement and compatibility checks now in `1.1.10`](#dp-replace-retirement--owner-decision-2026-09-09).
General Replace continues to use the shared Replace engine; this does not
implicitly reopen the retired DP Replace experience.

## Work package: saved and customized rule authoring

Current allocation: `1.3.2`.

After the General authoring contracts are settled, complete saved/customized
rule authoring, persistence, import and validation through the existing typed
operation model. Preserve validation on load/import and the same execution
owner; arbitrary scripts and per-run executable paths remain forbidden.
Rule/schema/migration details require their existing contract review before
implementation.

## Work package: CLI completion and deterministic UI automation

Current allocation: `1.2.3`, before the remaining broad UI acceptance.

Complete the command-line surface after inventorying the already-shipped CLI
commands and Desktop launch options. Extend existing owners rather than adding
a second parser or execution path: firmware Preview/Build commands continue to
use the shared Application planner/executor, while Desktop-only navigation and
capture options remain non-semantic Presentation startup controls.

The minimum Desktop automation outcome extends the existing `--load-report`
(`--report`) plus `--open-report` path so one bounded command can select a
report tab such as `Changes`, choose an approved deterministic visual state,
write a screenshot to an explicit destination, return a meaningful exit code,
and close without file-picker or pointer automation. Add stable help and
argument-error behavior, path/overwrite bounds, and focused launch-to-capture
tests. Headless rendering may be used for test evidence, but it must render the
same XAML and ViewModels as the packaged Desktop application.

This milestone also reconciles missing workflow CLI coverage against the
existing typed authoring contracts. It does not authorize CLI-owned firmware
semantics, arbitrary scripts, bypassing profile/range/integrity policy, or
turning visual test fixtures into product inputs. Keep interactive computer
automation only for behavior that genuinely requires native interaction.

## Work package: IC rule authoring and Launcher extraction review

### IC family / rule-authoring UI

Historical owner decision on 2026-09-05 deferred this feature to `1.3.0`.
The current sequence assigns the maintainer UI to `1.3.3`, after General
Merge/Replace and saved/custom user rules in `1.3.0`-`1.3.2`. This schedules
the maintenance feature, not an already-approved screen specification.

The [SPEC](../../SPEC.md) describes a future maintainer-facing editor that
could create, validate and export untrusted IC-definition bundle candidates:
family membership, declared memory maps and reusable profile/rule definitions.
This is different from selecting an already supported IC or editing one
General Merge/Replace mapping and saving that user rule. It is not an
existing-screen correction omitted from `1.1.4`.

Define the bounded editing/export scope before implementation, and admit the
required trusted-bundle/evidence models and IC/profile/rule contracts.
Candidates remain untrusted until independent review, CI/evidence and
firmware-owner approval promote exact bytes through the existing trust index.
The UI cannot mutate the live catalog, grant support or approve its own rules.

### Launcher and publication-system extraction review

In `1.4.2`, after `1.2.0` begins the first remediation tranche, review whether Launcher
and release/publication infrastructure can move to an independently versioned
repository. This is not the first Launcher delivery, and extraction is not
pre-approved.

An ADR must first define public contracts, migration/deletion milestones,
repository trust boundary and rollback. Do not duplicate NVT FW Combiner's
managed-version semantics or move firmware facts, composition, profiles,
Golden authority or product-specific support policy into the extracted owner.

## 1.2.1 Python Combiner intake — 2026-09-21

Owner requested these TODOs for `1.2.1`; this is planning intake, not a tool
binding change or an approved new firmware mode. Existing `1.2.1` allocations
remain; the current `1.1.10` / planned `1.2.0` AB Replace work stays separate.

- [ ] Replace the current legacy Combiner with
  [Dennis40816/nvt_combiner](https://github.com/Dennis40816/nvt_combiner), the
  Python implementation that can be packaged as `Combiner.exe`. Intake source:
  `d7b08b92d1e566a0fc4066005e3e058d60ecd25f`, product version `2.0.0.1`, legacy
  console banner `1.13.0.0`; the deployment candidate must be selected and pinned
  during implementation. Inventory every currently used mode/argument contract,
  compare complete outputs against the fixed legacy tool and applicable owner
  Goldens, and verify package/runtime readiness, failures and rollback. Reuse
  the existing processor adapter and host write-range audit; preserve independent
  legacy reference evidence. The Python repo's existing differential records
  have been read, not rerun or accepted as coverage of every current route.
- [ ] Assess a dedicated B-bank CRC/postbuild mode in that repo. Compare a
  CRC-only operation with a complete local-address normalization → existing
  postbuild → B-address restoration/family CRC operation. CRC-only may be
  insufficient: the observed 929 failure occurs at the Backup copy before CRC.
  Evaluate explicit profile-bound bank geometry, exact read/write bounds,
  preservation of B's own unselected content/version and byte-for-byte retention
  of the unselected bank. Cover 929's three relocated fields outside Header CRC
  separately from 950/951's relocated fields within Header CRC; do not introduce
  one unconditional CRC rule. Mode name, arguments, CRC-only versus full
  postbuild scope, implementation owner and integration contract remain pending.

The linked repo currently records AB Merge and range-safety evidence, not
complete AB CtrlRAM Replace validation. Final design and implementation
admission follow that evaluation; this intake does not create GitHub issues,
modify either runtime or promote support. The related current delivery entry is
[1.1.10](../ui/v1.1.10-delivery.md#121-combiner-後續規劃--2026-09-21).

## Explicit owner-unallocated queue

All retained items inventoried through the 2026-09-14 owner resequencing have
allocations in the current sequence above. Execution prerequisites and external-evidence
blockers stay with those milestones; a scheduled item is not automatically
approved for implementation, support promotion, publication or activation.
New findings require explicit allocation rather than silently expanding a
release.

### Historical tracker reconciliation

| Retained issue | Current allocation and reconciliation boundary |
| --- | --- |
| [#380 preload evidence/release](https://github.com/Dennis40816/nvt_fw_combiner/issues/380) | Preserve completed `1.1.3`/`1.1.5` CI/performance history; Home startup/catalog-ready optimization is now `1.1.12`, provenance reconciliation remains `1.2.7`, and conditional CtrlRAM cold first-open/F14/F15 follow-up remains `1.2.8`. Do not restore its old five-minute CI target or re-release `0.10.5`. |
| [#291 theme audit](https://github.com/Dennis40816/nvt_fw_combiner/issues/291) | Remaining existing-surface theme and native-accessibility audit belongs to `1.2.6`; preserve shipped `1.1.4` corrections and their evidence. |
| [#2 early UI planning](https://github.com/Dennis40816/nvt_fw_combiner/issues/2) | Reconcile the early umbrella in `1.2.7`; remaining existing-screen work follows `1.1.7` and `1.2.4`-`1.2.6`, with new authoring in `1.3.x`. Do not redo completed demo/shell work. |
| [#1 early core implementation](https://github.com/Dennis40816/nvt_fw_combiner/issues/1) | Reconcile the early umbrella in `1.2.7`; an old open item is not evidence that the current compiler/planner/executor is missing. Retain any genuine unmet acceptance criteria. |

GitHub still owns live open/closed state. This table allocates work and does
not close, relabel or rewrite the issues.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
