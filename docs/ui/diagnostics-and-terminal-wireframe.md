# Diagnostics and Terminal Wireframe Plan

`0.1.1` reserves a diagnostics UI before real processors and firmware flows are implemented.

## Diagnostics page layout

```text
Diagnostics
  Run summary card
  Validation issue list
  External processor readiness
  Read-only terminal transcript
  Structured report preview
  Copy diagnostics action
```

## Run summary card

Synthetic fields for the demo:

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

Allowed demo rows:

```text
[info] Loaded candidate profile: demo-standard-merge
[info] Compiled 6 synthetic operations
[warn] Build disabled until Composition Core milestone
[info] External tool binding legacy-combiner-1.10 is declared but not executable in demo
```

Forbidden content:

- raw firmware bytes;
- full user paths unless sanitized;
- arbitrary command input;
- secrets, tokens, signing material;
- unbounded stdout/stderr.

## External processor readiness card

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

In `0.1.1`, this may be static synthetic data. Real execution starts in later milestones.

## Structured report preview

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

The report preview helps validate UX without doing real composition.

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
