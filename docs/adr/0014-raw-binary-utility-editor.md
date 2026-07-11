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
3. The raw document supports byte overwrite, inclusive range overwrite, inclusive one-byte fill, insert `00` before or after a byte, delete a byte, set `00`, set `FF`, undo, and redo.
4. Insert/delete change only the memory document's address layout. The editor preserves original-byte identity so reference rows can distinguish inserted bytes from shifted source bytes.
5. Save and `Ctrl+S` require confirmation and use Save As semantics only. Export creates a new path through an atomic writer, rejects the loaded source path, and never overwrites an existing output.
6. The first viewport page renders immediately. Later pages append continuously from the in-memory document only while the Hex Editor page is active. The scheduler stops when no more rows remain, the view detaches, or navigation leaves the page. Loading another source, navigating, or editing resets the projection before subsequent pages append.
7. The Hex Editor must state that it does not validate firmware structure or produce a firmware composition report. A modified BIN can be structurally invalid firmware.

## Consequences

- General Replace remains the supported firmware-aware explicit mapping workflow, including profile compiler checks and required TP postbuild behavior.
- Raw Hex Editor output is an arbitrary user-authored BIN, not a supported firmware build claim.
- Presentation calls a typed Bootstrap facade. File reading/writing stays outside Presentation; the Application raw session has no filesystem, profile, composition, Flash Map, or external-tool dependency.
- No CLI command is required in this increment. Any future CLI editor command must use the same typed raw-session contract.

## Verification

- Application tests cover source immutability, overwrite/fill, insert/delete shifts, undo/redo, original-identity mapping, and page creation.
- UI smoke tests cover direct selection highlighting, progressive page append, source immutability after Save As, and raw workspace isolation from Replace state.
- Architecture tests reject General Replace/profile/postbuild references from the raw panel/workspace and direct Presentation file I/O.
- Visual review verifies compact 16-byte columns, row/header/address focus alignment, shared context actions, and an idle-free progressive renderer.

## Human Review Gate

This decision authorizes no firmware range, format, checksum, header behavior, or golden parity claim. A user may export arbitrary bytes; firmware owners remain responsible for deciding whether the resulting BIN is valid for a device.
