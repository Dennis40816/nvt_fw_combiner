# ADR 0067: Synchronize one certified input-only canonical evidence case into the reviewed release snapshot

- Status: Accepted by the repository owner on 2026-09-04
- Date: 2026-09-04
- Owners: Repository owner, firmware evidence owner, release owner
- Risk: R3 Golden evidence and release redistribution
- Builds on: ADR 0033 and ADR 0038; neither historical decision is modified

## Context

The owner certified two independent NT51929, 524288-byte firmware observations
for public Git and stable-release-package redistribution. Both contain the
existing DPCMI CMD1 Page 0 fact at `cmd1-page0 [0x401A,0x401D)`: bytes
`5F 09 12`, decoded by `DpcmiMetadataContract` as DP major `09`, minor `01`,
and Jira `607`. The Initial Code observation SHA-256 is
`5ccf5802511635dbed73fc8043acb0021ed379568e8479028b640dda5ec2b02a`; the
FlashCode observation SHA-256 is
`69fa975a9883db2494d2c2cf5dce05507573c9a753efb6f62589fa3acded68d4`.

The compact bytes at `[0x66,0x69)` are legacy evidence only. These files are
independent observations, not a Standard/AB input-output pair, expected output,
Direct Golden, full-byte parity claim, support promotion, or runtime semantics.

## Decision

Extend the existing canonical evidence validator and the existing versioned
release-canonical allowlist. The sole case is
`nt51929-certified-metadata-inputs-20260904`; it has exactly two neutral-named
role=`input` artifacts and `input-only-evidence` disposition. It references the
existing DPCMI contract without adding a decoder, selector, profile, route, or
support path.

The schema-1.1 `release-canonical-v1.json` is the sole redistribution
admission. A stable package is exactly its reviewed snapshot, not a scan of the
canonical tree or every `directEvidence` case. Packaging and protected smoke
use one explicit three-way branch: Direct Golden, selected direct input
evidence, or alias. Aliases may source only selected same-workflow Direct
Goldens, never input-only evidence.

The two raw BINs remain individually hash-pinned entries of the existing outer
release ZIP. There is no nested ZIP/7z, extractor, dependency, raw-file
bypass, parallel manifest, Direct-Golden relabel, or fabricated expected
artifact. `goldenFixture` remains an inert release-manifest storage role only.
Evidence, execution, publication, and support remain separate authorities.

## Consequences

- v1.1.2 selects 25 Direct Goldens, one owner-certified input-only evidence
  case, and nine aliases; older input-only cases and their dependent aliases
  remain repository-only.
- The package manifest, SBOM, provenance, and checksums include only the exact
  allowlisted files, including the two neutral raw BINs.
- Archive/transfer parts, original client names, CJK14/HackMD material,
  diagnostics, private/generated material, and unlisted content remain absent.
- Firmware profiles, byte execution, support policy, output naming, and ADR
  0033/0038 remain unchanged.

## Verification and gates

- Validate case size/SHA-256, explicit input-only classification, two input
  roles, no expected/provenance/executable/alias declaration, and the existing
  DPCMI contract-bound range/bytes.
- Validate exact allowlist identity and closed package/smoke projection,
  including the three-way branch and outer-ZIP raw-entry behavior.
- R3 finalization still requires independent exact-head Golden/release review
  and owner attestations; this ADR does not authorize publication, tagging, or
  support promotion.
