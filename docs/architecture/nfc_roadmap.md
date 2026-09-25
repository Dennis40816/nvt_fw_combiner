# NFC Roadmap

Status: active owner roadmap; release-closure checkpoint 2026-09-01; subsequent owner allocation amendments are recorded below.

Current evidence checkpoint, 2026-09-19: **`v1.1.8` is published** at `a2273c8798beb43b217b0ecfb9275af8fe4f9f96`; see the [release closure](../references/verification-report.md#118-published-release-closure--2026-09-19). Earlier candidate checkpoints below remain dated history, not open release gates. The latest planning input is `codex/1.1.9-intake` at `8344cb69`, which already allocates F01–F26 beyond the documents shipped in the release. The [post-release reconciliation](../ui/v1.1.x-custom-options-layout-handoff.md#post-118-audit-reconciliation--2026-09-19) maps the new AUD/HG reference to that existing schedule; it does not authorize runtime implementation, create a watcher, or replace the version sequence.

## Current release sequence — 2026-09-14

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

- measured startup optimization: deduplicated schema validation, parsing and
  compilation, and bounded parallel bundle preloading under
  [ADR 0075](../adr/0075-bounded-catalog-bundle-preload.md). The compressed
  composite ReadyToRun package shape and its 80,000,000-byte EXE ceiling are
  kept, so the first-window target is not expected to be met; the measured
  outcome against both hard targets is recorded in the `1.1.12` CHANGELOG
  entry at the release freeze. The owner accepted a 2-4 MB peak private-bytes
  increase over `1.1.11` for the parallel preload;
- F07: a committed output keeps its receipt when its delivery or report is
  interrupted;
- removal of the hard NT51950/NT51951 CtrlRAM size limits for Display OSD
  inputs (scoped R3 change; the owner required it in `1.1.12` on 2026-09-26),
  with AB Bases decided by two NVT markers. Base classification recognizes the
  published Standard lengths (256 KiB, 512 KiB, 1 MiB); other lengths stay
  rejected unless the owner decides otherwise;
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
| `1.1.12` | **Output/persistence correctness and measured Home startup optimization**: F07/F08, residual F20/F21; preserve committed receipts and existing destinations, expose retryable errors. Hard targets from process launch: visible first window within 500 ms, all startup loading within 2,000 ms; nonessential work in background. Use comparable per-launch timing and first-navigation regression evidence; see the 2026-09-25 startup amendment. TP Header SVN common modeling awaits owner confirmation; the scoped NT51950 Normal/Both Header backup CRC investigation follows its linked deferral amendment. 2026-09-26 release scope: see [Owner release scope for 1.1.12](#owner-release-scope-for-1112--2026-09-26); F08, residual F20/F21, TP Header SVN modeling and the Header backup CRC investigation move to `1.1.13`. |
| `1.1.13` | **Items moved from 1.1.12, then process cancellation and window lifetime**: the 2026-09-26 [moved items](#owner-release-scope-for-1112--2026-09-26) first; then F03/F06 before F01/F02/F25; bounded termination and recovery. |
| `1.1.14` | **Controlled diagnostics and repair regression**: F17/F23/F26, demonstrated F16 defects; retain F18 evidence throughout. |
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
| `1.1.12` | **Output and persistence truth**: F07/F08 and remaining F20/F21; Home startup optimization, deferred TP Header SVN modeling and the scoped Header backup CRC investigation follow the 2026-09-25 amendments. | Preserve committed output receipt after cancellation; show retryable save failures; local Report replacement preserves the original destination on precommit failure. Keep shipped snapshot/reentry/disposal fixes. Startup acceptance follows the linked handoff's comparable stage timings and navigation checks. Confirm Header contents with the owner before SVN model implementation. |
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

## `0.10.0`: planning and governance baseline

`0.10.0` reconciles its original `v0.9.15` planning baseline with the complete
reviewed `v0.9.16` hot-fix. Its scope is M0/M1 inventory, accepted
architecture and terminology, skill/workflow governance, validation standards,
the owner-updated FlashMap evidence reference, and the approved ticket
dependency plan.

It does not allocate or implement a production Support Matrix,
release-recovery, Error, Report, or maintainability slice. The annotated-tag
newline defect and visible clean-Windows UI-smoke gap remain explicit in the
verification report; neither is represented as closed by this planning
release.

## `0.10.1` through `0.10.6`: owner-allocated implementation

The approved GitHub issues named in the
[`0.10.x` ticket dependency plan](../governance/0.10.x-ticket-dependency-plan.md)
and each issue's `Blocked by` edges control the implementation frontier. Issue
numbers are not assumed to be a contiguous range; later approved tickets such
as #207, #214, #219, and #221 remain first-class program work. Dependency depth
is not a release version. The owner allocates only dependency-ready,
reviewable slices after considering risk, evidence, file ownership, and
available reviewers. The owner release allocation recorded on 2026-08-04 is:

1. `v0.10.1` closes the headless canonical foundation.
2. `v0.10.2` publishes the reviewed desktop adoption through #208, the shallow
   shell and shared read-only Hex viewport, and the first General/Saved Rule
   compatibility deletion through #254 as a support-neutral stable checkpoint.
3. `v0.10.3` completes the remaining approved refactoring graph through #197
   and LAR-00, including LAR-01 through LAR-12, zero Workbench/renamed parallel
   owners, one production path per module, all four Core Convergence ledgers,
   reviewed line-addressed residuals, and exact descending ratchets.
4. `v0.10.4` re-measured that result, audited whether ownership or code could be
   removed or expressed more simply without weakening evidence, and measured the
   exact packaged Home-window median against 700 ms on the controlled owner
   machine. The stable package did not reproduce 700 ms, so that absolute target
   remains an explicit performance residual. Shell construction does not
   synchronously publish the canonical capability catalog merely to show Home.
5. `v0.10.5` executes the approved `PL-01` through `PL-07` graph and `PL-00`
   terminal gate: one observable, cancellable, bounded, and user-controllable
   preload lifecycle, with selection-triggered inspection retaining its own
   workflow generation and typed semantic owner.
6. `v0.10.6` reserves a configured-path update screen and owns the managed
   version experience: a stable
   launcher, side-by-side content-verified payloads, explicit
   install/switch/delete, startup-readiness rollback, offline selection, and a
   unified Settings Version page. The owner-approved contract is
   `docs/specs/v0.10.6-version-management.md` and accepted ADR 0051.

The complete refactoring release `v0.10.3` closed #197 and records the applicable
architecture, firmware-owner, Golden, deterministic package/provenance,
protected-CI, and release-owner gates before publication. Its published
changelog retained visible clean-Windows smoke as an external attestation; this
roadmap does not reconstruct or independently claim that evidence. The former
fixed total and slice targets are dated planning benchmarks, not release gates.
No roadmap entry may waive the retained gates or move, overwrite, or redefine an
existing stable tag or asset.
The later audit, performance, and update-experience releases do not reopen #197
or weaken its retained gate.

## `1.0.0`: supported release and `1.0.1` upgrade validation

`1.0.0` is the first owner-approved supported distribution. It must retain the
fixed multi-path update-source registry, recoverable managed Launcher,
CtrlRAM TP/full-base routes, and the exact Standard Merge, AB Merge, and
CtrlRAM support evidence admitted by the release gate. Repository-wide analyzer
cleanup is not part of this release and cannot be used to delay or weaken its
firmware, update, package, or clean-Windows evidence.

Before publishing `1.0.0`, one isolated validation lineage produces a genuine
`1.0.1` package from different reviewed source identity. The pair must prove
catalog discovery, package and inner-manifest verification, install, READY
activation, restart, switch-back, rollback, damaged-version reporting, and
explicit deletion. Renaming the `1.0.0` ZIP, directory, executable, manifest,
or catalog entry is not a valid `1.0.1` package. The validation package is not
an official stable release unless the owner separately approves publication.

## `1.1.0`: published manual-only Windows baseline

`v1.1.0` was published on 2026-09-01 as the bounded direct-run Windows x64
distribution. It preserves the frozen `v1.0.8` Application behavior and changes
no firmware semantics, profiles, ranges, processors, support decisions, output
bytes, or output naming. The observed tag/tree identity, reviewed-tree
equivalence, CI observations, exact three-asset publication, provenance,
smoke, release-note, retry, waiver, enforcement gaps, and residual facts are
recorded only in the canonical
[`verification report`](../references/verification-report.md); this roadmap does
not duplicate that release-closure authority.

Publication of `v1.1.0` does not allocate any former product-expansion,
analyzer, UI, evidence, or delivery backlog to that released version.

## `1.1.1`: verification, test, CI, and release architecture only

`v1.1.1` owns only the verification, test, CI, and release architecture review
reserved by the single-use `v1.1.0` waiver. It must restore a passing canonical
full gate or replace that gate through separately owner-approved architecture;
close the shadow-root and capability-history failure modes; add the durable
committed mutation matrix; measure and reduce test runtime by first profiling
lane and fixture cost, then reusing accepted component evidence, improving
narrow-test selection, and removing duplicated setup; and preserve exact
failure, retry, waiver, and residual evidence without hiding coverage,
weakening a release gate, or replacing the frozen-candidate full verifier with
an unproven shortcut.

The original allocation also included CI-flow optimization. The 2026-09-05
owner decision moves every unfinished `1.1.x` CI/release improvement to
`v1.1.3`, including these residuals; the preceding `v1.1.1` scope is historical
allocation, not a competing current work queue or a reopened release.

No product feature, firmware behavior, support promotion, selector/Settings
change, Memory Layout review, generic Golden-evidence closure, Installer or
Launcher refinement, or former product-expansion item belongs to `v1.1.1`.

## `1.1.2`: support-neutral repository and DPCMI convergence

`v1.1.2` is the approved support-neutral release-candidate scope for completed
whole-repository document convergence and boilerplate cleanup; bounded verifier
script sharding/deadline work without a skipped gate or second verifier; the AB
selector cold-start/Mode/IC-menu correction without support promotion; public
certified NT51929 input-only evidence without expected-output, Golden-parity,
or support claims; and Standard Merge DPCMI CMD1 Page 0 authority for the
NT51919/NT51929/NT51932 perfect family. Standard reads only
`[0x401A,0x401D)`; AB retains `[0x401A,0x401D)` and `[0x4401A,0x4401D)`;
General has no DPCMI reader. The intended naming correction is `D0200` to
`D2004` while NT51929 Golden output bytes and SHA remain unchanged.

The [`v1.1.2 repository document convergence handoff`](v1.1.2-repository-document-convergence-handoff.md)
and [manifest](v1.1.2-repository-document-convergence-manifest.md) preserve
D1-D7 and C1 final historical evidence, frozen identities, and the completed
boilerplate cleanup. They do not create an inventory, TODO, or archive
framework and do not authorize further cleanup. FWConfig and unrelated metadata
are non-goals. Dummy DP was deferred past `v1.1.2`; its current allocation is
`v1.1.4` below. Launcher
remains secondary; bounded development begins in `v1.2.0`, with activation
separately gated. This version cannot be called released until full verification,
protected CI, package and smoke checks, release approval, publication, and
fresh-download verification complete as mandatory no-waiver gates.

## `1.1.3`: consolidated CI and release optimization

Published closure (2026-09-06 Taiwan time): PR #426 merged the reviewed tree to
`e5202e2707d272076d24216222188d478314a07d`; actual-source CI passed in 416 s.
Release run `33974287659` published immutable `v1.1.3` with all 25 certified
Golden outputs executed and public-download smoke passed, not skipped. The
owner-authorized Codex approval kept the protected environment unchanged.
Candidate execution took 545 s; original dispatch through public smoke took
4,641 s including review reconciliation and approval waits, so the actual
end-to-end run did not achieve a reduction. The
[verification report](../references/verification-report.md#actual-timing-including-the-delay)
owns the exact phase table, adjusted comparison and evidence limitations.
The allocation and pre-freeze checkpoints below describe this completed scope;
their then-pending gates are historical, not current blockers. The next planned
version remains `1.1.4`; publication does not automatically start its backlog.
Post-release owner amendment on 2026-09-06 assigns the newly assessed local
full-verifier parallelization to `1.1.5` below. This supersedes the earlier
all-CI/release-in-`1.1.3` allocation only for that follow-up; the published
`1.1.3` implementation and evidence remain unchanged.

Owner decision on 2026-09-05: complete all remaining CI/release optimizations
allocated anywhere in `1.1.x` in `v1.1.3`; move the UI work previously assigned
to `v1.1.3` to `v1.1.4`. The subsequent same-day owner resequencing below
consolidates all existing UI corrections in `v1.1.4` and allocates the remaining
backlog without pulling UI, AI-skill, firmware or Launcher work into `v1.1.3`.
Target roughly 10 minutes of complete CI plus release packaging;
this is a measurement target, not an achieved runtime or a relaxed deadline.
Every release still freshly executes every applicable certified Golden output
case. Only non-Golden source verification may reuse successful exact-source CI;
PR equal-tree evidence, hashes alone and an older green run are insufficient.

This is the single current allocation for the following work; linked contracts
remain the behavior owners, and completed historical evidence is not rewritten.

| Workstream | Completion criterion |
| --- | --- |
| Pipeline duplication and runtime | Measure PR, exact-source `main`, manual package-preview and release-candidate paths; remove duplicate orchestration/setup and reuse only admitted exact-source non-Golden evidence. |
| Isolation and determinism | Safely parallelize independent CI lanes, retain local serial fallback, and close the existing four-seed Infrastructure evidence gate before removing its temporary seed pin. |
| Mechanical preflight | Synchronize all registered source-derived projections before formal verification, without regenerating Golden expectations or approvals. |
| Governance and document-check burden | Resolve the reviewed ten-file AGENTS batch through bounded owner-authorized admission; retain useful safety/traceability guards and remove unnecessary prose-snapshot verification. Assess remaining mechanism costs with evidence. |
| Package and publication | Preserve approved package contents and runtime behavior, review build/setup duplication, complete version/release notes, and pass immutable publication plus public-download smoke. |
| CI/release document convergence | Reconcile active CI/release instructions, release-evidence entry points and these version assignments; this part moves from the former `1.1.14` documentation allocation, while remaining SPEC/dead-code work is now combined with agent workflow work in `1.1.6`. |
| Measured result | Report complete source-CI and release elapsed times, phase/queue/approval breakdowns and the measured historical release reduction below. |

`scripts/verify.py` remains the only repository verifier. Windows-only execution,
Golden whole-output comparisons, write ranges, package allowlists,
SBOM/provenance, stable required checks, branch protection and fresh-download
verification remain enforced. This consolidation does not pre-approve a cache,
alter packaging flags or activate a Launcher distribution system.

The first local batch fixes published-smoke skip propagation, binds release
admission to the actual source's latest complete successful CI workflow/attempt,
and reuses the existing verifier for fresh complete Bootstrap/GoldenRegression
projects. It also removes the repeated-timeout test's wall-clock dependency.
These are local changes under verification, not integrated or released results.
Packaging, independent public download/smoke, protected approval and immutable
publication boundaries remain. No firmware bytes, expected outputs or UI
behavior change in this CI batch.

The next owner-approved local efficiency batch isolates the existing three
repository-script shards on separate Windows CI runners. The unchanged required
`python-worker / verify` check rejects any non-success shard result before
running the worker-only lane. All local full-verifier lanes remain serial.
Release Golden omits only repeated ownership/format prechecks and unused
Coverlet collection; full solution restore/build, immutable output checks,
discovery/TRX reconciliation and all certified output cases remain. A proposed
two-project-only build was rejected: its dependency closure omits four production
outputs required by the existing shadow validation. No new scheduler, retry,
case filter, or shortened Golden comparison was introduced.

The owner also approved source-derived preflight automation. The common
[`sync_derived.py` tool](../../scripts/sync_derived.py) has four fixed providers:
live workflow-contract identity projections, the exact CI documentation mirror,
approved policy/trust-index/Golden-allowlist consumer pins, and the two numeric document-version
headers derived from authorized `VERSION`. Status prose, dates and historical
results are not version projections. Authorized local changes are
synchronized before formal tests; the existing structure lane checks all
providers first without writing. Golden expectations, historical evidence and
approvals are not regenerated. Existing artifact-stage generators remain their
sole owners. [Contributing](../../CONTRIBUTING.md#derived-file-preflight) owns
the commands; [ADR 0068](../adr/0068-derived-file-synchronization.md) owns the
single-writer boundary. The live workflow contract and CI mirror are synchronized;
current policy/index payloads and their trust pins are unchanged.
The owner renewed the unchanged 35-case/158-file Golden reference selection for
`v1.1.3` on 2026-09-05. Only its authorized version/date changed; the tool updated
the two existing package/smoke raw-SHA pins, then a second write and the
all-provider read-only check reported zero changes. Canonical payloads and
historical evidence remain unchanged; this does not replace final release gates.
The structure adapter also selects historical parity H2 from its existing
immutable final-review record, not the last edit to the mixed current/historical
plan. The original H1/H2 and exact H1-H4 package-source checks remain unchanged;
current workflow fingerprint synchronization does not renew old certification.

Pre-freeze local evidence on 2026-09-05 (final closure is linked above):

| Check | Result |
| --- | --- |
| Combined local `--all` candidate | FAIL: the whole .NET lane reached its unchanged 900 s deadline. Structure (134.7 s), all three script shards (414/155/367 tests; five existing platform skips in the last shard) and the 30-test CRC worker passed. Seven .NET projects produced fresh passing TRX for all 4,494 tests; Infrastructure produced only a start log and no final TRX. No complete coverage/freshness or full-pass claim follows. |
| Infrastructure-only diagnostic after that failure | Existing canonical session/shadow/discovery/coverage collector passed 1,070 identities (1,068 passed, two existing platform skips) in 125.9 s with detailed VSTest diagnostics and a no-dump hang observer; cleanup completed. This isolated pass does not reproduce or close the full-lane stall. Preserve both runs and diagnose the ordering/concurrency/lifetime difference before another full gate. |
| Complete .NET diagnostic with native long-running messages | `--skip-structure --skip-python` passed in 592.5 s after the background-test fixture changes: all eight projects, 5,564 identities, 5,562 passed and two existing Unix-only skips. Line coverage was 89.46% (75,703/84,624), branch coverage 78.04% (24,798/31,774); freshness and exact-session cleanup passed. This run did not reproduce the earlier stall; it is neither a root-cause fix claim, final `--all`, nor GitHub CI/release timing. |
| Pinned .NET 10.0.301 `--release-golden` | Earlier method-level run: 88.9 s end to end. After per-case identity correction, the fresh complete run passed all 25 canonical case identities, 1,172 Bootstrap + 25 GoldenRegression tests, zero skips and cleanup. Standard cases are separate theory rows; direct versus TP-only/alias checks retain separate executions. Full build took 14.2 s; test lanes 54.4/9.5 s. These phase measurements are not a complete release stopwatch. |
| Per-case Golden gate and public source-CI projection | The old method-only gate failed the missing/shared-case regression; all 197 orchestration tests now pass with four existing platform skips. Fresh TRX exposed three direct/TP-or-alias methods needing distinct test names; their comparisons remain unchanged and the strict 25-case gate now passes. Canonical validation: 80 passed. Promotion policy: 71 passed, including sentinel exclusion from collected source-CI and candidate manifest; no raw actor/author/runner/step metadata is published. |
| Worker-only CI lane | 13.0 s; 30 tests, 100% line/branch coverage. |
| Promotion/package policy | Complete 148-test rerun passed in 359.7 s. |
| Repository scripts s-z after workflow-contract synchronization | Complete 341-test shard passed in 225.6 s, five expected platform skips; measured before the later generic-tool additions. |
| Generic automation, parity approval and coverage-CI contract | 58 tests passed in 115.5 s, zero skips; includes pre-write convergence, protected-authority rejection and fail-fast verifier ordering. |
| Real synchronization | All-provider check passes; repeated selected writes change zero files. The structure preflight took 0.8 s. |
| Golden redistribution renewal and historical binding | Canonical Golden validation: 80 tests passed; common sync: 23 passed; package policy: 78 passed in 319.4 s. The historical-selector regression first reproduced the planned-commit failure, then the complete 46-test parity-contract module passed in 79.1 s with original H1/H2 and H1-H4 rejection tests retained. Committed-candidate and final release gates remain required. |
| Roadmap-test simplification | Reproduced the stale-heading failure; replaced the first method's paragraph snapshots with canonical routing/version-structure checks. Full Architecture project passed 256/256 with zero skips before the final compact later-version presence guard. Actual Golden/package/firmware tests remain unchanged. |
| Infrastructure seed deletion gate | Seeds 1, 1738590270, 460925769 and 1950330798 passed in 107.9/108.3/106.5/109.6 s. Each reconciled 1,070 identities, 1,068 passed and the two existing approved Unix-only skips, with zero failures and normal freshness/coverage/cleanup. |
| Temporary seed removal | Existing command regression failed with the old pin, then passed after removing only the seed. Complete orchestration module: 195 tests in 15.5 s, four expected platform skips. Both serialization settings and all identity/coverage gates remain. |
| Ordinary-document workflow | Root/docs instructions, CONTRIBUTING, execution runbook and Polytail policy agree on the R0 diff/affected-link short path. Thirteen existing authority-boundary regressions passed in 12.0 s; affected links and scoped review passed. Governed documents and all release/Golden gates remain protected. |
| Version-header synchronization | All 21 common-tool tests passed, including no-other-bytes/idempotence and negative header/SDK checks. The actual write updated only the two numeric headers; subsequent all-provider check and selected write changed zero files. The historical tag index no longer needs a duplicate current-version entry. |
| Path-state Git transport | All 114 governance tests passed in 197.5 s, including frozen v1 digest equality, corruption/deletion/mode/duplicate-path handling and existing historical tamper gates. Only exact-read transport is batched; no digest or historical authority is changed. |

The 70 affected Application clock/registry tests also passed. Workflow validation
and Ruff passed. The earlier scoped review returned `PASS-WITH-HUMAN-GATE`.
Pre-freeze review then found the per-case identity and raw source-CI metadata
gaps recorded above; both are corrected locally and independent incremental
reviews returned `PASS-WITH-HUMAN-GATE` with no P0-P3 findings. The resulting
pre-freeze structure check passed in 144.7 s. Integration and release-owner gates
are not waived.
After the owner-authorized, personally reviewed ten-file AGENTS batch received
one aggregate [R2 admission](../governance/change-records/AGENTS-113-SCOPE-RECONCILIATION-01.json),
the latest `--structure-only` passed in 135.3 s after the Git-read and version-header
corrections, including a 0.7 s derived-file check. The preceding local structure
pass was 198.7 s: these single-machine samples show about 32% less structure-lane
time, not a measured full-CI or release improvement.
No classifier, historical record, Golden expectation or release permission was
relaxed. The previous missing-record blocker and four-seed deletion gate are
closed; frozen-head integration and release gates still remain.

Mechanism assessment: exact changed-source/review binding and tamper detection
remain useful for firmware/release authority (ADRs 0054/0059/0061). A filename-only
R2 classification cannot distinguish instruction wording from a real permission
change; this batch's ten-file barrier is evidence of that overbroad scope, not a
reason to disable all authority checks. The existing aggregate admission resolves
this occurrence without a new bypass mechanism. Paragraph snapshot assertions
have been reduced. Ordinary non-normative, non-classifier-governed prose now
uses diff/affected-link review, with extra checks for layout or parsed consumers;
it needs no issue, code-size census, full-suite run or separate handoff artifact.
This does not reclassify AGENTS/governance or normative release/permission rules.
A read-only caller profile measured 1,228 per-path Git processes taking 67.2 s
within the path-state digest. That transport now uses at most two Git processes
per record with unchanged v1 digest bytes; the full governance module passes.
No immutable-history check has been removed. This local optimization does not
yet establish the complete GitHub CI/release elapsed-time reduction.
The initial local Golden baseline recorded about 62 s of build-related commands
and 118.4/28.2 s test lanes, but no complete stopwatch; it cannot establish a
total percentage reduction. These local results are not a fresh GitHub
CI/package/publication measurement.

Read-only inspection of historical release run `33905351419` confirms the
319-second packaging step includes Desktop self-contained/ReadyToRun publish
(about 184 seconds from clean completion to app publish), managed launcher,
worker packaging (about 10 seconds), ZIP creation, and subsequent distribution
launcher packaging. No packaging flags or artifacts were changed in this batch;
disabling ReadyToRun would require a separately measured startup/package tradeoff.

Owner acceptance now explicitly requires the **complete release time reduction**,
not only the Golden lane. Historical run
[`33905351419`](https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/33905351419)
took **31 min 15 s** from creation to completion: source verification 1,303 s,
packaging 319 s and promotion 65 s, with remaining setup/transfer/queue time.
Its `published-smoke` job was skipped, so it is not a complete smoke-equivalent
baseline. Measure the new admitted source from release dispatch through successful
public-download smoke, report phase durations and queue/approval waits separately,
and calculate `(1875 - newElapsedSeconds) / 1875 * 100`. Label this as comparison
with the historical observed workflow, explicitly noting the added smoke gate.
Do not substitute local Golden time for GitHub time or claim an achieved reduction
before that authorized run. The separate source-CI duration must also be reported
so moving work before release is not represented as deleting it.

`v1.1.3` completed the four-seed Infrastructure gate and removed only the
temporary seed pin. This sample is not a claim that all possible orderings are
deterministic; final verification without the pin remains required. Shared
restore/lock-file mutation must be isolated before parallelizing script tests
against .NET builds. The exact
residual [VERIFY-111-XUNIT-SEED-01](../governance/change-records/VERIFY-111-XUNIT-SEED-01.json)
is immutable historical evidence; its former `v1.1.2` allocation is superseded
to `v1.1.3` by the current owner decision.

## `1.1.4`: UI corrections and AB Dummy DP

Latest owner decision on 2026-09-05: consolidate all existing UI corrections
formerly split across `1.1.4`-`1.1.12`, plus the retained theme audit, into
`1.1.4`. This supersedes the earlier same-day decision to move only the
former `1.1.3` UI. The owner delegated the remaining task allocation below.
The published `1.1.3` CI/release scope includes none of these UI changes.
The later owner amendment adds the AB Dummy DP checkbox and its complete
firmware behavior to this same release; it is not a visual-only change.

Original design checkpoint (2026-09-06; Git status below is historical):
the owner requested improvement mockups
before implementation and will approve the reference to be implemented. The
owner clarified that proposals must start from an actually opened published
`v1.1.3` app and captured full-page screenshots, not invented layouts. Preserve
the observed shell, proportions and controls except for each approved correction.
Start with screenshot-based visual proposals, not production UI edits; reference approval remains
separate from unresolved navigation lifetime or Dummy DP firmware decisions.
The owner subsequently approved the final screenshot-based AB DP four-field
proposal for step 1; its baseline, approval scope and retained geometry are
recorded in the [metadata handoff](../ui/v1.1.x-ab-dp-metadata-layout-handoff.md#approved-reference).
Step 1 is now locally implemented and verified: 15 narrow tests and all 869 UI
Smoke cases passed, scoped review passed, and actual desktop/compact captures
are recorded in that handoff. It is not committed, integrated or published.
This completion does not approve references for the other UI items.
Step 2 was subsequently authorized, including an owner amendment to review input
errors and lead Report Summary with plain-language descriptions. It is locally
implemented and reviewed, including the later approved shared Error/Warning/
disabled-Build cards and typed size/range/repeated-byte diagnostics: 912 UI Smoke
cases passed, followed by 12 focused cases after the final tooltip-padding fix;
1,395 Application and six diagnostic inspection cases also passed. The initial
893 UI/30 inspection results remain dated evidence in the handoff. No firmware
bytes or Build-readiness policy changed; a fresh NT51929 control output matches
its certified Golden byte-for-byte. The
[feedback handoff](../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md)
owns the actual capture, code inventory and review evidence. It remains
uncommitted, not integrated or published. That review also recorded a separate
Report import assessment for step 7: noncanonical/missing report fields must not
mislead users with a success label; parser/legacy admission is not changed in
the localized-feedback batch.

The owner also deferred test-architecture documentation to `1.1.4`: draw the
current test items, source locations, test counts and parallel/serial dependencies
across local verification, CI and release, and create `tests/README.md` with the
retained `v1.1.3` per-item measured results. Distinguish test counts, command/job
wall times, overlapping lanes, retries and queue/approval waits; label unmeasured
stages explicitly. This documentation does not block `v1.1.3` or authorize test
architecture changes, extra test runs or a separate documentation framework.

Implement the following as bounded changes within one release, using the
existing controls and semantic owners. This allocation does not approve a
new visual reference, a state-lifetime decision, or a firmware/delivery
contract change. Close those existing gates before the affected implementation.

Execution-order amendment (2026-09-06): the owner requests small, bounded
corrections first, then a broad real-screen inventory and reprioritization,
followed by one-at-a-time implementation. The numbered rows split the existing
scope into smaller steps, not additional features. A subsequent owner amendment
moves CtrlRAM selector, Report-owned Load report, bundle primary-output rename
and complete AB Dummy DP into the first phase. The table below retains stable
scope identifiers for those allocations; the later small-impact-first queue
below supersedes its original execution order. Priority
does not reclassify the delivery/firmware changes as small UI-only fixes or
waive their existing owner decisions, review and Golden evidence.

| Scope ID | Workstream | Retained scope and acceptance boundary |
| --- | --- | --- |
| 1 | AB DP metadata | [Metadata handoff](../ui/v1.1.x-ab-dp-metadata-layout-handoff.md): DP1/DP2 Version and optional Jira Index layout, long/missing values, unknown-bank feedback and unused width. Reuse the shared four-/two-column facts layout. The AB IC selector/Mode correction already completed in `1.1.2` is not reopened. |
| 2 | Standard Merge feedback | Locally implemented and verified. [Verification-feedback handoff](../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md): shared compact Error/Warning/disabled-Build cards; concise minimum size and actual repeated byte; consistent Report Summary plus exact facts/original diagnostics in Issues. The approved additive typed Application evidence extension is included. No change to severity, `BlocksBuild`, validation ownership or bytes; no pre-Build fabricated report. Actual screenshots and scoped test/Golden evidence are retained. |
| 3 | Perfect-family filename hint | Locally implemented and verified (2026-09-06). Suppress the filename-read IC mismatch hint when the compared ICs belong to the same declared complete Perfect family; retain it for other relationships, including partial/shared-fact families. [ADR 0041 amendment](../adr/0041-perfect-family-and-typed-shared-fact-relationships.md#ui-114-advisory-hint-amendment-accepted-2026-09-06-local-owner-approved-not-integrated) records the additive typed hint provenance: header/unknown-source hints retain existing behavior. Reuse the canonical relationship query, not filename similarity or a UI family table. IC admission, support, selected input and output bytes remain unchanged. |
| 4 | Raw JSON copy | Locally implemented and verified. [History handoff](../ui/v1.1.x-report-history-usability-handoff.md#raw-copy-approved-reference-and-implementation--2026-09-06): approved copy icon inside the Raw text box (not its header), exact full JSON, shared failure-safe clipboard event, preserved selection/scrolling, localized accessible tooltip and keyboard/Escape behavior. Actual screenshot, 36/36 Report regression and final 9/9 scoped tests retained; not integrated/published. |
| 5 | Report History open/delete | Locally implemented and verified (2026-09-06). [History handoff](../ui/v1.1.x-report-history-usability-handoff.md#history-action-implementation-and-evidence--2026-09-06): repaired commands, one-click/keyboard opening, centered issue count/trash and single-entry confirmation with Cancel/red Delete. Actual native captures, isolated close-flush/relaunch and 223/223 Report/navigation regression cases retained. Schema/capacity, loaded report and bulk clear remain unchanged; not integrated/published. |
| 5a | Exit/navigation confirmation consistency | Locally implemented and verified. [Navigation handoff section 3](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#3-exit-and-navigation-confirmation-consistency--2026-09-06): selected composition files, clean/dirty/pending Hex and explicitly loaded/pending Report JSON prompt before App close. Cancel preserves work and queues; automatic history restore alone does not prompt. Hex page-leave confirmation retains its document; existing composition navigation still activates before clearing. Shared modal style, safe Cancel focus, red Exit and topmost overlay; 258/258 scoped regressions and native capture retained. Not integrated/published. |
| 6 | CtrlRAM selector | [Visual contract](../ui/v1.1.x-ctrlram-selector-visual-contract.md): Base heading/subtitle, shared anchors, intermediate-outline removal, spacing and aligned `Max Size` / `Target Addr` guidance locally implemented; suggested filename omitted, actual selected filename and technical details retained. Owner accepted actual layout; logic re-review and 194/194 relevant tests pass (2026-09-06), including real selected-input full-window states and strengthened hover invariants. Not integrated/published. Remaining visual acceptance: genuine 125% evidence and resolution of unsupported High Contrast; do not claim the complete visual matrix passed. |
| 7 | Run reports list and Load report entry | [Approved list and evidence](../ui/v1.1.x-report-history-usability-handoff.md#approved-run-reports-list--2026-09-06): locally implemented; primary compared complete actual render with final owner-approved reference. Centered capped columns, no vertical lines, red confirmed deletion, execution-date ordering, overflow-only tooltips, full-width sidebar and detail return. Load exists only at Run reports and remains reachable when empty; one bounded loader, retained cancellation/latest-publication. Final 84/84 scoped tests passed, including corrected multi-entry scroll/return focus and remaining-row/Load focus after deletion; desktop build passed. Incomplete-import outcome correction is tracked in the accepted bounded follow-up below; integration/native DPI/high-contrast gates remain separate. |
| 8 | Bundle primary-output rename | [Implementation and evidence](../ui/v1.1.x-bundle-primary-output-rename-handoff.md#local-implementation-and-evidence--2026-09-06): locally implemented; independent primary/folder editing, same effective output/report/receipt identity, retained cancel/retry and invalid-name validation. Sources renamed and styled as ordinary disclosure. UI 48/48, Application 98/98, Infrastructure 263/263; actual-byte parity and source hashes retained. R2 scoped review completed; native/packaged integration and release Golden gates remain separate. Not integrated/published. |
| 9 | AB Dummy DP | Locally implemented and committed at `30c91905`, with final-evidence owner approval recorded at `dbd269ff`. The [Dummy DP handoff](../ui/v1.1.x-ab-dummy-dp-handoff.md) records the confirmed horizontal checkbox, DP-input exclusion and non-TP `0xFF` output for NT51919/NT51929/NT51932/NT51950/NT51951. Six independent complete-output map cases passed under the owner-accepted contract; these are not certified Dummy Golden or support promotion. Final combined integration and release gates remain pending. |
| 10 | Broad screen inventory and reprioritization | [Issue #291](https://github.com/Dennis40816/nvt_fw_combiner/issues/291): inspect the actually opened published `1.1.3` baseline and the accumulated candidate for Merge, Replace, Settings, Message Center, Report, Inspector, dialogs, menus, tooltips, cards and overlays. Reproduce the recorded Dark baseline; group remaining issues by shared control, dependency and risk, remove duplicates, and update this remaining order before broad implementation. Do not begin a blanket redesign. |
| 11 | Shared visual/theme corrections | Use step 10 findings to correct shared tokens/controls first. Cover normal/hover/focus/selected/disabled/checking/verified/warning/error states, Light/Dark/high contrast, contrast and keyboard visibility. Reuse the same regression matrix for later steps without silently changing approved geometry. |
| 12 | Report Changes | [Compare handoff](../ui/v1.1.x-report-changes-compare-handoff.md): gutter, Light Original colors, address spacing, approved range cards, navigation and recorded CRC/other causes are locally implemented and committed through `997fac98` / evidence `a7104dcf`. Physical-section grouping remains open and requires its own bounded design; preserve raw runs/order/hash/Why/Result/replay and virtualization. |
| 13 | Settings Version page | [Accepted source/list layout and evidence](../ui/v1.1.x-settings-version-handoff.md): owner approved the screenshot-based full-page reference on 2026-09-08. Compact update banner, full-width source editor, secondary Check now at list heading, and per-version typed Catalog `releaseNotes`; existing transactions/confirmation rules and modal/sidebar anchors remain unchanged. Locally implemented and committed at `ca6feb9f`, UI68/68 and accounting19/19 pass with independent fixed-head review; wider native DPI/High Contrast assessment stays separate. |
| 14 | Memory Layout | [CtrlRAM endpoint layout](#ctrlram-endpoint-layout--2026-09-09) locally implemented against the approved NT51927 / 3 IC reference: declared TP FW/DP overview and separate continuous target lanes, with shared leaf cards. Wider screen inventory remains separate; no firmware facts, range authority or interpretation move into UI. |
| 15 | Session diagnostics | Privacy-filtered current-session diagnostics/history, separate from immutable run reports. Preserve existing diagnostic ownership and lifecycle rather than adding another history system. |
| 16 | First-entry IC selection | [Navigation handoff section 1](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#1-shared-first-entry-ic-selection): reuse Home/navigation/accepted-session admission when compatible accepted IC context is absent. Decide lifetime, invalidation, cross-workflow compatibility and Cancel/Back first; no second catalog, selection owner or UI-only admission. |
| 17 | Test diagram and README | Completed and committed at `956a027f`: [execution map and measurements](../../tests/README.md), using retained `1.1.3` counts/timings. Original release evidence, later static counts, overlapping lanes, serial dependencies, retries and waits are distinguished; unavailable historical measurements remain explicitly unavailable, without rerunning tests. |

### Current remaining queue: small-impact work first

Owner-approved resequencing (2026-09-06): completed scopes 1-8, including 5a,
are retained in separate local commits through `ff074cd9`; they are not released
or final-integration approved. Implement one bounded item, verify it, then
commit that item before beginning the next. Previously recorded uncommitted
evidence below describes its original observation, not today's Git state.

Current checkpoint (2026-09-08): scopes 1-8/5a have the combined local UI
closure record at `d937747a`. Dummy DP (9), the test diagram/README (17), the
approved local Report Changes corrections (part of 12), and Settings Version
(13; implementation `ca6feb9f`, final evidence `947d10ec`) are also committed.
They are no longer active implementation TODOs. These are local scoped
completions, not a fresh combined integration pass or publication.

Current reconciliation (2026-09-09): the later completed local units also
include Report-import outcome correction, narrow Replace headers, selected
filename layout, Support Matrix layout/scrolling, shared issue-card placement,
Memory Layout source/initialization wording, technical disclosures, small-slice
hover cards and CtrlRAM endpoint lanes. The rail clipping fix is committed at
`24ad3778`, with evidence checkpoint `02da6a3a`. Earlier dated statements that
these changes were uncommitted remain historical observations. Do not reopen
them as new TODOs; whole-candidate/native/release acceptance remains pending.

The owner now requests already-authorized work without new design/product
decisions first, one independently verified unit per commit. The owner removed
the earlier 10:00 stop limit on 2026-09-08; it is no longer an execution boundary.
Continued work does not authorize unresolved designs, new support claims or
release. Assessments may record a limitation and move to another independent
item rather than force a decision.

2026-09-09 follow-up: shared Browse/clear actions now center on the complete
input card, including the filename, under the
[selector amendment](../ui/v1.1.x-ctrlram-selector-visual-contract.md#approved-selector-direction).
The [scoped evidence](../../tests/README.md#shared-slot-action-alignment--2026-09-09)
is local verification, not publication or whole-candidate acceptance.

The subsequent [primary-rail separator correction](../../tests/README.md#primary-memory-rail-separator--2026-09-09)
removes only the decorative one-pixel seams, retaining local-strip divisions,
focus outlines, exact addresses and true Unmapped. It is locally verified;
the unresolved Memory Layout items below remain open.

2026-09-10 local follow-up: [selected-input lifecycle regressions](../../tests/README.md#selected-input-facts-regression--2026-09-10)
now cover Shared CtrlRAM, Standard and AB cards (`68697976`, `3bc1e03f`). The
[loaded NT51929 AB memory-card checkpoint](../../tests/README.md#loaded-ab-memory-cards--2026-09-10)
adds narrow/wide, language/theme and dismissal evidence through the shared
control. This closes that bounded inventory gap only; other workflows, native
acceptance and the existing integration/release gates remain open.

Owner resequencing, 2026-09-10: finish Memory Layout workflow rendering and
interaction coverage first in `1.1.4`, including bounded responsibility
separation and tests against behavior rather than private methods/source text.
Preserve the approved CtrlRAM visual contract and firmware semantics. Other
unfinished `1.1.4` feature/inventory work in the table below moves to `1.1.7`;
completed units remain historical `1.1.4` work. `1.1.5` and `1.1.6` are unchanged.
Mandatory validation for an actual release is not deferred by this allocation.

Owner addition, 2026-09-10: reconcile all existing Golden fixture inventory
with the release chain so the release package can be used for manual testing.
Use the existing manifest/redistribution/copy/smoke owners; identify and migrate
omissions with exact source, role, size and hash evidence. Keep certified full
output cases separate from input-only/evidence-only material; inclusion is not
support promotion or fabricated certification. Preserve confidentiality and
exclude transfer wrappers, credentials and unrelated private material. Assess
any conflict with the current closed package allowlist before changing policy.
This remains an open `1.1.4` release-package task, not permission to publish now.

Current inventory: all 25 Direct output cases are already selected (11 full
output, 14 allowed-byte-difference). Canonical has 40 cases; release has 35.
Missing are input-only `nt51927-2chip-self-20260705` and
`nt51927-3chip-self-20260705`, plus their aliases
`nt51917-fw132-cascade2-nt51927-alias`,
`nt51917-fw140-cascade3-nt51927-alias` and
`nt51928-fw132-non-nb-cascade2-nt51927-alias`. Before the local migration below,
validator/package/smoke explicitly excluded these inputs/dependent aliases; preserve their dispositions
when extending reference-only redistribution. This inventory is not a
completed policy migration or candidate Golden execution result.

Owner confirmation, 2026-09-10: include the two CtrlRAM input-only cases and
their three dependent aliases in the release reference payload, and retain
their firmware inputs plus provenance as GitHub source evidence. This is
explicit redistribution authorization for those five canonical cases, not
output certification, runtime support promotion or authorization to publish a
release. Keep `input-only-evidence` and fact-scoped alias declarations intact;
package instructions must state that no independent expected output exists.
Use the existing canonical files, not a second evidence tree or transfer archive.

Source-presence verification: live `origin/main` resolved to
`e5202e2707d272076d24216222188d478314a07d`; all 21 tracked files for these five
cases (16 input BINs and five case manifests) match that remote commit's Git
objects exactly. They are already in GitHub source; no duplicate upload or
push of unrelated `1.1.4` work is needed. The local release allowlist, validator,
packager/smoke and derived-pin migration is recorded below; actual candidate
packaging is still required before claiming published-package availability.

Local release preparation, 2026-09-10: explicit 40-case reference selection
implemented (25 Direct, three input-only, twelve aliases; 177 declarations,
174 unique artifacts, 164 BINs, 215 projected paths). All previous 35 case
declarations and all fixture bytes are unchanged. VERSION is 1.1.4; changelog
is explicitly a not-yet-published candidate. Canonical/sync tests pass 103/103;
package policy and deterministic dry-run tests pass 7/7. Independent fixed-diff
R3 review is PASS-WITH-HUMAN-GATE. See [test evidence](../../tests/README.md#v114-ctrlram-reference-package-preparation--2026-09-10).

Integration is still blocked by the pre-existing immutable design record
`UI-114-CTRLRAM-GUIDANCE-63`: four declared test paths are not governed under
the current classifier. The existing checker returned these four errors;
this does not certify that later gates will pass. No old record or validator
was changed or bypassed. A separately approved governance repair is required
before final candidate verification, exact-head release-owner evidence and
publication. The new reference admission remains design-active, not finalized.

Local Memory Layout maintenance checkpoint, 2026-09-10:
[connector responsibility/test coupling](../../tests/README.md#memory-connector-responsibility-and-test-coupling--2026-09-10)
is committed at `5157bfb3`; [loaded Standard DP/TP/LDC card coverage](../../tests/README.md#loaded-standard-memory-cards--2026-09-10)
adds the missing real-window lifecycle checks. Neither changes the approved
CtrlRAM design, firmware semantics or public support. The subsequent existing
AB/Dummy, CtrlRAM single/cascade, display-failure, list and explorer regressions
pass 33/33 on `e5057718`, alongside the 72-case Standard/popup/geometry/style
run: 105 scoped cases, zero skipped. This closes this bounded local Memory
Layout maintenance/coverage tranche, not every IC/native or release gate.
Release-package policy reconciliation is locally implemented as recorded above;
actual candidate validation/publication remains pending. All certified Direct
output cases remain selected without altered expectations.

The following table retains the individual boundaries; only Memory Layout is
active implementation in `1.1.4`. All other rows and the custom-options preview
are allocated to `1.1.7` below.

| Remaining order | Scope | Boundary |
| --- | --- | --- |
| 1 | Native DPI/high-contrast acceptance | [Capability assessment complete](../../tests/README.md#v114-display-evidence-boundary--2026-09-08): current native display is 100%, High Contrast off; 20 scoped cases passed with explicit 100% headless scale evidence. Actual Windows 125%, custom-control High Contrast and screen-reader acceptance remain open. No theme/support promotion or OS preference change was made. |
| 2 | Broad screen inventory and reprioritization (10) | [Initial evidence inventory below](#visual-inventory-checkpoint--2026-09-08) separates inspected surfaces from uncaptured states. Remaining whole-app/native coverage is open; do not restart completed metadata/list/source/card changes or mistake a token search for visual acceptance. |
| 3 | Shared visual/theme corrections (11) | Change shared owners only after inventory identifies affected consumers and states; obtain approval for new visual designs. |
| 4 | Report physical-section grouping (remaining 12) | Reuse existing typed groups only after the grouping interaction and historical replay-coverage boundary are accepted; do not invent bytes between raw runs. |
| 5 | Memory Layout (14), active bounded work | All-IC DP/TP classification inventory is complete; context-binding implementation and its remaining integration gate are recorded below. The three-row supporting-list correction is locally implemented and committed (`eb314ebe`, language correction `9c1b1757`), not another implementation TODO. The [three-scenario responsive checkpoint](../../tests/README.md#memory-layout-responsive-checkpoint--2026-09-09) passes 18 viewport/theme/language states; other workflows and native acceptance remain open. Retain typed section/address-space authority, exact rails and independent CtrlRAM endpoints. Master/Slave hover-to-open was approved on 2026-09-09; the local interaction checkpoint below supersedes the earlier design-only status. |
| 6 | First-entry IC selection (16) | Resolve accepted-context lifetime, invalidation and Cancel/Back before cross-page behavior changes. |
| 7 | Session diagnostics (15) | Existing bounded current-session service, Important/Debug disclosure, privacy-filtered export and separation from immutable reports are inventoried and locally tested; see [current-session evidence](../../tests/README.md#session-activity-inventory--2026-09-10). The [selection/focus correction](../../tests/README.md#activity-selection-and-focus-correction--2026-09-10) retains the existing screen. [Nonempty warning/error filtering, Debug disclosure and compact navigation](../../tests/README.md#nonempty-activity-and-compact-navigation--2026-09-10) now have local real-window evidence; native acceptance remains open. Do not implement a second history owner. |

The per-page custom-options preview also remains awaiting visual acceptance
within the inventory/shared-visual scope, not a completed layout. Final
`1.1.4` closure requires combined affected regression, packaged Windows UI
observations and all applicable certified Golden outputs on the release
candidate. Those are release-boundary gates, not per-commit test requests.

#### Current local progress and health review — 2026-09-10

The owner requested decision-free `1.1.4` work first, with one commit per
coherent unit. Direct memory-card connectors (`981e6f73`), aligned standalone
input cards (`f982a294`) and duplicate context-detail removal (`8e914a48`) are
locally complete. Their [scoped evidence](../../tests/README.md#memory-context-disclosure--2026-09-10)
does not replace combined integration or release verification.

Primary plus GPT-5.6 Terra/high reviewed the `1.1.3..f982a294` architecture and
test-maintainability slice, then the bounded follow-up UI diffs. No confirmed
P0/P1 or duplicated firmware execution owner was found within that scope.
The two Presentation/Desktop boundary tests were freshly executed and passed;
this is not an exhaustive codebase, security or firmware certification.

| Health area | Evidence and disposition |
| --- | --- |
| Layer boundaries | [Presentation boundary](../../tests/NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.PresentationStructure.cs) and [Desktop wiring](../../tests/NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.DesktopHostConvergence.cs) already enforce the distinction. No new dependency framework is justified. |
| UI maintenance | [MemoryCoverageBar](../../src/NvtFwCombiner.Presentation.Avalonia/Views/MemoryCoverageBar.cs) concentrates lifecycle, pointer/keyboard, animation and popup geometry. Preserve its single owner and behavioral coverage; consider extraction only with a concrete independent responsibility, not a cosmetic file split. |
| Test maintenance | Source-text counts in [the soft-lift contracts](../../tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.MemoryCoverageInteraction.cs) coexist with real control/geometry tests. The counts are a maintenance risk, not proof of missing behavior coverage. Evaluate redundant assertions in the existing `1.1.6` minimality work, preserving unique contracts. |
| Document drift | `SPEC.md` still labels `1.1.3` release gates pending and DP Replace retirement owner-unallocated, while this roadmap records release/retirement decisions. Reconcile through the existing `1.1.6` document work; do not treat those stale status statements as new authority or change firmware contracts here. |

New designs for Report physical grouping, first-entry context and custom-option
placement remain owner-decision items. Native 125%/High Contrast/screen-reader
observations still need the actual environment; headless captures do not close
them. No release, push, merge or OS preference change is included in this work.

##### All-IC DP/TP context intake and compact supporting list — 2026-09-09

The owner requests classification coverage for every currently registered IC.
The package's current ten IC identities are NT51917/19/23/26/27/28/29/32/50/51;
this request does not open the distinct pending NT51928BT identity or adopt
the unresolved Desay workbook. Classification is display context, not support
promotion or a change to output bytes.

| Current ICs | Existing canonical context | Remaining work |
| --- | --- | --- |
| NT51917/23/26/27/28/29/32 | Exact same-IC Standard `ReportClassification` companion is already bound to CtrlRAM routes. | Retain existing coverage and verify every declared route/variant; no new IC/filename/capacity lookup table. |
| NT51919 | Unit58 binds the existing same-IC Standard map, whose regions reuse the declared 51929 family. Report metadata remains empty. | Locally implemented and verified; exact-head R3 owner attestation remains before integration. |
| NT51950/51 | Unit58 binds existing Standard `dp-container` / child `tp-overlay` as separate display-only context; UI labels DP image and TP FW, preserving disconnected ranges. | Locally implemented and verified, including TP-work without a DP claim; exact-head R3 owner attestation remains before integration. No capacity/filename map selection. |

Read-only confirmation: both NT51950/51 Standard profiles declare
`copy-dp-container` at sequence 100 followed by `overlay-tp` at sequence 200.
The latter replaces `[0x0A000,0x37000)` from the same offsets in TP input;
bytes outside that range retain DP-container content. `processorStages` is
empty. AB is a distinct layout contract. Proposed display wording is DP image
as the base with an explicit TP FW overlay, not an inferred DP Code complement.
The owner approved implementing this presentation. Unit58 changes six explicit
package bindings and ten capability fingerprints, not canonical map geometry or
output writes. Exact-head external owner attestation remains pending; local
evidence and limitations are recorded in `tests/README.md`. NT51928BT/Desay and
unverified IC × mode × topology combinations remain outside this completion.

Owner review feedback after candidate `6f625de5` (2026-09-09), confirmed as
bounded R1 display corrections after risk clarification:

- Shorten the Memory Layout label from `DP image` to `DP`; retain the canonical
  DP-container/TP-overlay distinction, exact ranges and disconnected sections.
- In the single-IC CtrlRAM detail view, show `Master` instead of `Common`.
  Use existing typed IC-count/topology context; do not globally rename `Common`
  or change multi-IC grouping, source sharing, replacement ranges or bytes.

The owner's confirmation permits this local display correction to proceed;
it is not owner attestation for the previous frozen R3 candidate. This
task-specific sequencing decision applies only to the two labels above and
expires when this correction is handed off. It does not change the validator,
immutable Unit58 record, firmware safety, Golden, integration or release gates.
Unit58 retains its admitted Presentation paths for the DP label correction;
`UI-114-CTRLRAM-TITLES-59` admits only the two additional lane-presentation
paths, with no overlapping coverage. The pending R3 owner gate remains explicit.
Implemented in Presentation only: DP uses the existing shared role title;
the detail heading uses accepted `TopologySelection.ChipCount == 1` without
mutating the underlying region group or input group. The red run reproduced
three single-IC failures and passed both Cascade controls. The final scoped
run passed 15/15 tests, zero skipped: single NT51919/950/951, Cascade 950/951,
three-IC NT51927, Standard NT51928, and logical-range grouping. It checks
actual English/Traditional-Chinese headings, unchanged Common input groups,
position markers, ranges, geometry and Build readiness. Evidence is in local
`v114-ctrlram-labels-review/ui-final.trx`; actual 1440x1040 Light/English and
Dark/Traditional-Chinese renders are under its `final/` directory and were
compared with Unit58's `v114-ctrlram-context58/green/` reference renders.
No full suite, Golden output execution or release run was repeated for labels.
Scoped independent review (GPT-5.6 Terra/high) found no P0-P3 issues in this
R1 correction; that result is not a pass for the earlier R3 batch.
This is a local implementation checkpoint; batch finalization/integration
remains pending the original R3 external-authority gate.

##### Hover-only CtrlRAM endpoint hierarchy — 2026-09-09

The owner approved this bounded R1 interaction revision after reviewing the
actual always-visible lanes and the prior floating style:

1. Show the firmware overview and exact CtrlRAM position markers initially.
2. Hover or keyboard-focus one position to reveal only that contiguous lane.
3. Hover/focus a terminal slice to raise it and show the shared information card.
   Ordinary DP/TP leaves open their card directly, without an endpoint tier.
4. Keep pointer transit to the local view/card possible; leaving the interaction
   closes the overlays after the existing short grace. A mouse click is not a pin.
5. Keep disconnected ranges independent even when their source/group matches.
6. Restore the legacy 118% vertical lift and shadow without changing width,
   address proportion or firmware semantics. Reduced Motion suppresses movement.

The shared `MemoryCoverageBar` replaces, rather than duplicates, the permanent
detail lanes. Its existing local view renders the selected lane's exact original
slices directly; it does not recursively aggregate them into a third zoom tier.
The endpoint heading and compact marker use the same display-only role, so a
single-IC Common input remains Common while its overview marker is `M` and its
expanded heading is localized Master. No map/profile/output rules change.

`UI-114-MEMORY-HOVER-60` records the local scope. The owner explicitly permitted
this task's continuation and commit while Unit58/59 remain unfinished, answering
“是可以先提交” to the narrowly scoped sequencing exception. This permission ends
at this task's handoff; no immutable record is rewritten, no final lifecycle
pass is asserted, and firmware/Golden/external-owner/integration/release gates
remain unchanged. Local verification is recorded in `tests/README.md`.

The subsequent owner feedback is tracked as `UI-114-MEMORY-MARKERS-61`:
M/R/L are quiet aligned position markers with a fine underline, not floating
buttons. The shared secondary view/card use the existing themed blue tint,
fine border and shadow; transparent connector gaps preserve the underlying
panel lines. Actual memory leaves retain their lift. The owner renewed the
local continuation/commit exception for this visual unit only, including the
subsequent expanded-border/card feedback; prior lifecycle records remain open.
The scoped UI run passes 42/42 with whole-window captures in `tests/README.md`.

The supporting-list correction is a separate Presentation-only unit:
reuse already-projected primary rows and existing card/button styles; initially
show at most three, with Unmapped behind mapped rows, then show every row in
address order on expansion. Keep exact rail data and CtrlRAM endpoint lanes
unchanged. Actual new input publication resets the list; relocalization
preserves its disclosure state. Implemented at `eb314ebe`; the CtrlRAM hidden
auxiliary-list relocalization correction is `9c1b1757`, not a change to visible
endpoint lanes. The independent final review passes with no findings.
`v114-memory-list53` under the existing test-area evidence root contains the
real two-case red (five cards instead of three), 23/23 scoped regression
(29 s), and final 11/11 list/language/CtrlRAM cases (25 s). Shell wiring passes
1/1 and accounting passes 19/19 (31.396 s); unchanged accounting evidence is
reused after the equal-line correction, with a fresh source measurement.
Actual complete control renders cover NT51928 Merge/DP Replace in EN Light and
zh Dark, along with the retained NT51927 three-IC lanes. These are not native
DPI/High Contrast/screen-reader or firmware-output certification.
Scoped final evidence is bound in
[UI-114-MEMORY-LIST-53](../governance/change-records/UI-114-MEMORY-LIST-53.json);
the canonical structure check passes in 184.2 s with zero derived-file changes.
Existing advisory size warnings remain visible; no full-release pass is implied.

##### Family reuse inventory — 2026-09-09

The owner reiterates that NT51919 belongs to NT51929's Perfect family and
shared family facts must not be redefined for each member. No new map is
needed merely to label NT51919. The read-only audit below distinguishes the
owner's sharing intent from how current versioned packages expose it; a missing
declaration in one package does not overturn that owner direction.

| Members | Current explicit mechanism | Finding / next action |
| --- | --- | --- |
| NT51917/27 | Standard family declares `perfect-like-family`; NT51917 CtrlRAM materializes the canonical NT51927 family. | Retain shared facts and per-member support/evidence distinctions. |
| NT51917/27/28 | Separately declared partial relationships for Initial Code, TP and TP header facts. | Reuse exactly those shared facts; do not promote the whole NT51928 map to Perfect. |
| NT51919/29/32 | DP Replace family version explicitly declares Perfect and uses one map for all three. Standard uses another version: NT51919 has an explicit NT51929 region-set alias and retains its own target-map identity. CtrlRAM shares 19/29 geometry, while 32 has a distinct workflow family. | The sharing declarations and references are not uniform across workflows/versions. Trace existing canonical facts and the missing NT51919 display binding before extending it; do not add another range table or silently bypass exact-map admission. |
| NT51923/26 | Same Standard family container, but no explicit Perfect/shared-fact relationship in that definition; distinct CtrlRAM families. | A common container or similar addresses alone does not prove complete equivalence. Identify the actually shared facts before consolidation. |
| NT51950/51 | Standard declares the TP overlay as a shared fact; DP-container/map variants and other workflows retain distinct identities. | Reuse the common TP fact, not an inferred whole-map Perfect relationship. Keep the pending Desay variant distinct. |

Decisive evidence: the [normalized 19/29/32 family](../../profiles/built-in/nt51919-nt51929-nt51932-shared-facts/families/nt51929-nt51932.json)
has the Perfect declaration (the audit loaded it through DP Replace; 1.1.10 relocates identical bytes to a neutral provider); the [Standard family](../../profiles/built-in/nt51929-standard-merge/families/nt51929-nt51932.json)
has the 19-to-29 region-set alias. The Standard NT51919 metadata binding lacks
the ReportClassification purpose used by the existing CtrlRAM companion path.
Domain's [relationship validation](../../src/NvtFwCombiner.Domain/Firmware/FirmwareFamilyResolutionDefinition.cs)
rejects member-specific maps/aliases/capability facts within a declared Perfect
family, but this does not itself reconcile different family versions across
workflow packages. The [trust index](../../profiles/built-in/package-trust-index.json)
explicitly materializes canonical source definitions; such references are
reuse, not independently maintained copies.

No direct violation was proved merely by finding repeated family IDs or
versioned bundles. The actionable gap is cross-workflow/version consistency
and display-context coupling to Report metadata. Do not solve the latter by
inventing Report metadata or new maps. Family convergence belongs to the
`1.1.6` semantic-consistency work; this intake is not a firmware migration or
a claim that all missing classification bindings are already implemented.

#### New owner intake: IC/layout updates and option density — 2026-09-08

This intake records requested work and unresolved dependencies, not a new
firmware contract or a completed implementation. The IC/layout work has not
yet received a release-version allocation; its arrival during `1.1.4` does not
silently add a firmware release gate to that UI milestone. The existing queue
above retains its contents. Owner sequencing amendment on 2026-09-08:
record and prepare the preview-first custom-options work. The owner's later
2026-09-08 amendment defers NT51928 confirmation and resumes the next bounded
Memory Layout item (residual generic Reserved-source wording assessment)
without waiting for the unresolved IC/Desay firmware decisions. Keep those
decisions pending; this does not approve new firmware definitions. Do not reopen the
completed Technical details disclosure. Option-density assessment extends the
inventory/shared visual items rather than creating a blanket redesign backlog.
The [custom-options handoff](../ui/v1.1.x-custom-options-layout-handoff.md)
owns the preview and visual-acceptance detail.

| Item | Confirmed request / scope | Next action and completion boundary |
| --- | --- | --- |
| NT51928BT family and staged capabilities | Owner states NT51928BT belongs to the NT51950 family and requests most applicable functions to be implemented but not opened for use. This is distinct from existing NT51928; membership alone does not prove identical geometry or perfect-family parity. | Retrieve the owner-named `51928BT` HackMD note through the requested API, inventory supported semantics per workflow, and agree the exact capability subset from that evidence. Implement only confirmed definitions through existing owners; verify ordinary UI and execution admission remain closed, not merely hidden by a selector. No support/certification promotion. |
| Desay NT51950/NT51951 unified layout | Owner reports a major Desay-specific layout unification and all TP backup starts becoming `0x4A000`; the Excel was recovered on 2026-09-08 and awaits cell-level inspection. | Compare old/new regions, address spaces, capacities, topology, preservation and processor requirements, then update the existing IC FlashMap and provenance dates. Resolve whether this is a customer-specific variant or replacement contract before changing existing routes; do not apply it to other customers implicitly. |
| NT51951 AB and Dummy DP impact | Current [map-based inventory](../ui/v1.1.x-ab-dummy-dp-handoff.md#read-only-implementation-inventory-2026-09-06) places TP B at `[0x8A000,0xB7000)`, while NT51950 starts it at `0x4A000`. Whether the new term TP backup means this AB TP B placement is not yet verified. | If confirmed, audit TP B placement/length, relocation, Combiner staging, header/CRC imports and allowed writes, output capacity, and Dummy TP/non-TP ranges together. Add affected regression and independent complete-output evidence; retain Normal Golden and firmware-owner gates. Do not assume the new end from the old length. |
| Per-page custom-option density | Owner requests layout advice because individual pages have accumulated custom controls. Assessment is authorized; a new visual design is not yet approved. | Inventory the actual current Standard/AB/CtrlRAM pages and group controls by task and consequence. Preview one representative AB page before implementation; retain existing button styles, widths, keyboard access and confirmation behavior. |

API access was verified on 2026-09-08 through the global
`hackmd-cjk14-transfer` skill's existing DPAPI-protected credential store;
the earlier environment-variable-only check was insufficient. Exactly one
`51928BT` note was retrieved (15,749 characters; source SHA-256
`4aaa803e92d39a024ae90c90880ec0290c283634045bf45772b959c0a436890a`).
Its encrypted payload was recovered using the existing decrypt tool on
2026-09-08: original filename
`NT51928BT_NT51950TT_NT51951TT flash mapping table for desay_0908.xlsx`,
31,353 bytes, recovered SHA-256
`f49368ee605301ae4e310cc990b674ef8cd82021ffa40d9c00f42d7b2108030b`.
The recovered source is archived outside Git under the transfer tool's
timestamped `record/51928BT-20260908-172414` directory. Workbook inspection
and map comparison are pending; the earlier statement that the Excel had
not been received is superseded. The owner requests incorporation into the
existing IC FlashMap with updated provenance dates after comparison. Preserve
the workbook's actual revision/date separately from the 2026-09-08 retrieval
date; the `0908` filename alone does not establish a full revision date.
Cell-level inspection is recorded in the
[FlashMap intake](../references/ic-flashmap/README.md#desay-workbook-intake--2026-09-08):
BT/JT identity, the left-side `0x4D000` backup, expanded TP envelope,
overlapping row 39 and capacity/label discrepancies need owner resolution.
The original also has three external-link relationships and is not copied to
distributable references. The inspected values are not admitted firmware facts.
No profile, production range
or availability was changed by this intake. Private note URLs, credentials
and firmware payloads stay out of this roadmap.

UI preview-first workflow accepted; final visual design pending: retain IC/mode/topology context
and required inputs as the main surface; place related routine options in a
compact row near their input; use one shared-style collapsible section for
infrequent options with a visible active-setting summary. Output-changing
choices such as Dummy DP must remain conspicuous when active, retain their
confirmation, and never disappear with a collapsed section. Keep naming and
delivery in the existing Build settings surface. Show only applicable controls
from typed capability facts; do not introduce per-customer pages, inferred
customer detection, new persistence or a generic dynamic-form framework.

Dummy DP's owner-accepted map-based evidence and write-range audit are recorded
in the [Dummy DP handoff](../ui/v1.1.x-ab-dummy-dp-handoff.md); the NT51950/NT51951
scope is no longer waiting for its initial implementation. Existing NT51951
ordinary AB certified-Golden/support limitations remain distinct from Dummy
feature approval. The package/smoke schema mismatch discovered at integration
was corrected at `1da0b11f` and its committed-head regressions passed; this does
not constitute a complete verifier pass. Combined regression, packaged native
UI observations and all required release Golden execution remain separate
closure gates, not reasons to rerun a full release per internal commit.

Report import correction is locally complete: implementation `e9b19cef`,
review/evidence `c3c81ba2`; Report205/205, Application24/24, accounting19/19
and repository validation passed. Do not reopen this as an admission decision.

#### Visual inventory checkpoint — 2026-09-08

This is a bounded evidence inventory, **not completion of the whole-app theme
audit**. The primary inspected retained actual published `1.1.3` captures and
the listed local implementation captures; no baseline was redrawn. Historical
captures are not fresh current-source runs. Candidate screenshots may still
display the retained `1.1.3 desktop` version label; their recorded source and
test evidence, not that label, identify the candidate.

| Surface | Evidence inspected | Disposition |
| --- | --- | --- |
| AB metadata | Actual [1.1.3 baseline](../ui/references/v1.1.3-ab-dp-metadata-baseline.jpg) and [implemented four-field layout](../ui/references/v1.1.4-ab-dp-metadata-actual.jpg) | Completed local correction; do not reopen it as a new metadata redesign. This historical capture predates Dummy DP and is not Dummy evidence. |
| CtrlRAM inputs | Actual [1.1.3 baseline](../ui/references/v1.1.3-ctrlram-selector-baseline.jpg) and current 1440x900 Dark EN selected-input frame in `v114-display-assessment` | Shared anchors and Max Size/Target Addr corrections remain implemented. Owner approved the dedicated full-width filename footer on 2026-09-08; locally implemented by `UI-114-SLOT-FILENAME-22` below. Native scaling remains separate. |
| Run reports | Actual [1.1.3 hub baseline](../ui/references/v1.1.3-load-report-hub-baseline.jpg) and [implemented list](../ui/references/v1.1.4-run-reports-black-inter-actual.png) | List hierarchy, Load entry, centered columns, red deletion and 800-weight headings/sidebar are existing accepted work. The separate import false-success defect is behavioral, not a reason to redesign this list. |
| Settings Version | [Approved reference and current full-page/compact evidence](../ui/v1.1.x-settings-version-handoff.md), plus scale-verified Light/Dark frames | Bounded layout/source/notes work complete; native file picker and OS accessibility observations remain separate. |
| Report Changes | [Approved cards and four-combination rendered matrix](../ui/v1.1.x-report-changes-compare-handoff.md#complete-languagetheme-cross-product--2026-09-08) | EN/zh-TW × Light/Dark card-layout gap closed at `e77f7f89`. Physical-section grouping remains a distinct pending design/typed-projection item, not unfinished card styling. |

The import ambiguity is now closed locally. Priority continues with independent
small reproduced presentation defects, followed by shared theme fixes after
their affected consumers are captured. Physical grouping, Memory Layout,
first-entry context and session diagnostics retain their existing decision
boundaries; no new screen design was approved by this inventory.

The source scan found no matches for hex color literals, `Brushes.White/Black`,
`Colors.White/Black`, or `Color.Parse` in Presentation C#/XAML outside
`ThemeTokens.axaml`. This only checks those spellings; it does not establish
complete token ownership, contrast, focus or native theme correctness.
Existing Light/Dark palette-ratio evidence is separate from visual inspection.
The still-open [issue #291](https://github.com/Dennis40816/nvt_fw_combiner/issues/291)
has a historical `0.10.7` title; current allocation is this `1.1.4` roadmap, not
that stale title. Its original `v0.9.18` Dark reproduction was not recreated in
this pass, and no GitHub issue was modified.

Remaining inventory coverage includes Inspector entry reachability and the
menu/tooltip/overlay state matrices, representative checking/disabled
states, native 125%/High Contrast/screen-reader observations, and current
candidate comparisons where only retained historical images exist. These are
explicit unverified areas, not new blanket redesign tasks or evidence that
already-reviewed functionality is broken.

##### Memory Layout source-label assessment — 2026-09-08

Read-only product assessment on `30212f66` after the accepted Support Matrix
scrollbar follow-up. No Memory Layout redesign or firmware change is included.
`UiCompositionRunner.Common.cs:211` (`MemorySource`) classifies CustomerInformation
and Reserved as the generic Reserved source before considering SourceSpaceId.
`MemoryCompactDetail` separately names the actual DP input/replacement source
for customer information. The existing DP-perspective test explicitly expects
both `Reserved` and a DP-replacement explanation for the same protected range
(`ShellViewModelDpPerspectiveTests.cs:378`). This is a source-label ambiguity,
not evidence that firmware bytes are incorrect.

Proposed next bounded design: distinguish canonical section purpose from the
existing typed byte source; retain protected-region status and do not equate
Reserved with `0xFF`. Initialization may name a fill byte only when the typed
layout actually supplies it. Physical grouping/layout interaction still needs
owner acceptance; this assessment does not approve a new design or change the
current source-label tests.

Attempted current-source capture command:
`dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore --filter FullyQualifiedName~RealNt51950GoldenCtrlRamPanelFitsProductionRail --logger 'trx;LogFileName=memory-inventory.trx' --results-directory $env:NFC_VISUAL_OUTPUT_DIR`.
Test-area environment was set to
the existing external root; `NFC_VISUAL_OUTPUT_DIR` and results directory were
`D:/NvtFwCombiner-TestArea/evidence/v114-memory-inventory-20260908`.
Build completed, but no case result or PNG appeared for approximately 2.5 minutes
after testhost started; the primary cancelled this invocation without rerunning.
There is no pass/failure assertion or current screenshot from this attempt.
The eight intended cases cover isolated 430/360 px CtrlRAM rails in EN/zh-TW
and Light/Dark, not whole-window or native DPI acceptance. Capture-host progress
must be diagnosed before reusing this seam; the cause is not established.

##### Resumed Reserved-source assessment — 2026-09-08

Read-only follow-up on `c1f792d9`, after the owner deferred NT51928 confirmation.
The [current AB Dummy baseline](../ui/references/v1.1.4-custom-options-baseline.png)
shows `Source: Reserved` and `Output range uses bytes from Reserved.` This is
not sufficient evidence that the canonical region is Reserved or that final
bytes are `0xFF`.

`UiCompositionRunner.Common.cs` (`MemorySource`) also maps a source-less
`MemoryWorkflowDisposition.Blank` to the Reserved label. `MemoryCompactDetail`
then sends non-Reserved roles without a recognized source to the generic
source sentence. `MemoryCoverageSegmentViewModel` uses that source label as
the title for non-customer-information roles. Therefore a source-less region
can look like a Reserved region even though its typed content role differs.
`MemoryLayoutProjector.InitialDisposition` deliberately distinguishes these
states; the wording must not collapse them into a firmware fact.

Display convention accepted by the owner on 2026-09-08; implementation remains
pending. This acceptance does not approve a new grouping layout:

| Typed situation | Proposed concise display | Guardrail |
| --- | --- | --- |
| Source-backed writes | `Source: DP BIN` / `TP BIN` / the existing typed source | Retain actual source and current write-state label; region purpose must not mask the source. |
| Reference bytes retained | `Source: Base flash` | Keep the reference-kept precedence; do not call retained bytes initialization fill. |
| Explicit blank initialization and no planned writes to this range | `Initialization: 0xFF` (use the actual typed byte) | Explain `No writes planned for this range.` This describes the current plan, not a verified output or Build readiness. |
| Source not assigned, or initialization not established | `Source: Not assigned` | Do not invent a fill byte or classify the region as Reserved from the absence of an input. |

Keep the accepted card width, leading color marker, range/size rows and compact
Technical details disclosure. A genuine Reserved purpose remains a purpose,
not an input file. Do not alter canonical ranges, input requirements, planner
operations, Dummy DP semantics or firmware bytes. Implementation needs focused
source/blank/reference/operation coverage and a same-state production render;
this assessment has not changed production or rerun product tests.

Implementation update (`UI-114-MEMORY-SOURCE-35`, 2026-09-08): the local
candidate now separates typed purpose, actual source and unwritten explicit
initialization. AB Dummy's source-less logical regions retain their typed
General/Data purpose (not an invented DP or Reserved role). Kept CtrlRAM shows
its subtype as title and Base flash as source, including assistive text.
Coalesced supporting rows carry the same title/source-field metadata.
Focused UI tests pass 48/48, zero skipped, 17 seconds of execution at
`D:/NvtFwCombiner-TestArea/evidence/v114-memory-source35/reviewed/reviewed.trx`.
The initial AB actual-window regression failed on the old Reserved wording;
additional tests cover non-FF initialization, unknown initialization,
source-less declared writes, reference preservation and real DP/CtrlRAM input
projections. This is local UI evidence, not a new Golden certification or full
integration/release pass. Small-slice aggregation/local-view implementation is
the next unit; source wording verification alone does not complete that work.

##### Small-region grouping: accepted information model — 2026-09-08

The owner accepted the following information/interaction requirements before
further visual design. Earlier generated magnification images are exploratory,
not approved references. Do not implement their geometry by implication.

| Surface / interaction | Required information |
| --- | --- |
| Main map | Output address space and overall address bounds; proportional overview. A small-region aggregate has a group marker/count, not the identity of a single physical region. |
| Hover aggregate | Local view containing the original slices in address order, group bounds, total length and slice count. Group endpoints correspond to the same two boundaries on the main map. |
| Hover a constituent slice | Highlight its contiguous physical run and show the shared nearby card with its name, complete address range and size. Do not label every internal boundary at once. |
| Existing detail card | Show the focused slice's actual source, planned/retained state, processing explanation and Technical details. Do not infer a common source, role or Kept state for a heterogeneous group. |

Only address-contiguous small slices in one address space may aggregate;
preserve the aggregate's summed width on the main map. Grouping is presentation
only, not a change to canonical ranges or execution. Mark the secondary scale
as `Local view`. Hovering only the group shows group facts, not an automatically
chosen first slice. The visible local view remains reachable for individual
slice inspection; implementation must also provide a keyboard/focus equivalent.

The accepted UI convention retains the existing start-to-last-valid-address
display; internal contracts and document ranges remain half-open. For example,
the illustrative range `[0x41000, 0x42000)` has length `4 KiB`; its UI last
address is `0x41FFF`, not `0x42000`. This example is not an IC map definition.
Do not mix these two endpoint conventions in the preview or implementation.

The subsequent information-complete preview was accepted for implementation
on 2026-09-08 and a bounded Memory Map goal was created. The initial local
candidate uses a two-percent per-slice threshold, retaining exact summed
weights and typed address-space/contiguity boundaries. Initial projection and
production-host checks pass 10/10, zero skipped, in 8 seconds
(`D:/NvtFwCombiner-TestArea/evidence/v114-memory-hover37/initial/initial.trx`).
These do not certify the hover/focus popup, final geometry or animation; the
hover unit remains uncommitted and incomplete. Source wording is separately
committed at `f1d223b9`, with review record checkpoint `2a44733c`.

Follow-up verification: removed a newly introduced clipping wrapper and
reused the original bar template directly. The existing shared/source/projection
checks now pass 49 cases; six actual-control geometry cases separately pass at
240/388/620 px in both bar styles (`shared-regression-fixed/` and `geometry/`
under the same evidence root). Geometry retains exact weights and bounds within
the existing one-pixel raster rounding, not an inflated minimum slice width.
This is reused narrow evidence, not a new full-suite or completed hover review.

Owner clarification during implementation: a terminal, ungrouped DP/TP region
should expose its information card directly on hover; a grouped CtrlRAM area
may first expose the local slices, then the same information card when a
constituent is hovered. Depth follows actual display grouping, not a hardcoded
DP/TP/CtrlRAM classification. The proposed card rises from the slice baseline
so the user need not scroll to the supporting cards. Keep the slice and address
anchors fixed; retain the exact range, size, source/state and existing Technical
details. The owner approved the updated
[one-level/two-level reference](../ui/references/v1.1.4-memory-hover-cards-approved.png)
on 2026-09-08, including the brief baseline reveal and reduced-motion fallback.
The generated pixels do not certify proportions; the prior in-flow detail-card
prototype is not accepted final geometry. A subsequent accepted constraint:
even when slices have the same source file, disconnected physical ranges must
not lift/highlight together. Interaction may span only valid, adjacent ranges
in the same declared address space. Keep logical source grouping for the rows;
a multi-run heading does not activate all disconnected bars. Unknown ranges,
unknown address spaces, gaps and overlaps do not establish physical continuity.
NT51928/Desay decisions remain pending independently.

Local implementation evidence (`UI-114-MEMORY-HOVER-37`, 2026-09-08): the
shared Merge/Replace rail now opens a nearby card directly for a terminal
slice, or a separate-scale local strip before the terminal card for a group.
The card uses a 140 ms fade/8 px reveal, disabled with reduced motion; main and
local strip geometry stays fixed. The existing card, colors and source/state
owners are reused. Logical source rows remain grouped, but physical interaction
is split at gaps, overlap, unknown ranges/spaces or address-space boundaries.

Targeted command (after the required test-area environment setup):
`dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore --filter 'FullyQualifiedName~MemoryCoverage|FullyQualifiedName~MemorySourcePresentationTests|FullyQualifiedName~MemoryProcessingPresentationTests|FullyQualifiedName~AbDummyDpControlTests'`.
Result: **73/73 passed, zero skipped, 23 seconds**;
`D:/NvtFwCombiner-TestArea/evidence/v114-memory-hover37/reviewed/reviewed.trx`.
Coverage includes typed grouping/proportions, disconnected-source interaction,
real keyboard/Technical details/Escape, pointer transit and delayed dismissal,
collection/disable/resize/scroll/detach cleanup, reduced motion, fixed 240/388/620
px rail geometry and source/AB regressions. Exact size-accounting tests pass
19/19 (28.131 seconds). No firmware/profile/runtime-slice change or new Golden
certification; this is not an integration/release full-suite run.

Actual full-window captures at 1440x900 EN Light / zh Dark are
`reviewed/memory-hover-{False|True}-{False|True}-direct.png` under that evidence
root (Merge/Replace). The reference's synthetic 512 KiB/8-slice geometry is
separately rendered with production controls at 388 px in
`reviewed/memory-popup-388-{light-en|dark-zh}-bottom.png`; it is not an IC flashmap
or Golden. Reference/actual comparison preserves one-/two-level hierarchy,
nearby range/size/source/status card and fixed anchors; existing app styling
and typed colors take precedence over illustrative bitmap pixels. Local-view
metadata stays on the inward side of its strip to avoid card occlusion.
Scoped independent Terra/high review clears the popup acceptance findings.
Native OS deactivation and physical display-DPI/screen-reader certification
were not simulated; close-on-deactivation uses the same reviewed cleanup.
Commit-bound final evidence is recorded in the hover37/size38 change records.

Reference-fidelity correction (`UI-114-MEMORY-FIDELITY-39`, 2026-09-08): the
owner's subsequent visual feedback showed that hover37's functional acceptance
did not establish full reference fidelity. The approved image is unchanged.
Correct the concrete deviations: local title/count above the rail and endpoints
below; remove the enclosing local-card border; use rounded rail framing, fine
slice boundaries and a non-scaling accent selection outline. Direct and local
cards share the reference notch/stem, title/status-first hierarchy and a
popup-only separator above Technical details. Address space and the processing
summary remain available inside the disclosure; persistent supporting cards
keep their prior facts and layout. At the 388 px reference viewport the card
occupies about 68% of the rail width; a 240 px readable minimum and 280 px
maximum keep narrow and wide layouts bounded.

Card clearance uses the measured local metadata extent on either side. A stem
crossing a header/endpoint label is interrupted behind that label's measured
bounds; its terminal still identifies the selected slice center. A flat opaque
local projection background prevents underlying supporting text from showing
through, without restoring an enclosing card frame. Main/local weights, ranges,
small-slice threshold, disconnected-source interaction and firmware output are
unchanged.

Evidence root: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-fidelity39/`.
`red/red.trx` reproduces the unwanted local border in both themes;
`width-red/width-red.trx` reproduces the oversized card (93% of rail width).
After both corrections, `compact/compact.trx` passes **79/79**, zero skipped, in **28 seconds**
using the same targeted command above. Coverage includes first/middle/last
selections at 240/388 px, above/below placement, stem endpoint error at most one
pixel, no card/text or stem/glyph overlap, and the prior grouping/lifecycle/
source/AB regressions. The old four rendered height-expansion checks now assert
the owner-approved non-scaling outline, exact bounds and existing accent color;
standalone legacy template tests are retained.

Actual full-window NT51950 CtrlRAM captures use the existing canonical input
fixture loader through public slot-loading APIs, not fabricated IC metadata:
`compact/memory-ctrlram-{False|True}-leaf.png` (1440x900, EN Light/zh Dark).
The first visible aggregate here contains declared small processing ranges;
its facts differ legitimately from the reference's illustrative CtrlRAM data.
`compact/memory-popup-388-{light-en|dark-zh}-bottom.png` retains the original
synthetic 512 KiB geometry for like-for-like grouping comparison, and
`compact/memory-hover-False-False-direct.png` covers the actual NT51929 direct
card. Primary visual comparison confirms the corrected hierarchy in these
complete captures. This fixture reuse is UI evidence, not fresh Golden-output
execution or release verification. Commit-bound review uses records39/40.

CtrlRAM primary-content correction (`UI-114-MEMORY-PLACEMENT-42`, 2026-09-08):
the preceding 79-case pass did not catch a semantic selection error: the first
aggregate captured above was a 100-byte Header/CRC trace, not a CtrlRAM payload.
The existing SPEC exclusion of Header/CRC primary content is now applied by
Application `MemoryLayoutSegment.IsPrimaryContent`, using canonical Header or
Checksum kind only. Runner transports that fact; primary rows, logical groups
and the shared flat template consume it without UI firmware heuristics.
Technical trace remains in the exact raw partition and coverage weights as an
inert spacer, not a hoverable content group. Ranges, operations, processor and
diagnostic facts, profiles and output bytes are unchanged. Moving onto trace
clears stale overlays; noncontiguous content is not joined across it.

Cards now stay within their memory-rail column, and the local view chooses the
side with more available vertical space instead of a fixed 380-pixel threshold.
Evidence root: `D:/NvtFwCombiner-TestArea/evidence/v114-ctrlram-primary41/`.
`geometry-red/geometry-red.trx` reproduces five placement failures and
`semantic-red/semantic-red.trx` reproduces two primary-row failures.
`application/application.trx` passes **31/31**, zero skipped (433 ms), covering
canonical Header/Checksum versus System Data, logical output, and unchanged
raw facts/partition. The final `reviewed/reviewed.trx` passes **85/85**, zero
skipped (**31 seconds**); `architecture/architecture.trx` passes the scoped
Memory Layout boundary check (**1/1**, 103 ms). The earlier `ui-green` run's two
new pointer-cleanup failures are resolved, not waived.

Actual NT51950 captures now select written NF, Normal and VN CtrlRAM by typed
role and selected-write state, rather than taking the first aggregate:
`reviewed/memory-ctrlram-{False|True}-{Nf|Normal|Vn}-leaf.png` (1440x900,
EN Light/zh Dark). NF exercises a two-level local view; Normal and VN exercise
direct cards. Primary visual inspection confirms the actual role/range/source
and in-column placement. These are rendered Avalonia test-window captures,
not native-window, physical-DPI or screen-reader certification. No fresh
Golden-output execution or release verification is claimed.

Pointer-exit correction (`UI-114-MEMORY-POINTER-44`, 2026-09-08): owner reported
that the local view/card stayed open after leaving it. Actual pointer clicks
gave the rail or popup keyboard focus; the old dismissal check treated any
focus as a reason to remain open. The shared rail now distinguishes input
origin: pointer press/movement within its main/local/card surfaces takes over
from keyboard focus, while keyboard navigation and interaction retain the
focused card. The existing 160 ms cross-surface transit delay is unchanged;
leaving the complete union closes both tiers, including after a card disclosure
click. Closing resets the interaction mode. Geometry, colors, source grouping,
firmware facts and the separate DiffDLM details-panel contract are unchanged.

Evidence root: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-pointer44/`.
`red/red.trx` reproduces all three clicked direct/group/local exit failures
while two hover-only cases pass. `mixed-red/mixed-red.trx` additionally exposes
four keyboard-to-pointer handoff failures in the first correction. The final
popup-focused `popup/popup.trx` passes **29/29**, zero skipped (**9 seconds**),
including both directions of input handoff, pointer traversal, disclosure
clicks, keyboard focus/Space/Escape and existing lifecycle/geometry cases.
The broader shared-memory/source/processing/AB regression command above passes
**96/96**, zero skipped (**35 seconds**), in `reviewed/reviewed.trx`.
These rendered-control tests do not certify a native multi-screen or
screen-reader session. No firmware output or release gate is claimed.

##### CtrlRAM endpoint layout — 2026-09-09

The [approved reference](../ui/references/v1.1.4-ctrlram-memory-layout-proposed.png)
is implemented in the existing-width Output layout panel. The real NT51927 /
3 IC fixture loads eight inputs and displays twelve targets in Master, Slave R
and Slave L lanes. Flash overview shows declared TP FW/DP and explicit gaps;
the detail lanes emphasize CtrlRAM rather than CRC/header cuts. Each continuous
target retains its exact range and independent shared hover card. Partial
replacement preserves the selected/retained pattern. Firmware bytes, primary
coverage, profiles and Report semantics are unchanged.

Application reuses the exact already-bound report metadata counterpart for
read-only context ([ADR 0052](../adr/0052-exact-ctrlram-report-metadata-counterpart.md)).
Missing context stays neutral: the current NT51950 fixture does not invent a
TP/DP classification, and its disconnected ranges remain two separate lanes.

Local evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-ctrlram-layout49/`.
`section-context-fixed.trx` passes 7/7; `focus-layout-regression-fixed.trx`
passes 60/60; `existing-layout-fixed.trx` passes 22/22, all zero skipped.
Coverage includes full/TP-only/context fallback, exact proportional geometry,
independent targets, partial replacement, shared popup lifecycle, 360/430 px,
Light/Dark and EN/zh-TW. Accounting tests pass 19/19; isolated Desktop build
succeeds. Actual production-control captures are `nt51927-threechip-actual.png`,
`nt51927-threechip-hover.png` and `nt51927-threechip-dark-zh.png` in that root.
These are headless full-window renders, not native DPI or screen-reader
certification. Native UIA confirms the preview loaded all eight inputs, but
OS capture is unavailable because its window crop is outside the captured
monitor. No full-suite, output Golden certification or release is claimed.
Commit-bound review is recorded in
[layout49](../governance/change-records/UI-114-CTRLRAM-MEMORY-LAYOUT-49.json) and
[size50](../governance/change-records/UI-114-CTRLRAM-MEMORY-LAYOUT-SIZE-50.json).

Hover-lift correction (`UI-114-MEMORY-LIFT-51`, 2026-09-09): the shared rail
root still clipped its 3 px raised target despite its inner track allowing
overflow. Disable only that root clip; retain 34 px geometry and outer scroll
viewport clipping. The actual 927 regression fails before the fix and passes
afterwards. `v114-memory-lift51/shared-final.trx` under the same test-area
evidence parent passes 39/39 (31 s), including six edge targets in both themes,
pointer dismissal, global reduced motion, shared keyboard/popups/geometry,
950 partial replacement and actual 928 Standard DP/TP/LDC inputs. Accounting
tests pass 19/19. These are scoped headless UI checks, not output certification.
Fresh renders: `nt51927-threechip-hover.png`,
`memory-ctrlram-False-overview.png` and `nt51928-standard-actual.png` in that
evidence directory. 950 retains neutral overview context; 928 Standard retains
its existing longer supporting-card list. Master/Slave hover-to-expand is
assessment only: if later approved, replace rather than duplicate the always
visible detail lanes, retain exact leaves and gaps, and support click/keyboard
alongside pointer transit, dismissal and address visibility.

##### Customer-information source presentation — 2026-09-08

Customer-information source follow-up (`UI-114-MEMORY-SOURCE-28`, 2026-09-08):
owner approved separating section purpose from byte source. The first bounded
unit changes only customer-information presentation in the existing flat/plain
rows and shared tooltip, plus the Plan source label. The heading now names
`Customer information` / `客戶資訊`; a separate caption renders the existing
typed source after the range. Existing protection detail, neutral hue,
reference-kept priority, Reserved/no-source fallback and firmware operations
remain unchanged. Grouped CtrlRAM layout, fill-byte explanation, CRC naming,
physical grouping and General conflict geometry remain later scopes.

Admission: [UI-114-MEMORY-SOURCE-28](../governance/change-records/UI-114-MEMORY-SOURCE-28.json).
Evidence root: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-source/`.
`red/red.trx` fails three NT51950/NT51951 source/localization cases against the
old Reserved label. `green/green.trx` passes 13 scoped cases. After adding range
before source ordering, `final/final.trx` passes 21 of 22; its sole failure is
an incorrect new AB test assumption. The canonical NT51929 AB family declares
CMI as command/code regions, not CustomerInformation. The corrected test asserts
that actual typed distinction and unchanged DP AB labels rather than inventing
a customer-information role. `final/source-corrected.trx` passes that case plus
two strengthened DP/no-source-before checks (3/3, 5 s). Combined final evidence
covers all 22 selected cases; it is not a fresh full-suite pass. Accounting tests
pass 19/19 in 27.180 s, with independently admitted full138763/allowance35867
(+19), unchanged runtime/slices and no new headroom.

Four `final/memory-source-<light|dark>-<en|zh>.png` captures exercise the actual
shared templates with a synchronous typed fixture. The primary inspected EN
Light and zh Dark including non-empty tooltip labels, range/source order and
protected wording. These are isolated template comparison frames, not fresh
full-window, native DPI or screen-reader acceptance. The previously stalled
async GoldenCtrlRam capture is not rerun or claimed fixed by this new seam.
Final source/review are bound in the admission record; this is local work,
not release or support promotion.

##### Memory initialization wording — 2026-09-08

Initialization wording follow-up (`UI-114-MEMORY-INITIALIZATION-29`, 2026-09-08):
the existing technical detail now says `Output initialization: 0xXX (before
writes).` / `輸出初始化：0xXX（寫入前）。`. This is the typed whole-output
initialization value, not a final per-range fill claim. Null/reference behavior,
operation details, Reserved and source labels, layout and firmware execution
remain unchanged. No new final-fill classification or production-line growth.
Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-initialization/`.
`red/red.trx`: four old-wording failures and two null-value passes.
`green/green.trx`: 16/16 passed, zero skipped, 7 s, covering FF/00/null,
with/without writes, EN/zh and real shared-template Light/Dark fit. Primary
inspected complete EN Light/zh Dark fixture frames; these are not native
whole-app or release evidence. Fixed-head review is bound in the
[admission record](../governance/change-records/UI-114-MEMORY-INITIALIZATION-29.json).

##### Memory Plan postprocessing wording — 2026-09-08

Generic postprocessing wording (`UI-114-MEMORY-POSTPROCESS-30`, 2026-09-08):
the existing generic declared-write action now displays `Replace + postprocess`
/ `替換 + 後處理`; English `Postbuild` becomes `Postprocess`, retaining
Chinese `後處理`. The current projection only establishes an external processor
write, not a specific CRC effect. Internal action identities, processors/CRC,
operation details, ranges and output bytes are unchanged. This does not claim
CRC is absent or implement a precise effect taxonomy.
Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-postprocess/`.
`red/red.trx`: 2/2 fail against the old CRC wording. `green/green.trx`:
22/22 pass, zero skipped, 7 s, including both actual Plan-row templates at
360 px in EN/zh and Light/Dark. Primary inspected EN Light/zh Dark fixture
frames; native workflow/accessibility and release remain separate.
Only two production lines are replaced, without code-size growth or ledger
changes. Fixed-head review is bound in the
[record](../governance/change-records/UI-114-MEMORY-POSTPROCESS-30.json).

##### Narrow region information cards — 2026-09-08

Owner-approved `UI-114-MEMORY-CARDS-31` replaces the duplicated physical
region list rows with one shared narrow card. Range, Size and Source are
vertically aligned; the existing role-color marker precedes the title and
the existing state badge remains separate. Processing details are collapsed
by default and use the shared keyboard-operable disclosure. Initialization
and operation rows format existing typed projection facts, not parsed prose.
Tooltip/accessibility still retain technical information; DiffDLM preservation
actions, logical grouped rows, bar geometry, firmware ranges and bytes remain
unchanged.

Reference: [approved narrow preview](../ui/references/v1.1.4-memory-region-card-approved.png),
amended by owner to retain the region-color marker. This is a generated design
reference, not a runtime screenshot. Actual [EN Light component render](../ui/references/v1.1.4-memory-region-card-actual.png)
uses a 380 px host with 348 px cards; collapsed/expanded states are shown for
the same Customer information fixture. [zh Dark render](../ui/references/v1.1.4-memory-region-card-dark-actual.png)
uses the same geometry. Customer information retains its existing neutral hue;
the UI does not recolor it as DP merely because DP is its source.

Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-cards/`.
`red/red.trx`: 4 failures because the old templates have no disclosure.
`verified/verified.trx`: 56/56 passed, zero skipped, 10 s; includes memory,
source, initialization, DP Perspective coverage, interaction and DiffDLM cases.
EN/zh Light/Dark render checks exercise keyboard expansion, text bounds and
metadata alignment. Stale old-layout assertions were updated to the approved
layout, retaining palette and preservation assertions. These isolated production
template renders do not establish native whole-app/DPI/high-contrast or release
acceptance. Fixed-head review is recorded in the
[admission record](../governance/change-records/UI-114-MEMORY-CARDS-31.json).

##### Quiet technical-details disclosure — 2026-09-08

Owner-approved `UI-114-MEMORY-DISCLOSURE-33` refines only the card footer:
`Technical details` / `技術細節` uses the existing caption style, aligned with
Range, in a 28 px native Expander header. The divider appears below that header
only when expanded. Native keyboard focus remains visible; card width, marker,
metadata, technical facts and other disclosures are unchanged.
[Approved preview](../ui/references/v1.1.4-memory-technical-details-approved.png)
and actual [EN Light](../ui/references/v1.1.4-memory-technical-details-actual.png)
/ [zh Dark](../ui/references/v1.1.4-memory-technical-details-dark-actual.png)
retain the 380 px host / 348 px card geometry.

Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-memory-disclosure/`.
Red: four cases fail on the old 48 px header. `verified/verified.trx`: 14/14
passed, zero skipped, 4 s, including measured header height, caption alignment,
collapsed/expanded separator visibility, keyboard focus/Space activation,
shared card variants and palette/preservation contracts. Scope remains isolated
production-template acceptance, not native full-app/DPI/high-contrast/release.
Review is bound in [record33](../governance/change-records/UI-114-MEMORY-DISCLOSURE-33.json).

##### Current Home and Settings inventory follow-up

On production source `c3c81ba2`, the primary inspected eight current real
`MainWindow` headless renders: Home, Preferences, Overview and Support Matrix,
each English Light/Dark at 1440x900 with asserted scale1 and actual theme.
Two additional existing isolated Preferences renders cover zh-TW Light/Dark
at 980x640; these prove template fit, not shell preference/theme synchronization.
The reproducible command and evidence boundary are in
[`tests/README.md`](../../tests/README.md#current-home-and-settings-inventory--2026-09-08).

| Surface | Observed disposition / next bounded action |
| --- | --- |
| Home | Existing cards and Open pills remain aligned in both themes. The stale DP-enumerating subtitle is corrected locally by `UI-114-HOME-SUBTITLE-21` below; availability and card layout are unchanged. |
| Preferences | Theme/language/reduced-motion controls fit their existing rows; actual full-shell theme agrees with selection. Keep current geometry. |
| Overview | Current-version/catalog/capability rows remain readable and bounded. No layout change justified by this inventory. |
| Support Matrix | Scroll-extreme/keyboard interaction checks completed locally in `UI-114-TOOLTIP-ESCAPE-24` below. Last cell is reachable in both viewports. First Escape now dismisses the tooltip without closing Settings; support facts and table styles are unchanged. Native display acceptance remains separate. |
| BIN Inspector | Production source search finds only the panel class/XAML and ViewModel definitions, not a constructor caller or host reference. Unit adapter evidence is not a reachable user-flow capture. Keep host/reachability unresolved; no component deletion or new host is authorized here. |

This unit adds only a repeatable test/capture seam and records observations.
The isolated test host can explicitly choose production capability policy;
existing Report tests retain their default historical DP regression policy.
Early captures using that historical policy and window-only theme assignment
are retained as superseded test-fixture evidence, not current product screens.
No product layout, firmware behavior, dependency or support claim changed.

Home subtitle follow-up (`UI-114-HOME-SUBTITLE-21`): the owner approved the
small correction on 2026-09-08. Existing localized strings now say
`Choose a replacement workflow.` / `選擇取代流程。`, avoiding an obsolete DP
availability claim without introducing another availability calculation.
The production diff replaces only these two strings, with no source-line
growth. Red evidence: four locale/theme cases rejected the old text. Green:
six scoped cases passed, zero skipped, 11 seconds (four real-shell locale/theme
cases, existing Home order and card-center regression). The primary inspected
all four complete 1440x900 Home frames against the preceding actual inventory;
geometry, controls and hidden DP entry remain unchanged.
Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-home-subtitle/`,
`home-subtitle-red.trx`, `home-subtitle-green.trx`, and
`inventory-home-<light|dark>-<en|zh-TW>.png`. Fixed-head review and final
checkpoint are bound in the capability record. This is not publication,
native display certification or a firmware/sub-version change.

The same localized subtitle is also consumed by the Replace page
(`MainWindow.axaml:172`); the neutral wording fits both consumers and leaves
the Mode selector unchanged. Independent scoped review passed at `859ea9f8`.
Existing CtrlRAM selector consumer checks passed 16/16, zero skipped, in
24 seconds at that head (`ctrlram/home-shared-subtitle.trx` under the evidence
directory above). The primary inspected the complete 1440x900 selected English
Light/Dark Replace captures. The existing long-filename last-character wrap
is still visible and remains a separate follow-up, not a fix claimed here.

Selected-filename follow-up (`UI-114-SLOT-FILENAME-22`, 2026-09-08): owner
approved moving the selected filename to a dedicated full-width bottom row.
The shared card reuses the same reveal button, binding, tooltip, automation
name, wrap behavior and style; its upper responsive grid and browse/clear
controls are unchanged. The visual contract records the placement amendment.
Red: eight selected-input layout cases fail against the prior layout; eight
empty cases pass. Final scoped UI checks: 73/73 passed, zero skipped, 27 seconds,
including real CtrlRAM pages, shared DP/TP card geometry, long wrapped text,
hover/click, browse and drop behavior. Six first-green failures were old
whole-card-center/fixed-height assertions superseded by the approved footer;
their replacement retains upper-row alignment and explicit height checks.
Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-slot-filename/`, `red.trx`,
`green/green.trx` (superseded), and `green-final/green-final.trx`.
The primary inspected full 1440x900 English Light/Dark selected captures and
980x640 English Light/Traditional Chinese Dark captures. All eight empty-page
PNG hashes exactly match the preceding Home-subtitle capture matrix. At the
narrow viewport the pre-existing Replace header/Mode layout remains cramped;
the English filename falls below the initial fold and its geometry is covered
by measured assertions, not claimed as visible in that initial screenshot.
Track the narrow page header in the remaining responsive-layout inventory;
this unit does not certify native DPI/High Contrast or release Golden output.

Narrow Header follow-up (`UI-114-REPLACE-HEADER-23`, 2026-09-08): the owner
approved correcting the previously observed clipped Replace title. Native
container styles retain wide geometry and move Mode/badge/Targets below the
full-width title/subtitle when the left content width is at most 700 px.
No new control, ViewModel, event handler or firmware behavior is introduced.
Red: eight narrow cases fail the measured text-width assertion, eight wide
cases pass. Final scoped tests: 18/18 pass, zero skipped, 24 seconds, including
both viewport directions of live resize, unchanged Mode and selected paths,
and existing mode-choice/shortcut regressions. All eight 1440x900 PNG hashes
exactly match the preceding filename-footer baseline. The primary inspected
English Light/Dark narrow frames and final Traditional Chinese Light/Dark
frames; title, subtitle and actions are now readable without overlap.
Evidence: `D:/NvtFwCombiner-TestArea/evidence/v114-replace-header/`, `red.trx`
and `final/final.trx`; exact +19 AXAML accounting is independently admitted
under `UI-114-REPLACE-HEADER-ACCOUNTING-23`. Native DPI/High Contrast and release
Golden remain separate. Next bounded item: Support Matrix scrolling and
interaction inventory; do not infer support-policy changes from that audit.

Support Matrix interaction follow-up (`UI-114-TOOLTIP-ESCAPE-24`, 2026-09-08):
real Settings checks cover 1440x900/980x640 English Light and Traditional
Chinese Dark, complete first/last-cell visibility after scrolling, typed
tooltip names/details, keyboard focus, focus-loss restoration and two-stage
Escape. Four red cases reproduced one Escape closing both tooltip and
Settings. The existing shared tooltip owner now consumes Escape only while
an open tooltip is dismissed; a subsequent Escape still closes Settings.
Final affected UI tests pass 35/35, zero skipped, 12 seconds; accounting
tests pass 19/19, 27.237 seconds. Evidence:
`D:/NvtFwCombiner-TestArea/evidence/v114-support-matrix/interaction.trx` (red)
and `green/green.trx`, with eight first/last-cell full-window captures. The
primary inspected desktop Light-last/Dark-first and narrow Light-last/Dark-first
captures. No table styles, support states, workflow selection or firmware bytes
changed. The IC column still scrolls horizontally with the table; freezing it
would be a separate visual decision, not a failure of last-cell reachability.
Native screen-reader/DPI/High Contrast and complete release gates remain open.

Subsequent owner-approved Support Matrix visual update
(`UI-114-SUPPORT-MATRIX-LAYOUT-25`, 2026-09-08) removes vertical grid lines and
full-cell color fills, centers equal workflow columns, uses 46 px rows,
places the legend below the table and moves hash/token into `Catalog details`.
Unlike the preceding interaction-only checkpoint, this approved scope fixes
the IC column during horizontal scrolling. Actual desktop/narrow geometry,
keyboard disclosure, resize and shared-tooltip regressions pass 35/35 in
12 seconds, zero skipped. Reference, actual full frames, state differences
and final checkpoint evidence are in the existing
[Settings handoff](../ui/v1.1.x-settings-version-handoff.md#support-matrix-layout--approved-2026-09-08).
No support facts or firmware behavior changed; native acceptance remains open.

Step 3 local evidence over `e5202e2707d272076d24216222188d478314a07d`:
`dotnet test --no-restore` UI filter `FullyQualifiedName~SlotLoading` passes
13/13 (Perfect pairs in both directions, partial/unrelated, same/unsupported IC,
header and unknown provenance; suppressed input remains selected). Bootstrap
`InspectionKeepsFilenameFirstBoundedIcHintSemantics` passes 1/1 and
`PerfectFamilyPairExcludesPartialAndUnrelatedIcs` passes 4/4;
architecture `PresentationUsesFocusedApplicationContractsInsteadOfConcreteAdapters`
passes 1/1. All run with the fixed local test-area environment; TRX files are
under `D:\NvtFwCombiner-TestArea\evidence\v114-perfect-filename-hint`.
The primary agent's scoped R2 review/Polytail and scoped capability-governance
validation pass. At that original checkpoint this was uncommitted local work,
not a fresh full-suite/Golden run or integration approval; the accumulated
code-size gate was still open.

For each step: inspect its real baseline and reproduce the specific issue,
show the bounded screenshot-based visual change for owner approval where
applicable, implement that one item, run affected tests and compare the actual
result, then proceed to the next. Do not wait for the whole-app inventory before
a genuinely independent small correction, or implement several items together.
If a small item reveals a material shared contract or unresolved owner decision,
record the exact dependency and requeue it rather than expanding its scope.
At the frozen integration boundary, run the combined UI regression and packaged
observation; retain all mandatory release Golden execution. Do not require a
complete release cycle after each small correction.

Measured startup/first-open performance is a separate `1.1.5` outcome, not
an omitted UI correction. New General/saved-rule/IC-rule authoring capabilities
below are product expansion, not permission to redesign existing screens in
`1.1.4`. Ordinary UI corrections do not change firmware semantics. The requested
Dummy DP implementation must pass its own firmware gates below.

### AB Dummy DP checkbox and firmware behavior

Implement the owner-requested [Dummy DP scope](../ui/v1.1.x-ab-dummy-dp-handoff.md):
the checkbox is unchecked by default; when on, every output section that does not belong
to TP is filled with `0xFF`, and DP input is not allowed. This is not merely
filling the section named DP, and it was not shipped in `1.1.2` or `1.1.3`.

Before implementation, approve the per-profile TP/non-TP ownership and complete
output/write-range contract, then obtain independent expected output and the
firmware-owner R3 evidence. Keep this feature's Golden gate with this version;
the separate `1.1.7` evidence backlog does not defer or waive it. No support
promotion or byte behavior is certified by scheduling alone.

## `1.1.5`: startup, first-open and local verification performance

Current allocation pointer, 2026-09-25: the owner brought Home startup/catalog
readiness optimization into [1.1.12](#owner-approved-1112-startup-optimization--2026-09-25).
The targets, deferral and measurements below retain their dated 1.1.5 meaning.

| Target | Measurement and implementation gate |
| --- | --- |
| Packaged Home first window | Close the retained [700 ms residual](../references/v0.10.4-startup-700ms-evidence.md): exact package on the controlled owner Windows machine, one unscored warm-up and five scored launches, median nonzero main-window handle at or below 700 ms; record cold behavior separately. The stable predecessor did not reproduce the target. |
| CtrlRAM Replace cold first-open | [Navigation handoff section 2](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#2-ctrlram-replace-cold-first-open-performance): establish cold/warm and per-stage baselines with fixed IC/topology/catalog/profile/window/theme/preload. Obtain a measured target and owner approval before optimization. The Home 700 ms threshold does not apply automatically. |

Reuse the existing preload, navigation, accepted-session and immutable
projection owners. Preserve visible loading, validation, slot/readiness
equivalence and bounded lifetime; no duplicate semantic path or unbounded
cache. Claims require comparable packaged measurements, not a stopwatch from a
different environment.

### Local full-verifier parallelization

Progress on 2026-09-12: the [opt-in four-worker full run](../../tests/README.md#opt-in-four-worker-complete-measurement--2026-09-12)
passed in 730.59 s (12 min 10.6 s); default remains three workers because the
single trial also showed increased contention. The owner has deferred further
Home startup tuning for now; the 700 ms target remains unachieved, not waived
as a pass. CtrlRAM first-open acceptance still requires its separately defined
baseline and target. The latest [local package refresh, 2026-09-13](../../tests/README.md#local-package-refresh--2026-09-13-unpublished)
on `dba19a30` took 214.13 s packaging plus 9.38 s non-UI smoke, with a
75,051,280-byte main EXE and 116,780,552-byte ZIP. Packaged Home window median
was 719.517 ms; the 700 ms target is still not achieved. Merge/Replace direct
startup smoke passed; this is not an in-process navigation latency result.
The [group/first-activation local fixes](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#5-v115-first-workflow-activation--2026-09-13)
and their affected tests are complete. The package remains an unpublished
development build retaining VERSION 1.1.4, not final-source admission or a
v1.1.5 release. Prior dated measurements remain in the test README.

Owner allocation on 2026-09-06 adds this work beside the existing startup and
first-open targets; it does not replace them or move `1.1.4` UI work. Target the
complete local `python scripts/verify.py --all` wall time at approximately
10 minutes, governed by the longest independent lane plus small shared
preflight/final-aggregation overhead, rather than the sum of lane durations.
The current `v1.1.3` baseline is 1,879.6 s: structure 145.2 s, three serial
script shards 440.0/353.1/288.9 s, CRC worker 12.2 s, and partially parallel
.NET 638.6 s. This target is not an achieved measurement or a timeout change.

Owner clarification on 2026-09-11: approximately ten minutes is an optimization
target, not a hard gate that indefinitely blocks subsequent startup/first-open
work. The latest full local candidate passed in 886.42 s (14 min 46 s); retain
the measured gap and remaining bottlenecks rather than calling ten minutes
achieved. See [the measured verifier results](../../tests/README.md#post-restore-overlap-measurement--2026-09-11).
Continue with packaged Home and CtrlRAM measurements; required tests, coverage,
Golden comparisons and failure gates remain unchanged.

| Work | Planned acceptance |
| --- | --- |
| Safe local overlap | Reuse the existing verifier/lane executor. Isolate mutable build/restore outputs, lock files, temporary data, evidence paths and cleanup ownership before overlapping independent script and .NET lanes. Preserve genuinely exclusive work until its shared-resource dependency is removed. |
| Accurate concurrency controls | Make the documented `--jobs` behavior and displayed policy match actual scheduling; a value of three must not imply top-level overlap when only one lane is submitted at a time. |
| Complete measured result | Measure the same controlled Windows machine, source, SDK, test inventory and declared cache state; record full-command wall time, each lane, shared setup and aggregation, with repeatability and contention checks. Do not move work outside the stopwatch to meet the target. |

Retain the complete applicable tests, Golden output comparisons, coverage,
identity/freshness checks and bounded cancellation/cleanup. Do not achieve the
target by skipping cases, weakening expected bytes or substituting CI evidence
for this full local run. The implemented local overlap is documented in the
[release/verifier contract](../ci/release-package.md); CI and release-Golden
entry points retain their own execution paths. Use the `1.1.4` test diagram/README rather than creating
another scheduler, verifier or evidence-document framework.

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
[current inventory](#family-reuse-inventory--2026-09-09) to trace Perfect and
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
