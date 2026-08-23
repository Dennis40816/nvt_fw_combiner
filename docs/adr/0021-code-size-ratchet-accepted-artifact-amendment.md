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
