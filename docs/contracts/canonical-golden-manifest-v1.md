# Canonical Golden Manifest v1

## Purpose

The canonical golden inventory is repository test evidence, not a runtime support catalog. It gives
every direct firmware golden one physical location, preserves owner-approved immutable input evidence
without inventing an expected output, and represents owner-approved reuse as an explicit fact-scoped
alias without copying payloads or presenting an alias as a direct product golden.

## Root manifest

`testdata/golden/canonical/manifest.json` declares:

- `schemaVersion`: exactly `1.0`;
- `payloadClass`: exactly `owner-approved-golden`;
- `binaryPayloadsIncluded`: exactly `true`;
- `diagnosticsRoot`: exactly `testdata/diagnostics/golden-evidence`;
- `cases`: the closed case inventory, each with a globally unique `caseId` and confined
  `manifestPath`.

The root, every case manifest, and every declared artifact form a closed file inventory. Extra files,
missing files, duplicate declarations, path escapes, symlinked files or directories, and sibling-path
discovery fail validation. Containment is checked on resolved paths before any manifest or payload is
read.

## Case path and manifest

Each case manifest is located at:

```text
<IC>/<workflow>/<variant-or-version>/<topology>/<case-id>/provenance/case.json
```

under the canonical root. The manifest facts must reproduce the same path. IC values use `NT519xx`;
workflows are `standard-merge`, `ab-merge`, `dp-replace`, or `ctrlram-replace`; topology is `single`,
`cascade-<count>`, or `topology-unscoped`. Variant and case identifiers are stable lowercase slugs.

A direct golden declares `directGolden: true` and at least one immutable input plus one expected output.
Every artifact declares a case-unique `artifactId`, role, canonical path, non-negative JSON integer
byte size, lowercase SHA-256,
and one or more pre-migration `legacyPaths`. Input, expected, and provenance artifacts stay below
the corresponding case subtree; nested source groups such as `inputs/NF/` remain confined there.
Expected bytes are never regenerated during layout migration.

Every canonical `.bin` filename must itself contain the case IC number (for example `51950`). A
generic basename such as `tp_bin.bin`, `dp-input.bin`, `flash.bin`, or `expected-output.bin` is not
canonical even when its parent directories identify the IC. Existing descriptive owner filenames
may remain when they already identify the same case IC; otherwise the preferred bounded form is
`<ic>-<artifact-id>.bin`. The original supplied filename remains evidence in `originalFileName`
and/or `legacyPaths`; renaming a canonical path never authorizes payload or hash changes.

A direct input-evidence case declares `directGolden: false` and `directEvidence: true`. It contains
one or more immutable input artifacts but cannot declare an expected artifact. This represents
owner-approved base, replacement, or processor inputs whose execution facts are useful without
mislabeling a repository-derived output as an owner expected golden.

An alias case declares `directGolden: false`, omits `directEvidence` or declares it as `false`, has no
physical artifacts, and declares a direct `sourceCaseId`, non-empty `factScope`, and non-empty
`evidenceRefs`. Alias chains and cross-workflow aliases are forbidden: the source must be a physical
direct golden or direct input-evidence case in the same workflow. The alias directory contains only
its provenance manifest, so consumers cannot mistake alias evidence for a second expected BIN.

## Test disposition

Every case declares exactly one case-local `testDisposition`. Test runners parse this as a closed,
typed contract rather than interpreting a free-form status string. `evidenceRefs` is a non-empty,
duplicate-free list of `tests/<path>.cs#<test-symbol>` or `tests/<path>.py#<test-symbol>` references;
each referenced file and symbol must exist.

The only disposition kinds are:

- `direct-full-output`: a direct golden whose runner compares the complete produced output with its
  single expected artifact;
- `allowed-byte-difference`: a direct golden whose runner compares the complete output and permits
  differences only in the case-local object named by `differenceContractProperty`;
- `artifact-integrity-route-blocked`: a direct golden whose artifacts are fully hash-validated but
  whose route cannot run; `routeBlockingEvidenceRefs` must independently prove the typed blocker;
- `input-only-evidence`: a direct input-evidence case with no expected artifact;
- `fact-scoped-alias`: an artifact-free alias whose exact fact binding is tested.

An allowed-difference object declares `addressSpaceId: output-image` and one or more sorted,
non-overlapping, non-empty, half-open ranges using hexadecimal `start` and `endExclusive` values.
Every range has a non-empty `classification` and stays within the expected output size. This contract
authorizes only the named output-byte deviations; it does not weaken artifact SHA-256 checks or turn
a repository-derived output into an owner expected golden.

A derived regression output may be retained as a `provenance` artifact when its owner authorization,
input artifact references, processor trace, allowed changed ranges, and residual claims are recorded
in the same direct case. It remains supporting evidence and never becomes a second `expected` role.

## Diagnostics and release boundary

Diagnostics live outside the canonical root. They cannot be declared as expected artifacts or read by
canonical golden regression tests. Release packaging may include only artifacts selected from this
inventory by an explicit release allowlist; a canonical path alone does not authorize shipment or
support promotion.

The retired active CtrlRAM fixture authority (`ctrlram-replace/manifest.json`, its template,
`fixtures/20260705`, and `fixtures/derived`) must stay absent. Historical `legacyPaths` remain
provenance. The separately indexed `fixtures/20260717` tree remains diagnostic quarantine and is not
an executable golden source.

`testdata/golden/release-standard-merge-v1.json` is the current human-gated release selection. It
pins every selected `caseId`, case-manifest path, `artifactId`, artifact path, byte size, and SHA-256.
The packager fails closed if canonical facts drift from that independent allowlist. Changing the
allowlist is an R3 release/security action and still requires firmware-owner and release review.

`scripts/canonical_golden_validation.py` is the executable repository validator for this contract.
It uses only the Python standard library and verifies path confinement, exact inventory, hash/size,
direct-case completeness, typed dispositions, reviewed difference ranges, evidence references,
retired active authority, and fact-scoped alias integrity.
