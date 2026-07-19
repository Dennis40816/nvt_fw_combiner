# ADR 0020: V2 Runtime Reference-Replace Admission

- Status: Accepted
- Date: 2026-07-15
- Amended: 2026-07-18 for the NT51926 processor-free DP runtime slice
- Owners: Architecture owner + firmware owner
- Amends: ADR 0015 and ADR 0019

## Context

General Replace starts from one immutable reference image and accepts explicit
runtime source-to-output mappings. Its current Bootstrap path constructs a
legacy C# profile after it has selected the base capacity, input bindings, and
mapping rows. CtrlRAM Replace shares the immutable reference/output model but
also requires R3-proven Combiner staging. Neither workflow may be represented
as a logical output: both require a resolved physical map and its write
constraints.

The existing V2 logical-output route is intentionally restricted to blank
General Merge output with no physical-map claim. Extending that route to
reference images would weaken its admission semantics. Adding a second
executor or a Bootstrap-owned byte planner would duplicate ADR 0015's single
engine boundary.

## Decision

Add one map-bound `runtime-reference-replace` compilation context. General
Replace is the first admitted experience. Schema 2.9 also admits a closed
CtrlRAM Replace candidate shape after direct Postbuild evidence became
available; that admission is compiler authority only and remains gated from
runtime routing and support promotion.

This acceptance authorizes only request-scoped candidate compilation and
Application admission. It does not register a built-in route, promote firmware
support, or retire the existing legacy General Replace path.

The declaration must contain exactly:

- one required, exact resolved-map-capacity `reference-image` singleton that
  initializes the output clone;
- one required, unnormalized, bounded `one-or-more` source slot with a
  per-binding input template: `auxiliary` for General Replace or
  `ctrlram-replacement` for CtrlRAM Replace;
- one resolved-map-capacity output image cloned from that reference;
- no metadata bindings or validations; General Replace has no static byte
  operations and may use only the schema-2.9 conditional processor shape,
  while CtrlRAM Replace requires that one final processor shape; and
- explicit canonical region-access rules that cannot relax the resolved map's
  physical write constraints.

A typed request supplies only concrete immutable source binding identities,
exact source lengths, and ordered explicit `ReplaceRange` mappings. The exact
singleton `reference-image` length selects the canonical map capacity. General
Replace still rejects multiple maps at that capacity. A CtrlRAM request may
add the profile-owned IC-number topology selection so the canonical resolver,
rather than Bootstrap, selects one single/cascade map. The request has no
independent map-capacity override. It has no
host paths, source bytes, commands, process arguments, decoded firmware facts,
or caller-supplied mutable buffers. The compiler materializes those bindings,
checks every source and target range, derives the target's canonical region
chain, and requires both profile access and physical map constraints to allow
each write. It then lowers mappings through the existing `CompositionPlan` and
`CompositionEngine`.

Reference-replace rejection is local to the selected compile/run request. It
does not alter Standard Merge, DP Replace, CtrlRAM Replace, General Merge, UI
catalog availability, or CLI routing. There is no legacy fallback after a
profile has been selected for this V2 route.

Application admits only the explicit `RuntimeReferenceReplaceV2CompilationContext`
with promotion stage `executable-candidate`, a reference singleton, and one-or-more
experience-owned per-binding sources. A generic resolved-map artifact cannot obtain this
admission from its input shape. This candidate admission is not a production route,
UI catalog change, or support promotion.

## CtrlRAM Replace Candidate Boundary

The CtrlRAM alternative is narrower than General Replace. Its experience is
`fixed`/`fixed`; every typed mapping target must resolve to the deepest canonical
physical region with `owner = tp` and `kind = ctrlram`; and the profile must
declare exactly one final schema-2.9 Legacy Combiner stage. DP, Header, CRC,
customer, reserved, and unknown regions never become CtrlRAM authoring targets.

Each supplied physical CtrlRAM file is one immutable per-binding source. The
request maps only the bytes that the reviewed Postbuild command can consume. A
short source replaces only its available prefix and leaves the remaining
reference-cloned bytes unchanged; an oversized source grants no authority beyond
the declared section maximum. The final processor runs over the host staging
copy with no staged-source or staged-artifact bindings because its physical
files are materialized from the already modified staging image.

The compiler also narrows the processor's CtrlRAM allowed-write ranges to the
actual request mappings. Profile-declared Header, CRC, backup, and other reviewed
processor-only ranges remain unchanged. This prevents an omitted CtrlRAM source
or the preserved tail of a short source from becoming allowed diff authority.
The Domain artifact independently requires every CtrlRAM processor write range
to correspond to a compiled mapping and retain containing physical-view
provenance.

This candidate contract registers no IC route. Each IC/topology/profile still
requires direct or owner-approved fact-scoped golden parity, exact command and
allowed-range review, and firmware-owner approval before its V1 consumer can be
removed.

## General Replace Product Boundary

The `runtime-reference-replace` admission above is a narrow migration and evidence
context. It is not the final General Replace product contract and must not be used
to reduce General Replace to one IC, one number selector, DP-only targets, fixed
CtrlRAM slots, or a fixed set of mapping rows.

General Replace remains the extensible advanced authoring experience defined by
ADRs 0003 through 0005:

- one immutable reference image is cloned before any mutation;
- one or more immutable source bindings may contribute any number of ordered
  explicit mappings, hexadecimal patches, or fills;
- mappings may target any range inside the profile-approved General Replace
  envelope, independent of DP/CtrlRAM persona categories;
- IC-number selection may choose a declared postbuild branch when a mapped range
  requires that processor, but it must not narrow a processor-free mapping; and
- TP and full-Flash reference shapes may both be declared when their exact
  address-space relationship and tail-preservation behavior are proven.

"Extensible" does not mean unbounded execution authority. Every target remains
range checked against one canonical physical map; protected ranges remain denied;
overlap is rejected unless a reviewed operation explicitly declares otherwise;
the user cannot supply scripts, commands, processor paths, or processor arguments;
and the host still verifies all external-processor writes against declared
half-open ranges.

Schemas 2.6 through 2.8 deliberately forbid processor stages. Schema 2.9 implements
the reviewed extension as a second closed shape: zero or one final profile-owned
Legacy Combiner stage, with no staged source/artifact authority. The compiler
lowers all request mappings first and appends that stage exactly once only when a
target intersects a canonical TP-owned region. TP-touching requests fail closed
when the stage is absent. DP-only requests do not execute it. Header/CRC write views
remain processor authority and do not grant user authoring access. No UI or
Bootstrap byte-execution branch selects the stage.

This compiler capability does not itself route a production profile. The current
NT51926 DP-only candidate remains a parity probe until an exact TP/full-Flash
General Replace profile, Legacy Combiner golden output, report/naming/UI/CLI parity,
and firmware-owner review close independently.

## Non-goals

- No new executor, operation kind, dynamic script, map resolver, or processor
  host is introduced.
- No firmware range, postbuild command, header/CRC rule, or support promotion
  is introduced by this context.
- CtrlRAM runtime routing, TP FW version editing, and AB behavior remain R3
  work with their existing owner-evidence gates.

## Migration and Verification

1. Add the schema/profile context and typed request with synthetic contract
   tests for clone ownership, unique bindings/mappings, bounds, overlap,
   reference immutability, profile/physical access denial, and fingerprint
   changes.
2. Register an `executable-candidate` General Replace bundle only after exact
   map/region evidence is available; compare byte output, report, naming,
   CLI, and UI behavior to the legacy path before routing production traffic.
3. Keep every TP-touching production route closed until its declared tool
   binding, reads, writes, staging trace, golden bytes, and R3 owner review are
   complete. Candidate compilation alone is insufficient.
4. Retire a legacy consumer only when the Legacy Retirement Matrix has direct
   evidence for every production consumer.

## v0.9.9 Candidate Evidence

The hash-pinned NT51926 bundle now contains
`nt51926-general-replace-dp-single-candidate`. It admits only an exact
`0x40000` full-Flash reference and explicit mappings wholly inside the
declared DP range `[0x3E000,0x40000)`. A dedicated General Replace map omits
the CtrlRAM map's FWConfig metadata locator because this compilation context
accepts lengths and mappings, not firmware bytes. The original CtrlRAM maps,
metadata resolution, and physical write constraints remain unchanged.

Direct regression runs the current Workbench/V1 compiler and this V2
candidate from the same immutable base and DP source and requires complete
output-byte equality. Separate tests reject the `0x3C000` TP-only base and
every TP/CtrlRAM target. This closes only the processor-free NT51926/single/DP
candidate slice.

## v0.9.9 NT51926 DP Runtime Amendment

The owner-directed v0.9.9 convergence routes exactly the already-proven
NT51926 `single`, `0x40000` full-Flash, file-backed DP mapping slice through
the executable-candidate V2 profile. The route predicate requires every
mapping target to be wholly contained by the canonical DP region
`[0x3E000,0x40000)`. It does not select V2 for patches/fills, cascade, TP,
CtrlRAM, protected ranges, other capacities, or other ICs; those requests
retain their existing behavior while their independent evidence remains open.

Direct tests force the former V1 path with the cascade selector and require
full output-byte parity with the routed single-selector result. CLI and UI
tests lock profile identity, report operations, output naming, immutable base
handling, and the absence of a processor stage. This closes the former
`full-route-parity` blocker only for that exact processor-free slice. The
profile remains `executable-candidate`; TP postbuild evidence and firmware-
owner review still block support promotion and retirement of the residual
General Replace V1 consumer.
