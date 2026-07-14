# Owner Verification Inputs: v0.8.x to v0.9.x

Status: active owner checklist.

This checklist identifies data the owner may provide for verification. It does
not ask for secrets, and it does not authorize a profile, an IC, or a release
by itself. Candidate development may start without these inputs under the
[pre-tag candidate-development policy](../architecture/0.9.x-completion-roadmap.md#pre-tag-candidate-development-policy).
Golden parity, support promotion, release tags, and firmware-owner review still
require the evidence listed for their milestone.

## Submission Rules

- Keep private firmware, expected outputs, tool executables, and proprietary
  maps outside Git. Place only approved public fixtures under `testdata/golden/`
  with a manifest, provenance, sizes, hashes, and owner approval.
- Use `testdata/golden/owner-handoff/<workflow>/<ic>/inputs/` for a local
  intake copy. The tracked `CASE.md` file in that directory defines the
  requested case shape; payloads remain ignored.
- Retain original filenames. For every supplied file record logical role,
  original filename, byte length, SHA-256, source archive/ticket, and owner.
  A `provenance.json` alongside the private payload is the preferred format.
- For an external Combiner case, also provide the exact executable SHA-256,
  tool version, adapter id, platform, timeout, full command/argument trace,
  input/output mode, and every required sidecar such as `map.txt`.
- A workbook or FlashMap fact needs its original workbook/export checksum and
  the exact sheet, row, column/cell, and owner decision. Intake opens Office
  files read-only and never executes macros.
- A statement that two ICs are "same" or "similar" is useful planning input,
  but it does not replace a fact-scoped alias decision, an input/output hash,
  or a firmware-owner approval.
- Do not send passwords, API keys, signing private keys, or certificates with
  private keys. Supply signer identity, policy, and a detached verification
  result instead.

## Evidence Levels

| Level | What it permits | Minimum data |
| --- | --- | --- |
| Candidate branch | Schema, parser, intake, synthetic test, or blocked profile work | No owner firmware payload required; all unknown firmware facts stay blocked. |
| Runtime candidate | A closed V2 path with no support claim | Exact map/profile facts and synthetic or legacy-comparison evidence; unresolved integrity remains blocked. |
| Product golden | Byte-level behavior claim for one IC/mode/topology/capacity | Exact input set, expected output, hashes, original names, tool trace where applicable, and explained differences. |
| Supported/released workflow | UI/CLI exposure or release support claim | Product golden, processor/integrity evidence, support-matrix decision, firmware-owner review, and all applicable release gates. |

## Confirmed Missing Inputs (2026-07-14)

This is the current delivery list, rather than a hypothetical future checklist.
It is based on the checked-in handoff directories and tool inventory on this
date. A checked-in `CASE.md` or `.keep` file reserves a location; it is not
firmware evidence. Preserve the original filenames in `provenance.json` even
when using the normalized local names below.

| Milestone | Current state | Data to provide now | Not needed now |
| --- | --- | --- | --- |
| v0.8.x | Tagged baseline; no known evidence blocker. | Only for a UAT or package defect: the failing artifact/log record listed below. | Production firmware BINs for routine package verification. |
| v0.9.1 / v0.9.2 | Tagged, support-neutral migration baselines. | Only a new/disputed capacity, alias, workflow, or metadata case selected for release support. | A duplicate golden merely to re-verify the existing tag. |
| v0.9.3 AB | `testdata/golden/ab-merge/manifest.json` tracks one direct NT51929 case and two NT51950 cases with owner-approved fixture storage. | NT51919, NT51932, and NT51951 packages; plus NT51950 Combiner sidecar/trace evidence in the next table. | A C# CRC implementation. Header CRC must remain a declared Combiner mutation. |
| v0.9.3 CtrlRAM version edit | Handoff directories contain templates/placeholders only; no Preserve/Edit expected-pair evidence is present. | One Preserve/Edit package per release-selected IC, IC-number branch, and postbuild category. | A separate case for Preview: Preview is not allowed to edit FW version. |
| v0.9.4 candidate intake | Candidate tooling can proceed without firmware payload. | A source workbook/export/package only when a real candidate IC is to be reviewed. | A product golden or support sign-off. |
| v0.9.5 legacy retirement | No retirement row is yet closed. | One complete V2 parity package for the specific matrix row selected for removal. | Evidence for every legacy row at once. |
| v0.9.6 release/support | Not yet a release-evidence phase. | The release/support package only when a release scope is chosen. | Signing private keys, passwords, tokens, or private certificates. |

### v0.9.3 AB Delivery Matrix

Supply one package for every selected capacity/topology branch. Do not reuse an
NT51950 result for NT51951 or rely on a filename-only "same IC" assertion.

| Target | Required private files | Additional owner decision/evidence |
| --- | --- | --- |
| NT51919 | `dp-ab.bin`, `tpa.bin`, `tpb.bin`, `expected.bin`, `provenance.json` | Fact-scoped alias/parity decision identifying the effective map, capacity/topology, and direct member output hash. |
| NT51929 | The tracked 512 KiB fixture has a DP AB input, one TP FW reused as TPA/TPB, and an expected output. | Direct case metadata, including the effective map/capacity/topology, and firmware-owner review before runtime exposure. |
| NT51932 | `dp-ab.bin`, `tpa.bin`, `tpb.bin`, `expected.bin`, `provenance.json` | Direct case metadata; any reuse of NT51929 facts needs the fact-scoped parity decision. |
| NT51950 | Two tracked 512 KiB fixture cases reuse one TP FW as TPA/TPB and include expected outputs. | Exact `map.txt`, `combiner-command.txt`, `combiner-tool.json`, chip-count branch, declared stage read/write ranges, Combiner trace, tool SHA-256, timeout/platform, and final output filename. |
| NT51951 | `dp-ab.bin`, `tpa.bin`, `tpb.bin`, `expected.bin`, `provenance.json`, exact `map.txt`, `combiner-command.txt`, `combiner-tool.json` | Its own relocation/header, trace, tool binding, and output evidence. NT51950 evidence is not a substitute. |

The repository currently contains `Combiner.exe` and its manifest for version
`1.13.0`, but no AB-specific `map.txt` sidecar or replayable 950/951 AB command
trace. Supply those per target, not as an inferred shared default.

### v0.9.3 CtrlRAM Preserve/Edit Delivery Matrix

Use the identical base and replacement inputs for the two outputs so that the
only intentional branch is the user-selected TP FW major/sub-version edit.

| Item | Required private files or values |
| --- | --- |
| Base and replacement inputs | `base.bin` plus one replacement BIN for every selected CtrlRAM region/group. |
| Preserve branch | `expected-preserve.bin`, final output SHA-256, and expected output filename. |
| Edit branch | `expected-edit.bin`, requested TP FW major/sub-version, final output SHA-256, and expected output filename. |
| Combiner evidence | `combiner-command.txt`, `combiner-tool.json`, exact staged-file names, tool hash/version, command order, `map.txt` where required, and declared read/write/diff ranges. |
| FWConfig evidence | Source and final unique NVT Backup observations: terminal marker, `T - 0xFFF` base, original and edited major/sub-version bytes, plus the applicable `FirmwareConfigLayout` revision. |
| Approval | IC, IC-number mode/value, Common FW/postbuild category, owner, ticket/archive, and approval date. |

## v0.8.x: Packaging, Security, and UAT

`v0.8.0` is already tagged. These inputs are only needed for a corrective
0.8.x/UAT or packaging issue, and do not require firmware BINs by default.

| Needed data | Verification use |
| --- | --- |
| Failing CI/release job URL, complete log, commit SHA, and timestamp | Reproduce a packaging or policy failure without guessing the environment. |
| Generated Windows x64 ZIP, `RELEASE-MANIFEST.json`, `SHA256SUMS.txt`, SBOM/provenance, and third-party notices | Check the closed package allowlist and every shipped hash. |
| Clean Windows x64 smoke record: Windows version, no separate .NET/Python confirmation, startup result, CRC worker self-check, and UI launch result | Prove the portable package rather than the developer workstation. |
| Signing policy, signer identity, detached signature/verification result, and legal approval | Release audit. Never provide a signing private key. |
| Proprietary/reference redistribution approval and fixture manifest | Decide whether a reference or approved public fixture may be packaged. |

## v0.9.0: Raw-BIN Utility

No IC or firmware-validity data is required for the tagged raw-BIN utility.
For a UAT defect, provide a non-sensitive reproducible byte sample, the exact
editor action sequence, expected/actual bytes, and application version. Do not
use a production firmware image unless its handling and provenance are approved.

## v0.9.1: V2 Migration Parity

`v0.9.1` is tagged and support-neutral. New data is required only when an IC,
capacity, alias, or workflow is selected for release support or its current
parity is questioned.

| Area | Owner data needed for a new or disputed case |
| --- | --- |
| Standard Merge | DP input, TP input, expected flash output, exact output filename, source/output SHA-256, selected capacity, and owner approval. |
| NT51950/NT51951 Standard Merge | One direct DP/TP/expected case for every additional capacity selected for release; state whether customer info remains from DP. Do not derive capacity behavior from a differently sized sample. |
| NT51950/NT51951 DP Replace | Base flash, DP replacement input, expected output, selected capacity, TP/customer ownership decision, and full-byte legacy/V2 comparison record. |
| Alias or IC-count fact | Source IC/map, target IC/map, exact fact kind/id, applicable topology/capacity, direct parity input/output hashes, and owner approval. |
| Firmware metadata | Firmware sample or approved observation plus NVT Backup location evidence. Runtime FW Config is always terminal `T - 0xFFF`; a primary flash-map address is evidence only. |

## v0.9.2: Profile-Bundle Consolidation

`v0.9.2` is tagged. It needs no new firmware payload for normal verification.
For a new trusted bundle or schema source, provide the schema/source license
decision, exact content SHA-256, bundle manifest, and any expected
materialization/validation report. This never promotes firmware support.

## v0.9.3: AB Code and CtrlRAM Version Edit

### AB Merge: Every Target

For an AB target not already represented by an owner-approved fixture manifest,
provide an exact case under `testdata/golden/owner-handoff/ab-merge/<ic>/inputs/`:

```text
dp-ab.bin
tpa.bin
tpb.bin
expected.bin
provenance.json
```

`provenance.json` must retain original filenames, SHA-256, lengths, source
archive/ticket, output filename, capacity/topology, owner, and approval date.
The submitted DP_AB container owns customer information unless an owner-approved
case explicitly proves a narrower rule. The 2026-07-14 owner-approved fixture
manifest provides this information for one NT51929 case and two NT51950 cases;
it does not replace the unresolved processor evidence or member-specific gates
listed below.

| AB group | Additional required evidence |
| --- | --- |
| NT51919 / NT51929 / NT51932 | An owner-approved fact-scoped AB alias/parity matrix. It must identify each effective member/map/capacity and provide direct output hashes for every reused member. The tracked NT51929 fixture proves its own fixed 512 KiB bytes only; it does not prove NT51919/NT51932. |
| NT51950 | Exact `Combiner.exe` identity, version, SHA-256, adapter/platform/timeout, full invocation trace, exact `map.txt`, and declared read/write ranges. The two tracked expected outputs prove reference parity, but not the declared legacy-Combiner stage. The Combiner, not C#, owns AB header CRC mutation. |
| NT51951 | The same tool/map/trace/output package as NT51950, independently. Do not copy NT51950's map or result merely because the flows appear similar. Its relocation/header facts must be proven by its own case. |

The existing 256 KiB NT51929 `initial code` / `TPFW` / `FlashCode` archive is a
Normal case, not this AB evidence. Existing V2 candidate profiles and reference
scripts also do not replace a product golden or firmware-owner review.

### CtrlRAM Replace: Preserve and Edit

For every IC/IC-number/postbuild branch selected for release, provide:

```text
base.bin
one replacement BIN for each selected CtrlRAM region/group
expected-preserve.bin
expected-edit.bin
combiner-command.txt
combiner-tool.json
provenance.json
```

Record the source and final NVT Backup metadata, requested FW major/sub-version
for the edit case, expected output name, exact Combiner command order, declared
read/write/diff ranges, and output SHA-256 values. `expected-preserve.bin` and
`expected-edit.bin` must use the same base/replacement input set. The edit case
must prove that the source image remains immutable, the staged source changes
before postbuild, and the final version is read through the NVT Backup.

Current high-priority missing CtrlRAM cases include NT51927 single/2-chip/3-chip
and the selected NT51919/NT51929/NT51932/NT51950/NT51951 branches. A real
expected output is required; a successful Combiner exit code is not parity.

### DP and General Replace

| Workflow | Required owner data |
| --- | --- |
| DP Replace beyond approved NT51950/NT51951 migration cases | Base, replacement DP/LD files, expected output, exact partition/atomicity rule, TP/customer ownership, size policy, and output hash. |
| General Replace touching TP/CtrlRAM | Base, each source input, approved target envelope, forbidden/protected ranges, alignment/overlap policy, exact Combiner trace, expected output, and owner approval. |
| General Merge reusable rule | Input set, blank output size/fill byte, explicit mappings, expected output, naming decision, and owner approval before any normal-workflow promotion. |

## v0.9.4: Candidate IC Intake

Candidate intake does not need a product golden. To create a useful candidate,
provide a manifest or read-only source package containing:

- workbook/export/source file with SHA-256 and source ownership;
- sheet/cell or source-line provenance for every IC/map/region/metadata fact;
- declared intended IC ids, modes, capacities, topology choices, and aliases;
- known unsupported/unknown facts and exclusions; and
- one expected candidate report or a precise acceptance list for the generated
  report.

For parser hardening, synthetic malformed fixtures are preferred: duplicate ids,
path escapes, lock files, macro-bearing Office files, missing cells, ambiguous
maps, and conflicting aliases. Intake output remains candidate-only until an
owner reviews and commits it through the trusted bundle path.

## v0.9.5: V2 Convergence and Legacy Retirement

Choose one row from the [Legacy Retirement Matrix](legacy-retirement-matrix.md)
at a time. Before a legacy item can be removed, provide the data that proves
the replacement covers every current consumer:

| Legacy item | Evidence package required |
| --- | --- |
| `BuiltInStandardMergeProfiles` | Independent V2 golden inputs/outputs for every retained family, naming/report parity, and owner approval. |
| Legacy `CompositionProfileCompiler` authority | Per-workflow V2 input/output/report/CLI/UI parity, including invalid cases and every selected processor path. |
| `TpFlashMapCatalog` / `IcMetadataFacade` projections | Source-map provenance and UI/CLI metadata/number-selection parity for every consumer, with no copied firmware semantics. |
| `LegacyCombinerPostbuildCatalog` | V2 processor declaration, exact tool binding, staging/read/write ranges, command trace, real expected output, and firmware-owner review. |

## v0.9.6: Support and Release Consolidation

Before a support or release claim, provide:

- owner-selected IC/workflow support matrix, including explicitly excluded
  capacities, topologies, and aliases;
- final golden/provenance package for every selected workflow;
- firmware-owner approvals for ranges, CRC/header behavior, processor order,
  and output naming;
- final release version, changelog/release notes, package ZIP, manifests,
  SHA-256 sums, SBOM/provenance, third-party notices, and signing/legal result;
- clean Windows x64 smoke record without separate .NET/Python, including app
  startup, catalog/settings load, worker vector, representative Preview/Build,
  report/history, and Combiner readiness; and
- PR/review/CI evidence or the documented local-verification exception.

## Current Highest-Priority Owner Inputs

1. AB product evidence for NT51919, NT51929, NT51932, NT51950, and NT51951 as
   specified above; NT51950/NT51951 additionally require their own Combiner
   sidecar and trace.
2. Real CtrlRAM Replace Preserve/Edit expected outputs and postbuild evidence
   for the release-selected IC/count branches.
3. General Replace safety envelopes and TP/CtrlRAM expected-output cases if
   those workflows are selected for support.
4. A signed-off release support matrix before `v0.9.6`; package/signing data
   is needed only when a release is prepared.

## Do Not Send

- private keys, passwords, tokens, API credentials, or certificates containing
  private keys;
- a firmware output without its source inputs and provenance;
- a `map.txt` or Combiner executable without the matching command trace and
  executable hash;
- a filename-only assertion that two ICs are equivalent; or
- a private BIN intended for Git without an explicit fixture approval and
  manifest decision.
