# ADR 0020: V2 Runtime Reference-Replace Admission

- Status: Accepted
- Date: 2026-07-15
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

Add one map-bound `runtime-reference-replace` compilation context. The first
admitted experience is `general-replace`; CtrlRAM Replace is deferred until
its Combiner evidence is independently complete.

This acceptance authorizes only request-scoped candidate compilation and
Application admission. It does not register a built-in route, promote firmware
support, or retire the existing legacy General Replace path.

The declaration must contain exactly:

- one required, exact resolved-map-capacity `reference-image` singleton that
  initializes the output clone;
- one required `auxiliary`, unnormalized, bounded `one-or-more` source slot
  with a per-binding input template;
- one resolved-map-capacity output image cloned from that reference;
- no static views, metadata bindings, operations, validations, or processor
  stages; and
- explicit canonical region-access rules that cannot relax the resolved map's
  physical write constraints.

A typed request supplies only concrete immutable source binding identities,
exact source lengths, and ordered explicit `ReplaceRange` mappings. The exact
singleton `reference-image` length uniquely selects the canonical map capacity;
the request has no independent map-capacity override. It has no
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
auxiliary per-binding sources. A generic resolved-map artifact cannot obtain this
admission from its input shape. This candidate admission is not a production route,
UI catalog change, or support promotion.

## Non-goals

- No new executor, operation kind, dynamic script, map resolver, or processor
  host is introduced.
- No firmware range, postbuild command, header/CRC rule, or support promotion
  is introduced by this context.
- CtrlRAM Combiner staging, TP FW version editing, and AB behavior remain R3
  work with their existing owner-evidence gates.

## Migration and Verification

1. Add the schema/profile context and typed request with synthetic contract
   tests for clone ownership, unique bindings/mappings, bounds, overlap,
   reference immutability, profile/physical access denial, and fingerprint
   changes.
2. Register an `executable-candidate` General Replace bundle only after exact
   map/region evidence is available; compare byte output, report, naming,
   CLI, and UI behavior to the legacy path before routing production traffic.
3. Keep TP-touching and Combiner stages outside the candidate until their
   declared tool binding, reads, writes, staging trace, golden bytes, and R3
   owner review are complete.
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
candidate slice. It does not register a production route or close report,
naming, CLI, UI, cascade, TP postbuild, or firmware-owner review gates.
