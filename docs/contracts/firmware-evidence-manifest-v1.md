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

The empty output directory receives only four deterministic JSON records:

- `candidate-evidence-manifest.json` — the input record plus candidate-only intake provenance;
- `candidate-bundle-rows.json` — review rows referring to evidence facts, not a runtime profile bundle;
- `missing-evidence.json` — unbound, unresolved, rejected, and promotion-blocking evidence ids; and
- `validation-report.json` — artifact verification results and the candidate-only scope.

The command binds that directory for the complete validation-and-write interval. Unix writes use
the opened directory descriptor for handle-relative staging and publication; Windows holds an
exclusive temporary lock file that prevents the validated directory from being renamed or replaced.
All four records are serialized before writing, staged with exclusive creation, flushed, and then
published individually with atomic no-clobber filesystem operations. Each unpredictable staged name
remains bound to its open file descriptor through hard-link publication, and the final name must
resolve to that same filesystem identity before success. An error or interruption scans every staged
and published identity, removes only files that still belong to this run, preserves all replacements,
and reports them after the remaining cleanup completes. A competing output is never overwritten or
removed, and a successful command leaves exactly the four records above.

The command rejects approved manifests, output overwrite, path traversal, reparse points, lock files,
unknown or duplicate artifact bindings, and hash or size mismatch. It never copies firmware payloads,
registers an IC/profile, promotes support, changes an allowlist, or infers ranges, CRC/header behavior,
aliases, FWConfig layouts, or executable profile data. Existing runtime routes therefore do not consume
candidate intake output and remain independent of candidate validation failures.
