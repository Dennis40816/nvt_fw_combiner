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

The canonical `composition-report-v1` document may contain sanitized display
names but not firmware bytes, secrets, arbitrary environment variables, or
portable absolute input paths. The separately versioned Application projection
has the narrow Replace-only replay exception below; it does not alter the
canonical schema or permit complete-BIN persistence.

Canonical schema: [`composition-report-v1.schema.json`](composition-report-v1.schema.json).

## Application Run Report Semantic Extension

`CompositionRunReport` is an Application projection and is not the canonical
`composition-report-v1` wire model. The 2026-08-09 complete-retirement decision
removes the CLR Workbench envelope and in-process JSON round trip; UI and CLI
consume this typed Application result directly. Persisted report JSON remains
wire-compatible and older documents remain readable. Its Replace-only
`OutputDifferences[]` rows may carry an optional `Semantic` object with
category, field/section subject, and plain-language explanation. This allows a
report renderer to show `TP Flash Header` / `DLM CRC 0` without calculating
firmware meaning from an address.

An `OutputDifferences[]` row may also carry an optional `Replay` object for the
read-only Report Diff viewport. `Replay.Range` covers the complete changed
range plus at most two aligned 16-byte context rows before and after, clipped
to the declared output bounds. `BeforeBytes` and `AfterBytes` are Base64 byte
planes for exactly that range; `BeforeSha256` and `AfterSha256` bind each full
replay plane. The row's existing `BeforeSha256` and `AfterSha256` continue to
bind the changed range itself. A reader must validate both layers before
showing replay bytes. Missing hashes, invalid Base64, range/length mismatch, a
changed-range hash mismatch, or a full replay-plane hash mismatch makes Diff
preview unavailable.

Replay is local firmware-bearing report data. It inherits the report store's
access, retention, deletion, and export boundary and must not be treated as a
sanitized support attachment. It never persists the complete BIN merely for
viewport navigation: when the aligned replay envelope would cover the complete
artifact, `Replay` is omitted and Diff preview is explicitly unavailable. It
never rereads source paths. Older persisted reports without `Replay`, including
legacy rows with only truncated Hex previews, remain readable but report that
Diff preview is unavailable.

Readers accept `Replay` only when its range is the unique clipped, aligned
two-row context envelope for the declared output size and changed range. The
two replay-plane hashes, changed-range hashes, and observed changed-byte count
must match the row; otherwise Diff preview fails closed without fabricating
bytes.

For an explicit General Replace Preview blocked by unavailable required
POSTBUILD, that Application projection may instead carry `DiagnosticPreview`,
but only after the exact canonical route has produced the one accepted
`CompiledComposition` and its compilation-bound readiness snapshot.
`Mode = diagnostic-plan-only`, `OutputProduced = false`, and
`ClaimsFinalIntegrity = false` distinguish it from executable Preview. It
retains the shared Build-readiness blocker, accepted mapping operations,
projected complete `Kept`/`Changed` coverage, and the compiled required stage
id when one exists. Its `Output` projection is `null`, so no filename
or empty-content SHA-256 can be mistaken for a produced artifact. The canonical
report-v1 schema is unchanged.
A disabled General Replace Build action returns only the shared typed readiness
snapshot; it creates neither `CompositionRunReport` nor report JSON/file. Only
an explicit coherent, exactly compiled Preview may create the diagnostic
plan-only report. Route-unavailable targets fail admission before runtime
inspection and create no report.

`Validations[]` is another optional projection field. Each immutable row contains `RuleId`, `Stage`, `Status`, `Severity`, and the requirement's declared or emitted `IssueCode`. `Passed` and `Failed` mean the rule evaluated against the completed image; an `Error`-severity failure blocks publication. `Skipped` means an earlier stage produced no image suitable for that rule, so it neither passes nor fails. Older report JSON that lacks `Semantic` or `Validations` is interpreted as an empty collection for the absent field.

These extensions do not add properties to [`composition-report-v1.schema.json`](composition-report-v1.schema.json). See [TP Header Semantic Catalog](../architecture/tp-binary-model-catalog.md), [ADR 0013](../adr/0013-tp-binary-model-and-report-semantic-projection.md), and [ADR 0028](../adr/0028-capability-driven-shared-hex-viewport.md).
