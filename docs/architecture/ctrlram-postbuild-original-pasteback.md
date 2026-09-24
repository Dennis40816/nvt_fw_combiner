# CtrlRAM Postbuild Original Pasteback Investigation

Status: investigation note, not a production behavior decision.
Date: 2026-07-03.

Latest searchable conclusion reference: [`ctrlram-postbuild-investigation-reference.md`](ctrlram-postbuild-investigation-reference.md).

## Scope

This note records the current understanding of CtrlRAM original-information pasteback through the legacy postbuild combiner.
It covers:

- postbuild combiner command shapes;
- `Copy Header` behavior;
- TP flash header workbook implications;
- `map.txt` staging behavior;
- self-pasteback verification results when CtrlRAM regions are cut from a golden output and pasted back through `Combiner.exe`.

Affected layers:

- `profiles/built-in/ctrlram-postbuild-v2/catalog.json`: hash-pinned structured command data.
- `NvtFwCombiner.Infrastructure`: strict command-data loading, staged real-tool execution, output-length normalization, and byte diff validation.
- Golden evidence: owner-approved direct Standard Merge expected outputs indexed by `testdata/golden/canonical/manifest.json`.

Affected workflow:

- Composition kind: CtrlRAM Replace postbuild investigation.
- Experience: CtrlRAM Replace.
- Address space: staged firmware image passed to `Combiner.exe`.
- Tool binding: committed `external-tools/legacy-combiner/1.13.0/Combiner.exe`.

Invariants:

- The external combiner may mutate only a host-created staging copy.
- Host-side diff remains authoritative.
- Any production change to command order, CRC/header offsets, copy-header ranges, or allowed write ranges remains R3 and requires firmware-owner review plus golden evidence.
- This note does not claim end-to-end CtrlRAM Replace production parity.

## Evidence Inputs

Primary evidence:

- `docs/references/tddi-flash-header/Combiner.c`, version string `1.6.0.1`, used as source-level evidence for legacy normal-mode behavior.
- `docs/references/tddi-flash-header/TDDI_Flash_Header.xlsx`, used as TP header descriptor layout evidence.
- `external-tools/legacy-combiner/1.13.0/Combiner.exe`, used for real-tool experiments.
- Direct `standard-merge` expected artifacts under `testdata/golden/canonical/`, used only as existing owner-approved firmware outputs for self-pasteback experiments.

Important limitation:

- The inspected `Combiner.c` source is not the same version as the committed 1.13.0 executable.
- Normal-mode behavior aligns closely enough to explain NT51920/NT51923/NT51926 16-byte CRC effects.
- NT-based modes and common-header modes must be treated as 1.13.0 executable behavior until owner provides matching source or golden Replace vectors.

## Command Families

The postbuild catalog currently uses these combiner command families:

| Family | Argv head | Examples | Notes |
| --- | --- | --- | --- |
| Legacy normal | `CRC_Enable <fw>` | NT51920, NT51923, NT51926 | Source evidence exists in `Combiner.c`; requires `map.txt` for real-tool runs. |
| NT-based normal | `<NTxxxxxBASED_NORMAL_MODE> CRC8 <fw> <fw>` | NT51930, NT51931, NT51932, NT51950, NT51951 | 1.13.0-only behavior for current repo evidence; requires `map.txt` for merge/postbuild commands. |
| Merge mode | `MERGE_MODE <fw>` | NT51927 family | May shorten command output; host overlays shortened output onto the previous full-length staged image when declared coverage is complete. |
| CRC-only | `NT51927BASED_GEN_CRC_MODE CRC32 <fw> <fw>` | NT51927 family | Does not need `map.txt` in the observed smoke tests. |

## Copy Header Catalog Summary

Current single-branch copy/header-refresh data:

| IC | Command family | Copy/header source | Copy/header target | Length |
| --- | --- | ---: | ---: | ---: |
| NT51920 | legacy normal | `0x0` | `0x26680` | `0x100` |
| NT51923 | legacy normal | `0x0` | `0x30310` | `0x100` |
| NT51926 1.4.1 | legacy normal | `0x0` | `0x32F50` | `0x100` |
| NT51926 2.0.0 | legacy normal | `0x0` | `0x32A70` | `0x100` |
| NT51927 | merge/crc-only | `0x200` and `0x0` | `0x1E230` and `0x32DC0` | `0x190` and `0x460` |
| NT51928 | merge/crc-only | follows NT51927 | follows NT51927 | follows NT51927 |
| NT51930 1.4.0 | NT-based normal | `0x7000` | `0x28FB0` | `0x100` |
| NT51930 2.0.0 | NT-based normal | `0x7000` | `0x28FB0` | `0x200` |
| NT51931 | NT-based normal | `0x0` | `0x1DA30` | `0x100` |
| NT51932 | NT-based normal | `0x7000` | `0x27EF0` | `0x200` |
| NT51950 | NT-based normal | `0xA000` | `0x2D30C` | `0x200` |
| NT51951 | NT-based normal | follows NT51950 | follows NT51950 | follows NT51950 |

NT51927-family 2-chip and 3-chip branches also refresh right/left headers and copy windows. They are not reducible to a single `Copy Header` location.

## TP Flash Header Findings

The TP flash header workbook is useful for descriptor interpretation, not sufficient for full postbuild command reconstruction.

Useful facts from the workbook and normal-mode source:

- The header is organized in 16-byte descriptors.
- A descriptor is four little-endian 32-bit words.
- The CRC field is the descriptor's last word, at descriptor offset `+0x0C`.
- For normal NT51920/NT51923/NT51926 style headers, `0x10 + 0x0C == 0x1C` is the DLM0 CRC word.
- The final header descriptor at `HeaderSize - 0x10` stores Header CRC at `HeaderSize - 0x04`.
- NT51923 and NT51926 normal headers support a `0x100` copy-header length.

The workbook is not enough to decide:

- which combiner command family to use;
- exact copy-header target addresses;
- single/cascade/numeric branch selection;
- CRC method and command order;
- NT-based 1.13.0 special behavior;
- whether a copy-header area must already contain a prior-generation header.

## Why 16 Bytes Can Differ

NT51923 self-pasteback demonstrates the core normal-mode issue.

For 51923 golden:

```text
main header  [0x00000, 0x00100)
copy header  [0x30310, 0x30410)
```

The main header and copy header differ only at two CRC words:

```text
main 0x001C:  EE55AF3A
copy 0x3032C: 5769952D

main 0x00FC:  20730989
copy 0x3040C: C4598BAB
```

The source-level write sites explain those words:

- `Cal_ILM0_DLM0_CRC()` reads DLM0 start from `0x0C`, size from `0x14`, and writes DLM0 CRC to `0x1C`.
- Header CRC writes to `HeaderSize - 0x04`, which is `0xFC` when `HeaderSize == 0x100`.
- `Copy Header` copies `0x0..0x100` to the IC-specific copy-header target before CRC recalculation.

The observed final state is therefore two generations of header:

```text
final copy header = pre-combiner main header
final main header = post-combiner recalculated main header
```

Re-running full postbuild on an already-final golden advances the generation and is not expected to be byte-identical.

## MAP.TXT Findings

Real-tool smoke results:

| Command type | `map.txt` missing | empty `map.txt` present | Current interpretation |
| --- | --- | --- | --- |
| NT51923 legacy normal VN-only | fails opening `map.txt` / `output\map.txt` | exits `0`, prints `This FW has no overlay.` | `map.txt` is launch-required even when no overlay data is used. |
| NT51930 NT-based VN-only | fails opening `map.txt` / `output\map.txt` | exits `0`, prints `This FW has no overlay.` | `map.txt` is launch-required for NT-based merge/postbuild commands too. |
| NT51927 CRC-only | exits `0` without map | exits `0` with map | CRC-only command does not require `map.txt` in the observed case. |

Implications:

- Production real-tool normal/NT-based postbuild must stage either `map.txt` or `output\map.txt`.
- An empty `map.txt` is sufficient for the observed no-overlay golden runs.
- Empty `map.txt` is not proven safe for overlay-enabled firmware. If overlay bits are set, real map content may be needed to derive overlay metadata.
- Current production behavior should not claim real-tool normal/NT-based parity until map staging policy is explicit and tested.

## Self-Pasteback Verification

Experiment setup:

- input: existing standard-merge golden expected output;
- BIN files: cut from the same input image according to current postbuild catalog ranges;
- run directory: temporary staging directory;
- `map.txt`: empty file present for normal and NT-based merge commands;
- result model: host-like normalization for command-shortened `MERGE_MODE` outputs;
- branch: single branch only;
- objective: measure whether original CtrlRAM information pasted back from itself changes bytes.

### VN-only

VN-only means the combiner command includes only the VN block needed for self-pasteback, plus any command-family-required base copy for the NT51927 merge family. It intentionally does not run the full postbuild sequence.

| IC | Result | Interpretation |
| --- | --- | --- |
| NT51920 | `0` diff bytes | Expected no-op. |
| NT51923 | `0` diff bytes | Expected no-op. |
| NT51926 | `0` diff bytes | Expected no-op. |
| NT51927 | `0` diff bytes after host-like normalization | Expected no-op with merge normalization. |
| NT51928 | `0` diff bytes after host-like normalization | Expected no-op with merge normalization. |
| NT51929 | `0` diff bytes | Expected no-op. |
| NT51930 | `2853` diff bytes | Unexpected for a VN-only no-op; 1.13.0 NT51930 mode mutates additional data near `0x32000`. |
| NT51931 | historical 1.13.0/51930-based pairing crash, exit `0xC0000005` | Later resolved by selecting 1.13.0/51931-based after full-byte control parity. |
| NT51932 | `0` diff bytes | Expected no-op. |
| NT51950 | `0` diff bytes | Expected no-op. |
| NT51951 | `0` diff bytes | Expected no-op. |

Unexpected VN-only call examples:

```text
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 .\NT51930_fw.bin .\NT51930_fw.bin .\BIN\VN_Ctrlram.bin 0x0 0x27650 6496
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 .\nt51931_fw.bin .\nt51931_fw.bin .\BIN\VN_Ctrlram.bin 0x0 0x1C3D0 5728
```

### Full Single Postbuild

Full single means current catalog single-branch command sequence is run on a final golden image with original CtrlRAM BINs cut from that same image.

| IC | Diff bytes | Classification | Diff ranges summary |
| --- | ---: | --- | --- |
| NT51920 | `16` | Expected CRC-generation drift | `0x1C..0x20`, `0xFC..0x100`, `0x2669C..0x266A0`, `0x2677C..0x26780` |
| NT51923 | `16` | Expected CRC-generation drift | `0x1C..0x20`, `0xFC..0x100`, `0x3032C..0x30330`, `0x3040C..0x30410` |
| NT51926 | `251` | Postbuild-version mismatch candidate | `2.0.0` run changes `0x1C..0x20`, `0xFC..0x100`, plus most of `0x32A70..0x32B70`; the supplied base matches the `1.4.1` target near `0x32F50` except CRC words |
| NT51927 | `24` | Expected CRC/header-word drift, not exactly 16 | six 4-byte CRC/header words near `0x1E26C`, `0x1E27C`, and `0x32FDC..0x33010` |
| NT51928 | `64` | Expected CRC/header-word drift, not exactly 16 | multiple 4-byte CRC/header words near `0x23C`, `0x24C`, `0x1E24C..0x1E2B0`, and `0x32FDC..0x33040` |
| NT51929 | `16` | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51930 | `2901` | Postbuild-version mismatch candidate | `2.0.0` run changes `0x7100`, `0x7118`, copy/header area near `0x28FB0`, and extensive `0x32000`; current golden aligns with `1.4.0` header-copy length `0x100` |
| NT51931 | historical 1.13.0/51930-based pairing crash, exit `0xC0000005` | Resolved pairing is 1.13.0/51931-based | no output diff was available for the rejected pairing |
| NT51932 | `16` | Expected CRC-generation drift | `0x7100..0x7104`, `0x7118..0x711C`, `0x27FF0..0x27FF4`, `0x28008..0x2800C` |
| NT51950 | `16` | Expected CRC-generation drift | `0xA11C..0xA120`, `0xA130..0xA134`, `0x2D428..0x2D42C`, `0x2D43C..0x2D440` |
| NT51951 | `16` | Expected CRC-generation drift | same ranges as NT51950 |

NT51926 full-single calls currently implemented from `2.0.0`:

```text
Combiner.exe CRC_Enable .\nt51926_fw.bin .\BIN\Normal_Ctrlram.bin 0x0 0x22800 11264 .\BIN\MP_Ctrlram.bin 0x0 0x25400 9216 .\BIN\VN_Ctrlram.bin 0x0 0x315D0 5278 .\BIN\NF_Ctrlram.bin 0x0 0x2C800 11728 .\nt51926_fw.bin 0x22000 0x3B000 1920 .\nt51926_fw.bin 0x0 0x32A70 256
Combiner.exe CRC_Enable .\nt51926_fw.bin .\nt51926_fw.bin 0x0 0x32A70 256
```

Owner-provided `PostbuildSetup_51926_1.4.1.bat` instead uses `0x32F50` for the same header-copy length and uses `VN_Ctrlram.bin` length `5728` plus FWConfig length `2048`. The 2026-07-05 NT51926 base has its initialized header-copy area at `0x32F50`, not `0x32A70`.

The NT51926 Common FW `1.4.1` cascade reference maps to this canonical host-staging argv. Path tokens are staging-relative; the runtime report expands them beneath one host-created staging working directory:

```text
Combiner.exe CRC_Enable output/nt51926_fw.bin BIN/Normal_Ctrlram.bin 0x0 0x22800 11264 BIN/DiffDLM.bin 0x0 0x27800 10240 BIN/MP_Ctrlram.bin 0x0 0x25400 9216 BIN/VN_Ctrlram.bin 0x0 0x315D0 5728 BIN/NF_Ctrlram.bin 0x0 0x2C800 11728 output/nt51926_fw.bin 0x22000 0x3B000 2048 output/nt51926_fw.bin 0x0 0x32F50 256
Combiner.exe CRC_Enable output/nt51926_fw.bin output/nt51926_fw.bin 0x0 0x32F50 256
```

NT51930 full-single calls currently implemented from `2.0.0`:

```text
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 .\NT51930_fw.bin .\NT51930_fw.bin .\BIN\Normal_Ctrlram.bin 0x0 0x21650 11264 .\BIN\VN_Ctrlram.bin 0x0 0x27650 6496 .\BIN\NF_Ctrlram.bin 0x0 0x1FC00 6736 .\NT51930_fw.bin 0x7000 0x28FB0 512
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 .\NT51930_fw.bin .\NT51930_fw.bin .\NT51930_fw.bin 0x7000 0x28FB0 512
```

Owner-provided `PostbuildSetup_51930_1.4.0.bat` instead uses one command with `0x7000 -> 0x28FB0`, length `0x100`, and no second header-only command. The current 51930 golden has only one differing byte for the first `0x100` of that copy target but 40 differing bytes if interpreted as the `2.0.0` `0x200` copy target.

NT51931 failing full-single call:

```text
Combiner.exe NT51930BASED_NORMAL_MODE CRC8 .\nt51931_fw.bin .\nt51931_fw.bin .\BIN\NF_Ctrlram.bin 0x0 0x16800 4048 .\BIN\Normal_Ctrlram.bin 0x0 0x177D0 10240 .\BIN\MP_Ctrlram.bin 0x0 0x19FD0 9216 .\BIN\VN_Ctrlram.bin 0x0 0x1C3D0 5728 .\nt51931_fw.bin 0x0 0x1DA30 256 .\nt51931_fw.bin 0x16000 0x3B000 2048
```

## Unified Countermeasure Evaluation

### A. Use VN-only commands for original-info self-pasteback verification

Status: recommended for no-op diagnostics only.

Rationale:

- It verifies that the staged VN file, source offset, destination range, and length are correct.
- It avoids known non-idempotent copy-header behavior.
- It produced `0` diff bytes for all observed ICs except NT51930 and NT51931.

Limit:

- It is not a complete Replace postbuild parity test.
- Real replacement still needs CRC/header recomputation through the full IC-specific postbuild flow.

### B. Accept known CRC-only drift in full postbuild no-op tests

Status: acceptable only with per-IC declared ranges.

Rationale:

- NT51920, NT51923, NT51929, NT51932, NT51950, and NT51951 show a consistent 16-byte pattern: four 4-byte CRC words.
- NT51927 and NT51928 show only 4-byte CRC/header-word changes, but more than 16 bytes because their flow updates multiple headers.

Limit:

- NT51926 and NT51930 are not CRC-only under the current standard-golden self-pasteback setup.
- A fixed "allow 16 bytes" rule would be wrong.

### C. Clear or restore copy-header area to `0xFF`

Status: not useful as a production default. Potentially useful only as a diagnostic experiment.

Verified fact:

- For NT51923, pre-filling `[0x30310, 0x30410)` with `0xFF` before running `Copy Header` produces the same result as re-running final golden unchanged.
- The copy-header command overwrites the area before CRC calculation, so the initial value is irrelevant when the copy command runs.
- This does not solve cross-version evidence mismatch. If the selected postbuild version writes a different target or length, pre-filling with `0xFF` may hide the mismatch rather than prove parity.

Limit:

- This does not make final golden full-postbuild idempotent.
- It may hide whether the starting image already has a meaningful prior-generation copy header.
- If used as a mode, it must be explicit and diagnostic-only unless firmware-owner evidence approves it as a real production transform. The prefill range must be declared as processor-owned mutable work-buffer bytes and covered by before/after range checks.

### D. Clear specific CRC words before full postbuild

Status: possible as a diagnostic preimage reconstruction, not a production shortcut.

Verified fact:

- For NT51923, setting the main header CRC words to the values currently stored in golden's copy header, then running copy-header + CRC, reproduces golden exactly.

Risk:

- This requires per-IC and per-header knowledge of which CRC words are prior-generation values.
- It changes the staged input state before calling the tool and could mask real combiner behavior.
- TP header workbook helps identify descriptor words, but it is not enough to know all 1.13.0 header generations.

### E. Restore original copy-header bytes after postbuild

Status: not recommended without explicit owner evidence.

Rationale:

- It can force no-op parity, but it also undoes combiner writes inside declared copy-header ranges.
- For normal-mode ICs, copy-header target may be inside DLM CRC coverage. Restoring bytes after CRC recomputation can make header CRC and DLM CRC inconsistent.

### F. One unified postbuild command for all ICs

Status: rejected.

Reasons:

- NT51920/NT51923/NT51926 use legacy normal mode.
- NT51930/NT51931/NT51932/NT51950/NT51951 use NT-based modes.
- NT51927/NT51928 use merge + CRC-only sequences and require output-length normalization.
- NT51931's historical 1.13.0/51930-based pairing crashes; the selected 1.13.0/51931-based pairing exits 0 and matches the 1.2.0.4/51930-based control byte-for-byte on the final staged case.

## Current Recommendations

1. Keep "original-info self-pasteback" and "real Replace postbuild" as separate validations.
2. Use VN-only self-pasteback to verify per-region source/destination/length.
3. Use full postbuild only for Replace/golden parity, and classify differences by per-IC declared CRC/header ranges.
4. Stage an explicit `map.txt` for normal and NT-based merge/postbuild real-tool runs. Empty map is sufficient only for verified no-overlay cases.
5. Do not implement "clear copy header to FF" as a general production strategy. If implemented later, keep it as an explicit diagnostic mode with declared ranges and golden evidence.
6. Do not implement "restore original copy header" as a general strategy.
7. Do not use TP flash header workbook alone to patch bytes. Combine workbook facts with command catalog, real-tool behavior, and owner-approved golden diff evidence.
8. Treat NT51930 and NT51931 as current investigation blockers for a unified CtrlRAM self-pasteback story.

## Open Questions

- Should NT51926 CtrlRAM Replace select `1.4.1`, `2.0.0`, or a version-detected postbuild profile? The current 2026-07-05 base aligns with the `1.4.1` header-copy target.
- Should NT51930 CtrlRAM Replace select `1.4.0`, `2.0.0`, or a version-detected postbuild profile? The current 51930 golden aligns with the `1.4.0` header-copy length.
- What exact `map.txt` content is required for overlay-enabled firmware?
- Resolved 2026-07-19: NT51931 selects registered 1.13.0 `NT51931BASED_NORMAL_MODE CRC8`; the 1.13.0/51930-based combination is rejected.
- Should the real-tool host adapter always stage an empty `map.txt` for no-overlay normal/NT-based commands, or should profiles declare map authority explicitly?
- Which per-IC CRC/header diff ranges should be accepted for full postbuild no-op diagnostics, if any?

## NT51929 AB address strategy characterization — 2026-09-21

Status: investigation only; neither strategy is admitted for production AB Replace.
The owner-approved conditional invariant is recorded in
[ADR 0024](../adr/0024-ctrlram-tp-and-flash-base-convergence.md#ab-ctrlram-conditional-invariant--2026-09-21).

The new Infrastructure test
`Nt51929AbSameContentControlsPreserveRealToolStrategyEvidence` uses the existing
`LegacyCombinerPostbuildProcessor`, staged replacement sources, profile command
plan and host write audit. It does not modify the catalog or add a byte-execution
path. Windows is required; other platforms explicitly skip the real-tool test.

### Controlled input and evidence

- Source: canonical `ab-merge / NT51929 / expected-output / t05-d06`, complete
  SHA-256 `c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2`.
- A control: `[0,0x40000)` of that source; SHA-256
  `e257e734a63d0d8a0e471bc7b541366578b9b56c94dd914197508d5af1127c12`.
- Synthetic B: a separate copy of that same A slice, adding `0x40000` only to
  its own U32 LE stored addresses at `0x7164`, `0x7168`, `0x716C` using checked
  arithmetic. The original Golden B half is not this synthetic control.
- Tool: existing Combiner 1.13.0, SHA-256
  `ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf`;
  existing two-command `NT51932BASED_NORMAL_MODE CRC8` single-IC plan.
- Normal control replacement: typed flash-map region `normal`, whose range
  contains `0x21B90`; toggle the first source byte with `0x5A`, supplied through
  `ExternalProcessorStagedSource`. Assert that it reaches the resulting output.
- Compare complete outputs. Differences are classified only against the three
  address words, declared CRC words `[0x7100,0x7104)` / `[0x7118,0x711C)`, and
  their exact counterparts in the `[0x27EF0,0x280F0)` header copy. The whole copy
  range is not a comparison allowance. Write authority and CRC read coverage
  remain distinct facts.

External private artifacts are under
`D:\NvtFwCombiner-TestArea\evidence\v1110-nt51929-ab-ctrlram-characterization`.
The final Normal run is `272de94807f8405d8a440d9712d6b716`; its `report.json`
SHA-256 is `389cbb65b2f3d67b32a1e1926be5f30a927a2a9c83f5ffedc8acf5c240315348`.
Each command retains full pre/post snapshots, exact argv, exit status, stdout,
stderr, hashes, field values and complete changed ranges. Restored B is saved
separately. These BIN files are private experiment artifacts, not Git fixtures.

### Observations and limitations

| Strategy | Observed result | What it establishes |
| --- | --- | --- |
| A, real Normal replacement | Success; nonzero replacement reaches output | Valid control through existing host audit |
| Local B with final B stored addresses | First command exits `-1073741819` (`0xC0000005`); no successful processor output | This local-B invocation cannot be used unchanged |
| B addresses normalized, same replacement | Success; complete output equals A control | Same-input tool consistency only |
| Add B delta to the three main-header fields after postbuild | Exactly three changed bytes versus A: `0x7166`, `0x716A`, `0x716E` | Address restoration changes no other bytes; does not prove CRC correctness |

A and normalized output SHA-256:
`12d50be389c038644d0e1785e99c025d831359e388d6b48e8dd2dc78bf853d45`.
Restored B SHA-256:
`2900cb52f39a2e72c0c7be214a0041855e196f51d7431218f01d8a1422d6312a`.
Restoration leaves CRCs equal to A and leaves the three copied addresses at
`0x28054/0x28058/0x2805C` in A coordinates. No copied fields or CRCs were repaired
to force agreement. An empty outside-field diff does **not** prove the CRC
valid or invalid: the exact CRC read ranges and required copied-header generation
are still unproven. The test does not cover differing A/B original contents,
versions, unselected-bank preservation, cascade, NT51950 or NT51951.

The earlier NF control is preserved separately as
`2ed41f9f7e2c4266ba920ec9c4409640`. Toggling the NF byte at `0x1FC00` also changed
`0x2EA00`; both A and normalized B were correctly rejected by host write audit.
The current fw200 profile's Backup authority `[0x2E000,0x2E07C)` does not cover
that byte. Its meaning and required authority are unresolved; do not expand
the allowed range or call the Normal control a fix for this NF failure.

The final Windows filter `FullyQualifiedName~Nt51929Ab` passes **2/2**, zero
skipped, including the existing first-half evidence test. This is successful
characterization, not successful B runtime support or new Golden certification.
At that checkpoint, source lookup had missed the ignored owner archive. The
source findings below supersede the missing-source premise. Runtime integration,
independent complete-output checks and exact NF propagation authority remain
separate work.

### Recovered existing 1.13 source — 2026-09-21

The owner pointed out that this checkout already contains the 1.13 source.
It is present under Git-ignored `.tmp/combiner-1.13-source/`, with the matching
owner archive also in `.tmp/owner-archive/929-golden-combiner-intake/`.
The previously inspected tracked `tddi-flash-header/Combiner.c` is a different,
older source and was not a sufficient search of the available evidence.

Provenance is already recorded in the
[owner intake](../../testdata/golden/owner-handoff/combiner-and-51929/CASE.md).
The nested `Combiner_1.13_SourceCode.7z` SHA-256 is
`0c86ad1d292db279c613b0f23a2e4cef8c2422950c56d1c22fdd1130660b47b8`.
The entry `firmware-merge-tool/Combiner/Combiner.c` was read directly from that
archive and compared byte-for-byte with the existing extracted file; both hash
to `7fb6551894d5a71f7df42b6b7c2bda99f35cbcc13f5c01713dc0ae596ebb5ea8`.
The archived `.vcxproj` compiles `Combiner.c`; its `main` at line 2142 prints
`Combiner version:1.13.0.0`. This establishes source provenance, not a claim of
reproducible binary-build equivalence. No archive payload was added to Git.

Source observations (line numbers refer to that exact C entry):

| Path | Source evidence | Consequence for the experiment |
| --- | --- | --- |
| NT51929 uses the catalog's NT51932-based mode | `NT51932_CalculateDlmDiffCrcAndHeaderCrc`, lines 1469–1473, reads header start `0x7104` with size code `0x23`; `CRC8Alg` line 42 includes the end byte | Header CRC covers `[0x7104,0x7128)`, excluding address fields `0x7164/0x7168/0x716C` |
| NT51932 AB assembly | `NT51932BasedMergeABMode`, lines 1658–1673, adds the B delta to the three main-header addresses and writes the output; it calls no CRC or header-copy routine | Normal postbuild followed by restoring only those addresses agrees with this source's assembly sequence; equal A/B CRCs and local copied addresses are not themselves a defect |
| NT51950 AB assembly | `NT51950BasedMergeABMode`, lines 1930–1934, relocates ILM/DLM addresses then calls `NT51950_CalculateHeaderCrc` | Its header range is `[0xA100,0xA130)` (lines 1721–1727), including its address fields, so the 929 no-extra-CRC conclusion must not be generalized to 950 |
| Local-B failure | `NT51932_MergeBinsThenInsertFwConfigAndEndFlag`, lines 1495–1520, allocates the local image and computes Backup destination from the stored DIFF address without a destination bound check | The observed 1-IC B control has length `0x40000`, DIFF address `0x6D100` and computed Backup destination `0x6E000`; the 4096-byte copy is outside its buffer even before CRC calculation. This is a concrete unsafe access explaining the crash, not a captured exception stack |
| NF propagation | The same routine copies 4096 bytes from FWConfig source, then writes the final NVT marker | For the local control, `[0x1F200,0x20200)` is copied to `[0x2E000,0x2F000)`. NF byte `0x1FC00` is therefore copied to `0x2EA00`, exactly matching the retained host refusal |

The source resolves the missing address/CRC-coverage explanation for these
functions. It does not by itself widen the profile's 124-byte FWConfig Backup
write authority, admit an AB Replace route, prove unselected-bank preservation,
or certify a new Golden. Follow-up implementation can use this existing source
and the existing AB family owner; a second request for the same source is no
longer necessary. No new product tests were run for this source/document correction.

Subsequent local implementation on 2026-09-21 corrects the existing Single
family Backup declaration to `[0x2E000,0x2F000)` while preserving the 124-byte
metadata schema and adjacent-byte protection. Real NF replacement, the complete
Backup copy/marker, output difference bounds, immutable inputs, original Golden
and alias execution, and affected route/compiler regressions pass in the
118-case targeted run. See the
[delivery evidence](../ui/v1.1.10-delivery.md#nt51929nt51919-single-backup-修正--2026-09-21).
This fixes the NF write-authority mismatch; it does not implement AB Replace or
turn the prior characterization into an independent AB output Golden.
