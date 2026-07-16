# Golden Fixture Retention and Privacy

Status: Owner-approved repository policy, 2026-07-16.

## Decision

Every accepted golden case must be reproducible from a fresh clone. All input
and expected-output artifacts required to replay that accepted case are stored
under the appropriate tracked `testdata/golden/<workflow>/` fixture and are
anchored by a manifest.

`testdata/golden/owner-handoff/**/intake/` is only a temporary quarantine for
unreviewed incoming evidence. A payload does not become a golden merely because
it was copied into intake. After byte comparison, firmware-owner approval, and
privacy review, every accepted replay artifact is promoted into the tracked
workflow fixture in a separate R3-reviewed change.

## Required manifest record

Every tracked golden artifact records:

- repository-relative path;
- exact byte size and SHA-256;
- IC, workflow, firmware/profile version, and mode/count scope;
- source archive/ticket or other non-personal provenance reference;
- independent reference tool version and SHA-256 where applicable;
- expected output and approved allowed-difference ranges;
- approval authority as a role/team label and approval date; and
- confidentiality classification and any distribution restriction.

Project and product names remain when they are needed to identify technical
provenance. They must not be replaced with fake identifiers merely to satisfy a
generic redaction pass.

## Personal-information exclusion

Tracked fixtures, manifests, filenames, logs, documents, archives, and commit
messages must not contain a person's:

- name, email address, phone number, employee/account id, or username;
- Windows user-profile, home-directory, cloud-drive, or workstation path;
- document author, last-saved-by, comment author, or revision identity metadata;
- personal transfer URL/token or signed/credential-bearing URL; or
- other information that identifies the owner as an individual.

Use stable role labels such as `firmware-owner`, `validation-team`, or
`release-owner`. Preserve technical project identifiers, IC numbers, firmware
versions, original technical filenames, commands, ranges, and hashes.

Office/PDF/archive containers must be inspected for embedded metadata and path
leaks before promotion. When sanitization changes the artifact bytes, the
manifest records the sanitized committed artifact hash and retains the original
source hash only in a non-personal provenance record when required.

## Exclusions

The following are not automatically golden artifacts and are not committed
under this policy:

- external or licensed executables, unless separately approved by the external
  tool packaging and legal policy;
- credentials, signing material, license keys, access tokens, or private URLs;
- unreviewed firmware, generated exploratory output, or unexplained bytes;
- source archives whose contents are not all required to replay the case; and
- logs or documents that still contain personal identifiers.

For an external tool, the golden manifest records the approved tool name,
version, SHA-256, command order, and authority. This does not authorize bundling
the executable.

## Promotion gate

Before a golden fixture commit:

1. classify the case as R3 and keep it on a non-`main` branch;
2. verify immutable input provenance and independent expected-output origin;
3. inspect filenames, text, logs, Office/PDF metadata, archives, and paths for
   personal information;
4. generate and review path/size/SHA-256 manifest entries;
5. run the workflow golden regression and full repository verification;
6. record firmware-owner byte/range/tool approval without personal identity;
7. run Polytail and independent PR review; and
8. promote runtime/support only in a separate explicitly approved decision.

This policy authorizes retaining accepted goldens in the repository. It does
not weaken firmware semantics, legal/tool restrictions, release-package
allowlists, or the firmware-owner gate.
