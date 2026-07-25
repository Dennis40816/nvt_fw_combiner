# ADR 0002: Use a constrained external Python worker for CRC and approved header transforms

- Status: Accepted for the constrained Python worker; legacy Combiner production transforms remain governed by ADR 0006 and ADR 0015
- Date: 2026-06-25
- Last amended: 2026-06-25
- Owners: Product owner + firmware reviewer + security reviewer
- Related: ADR 0003, CRC Protocol 1.x, Transform Protocol 2.x draft

## Context

The uploaded AB combiner calculates CRC-32/MPEG-2 and, for NT51950/NT51951, writes the calculated value into TPB after address relocation. Future header completion may require Python to update multiple fields. A calculation-only worker is insufficient if the authoritative Python implementation must rewrite the BIN.

At the same time, allowing Python to open the user's original firmware or final output would bypass declared address spaces, mutation trace, atomic output, and write-range policy.

## Decision

Use one bundled one-shot Python worker executable with two versioned authority levels:

1. **Protocol 1.x — Calculate**: the host sends exact bytes; the worker returns a CRC result and performs no filesystem mutation.
2. **Protocol 2.x — Transform**: the host creates an isolated staging directory and writes an exact `work.bin` copy. The worker may mutate only that relative staging file using an approved `processorId`.

The host remains the final mutation-policy authority. For transform mode it records the original bytes/hash, independently computes the resulting byte diff, and rejects any changed byte outside profile-declared `allowedWriteRanges`. The transformed bytes are imported into a named work buffer or output only after all checks pass. User inputs and the final output path remain immutable until atomic promotion.

## Applicability

CRC/header behavior is declared per profile stage, not by `needsCrc: bool`. Two orthogonal fields are required:

```text
integrityDisposition = none | verify-existing | recalculate-and-write
processorAuthority   = calculate | transform
```

`processorPurpose` records checksum, header, header-and-integrity, relocation, or another approved post-process category. Evidence inventories may use `unknown`; supported profiles may not. Protocol 1 implements `calculate` authority; Protocol 2 implements constrained `transform` authority.

## Security controls

- executable path comes from the signed installation manifest;
- no shell, network, child process, plugin, or user-provided command;
- staging working directory is host-owned and short-lived;
- worker request uses one plain relative filename; absolute/traversal/symlink/junction/reparse paths are rejected;
- bounded stdin/stdout/stderr, timeout, process-tree kill, and environment allowlist;
- host validates file names/count, length, before/after hashes, independent changed ranges, and postconditions;
- any failure discards the staging directory and leaves original/output unchanged.

## Current algorithm evidence

```text
id          crc-32-mpeg-2
width       32
poly        0x04C11DB7
init        0xFFFFFFFF
refin       false
refout      false
xorout      0x00000000
check       0x0376E6E7 for ASCII "123456789"
```

Current NT51950/NT51951 evidence uses read `[0xA100,0xA130)` and little-endian write `[0xA130,0xA134)`, with TPB relocation preceding CRC calculation. Exact future header processor parameters remain open until the owner supplies the invocation and field rules.

## Rejected options

- Inline C# only: conflicts with the external-Python requirement.
- Python writes the original/final BIN: excessive authority and non-atomic failure.
- Passing arbitrary user paths or command strings: traversal and process-injection risk.
- Separate worker per IC: duplicates packaging/protocol logic.
- Workflow-specific direct Python calls from UI: violates architecture and traceability.

## Consequences

### Positive

- Python can perform authoritative CRC/header updates without risking original files.
- IC-specific behavior stays in versioned profile/processor declarations.
- C# independently verifies every changed byte.
- One release executable can support calculate and transform contracts.

### Trade-offs

- Transform mode requires staging filesystem and cross-language contract tests.
- File-diff verification adds one extra copy/read, acceptable for expected image sizes.
- Exact processor parameters cannot be finalized before firmware-owner evidence.

## Verification

- pure CRC vectors and current NT51950 values;
- protocol/schema negative cases;
- traversal/symlink/reparse and extra-file tests;
- out-of-range single-byte mutation test;
- timeout/crash atomicity;
- C#/Python contract tests;
- clean Windows package without system Python.
