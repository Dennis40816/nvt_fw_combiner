# ADR 0032: Re-admit AB Code through typed production authority

- Status: Accepted for `v0.9.14` planning; implementation remains R3-gated
- Date: 2026-07-22
- Owners: Product owner, architecture owner, firmware owner

## Context

The repository already contains rejected V2 AB candidates for NT51919,
NT51929, NT51932, NT51950, and NT51951. They preserve useful direct and
fact-scoped evidence, but Application run requests remain rejected and the
Merge UI/CLI does not expose AB execution.

AB is not a larger Standard Merge or a CtrlRAM postbuild variant. Its current
candidate model includes a complete DP_AB initializer, TPA/TPB work buffers,
ordered overlays, bank-specific targets, checked scalar relocation, external
Combiner behavior, CRC/header authority, multiple output capacities, and
fact-scoped IC relationships. Reusing one workflow's admission rules without
review could therefore either reject valid production inputs for golden-only
reasons or admit a route whose byte authority has not been proved.

The `v0.9.12` CtrlRAM work separates production authority from golden identity.
AB needs the same separation, but it must derive its own typed discriminators
from AB evidence instead of copying CtrlRAM Common-FW or Number semantics.

## Decision

### Release boundary

Owner reschedule, 2026-07-22: AB architecture re-admission moves to `v0.9.14`.
`v0.9.12` and the `v0.9.13` stabilization release change no AB
profile, runtime route, processor authority, visibility, or support status.
Existing AB candidates remain hidden and fail closed at the Application run
boundary until the full gate below is complete.

### Production authority chain

An admitted AB route will use three explicit layers:

1. requested IC selects only an owner-declared AB family or fact-scoped
   applicability relation;
2. an effective AB profile is selected only when owner evidence proves a real
   byte-behavior boundary;
3. a typed AB build-plan selector chooses an owner-provided container, bank,
   topology, capacity, and command-plan shape.

PID, filenames, complete firmware SHA-256 values, golden fixture versions, and
one fixture's observed topology never select an AB family or production route.
They remain regression and report evidence. An AB metadata field may become a
selector only when its address, interpretation, interval/set semantics, and
different executable consequence are profile-owned and independently tested.

No AB profile is inferred from Standard Merge, DP Replace, CtrlRAM Replace, a
similar IC name, or a matching image length. Fact-scoped reuse does not imply a
whole-profile alias, processor equivalence, output capacity, or support stage.

### First pilot: NT51919/NT51929/NT51932 perfect family

The owner confirmed the following first-pilot facts on 2026-07-22:

- NT51919, NT51929, and NT51932 form one perfect family for this AB layout. IC
  Number is not an AB route selector, and the layout applies across firmware
  versions rather than only the existing T05/D06 fixture.
- `DP_AB` has a required and expected 512 KiB execution span. DP1 is
  `[0x00000,0x40000)` and DP2 is `[0x40000,0x80000)`. Each bank is an immutable
  DP view and reuses the same IC-owned three-byte CMI Reg16h-18h layout. For
  this family the in-view triple is `[0x401A,0x401D)`, producing container
  ranges `[0x401A,0x401D)` and `[0x4401A,0x4401D)`. Reg17h is DP major;
  Reg18h high nibble is DP minor; Reg16h and Reg18h low nibble retain Jira.
  Leading zeroes remain explicit, so major `0x00` and minor `0x0D` display as
  `D000D`. The legacy AB snapshot's `0x67/0x68` reader remains output-naming
  compatibility evidence and is not the cross-workflow Display contract.
- TPA and TPB have separate required and expected 256 KiB execution spans for
  this fixed plan. They may
  be the same file or different files and may carry equal or different TP
  versions. Each selected TP BIN is inspected independently using the
  owner/reference NVT terminal-relative (`T - 0xFFF`) TP version rule.
- Only the TPB scalar fields at `0x7164`, `0x7168`, and `0x716C` are relocated
  by `+0x40000`. This family has no additional CRC/header processor stage.
- NT51919 and NT51932 may use the reviewed perfect-family relation
  support-neutrally; their absence of separate direct goldens is not a
  production route discriminator or automatic support promotion.

Owner amendment, 2026-07-22: input selection reports a non-modal diagnostic as
soon as inspection finishes. DP_AB shorter than `0x80000`, or TPA/TPB shorter
than `0x40000`, remains Build-blocking because the compiled plan reads through
those required ends and no padding authority exists. A longer input is accepted
with a non-blocking warning: the immutable execution snapshot exposes only the
declared prefix/view, metadata extraction uses that consumed snapshot, and the
report preserves the actual source hash/length plus the ignored trailing byte
count. The original source is never changed. This is a profile/compiler/
Application policy, not a Presentation exception, and remains R3-gated until
boundary and golden tests plus firmware-owner review pass.

A missing or unreadable version reports `Unknown` with a non-modal warning and
does not select or reject the route. CMI metadata is used for human confirmation
and report traceability; any output-naming migration is reviewed independently.

Family/profile data owns the CMI layout and typed topology selector. Application
selects an immutable DP view for each bank and exposes typed metadata through
Bootstrap. Presentation receives four explicit values, never accepts a raw
offset, and never scans firmware bytes:

```text
DP_AB
  DP1  [0x00000,0x40000)  Dxxxx · AUTO_PRJ-n
  DP2  [0x40000,0x80000)  Dxxxx · AUTO_PRJ-n
TPA                           Txx
TPB                           Txx
```

The DP1/DP2 values appear as subrows of one DP_AB slot; TPA and TPB keep their
own slot rows. Equal values are not collapsed, and mixed A/B versions are
shown explicitly. Localized labels, technical-font values, keyboard reading
order, screen-reader names, and warning state are part of A5 acceptance.

### Reusable and AB-owned surfaces

AB continues through the one compiled composition boundary from ADR 0003 and
ADR 0015. It reuses the generic planner/compiler, checked operations, immutable
artifact snapshots, host-owned staging, constrained external-processor port,
mutation report, final write-range validation, and atomic output writer.

AB does not get a workflow-specific executor. The following remain AB
profile/adapter facts and must not move into Presentation or generic Domain
branches:

- DP_AB versus split DPA/DPB input shape;
- output and TPA/TPB work-buffer initializers;
- bank layout, target-bank views, and overlay order;
- source, copy, and backup roles and their direction;
- relocation-field address, width, byte order, expected-before value, and
  overflow rule;
- output capacity and customer-information ownership;
- processor identity, argv/command order, read/write views, CRC/header stages,
  and allowed final differences.

The current owner direction that the five candidate ICs initialize from a
complete submitted DP_AB container before declared TPA/TPB overlays remains the
baseline to audit. Changing that byte behavior requires new owner evidence; the
re-admission work does not infer an alternative initializer.

### Fail-closed construction

An AB plan is invalid unless all mutable spaces have exactly one initializer,
the final output space is unique, overlaps are explicitly declared, all bank
and relocation facts are resolved, processor ranges are closed, and the
selected capacity contains every operation and allowed processor write.

Missing or ambiguous authoritative metadata, unsupported bank/topology shapes,
undeclared copy/backup direction, unexpected processor changes, or a route that
exists only as golden evidence fail before output publication. Source inputs
remain immutable and no failure may commit a partial output.

## Delivery slices

Each slice is an independent commit/test/review boundary.

| Slice | Scope | Exit gate |
| --- | --- | --- |
| A0 | Inventory every current AB candidate, runtime rejection, UI/CLI gate, profile field, family/fact binding, processor, command, range, fixture, and support blocker. Record the exact released `v0.9.12` predecessor. | Reviewed authority/evidence matrix with no inferred facts. |
| A1 | Define typed AB profile and build-plan selectors, the four-value AB input-metadata projection, and invalid-combination rules. Identify which current checks are structural production authority and which are golden-only evidence. | Contract/property and version-parser tests; no runtime route enabled. |
| A2 | Normalize address spaces, initializer ownership, source/copy/backup direction, bank targets, relocation fields, output capacity, overlaps, and processor ranges in V2 profile data. | Profile/compiler tests and architecture review; old runtime rejection retained. |
| A3 | Compile each candidate through one AB route registry and the shared composition engine. Keep UI/CLI hidden and retain the old rejection boundary as rollback. | Negative admission, immutable-source, staging, range, and atomic-output tests. |
| A4 | Reproduce direct or approved fact-scoped output evidence per IC/plan. Compare V2, approved reference behavior, Legacy Combiner/Python where applicable, changed ranges, report facts, and source hashes. | Exact parity or a documented firmware-owner decision for every difference. |
| A5 | Add the reviewed AB authoring, Preview, failure explanation, and report flow without UI-owned bank semantics. | English/Traditional Chinese, keyboard, accessibility, no-output, and stale-context tests. |
| A6 | Remove superseded golden-identity gates and duplicate candidate paths only after A1-A5 parity and rollback review. Decide support exposure separately per IC/plan. | Full verification, Polytail, independent architecture/code review, Codex review, and firmware-owner approval. |

## Required evidence and tests

- prove filename, PID, complete input hash, fixture version, and unrelated
  informational metadata cannot choose or reject a production route;
- prove every genuine selector changes a compiled profile/plan fingerprint and
  invalid or overlapping selector definitions fail construction;
- cover DP_AB initializer order, TPA/TPB overlays, customer information,
  bank targets, scalar relocation, capacity boundaries, and deterministic
  operation order;
- verify independent DP1/DP2 and TPA/TPB version parsing, mixed-version display,
  explicit `Unknown` warnings, and prove version values cannot select or reject
  the perfect-family production route;
- prove Merge, DP Replace, preserved-base inspection, and AB bank views for one
  IC share the same CMI layout/decoder; topology selects only a declared offset
  variant and Presentation never supplies an offset;
- verify processor identity, exact argv order, declared read/write views,
  independent staged diffs, and rejection outside allowed ranges;
- verify source immutability, zero output authority on failure, atomic commit,
  report traceability, and no reread or mutation of user paths;
- verify one-byte-short, exact, one-byte-oversized, and large-ignored-tail cases
  independently for DP_AB, TPA, and TPB; short inputs commit no output, while
  oversized outputs equal the exact declared-prefix case and report the actual
  source identity plus ignored trailing length;
- for TPA/TPB, append a large tail containing different decoy terminal-relative
  version bytes and prove decoding still uses the declared `0x40000` execution
  snapshot end, while the full-file hash/length remain distinct report evidence;
- keep NT51929 and NT51950 direct evidence distinct from NT51919/NT51932 and
  NT51951 fact-scoped applicability; do not convert aliases into direct goldens;
- add architecture tests prohibiting AB byte semantics in UI/CLI and production
  admission by whole-file golden identity.

## Alternatives rejected

- **Copy the CtrlRAM route model verbatim:** AB selectors and bank semantics are
  different; shared types are allowed only where their meaning is identical.
- **Keep an exact golden tuple switch:** it rejects valid production inputs and
  confuses regression identity with firmware authority.
- **Infer by filename, IC number proximity, capacity, or Normal Merge layout:**
  none proves AB bank, relocation, or processor behavior.
- **Add an AB-only executor:** it would duplicate range, staging, reporting,
  processor, and atomic-output safety from the shared engine.
- **Promote all five candidates together:** direct evidence and fact-scoped
  applicability differ; support remains an IC/plan decision.

## Consequences

- `v0.9.12` remains behaviorally unchanged for every AB workflow.
- `v0.9.14` gains a larger R3 planning and evidence phase before any user-facing
  AB exposure.
- Existing profiles and tests are preserved as migration evidence and rollback
  until replacement parity is reviewed.
- Some current AB checks may remain because they prove structural authority;
  only golden-identity gates without production meaning are candidates for
  removal.
- Release notes must distinguish architecture readiness, executable candidate,
  direct golden parity, and support promotion.
