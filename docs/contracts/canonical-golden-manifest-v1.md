# Canonical Golden Manifest v1.1

## Purpose

The canonical golden inventory is repository test evidence, not a runtime support catalog. It gives
every direct firmware golden one physical location, preserves owner-approved immutable input evidence
without inventing an expected output, and represents owner-approved reuse as an explicit fact-scoped
alias without copying payloads or presenting an alias as a direct product golden.

## Root manifest

`testdata/golden/canonical/manifest.json` declares:

- `schemaVersion`: exactly `1.1` (case manifests remain version `1.0`);
- `payloadClass`: exactly `owner-approved-golden`;
- `binaryPayloadsIncluded`: exactly `true`;
- `diagnosticsRoot`: exactly `testdata/diagnostics/golden-evidence`;
- `cases`: the closed case inventory, each with a globally unique `caseId` and confined
  `manifestPath`; and
- `routeEvidence`: the closed exact-route evidence inventory described below.

The root, every case manifest, and every declared artifact form a closed file inventory. Extra files,
missing files, duplicate declarations, path escapes, symlinked files or directories, and sibling-path
discovery fail validation. Containment is checked on resolved paths before any manifest or payload is
read.

## Exact-route evidence inventory

`routeEvidence` contains exactly one selected evidence declaration for every admitted capability
route. A declaration never admits execution, changes publication, or alters a case or payload. It
only binds the evidence classification to the exact current capability identity. Every declaration
therefore contains these common fields:

- `evidenceId`: a stable identifier, unique across the inventory;
- `kind`: exactly `direct-golden`, `approved-alias`, `synthetic-oracle`, or
  `contract-only`;
- `routeId`: the exact, opaque canonical route id; and
- `capabilityFingerprint`: the exact lowercase 64-hex capability fingerprint.

The pair `(routeId, capabilityFingerprint)` is also unique. A changed fingerprint makes the old
declaration stale; it cannot inherit evidence by retaining the same route id. Kind-specific objects
are closed: undeclared fields fail validation. The validator loads
`docs/contracts/canonical-capability-policy-v1.json` and requires exactly one manifest declaration
for every policy route and no extras. The manifest `evidenceId`, `routeId`,
`capabilityFingerprint`, and `kind` must respectively equal that route's evidence `decisionId`,
route identity, fingerprint, and `value`. Missing, extra, renamed, stale, or reclassified evidence
therefore fails the same structure gate; the two inventories cannot drift independently.

A `direct-golden` declaration adds `caseId` and `testReference`. The case must be a physical direct
golden with its owner expected artifact. It may also add one `expectedView` object containing exactly
`artifactId`, non-negative integer `start`, positive integer `length`, and lowercase `sha256`.
`artifactId` must select the case's expected artifact. The half-open byte view
`[start, start + length)` must remain inside that immutable payload, and `sha256` must equal the hash
of those exact bytes. Omitting `expectedView` means the declaration relies on the case's complete
expected output and its full-output runner; it does not authorize a partial comparison.

An `approved-alias` declaration adds `caseId`, `sourceRouteId`,
`sourceCapabilityFingerprint`, a non-empty duplicate-free `factScopeIds` array, and
`testReference`. `caseId` must select a canonical artifact-free alias case. The exact source identity
must be present as `direct-golden`, and that declaration's `caseId` must match the alias case's
`sourceCaseId`. Alias chains, a missing or stale source fingerprint, and aliasing another fingerprint
of the same route id fail validation. Each `factScopeIds` item uses the closed
`<alias-case-id>:fact-<one-based-index>` form and resolves to the corresponding entry in that alias
case's declared `alias.factScope` array. Unknown indexes, another case's prefix, and duplicates fail
validation. These IDs grant only the named reviewed facts; they never grant whole-family or
whole-workflow equivalence.

A `synthetic-oracle` declaration adds `oracleReference`, `expectedSha256`, and `testReference`.
`oracleReference` is a normalized, confined, existing repository file; `expectedSha256` is the
lowercase 64-hex oracle result pinned by the referenced test. Structure validation does not execute
.NET, but it requires that exact lowercase hash literal to remain present in the referenced test
source; a manifest-only hash edit therefore fails closed. The normal executable test gate remains
responsible for proving that the named test actually produces and compares that value. The
declaration does not turn a synthetic result into owner-supplied expected firmware.

A `contract-only` declaration contains exactly one of `testReference` or `contractReference`.
`testReference` uses `tests/<path>.cs#<test-symbol>` or
`tests/<path>.py#<test-symbol>` and must resolve to an existing symbol. `contractReference` is the
honest fallback when there is no route-specific executable oracle; it is a normalized, confined,
existing repository file with an optional non-empty `#locator`. A contract-only row cannot carry a
case, expected hash, oracle, alias source, or fact scope.

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

The sole owner-certified public-input exception is
`nt51929-certified-metadata-inputs-20260904`. Its artifact declarations omit
`legacyPaths` because the original names and intake archive are prohibited from
Git; the case's owner certification, two exact artifact SHA-256 values, and
exact release allowlist admission are its provenance. No other canonical
artifact may omit `legacyPaths`.

A direct input-evidence case declares `directGolden: false` and `directEvidence: true`. It contains
one or more immutable input artifacts but cannot declare an expected artifact. This represents
owner-approved base, replacement, or processor inputs whose execution facts are useful without
mislabeling a repository-derived output as an owner expected golden.

An alias case declares `directGolden: false`, omits `directEvidence` or declares it as `false`, has no
physical artifacts, and declares a direct `sourceCaseId`, non-empty `factScope`, and non-empty
`evidenceRefs`. Alias chains and cross-workflow aliases are forbidden: the source must be a physical
direct Golden case in the same workflow. The alias directory contains only
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

`testdata/golden/release-canonical-v1.json` schema `1.1` is the v1.1.2 human-gated redistribution authority. It
pins the canonical README by exact-byte `canonicalReadmeSha256` and every selected `caseId`,
case-manifest path and exact-byte `manifestSha256`, workflow, test-disposition kind, alias source, and
logical artifact declaration including role, path, byte size, and SHA-256. Its 2026-09-01 owner
authorization is limited to immutable reference-payload redistribution. It neither rewrites older
case provenance nor promotes runtime support or widens a case's declared parity scope.

The closed selection contains 25 Direct Goldens, one owner-certified direct input-evidence case,
and nine fact-scoped aliases whose exact same-workflow Direct Golden source is also selected: 35
cases, 161 logical artifact declarations, and 158 unique physical paths. The input-only admission
is exactly `nt51929-certified-metadata-inputs-20260904`, with 524288-byte Initial Code SHA-256
`5ccf5802511635dbed73fc8043acb0021ed379568e8479028b640dda5ec2b02a` and FlashCode SHA-256
`69fa975a9883db2494d2c2cf5dce05507573c9a753efb6f62589fa3acded68d4`; both observe the existing
DPCMI CMD1 Page 0 `cmd1-page0 [0x401A,0x401D)` bytes `5F 09 12` (DP `09.01`, Jira `607`). This is
storage evidence only: it is not a Direct Golden relabel, expected output, runtime/profile/support
authority, or full-byte parity assertion. Matching parsed case facts or a self-consistent replacement does not
satisfy the case-manifest exact-byte identity. Eleven direct cases declare full-output comparison and fourteen
declare reviewed allowed-byte differences. The two older direct input-evidence cases and the three aliases
that depend on them remain repository-only evidence; they are not release Goldens. No alias may
source input-only evidence. Diagnostics, owner-handoff, quarantine, retired-IC, CJK14/HackMD
transfer parts, archives, generated, private, and unlisted material is likewise excluded. A canonical
path, a `directEvidence` flag, or a raw BIN alone never admits content to the package.

The allowlist treats its BAT and CONFIG provenance artifacts as inert, hash-pinned reference bytes.
They are not processors, tools, commands, or executable Golden runtime. The packager fails closed if
canonical facts, disposition, alias closure, or artifact identity drift from the independent
allowlist. Changing this authority is an R3 Golden and release/security action and still requires
firmware-owner and release-owner review.

`scripts/canonical_golden_validation.py` is the executable repository validator for this contract.
It uses only the Python standard library and verifies path confinement, exact inventory, hash/size,
direct-case completeness, typed dispositions, reviewed difference ranges, evidence references,
retired active authority, exact release counts, direct-Golden/direct-input-evidence/alias separation,
and self-contained fact-scoped alias integrity. It binds the certified NT51929 bytes to the existing
`DpcmiMetadataContract`; it does not add another offset decoder or selector.
