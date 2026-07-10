# ADR 0009: Compile General Replace binary patches as explicit mappings

- Status: Accepted
- Date: 2026-07-10
- Owners: Product owner + architecture owner; individual IC/range enablement remains firmware-owner reviewed

## Context

General Replace needs an advanced hex-authoring surface for correcting a bounded firmware region without asking the user to create a temporary replacement BIN. A second byte executor, UI-owned buffer write, or CLI-only patch command would bypass the established profile compiler, TP postbuild selection, protected-range policy, report traceability, and atomic output writer.

## Decision

General Replace binary patch authoring remains a `Replace` composition with the selected base flash BIN as its immutable reference initializer.

- A patch is a typed, equal-length `overwrite` or single-byte `fill` instruction with a checked inclusive target range.
- Bootstrap materializes each accepted instruction as a host-owned virtual immutable artifact and compiles it to the existing General Replace `explicitMappings` contract with a normal `replace-range` operation.
- The shared profile compiler remains the sole authority for bounds, region access, protected ranges, alignment, overlap, and processor dependencies.
- The shared runner serves virtual artifacts through an infrastructure artifact-reader overlay; virtual artifact identifiers are never paths, cannot be output/report targets, and are reported using their stable report-safe binding ids.
- Build still runs preview-before-build and writes one complete output BIN through the existing atomic output adapter. Any TP/CtrlRAM patch uses the existing selected postbuild plan and declared write ranges.
- Hex UI state may render a provisional before/after view and undo/redo history, but it cannot write the base file, a final output file, or bypass the workbench request.

## Explicit non-goals

True insert, delete, and image-length changes are not General Replace patch operations. They move absolute addresses and can invalidate flash maps, FWConfig/header copies, CRC addresses, and postbuild inputs. They require a separately accepted re-layout contract, profile semantics, and owner-approved golden evidence.

## Alternatives rejected

- `PatchScalar` operations directly authored by UI or CLI: a separate execution shape would weaken the General Replace explicit-mapping contract and make source/input traceability inconsistent.
- Temporary files selected or written by the UI: leaks host paths into authoring and creates source/output alias risks.
- Arbitrary patch scripts or user-supplied commands: bypasses reviewed processor declarations and cannot be reproduced safely.
- Treating delete as fill: delete has length-changing semantics; a fill is exposed only as an explicit equal-length byte overwrite.

## Consequences

- The experimental Hex Editor is a separately labelled section below normal Replace workflows; it is not a General Replace authoring mode. UI and CLI share the same `WorkbenchGeneralReplacePatchInput` contract and receive the same profile/postbuild/report behavior.
- A virtual patch is visible in reports as a named generated input rather than a local file path.
- Existing file mapping rows remain supported and can be combined with non-overlapping patches.
- Repeated preview/build uses the immutable virtual bytes supplied in the request, so the preview token captures the same input hash and plan fingerprint as a regular mapping.

## Verification

- A virtual patch produces the same full output as an equal file-backed General Replace mapping.
- Malformed hex, fill-byte errors, range/length mismatch, duplicate ids, bounds, overlap, and protected-region requests fail closed with stable issues.
- TP/CtrlRAM virtual patches select the existing postbuild path and report only declared postbuild/header mutations.
- Base-file bytes/hash remain unchanged after preview, failure, and build.
- CLI and UI smoke coverage exercise the same workbench request; no UI/CLI implementation may write firmware bytes directly.
