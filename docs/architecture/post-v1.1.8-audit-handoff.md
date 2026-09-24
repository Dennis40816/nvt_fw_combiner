# Post-1.1.8 cross-version audit handoff

Status: existing reconciliation relocated on 2026-09-19; no new allocation or
implementation authority. The [roadmap](nfc_roadmap.md) owns version scheduling;
this handoff owns the linked audit acceptance detail. Original source and
review limitations are preserved below.


## Current allocation amendment — 2026-09-25

The owner accepted the [1.1.x repair allocation](nfc_roadmap.md#owner-approved-11x-repair-allocation--2026-09-25).
The current table below follows it; dated source descriptions retain their
historical allocation. Revalidate findings against the actual implementation.
F19 is shipped; F20/F21 remain partially completed; F24 shared-fact extraction
and DP retirement are implemented in the 1.1.10 candidate. F18 applies to each
repair. AB CtrlRAM independent evidence/owner checks and OSD intake are deferred
to 1.1.11 by the explicit one-release decision; they are not certified by it.
1.1.11 also temporarily hides Customized Merge/Replace UI entry points while
preserving implementation and data, with reopening tied to the corresponding
accepted feature release. Refer to the roadmap for the CLI-scope decision and
complete UI/firmware-review follow-up; this handoff does not create a second
version-allocation owner.

## Post-1.1.8 audit reconciliation — 2026-09-19

Owner request: correct and inventory the current handoff using the
[post-release shared review](https://chatgpt.com/share/6aae8e97-663c-83ee-b84c-dbf8018e774e).
This is documentation intake, not approval of all external specs, APIs,
timeouts, firmware choices or suggested resequencing. The roadmap remains
the sole version-allocation owner. No monitoring/agent-control system is
requested by this intake; the reference's earlier watcher proposal is obsolete
for the already published release.

### Source identity and scope

- Released runtime: `v1.1.8`, source
  `a2273c8798beb43b217b0ecfb9275af8fe4f9f96`, annotated tag object
  `1f73688cdeaefc205e4e9387d4b4b4dbb28cb23e`, published
  `2026-09-19T12:29:23Z`; release run `35441813386` succeeded.
- Planning baseline: `codex/1.1.9-intake` / `8344cb69` already includes the
  2026-09-17 F01–F26 allocation and 2026-09-18 brought-forward amendments.
  Those changes are not in the released roadmap. Thus the source report's
  "nine unscheduled findings" and "Customized not recorded" describe its
  release-document snapshot, **not this latest planning branch**.
- The original `codex/1.1.9-intake` planning branch has no new product commits;
  its older runtime tree lacks later release fixes. Inspect the release SHA,
  not this checkout's old runtime, when classifying shipped fixes. Before
  implementation, reconcile onto the approved release-based branch and check
  any newer implementation head; do not merge old runtime over `main`.
- Continuation check, 2026-09-19: the owner requested another omission check
  then `1.1.9` development. All 20 AUD rows and F01–F26 allocations remain
  present; Toolchain selection and CtrlRAM AB intake remain explicit. Local
  `1.1.9` starts at release source `a2273c87`, and
  `codex/1.1.9-long-name` replays only the seven planning/document commits
  through `6c62a3f3`. Its production tree at that checkpoint equals the release.
  The original intake branch is preserved; nothing was pushed or merged.
  Start with issue #434; the unresolved runtime-selection and AB firmware
  decisions below are not silently accepted by this continuation.
- Read the visible report, embedded AUD owner/failure-contract sections and
  complete handoff proposal. Downloaded ZIP/117 evidence excerpts and their test logs
  were not independently recovered; external reproductions remain attributed
  evidence. This update is not a fresh all-code audit or Windows/Golden run.

### Released corrections and remaining work

F19 is closed by the released staging owner correction. F20 is **partial**:
Report Save's picker/write/flush/dispose boundary and retry shipped; other
picker consumers remain to inspect. F21 is **partial**: snapshot-before-picker,
duplicate-save guard and success-after-disposal shipped; local atomic
replacement did not. These facts are backed by the
[release closure](../references/verification-report.md#118-published-release-closure--2026-09-19),
not by classifying every item in a related feature as complete.

### AUD to existing allocation and acceptance index

All remaining runtime rows below are **proposals pending scoped implementation
approval/current-head validation**. `SPECIFIED` is the external author's
definition status, not repository `ready-for-agent`. No new issue state is
created here. The following index refines acceptance and preserves existing
allocation; it does not wholesale adopt the external spec's exact APIs or
policy defaults. Completed/disposition rows are documentary evidence only.

| AUD / original finding | Planning coverage | Residual owner and observable acceptance / definition boundary |
| --- | --- | --- |
| AUD-00: F16/F19, HG01/HG10 | This handoff reconciliation | Bind release and planning SHAs; retain old evidence; subtract only shipped subitems. No runtime work. |
| AUD-01: F01/F02/F08/F18/F25, HG02 | `1.1.13` lifetime; F08 persistence in `1.1.12` | `MainWindow`, `LatestSnapshotPersistenceCoordinator`, run/inspection/Config/Report task owners: failed handoff can resume saving and close again; READY cancellation contained; observe terminal work before permanent disposal. Specified proposal, not a newly approved shutdown state machine. |
| AUD-02: F03/F06, HG03 | `1.1.13`, before full lifetime acceptance | `SystemExternalProcessRunner` / `BoundedProcessOutputReader`: cancellation callback signals; one owner bounds termination confirmation and pipe drain, including held child pipes/denied kill. Preserve F19 ownership. Proposed extra five-second cleanup budget and typed result/API shape need admission; not an existing profile timeout. |
| AUD-03: F07, HG04 | `1.1.12` | `CompositionRunService` and run presentation: keep exact committed path/size/hash after loose-delivery cancellation or Report failure; do not rerun processors to repair a report. Atomic bundles retain one transaction. Specified proposal. |
| AUD-04: F04/F20, HG05/HG06 | F04 `1.1.11`; F20 residual I/O `1.1.12` | Existing firmware/mapping/output/Hex/Settings picker and accepting session owners: capture original context/request before await; reject Cancel/Reopen or changed-slot stale returns without mutation; visible I/O failure and retry. Preserve legitimate preparation successors and shipped Report Save. Specified proposal. |
| AUD-05: F05, HG05 | `1.1.11` | `RawBinaryEditorFileSession` and Hex workspace: later selection wins at document mutation, not only UI publication; failed new Load preserves accepted bytes/path. Proposed edit/save disabling while loading remains reviewable, not accepted UX. |
| AUD-06: F21, HG06 | `1.1.12` | Existing `ILocalFileStore` / atomic-write owner with Report Save: failure before local commit preserves original destination; no half-file or false success. Nonlocal providers need explicit best-effort disclosure, not a fake filesystem path. Keep 1.1.8 snapshot/guard/error behavior. Specified proposal. |
| AUD-07: F09/F10/F12/F16, HG07/HG08 | F09/F10/F12 `1.2.5`; F16 `1.1.14`; DP retirement `1.1.10` | Run state/resources/mapping-row owners: language changes preserve drafts and typed results. Use Customized labels without changing IDs/commands/history; capacity text comes from the actual limit. Naming is already recorded in the [Settings handoff](../ui/v1.1.x-custom-options-layout-handoff.md), not a missing decision. |
| AUD-08: F11, HG07 | `1.2.6` | Shared styles/templates: remove proven same-role local overrides, retain named legitimate variants and approved rendered geometry; do not ban every XAML numeric value. Specified proposal. |
| AUD-09: F13/F14/F23, HG10 | F13 `1.2.6`; F23 `1.1.14`; F14 `1.2.8` | Existing accepted-output helper, command publication and `WorkflowInspectionSet`: preserve naming order; distinguish legal None from unknown mode; count duplicate notifications before changing them. Do not combine these into one global store or pull all into `1.2.6`. |
| AUD-10: F15, HG11 | `1.2.8` explicitly includes allocation work | **MEASUREMENT_FIRST**: engine after-range copy may be removed only after allocation/ownership evidence; preserve complete bytes, before/after hashes, diff ranges and ordering. No-benefit is a valid outcome; not an unallocated startup redesign. |
| AUD-11A: F22, HG11 | `1.2.7` | File snapshot/external output/Hex existing-limit owners: same-handle bounded read rejects short/growing/oversized content before full allocation; preserve source-stability checks. Specified proposal; no invented new capacity. |
| AUD-11B: F22, HG11 | `1.2.7` assessment; implementation **BLOCKED_RESOURCE_POLICY** | Aggregate materialization budget requires measured peak model, numeric limit and compatibility decision. Existing inline/single-file limits do not approve a total budget. |
| AUD-12: F17 | `1.1.14` | Desktop `Program` / `UiLaunchOptions`: missing value, next flag as value and invalid path produce controlled failure before host; preserve valid argument passthrough and handle cleanup. Proposed usage exit code 2 is not silently adopted. |
| AUD-13: F24, HG10 | `1.1.10` implemented; publication pending | Canonical metadata/family/trust-index owners: DPCMI and 19/29/32 Perfect relations survive without DP authoring registration; exact references and surviving workflow bytes remain valid. Existing prerequisite, not a new map per IC. |
| AUD-14: F24/F16 | `1.1.10` implemented after shared-fact extraction; F16 remaining text `1.1.14` | Retire only DP-specific routes/wiring/commands; retain DP input/shared Replace, metadata, old Report interpretation and required evidence. Update obsolete current-runtime reason, not historical records. |
| AUD-15A: HG09 | `1.1.10` implementation; remaining evidence `1.1.11` | Read-only per-member/layout/count/bank/material matrix from existing profiles: AB Merge and CtrlRAM postbuild availability separate; equal capacity is not layout identity. List missing independent inputs/expected BINs. Specified prework, not implemented here. |
| AUD-15B: HG09 | `1.1.10` Candidate implementation; independent evidence/validation `1.1.11` | The target-bank/default/source decisions and shared bank implementation are recorded in the 1.1.10 delivery. Remaining 1.1.11 work validates inverse relocation, B Header/CRC/finalization and independent complete outputs for the admitted shapes. Preserve non-target bank/DP/customer bytes and Candidate/ContractOnly; the release exception is not Golden certification. |
| AUD-16A: F26, HG12 | `1.1.14` | Worker lexical JSON boundary: oversized integer returns one structured request error rather than empty stdout/traceback; keep digit/payload limits and valid CRC result. External 5000-digit reproduction is not a test rerun by this update. Specified proposal. |
| AUD-16B: HG12/N01 | New policy intake; **unallocated / BLOCKED_PROTOCOL_POLICY** | Decide duplicate-key last-wins versus strict rejection and compatibility. Observation is not a proven contract violation; do not block F26 or silently change all JSON consumers. |

Coverage check: the 20 rows retain all F01–F26 and HG01–HG12 references.
F18 remains cross-cutting behavioral/interleaving evidence, not a separate
last-stage task. HG items elaborate existing findings; do not count twelve
additional bugs. The external totals (16 SPECIFIED, one MEASUREMENT_FIRST,
three BLOCKED) describe proposal completeness only, not twenty approved jobs.

Dependency review before implementation: AUD-02 termination facts precede
AUD-01's full shutdown acceptance; AUD-03 committed-result facts precede the
corresponding AUD-07 typed projection; AUD-13 precedes AUD-14; AUD-15A and
owner evidence precede AUD-15B. AUD-11B depends on AUD-11A assessment.
Shared `MainWindow`, picker/Report and external-adapter surfaces need one
writer at a time; separate AUD IDs do not imply independent writable scopes.
Do not make this dependency inventory a reason to move all work into `1.1.9`.
