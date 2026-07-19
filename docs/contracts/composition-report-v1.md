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
- issue `severity` values are `info`, `warning`, or `error`; warnings are non-blocking diagnostics that still require audit before treating output as final evidence.

`integrityDisposition` describes the required firmware outcome. `processorAuthority` separately describes what the external process may do. Reports never conflate the two.

Reports may contain sanitized display names but not firmware bytes, secrets, arbitrary environment variables, or portable absolute input paths.

Canonical schema: [`composition-report-v1.schema.json`](composition-report-v1.schema.json).

## Workbench Report Semantic Extension

`CompositionRunReport` is an Application/workbench projection and is not the canonical `composition-report-v1` wire model. Its Replace-only `OutputDifferences[]` rows may carry an optional `Semantic` object with category, field/section subject, and plain-language explanation. This allows a report renderer to show `TP Flash Header` / `DLM CRC 0` without calculating firmware meaning from an address.

`Validations[]` is another optional projection field. Each immutable row contains `RuleId`, `Stage`, `Status`, `Severity`, and the requirement's declared or emitted `IssueCode`. `Passed` and `Failed` mean the rule evaluated against the completed image; an `Error`-severity failure blocks publication. `Skipped` means an earlier stage produced no image suitable for that rule, so it neither passes nor fails. Older report JSON that lacks `Semantic` or `Validations` is interpreted as an empty collection for the absent field.

These extensions do not add properties to [`composition-report-v1.schema.json`](composition-report-v1.schema.json). See [TP Header Semantic Catalog](../architecture/tp-binary-model-catalog.md) and [ADR 0013](../adr/0013-tp-binary-model-and-report-semantic-projection.md).
