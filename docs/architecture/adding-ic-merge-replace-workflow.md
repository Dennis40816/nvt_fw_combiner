# Adding an IC Merge / Replace Workflow

Status: architecture runbook.

This runbook lists the files and review flow for adding one IC to Merge and Replace. It is not a support claim by itself. An IC/mode is releasable only after profile validation, processor diff review, golden regression, and firmware-owner sign-off.

Canonical physical facts are added to a trusted firmware-family document and
workflow policy to a composition profile. Built-in bundle and executable-route
admission are declared together in
`profiles/built-in/package-trust-index.json`; generic C# registries only project
that immutable package data. Support exposure remains a separately reviewed
policy and must not be inferred from runtime admission.

## Non-negotiable model rules

- Merge starts from a blank output image; Replace clones an immutable reference/base image.
- UI, CLI, and workbench paths must call the same composition profile/compiler/runner contracts.
- All ranges are half-open `[start, end)` and name their address space.
- Built-in Initial Code, DP, TP, LDC, TPA, and TPB inputs use address-bearing
  source views. TPA is same-coordinate; TPB uses a resolved bank placement
  delta. Compact CtrlRAM payloads alone currently use source byte `0` as the
  normal origin for a nonzero firmware target.
- Section inputs require source-view coverage and may be supplied by a
  compatible same-IC FlashCode. Replace Reference and complete DP AB seeds
  instead require one exact declared container variant.
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
| Merge source ranges and output size | Standard Merge | Normalize inclusive legacy ranges into half-open `[start, end)`; record every source/metadata/validation read and expected outer length separately. |
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
| IC support / exposure policy | `docs/contracts/canonical-capability-policy-v1.{json,md,schema.json}` | Runtime onboarding through the package trust index does not itself expose or promote support. Add or revise an exact route only in its separately approved policy ticket; every authoring/publication/evidence decision must pin the canonical route and capability fingerprint. |
| V2 family/map/profile facts | `profiles/built-in/<bundle>/{families,maps,profiles}` plus its manifest | Put shared family facts, supported capacities, canonical named ranges, operations, and access rules in the manifest-pinned V2 bundle. Runtime and display projections must consume the resolved map and compiled plan; do not add a companion C# family-fact catalog or duplicate facts in UI/CLI code. |
| Built-in bundle / deployment / runtime registration | `profiles/built-in/<bundle>/{profile-bundle.json,families,profiles}` plus `profiles/built-in/package-trust-index.json` | Add a manifest-pinned V2 family/profile source bundle. The build materializer injects the selected canonical schemas from `docs/contracts`; do not add source schema snapshots. After evidence review, add one hash-pinned bundle entry and its closed-vocabulary runtime registrations to the package trust index. Existing-vocabulary onboarding requires no IC-specific Domain, Application, Bootstrap, CLI, or Presentation route edit. Candidate-only staging must not enter the package index. |
| Replace profile / V2 deployment | The same bundle and package trust-index entry | Add an evidence-backed V2 Replace profile and the exact `dp-replace`, `general-replace`, or `ctrlram-replace` data registration before routing an IC. Processor and branch fields are allowed only for CtrlRAM registrations. Synthetic compiler fixtures are test-only under `tests/NvtFwCombiner.TestSupport/` and are never a production fallback. |
| Profile compiler rules | `src/NvtFwCombiner.Profiles/V2/` | Change only for general validation gaps, not to special-case one IC. The V1 `CompositionProfileCompiler` is retired. |
| TP/DP/CtrlRAM compatibility catalog | `profiles/built-in/ctrlram-postbuild-v2/flash-map.json` plus `BuiltInTpFlashMapCatalog` | Add reviewed TP/full-Flash shapes and TP Overview rows as hash-pinned config facts. Canonical CtrlRAM eligibility is physical `owner = tp` plus `kind = ctrlram`; do not add a parallel C# or tag authority. |
| TP Header metadata and behavior | Versioned family/profile contracts | Declare the common `tp-flash-header` definition once, then reference exact resolved spans, fields, series, or groups from read-only or execution behavior bindings. Do not add a parallel C# layout or report catalog. |
| FWConfig metadata reader/catalog | `src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigLayout.cs`, `FirmwareConfigMetadataReader.cs`, and hash-pinned `flash-map.json` | Retain the IC's primary FWConfig flash address only for TP Overview/evidence. Every runtime FWConfig value must come from the unique NVT Backup at terminal `T - 0xFFF`; record it in `docs/references/nvt-fwconfig-copy-validation.md` and cross-check exposed primary/Backup fields in golden tests. Do not add a primary fallback. Change layout offsets only through the reviewed layout catalog. |
| Output naming metadata | Versioned family/profile metadata bindings plus `src/NvtFwCombiner.Bootstrap/CompositionOutputNaming.cs` and `FirmwareInspectionAdapter.Metadata.cs` | Add DPCMI and FWConfig typed fields, target purposes, and FlashCode naming metadata only from owner-approved evidence. UI passes selected slot roles and paths; it must not decide DP/TP version offsets, CMI branches, metadata priority, or date/name format. |
| CtrlRAM postbuild catalog | `profiles/built-in/ctrlram-postbuild-v2/catalog.json` plus `BuiltInPostbuildProfileCatalog` | Add structured command sequences, branch rules, staged-file names, firmware block ranges, evidence source, and Common FW rule metadata. Update the pinned SHA-256 and parity tests; never assemble one shell command string. |
| External tool manifest | `external-tools/legacy-combiner/.../manifest.json` | Add or update only when a new exact `combiner.exe` binding/version is approved. |
| Golden manifest | `testdata/golden/canonical/manifest.json` | Add owner-approved direct cases or explicit fact-scoped aliases only. Keep diagnostics outside canonical expected evidence. |
| Golden test disposition | canonical case `provenance/case.json` | Declare one typed disposition and exact test-symbol evidence; allowed differences require named, bounded `output-image` half-open ranges. |
| Architecture docs | `docs/architecture/supported-ic-matrix.md` and `docs/architecture/ic-workflow-flowcharts.md` | Add the IC/mode status, support gaps, flow id, and evidence notes in the same PR. |
| Processor docs | `docs/architecture/ctrlram-replace-status-report.md`, `docs/architecture/ctrlram-postbuild-command-matrix.md`, and `docs/architecture/integrity-processing-matrix.md` | Update when command count, CRC/header behavior, allowed writes, processor status, experiment result, or owner conclusion changes. |
| Tests | files under `tests/` listed below | Add contract, catalog, postbuild, CLI/UI, and golden coverage matching the risk. |

Do not add IC-specific byte behavior to:

- `src/NvtFwCombiner.Presentation.Avalonia/**`
- `src/NvtFwCombiner.Cli/**`
- `src/NvtFwCombiner.Cli/CliApplication*`
- focused Bootstrap port adapters, unless the change is only adapting compiled profile data into an existing typed request.

## Standard Merge steps

1. Normalize source evidence into output capacity, fill byte, address spaces, and copy ranges.
2. Author or update one manifest-pinned V2 source bundle: `profile-bundle.json`, family document, profile document, and evidence references. Source schema snapshots are forbidden; the materializer injects the exact manifest-pinned inventory bytes into the closed runtime root.
3. After evidence review, add the bundle hash/materialization and exact `standard-merge` registration to the package trust index, then compile the deployed materialized bundle. Production V2 routes have no legacy runtime fallback.
4. Confirm blank initialization plus ordered copy operations, then add invalid input-size tests for every declared input length rule.
5. Add or update golden regression:
   - owner-approved direct fixtures under `testdata/golden/canonical/`, or
   - private golden manifest and documented owner sign-off when firmware cannot be committed.
6. Update `supported-ic-matrix.md` and `ic-workflow-flowcharts.md`.

Minimum tests:

- the focused trusted-V2 bundle/routing tests matching the changed profile family
- direct V2 plan contract tests plus the deployed Standard Merge golden suite (currently named `tests/NvtFwCombiner.GoldenRegression.Tests/StandardMergeWorkbenchGoldenTests.cs` on the pre-retirement baseline); no C# profile oracle remains after the retirement matrix closes the family
- canonical capability policy schema/loader tests and exact-route projection tests when support exposure or family facts change
- direct trusted-V2 map/plan contract tests for every shared family fact or declared capacity that changes
- the deployed Standard Merge golden suite, which exercises the canonical V2 runtime against every approved Standard Merge fixture; the pre-retirement filename above is renamed when the last Workbench symbol is deleted
- CLI/UI smoke tests only when the new IC changes surfaced selector behavior or output naming.

## DP Replace steps

1. Confirm Initial Code/LDC partition boundaries and whether replacement is
   whole-only or declared-parts.
2. Confirm base/reference image length and whether shorter replacement inputs may be padded. Padding must be profile-declared.
3. Add a Replace profile with `ImageInitialization.Reference`.
4. Add deny-by-default access rules. DP Replace must expose only DP whole/declared partitions, not TP-persona categories.
5. Preserve protected TP/customer-info/header ranges through profile operations, not UI logic.
6. Add oversize, undersize, boundary, and protected-range tests.
7. Add golden output evidence before claiming support.

Minimum tests:

- a direct V2 compilation/routing test for the changed Replace profile family
- canonical capability policy schema/loader tests and exact-route projection tests when support exposure changes
- direct trusted-V2 map/plan contract tests for every shared family fact or declared capacity that changes
- `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.cs` when CLI can build the profile
- UI smoke tests when the IC changes selector, slot, memory coverage, or report behavior
- Golden regression or private golden evidence before support promotion

## CtrlRAM Replace steps

1. Add or confirm TP/full-Flash shapes and TP flash-map CtrlRAM rows in hash-pinned `profiles/built-in/ctrlram-postbuild-v2/flash-map.json`.
2. Add or reference the canonical TP Header definition and declare exact behavior bindings for header copy, backup, CRC, processor, or report consumers. When the same-IC Standard profile declares report classification, every admitted CtrlRAM runtime registration names that exact map through `reportMetadataMapId`; when it declares none, the field is absent. Do not infer the map from capacity or input length, and do not add a parallel geometry or report authority.
3. Add postbuild structured commands in `profiles/built-in/ctrlram-postbuild-v2/catalog.json` from owner-approved postbuild/mmap evidence and update its pinned SHA-256.
4. Declare only owner-provided runtime postbuild revisions. One runtime profile covers Common FW
   `[1.0.0, infinity)`; multiple profiles form ordered effective-version intervals whose next entry
   starts the next range. Evidence-only entries create no runtime boundary. Test interval edges and
   fail closed only when multiple runtime profiles cannot be selected unambiguously.
5. Declare build plans from distinct owner-provided command sequences. Use `single` and generic
   `cascade` (`Number > 1`) unless command evidence requires exact 2/3-chip or non-overlapping count
   ranges. Never create a plan from a golden's observed count.
6. Ensure selected staged-file blocks map to visible CtrlRAM rows.
7. Ensure every CtrlRAM Replace run executes the required postbuild sequence. A raw range replacement without postbuild is not a finished image.
8. Confirm processor allowed write ranges include every Combiner-written byte and reject all others.
9. Run self-replacement and idempotence tests against postbuild-canonical output, then compare with owner golden output when available.

Minimum tests:

- `tests/NvtFwCombiner.Infrastructure.Tests/ExternalTools/BuiltInPostbuildProfileCatalogTests.cs`
- the existing Application postbuild planner/command parity tests
- `tests/NvtFwCombiner.Infrastructure.Tests/FlashMaps/BuiltInTpFlashMapCatalogLoaderTests.cs` plus the Bootstrap flash-map projection tests
- processor/staging tests for command argv, staged-file seed bytes, changed-range verification, and failure cases
- UI smoke or CLI tests proving Preview/Build records the postbuild sequence and final artifact hash
- private CtrlRAM golden regression before support promotion

## General Replace steps

1. Define the allowed explicit-mapping envelope and protected ranges in the profile/catalog.
2. Add the exact `general-replace` IC/profile/version registration to the package trust index; do not add an IC-specific Application, Bootstrap, UI, or CLI branch.
3. Compile runtime mappings into normal `replace-range` operations; do not generate scripts.
4. If a General Replace mapping writes any TP/TP-CtrlRAM/CRC-covered range for the selected IC, the profile must declare the same approved post-processing requirement as the normal workflow. The UI must not decide this.
5. Reject overlap, out-of-bounds, protected-range, and unsupported post-processing cases before execution.
6. Add exact boundary tests around protected and processor-covered ranges.

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
