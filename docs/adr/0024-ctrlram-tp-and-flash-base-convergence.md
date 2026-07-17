# ADR 0024: CtrlRAM TP-BIN and Full-Flash Base Convergence

- Status: Accepted
- Date: 2026-07-17
- Owners: Architecture owner + firmware owner
- Supersedes: ADR 0023
- Amends: ADR 0015 and ADR 0020

## Context

The owner requires CtrlRAM Replace to accept both the TP work BIN and the full
Flash BIN. Both forms must execute the same replacement and Legacy Combiner
postbuild semantics. A full-Flash input must not cause the Combiner to receive
unrelated DP bytes, and processing must not change the container tail.

ADR 0023 temporarily restricted the non-routed NT51926 V2 candidate to exact
`0x3C000` TP input because processor operations previously staged the complete
output space. That restriction was safe but incomplete.

## Decision

Composition-profile schema 2.8 gives each Legacy Combiner stage an explicit
zero-based `targetViewId`. The shared
executor already stages an operation target range and imports the returned
bytes into that same range; the Domain contract now admits a zero-based prefix
while continuing to reject nonzero subranges.

NT51926 Common FW 1.4.1 cascade therefore binds two canonical maps:

- `0x3C000` TP work: clone and process the complete image.
- `0x40000` full Flash: clone the complete container, process only
  `[0,0x3C000)`, and preserve `[0x3C000,0x40000)` byte-for-byte.

Both map shapes resolve the same TP regions, staged CtrlRAM sources, selected
Combiner 1.13 invocation profile, read authority, and write authority. Input length alone
selects the canonical map. Every other length fails closed.

The exact command family, ordered blocks, arguments, source filenames/offsets,
and target ranges are loaded from `profiles/built-in/ctrlram-postbuild-v2/catalog.json`.
Infrastructure verifies its pinned SHA-256 before typed construction. This retires
the static C# command declarations without duplicating them in the V2 candidate;
the profile retains only the closed invocation profile id and processor authority.

## Consequences

- TP BIN and full Flash share one compiled workflow rather than two byte paths.
- Full-Flash DP/gap bytes never enter the Legacy Combiner staging image and are
  preserved by the engine-owned reference clone.
- The candidate remains non-routed and `executable-candidate`; this change does
  not promote runtime support.
- Legacy Combiner EXE/runner, staging isolation, host diff enforcement, and
  owner review remain mandatory.
- NT51926 Common FW 2.0.0, other IC/count branches, and optional CtrlRAM slots
  remain separate evidence/migration work.

## Verification

- Domain tests prove a processor can transform only a zero-based prefix and
  that the container tail remains unchanged.
- Profile/schema tests require the processor target view only in 2.8.
- NT51926 candidate tests select both exact capacities, pass only `0x3C000` to
  the processor, preserve the `0x4000` Flash tail, reject neighboring lengths,
  and execute against the hash-pinned Postbuild profile selected by id.
- Owner-supplied 1.4.1 cascade inputs still require full output parity through
  Legacy Combiner 1.13; runtime/support promotion remains an R3 owner gate.
