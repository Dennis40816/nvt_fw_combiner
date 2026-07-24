# TDDI Flash Header Evidence - 2026-07-03

## Scope

This note records owner-provided `combiner_info.7z` evidence recovered from the HackMD CJK14 transfer on 2026-07-03. It is reference evidence only. It does not promote new production firmware behavior, golden parity, or a new committed external-tool binding.

Affected firmware area:

- Composition kind: Replace post-processing evidence.
- Experience: CtrlRAM Replace / TP-affecting Replace paths.
- IC focus: NT51923 and NT51926 normal-mode postbuild comparison.
- Address space: staged firmware/output image processed by the legacy combiner.
- Invariants: the external combiner may mutate only a host-created staging copy; host-side byte diff remains authoritative; all production ranges remain half-open and profile-declared.

The executable report mapping derived from this evidence is maintained in the [TP Header Semantic Catalog](../architecture/tp-binary-model-catalog.md). That catalog does not change Combiner behavior or promote new firmware support.

## Archive Provenance

Recovered archive:

| Field | Value |
| --- | --- |
| Filename | `combiner_info.7z` |
| Recovered bytes | `213883` |
| SHA-256 | `e3c036b5735d6356205140567af8315e5d3df0ac40764ee366d15e8ca7429aed` |
| CJK14 file id | `daa3fa85466b43ffa6a4422f487312c8` |
| CJK14 parts | `2` |

The archive itself is not committed because it contains a real firmware BIN fixture. Per owner request, the C source and workbook are committed as reference artifacts under `docs/references/tddi-flash-header/`; executable, map, and BIN payloads remain uncommitted.

Extracted archive inventory:

| Relative path | Bytes | SHA-256 | Commit policy |
| --- | ---: | --- | --- |
| `togo/TDDI_Flash_Header .xlsx` | `316493` | `930fb3e9a3cd652ab493817fc02006bcf4046a5b648b2fd32494c42ff4174fa4` | committed as `docs/references/tddi-flash-header/TDDI_Flash_Header.xlsx` |
| `togo/Combiner_20250418_0951_ReleaseToNK_71fe7ddd1381238f29d0cea0ae21a6c303d09b25_/Combiner` | `30368` | `9ef149beff10f510e0b15a52abf76258daaf52c91c9d0fc0adf3e3ddef22e983` | not committed |
| `togo/Combiner_20250418_0951_ReleaseToNK_71fe7ddd1381238f29d0cea0ae21a6c303d09b25_/Combiner.c` | `34471` | `5ce2048b9a2e07e970119733c62939f6007ccf061dae6b350194e629171f386c` | committed as `docs/references/tddi-flash-header/Combiner.c` |
| `togo/Combiner_20250418_0951_ReleaseToNK_71fe7ddd1381238f29d0cea0ae21a6c303d09b25_/Test/map.txt` | `648415` | `4821125eb2cf601c06dcbcb5d95acc248cfbd17805039f33d5838f246456e204` | summarized only |
| `togo/Combiner_20250418_0951_ReleaseToNK_71fe7ddd1381238f29d0cea0ae21a6c303d09b25_/Test/NT51925_154_D05_T06_20241024.bin` | `262144` | `19fbb0533bbd588ba3f2528a0bc46711f5c23c75852bdac06f38ef804b7f5be5` | not committed; real firmware BIN |

## Combiner.c Normal Mode Findings

The inspected `Combiner.c` source is generic normal-mode combiner evidence. It does not contain an NT51923/NT51926-specific branch.

Relevant facts:

- Normal mode argv shape is `mode`, `FW_bin`, followed by repeated `block_bin`, `source_address`, `destination_address`, `length` groups.
- `CRC_Enable` selects `Crc8`; `CRC32_Enable` selects `Crc32`; any other normal-mode value disables CRC.
- The source writes the processed firmware back to `argv[2]`, so production use must keep the existing staged-copy model.
- `DecodeOneHeaderSize` derives `HeaderSize = len + 1` from the first descriptor whose `binAddr == 0` and `len != 0`.
- DLM CRC descriptors are walked in `0x10` increments. Each 16-byte descriptor is four little-endian 32-bit words:
  - word 0: destination/start-related field for the descriptor,
  - word 1: size,
  - word 2: BIN start address,
  - word 3: CRC output.
- For each DLM descriptor, the combiner writes the CRC to descriptor offset `+ 12`.
- Header CRC uses the final 16-byte descriptor at `HeaderSize - 0x10`. It reads header size at `HeaderSize - 0x0C`, header start at `HeaderSize - 0x08`, subtracts 4 from the size before calculation, and writes the header CRC at `HeaderSize - 0x04`.
- The `0x30` FWConfig descriptor therefore has its normal-mode CRC word at `0x3C..0x3F`; an NT51926 Combiner 1.13 staged-build smoke confirms that exact write. It remains an explicit `tp-flash-header-crc` processor range, not a general FWConfig write permission.

Interpretation:

- The evidence supports a 16-byte descriptor granularity.
- The CRC field itself is 4 bytes at the last word of each 16-byte descriptor.
- The final header descriptor reserves the last word for Header CRC; the CRC calculation excludes that CRC field.

## TP Header Workbook Findings

`TDDI_Flash_Header.xlsx` contains separate sheets for `920&923`, `925&926`, and `926NB`.

For `920&923` and `925&926`, the normal header layout is structurally aligned:

| Descriptor offset | `920&923` label | `925&926` label |
| --- | --- | --- |
| `0x00` | ILM start/dest/size and DATA start | ILM start/dest/size and DATA start |
| `0x10` | DATA dest/size, ILM0 CRC, DLM0 CRC | DATA dest/size, ILM0 CRC, DLM0 CRC |
| `0x20` | `Same_code`, `SPI_OPTION`, SVN, OV info | `Cas_info`, `SPI_OPTION`, `T6/T4`, SVN, OV info |
| `0x30` | FW_Config dest, size 0, FW_Config start, FW_Config CRC | same |
| `0x40` | CtrlRam dest, size 0, CtrlRam start, CtrlRam CRC | same |
| `0x50` | MPCtrlRam dest, size 0, MPCtrlRam start, MPCtrlRam CRC | same |
| `0x60`..`0xE0` | Reserved | Reserved |
| `0xF0` | Header dest, Header size, Header start, Header CRC (FW) | same |

`926NB` is different evidence and should not be silently treated as normal NT51926. It adds `TX__SSC` at the `0x20` descriptor and includes extra cascade/TX SSC bitfield notes.

The included NT51925 test BIN, which belongs to the `925&926` family layout, has:

```text
0xF0: 0x00091200 0x000000FF 0x00000000 0x91E8FA91
decoded HeaderSize: 0x100
header size field : 0xFF
header start field: 0x00000000
header CRC field  : 0x91E8FA91
```

This supports the existing 256-byte Copy Header length for NT51923/NT51926 normal-mode postbuild.

## 2026-07-24 Owner Classification Update

The owner reviewed the same hash-pinned workbook against current product
topology and confirmed:

- NT51919/NT51929/NT51932 currently admit at most eight IC. Their 932 model
  therefore uses workbook Type A/B, not Type C. Cascade is explicitly 2–8 IC,
  and DLM CRC 1 through 7 occupy `[0x7128,0x7144)`.
- On the 950 worksheet, DLM CRC 0 is `[0xA11C,0xA120)`, Header CRC is
  `[0xA130,0xA134)`, and the 19 four-byte words labeled
  `Reserved (DIFF CRC)` at `[0xA134,0xA180)` are DLM CRC 1 through 19 in
  ascending address order. NT51951 aliases this TP header fact.
- NT51930 cascade uses DLM CRC 1 through 12 at `[0x7128,0x7158)`;
  NT51931 cascade uses DLM CRC 1 through 19 at `[0x006C,0x00B8)`.
- For normal 920/923/925/926 headers, Combiner may refresh ILM0, DLM0,
  FW Config, CtrlRAM, MP CtrlRAM, and Header CRC words. This classification
  applies to postbuild write authorization only; it does not grant write access
  to intervening descriptor bytes.

Single-chip NT-based plans retain only their base Header/DLM0 CRC authority.
The additional DLM words above are cascade-only.

## NT51923 vs NT51926 Implications

Current repo behavior that this evidence supports:

- NT51923 and NT51926 both use legacy normal-mode `CRC_Enable` command family for CtrlRAM postbuild.
- NT51926 should not be switched to an NT-based normal-mode family solely because the IC number is 926. The workbook normal sheet is `925&926`, and `Combiner.c` normal mode is generic.
- Copy Header length `0x100` is consistent with the header descriptor at `0xF0` and decoded `HeaderSize = 0x100`.

Known NT51923/NT51926 differences that should remain explicit in structured command evidence:

| Area | NT51923 | NT51926 |
| --- | --- | --- |
| Header copy target | `0x30310` | `0x32A70` |
| Header copy length | `0x100` | `0x100` |
| Normal CtrlRAM block | target `0x22800`, length `14336` | target `0x22800`, length `11264` |
| MP CtrlRAM block | target `0x26000`, length `10240` | target `0x25400`, length `9216` |
| VN CtrlRAM block | target `0x2E800`, length `5728` | target `0x315D0`, length `5278` |
| NF CtrlRAM block | target `0x2A000`, length `17584` | target `0x2C800`, length `11728` |
| FW config backup | source `0x22000`, target `0x3B000`, length `2048` | source `0x22000`, target `0x3B000`, length `1920` |
| Cascade DiffDLM | split source offsets `0x0` and `0x1400`, targets `0x28800` and `0x29400`, length `3072` each | one block from source `0x0`, target `0x27800`, length `10240` |

## Gated Items

This evidence is not enough to claim end-to-end Replace parity:

- The archive includes only an NT51925 test BIN, not NT51923/NT51926 golden Replace outputs.
- The exact production tool version and owner-approved executable binding still govern real transform execution.
- Any production change to CRC/header ranges, command order, Copy Header offsets, or allowed write ranges remains R3 and requires firmware-owner review plus golden evidence.
