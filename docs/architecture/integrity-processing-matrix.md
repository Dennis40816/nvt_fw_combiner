# Integrity and External Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

Owner update 2026-06-30:

- Replace is expected to require legacy `combiner.exe` CRC/header recalculation after the replacement mutations.
- `IC FlashMap` postbuild scripts now provide the first verified legacy Combiner 1.13.0 command sequences for CtrlRAM Replace. Postbuild remains the behavioral truth; mmap and TP Overview explain/document the ranges.
- Do not declare production Replace parity until each enabled profile has command shape, tool version, parameters, read/write ranges, execution order, and golden evidence.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51919 | fact-scoped NT51929 alias | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate resolves the explicit region-set alias, copies full DP, and applies the same three checked TPB scalar relocations as its source fact | Complete alias-plan parity to the tracked NT51929 fixture; no direct NT51919 product golden, and firmware-owner approval of the alias scope remains pending |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Direct tracked fixture full-byte V2/reference parity: `c7e1e263...3d66abe2`; runtime exposure and firmware-owner promotion pending |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Owner-approved fact-scoped NT51929 golden applicability plus direct named Python-configuration synthetic parity: `cd54e124...7de10ce`; firmware-owner promotion review remains pending |
| NT51950 | uploaded AB combiner | Verify existing CRC | DIFF relocation, then Combiner recalculates and writes CRC | V2 copies full DP, builds immutable A/B banks, overlays TPA/TPB, and relocates only TPB DIFF by `+0x40000`. Combiner 1.13.0 `NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x40000` writes the remaining B-header ILM/DLM fields and CRC. Two owner-approved fixtures are full-byte equal to the uploaded Python reference and Combiner output. | Executable candidate only; no UI/CLI route and firmware-owner promotion pending |
| NT51951 | uploaded AB combiner config | Verify existing CRC | DIFF relocation, then Combiner recalculates and writes CRC | A one-mebibyte V2 candidate copies full DP, stages two `0x80000` A/B artifacts, overlays TPA/TPB, and relocates only TPB DIFF by `+0x80000`. Its declared Combiner stage is `NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x80000`; the Windows-only synthetic regression fixes the Python-reference SHA-256 `e1524ba5...2e628c71` for that command after pre-tool DIFF relocation. The owner approved NT51950 evidence for this workflow-logic scope; it is not a direct NT51951 product golden. | Compilable candidate only; no UI/CLI route and firmware-owner review remains required before promotion |
| Other Standard-reference ICs | `gen_flash_bin_v2` | Unknown | Unknown/not applicable | no integrity rule established by current evidence | Must inventory |

## 2026-07-14 NT51950 AB private Combiner audit

The owner-approved tracked NT51950 AB fixtures contain BOE and Hiway cases.
Their reference output SHA-256 values are respectively
`D18DB8DC02AB4FF52CB17B4B3B3B90F99047C9D1ACD2A5C23627197CF32F8650` and
`4A292CD9615C58079B8994AF8060AF92562EAA92A55BC24BACC5EC5234E23B30`.
The source-verified command is `NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin
output.bin 0x40000`; it does not consume `map.txt`. `CRC8` is a legacy command
selector, not a claim that the AB header result is an eight-bit CRC. The V2 plan
uses a checked little-endian `u32` scalar transform for TPB DIFF
`[0x4A120,0x4A124)` before the tool runs. Combiner then changes only bytes within
the complete ILM `[0x4A100,0x4A104)`, DLM `[0x4A110,0x4A114)`, and header CRC
`[0x4A130,0x4A134)` fields. C# never calculates or writes the AB header CRC.

The committed Combiner 1.13.0
(`ED6B58289CC780F73D36B831F5424CEF44AD93187BA7518D36DF6A77AD0C76BF`) now
reproduces both owner-approved NT51950 outputs byte-for-byte after the V2 DIFF
relocation. The Windows-only `Nt51951CombinerTopologyMatchesPythonReferenceVectorAsync`
regression fixes the exact SHA-256 of a deterministic 1 MiB Python NT51951 input
vector and verifies that the same Combiner command with `0x80000` reproduces it
after DIFF relocation. This establishes command capability only; it does not
establish NT51951 product golden parity or support.

## Replace processing evidence

| Flow | Stage/purpose | Processor expectation | Current processor facts | Status |
| --- | --- | --- | --- | --- |
| CtrlRAM Replace priority flows | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation expected | Combiner 1.13.0 postbuild catalog implemented for NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928 non-NB, NT51929, NT51930, NT51931, NT51932, NT51950, and NT51951; NT51926 and NT51930 select postbuild category from base FWConfig Common FW version. A Build-only TP FW version edit patches the approved Combiner source, then requires the canonical NVT Backup output fields to match before commit. Command sequence tests exist; 2026-07-05 private NT51926/NT51927 self-replacement fixtures execute through workbench, but real expected golden replace outputs are still needed | Core command module implemented; golden parity pending |
| DP Replace priority flows | no CRC/header stage unless DP evidence says otherwise | restore only the profile-declared TP range when DP container includes TP | 950/951 DP Replace clones an exact base image whose length must be `0x40000`, `0x80000`, or `0x100000`, replaces the full padded selected-length DP container, restores TP `0x0A000-0x36FFF (len 0x2D000)`, and keeps customer info `0x37000-0x37FFF (len 0x1000)` from replacement DP; other IC DP Replace mappings remain gated | 950/951 workbench V2 route, static public deterministic hashes, and archived owner-approved legacy comparison are migration evidence, not an independent hardware golden |
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
