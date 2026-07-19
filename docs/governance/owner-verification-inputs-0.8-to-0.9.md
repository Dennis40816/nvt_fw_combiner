# Owner Verification Inputs: v0.8.x to v0.9.x

Status: superseded historical checklist for the stable v0.9.9 final intake.

Do not use this document to request more v0.9.9 owner data. The current,
owner-complete authority is:

- `testdata/golden/ctrlram-replace/manifest.20260718.json`;
- `docs/governance/v0.9.9-final-owner-golden-gap-matrix-20260718.md`.

The final intake has no remaining owner-input or owner-decision gate. Any older
"missing" language below is retained only to explain the pre-intake workflow;
remaining parity, tool experiments, route convergence, retirement, code-size,
review, and release verification are agent-owned.

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
- Retain original filenames and record the logical role, source archive/ticket,
  and owner for every supplied file. Repository intake calculates byte length
  and SHA-256, then writes or verifies `provenance.json`; the owner need not
  calculate those values manually.
- For an external Combiner case, provide the executable or approved source
  package, tool version, adapter id, platform, timeout, full command/argument
  trace, input/output mode, and every sidecar required by that exact command
  mode. Repository intake calculates and records the executable SHA-256.
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
| Standard Merge | DP input, TP input, expected flash output, exact output filename, selected capacity, and owner approval. Repository intake records source/output SHA-256. |
| NT51950/NT51951 Standard Merge | One direct DP/TP/expected case for every additional capacity selected for release; state whether customer info remains from DP. Do not derive capacity behavior from a differently sized sample. The tracked direct cases cover NT51950 `0x40000` and NT51951 `0x80000`; the remaining capacities are release-evidence gaps only because V2 direct plan contracts have retired the legacy C# oracle. |
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

For each of `NT51919`, `NT51929`, `NT51932`, `NT51950`, and `NT51951`, provide
an exact AB case under `testdata/golden/owner-handoff/ab-merge/<ic>/inputs/`:

```text
dp-ab.bin
tpa.bin
tpb.bin
expected.bin
provenance.json
```

`provenance.json` must retain original filenames, source archive/ticket, output
filename, capacity/topology, owner, and approval date. Repository intake
calculates and records the SHA-256 and byte lengths from the supplied payloads.
The submitted DP_AB container owns customer information unless an owner-approved
case explicitly proves a narrower rule.

Tracked commit-approved fixtures currently cover NT51929 (`nt51929-ab-t05-d06`)
and NT51950 (`nt51950-ab-boe-d82t80` and `nt51950-ab-hiway-d82t80`) in
the [canonical golden inventory](../../testdata/golden/canonical/manifest.json).
They establish only the named V2/reference parity facts. They do not grant
runtime support or replace the remaining map, command-trace, alias, or
firmware-owner review requirements below.

| AB group | Additional required evidence |
| --- | --- |
| NT51919 / NT51929 / NT51932 | Closed for duplicate-product intake by the 2026-07-18 owner-approved NT51929 fact scope. Keep the member/map/capacity declaration and independent NT51932 named-configuration test; firmware-owner promotion review remains. |
| NT51950 | The tracked candidate already pins the source-verified `Combiner.exe` 1.13.0 binding, command, tool hash, and declared writes. Supply direct inputs, expected output, provenance, and owner approval for each release case; no `map.txt` is used. Submit command/tool sidecars only when the owner case differs from the pinned binding. Combiner, not C#, owns AB header CRC mutation. |
| NT51951 | Closed for duplicate-product AB intake by the 2026-07-18 owner-approved NT51950 workflow-logic scope. Keep the distinct `0x80000` Python/Combiner synthetic test. No `map.txt` is used, C# does not write header CRC, and firmware-owner promotion review remains. |

The existing 256 KiB NT51929 `initial code` / `TPFW` / `FlashCode` archive is a
Normal case, not this AB evidence. Existing V2 candidate profiles and reference
scripts also do not replace a product golden or firmware-owner review.

### CtrlRAM Replace: Preserve and Edit

The checked-in Combiner source and postbuild references already support the
current candidate command wiring. The following package is needed only to
promote a selected IC/IC-number/postbuild branch to a release support claim;
it is not a request for another Combiner source copy.

For every IC/IC-number/postbuild branch selected for release, provide:

```text
one replacement BIN for each selected CtrlRAM region/group
expected-preserve.bin
expected-edit.bin
combiner-command.txt
combiner-tool.json
provenance.json
```

The official owner system is not expected to export a separate pre-replacement
`base.bin`. The expected output is the authoritative firmware artifact. Intake
may create a temporary expected-derived sentinel work image for range/processor
parity, but that method cannot independently prove base selection or unchanged
bytes and still requires the R3 decisions recorded in the
[v0.9.9 owner CtrlRAM evidence audit](v0.9.9-owner-ctrlram-evidence-audit.md).

Record the source and final NVT Backup metadata that is available, requested FW
major/sub-version for the edit case, expected output name, exact Combiner
command order, and declared read/write/diff ranges. Repository intake
calculates output SHA-256 values. `expected-preserve.bin` and
`expected-edit.bin` must use the same base/replacement input set. The edit case
must prove that the source image remains immutable, the staged source changes
before postbuild, and the final version is read through the NVT Backup.

Historical pre-final-intake requests in this section are superseded. The final
manifest accepts the corrected NT51951 single and the exact NT51930 INX,
NT51931, NT51932, and NT51926 2.0.0 cases. NT51950 cascade has no product case
and NT51951 cascade has no project, so both are v0.9.9 release-scope exclusions,
not missing owner evidence. Existing approved aliases and NT51927 evidence also
must not be re-requested. A successful Combiner exit code still is not parity;
the repository must now prove the supplied expected bytes.

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
| Standard Merge V2 registrations | Additional capacity golden inputs/outputs, naming/report parity, and owner approval for any release-support expansion. Legacy C# profiles are not retained as a runtime or test oracle. |
| Legacy `CompositionProfileCompiler` authority | Per-workflow V2 input/output/report/CLI/UI parity, including invalid cases and every selected processor path. |
| Config-backed flash-map query | Source-map provenance and UI/CLI map parity for every consumer, with no copied firmware semantics. Static C# region/base-shape authority and the forwarding `IcMetadataFacade` are retired. |
| Hash-pinned CtrlRAM postbuild profile data | Exact tool binding, staging/read/write ranges, command trace, real expected output, and firmware-owner review before any support promotion. Static C# command authority is retired. |

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

## Current Owner Input Status

There are no remaining owner inputs for stable v0.9.9. Do not request another
golden package, corrected single/cascade output, InsertSID script, Combiner 1.13
output, or DiffNFMerge package. Use the final manifest and close the remaining
hash/command/full-byte parity, exact V2 route, caller-zero retirement, code-size,
R2/R3 review, structure, and `verify --all` gates in the repository.

## Do Not Send

- private keys, passwords, tokens, API credentials, or certificates containing
  private keys;
- a firmware output without its source inputs and provenance;
- an unbound `map.txt` or Combiner executable that does not identify the
  matching command trace and executable hash;
- a filename-only assertion that two ICs are equivalent; or
- a private BIN intended for Git without an explicit fixture approval and
  manifest decision.
