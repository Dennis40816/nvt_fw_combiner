# Integrity and External Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

Owner update 2026-06-30:

- Replace is expected to require legacy `combiner.exe` CRC/header recalculation after the replacement mutations.
- `IC FlashMap` postbuild scripts now provide the first verified legacy Combiner 1.13.0 command sequences for CtrlRAM Replace. Postbuild remains the behavioral truth; mmap and TP Overview explain/document the ranges.
- Do not declare production Replace parity until each enabled profile has command shape, tool version, parameters, read/write ranges, execution order, and golden evidence.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Local full-byte candidate/reference parity confirmed (`2cc711...fd57f4`); product golden/owner promotion pending |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Candidate profile evidence confirmed; independent product golden/owner promotion pending |
| NT51950 | uploaded AB combiner | Verify existing CRC | Relocate, recalculate, write CRC | Reference result uses CRC-32/MPEG-2 over `[0xA100,0xA130)` and writes little-endian `u32` at `[0xA130,0xA134)`. A private Combiner 1.13.0 audit has exact output parity; see the dated evidence below. | Evidence confirmed; profile and firmware-owner promotion pending |
| NT51951 | uploaded AB combiner config | Verify existing CRC | Relocate, recalculate, write CRC | same algorithm/ranges; relocation differs; exact legacy combiner version/tool binding still required | Needs golden output and tool binding |
| Other Standard-reference ICs | `gen_flash_bin_v2` | Unknown | Unknown/not applicable | no integrity rule established by current evidence | Must inventory |

## 2026-07-14 NT51950 AB private Combiner audit

The owner-supplied private NT51950 AB sample is not committed. Its staged
reference output SHA-256 is
`4A292CD9615C58079B8994AF8060AF92562EAA92A55BC24BACC5EC5234E23B30`.
The same hash was produced by legacy `Combiner.exe` 1.13.0
(`ED6B58289CC780F73D36B831F5424CEF44AD93187BA7518D36DF6A77AD0C76BF`)
using `NT51950BASED_NORMAL_MODE` with its `CRC8` selector.

`CRC8` is the legacy command selector, not a claim that the AB header result
is an eight-bit CRC. The resulting TPB header stores the same CRC-32/MPEG-2
value as the reference output after relocation. The verified staged mutation
ranges are `0x4A102`, `0x4A112`, and `[0x4A130,0x4A134)`; the last range is
the little-endian header CRC word. The future V2 AB profile must declare those
writes and must use the external Combiner transform. C# must not recalculate
or write the header CRC.

This is direct NT51950 evidence only. It does not establish the NT51951
topology, its `0x80000` TPB relocation, or an NT51951 Combiner binding. The
isolated reproduction environment additionally requires the legacy tool's
`map.txt`; that environment input must be captured with the eventual
owner-approved AB golden before the production profile is promoted.

## Replace processing evidence

| Flow | Stage/purpose | Processor expectation | Current processor facts | Status |
| --- | --- | --- | --- | --- |
| CtrlRAM Replace priority flows | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation expected | Combiner 1.13.0 postbuild catalog implemented for NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, and NT51951; NT51926 and NT51930 now select postbuild category from base FWConfig Common FW version; command sequence tests exist; 2026-07-05 private NT51926/NT51927 self-replacement fixtures execute through workbench, but real expected golden replace outputs are still needed | Core command module implemented; golden parity pending |
| DP Replace priority flows | no CRC/header stage unless DP evidence says otherwise | restore only the profile-declared TP range when DP container includes TP | 950/951 DP Replace clones an exact base image whose length must be `0x40000`, `0x80000`, or `0x100000`, replaces the full padded selected-length DP container, restores TP `0x0A000-0x36FFF (len 0x2D000)`, and keeps customer info `0x37000-0x37FFF (len 0x1000)` from replacement DP; other IC DP Replace mappings remain gated | 950/951 workbench V2 route, static public deterministic hashes, and direct V2/legacy comparison are migration evidence, not an independent hardware golden |
| General Replace touching TP-classified ranges | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation required after the explicit mapping | Owner rule recorded 2026-07-03; profile compiler requires a later external processor operation for TP-classified explicit mappings. Workbench/UI and CLI now append the selected Combiner 1.13.0 postbuild plan for TP/CtrlRAM mappings when the IC has a postbuild profile; NT51950 golden-backed self-replacement evidence locks command traceability. Golden expected outputs remain required before production enablement. | Workbench execution wired; golden parity pending |

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
