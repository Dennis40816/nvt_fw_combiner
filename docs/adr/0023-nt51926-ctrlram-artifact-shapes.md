# ADR 0023: NT51926 CtrlRAM Reference Artifact Shapes

- Status: Accepted
- Date: 2026-07-17
- Owners: Architecture owner + firmware owner
- Amends: ADR 0015 and ADR 0020

## Context

The non-routed NT51926 Common FW 1.4.1 cascade CtrlRAM V2 candidate may receive
either a TP work image or a full Flash image. Filename inference, bounded-size
admission, and a separate split/reinsert executor would make the selected
address space ambiguous and duplicate composition behavior.

## Decision

Keep one CtrlRAM Replace profile and declare two exact canonical maps. Reference
length alone selects either the TP work-image map at `0x3C000` bytes or the full
Flash map at `0x40000` bytes. Every other length is rejected.

Both maps reuse one TP-prefix region set covering `[0x00000, 0x3C000)`. The full
map adds only the forbidden, preserved tail `[0x3C000, 0x40000)`. Metadata marker
search and external-processor reads are restricted to the TP prefix. Existing
processor write ranges, command order, CRC/header authority, and tool binding
remain unchanged; their greatest end-exclusive address is `0x3B800`.

The existing resolved-map and artifact identities already bind map ID, exact
capacity, artifact SHA-256, and artifact length into deterministic fingerprints.
No Domain, Application, compiler, or schema extension is required.

## Consequences

- Output initialization clones exactly the selected reference capacity.
- A full Flash output preserves `[0x3C000, 0x40000)` byte-for-byte.
- A marker found only in the full-Flash tail cannot affect metadata resolution.
- The candidate remains non-routed and `executable-candidate`; direct golden
  parity and firmware-owner promotion review remain required.
