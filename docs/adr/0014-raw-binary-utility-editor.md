# ADR 0014: Raw Binary Utility Editor

- Status: Accepted
- Date: 2026-07-11
- Risk class: R2
- Supersedes: the Hex Editor UI integration portions of ADR 0009; the General Replace virtual-patch core and CLI contract remain accepted.

## Context

The Home-launched Hex Editor is intended for direct inspection and controlled editing of any BIN, including files that are not supported firmware images. Treating it as a General Replace authoring surface wrongly requires IC, IC number, Flash Map, TP Header, profile access, CRC, postbuild, and report semantics. It also prevents normal binary-edit operations such as insert and delete.

## Decision

1. Hex Editor is a standalone `Util Tools` raw-BIN workspace. It has no IC, number, profile, Flash Map, TP Header, CRC, postbuild, composition report, or General Replace dependency.
2. A source BIN is read once into a private, application-owned memory document. The original file remains immutable for the lifetime of the session.
3. The raw document supports byte overwrite, inclusive range overwrite, inclusive one-byte fill, insert one or a bounded number of zero-filled bytes before or after a byte, delete a byte, set `00`, set `FF`, undo, and redo. Range overwrite writes the supplied sequence from inclusive `Start`, accepts a sequence shorter than the selected range, and rejects a sequence that would cross inclusive `End`; bytes after a shorter sequence remain unchanged. Fill still writes one byte across every address in the selected range. One multi-byte insertion is one undoable operation and is limited by the Application contract.
4. Insert/delete change only the memory document's address layout. The editor preserves original-byte identity for structural classification, while an independent same-address original value drives the optional reference row. Therefore an inserted tail byte can retain a source identity for offset analysis while the source value at its new display address is correctly absent (`--`). Diff classification has two independent axes: `Data` compares a current value with its retained source identity, while `Structural` records that the current byte came from another source address. This prevents a pure address shift from falsely classifying every shifted value as a value edit. Changed-block navigation still uses their union, so one insert/delete produces a continuous structural block from the operation address through the affected tail even when repeated byte values happen to compare equal.
5. Address entry is hexadecimal only and canonicalized as lowercase `0x` plus uppercase digits. Presentation uses one shared Hex input behavior for addresses, bytes, and Excel-friendly byte sequences; the Application session rejects decimal and non-canonical address prefixes.
6. Save and `Ctrl+S` require confirmation and use Save As semantics only. Export creates a new path through an atomic writer, rejects the loaded source path, and never overwrites an existing output.
7. Source length determines the complete logical row count immediately. The Hex Editor presents a fixed bounded row window over that in-memory document with a dedicated document scrollbar; dragging or Go To replaces only that window. This keeps scrollbar geometry stable and never background-materializes the complete BIN.
8. Printable-ASCII search reports every in-memory match, highlights matching bytes and ASCII characters, and cycles deterministically through the result index. Contiguous changed blocks are navigation aids only; they do not imply firmware semantics. Value edits remain highlighted per byte. A structural shift uses text color rather than a byte-cell background, and its visible wrapped ASCII span is enclosed by one unfilled outline carrying the same one-based number as the changed-block inspector. The entire valid shift area inside that outline, including character spacing, padding, cross-row whitespace, and visible original comparison rows, exposes a context menu that navigates to the block head or tail. Optional original rows use the current display address, read same-address source bytes, and show `--` beyond the source end; when enabled, they appear for every visible value-edit or structural-shift row. Hover evidence lists the first value transition and exact derived insert/delete address and count.
9. The Hex Editor must state that it does not validate firmware structure or produce a firmware composition report. A modified BIN can be structurally invalid firmware.

## Consequences

- General Replace remains the supported firmware-aware explicit mapping workflow, including profile compiler checks and required TP postbuild behavior.
- Raw Hex Editor output is an arbitrary user-authored BIN, not a supported firmware build claim.
- Presentation calls the typed Bootstrap file-session facade and consumes the Application raw-editor contracts directly. Bootstrap does not mirror those contracts. File reading/writing stays outside Presentation; the Application raw session has no filesystem, profile, composition, Flash Map, or external-tool dependency.
- No CLI command is required in this increment. Any future CLI editor command must use the same typed raw-session contract.

## Verification

- Application tests cover source immutability, bounded short-sequence overwrite, rejected overwrite beyond the inclusive end, fill, bounded multi-byte insert/delete causes, undo/redo, retained-identity classification, same-address original values including absent inserted-tail addresses, structural-tail aggregation independent from displayed-address data equality, and page creation.
- UI smoke tests cover direct selection highlighting, single wrapped structural ASCII block numbering/navigation contracts, changed-block hover reasons, bounded insert modal behavior, same-address original rows across every visible shifted row, Undo/Redo feedback, stable full-document scrollbar extent, bounded viewport navigation, source immutability after Save As, and raw workspace isolation from Replace state.
- Architecture tests reject General Replace/profile/postbuild references from the raw panel/workspace and direct Presentation file I/O.
- Visual review verifies compact 16-byte columns, row/header/address focus alignment, the dedicated scrollbar gutter, shared context actions, and bounded viewport responsiveness.

## Human Review Gate

This decision authorizes no firmware range, format, checksum, header behavior, or golden parity claim. A user may export arbitrary bytes; firmware owners remain responsible for deciding whether the resulting BIN is valid for a device.
