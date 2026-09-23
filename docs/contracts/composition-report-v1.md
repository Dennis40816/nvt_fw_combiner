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

### Captured DP source envelope (1.1.10 candidate)

For a Standard Merge compiled from a nonstandard captured DP length, the
Application run report adds optional `SourceEnvelope`. It records
`SourceSlotId`, `RootRegionId`, `LayoutTemplateMapId`,
`LayoutTemplateCapacity`, `ActualOutputLength`, declared
`ExpectedOuterLengths`, and the typed nonblocking
`UnexpectedLengthIssueCode`. These are copied from the exact compiled
provenance; Save never reinterprets the current profile. `ActualOutputLength`
must equal the complete output extent, while the template capacity identifies
only the canonical map supplying layout anchors. Existing `MapId` continues
to identify that canonical map and must not be read as output length.

The property is omitted for exact-map and legacy runs. It contains no input
path, firmware bytes, or separate execution semantics. The focused DTO uses
generated JSON metadata through the existing Application resolver chain. This
additive projection does not change the frozen canonical
`composition-report-v1.schema.json`, existing report read completeness, or
owner Golden evidence requirements.

### Execution-captured AB format (v1.1.6)

`AbMergeFormat` is optional, immutable audit evidence on the Application run
report. The shared AB execution ingress captures its freshly admitted selection
once. An internal projection checks the exact compiled map/family and accepted
input identities, then retains `FormatId`, captured `DisplayName`,
`ConfigurationGeneration`, `ConfigurationSourceSha256`, `FamilyId`,
`FamilyVersion`, and `FamilyContentHash`. `TpA` and `TpB` each contain
`InputBindingId`, `StructureId`, `AddressSpaceId`, `Start`, `EndExclusive`, and
the observed numeric `FormatByte`. The range is the canonical resolved primary
structure range `[Start, EndExclusive)`, not the single format field or an output
write range. The binding id references the parent report's input evidence.

The parent report remains the owner of profile/map/compilation fingerprint and
complete input size/SHA. This extension contains no source/config paths, source
bytes or serialized Domain object graph. It is not another format classifier.
Save/reapply and serialization never reinterpret a running or completed report
using newer settings. Existing run-service success and failure reports carry
the same captured evidence; pre-admission rejection still produces no run
report. Existing Preview-token semantics are unchanged.

For non-AB and policy-absent legacy AB runs, the property is omitted rather than
written as null. Missing evidence in an older report means **not recorded**,
never an inferred Common format; it does not change legacy read completeness.
New summary metadata is source-generated through the existing JSON owner's
resolver chain. The unchanged Application report graph retains its existing
reflection fallback; this is not a whole-report serializer migration.

This additive Application extension does not alter the frozen canonical
`composition-report-v1.schema.json` or firmware output semantics.

### Imported outcome completeness (owner accepted 2026-09-08)

The existing Application JSON owner assesses persisted projection completeness
before a reader displays a run outcome. A recognized legacy projection has
nonempty string `ProfileId`, `IcId`, `ModeId`, `ExperienceId`, `CompositionKind`,
`RunId` and `StartedAtUtc`, plus an explicit `Issues` array of object rows with
string `Code` and `Message`. Missing/null severity retains the existing legacy
code fallback; nonempty unknown severity is not treated as success. Ambiguous
duplicate properties, conflicting `Severity`/`severity` aliases or incorrectly
typed issue evidence remain unknown; equal alias values retain compatibility.
This is not firmware validation, support admission or output certification.

Incomplete/unrecognized objects remain available for read-only Raw inspection
with an explicit neutral `Unknown` outcome, not a fabricated firmware error or
successful run. Original JSON is retained unchanged for export/history. Readers
reassess stored raw data instead of trusting stale cached success metadata.
Malformed JSON retains the existing parse-error behavior. Do not require
`Output`, `ProfileVersion`, `CompletedAtUtc`, `Validations`, `Semantic`, `Replay`
or other later optional fields merely to recognize older projections.
The canonical camelCase schema below is not a validator for this distinct
Application projection; this change does not introduce a canonical renderer.

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
