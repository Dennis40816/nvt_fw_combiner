# Adding an IC Merge / Replace Workflow

Status: architecture runbook.

This runbook lists the files and review flow for adding one IC to Merge and Replace. It is not a support claim by itself. An IC/mode is releasable only after profile validation, processor diff review, golden regression, and firmware-owner sign-off.

For 0.9.0 firmware-model-v2 work, canonical physical facts are added to a trusted
`firmware-family-v1` document and workflow policy to `composition-profile-v2`. The C# catalog rows
listed below describe the current v1 compatibility implementation only; do not add a second source
of truth there. Compatibility projections are removed after byte/name/trace parity.

## Non-negotiable model rules

- Merge starts from a blank output image; Replace clones an immutable reference/base image.
- UI, CLI, and workbench paths must call the same composition profile/compiler/runner contracts.
- All ranges are half-open `[start, end)` and name their address space.
- Physical IC facts live in firmware-family documents; workflow semantics live in composition
  profiles. Do not put them in XAML, ViewModels, CLI routing, Bootstrap joins, or one-off scripts.
- Experience selects authoring policy only. It must not create a second byte-execution branch.
- `unknown` integrity behavior is not `none`; it cannot be promoted as supported behavior.
- External tools may mutate only host-created staging copies, then the host must verify changed bytes against declared write ranges.
- Real firmware BIN files stay out of Git unless the owner explicitly approves a golden fixture under `testdata/golden/`.

## Required evidence before coding

Collect these items before adding a production IC profile:

| Evidence | Required for | Notes |
| --- | --- | --- |
| Owner-approved memory map or flash-map source | all workflows | Record source file, sheet/section, revision, owner, and hash when available. |
| Merge source ranges and output size | Standard Merge | Normalize inclusive legacy ranges into half-open `[start, end)`. |
| Replace base/reference length and mutable regions | DP/CtrlRAM/General Replace | Replace never writes back to the input/base artifact. |
| Protected ranges | DP/CtrlRAM/General Replace | Header, customer info, TP CRC/header areas, and any no-touch regions must be explicit. |
| Combiner postbuild command source | CtrlRAM Replace and TP-affecting General Replace | Prefer owner-approved postbuild and mmap references committed as documentation/reference evidence when they contain no firmware payload or secrets. |
| FWConfig layout/version reference | postbuild category selection and UI traceability | Record Common FW version, FW/bar, and PID offsets from a reviewed source such as `ap_fwconfig.c`; commit only non-secret reference code with sanitized provenance and SHA-256 hash. |
| External tool identity | any processor stage | Exact version string, manifest id, executable hash, timeout, and argv shape. |
| Golden fixtures or private golden manifest | promotion | Include input sizes/hashes, expected output hash, source provenance, and owner approval. |

## Owner reference intake

When the owner supplies a mixed folder of IC evidence, run the intake script before changing profiles or C# code:

```text
python scripts/intake_ic_reference.py --source <owner-drop-folder> --ic NT51950 --mode ctrlram-replace --case single --owner <owner-or-team> --source-ref <archive-or-ticket>
```

Use `--mode standard-merge`, `--mode dp-replace`, `--mode ctrlram-replace`, `--mode general-replace`, or `--mode reference-only`. Use `--case` for branch-specific evidence such as `single`, `cascade`, `2chip`, `3chip`, or `dp-0x40000`.

The script stages files under `testdata/golden/owner-handoff/<mode>/<ic>/.../intake/<run-id>/` and writes:

- `handoff_manifest.json` with file sizes, SHA-256 hashes, category guesses, payload role hints, and proposed tracked destinations.
- `NEXT_STEPS.md` with missing document families and promotion checklist.
- `AI_PROMPT.md` with a constrained follow-up prompt for turning the evidence into a reviewed implementation change.

This intake step does not promote support, does not edit C# code, and does not copy private BIN/tool payloads into tracked golden or external-tool locations. Promotion still requires the normal reviewed changes listed below.

## Future Automated Intake Rule

The planned 0.9.4 intake interface must use this declared-evidence model rather
than scan a user's workstation or infer firmware rules from arbitrary BINs. It
may generate only a candidate bundle, a materialized closed-root preview, a
validation report, and a missing-evidence checklist. It must not determine
ranges, CRC/header behavior, aliases, FW Config layouts, support exposure, or
promotion. Only a human-reviewed commit may turn a candidate into a trusted
runtime bundle.

Use `python scripts/create_candidate_ic_intake.py --help` for the concrete
manifest-directed candidate interface. It binds only owner-selected files below
one source root and writes candidate-only JSON records to an existing empty
staging directory. It is deliberately separate from `intake_ic_reference.py`:
the latter is an owner-folder handoff classifier, while candidate intake never
scans a folder and cannot alter runtime registration or existing workflows.

The owner drop folder should contain as many of these as apply:

- flash-map workbook/export and flash header reference;
- `mmap.h` or equivalent memory-map header;
- postbuild BAT/CMD/script/log when TP/CtrlRAM/CRC/header processing is involved;
- FWConfig source or layout note when postbuild category depends on Common FW version;
- combiner source/reference code or exact external combiner tool identity;
- golden input/output BIN files or private fixture hashes for the workflow;
- notes identifying source archive, revision, owner, IC-number branch, expected output filename, and approval status.

For the current detailed owner checklist, sentinel BIN validation method, and 0.7.0 refactor gates, also see
[`0.7.0-refactor-and-evidence-plan.md`](0.7.0-refactor-and-evidence-plan.md). That document is the active checklist for NT51926/NT51930 versioned postbuild categories and for universal special-value BIN validation.

## Files to update

Update only the rows that are relevant to the new IC/mode.

| Area | File | What changes |
| --- | --- | --- |
| IC support / exposure catalog | `src/NvtFwCombiner.Profiles/IcSupportCatalog.cs` | Add the IC id, supported workflow ids, owner-approved alias facts, and short onboarding notes. This is the first C# row to update when introducing a new IC/mode. Workflow ids must come from `IcWorkflowIds.All`; unknown ids fail catalog construction. |
| V2 family/map/profile facts | `profiles/built-in/<bundle>/{families,maps,profiles}` plus its manifest | Put shared family facts, supported capacities, canonical named ranges, operations, and access rules in the manifest-pinned V2 bundle. Runtime and display projections must consume the resolved map and compiled plan; do not add a companion C# family-fact catalog or duplicate facts in UI/CLI code. |
| Standard Merge bundle / deployment / runtime registration | `profiles/built-in/<bundle>/{profile-bundle.json,families,profiles}`; `src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj`; `src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.BuiltInV2.cs` | Add a manifest-pinned V2 family/profile source bundle. The build materializer injects the selected canonical schema from `docs/contracts`; do not add source schema snapshots. A reviewed, evidence-backed bundle must add one `<BuiltInProfileBundle Include="<bundle>" />` materialization allowlist entry and then receive the explicit Bootstrap V2 registration. A 0.9.4 candidate has no runtime authority and must add neither production allowlist nor registration; its closed-root preview belongs in caller-selected staging. |
| Replace profile / V2 deployment | `profiles/built-in/<bundle>/{profile-bundle.json,families,profiles}` plus a focused Bootstrap V2 registration | Add an evidence-backed V2 Replace profile and explicit deployed-bundle registration before routing an IC. Candidate bundles have no runtime authority. Synthetic compiler fixtures are test-only under `tests/NvtFwCombiner.TestSupport/` and are never a production fallback. |
| Profile compiler rules | `src/NvtFwCombiner.Profiles/CompositionProfileCompiler.cs` | Change only for general validation gaps, not to special-case one IC. |
| TP/DP/CtrlRAM compatibility catalog | `src/NvtFwCombiner.Application/FlashMaps/TpFlashMapCatalog.cs` | Project reviewed family rows during migration only. Canonical CtrlRAM eligibility is physical `owner = tp` plus `kind = ctrlram`; do not add a parallel tag authority. |
| TP header/write category | `src/NvtFwCombiner.Application/FlashMaps/TpHeaderCatalog.cs` | Add TP header/postbuild write section ids, report labels, overlap priority, and postbuild block-id classification when the IC introduces a new header copy, backup, CRC, or TP window category. Keep this out of planner, UI, and CLI code. |
| FWConfig metadata reader/catalog | `src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigLayout.cs`, `FirmwareConfigMetadataReader.cs`, and `TpFlashMapCatalog` | Retain the IC's primary FWConfig flash address only for TP Overview/evidence. Every runtime FWConfig value must come from the unique NVT Backup at terminal `T - 0xFFF`; record it in `docs/references/nvt-fwconfig-copy-validation.md` and cross-check exposed primary/Backup fields in golden tests. Do not add a primary fallback. Change layout offsets only through the reviewed layout catalog. |
| Output naming metadata | `src/NvtFwCombiner.Application/FlashMaps/GenFlashVersionCatalog.cs` and `src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.FirmwareMetadata.cs` | Add DP main/sub contiguous version-byte rules, CMI register evidence when applicable, and FlashCode naming metadata only from owner-approved evidence. UI passes selected slot roles and paths; it must not decide DP/TP version offsets, CMI branches, metadata priority, or date/name format. |
| CtrlRAM postbuild catalog | `profiles/built-in/ctrlram-postbuild-v2/catalog.json` plus `BuiltInPostbuildProfileCatalog` | Add structured command sequences, branch rules, staged-file names, firmware block ranges, evidence source, and Common FW rule metadata. Update the pinned SHA-256 and parity tests; never assemble one shell command string. |
| External tool manifest | `external-tools/legacy-combiner/.../manifest.json` | Add or update only when a new exact `combiner.exe` binding/version is approved. |
| Golden manifest | `testdata/golden/standard-merge-gen-flash/manifest.json` or workflow-specific golden folder | Add owner-approved public fixtures only. Use private manifests for confidential firmware evidence. |
| CtrlRAM golden template | `testdata/golden/ctrlram-replace/manifest.template.json` | Keep required private evidence fields synchronized when adding CtrlRAM Replace coverage. |
| Architecture docs | `docs/architecture/supported-ic-matrix.md` and `docs/architecture/ic-workflow-flowcharts.md` | Add the IC/mode status, support gaps, flow id, and evidence notes in the same PR. |
| Processor docs | `docs/architecture/ctrlram-replace-status-report.md`, `docs/architecture/ctrlram-postbuild-command-matrix.md`, and `docs/architecture/integrity-processing-matrix.md` | Update when command count, CRC/header behavior, allowed writes, processor status, experiment result, or owner conclusion changes. |
| Tests | files under `tests/` listed below | Add contract, catalog, postbuild, CLI/UI, and golden coverage matching the risk. |

Do not add IC-specific byte behavior to:

- `src/NvtFwCombiner.Presentation.Avalonia/**`
- `src/NvtFwCombiner.Cli/**`
- `src/NvtFwCombiner.Bootstrap/CliApplication*`
- Workbench facade classes, unless the change is only adapting catalog/profile data into an existing request.

## Standard Merge steps

1. Normalize source evidence into output capacity, fill byte, address spaces, and copy ranges.
2. Author or update one manifest-pinned V2 source bundle: `profile-bundle.json`, family document, profile document, and evidence references. Source schema snapshots are forbidden; the materializer injects the exact manifest-pinned inventory bytes into the closed runtime root.
3. After evidence review, add the source bundle to the explicit Bootstrap materialization allowlist, then add the explicit V2 registration and compile the deployed materialized bundle. Production V2 routes have no legacy runtime fallback.
4. Confirm blank initialization plus ordered copy operations, then add invalid input-size tests for every declared input length rule.
5. Add or update golden regression:
   - public approved fixtures under `testdata/golden/standard-merge-gen-flash/`, or
   - private golden manifest and documented owner sign-off when firmware cannot be committed.
6. Update `supported-ic-matrix.md` and `ic-workflow-flowcharts.md`.

Minimum tests:

- the focused trusted-V2 bundle/routing tests matching the changed profile family
- direct V2 plan contract tests plus `tests/NvtFwCombiner.GoldenRegression.Tests/StandardMergeWorkbenchGoldenTests.cs`; no C# profile oracle remains after the retirement matrix closes the family
- `tests/NvtFwCombiner.ProfileContract.Tests/IcSupportCatalogTests.cs` when support exposure or alias facts change
- direct trusted-V2 map/plan contract tests for every shared family fact or declared capacity that changes
- `tests/NvtFwCombiner.GoldenRegression.Tests/StandardMergeWorkbenchGoldenTests.cs`, which exercises the deployed V2 workbench runtime against every approved Standard Merge fixture
- CLI/UI smoke tests only when the new IC changes surfaced selector behavior or output naming.

## DP Replace steps

1. Confirm DP/LD partition boundaries and whether replacement is whole-only or declared-parts.
2. Confirm base/reference image length and whether shorter replacement inputs may be padded. Padding must be profile-declared.
3. Add a Replace profile with `ImageInitialization.Reference`.
4. Add deny-by-default access rules. DP Replace must expose only DP whole/declared partitions, not TP-persona categories.
5. Preserve protected TP/customer-info/header ranges through profile operations, not UI logic.
6. Add oversize, undersize, boundary, and protected-range tests.
7. Add golden output evidence before claiming support.

Minimum tests:

- a direct V2 compilation/routing test for the changed Replace profile family
- `tests/NvtFwCombiner.ProfileContract.Tests/IcSupportCatalogTests.cs` when support exposure changes
- direct trusted-V2 map/plan contract tests for every shared family fact or declared capacity that changes
- `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.cs` when CLI can build the profile
- UI smoke tests when the IC changes selector, slot, memory coverage, or report behavior
- Golden regression or private golden evidence before support promotion

## CtrlRAM Replace steps

1. Add or confirm TP flash-map CtrlRAM rows in `TpFlashMapCatalog`.
2. Add or confirm TP header/write categories in `TpHeaderCatalog` when postbuild writes new header copy, backup, CRC, or TP window sections.
3. Add postbuild structured commands in `profiles/built-in/ctrlram-postbuild-v2/catalog.json` from owner-approved postbuild/mmap evidence and update its pinned SHA-256.
4. Declare Common FW category rules for versioned ICs, then lock selection with catalog tests so unsupported or ambiguous versions fail closed.
5. Declare IC-number branch rules. Use `single`/`cascade` text choices unless the owner evidence requires numeric 1/2/3 branches.
6. Ensure selected staged-file blocks map to visible CtrlRAM rows.
7. Ensure every CtrlRAM Replace run executes the required postbuild sequence. A raw range replacement without postbuild is not a finished image.
8. Confirm processor allowed write ranges include every Combiner-written byte and reject all others.
9. Run self-replacement and idempotence tests against postbuild-canonical output, then compare with owner golden output when available.

Minimum tests:

- `tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/BuiltInPostbuildProfileCatalogTests.cs`
- the existing Application postbuild planner/command parity tests
- `tests/NvtFwCombiner.Application.Tests/FlashMaps/TpFlashMapCatalogTests.cs`
- processor/staging tests for command argv, staged-file seed bytes, changed-range verification, and failure cases
- UI smoke or CLI tests proving Preview/Build records the postbuild sequence and final artifact hash
- private CtrlRAM golden regression before support promotion

## General Replace steps

1. Define the allowed explicit-mapping envelope and protected ranges in the profile/catalog.
2. Compile runtime mappings into normal `replace-range` operations; do not generate scripts.
3. If a General Replace mapping writes any TP/TP-CtrlRAM/CRC-covered range for the selected IC, the profile must declare the same approved post-processing requirement as the normal workflow. The UI must not decide this.
4. Reject overlap, out-of-bounds, protected-range, and unsupported post-processing cases before execution.
5. Add exact boundary tests around protected and processor-covered ranges.

## Documentation update rule

Every IC workflow change must update:

- `docs/architecture/supported-ic-matrix.md`
- `docs/architecture/ic-workflow-flowcharts.md`

Also update these when applicable:

- `docs/architecture/ctrlram-postbuild-command-matrix.md` for CtrlRAM command count, branch, or alias changes.
- `docs/architecture/integrity-processing-matrix.md` for CRC/header, processor, allowed read/write, or evidence changes.
- `docs/architecture/nt51950-nt51951-dp-length-policy.md` for NT51950/NT51951 DP Perspective behavior.

## Verification commands

Run the narrowest meaningful tests first, then the repository gate before completion:

```text
dotnet test tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj --configuration Release --no-restore
dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj --configuration Release --no-restore
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --configuration Release --no-restore
dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj --configuration Release --no-restore
python scripts/verify.py --all
```

For CtrlRAM/postbuild changes, also run the hash-pinned catalog, planner/argv, staging processor diff validation, and UI/CLI Preview/Build report tests.

## PR notes checklist

Every PR that adds an IC workflow must state:

- IC, mode, composition kind, experience, IC-number choices, and address spaces.
- Evidence sources, hashes, owner, and confidentiality class.
- Changed firmware facts: ranges, operation order, initialization, padding/truncation, processors, allowed writes, output naming.
- Golden evidence run publicly or private evidence that remains owner-gated.
- Commands run and any residual evidence gaps.
- Risk class. Memory ranges, postbuild command order, processor write ranges, and golden output promotion are R3 and need firmware-owner review.
