# ADR 0021 normative appendix — current-session System Activity

The owner approved the two-level System Activity history on 2026-08-22: one
existing Application-owned System Information service retains a bounded,
privacy-filtered current-session activity list; the default view shows
important events, explicit Debug disclosure adds user operations, and the
Message Center matches the accepted 1,536 by 864 wide reference in Light and
Dark. The former diagnostic-transition list is replaced, not mirrored, and no
new persistence or report-history owner is introduced.

Relative to the exact descending checkpoint immediately before this feature,
full production changes from 109,213 to 109,849 (+636) and runtime changes from
74,937 to 75,135 (+198). Domain plus Profiles remains 20,632; Application
changes from 33,225 to 33,423 (+198); Bootstrap plus CLI plus Desktop host
remains 3,503; and Infrastructure plus Contracts plus CRC worker remains
17,577. The remaining +438 is Presentation XAML/localization. The Application
delta includes fail-closed validation of the activity disclosure/category/
severity vocabulary so exported diagnostics cannot contain undefined enum
values.

The executable allowances therefore become exactly 6,952 full production,
5,078 runtime, and 2,732 Application above the frozen pre-v0.10.6 base
ratchets; every other slice allowance remains unchanged. This named amendment
is non-transferable and becomes an exact descending ceiling immediately. It
changes no firmware/profile/range/output byte, CRC/header, processor, support,
Golden, update-package, installation, activation, deletion, or report-history
authority. It also does not close or fund the owner-requested repository-wide
single-implementation, layering, unused-module, and code-size audit.

## 2026-08-22 CtrlRAM input-admission crash correction

The owner approved the bounded correction after the real NT51950 `0x37000`
TP-work base exposed two invariant exceptions during CtrlRAM authoring. The
correction reuses the selected CtrlRAM route's profile maps to reject non-exact
reference capacities as a typed input-length issue before canonical identity
resolution, and returns the canonical empty report-metadata plan when the
reviewed Standard Merge profile declares no report-classification authority.
No second capacity table, map resolver, or Presentation exception policy is
introduced.

Relative to the preceding exact checkpoint, full production changes from
109,849 to 109,883 (+34) and runtime changes from 75,135 to 75,169 (+34).
Domain plus Profiles remains 20,632; Application remains 33,423; Bootstrap plus
CLI plus Desktop host remains 3,503; and Infrastructure plus Contracts plus CRC
worker changes from 17,577 to 17,611 (+34). Presentation is unchanged.

The executable allowances therefore become exactly 6,986 full production,
5,112 runtime, and 2,255 Infrastructure/Contracts/worker above the frozen
pre-v0.10.6 base ratchets; every other slice allowance remains unchanged. This
named R2 correction is non-transferable and becomes an exact descending ceiling
immediately. It changes no profile, range, output byte, operation order,
CRC/header, processor, naming, support, or Golden authority, and it does not
close or fund the separate repository-wide single-implementation, layering,
unused-module, and code-size audit.

## 2026-08-22 exact CtrlRAM report-metadata counterpart correction

The owner then approved removal of the remaining CtrlRAM report-metadata map
heuristic. The correction adds one optional, hash-closed trust-index counterpart,
validates the complete CtrlRAM registration set before publication, materializes
the exact declared Standard-profile map through the existing compiler, and binds
the actual map in the Application capability contract. It deletes the former
capacity/TP-length search, candidate ranking, deduplication, and fallback. It
introduces no second map catalog, per-IC C# table, runtime discovery, or UI path.

After readability-preserving simplification, full production changes from
109,883 to 109,929 (+46) and runtime changes from 75,169 to 75,215 (+46).
Domain plus Profiles remains 20,632; Application changes from 33,423 to 33,429
(+6); Bootstrap plus CLI plus Desktop host remains 3,503; and Infrastructure
plus Contracts plus CRC worker changes from 17,611 to 17,651 (+40).

The executable allowances therefore become exactly 7,032 full production,
5,158 runtime, 2,738 Application, and 2,295
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets;
every other slice allowance remains unchanged. This named R2 correction is
non-transferable and becomes an exact descending ceiling immediately. It
changes no profile geometry, range, output byte, operation order, CRC/header,
processor authority, naming, support, UI, or Golden authority, and it does not
close or fund the separate repository-wide single-implementation, layering,
unused-module, and code-size audit.

## 2026-08-22 typed CtrlRAM authoring-diagnostic correction

The owner then approved carrying a failed CtrlRAM authoring compilation through
the shared firmware-inspection result to the existing visible slot diagnostic.
The correction preserves the compiler-owned issue code, operation id, severity
and message, and keeps Build blocked without adding a Presentation exception
catch-all, diagnostic service, ViewModel hierarchy, XAML control, or alternate
firmware rule.

After minimizing the hand-off to the existing `FirmwareInspectionStatusBatch`
and slot diagnostic owners, full production changes from 109,929 to 109,955
(+26) and runtime changes from 75,215 to 75,223 (+8). Domain plus Profiles
remains 20,632; Application changes from 33,429 to 33,435 (+6); Bootstrap plus
CLI plus Desktop host remains 3,503; and Infrastructure plus Contracts plus CRC
worker changes from 17,651 to 17,653 (+2). The remaining +18 is the existing
Presentation projection and exact diagnostic formatting; no layout changes.

The executable allowances therefore become exactly 7,058 full production,
5,166 runtime, 2,744 Application, and 2,297
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets;
every other slice allowance remains unchanged. This named R2 correction is
non-transferable and becomes an exact descending ceiling immediately. It
changes no profile, range, output byte, operation order, CRC/header, processor,
naming, support, Golden, report wire, update, or release authority, and it does
not close or fund the separate repository-wide single-implementation,
layering, unused-module, and code-size audit.

## 2026-08-30 SETUP-104 managed installation candidate

The owner approved the minimum backend slice for the future single-entry
Launcher and Setup flow. The change extends the existing Version Management
owners: Application classifies healthy launch, genuine first installation, and
recovery; Infrastructure uses one held-handle, relative, no-follow write tree
for both ordinary update and Setup. `ManagedPackageVerifier` remains the only
ZIP plan, copy, and hash owner. The Bootstrap restart path adds one bounded
inherited filename, length, and SHA-256 identity context; a manual launch stays
usable but receives no managed-restart authority.

At the pre-freeze candidate, full production is 128,423 nonblank lines and
runtime production is 90,765. Domain plus Profiles remains 20,632; Application
is 40,058; Bootstrap plus CLI plus Desktop host is 4,081; and Infrastructure
plus Contracts plus CRC worker is 25,994. The four runtime slices sum exactly
to 90,765. The increase from the first SETUP checkpoint is the bounded
promotion-custody closure and its typed result handling; it does not fund a
second installer, parser, updater, or firmware path.

The executable allowances therefore become exactly 25,527 full production,
20,709 runtime, 5 Domain/Profiles, 9,368 Application, 703 Bootstrap/CLI/Desktop,
and 10,638 Infrastructure/Contracts/worker above the frozen base ratchets.
This SETUP-104 allowance is named, exact, non-transferable, and provides no
reusable budget for later work; any reduction must lower the allowance. It
changes no firmware profile, range, operation, output byte, CRC/header,
processor, naming, support, Golden, or UI-layout authority. Later code-size
reduction remains a separate owner-reviewed task.

## 2026-08-30 Distribution Launcher and recovery-diagnosis candidate

The reviewed single-entry continuation adds one thin Distribution Launcher
composition host and the read-only RECOVERY-105A diagnosis. The Launcher reuses
the existing Bootstrap composition, Registry/Catalog/package experience, Setup
materializer, state store, and exact process handoff owners. Recovery reuses the
existing state/root probes, process lifetimes, stable no-follow custody, and one
strict Setup marker codec. Neither slice adds a second updater, parser,
repository, state writer, scanner, firmware executor, or mutation path.

Relative to the SETUP-104 checkpoint, full production changes from 128,423 to
129,271 (+848) nonblank lines and runtime changes from 90,765 to 91,613 (+848).
Domain plus Profiles remains 20,632; Application changes from 40,058 to 40,348
(+290); Bootstrap plus CLI plus Desktop and the new Distribution Launcher host
changes from 4,081 to 4,359 (+278); and Infrastructure plus Contracts plus CRC
worker changes from 25,994 to 26,274 (+280). The four runtime slices sum exactly
to 91,613. Distribution Launcher contributes 71 lines to the existing host
slice; it does not create a fifth runtime slice.

The executable allowances therefore become exactly 26,375 full production,
21,557 runtime, 5 Domain/Profiles, 9,658 Application, 981
Bootstrap/CLI/Desktop/Launcher, and 10,918
Infrastructure/Contracts/worker above the frozen base ratchets. This named R2
amendment is exact, non-transferable, and provides no reusable budget. The
approved 1.0.8 package/source-size task must remeasure the accepted product and
lower every reduced allowance rather than spend unused capacity. This amendment
changes no firmware profile, range, operation, output byte, CRC/header,
processor, naming, support, Golden, or Presentation-layout authority.

## 2026-08-30 bounded Launcher payload-admission correction

The fixed-head Launcher review found that the distribution host copied the
complete embedded Bootstrap before Application began its 250 ms entry budget.
The correction keeps the host as pure stream-factory wiring, moves bounded
descriptor and Bootstrap-metadata admission into the existing entry
coordinator, and reserves full streaming hash/capture for explicit Setup.
The owner set one 200,000,000-byte Launcher/Bootstrap executable safety ceiling;
this is not a package-size or optimization release gate.

After the correction and exact recovery-state identity binding, full production
is 129,509 nonblank lines and runtime production is 91,851. Domain plus Profiles
remains 20,632; Application is 40,425; Bootstrap plus CLI plus Desktop and
Distribution Launcher host is 4,323; and Infrastructure plus Contracts plus CRC
worker is 26,471. The four runtime slices sum exactly to 91,851. Relative to the
preceding candidate this is +238 total/runtime, +77 Application, -36 host, and
+197 Infrastructure.

The executable allowances therefore become exactly 26,613 full production,
21,795 runtime, 5 Domain/Profiles, 9,735 Application, 945
Bootstrap/CLI/Desktop/Launcher, and 11,115
Infrastructure/Contracts/worker above the frozen base ratchets. The allowance
is exact and non-transferable; the approved 1.0.8 reduction work must remeasure
and lower any reduced ceiling. No firmware, profile, range, operation, output
byte, CRC/header, processor, naming, support, Golden, or UI-layout authority
changes.

## 2026-08-30 Windows child-handle containment correction

The reviewed containment correction adds one dependency-free Platform process
owner, moves READY/START/ADMITTED/lifetime transport onto explicit Windows
handle allowlists, and captures inherited READY handles before descendants can
start. Infrastructure retains typed protocol binding; no second process policy,
updater, installer, state writer, or firmware execution path is introduced.

Full production is 130,153 nonblank lines and runtime production is 92,495.
Domain plus Profiles remains 20,632; Application remains 40,425; Bootstrap plus
CLI plus Desktop and Distribution Launcher host is 4,347; and Infrastructure
plus Contracts plus CRC worker and Platform is 27,091. The four runtime slices
sum exactly to 92,495. Relative to the preceding checkpoint this is +644
total/runtime, +24 host, and +620 Infrastructure/Platform.

The executable allowances therefore become exactly 27,257 full production,
22,439 runtime, 5 Domain/Profiles, 9,735 Application, 969
Bootstrap/CLI/Desktop/Launcher, and 11,735
Infrastructure/Contracts/worker/Platform above the frozen base ratchets. This
R2 allowance is exact and non-transferable. It changes no firmware profile,
range, operation, output byte, CRC/header, processor, naming, support, Golden,
or Presentation-layout authority.

## 2026-08-30 managed Setup recovery backend

The owner-approved RECOVERY-105B slice adds only the non-composed backend for
an explicitly confirmed managed Setup rollback or READY convergence. One pure
Application policy owns the closed state-pair table and canonical writer lease;
one Infrastructure adapter owns held no-follow evidence and deterministic
mutation. The adapter reuses the existing marker codec, state stores, package
manifest/checksum validator, root verifier, and Windows custody primitives. It
adds no production caller, UI, journal, updater, parser, state writer, recursive
delete, process launch, or firmware execution path.

Full production is 133,488 nonblank lines and runtime production is 95,830.
Domain plus Profiles remains 20,632; Application is 41,047; Bootstrap plus CLI
plus Desktop and Distribution Launcher host remains 4,347; and Infrastructure
plus Contracts plus CRC worker and Platform is 29,804. The four runtime slices
sum exactly to 95,830. Relative to the preceding checkpoint this is +3,335
total/runtime, +622 Application, and +2,713 Infrastructure/Platform. The
Infrastructure delta contains the held identity/length/SHA proof, every-boundary
prefix revalidation, action-specific final postcondition, marker-last custody,
and restart-prefix handling required for the destructive R3 boundary.

The executable allowances therefore become exactly 30,592 full production,
25,774 runtime, 5 Domain/Profiles, 10,357 Application, 969
Bootstrap/CLI/Desktop/Launcher, and 14,448
Infrastructure/Contracts/worker/Platform above the frozen base ratchets. This
R3 allowance is exact and non-transferable; any simplification must lower the
corresponding value. It changes no firmware profile, range, operation, output
byte, CRC/header, processor, naming, support, Golden, package, or Presentation
authority. Production composition remains a separate reviewed 1.0.6 slice.

The owner also rejected the former 700-physical-line repository gate as an
architecture proxy. The global file check is now only a 2,500-line
catastrophic-growth alarm. Cohesion is instead enforced through the existing
unique semantic-owner, dependency-boundary, duplicate parser/policy/writer,
and process-launch tests. Files are split when those boundaries warrant it,
not solely to satisfy a line count.

## 2026-08-30 Distribution Launcher recovery composition

RECOVERY-105C wires the already reviewed recovery diagnosis and execution
owners into the existing thin Distribution Launcher host. The host exposes one
exact-root session only for the typed `RecoveryRequired` result; diagnosis
remains read-only and only an explicitly confirmed action may invoke the
existing coordinator. The same state store, root probe, repository, marker and
lifetime probes, evidence/execution adapter, and canonical entry rerun are
reused. No process start, parser, policy, writer lease, or path inference is
added.

Full production is 133,610 nonblank lines and runtime production is 95,952.
Domain plus Profiles remains 20,632; Application remains 41,047; Bootstrap,
CLI, Desktop and Launcher is 4,469; Infrastructure, Contracts, worker and
Platform remains 29,804. Relative to RECOVERY-105B this is +122 total/runtime,
all in the host slice.

The executable allowances become exactly 30,714 full production, 25,896
runtime, 5 Domain/Profiles, 10,357 Application, 1,091
Bootstrap/CLI/Desktop/Launcher, and 14,448
Infrastructure/Contracts/worker/Platform above the frozen base ratchets. This
R3 allowance is exact and non-transferable. It changes no firmware, profile,
range, operation, output byte, CRC/header, processor, naming, support, Golden,
package, Registry, Catalog, or Presentation authority.
