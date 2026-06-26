# Integrity and Python Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51950 | uploaded AB combiner | Verify existing CRC | Relocate, recalculate, write CRC | CRC-32/MPEG-2, read `[0xA100,0xA130)`, write `[0xA130,0xA134)` | Evidence confirmed |
| NT51951 | uploaded AB combiner config | Verify existing CRC | Relocate, recalculate, write CRC | same algorithm/ranges; relocation differs | Needs golden output |
| Other Standard-reference ICs | `gen_flash_bin_v2` | Unknown | Unknown/not applicable | no integrity rule established by current evidence | Must inventory |

## Canonical integrity dispositions

- `none`: reviewed evidence proves no integrity result is required for this profile stage.
- `verify-existing`: calculate and compare without mutation.
- `recalculate-and-write`: calculate after declared prior mutations and write to a declared range.
- `unknown`: evidence incomplete; profile cannot be released.

External authority is recorded separately:

- `calculate`: Python returns a result and cannot mutate a file.
- `transform`: Python may update only a host-created staging copy; host independently verifies the diff.

A transform may serve `header`, `checksum`, or combined `header-and-integrity` purpose. It is not itself an integrity disposition.

## Promotion requirements

For every IC/mode/stage, record:

```text
processorId and contract version
address-space basis
read ranges
write ranges
execution order
algorithm parameters
stored byte order
pre/postconditions
reference vectors/golden hash
firmware owner sign-off
```
