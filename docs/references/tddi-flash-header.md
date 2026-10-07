# TDDI Flash Header Evidence

This note records the purpose and conclusions of owner-provided flash-header
sheet and legacy combiner source evidence. Their public identifiers are
`tddi-flash-header-sheet` and `tddi-flash-header-source` in the
[confidential reference manifest](confidential-references.json). The private
asset repository resolves each manifest SHA-256 to the original evidence.

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
