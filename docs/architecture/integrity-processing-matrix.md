# Integrity and External Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

Owner update 2026-06-30:

- Replace is expected to require legacy `combiner.exe` CRC/header recalculation after the replacement mutations.
- `IC FlashMap` postbuild scripts now provide the first verified legacy Combiner 1.13.0 command sequences for CtrlRAM Replace. Postbuild remains the behavioral truth; mmap and TP Overview explain/document the ranges.
- Do not declare production Replace parity until each enabled profile has command shape, tool version, parameters, read/write ranges, execution order, and golden evidence.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | offsets `0x7164/0x7168/0x716C` | Evidence confirmed |
| NT51950 | uploaded AB combiner | Verify existing CRC | Relocate, recalculate, write CRC | CRC-32/MPEG-2, read `[0xA100,0xA130)`, write `[0xA130,0xA134)`; exact legacy combiner version/tool binding still required | Evidence confirmed; tool binding pending |
| NT51951 | uploaded AB combiner config | Verify existing CRC | Relocate, recalculate, write CRC | same algorithm/ranges; relocation differs; exact legacy combiner version/tool binding still required | Needs golden output and tool binding |
| Other Standard-reference ICs | `gen_flash_bin_v2` | Unknown | Unknown/not applicable | no integrity rule established by current evidence | Must inventory |

## Replace processing evidence

| Flow | Stage/purpose | Processor expectation | Current processor facts | Status |
| --- | --- | --- | --- | --- |
| CtrlRAM Replace priority flows | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation expected | Combiner 1.13.0 postbuild catalog implemented for NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, and NT51951; command sequence tests exist; real golden replace outputs still needed | Core command module implemented; golden parity pending |
| DP Replace priority flows | no CRC/header stage unless DP evidence says otherwise | preserve or overlay TP range when DP container includes TP area | 950/951 DP Replace workbench path clones an exact `0x100000` base image, replaces the full padded DP container, then restores TP `0x0A000-0x36FFF (len 0x2D000)` from base; other IC DP Replace mappings remain gated | 950/951 workbench path implemented; golden parity and production profile promotion pending |
| General Replace touching TP-classified ranges | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation required after the explicit mapping | Owner rule recorded 2026-07-03; profile compiler requires a later external processor operation for TP-classified explicit mappings. Exact per-IC tool binding, write ranges, command arguments, and golden outputs remain required before production enablement | Policy enforced at profile compile; production evidence pending |

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
