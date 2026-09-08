# ADR 0021 accepted-artifact snapshot consolidation amendment

Status: accepted normative appendix to
`0021-code-size-ratchet-and-convergence.md`.

## Decision

The 2026-08-23 fixed-workflow same-locator correction removes the duplicate
fixed/General accepted-artifact aggregation and centralizes the OS-aware
locator comparer.

With ordinary multiline formatting, full production decreases from 109,955 to
109,954 nonblank lines, runtime from 75,223 to 75,222, and Application from
33,435 to 33,434. The executable base ratchets therefore descend by one to
102,896 full production, 70,056 runtime, and 30,690 Application; existing
non-transferable allowances remain unchanged.

The consolidation retains one immutable reader snapshot only when accepted
`FileStamp` and complete bytes agree, while preserving every logical binding,
source view, operation, trace, and report identity. It changes no profile,
range, operation order, firmware byte, CRC/header behavior, processor, naming,
writer, support claim, Golden expectation, or release authority.

## Verification

- `python scripts/verify.py --structure-only`
- `python -m unittest discover -s tests/scripts -p test_code_size_policy.py`
- fixed-workflow accepted-session identity regressions for Standard Merge, AB
  Merge, and CtrlRAM Replace
- `python scripts/verify.py --all`

## Fixed-workflow bounded content-read admission

The owner approved decimal 100 MB (`100,000,000` bytes) as the inclusive hard
resource ceiling for fixed-workflow selected-file inspection, further narrowed
only by the exact compiled slot's declared maximum. The existing Application
compiled-input inspector owns the policy. Infrastructure groups immutable typed
bindings for one selected path, passes their minimum resolved ceiling to the
existing complete-file snapshot adapter, and rejects an oversized stream length
before allocating its retained byte array. General Merge/Replace retains its
separate existing resource owner.

Relative to the preceding extension-admission checkpoint, ordinary multiline
production code grows by exactly 80 nonblank lines. Full production changes
from 109,938 to 110,018 and runtime from 75,203 to 75,283. Application changes
from 33,415 to 33,445 (+30); Infrastructure plus Contracts plus CRC worker
changes from 17,653 to 17,703 (+50). Domain plus Profiles and Bootstrap plus CLI
plus Desktop host are unchanged.

The executable allowances therefore become exactly 7,122 full production,
5,227 runtime, 2,755 Application, and 2,347
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets.
They are non-transferable exact descending ceilings. The bounded read keeps
complete immutable bytes, SHA-256, `FileStamp`, source-view trailing diagnostics,
and evidenced CtrlRAM truncation behavior unchanged. It changes no profile,
firmware range, output byte, operation order, CRC/header behavior, processor,
output naming, support, Golden, UI, General authoring, or release authority, and
does not close or fund the separate repository-wide unused-module/code-size
investigation.

Additional verification:

- Application boundaries `99,999,999`, `100,000,000`, and `100,000,001`;
- Infrastructure sparse-file rejection before materialization;
- fixed-workflow and same-path Bootstrap wiring;
- exact-container, source-view, and CtrlRAM truncation regressions; and
- independent R2 architecture/contract and scoped Polytail review.

## 2026-08-24 explicit AB same-TP authoring

The owner approved an explicit Presentation-only `Use the same TP for A and B`
authoring option. It preserves the existing independent `tp-a-input` and
`tp-b-input` compiled bindings, operations, typed health and reports while
reusing the accepted immutable same-path snapshot behavior already owned by
Application. Presentation adds only the linked selector state, the focused
keep-TPA/keep-TPB/cancel conflict choice, localization, and the existing-card
filename marker. No executor, inspection adapter, profile, or firmware rule is
duplicated.

Relative to the preceding `SEL-02` checkpoint recorded in the canonical
handoff, full production changes from 110,066 to 110,383 (+317). The non-UI
runtime metric remains 535 files / 75,287 nonblank lines; Domain plus Profiles
remains 20,632, Application remains 33,474, Bootstrap plus CLI plus Desktop
host remains 3,503, and Infrastructure plus Contracts plus CRC worker remains
17,678. The executable full-production allowance therefore becomes exactly
7,487 above the frozen pre-v0.10.6 ratchet; every non-UI slice allowance remains
unchanged. This named R2 allowance is a non-transferable exact descending
ceiling and does not close or fund the separate repository-wide
single-implementation, layering, unused-module, and code-size audit.

Verification includes every approved enable/conflict/disable transition,
independent typed TPA/TPB health, locked TPB selection while linked, output-byte
and operation parity against two independent equal-content TP files, complete
UI Smoke, Architecture, structure/Polytail, and independent R2 review.

## 2026-08-24 shared selected-slot Clear action

The owner approved one shared Presentation-only Clear action for an already
selected fixed-workflow firmware slot. The action reuses the existing
`FirmwareSlotCard`, session selection lifecycle, typed readiness owners, and
dependent inspection refresh. It clears only in-memory authoring state, never
deletes the selected source file, and adds no parser, cache, profile, executor,
or workflow-specific service. The linked AB same-TP state clears its shared
selection through the existing TPA-owned lifecycle; independent slots remain
independent.

Relative to the preceding `SEL-03` checkpoint, full production changes from
110,383 to 110,547 (+164). The non-UI runtime metric remains 535 files / 75,287
nonblank lines; Domain plus Profiles remains 20,632, Application remains
33,474, Bootstrap plus CLI plus Desktop host remains 3,503, and Infrastructure
plus Contracts plus CRC worker remains 17,678. The executable full-production
allowance therefore becomes exactly 7,651 above the frozen pre-v0.10.6 ratchet;
every non-UI slice allowance remains unchanged. This named R1 allowance is a
non-transferable exact descending ceiling and does not close or fund the
separate repository-wide single-implementation, layering, unused-module, and
code-size audit.

Verification covers Standard Merge dependency recomputation, the NT51950
DP-first waiting-for-TP lifecycle, independent and linked AB selection clearing,
CtrlRAM base/region-derived state, source-file preservation, real shared-card
interaction, and exact 480/900 Light/Dark geometry without card reflow.

## 2026-08-24 responsive System Activity and startup duration

The owner approved a Presentation-led responsive boundary for the existing
System Activity modal and one current-session startup-duration event. The
surface replaces only its fixed width and height with equivalent maxima and
native stretch margins; the approved 1536x864 geometry, timeline composition,
filters, event store, and Debug opt-in remain unchanged. The duration reuses
the sole monotonic `StartupTraceSession`, is sampled once at the existing
required-ready seam, and is appended through the sole
`SystemInformationService` activity path. No timer, history store, responsive
control, or page-local scrolling implementation was added.

Relative to the preceding shared Clear-action checkpoint, full production
changes from 110,547 to 110,595 (+48). The runtime metric changes from 535
files / 75,287 nonblank lines to 535 / 75,289; Domain plus Profiles remains
20,632, Application changes from 33,474 to 33,476, Bootstrap plus CLI plus
Desktop host remains 3,503, and Infrastructure plus Contracts plus CRC worker
remains 17,678. The executable full-production allowance therefore becomes
exactly 7,699 above the frozen pre-v0.10.6 ratchet; runtime and Application
allowances increase by exactly two, while all other non-UI slice allowances
remain unchanged. These named R1 allowances are non-transferable exact
descending ceilings and do not close or fund the later architecture and
unused-module audit.

Verification covers the 980x640 application minimum in Light/Dark and English/
Traditional Chinese, unchanged approved wide geometry, monotonic measurement
without trace-file output, one required-ready publication guard, Important
default visibility, Debug opt-in, and localized duration text.

## 2026-08-24 focused shell-navigation owner

The A2 architecture slice moves navigation history, breadcrumb projection,
pending-clear state, and navigation commands out of the shell aggregate into
one typed `ShellNavigationViewModel`. The existing Main shell remains the page,
settings, and blocking-surface composition owner. Deferred modal creation,
navigation behavior, localization, page isolation, and every approved visual
layout remain unchanged; obsolete mutable breadcrumb-entry state is deleted.

Full production decreases from 110,595 to 110,594 nonblank lines while counted
runtime remains 535 files / 75,289 lines. The executable full-production
allowance therefore descends from 7,699 to 7,698; all non-UI slice allowances
remain unchanged. The MainWindowViewModel aggregate falls below its named
985-line ceiling.

## 2026-08-24 immutable run publication and resolved-map compiler phase

The A3/A11 slice retains one public compiler and one public run service while
extracting resolved-map lowering into a private compiler phase and constructing
all run evidence before publication. Six result properties become get-only.
Three result dimensions with no production writer (`HasRunReport`,
`ActionReadiness`, and `SuppressOutputInExternalReport`) and their unreachable
client branches are deleted. No firmware, report, delivery, readiness, or UI
behavior changes.

The same A11 boundary classifies each canonical route once, resolves it once,
and creates one disclosure per accepted catalog policy revision. Static routes
still materialize before dynamic routes, preserving the existing progress and
failure sequence without a process-global cache.

Full production descends from 110,594 to 110,559 nonblank lines. Counted runtime
descends from 75,289 to 75,272; Domain plus Profiles remains
20,632; Application descends from 33,476 to 33,473; and Bootstrap plus CLI plus
Desktop descends from 3,503 to 3,489. Their exact ratchets/allowances descend to
those values; Infrastructure plus Contracts plus CRC worker remains 17,678.

## 2026-08-24 focused Application-to-Infrastructure adapter boundary

The A7 slice removes sibling `InternalsVisibleTo` access from Application to
Infrastructure. Existing platform implementations cross through explicit
Application-owned ports and immutable adapter records; one generic compiled
slot-inspection port and one Standard Merge compilation port replace the five
concrete Application implementation leaks. Those concrete types remain
internal and Bootstrap remains their sole construction root.

Full production rises from 110,559 to 110,620 nonblank lines while counted
runtime rises from 75,272 to 75,333. Application rises from 33,473 to 33,533
and Infrastructure plus Contracts plus CRC worker rises from 17,678 to 17,679.
Domain plus Profiles remains 20,632 and Bootstrap plus CLI plus Desktop remains
3,489. The exact approved allowances therefore become 7,724 full, 5,277
runtime, 2,843 Application, and 2,323 Infrastructure/Contracts/worker. This is
accepted as the measured cost of replacing compiler-visible sibling internals
with narrow compiled contracts; it does not authorize later growth.

## 2026-08-24 approved Build settings A3/T2 reference

The owner approved the A3 compact review layout and T2 two-row TP-version
layout for the existing shared Build settings surface. The Presentation change
keeps the canonical output name locked until the pencil action, collapses the
already-typed source summaries behind one disclosure, and uses the existing
typed CtrlRAM preserve/edit state and bundle-destination validator. The output
filename and bundle folder name remain read-only until their respective pencil
actions. Parent-folder selection remains a direct folder-icon action whenever
the existing bundle-destination admission permits editing; it is not part of
the bundle-name pencil transaction. Header, scrolling body, footer, Light/Dark
theme tokens, localization, focus return, and the existing shared confirmation
remain within their current owners. No output naming, source acceptance,
validation, planner, executor, firmware, report, or delivery semantics change,
and no second semantic implementation is introduced.

The owner's 2026-08-25 bundle/parent and Sources screenshots are retained only
as before-state problem evidence, not final pixel authority: respectively
742x151 / SHA-256 `079f07196faaa8c2ad59afbc50f8b7c88d535ab8904314ab7aa746bba57cf9e3`
and 742x99 / SHA-256
`63025016904f5ef66e0c265c590dbdbc227381eb8c6b7aff2d5ffea0f48533f9`.
The collapsed Sources action therefore stays in its existing output-row
placement; only the expanded source list uses the full existing content width.

Relative to the `0.10.7` DP Replace exposure checkpoint, full production rises
from 110,688 to 110,876 nonblank lines (+188), entirely under Presentation.
Counted runtime remains 536 files / 75,383 lines, and every non-UI slice remains
unchanged. The executable full-production allowance therefore becomes exactly
7,980 above the frozen 102,896 ratchet. This owner-approved R1 visual allowance
is non-transferable, becomes an exact descending ceiling immediately, and does
not close or fund the separate unused-module and repository-size investigation.

Verification covers the shared ViewModel admission states, production-host
focus and interaction, 980x720 Light/Dark geometry, the T2 mode-above-fields
contract, unchanged collapsed Sources placement, full-width expanded source
content, two-line bundle review, output-name and bundle-folder-name pencil
locks, the direct parent-folder icon, and the canonical full repository gate.

## 2026-08-25 reviewed path-safety corrections

The owner authorized closure of the non-UI path-risk findings before the final
`1.0.0` review. Standard and AB CLI inputs now use the existing stable bounded
file adapter and compiler-resolved slot ceiling; firmware-inspection batches
reuse the existing platform artifact identity for valid aliases and retain the
typed unavailable result for malformed locators; every inspected IC hint now
waits for explicit confirmation. No second reader, identity policy, IC
classifier, planner, executor, or firmware rule is introduced.

Relative to committed checkpoint `ba1626af`, full production changes from
110,865 to 111,066 nonblank lines (+201) and counted runtime changes from
75,382 to 75,590 (+208). Bootstrap plus CLI plus Desktop changes from 3,498 to
3,665 (+167); Infrastructure plus Contracts plus CRC worker changes from
17,673 to 17,714 (+41); Domain plus Profiles remains 20,632 and Application
remains 33,579. The Presentation IC-confirmation correction removes seven
lines, which accounts for the difference between full-production and runtime
deltas.

The executable allowances therefore become exactly 8,170 full production,
5,534 runtime, 287 Bootstrap/CLI/Desktop, and 2,358
Infrastructure/Contracts/worker above their frozen base ratchets. All other
allowances remain unchanged. These values are non-transferable exact descending
ceilings and do not fund or close the separate unused-module, duplication, and
repository-size audit.

Independent R2 reviews found no remaining P0-P3 and confirmed that firmware
bytes, ranges, operation order, CRC/header behavior, processors, profiles,
output naming, support, and Golden authority are unchanged. Verification covers
the inclusive decimal 100 MB CLI boundary and oversized rejection, dynamic
Standard exact-route resolution, AB exact admission, case and relative path
aliases, malformed locators, IC-confirmation flow, Release build with zero
warnings/errors, Architecture, and the canonical final gate.

## 2026-08-25 supported CtrlRAM CLI admission and unreachable UI deletion

The final supported-CLI path audit extends the same stable bounded file adapter
to CtrlRAM base and replacement inputs. The CLI passes one accepted immutable
base snapshot into the existing Application/Infrastructure display projection,
then delegates exact geometry to the existing compiler and slot-inspection
owner. Standard missing-versus-unreadable diagnostics also consume the typed
adapter result without a second filesystem probe. No profile, range, mapping,
operation order, processor, CRC/header, output naming, firmware bytes, or
Golden authority changes.

The conservative unused-module audit separately deletes 163 nonblank
Presentation lines whose only references were declarations, initializers, or
stale tests: an unmounted AB A-FlashCode prompt already superseded by the live
Output Delivery Confirmation flow, one unreferenced report pager template, and
unused localization/style declarations. It does not delete or alter a mounted
control, live ViewModel call path, AppIcon, Saved Rule, Bin Inspector, hidden DP
Replace, General experience, profile, or Golden fixture.

Relative to the preceding reviewed path-safety snapshot, full production falls
from 111,066 to 111,031 nonblank lines (-35) while counted runtime rises from
75,590 to 75,718 (+128). Application changes from 33,579 to 33,602 (+23),
Bootstrap plus CLI plus Desktop from 3,665 to 3,730 (+65), and Infrastructure
plus Contracts plus CRC worker from 17,714 to 17,754 (+40); Domain plus Profiles
remains 20,632. The runtime increase is exactly offset by the 163-line
Presentation deletion in the full-production total.

The executable allowances therefore become exactly 8,135 full production,
5,662 runtime, 2,912 Application, 352 Bootstrap/CLI/Desktop, and 2,398
Infrastructure/Contracts/worker above their frozen base ratchets. These values
are non-transferable exact descending ceilings; the 35-line net reduction is
retained rather than reassigned. Independent Architecture and semantic reviews
must remain green, and the canonical final gate remains required.

The 2026-08-25 formal-route and managed-root integration snapshot subsequently
reduced Infrastructure/Contracts/worker from 17,754 to 17,714 nonblank lines.
Its non-transferable allowance is therefore lowered from 2,398 to 2,358. The
40-line reduction is retained and is not transferred to another slice.

The same integration review rejected argument-line consolidation as a valid
Domain/Profiles reduction. Readable formatting is restored; immutable input
lookup now uses one dictionary result, and clone-reference resolution owns its
one required initializer lookup instead of passing redundant state. These
semantic-preserving simplifications lower Domain/Profiles from 20,632 to
20,627 nonblank lines and the V2 compiler aggregate to 2,796. The exact
Domain/Profiles ratchet is therefore lowered to 20,627 with no transfer.

## 2026-09-04 UI AB context accepted-artifact accounting

The already accepted `UI-AB-CONTEXT-112-01` Presentation artifact adds exactly
24 nonblank production lines: +23 in `MergePresentationViewModel.State.cs` and
+1 in `WorkflowSessionPresentationViewModel.DeviceContextNotifications.cs`.
Full production therefore ratchets from 136,389 to 136,413. Runtime remains
98,559; Domain plus Profiles remains 20,632; Application remains 42,205;
Bootstrap/CLI/Desktop/Launcher remains 5,039; Infrastructure/Contracts/worker/
Platform remains 30,683; and all file counts and every runtime or slice
baseline and allowance remain unchanged.

The frozen full-production base remains 102,896, so the exact non-transferable
full-production allowance is 33,517. The 12-file
`WorkflowSessionPresentationViewModel` aggregate receives only its measured
2,627-line named ceiling. Neither value creates a transferable budget, permits
line packing, or funds unrelated deletion. This is accounting only: it changes
no UI product behavior, firmware/profile/range/output byte, support, or other
semantic contract.

The earlier immutable UI final record's structure-only success sentence is not
relied on. The reproducible focused policy test failed 1/19 solely because its
full-production threshold was stale at 136,389; this amendment closes that
accounting mismatch without rewriting the immutable record.

## 2026-09-07 existing v1.1.4 UI batch accounting

The owner explicitly approves a one-time exact allowance for the necessary
reviewed growth of the existing UI batch, after removing proven unused code.
This does not authorize Dummy DP or waive functional tests, Golden execution,
visual acceptance, firmware safety, or the final capability lifecycle.

Independent runtime and Presentation reviews of `fffc4bac..9513b27d` covered
all 20 changed Application/Infrastructure files and all 67 changed Presentation
files. The old two-card Message Center commands have no production consumers
after the approved direct Run reports list; removing their construction,
properties, notifications and handlers saves 26 nonblank lines without changing
the existing report/history owners or current navigation. The separate legacy
report-label projections still exercised by tests are not deleted here.

The canonical measurement after that deletion is:

| Metric | Previous exact ceiling | New exact ceiling | Necessary net growth |
| --- | ---: | ---: | ---: |
| Full production | 136,413 | 137,984 | 1,571 |
| Runtime | 98,559 | 98,890 | 331 |
| Domain + Profiles | 20,632 | 20,632 | 0 |
| Application | 42,205 | 42,522 | 317 |
| Bootstrap / CLI / Desktop / Launcher | 5,039 | 5,039 | 0 |
| Infrastructure / Contracts / worker / Platform | 30,683 | 30,697 | 14 |

The remaining 1,240 full-production lines are Presentation. Runtime growth
extends the existing typed diagnostic-evidence, ordered Build-blocker,
effective output-name and inspection-provenance owners; Presentation provides
the shared issue cards/history table, safe confirmations, clipboard handling
and approved authoring/display corrections. No second byte executor is added.

Keep all frozen base ratchets unchanged. Exact allowances become 35,088 full,
28,834 runtime, 11,832 Application and 15,341 Infrastructure/Contracts/worker;
Domain/Profiles stays 5 and Bootstrap/CLI/Desktop/Launcher stays 1,661. These
allowances are non-transferable, not headroom for later work. Further genuine
reductions lower the matching ceiling. Preserve the measuring algorithm,
exclusions, four-slice sum, duplicate checks, default 2,500-line partial limit
and existing named aggregate warnings; this amendment does not clear them.

The fresh pre-cleanup UI run on `9513b27d` executed 1,008 cases: 990 passed,
18 failed, zero skipped. Semantic review and this accounting decision do not
override those failures or certify complete visual acceptance. The current
batch remains non-mergeable until its applicable checks and final reviews are
actually complete. Exact-baseline tests must follow the measured ledger while
synthetic growth, reduction and slice-allocation regressions remain unchanged.

Accounting verification: all 19 focused code-size tests pass (30.624 s).
The subsequent structure lane completes in 157.6 s with no code-size error;
it still fails on the 12 committed-active UI records. Existing aggregate
warnings remain visible. Independent scoped accounting review found no
finding; neither that review nor these results finalize the UI batch.

The subsequent same-batch token cleanup removes the two unreferenced
`NfcWarningAccentCriticalBrush` definitions. Full production and its exact
allowance descend by two to 137,982 and 35,086 respectively; every runtime
slice remains unchanged. Equal-value shared-token substitutions and the
single-row vertical-content alignment correction do not add physical lines.
The former 137,984 checkpoint remains historical accounting, not reusable
budget. Resource reachability and real-control tests remain required.

## 2026-09-07 owner-approved AB Dummy DP accounting

The owner approves the exact necessary increment for the v1.1.4 Dummy DP batch,
including its reviewed naming, public execution and informational Report fixes.
The canonical measurement of the complete local candidate, against `d937747a`, is:

| Metric | Previous ceiling | Exact candidate ceiling | Delta |
| --- | ---: | ---: | ---: |
| Full production | 137,982 | 138,542 | 560 |
| Runtime | 98,890 | 99,170 | 280 |
| Domain + Profiles | 20,632 | 20,757 | 125 |
| Application | 42,522 | 42,647 | 125 |
| Bootstrap / CLI / Desktop / Launcher | 5,039 | 5,039 | 0 |
| Infrastructure / Contracts / worker / Platform | 30,697 | 30,727 | 30 |

The remaining 280 lines are Presentation. Existing base ratchets remain fixed;
allowances are exactly 35,646 full, 29,114 runtime, 130 Domain/Profiles,
11,957 Application, 1,661 Bootstrap/CLI and 15,371 Infrastructure/Contracts/worker.
There is no transferable headroom. The existing measurement algorithm, exclusions,
slice sum, duplicate checks, aggregate warnings and synthetic growth/reduction
tests are unchanged. This approval concerns accounting only; it does not grant
firmware support promotion, alter Golden expectations, waive final verification
or complete the capability-record lifecycle. Independent accounting verification
and fixed-head integration evidence remain required.

## 2026-09-07 v1.1.4 Report Changes presentation accounting

The owner-approved Report Changes correction adds exactly 34 necessary
full-production nonblank lines after independent R2 design review. The ceiling
moves from 138,542 to 138,576 and its exact allowance from 35,646 to 35,680
against the unchanged 102,896 ratchet. Runtime remains 99,170; every runtime
slice and allowance remains unchanged. The growth is confined to the existing
Presentation owners for the non-overlay range scrollbar, dedicated Light/Dark
Original colors, accessible range naming, and measured localized address
gutter. There is no transferable headroom.

The measuring algorithm, exclusions, partial-type limits, duplicate checks,
synthetic growth/reduction tests, firmware/report semantics and all verification
or release gates remain unchanged. This ledger records only exact source-size
accounting for `UI-114-REPORT-CHANGES-15`.

## 2026-09-08 v1.1.4 Report clarity presentation accounting

The owner-approved range-card and Report navigation clarification reduces full
production from 138,576 to 138,574 nonblank lines. The exact full-production
allowance therefore descends from 35,680 to 35,678 against the unchanged
102,896 ratchet. Runtime remains 99,170 and every runtime slice and allowance
remains unchanged.

The change stays inside the existing Presentation owners: it increases the
shared Current/Original address inset, derives complete visible range verdicts
from the existing typed acceptance value, replaces redundant selected/status
badges with one card hierarchy, and consolidates two Run reports actions into
one keyboard-accessible breadcrumb. It does not alter report JSON, range order,
acceptance, replay evidence, firmware semantics, output bytes, measurement
algorithms, duplicate checks, partial-type limits, or any verification or
release gate. This ledger records only exact source-size accounting for
`UI-114-REPORT-CLARITY-16`.

## 2026-09-08 approved Report range-card reference accounting

The replacement right-card presentation for `UI-114-REPORT-CARD-REFERENCE-17`
reduces full production from 138,574 to exactly 138,572 nonblank lines. Its
allowance descends from 35,678 to 35,676 against the unchanged 102,896 ratchet.
Runtime remains 99,170; all runtime slices and allowances are unchanged.
The existing card theme replaces two obsolete overriding item styles, reorders
existing display bindings and shortens the generic unaccepted reason. Existing
report-tab styles now apply to headers without overriding body text roles. There is
no new semantic owner or transferable budget. Measuring algorithms, exclusions,
duplicate checks, partial-type limits, report/firmware behavior and verification
or release gates remain unchanged.

## 2026-09-08 Report recorded-cause presentation accounting

For `UI-114-REPORT-CAUSE-18`, the canonical candidate measures exactly 138,577
full-production nonblank lines, five above the previous 138,572 ceiling.
Independent R2 review admits this necessary increment for the existing
Report cause projector: string-only legacy explanation input and neutral
classification fallbacks, retaining typed field-level explanation precedence.
The full allowance is exactly 35,681 against the unchanged 102,896 ratchet;
there is no transferable headroom. Runtime stays 99,170, with slices
20,757 / 42,647 / 5,039 / 30,727 and all their allowances unchanged. Measurement
algorithms, exclusions, duplicate checks, synthetic tests and release/Golden
gates are unchanged. This is not a firmware or CRC-calculation change.

## 2026-09-08 Settings Version presentation accounting

For `UI-114-SETTINGS-VERSION-19`, canonical full production is exactly 138,594
nonblank lines, 17 above 138,577. Independent R2 review admits that increment
for the existing Presentation owners: accessible per-row Catalog-note
disclosure, compact source/list arrangement and font-independent information
indicators. The exact allowance is 35,698 against the unchanged 102,896
ratchet, with no transferable headroom. Runtime stays 99,170 with slices
20,757 / 42,647 / 5,039 / 30,727 and all their allowances unchanged.
Measurement algorithms, exclusions, duplicate checks, synthetic tests,
version-management/firmware behavior and release/Golden gates are unchanged.

## 2026-09-08 incomplete imported Report accounting

`UI-114-REPORT-IMPORT-20` measures 138,704 full-production nonblank lines,
110 above 138,594. Application grows by 61 to 42,708 for the existing JSON
owner's minimal readable-evidence assessment; Presentation grows by 49 for
Unknown projection and stale-history normalization. Runtime is 99,231.
Exact allowances are full 35,808, runtime 29,175 and Application 12,018;
the frozen ratchets remain 102,896 / 70,056 / 30,690. Other slices, measurement
algorithms, exclusions, duplicate/partial checks and release/Golden gates
remain unchanged. This increment has no transferable headroom; independent
admission and fixed-head review are recorded in the batch capability record.

## 2026-09-08 narrow Replace header presentation accounting

`UI-114-REPLACE-HEADER-23` adds 19 nonblank AXAML lines for native container
queries on the existing header. Full production is exactly 138,723 and its
allowance is 35,827 against the unchanged 102,896 ratchet. Independent R2
admission confirms runtime 99,231 and slices 20,757 / 42,708 / 5,039 / 30,727
remain unchanged. This records only the approved responsive fix, with no
transferable headroom or changes to measurement, exclusions, duplicate/partial
checks, firmware semantics or release/Golden gates.

## 2026-09-08 shared tooltip Escape accounting

`UI-114-TOOLTIP-ESCAPE-24` adds exactly one production line to consume an
Escape event after dismissing an open tooltip. Full production is 138,724;
the exact allowance is 35,828 against the unchanged 102,896 ratchet. Runtime
99,231 and all slices remain unchanged. Independent R2 admission covers only
this necessary increment, not transferable headroom or changes to counting,
exclusions, checks, support/firmware semantics or release gates.

## 2026-09-08 Support Matrix layout accounting

`UI-114-SUPPORT-MATRIX-LAYOUT-25` removes 18 full-production nonblank lines
through its approved existing-template/style redesign. Independent R2 admission
`UI-114-SUPPORT-MATRIX-SIZE-25` lowers the exact full ceiling from 138,724 to
138,706 and allowance from 35,828 to 35,810 against the unchanged 102,896
ratchet. Runtime remains 99,231; slices remain 20,757 / 42,708 / 5,039 / 30,727.
The exact baseline test follows the new measurement; equality, counting,
exclusions, duplicate/partial checks and firmware/release gates are unchanged.
No headroom is retained or transferred from this reduction.

## 2026-09-08 Support Matrix scrollbar/focus accounting

`UI-114-MATRIX-SCROLL-FOCUS-26` adds exactly 12 production lines to the existing
UI owners for local scrollbar separation and a single themed keyboard outline.
Independent measurement/admission sets full production 138,718 and allowance
35,822 against unchanged ratchet 102,896. Runtime 99,231 and slices
20,757 / 42,708 / 5,039 / 30,727 are unchanged. The exact baseline test is
synchronized; counting, exclusions, ratchets and release/Golden gates are not
modified, and no transferable headroom is added.

## 2026-09-08 Support Matrix horizontal scrollbar styling accounting

`UI-114-MATRIX-SCROLL-STYLE-27` adds exactly 26 production lines to the existing
local horizontal scrollbar styles, matching the vertical owner without a new
template or interaction controller. Independent R2 measurement/admission sets
full production 138,744 and allowance 35,848 against unchanged ratchet 102,896.
Runtime 99,231 and slices 20,757 / 42,708 / 5,039 / 30,727 remain unchanged.
The exact baseline follows this measurement; no transferable headroom,
counting/exclusion changes, or release/Golden gate changes are introduced.

## 2026-09-08 protected Memory Layout source presentation accounting

`UI-114-MEMORY-SOURCE-28` adds exactly 19 production lines in existing
Presentation owners to distinguish customer-information purpose from typed
source. Independent R2 measurement/admission sets full production 138,763 and
allowance 35,867 against unchanged ratchet 102,896. Runtime 99,231 and slices
20,757 / 42,708 / 5,039 / 30,727 remain unchanged. The exact baseline follows;
no headroom, measurement/exclusion or firmware/release/Golden gate changes.

## 2026-09-08 narrow region-card consolidation accounting

`UI-114-MEMORY-CARDS-31` consolidates the two physical region-row layouts into
one shared card, reducing full production by 55 nonblank lines. Independent
R2 admission `UI-114-MEMORY-CARDS-ACCOUNTING-32` sets the exact full ceiling
and baseline to 138,708 and allowance to 35,812 against unchanged ratchet
102,896. Runtime 99,231 and slices 20,757 / 42,708 / 5,039 / 30,727 remain
unchanged. Equality, counting, exclusions and firmware/release/Golden gates
are unchanged; the reduction grants no transferable headroom.

## 2026-09-08 compact technical-details disclosure accounting

`UI-114-MEMORY-DISCLOSURE-33` adds exactly nine production lines for local
native-disclosure styling. Independent R2 admission `UI-114-MEMORY-DISCLOSURE-SIZE-34`
sets full production/baseline to 138,717 and allowance to 35,821 against the
unchanged 102,896 ratchet. Runtime 99,231 and slices 20,757 / 42,708 / 5,039 /
30,727 remain unchanged. No transferable headroom, measurement/equality,
exclusion, firmware, Golden or release-gate changes are introduced.
