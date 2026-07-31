# Integrity and External Processing Matrix

This matrix records current evidence. It is not a blanket support claim. `Unknown` must never be interpreted as `None`.

`0.10.x` target amendment (2026-07-27):

- ADR 0042/#221 retire NT51920, NT51925, NT51930, and NT51931. Their rows and
  ranges below remain legacy `0.9.x` evidence only and cannot become target
  selector, processor, or Support Matrix authority.
- For #219/#188 `PreserveActiveDiffNf` routes, composition scatters only the
  declared `N - 1` active DLM prefixes from the selected DiffDLM payload. The
  AE suffix after the active prefix does not enter the read set or write set.
  Every active Diff NF tail and every inactive target record remains
  byte-identical to the immutable reference before CRC/Postbuild runs.
  NT51923/NT51926 and the NT51927 TP family retain full-artifact DiffDLM
  replacement.

Owner update 2026-06-30:

- Replace is expected to require legacy `combiner.exe` CRC/header recalculation after the replacement mutations.
- `IC FlashMap` postbuild scripts now provide the first verified legacy Combiner 1.13.0 command sequences for CtrlRAM Replace. Postbuild remains the behavioral truth; mmap and TP Overview explain/document the ranges.
- Do not declare production Replace parity until each enabled profile has command shape, tool version, parameters, read/write ranges, execution order, and golden evidence.

Owner update 2026-07-24:

- Normal-mode CtrlRAM postbuild may rewrite only the modeled ILM0, DLM0,
  FW Config, CtrlRAM, MP CtrlRAM, and Header CRC words at
  `[0x18,0x1C)`, `[0x1C,0x20)`, `[0x3C,0x40)`, `[0x4C,0x50)`,
  `[0x5C,0x60)`, and `[0xFC,0x100)`.
- Cascade-only DLM CRC authority is bounded by the approved header layout:
  NT51919/29/32 (2–8 IC) `[0x7128,0x7144)`, NT51930 (2–13 IC)
  `[0x7128,0x7158)`, NT51931 `[0x006C,0x00B8)`, and NT51950/51
  `[0xA134,0xA180)`. Single-chip plans do not receive these ranges.
- NT51950/51 AB remains unchanged: the full A/B image is the Combiner
  staging/read scope, while processor write authority remains only TPB
  `[0x4A100,0x4A104)`, `[0x4A110,0x4A114)`, and `[0x4A130,0x4A134)`.
  The UI now states this distinction instead of presenting the whole scope as
  if it were a postbuild calculation range.

| IC | Mode/evidence | TPA policy | TPB policy | Current processor facts | Status |
| --- | --- | --- | --- | --- | --- |
| NT51919 | fact-scoped NT51929 alias | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate resolves the explicit region-set alias, copies full DP, and applies the same three checked TPB scalar relocations as its source fact | Owner-approved fact-scoped alias with complete plan parity to the tracked NT51929 fixture; no direct NT51919 product golden, and runtime promotion review remains pending |
| NT51929 | uploaded AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Direct tracked fixture full-byte V2/reference parity: `c7e1e263...3d66abe2`; runtime exposure and firmware-owner promotion pending |
| NT51932 | uploaded/reference AB combiner | None | Address relocation only; no CRC configured | fixed-`0x80000` V2 candidate relocates little-endian `u32` offsets `0x7164/0x7168/0x716C` by `+0x40000` in a cloned TPB buffer | Owner-approved fact-scoped NT51929 golden applicability plus direct named Python-configuration synthetic parity: `cd54e124...7de10ce`; firmware-owner promotion review remains pending |
| NT51950 | uploaded AB combiner | Verify existing CRC | DIFF relocation, then Combiner recalculates and writes CRC | V2 copies full DP, projects both TP inputs from the same native section, overlays TPA/TPB, and relocates only TPB DIFF by the resolved `+0x40000` instance delta. Combiner 1.13.0 `NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x40000` writes the remaining B-header ILM/DLM fields and CRC; the host imports only those three exact fields. Two owner-approved fixtures are full-byte equal to the uploaded Python reference and Combiner output. | Executable candidate only; firmware-owner promotion and direct cascade evidence pending |
| NT51951 | uploaded AB combiner config | Verify existing CRC | DIFF relocation, then Combiner recalculates and writes CRC | A one-mebibyte V2 candidate copies full DP, projects both TP inputs from the same native section, stages two `0x80000` A/B artifacts, and relocates only TPB DIFF by the resolved `+0x80000` instance delta. Its declared Combiner stage is `NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x80000`; the host imports only the exact B ILM/DLM/CRC fields. The Windows-only synthetic regression fixes the Python-reference SHA-256 `e1524ba5...2e628c71` for that command after pre-tool DIFF relocation. The owner approved NT51950 evidence for this workflow-logic scope; it is not a direct NT51951 product golden. | Compilable candidate only; direct product evidence and firmware-owner review remain required before promotion |
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
| CtrlRAM Replace priority flows | post-replace header/integrity stage | legacy `combiner.exe` CRC/header recalculation expected | Pre-#221 `0.9.x` compatibility evidence implemented Combiner 1.13.0 postbuild for all 13 then-selectable ICs and covered 31 cataloged interval/plan pairs with trusted V2 routes. This is historical characterization only: the `0.10.x` target retires NT51920/25/30/31 and cannot use those rows as selector, processor, publication, or admission authority. In the admitted inventory, NT51927 and NT51928 non-NB each expose single/2-chip/3-chip; NT51950/51 each expose single/cascade with identical TP authority but distinct image capacity. NT51928 preserves its DP/LDC tail and independently routes DP plus LDC; NT51950/51 package LDC inside DP. Exact PID/version/SHA/count facts remain regression evidence. Existing direct cases retain V1/V2, command, range, report, and immutable-input evidence; Build-only NT51926 TP-version edit still validates the final Backup before commit. | Route closure is support-neutral; direct output evidence and firmware-owner promotion remain per-plan gates |
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
