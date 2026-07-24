# Firmware Coordinate and Relocation Vocabulary

Status: accepted architecture vocabulary for the `0.10.x` maintainability
program. Runtime/schema adoption remains an implementation task.

## Purpose and authority

This document owns the shared meaning of firmware positions, stored addresses,
and relocation distances. It extends the canonical composition variable model;
it is not a memory map, profile, processor declaration, or source of new
firmware evidence.

Profiles and trusted bundles own evidenced values and ranges. Contracts own
their serialized representation. Domain types enforce arithmetic and address
space. Reports and UI reuse the terms here without inventing synonyms.

Every range remains half-open `[start, endExclusive)` and names its address
space. A bare number called only “offset” is insufficient at a public boundary.

## Canonical terms

| Term | Target code/schema name | Definition |
| --- | --- | --- |
| TP BIN offset | `TpBinOffset` / `tpBinOffset` | Byte position inside one immutable TP input artifact. Its origin is byte zero of that TP BIN. TPA and TPB can therefore expose the same source offset even when they are later pasted into different Flash banks. |
| Flash image offset | `FlashImageOffset` / `flashImageOffset` | Byte position inside the complete composed Flash image. Its origin is byte zero of the final Flash output address space. |
| Header stored address | `HeaderStoredAddress` / `headerStoredAddress` | Integer value encoded in a declared TP Header field. It is data contained by the header, not the byte position of the field itself. Its semantic basis must be declared by the field definition. |
| TP placement start | `TpPlacementStart` / `tpPlacementStart` | Flash image offset at which the declared TP section begins for one role/bank instance. |
| TPB placement delta | `TpbPlacementDelta` / `tpbPlacementDelta` | Signed distance from the TPA placement start to the TPB placement start. It is a displacement, not a byte position and not a stored header field. |
| Instance-relative offset | `InstanceRelativeOffset` / `instanceRelativeOffset` | Byte position measured from a named artifact, bank, section, or metadata-structure instance base. It becomes an absolute position only after resolving that instance base. |

“Shift” is not a canonical data field. In explanatory prose it may describe
adding a declared displacement, but code, schema, reports, and tests name the
specific value such as `TpbPlacementDelta`.

## Coordinate equations

For one resolved TP role/bank:

```text
Flash image offset =
    TP placement start + TP-section-relative offset
```

For current whole-block AB mappings whose TP input begins at the corresponding
section origin:

```text
TPB placement delta =
    TPB placement start - TPA placement start

TPB Flash image offset =
    TPA Flash image offset + TPB placement delta
```

For an explicitly declared stored-address field that follows TP bank
placement:

```text
relocated Header stored address =
    original Header stored address + TPB placement delta
```

The placement equation moves where TPB bytes appear in the final Flash image.
The relocation equation changes only the encoded values of explicitly selected
header fields. Applying one equation never implies applying the other, and the
same displacement must not be added twice.

## NT51950 and NT51951 examples

The currently evidenced TPA and TPB inputs both contain their TP Header at TP
BIN offset `0xA100`; neither input is pre-offset for its eventual Flash bank.

| IC | TPA Header Flash image offset | TPB placement delta | TPB Header Flash image offset |
| --- | ---: | ---: | ---: |
| NT51950 | `0x0A100` | `+0x40000` | `0x4A100` |
| NT51951 | `0x0A100` | `+0x80000` | `0x8A100` |

The header field bytes remain at TP BIN offset `0xA100` in the TPB work
artifact. Only the field's encoded value changes when the resolved behavior
binding identifies that field as a bank-relative stored start address. A later
paste places the TPB bytes into the B-bank destination.

These examples explain accepted coordinates; they do not authorize a range,
field, relocation, CRC write, or processor stage. That authority remains in
the reviewed profile/bundle and its evidence.

## Valid arithmetic

1. Add a typed instance-relative offset to the matching resolved instance base
   to obtain an absolute offset in that instance's target address space.
2. Subtract two positions only when they share the same address space and
   origin; the result is a typed displacement.
3. Add `TpbPlacementDelta` to TPB block placement once.
4. Add `TpbPlacementDelta` to a Header stored address only when an evidenced
   behavior binding references the exact field or its named group.
5. Convert a range only by converting its start and checked length within one
   declared mapping; retain half-open semantics.

## Invalid arithmetic and inference

- Do not add positions from different address spaces.
- Do not compare or subtract offsets whose origins are unspecified.
- Do not infer a stored address field from a label containing `start` or
  `address`.
- Do not relocate SRAM destinations, sizes, options, CRC fields, FirmwareConfig,
  CtrlRAM, or MP CtrlRAM merely because they appear in the same header.
- Do not use the processor staging-container offset as a public firmware
  coordinate.
- Do not infer mutation authority from the processor's read/transport scope.
- Do not store both an absolute range and an independently editable relative
  copy when one can be resolved from the other canonical definition.

## TP Header behavior binding

Each evidenced TP Header layout declares a named
`tp-bank-relative-start-addresses` field group containing only stored BIN-start
addresses that move with TP bank placement. Membership is a physical layout
fact. A composition behavior binding separately assigns each selected field to
one execution owner/stage and supplies the evidence and applicability.

This separation preserves one header geometry while keeping inspection,
relocation, Header Copy, CRC, external processing, memory projection, and
reporting from acquiring undeclared write authority.

## Consumer contract

- Domain values carry typed positions, displacements, and half-open ranges.
- Resolved plans carry references plus resolved applicability/state.
- Composition compilation chooses the exact behavior binding and execution
  owner.
- Infrastructure adapts any legacy staging coordinate internally and returns
  an independently verified diff.
- Presentation renders friendly labels but does not perform coordinate
  arithmetic or infer firmware meaning.
- Tests use these canonical names in new APIs and retain legacy aliases only at
  an explicit migration boundary.
