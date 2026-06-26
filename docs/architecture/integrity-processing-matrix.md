# Integrity and External Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51950 | uploaded AB combiner | Verify existing CRC | Relocate, recalculate, write CRC | CRC-32/MPEG-2, read `[0xA100,0xA130)`, write `[0xA130,0xA134)`; exact legacy combiner version/tool binding still required | Evidence confirmed; tool binding pending |
| NT51951 | uploaded AB combiner config | Verify existing CRC | Relocate, recalculate, write CRC | same algorithm/ranges; relocation differs; exact legacy combiner version/tool binding still required | Needs golden output and tool binding |
| Other Standard-reference ICs | `gen_flash_bin_v2` | Unknown | Unknown/not applicable | no integrity rule established by current evidence | Must inventory |

## Canonical integrity dispositions

- `none`: reviewed evidence proves no integrity result is required for this profile stage.
- `verify-existing`: calculate and compare without mutation.
- `recalculate-and-write`: calculate after declared prior mutations and write to a declared range.
- `unknown`: evidence incomplete; profile cannot be released.

External authority is recorded separately:

- `calculate`: processor returns a result and cannot mutate a file.
- `transform`: approved processor may update only a host-created staging copy; host independently verifies the diff.

A transform may serve `header`, `checksum`, or combined `header-and-integrity` purpose. It is not itself an integrity disposition.

## Tool binding requirements

Production CRC/Header transforms may be performed by approved legacy `combiner.exe` versions. Each such stage must additionally record:

```text
toolBindingId
toolId
toolVersion as exact string, e.g. 1.10
executable SHA-256
adapterId
argument template
input/output mode
timeout
platform
```

See `docs/adr/0006-external-combiner-tool-runner.md`.

## Promotion requirements

For every IC/mode/stage, record:

```text
processorId and contract version
address-space basis
read ranges
write ranges
execution order
algorithm parameters or tool parameters
stored byte order
pre/postconditions
reference vectors/golden hash
firmware owner sign-off
```
