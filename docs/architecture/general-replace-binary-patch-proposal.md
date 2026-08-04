# General Replace Binary Patch Proposal (Historical UI Scope)

- Status: Historical proposal; canonical General authoring is governed by ADR 0009, and Home Hex Editor UI scope is superseded by ADR 0014.
- Date: 2026-07-10
- Scope: General Replace authoring only; no new firmware range, header, CRC, or postbuild fact.

## Scope note

This document remains the historical rationale for profile-governed General Replace patch sources. Current UI, CLI, Saved Rule, readiness, display, and execution use the canonical `AuthoringMappingState` / `GeneralMappingDraftState` contract; the retired `WorkbenchGeneralReplacePatchInput` DTO is not a compatibility surface. The Home Hex Editor is now a raw BIN utility and follows [ADR 0014](../adr/0014-raw-binary-utility-editor.md); it does not use this proposal's profile, postbuild, or report behavior.

The superseded Bootstrap-only General Replace viewport, base-snapshot, and editable-range projection helpers were retired after ADR 0014 because no CLI or UI product path consumed them. General Replace patch execution, immutable input handling, output alias guards, profile compilation, and postbuild behavior remain on the ordinary composition path; compiler validation, not a display projection, is the range authority.

## Original goal

Allow an advanced user to inspect a selected reference BIN, edit a profile-authorized byte range in a hex view, review the exact before/after bytes, and export a new complete BIN without modifying the original file.

## Original user flow (superseded for the Home Hex Editor)

1. Open the independent `Hex Editor` entry from `Home`. Standard DP, CtrlRAM, and General Replace remain separate normal workflows on the Replace page.
2. The Hex Editor opens its own workspace and device context; it does not show General Replace mapping controls.
3. Load the required base BIN and select IC plus IC number.
4. Select an approved DP/CtrlRAM range from the flash-map view or enter a checked address range.
5. Browse a fixed-width hexadecimal viewport, select an address or an approved range, then stage an equal-length overwrite or fill. Changed cells are highlighted; each cell exposes its base and virtual value on inspection.
6. Use undo/redo while authoring. A compact change list records each patch range and the requested bytes.
7. Build writes a new full BIN through the ordinary General Replace pipeline. The report records generated patch inputs, normalized mapping ranges, byte differences, and any required TP postbuild refresh.

## Contract and safety rules

- Composition kind stays `Replace`; initializer stays the immutable reference/base image.
- The editor creates typed equal-length patch instructions. Bootstrap/Application materializes a host-owned temporary patch artifact, then compiles it to the existing General Replace `explicitMappings` contract.
- UI code never writes the selected base BIN, final output path, or temporary artifact directly.
- The compiler remains the access boundary. A selected range must be inside profile-enabled General Replace regions, non-overlapping, aligned, and in-bounds.
- A TP/CtrlRAM patch requires the same approved legacy Combiner postbuild selection and declared write ranges as any other TP-touching General Replace mapping.
- Cancelling a Build/Preview, closing the desktop window, CLI Ctrl+C, or an external-command timeout terminates the host-started processor tree before the caller returns. No Combiner or worker process may outlive its composition run.
- Export always creates a new full BIN through atomic output promotion. A separate patch-file export, if added later, is evidence only and cannot bypass Build validation.
- Historic reports remain self-contained JSON evidence. The report viewer must not reclassify an old run using a newer IC catalog.

## First increment

- Fixed-width hex viewport with address, hexadecimal and ASCII columns.
- Profile-authorized range selection plus checked Start + Length entry; any inclusive end shown by a surface is derived and read-only.
- Equal-length overwrite and explicit-byte fill commands.
- Virtual base-plus-patch byte view with changed-cell highlighting, undo/redo, and a changed-range list.
- Build-first validation and full-BIN export through existing General Replace execution/reporting.
- UI smoke plus application tests for protected, out-of-bounds, overlapping, and TP-postbuild paths.

## Explicitly deferred

True insert/delete and file-length changes are deferred. They move absolute flash addresses and can invalidate flash-map ranges, FWConfig/header copies, CRC locations, and postbuild command blocks. They require a separate re-layout contract with owner-approved range, header, integrity, and golden evidence; they must not be represented as a convenience edit command.

## Acceptance criteria

- Original base BIN hash remains unchanged after every preview, failed build, successful build, undo, and redo.
- A virtual patch compiles to ordinary General Replace mappings and yields the same bytes as an equivalent supplied replacement BIN.
- The editor rejects header, customer-info, project-ID, and all other protected regions through the compiler, not only through disabled controls.
- A TP/CtrlRAM patch executes the selected postbuild path and reports all expected postbuild/header differences by TP Header section.
- The report shows concise human-readable patch intent and ranges; raw byte/hash details remain audit-only.
- No insert/delete action is exposed until its independent re-layout contract and golden evidence are accepted.

## Promotion gates

- The implementation must pass the canonical local verification command, focused virtual-patch/CLI/UI/process-cancellation tests, and a review that confirms UI/CLI use the same canonical Application admission, exact-route readiness, compiler, and runner.
- A firmware owner must confirm that the existing profile-approved General Replace envelopes, TP postbuild categories, and write ranges are suitable for the intended IC/mode before any new production support claim is made. This feature does not authorize a new range or postbuild rule by itself.
- A release candidate must be committed, reviewed through a pull request, and packaged from the reviewed `main` commit. The portable ZIP must pass the closed-allowlist smoke and clean-Windows checks before a stable tag is created.
- No private BIN, source archive, generated output, or unapproved golden fixture may enter Git or the release ZIP as part of this feature.
