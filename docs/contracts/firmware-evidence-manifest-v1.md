# Firmware Evidence Manifest Contract 1.0

The executable schema is
[`firmware-evidence-manifest-v1.schema.json`](firmware-evidence-manifest-v1.schema.json).
It records immutable source artifacts, independently reviewable firmware facts, and
promotion-relevant blocker evidence without placing private firmware payloads in Git.

## Fact and promotion separation

`facts[].disposition` answers whether one assertion was observed, accepted, rejected, or remains
unresolved. `facts[].promotionImpact` records the consequence of uncertainty. It does not promote a
map or profile. Reviews accept or reject facts only.

An approved fact still cannot grant workflow execution. Family capability rows are technical facts;
only a matching composition profile owns promotion stage and blocker references. The compiler
derives eligibility from that profile plus resolved evidence.

## Source handling

Every cited workbook, source snapshot, firmware artifact, issue export, document, or owner record is
identified by logical name, exact byte size, and SHA-256. A repository path is optional and must not
point outside the repository. Private files remain in the owner's evidence store; the manifest keeps
only provenance, hashes, and precise locations such as workbook sheet/cell or source line.

Offline workbook intake may add `intakeProvenance` only to candidate output. The intake tool writes
to a caller-selected empty staging directory, opens Office files read-only, does not execute macros,
and cannot edit approved contracts, profiles, bundles, or evidence. Candidate output has no runtime
authority until reviewed and committed as an approved manifest in a trusted bundle.

## Candidate intake command

`scripts/create_candidate_ic_intake.py` is the `v0.9.4` standard input/output boundary. It consumes
one `status: candidate` evidence manifest plus only the source artifacts explicitly bound with
`--artifact ARTIFACT_ID=RELATIVE_PATH`. The relative paths are resolved below a caller-provided
`--source-root`; the command never scans the directory or discovers evidence by filename.

```text
python scripts/create_candidate_ic_intake.py \
  --evidence-manifest <candidate-evidence.json> \
  --source-root <owner-evidence-root> \
  --artifact <artifact-id=relative-path> \
  --output <existing-empty-staging-directory> \
  --generated-at <UTC-ISO-8601-ending-in-Z>
```

Every bound artifact must be a regular non-reparse file, must not be an Office lock file, and must
match the manifest's exact byte size and SHA-256. The command reads all files as bytes only; it does
not automate Office or execute macros. It validates the candidate-intake structural subset only; the
existing V2 materializer/loader remains the sole trusted full-schema validator. Unbound manifest
artifacts remain explicitly listed as missing evidence rather than being guessed or searched for.
The manifest and every bound artifact retain the regular-file identity captured before open; the
opened descriptor and the post-open path must both match that identity before any bytes are accepted.
Unix opens each parent component without following links and opens the leaf relative to that bound
directory chain. Windows snapshots every parent identity before and after the leaf open and rejects
any change.

The empty output directory receives only four deterministic JSON records:

- `candidate-evidence-manifest.json` — the input record plus candidate-only intake provenance;
- `candidate-bundle-rows.json` — review rows referring to evidence facts, not a runtime profile bundle;
- `missing-evidence.json` — unbound, unresolved, rejected, and promotion-blocking evidence ids; and
- `validation-report.json` — artifact verification results and the candidate-only scope.

The command binds that directory for the complete validation-and-write interval. Windows holds an
exclusive temporary lock file that prevents the validated directory from being renamed or replaced.
Unix keeps the caller's identity-bound destination empty while it builds the complete record set in
an unpredictable private sibling directory opened relative to the destination parent. It validates
the original destination identity and emptiness again before replacing that directory with the
complete private directory in one atomic operation. A public entry added before that commit blocks
publication and is preserved; no candidate record is visible at the public path before the commit.

All four records are serialized before writing, staged with exclusive creation, and flushed. Each
unpredictable staged name remains bound to its open file descriptor through hard-link publication,
and the final name must resolve to that same filesystem identity before success. The descriptor
content must also match the pre-serialized byte length and SHA-256 before linking, after linking, and
at final validation, and each final name is rechecked after its descriptor content is read. Windows
publishes the four final names individually inside the locked destination. Unix performs those same
checks inside the private sibling, flushes its directory descriptor, verifies exact four-record
membership, and then performs the single directory commit. On Windows, the lock name must still
identify the original exclusive temporary lock before final validation; closing that handle performs
the operating-system-owned cleanup.

An error or interruption keeps every run-owned output descriptor open while cleanup compares each
live file identity's link count with its tracked staged and published names. Any additional hard
link, including a lock-named or nested-directory link, blocks path-based cleanup and is reported
without releasing the tracked identity anchors; cleanup never guesses which concurrently writable
name is safe to unlink. After tracked-name removal, the still-open descriptors are checked again so
a link added during cleanup is reported before their identities can be reused. Cleanup preserves
unrelated replacements and reports failures only after the remaining safe cleanup completes. On
Unix this cleanup is confined to the private sibling before directory commit; an unowned entry there
is preserved with that private directory and reported instead of being deleted. A successful command
leaves exactly the four records above. These are point-in-time guarantees through the final boundary
validation; after the command closes its boundary handles, the caller owns and must protect the
resulting directory from later mutation.

The command rejects approved manifests, output overwrite, path traversal, reparse points, lock files,
unknown or duplicate artifact bindings, and hash or size mismatch. It never copies firmware payloads,
registers an IC/profile, promotes support, changes an allowlist, or infers ranges, CRC/header behavior,
aliases, FWConfig layouts, or executable profile data. Existing runtime routes therefore do not consume
candidate intake output and remain independent of candidate validation failures.
