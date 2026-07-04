# Adding IC Merge/Replace Workflow

Status: architecture implementation guide as of 2026-07-03.

This guide lists the files and verification flow for adding one IC to Merge and Replace. It is not a support claim. A new IC/workflow becomes releasable only after owner evidence, profile validation, processor diff review, golden regression, and firmware-owner sign-off.

## Invariants

- Reuse an existing flow type before adding a new IC-specific execution branch.
- Profile/catalog data owns ranges, slots, processors, validations, output names, and visibility.
- UI labels and persona choices may affect authoring policy, but they must not create byte-execution branches.
- All ranges use half-open notation `[start, end)`.
- Standard Merge starts from a blank image. Replace starts from a required reference/base image.
- CtrlRAM Replace runs the approved legacy Combiner postbuild sequence after any CtrlRAM replacement.
- General Replace remains explicit mapping over a cloned base image and must stay inside the approved safety envelope.
- Do not commit private BINs. Owner-approved golden fixtures under `testdata/golden/` must include manifest paths, sizes, hashes, source provenance, and human approval.

## Evidence Intake

Collect these before editing production behavior:

| Evidence | Required for | Repo location when owner-approved |
| --- | --- | --- |
| TP flash map, mmap symbols, and region names | Merge, Replace, UI region catalog | `docs/references/ic-flashmap/mmap/` and `docs/references/ic-flashmap/SOURCE_MANIFEST.json` |
| Postbuild BAT or exact command sequence | CtrlRAM Replace, General Replace when TP range can be affected | `docs/references/ic-flashmap/postbuild/` and `docs/architecture/ctrlram-postbuild-command-matrix.md` |
| Combiner version, tool manifest, and argv contract | Any legacy Combiner postbuild | `external-tools/legacy-combiner/1.13.0/manifest.json` and postbuild catalog tests |
| Standard Merge input/output sample | Standard Merge golden regression | `testdata/golden/standard-merge-gen-flash/manifest.json` or approved owner handoff path |
| Replace base, replacement BINs, and expected output | DP Replace, CtrlRAM Replace, General Replace promotion | `testdata/golden/` only after owner approval; otherwise keep private and document the evidence gap |
| Forbidden/protected regions and processor write ranges | Replace safety | profile/catalog code, contract tests, and architecture docs |

If any range, command order, checksum/header rule, padding/truncation behavior, or golden output is inferred rather than evidenced, keep the IC as a candidate and document the gap.

## Choose The Flow Type

Match the new IC to one of the existing flow types in [`ic-workflow-flowcharts.md`](ic-workflow-flowcharts.md):

| Workflow | Preferred reuse point | When to add a new type |
| --- | --- | --- |
| Standard Merge | `SM-GENFLASH`, `SM-GENFLASH-ALIAS`, `SM-FLASHMAP-DYNAMIC`, or `SM-950-951-DP-PERSPECTIVE` | Only when blank initialization, copy order, address spaces, or input-size policy cannot be represented by an existing profile shape. |
| DP Replace | `R-DP-GENERIC` or `R-DP-950-951` | Only when the IC has a different declared DP/LD partition, padding policy, or post-processor requirement. |
| CtrlRAM Replace | `R-CTRLRAM-927`, `R-CTRLRAM-LEGACY-NORMAL`, `R-CTRLRAM-51932`, `R-CTRLRAM-51930`, or `R-CTRLRAM-51950` | Only when postbuild commands or staged file semantics do not match an existing Combiner profile. |
| General Replace | `R-GENERAL` | Only promote after protected ranges, mapping envelope, overlap/alignment rules, and post-processing triggers are known. |

Aliases are acceptable only when the owner confirms the alias and tests prove the alias profile produces the same plan/bytes as the reference IC.

## Files To Update

### Reference and architecture docs

- `docs/references/ic-flashmap/SOURCE_MANIFEST.json`: add source provenance for imported mmap/postbuild references.
- `docs/references/ic-flashmap/mmap/*.h`: add owner-approved mmap evidence when available.
- `docs/references/ic-flashmap/postbuild/*.bat`: add owner-approved postbuild BAT evidence when available.
- `docs/architecture/supported-ic-matrix.md`: add candidate/support status, golden gaps, and owner validation package needs.
- `docs/architecture/ic-workflow-flowcharts.md`: add the IC row and update flow details if the IC changes a shared flow.
- `docs/architecture/ctrlram-postbuild-command-matrix.md`: add CtrlRAM postbuild command count, modes, alias notes, tests, and evidence gaps.
- `docs/architecture/nt51950-nt51951-dp-length-policy.md`: update only when the new evidence affects the NT51950/NT51951 DP rule.

### Profile and catalog code

- `src/NvtFwCombiner.Profiles/BuiltInStandardMergeProfiles.cs`: add or alias Standard Merge profile data.
- `src/NvtFwCombiner.Profiles/BuiltInReplaceProfiles.cs`: add promoted Replace profile data when it is ready to move out of workbench-only behavior.
- `src/NvtFwCombiner.Application/FlashMaps/TpFlashMapCatalog.cs`: add TP/DP/CtrlRAM/customer-info regions and IC-number visibility rows for UI/report catalog use.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildCatalog.cs`: add the structured Combiner postbuild profile and branch commands.
- `src/NvtFwCombiner.Application/ExternalTools/LegacyCombinerPostbuildProfile.cs`: change only when the IC needs a new reusable postbuild model, not for per-IC constants.
- `src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace*.cs`: wire only the temporary workbench path or shared helpers needed to call the existing application runner. Do not encode firmware semantics here if they can live in profiles/catalogs.
- `src/NvtFwCombiner.Presentation.Avalonia/*`: update UI only after profile/catalog data exists; the UI should consume shared catalog state and not duplicate ranges.

### Tests

- `tests/NvtFwCombiner.ProfileContract.Tests/BuiltInStandardMergeProfilesTests.cs`: lock Standard Merge profile shape, accepted input lengths, and alias behavior.
- `tests/NvtFwCombiner.ProfileContract.Tests/BuiltInReplaceProfilesTests.cs`: lock promoted Replace profile compilation and constraints.
- `tests/NvtFwCombiner.Application.Tests/FlashMaps/TpFlashMapCatalogTests.cs`: lock region catalog coverage and IC-number visibility.
- `tests/NvtFwCombiner.Application.Tests/ExternalTools/LegacyCombinerPostbuildCatalogTests.cs`: lock command branches, argv shape, aliases, and processor write/read ranges.
- `tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/LegacyCombinerPostbuildProcessorTests.cs`: lock staging behavior, external processor containment, output validation, and real-tool smoke coverage when available.
- `tests/NvtFwCombiner.GoldenRegression.Tests/*`: add golden byte regression only for owner-approved fixtures.
- `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.cs`: verify the workbench exposes the right slots, plan/report text, and Preview/Build behavior without duplicating firmware rules.
- `tests/NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.cs`: update only when architecture sync guards intentionally change.

## Implementation Flow

1. Add or update owner-approved reference docs and manifest entries.
2. Classify the IC against an existing Standard Merge, DP Replace, CtrlRAM Replace, and General Replace flow type.
3. Add declarative profile/catalog data before touching UI.
4. Add profile/catalog tests that fail if ranges, command branches, IC-number modes, or aliases drift.
5. Add external processor tests for any CtrlRAM or General Replace path that calls legacy Combiner.
6. Add golden regression only after owner approval of expected bytes and hashes.
7. Update workbench/UI smoke tests to prove the shared data is surfaced correctly.
8. Update support docs to separate executable candidate behavior from production support claims.
9. Run the focused tests for changed layers, then run `python scripts/verify.py --all`.
10. Run Polytail before requesting review.

## Promotion Checklist

- IC row exists in `supported-ic-matrix.md` and `ic-workflow-flowcharts.md`.
- Standard Merge profile compiles and has owner-approved golden bytes, or the row remains candidate-only.
- DP Replace declares exact base length, replacement length policy, partition map, and post-processing policy.
- CtrlRAM Replace declares region slots, truncation policy if any, Combiner branch commands, and allowed write ranges.
- General Replace declares allowed mapping envelope, protected ranges, overlap/alignment rules, and TP-range postbuild triggers.
- Preview/Build report records input hashes, output hash, normalized ranges, warnings, Combiner argv, and gated-state reason.
- CI and local `python scripts/verify.py --all` pass.
- R3 firmware-owner review is complete for ranges, command order, checksum/header behavior, processor write ranges, and golden outputs.

## Anti-Patterns

- Adding an IC by branching on `icId` in UI code.
- Treating `unknown` integrity behavior as `none`.
- Running legacy Combiner on user source files or final output paths.
- Allowing processor writes outside declared write ranges.
- Updating expected golden bytes to match unexplained output changes.
- Claiming support from public Standard Merge evidence when private Replace golden evidence is still missing.
