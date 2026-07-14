# IC Reference Intake Request Contract 1.0

The executable schema is
[`ic-reference-intake-request-v1.schema.json`](ic-reference-intake-request-v1.schema.json).
It defines a deterministic, offline request for staging owner-provided IC
evidence. It is not a runtime profile bundle and cannot register an IC, select
a map, expose a workflow, or promote support.

## Input and output

Run the intake command with a request, a source root containing the declared
files, and a destination path that does not already exist:

```text
python scripts/intake_ic_reference.py --request request.json --source-root <owner-drop-folder> --output-dir <empty-staging-path>
```

The command rejects an absolute path, `..` path segment, duplicate id/path,
lock file, symbolic link/reparse point, changed size/hash, existing output,
or an output directory within this repository. It reads source files only and
never opens an Office workbook, executes a macro, runs a tool, or mutates a
source file.

On success, the destination contains the source snapshots plus:

- `evidence-manifest.json`: a `firmware-evidence-manifest-v1` document with
  `status = candidate` and deterministic intake provenance;
- `intake-report.json`: request scope and staged relative paths; and
- `NEXT_STEPS.md`: human review and promotion gates.

The output evidence manifest deliberately does not contain local source paths
or a runtime bundle reference. Every free-text value copied to candidate output
rejects local-path syntax; `sourceRef` is intake-only and is not emitted.
Promotion requires a separate reviewed commit through the trusted
profile-bundle materialization path.

## Request rules

`sourceArtifacts` declares every file before the command runs. Each item pins:

- a unique `artifactId`;
- `sourceKind` and the original `logicalName`;
- a relative `sourcePath` below `--source-root`; and
- exact `contentHash` and `sizeBytes`.

`logicalName` must equal the basename of `sourcePath`, preserving the original
filename. The tool validates the supplied file before and after copying; a
changed source fails without promoting a partial destination.

`candidateScope` records the owner-selected members, modes, capacities, and
topology choices. It only bounds the proposed evidence. It does not create an
alias or determine an executable topology. Aliases, ranges, metadata layouts,
integrity behavior, and processor claims remain explicit `facts` with source
citations. A candidate fact may remain `unresolved` and must carry its
appropriate promotion blocker.

## Minimal example

```json
{
  "schemaVersion": "1.0",
  "requestId": "nt51951-ab-reference-intake",
  "manifestId": "nt51951-ab-evidence",
  "manifestVersion": "0.1.0",
  "requestedAtUtc": "2026-07-14T00:00:00Z",
  "owner": "firmware-owner",
  "workflow": "reference-only",
  "candidateScope": {
    "memberIds": ["NT51951"],
    "modeIds": ["ab-merge"],
    "capacityBytes": [524288],
    "topologyChoices": ["single"],
    "exclusions": ["No runtime promotion from intake output."]
  },
  "sourceArtifacts": [
    {
      "artifactId": "flashmap-workbook",
      "sourceKind": "workbook",
      "logicalName": "IC_FlashMap_20260714.xlsx",
      "sourcePath": "IC_FlashMap_20260714.xlsx",
      "contentHash": "<64 lowercase SHA-256 hex characters>",
      "sizeBytes": 1
    }
  ],
  "facts": [
    {
      "factId": "nt51951-ab-map-unresolved",
      "subject": { "familyId": "nt51951", "memberId": "NT51951", "modeId": "ab-merge" },
      "factKind": "range",
      "value": { "kind": "statement", "text": "Map review is pending." },
      "disposition": "unresolved",
      "promotionImpact": "blocks-map-resolution",
      "citations": [{ "artifactId": "flashmap-workbook", "location": "DP Perspective" }]
    }
  ],
  "reviews": []
}
```

Replace the illustrative hash and size with the actual values before running
the command. A generated candidate remains evidence intake only; it is never
an authorization to add a materialization allowlist entry, profile
registration, or support-matrix row.
