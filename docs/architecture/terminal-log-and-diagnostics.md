# Terminal, Log System, and Diagnostics Plan

This document reserves common engineering UX that is required before the firmware workflows become complicated.

## Goals

- Give users and engineers a clear view of what happened during Preview and Build.
- Keep human logs separate from machine-readable reports.
- Avoid hiding external combiner execution details.
- Make support/debugging possible without firmware samples.

## Surfaces

| Surface | Audience | Purpose |
| --- | --- | --- |
| User activity log | Normal users | High-level operations, validation issues, output summary. |
| Technical diagnostics panel | Engineers/support | Processor/tool id, staging status, hashes, changed ranges, timing. |
| Terminal pane | Advanced users/Codex/dev | Read-only command transcript for CLI/process calls where safe. |
| Structured report JSON | CI, golden regression, support | Machine-readable deterministic evidence. |
| Application log file | Support | Rotating local logs with redaction policy. |

## Terminal pane rules

- Terminal pane is read-only for firmware operations.
- It must not become an arbitrary shell.
- It may show sanitized command arguments for approved external processors.
- It must redact absolute user paths where possible.
- It must show tool binding id, executable version, exit code, duration, and result status.
- It must not print firmware bytes or large binary dumps.

## Logging model

Recommended event levels:

```text
Trace
Debug
Information
Warning
Error
Critical
```

Recommended event categories:

```text
Profile
Input
Plan
Operation
ExternalProcessor
Integrity
Output
Ui
Settings
Release
```

Every build run should have a stable `runId` that links:

- UI state;
- operation trace;
- external combiner invocation;
- mutation report;
- output file hash;
- user-visible log;
- diagnostic log.

## Structured report requirements

CLI Preview/Build commands may write the current application run report with `--report <path>`. This JSON is the machine-readable audit output used by CMD workflows and can later back the UI report modal/history view. It is not a replacement for the canonical `composition-report-v1` contract until that wire contract is promoted.

A build/preview report must include:

- product/app version;
- profile id/version;
- IC/mode/experience;
- input artifact metadata and hashes;
- ordered operations;
- validation issues;
- processor/tool invocations;
- changed ranges;
- output name and hash;
- elapsed timings;
- result status.

## External combiner logging

For legacy `combiner.exe` calls, report:

```text
toolBindingId
toolVersion
processorId
adapterId
executableSha256
resolved executable path
staging working directory
one completed ProcessStartInfo.ArgumentList record per invocation
staging input length
beforeSha256
afterSha256
observed changed ranges
allowed write ranges
exit code
timeout status
stderr/stdout summary
```

The report must not rely on the combiner's own console text as authority. Host-side diff is authoritative.

## 0.1.1 UI planning

The initial UI design document should include:

- where logs appear during Preview;
- where logs appear during Build;
- where report JSON is saved;
- whether terminal pane is visible by default or behind an advanced toggle;
- how failures from external combiner tools are explained to users;
- how to copy diagnostics without sharing firmware bytes.
