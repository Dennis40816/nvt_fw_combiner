# TDDI Flash Header Evidence

This note records the purpose and conclusions of owner-provided flash-header
sheet and legacy combiner source evidence. Their public identifiers are
`tddi-flash-header-sheet` and `tddi-flash-header-source` in the
[confidential reference manifest](confidential-references.json). The private
asset repository resolves each manifest SHA-256 to the original evidence.

## Provenance digests

The owner transferred the evidence as one 7z archive on 2026-07-03. The archive and its members are not
committed. These digests keep the chain from the archive to the manifest entries, without file names or paths:

| Item | Bytes | SHA-256 |
| --- | ---: | --- |
| Owner archive (7z) | 213883 | `e3c036b5735d6356205140567af8315e5d3df0ac40764ee366d15e8ca7429aed` |

Members extracted from the archive:

| Member | Public ID | Bytes | SHA-256 | Disposition |
| --- | --- | ---: | --- | --- |
| Flash-header workbook | `tddi-flash-header-sheet` | 316493 | `930fb3e9a3cd652ab493817fc02006bcf4046a5b648b2fd32494c42ff4174fa4` | Private. The manifest entry equals this digest. |
| Legacy combiner source | `tddi-flash-header-source` | 34471 | `5ce2048b9a2e07e970119733c62939f6007ccf061dae6b350194e629171f386c` | Private. The manifest entry is the line-ending-normalized Git blob of this file (33754 bytes, `b178267649dc030c67ceb98552270fe08e9508011dd69958ea3004f3d13298f7`). |
| Legacy combiner executable | none | 30368 | `9ef149beff10f510e0b15a52abf76258daaf52c91c9d0fc0adf3e3ddef22e983` | Not committed. |
| Linker map text | none | 648415 | `4821125eb2cf601c06dcbcb5d95acc248cfbd17805039f33d5838f246456e204` | Summarized only. |
| Firmware test image | none | 262144 | `19fbb0533bbd588ba3f2528a0bc46711f5c23c75852bdac06f38ef804b7f5be5` | Not committed. Real firmware BIN. |

The combiner source and executable come from one legacy combiner release dated 2025-04-18, built from the
commit `71fe7ddd1381238f29d0cea0ae21a6c303d09b25`.

The evidence was used to review normal-mode header interpretation and the
scope of postbuild CRC refreshes. It supports the existing distinction between
normal-mode and NT-based header families, the declared header-copy behavior
for NT51923 and NT51926, and explicit processor write ranges. The accepted
interpretation used by the product is maintained in the
[TP binary model catalog](../architecture/tp-binary-model-catalog.md), profiles,
and postbuild catalog.

The source and sheet do not establish complete CtrlRAM Replace output parity,
authorize a new firmware route, or extend allowed write ranges. Such changes
require firmware-owner review and independent complete-output Golden evidence.
