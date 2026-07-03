# Report Modal and Diagnostics Wireframe Plan

The report and diagnostics UI is production-backed. Diagnostics is not a top-level page; Preview and Build open a report modal that contains evidence, sanitized logs, and diagnostics from application run results.

## Report modal layout

```text
Preview/Build report modal
  Run summary
  Validation issue list
  External processor readiness
  Read-only terminal transcript
  Structured report preview
  Copy diagnostics action
```

## Run summary

Application-backed fields:

```text
runId
profileId
experience
supportStatus
inputCount
operationCount
processorCount
resultStatus
```

## Terminal transcript

The terminal pane is not a shell. It is a read-only transcript for approved commands and host-generated diagnostics.

Allowed transcript rows:

```text
[info] Loaded candidate profile: nfc.nt51950.standard-merge
[info] Compiled 6 operations
[warn] Build disabled until a valid Preview token exists
[info] External tool binding legacy-combiner-1.13.0 is declared
```

Forbidden content:

- raw firmware bytes;
- full user paths unless sanitized;
- arbitrary command input;
- secrets, tokens, signing material;
- unbounded stdout/stderr.

## External processor readiness

Show these fields when a processor is declared:

```text
processorId
authority: calculate | transform
purpose
toolBindingId
toolVersion
manifest status
executable hash status
allowedReadRanges count
allowedWriteRanges count
```

These fields must come from application catalogs, tool manifests, or run results.

## Structured report preview

The UI must not show raw JSON as the primary experience. A loaded report JSON should render as:

```text
Loaded report
  summary: run, profile, IC, status, output hash
  inputs: address space, size, short hash
  operations: sequence, source/target ranges, processor/tool ids
  issues: stable issue codes and messages
  mutations: changed range, changed byte count, before/after hash
```

Show a tree/table version of the report shape:

```text
Report
  identity
  inputs
  operations
  processorInvocations
  mutations
  output
  issues
```

The report preview helps audit real or loaded composition results. Settings may link to report export or diagnostics configuration, but the run-specific evidence remains in the Preview/Build report modal.

## Copy diagnostics rule

The Copy Diagnostics action must be designed to redact:

- firmware bytes;
- absolute user directories where possible;
- private golden file names if sensitive;
- environment variables;
- access tokens;
- signing paths.

## Failure explanation style

External processor errors should use user-readable messages backed by stable issue codes:

```text
NFC_TOOL_HASH_MISMATCH
NFC_TOOL_TIMEOUT
NFC_TOOL_OUT_OF_RANGE_MUTATION
NFC_TOOL_LENGTH_CHANGED
NFC_TOOL_UNKNOWN_BINDING
```

Do not display only raw process exit text.
