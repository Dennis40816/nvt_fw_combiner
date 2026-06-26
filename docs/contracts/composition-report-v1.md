# Composition Report Contract 1.0

## Purpose

The report is an immutable audit record for UI, CLI, CI, regression, and release evidence. It explains composition kind, experience, initialization, inputs, normalized mappings, operations, processors, validations, and exact byte mutations.

## Required evidence

- app/profile/composition/experience/mode identity and hashes;
- blank or reference initialization, including the reference hash for Replace;
- input binding ids, sizes, and SHA-256 values without portable absolute paths;
- normalized `explicitMappings`, address-space and region summaries;
- deterministic operation plan and statuses;
- processor authority, purpose, protocol/worker identity, hashes, worker-claimed changes, and host-verified changes;
- mutation traces, validation outcomes, stable issue codes, output hash, and atomic-commit status.

`integrityDisposition` describes the required firmware outcome. `processorAuthority` separately describes what the external process may do. Reports never conflate the two.

Reports may contain sanitized display names but not firmware bytes, secrets, arbitrary environment variables, or portable absolute input paths.

Canonical schema: [`composition-report-v1.schema.json`](composition-report-v1.schema.json).
