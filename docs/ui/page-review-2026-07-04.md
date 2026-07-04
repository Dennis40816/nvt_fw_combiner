# UI Page Review - 2026-07-04

Status: implementation decision note for the July 2026 workbench UI review.

Scope reviewed: Home, Settings, Merge, Replace, and the report modal. A second AI reviewer independently inspected the same surfaces and returned P0/P1/P2 findings without editing files.

## Implemented In This Change

- P0: Build now requires a successful Preview for the exact current workflow state. IC, Number, mode, slot file path, file length, or file timestamp changes invalidate Build until Preview is rerun.
- DP safety correction: NT51950/NT51951 DP Replace derives work length from the selected base firmware length. Approved lengths are `0x40000`, `0x80000`, and `0x100000`; `0x100000` is not a universal base assumption.
- Settings visual consistency: non-technical settings selectors use the normal UI font, while IC and Number selectors keep the fixed-width technical font.
- Settings readiness honesty: preference rows distinguish `Session`, `Pending`, `Default`, and `System` states instead of implying full persistence or complete localization.
- Report modal semantics: the outcome badge now shows a success or issue icon with an accessibility label instead of the same neutral information symbol for every status.

## Reasonable Follow-Ups Not Bundled Here

- Extract shared `FirmwareSlotCard`, `MemoryCoveragePanel`, and `WorkbenchActionBar` controls. This is reasonable, but it touches most Merge/Replace XAML and should be a dedicated UI refactor with screenshots.
- Make Merge and Replace memory coverage use one shared visual component. This should be paired with the shared-control extraction to avoid two near-identical panels.
- Move all remaining hard-coded page and report text into text resources. This is required for full bilingual readiness, but it is broad enough to review separately.
- Expand General Replace mapping authoring with source range, target space, reason, and validation state. This needs typed application/compiler contract work, not a XAML-only patch.
- Add report history and reopen-by-run metadata. The current modal remains a secondary surface; a history model should be implemented without adding a top-level Report page.
- Replace string-derived slot icon kind with typed slot metadata from the workbench contract. The current heuristic remains display-only and does not affect firmware behavior.

## Verification

- UI smoke tests cover Preview-before-Build, invalidation after input/context changes, Standard Merge golden builds, DP Replace approved base lengths, and CtrlRAM Replace postbuild output.
