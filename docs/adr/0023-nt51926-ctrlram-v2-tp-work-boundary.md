# ADR 0023: NT51926 CtrlRAM V2 TP-Work Boundary

- Status: Accepted
- Date: 2026-07-17
- Owners: Architecture owner + firmware owner
- Amends: ADR 0015 and ADR 0020

## Context

The validated legacy NT51926 CtrlRAM product path handles both full-Flash and
TP-work artifacts. The non-routed V2 candidate instead passed its processor the
entire resolved output image. Declaring the full `0x40000` Flash shape therefore
gave the current generic executor no way to stage only the `0x3C000` TP prefix
and preserve/reinsert the Flash tail.

The candidate must describe only behavior the existing V2 contract and shared
executor perform exactly. Full-Flash V2 support cannot be inferred from the
legacy product path or from offsets that happen to fit within a larger image.

## Decision

The NT51926 Common FW 1.4.1 cascade CtrlRAM V2 candidate declares one exact
`0x3C000` TP-work map. Reference length selects that map; every other length,
including `0x40000`, fails closed.

The reference clone, output capacity, metadata search, processor target, and
processor read view are all `[0,0x3C000)`. Existing CtrlRAM inputs, staged
bindings, command order, tool binding, CRC mode, and processor write ranges are
unchanged; their highest end-exclusive address remains `0x3B800`.

The candidate remains `executable-candidate` and has no UI, CLI, or Application
runtime route. This decision does not change the validated legacy product path
or promote NT51926 support.

Full-Flash V2 processing is deferred until a generic processor-subrange
contract can clone the full artifact, stage only the declared TP prefix, audit
the processor result, and reinsert that prefix while preserving all bytes
outside it. This phase adds no Domain, Application, schema, executor, padding,
truncation, or filename-inference behavior.

## Consequences

- The V2 candidate truthfully returns one exact `0x3C000` TP-work artifact.
- A `0x40000` reference remains supported by the validated legacy product
  runtime, but is deliberately rejected by this non-routed V2 candidate.
- Full-Flash V2 promotion requires the generic prefix-stage/reinsert contract,
  direct expected-output evidence, allowed-diff proof, and firmware-owner
  review.

## Verification

- Profile and Bootstrap tests lock exact map resolution, processor target/read
  ranges, unchanged write authority, output length, undeclared-length rejection,
  marker bounds, and deterministic resolution/compilation fingerprints.
- Architecture and structure gates ensure the candidate remains non-routed and
  no new contract, schema, executor, command, CRC, or widened range is added.
