# ADR 0035: Scope AB slot-layout selection to NT51950

- Status: Accepted product and firmware-owner policy for `v0.9.15` on 2026-07-23
- Owners: Product owner, architecture owner, firmware owner
- Extends: ADR 0032
- Amended by: ADR 0036

## Context

Merge normally has no IC-number authoring control.  NT51950 AB is the one
owner-confirmed exception: `single` and `cascade` select two declared DP
container/CMD layouts.  They do not make TP firmware metadata an authority and
they do not imply that TP payload bytes need per-chip splitting.  NT51951 has
one byte plan for its observed single/cascade contexts and therefore has no
selector.

`IC Num` is retained as the operator-facing label because it is familiar in
the product.  Its two values are closed symbolic values, not a numeric range:
the UI displays `1 IC` and `Cascade`, while the typed request and CLI use
`single` and `cascade`.

## Decision

### Selector and observation policy

Only NT51950 AB Merge declares the selector.  Standard Merge, General Merge,
NT51919/NT51929/NT51932 AB, and NT51951 AB expose none.  A hidden selector
never produces an IC-number mismatch prompt and TP metadata never chooses a
plan.

When the visible NT51950 selector and observed TP FWConfig classification
differ, Presentation opens a confirmation dialog offering to change the
selection to the observed `1 IC` or `Cascade` value.  The user must confirm
before the typed selection changes; cancellation retains the chosen value. The
dialog neither invents a numeric count nor silently rewrites a request.

The selected value is bound into the profile compile request, Preview token,
Build request, report provenance, and CLI.  It selects one already-declared
layout; it is never inferred from a filename, DP/TP payload length, CMI/DP
version, PID, hash, or metadata value.

### Common TP contract

For NT51950 and NT51951 AB, TPA and TPB may be any length that covers the
half-open source prefix `[0x00000,0x37000)`.  Metadata/NVT discovery uses that
prefix even when the file has a longer tail.  The only copied code range is
`[0xA000,0x37000)`.  A source ending before `0x37000` fails before output;
longer tail bytes are report evidence but cannot affect metadata, copied bytes,
or output naming.

TPB's declared little-endian four-byte DIFF field is input-relative
`[0xA120,0xA124)`.  The shared engine mutates only its cloned TPB work buffer.
The postbuild stage then recalculates TPB TP-header/CRC fields using the
approved TP Flash Header definition and a host-created output staging copy.
No external processor may modify DP bytes.

### NT51950 layouts

| Selection | DP input/output | DP CMD/CMI base within each A/B slot | TPA target | TPB target | TPB DIFF addend |
| --- | --- | --- | --- | --- | --- |
| `single` | `[0x00000,0x80000)` | `0x3B000`; CMI `+0x16..+0x18` | `[0x0A000,0x37000)` | `[0x4A000,0x77000)` | `0x40000` |
| `cascade` | `[0x00000,0x100000)` | `0x05000`; CMI `+0x16..+0x18` | `[0x0A000,0x37000)` | `[0x4A000,0x77000)` | `0x40000` |

The cascade plan copies the complete 1 MiB DP input first.  Its tail
`[0x80000,0x100000)` remains DP bytes; it receives no invented second TP
overlay.  The same TP slots already contain the cascade-required TP content.
The profile owns the exact TPB header/CRC write ranges, derived from the TP
Flash Header catalog, and they must remain inside the TPB destination range.

### NT51951 selector-free layout

NT51951's single/cascade contexts use one selector-free plan:

| DP input/output | A slot | B slot | TPA target | TPB target | DP CMD/CMI base | TPB DIFF addend |
| --- | --- | --- | --- | --- | --- | --- |
| `[0x00000,0x100000)` | `[0x00000,0x80000)` | `[0x80000,0x100000)` | `[0x0A000,0x37000)` | `[0x8A000,0xB7000)` | bank-relative `0x05000`; CMI `+0x16..+0x18` | `0x80000` |

The TPB postbuild stage may write only the TPB header/CRC ranges declared by
the profile and TP Flash Header catalog.  Observed TP FWConfig count remains
informational and cannot prompt, select, or mutate a plan.

### Availability and certification

NT51950 `single`/`cascade` and selector-free NT51951 are function-open in
`0.9.15` once their declared profile/runtime/UI/CLI paths pass review.  Their
status is `Available — Golden certification pending`; it is neither
`Supported` nor `Certified`.  A missing direct golden does not stop Preview or
Build, but it remains visible in the report and Support Matrix and blocks
certification.

## Verification

- Profile/compiler tests reject a selector for all other Merge routes.
- UI/CLI tests accept only `single`/`cascade`, show `1 IC`/`Cascade`, and prove
  that metadata cannot silently change a selection.
- Tests cover short TP rejection, arbitrary longer TP inputs, fixed-prefix NVT
  parsing, ignored-tail independence, the complete DP copy, each slot target,
  TPB relocation, TP-only postbuild write ranges, source immutability, and
  atomic failure.
- Reports/Support Matrix distinguish function availability, direct golden,
  certification debt, and firmware-owner review without exposing private BINs.
