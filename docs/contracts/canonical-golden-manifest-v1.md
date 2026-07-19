# Canonical Golden Manifest v1

## Purpose

The canonical golden inventory is repository test evidence, not a runtime support catalog. It gives
every direct firmware golden one physical location and represents owner-approved reuse as an explicit
fact-scoped alias without copying payloads or presenting an alias as a direct product golden.

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

A direct case declares `directGolden: true` and at least one immutable input plus one expected output.
Every artifact declares a case-unique `artifactId`, role, canonical path, non-negative JSON integer
byte size, lowercase SHA-256,
and one or more pre-migration `legacyPaths`. Input, expected, and provenance artifacts stay below
the corresponding case subtree; nested source groups such as `inputs/NF/` remain confined there.
Expected bytes are never regenerated during layout migration.

An alias case declares `directGolden: false`, has no physical artifacts, and declares a direct
`sourceCaseId`, non-empty `factScope`, and non-empty `evidenceRefs`. Alias chains are forbidden: the
source must be a direct case. The alias directory contains only its provenance manifest, so consumers
cannot mistake alias evidence for a second expected BIN.

## Diagnostics and release boundary

Diagnostics live outside the canonical root. They cannot be declared as expected artifacts or read by
canonical golden regression tests. Release packaging may include only artifacts selected from this
inventory by an explicit release allowlist; a canonical path alone does not authorize shipment or
support promotion.

`scripts/canonical_golden_validation.py` is the executable repository validator for this contract.
It uses only the Python standard library and verifies path confinement, exact inventory, hash/size,
direct-case completeness, and fact-scoped alias integrity.
