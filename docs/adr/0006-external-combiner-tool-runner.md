# ADR 0006: External Combiner Tool Runner for CRC/Header Processing

Status: Accepted for `0.1.0` planning baseline

Date: 2026-06-26

## Context

The NFC baseline originally reserved a Python CRC/header worker path. The owner clarified that production CRC/header work will depend on multiple legacy `combiner.exe` versions, for example `1.9` and `1.10`, and that different IC/mode/stage combinations may require different executable versions.

Therefore, CRC/header processing must not be implemented as one hard-coded Python algorithm container. NFC needs a versioned external tool runner that can safely materialize a temporary firmware image, invoke the approved combiner executable, and import only validated byte changes back into the composition pipeline.

## Decision

NFC will model legacy `combiner.exe` invocations as `run-external-processor` operations backed by an **External Combiner Tool Runner**.

The runner is part of `NvtFwCombiner.Infrastructure`. It implements the application external processor port and is selected by processor/tool metadata declared by the profile and tool manifest.

A production profile must not contain an executable path. A profile may reference only a logical tool binding such as:

```json
{
  "toolId": "legacy-combiner",
  "toolVersion": "1.10",
  "toolBindingId": "legacy-combiner-1.10"
}
```

The exact tool path, executable file name, SHA-256, argument template, input/output mode, timeout, and platform are supplied by an external combiner tool manifest.

`toolVersion` is always a string. It must never be parsed as a floating-point number. `1.10` and `1.9` are exact version tokens, not numeric values.

## Runtime authority model

For every external combiner transform:

1. Application reaches a `run-external-processor` operation.
2. Infrastructure creates a private staging directory for the run.
3. Infrastructure materializes the selected address space, usually `output-image` or `work-buffer`, as `work.bin`.
4. Infrastructure computes the before SHA-256 and records the expected file length.
5. Infrastructure resolves the exact `combiner.exe` from the manifest and verifies its SHA-256 before execution.
6. Infrastructure expands only approved staging tokens such as `{staging.workBin}` and `{staging.outputBin}`.
7. Infrastructure starts the process with `UseShellExecute = false`; no shell command string is assembled.
8. The combiner may mutate only files inside the staging directory.
9. Infrastructure reads back `work.bin` or the declared output file.
10. Infrastructure verifies file count, expected names, unchanged length, after SHA-256, and byte diff.
11. Application accepts the result only if every changed byte is inside the profile-declared `allowedWriteRanges` and all postconditions pass.
12. The validated bytes are imported into the current work buffer/output image.
13. Any timeout, crash, malformed output, path escape, unexpected file, length change, SHA mismatch, or out-of-range mutation fails closed.

The temporary firmware file is an implementation detail of the host. Profiles should name address spaces and ranges, not host filesystem paths.

## Profile expression

The current `composition-profile-v1` contract already has `run-external-processor`, `integrityDisposition`, and `processorInvocation.parameters`. Until the next schema revision adds a first-class `toolBinding` object, profiles may place the combiner binding under `processorInvocation.parameters`:

```json
{
  "operationId": "run-nt51950-crc-header",
  "sequence": 900,
  "kind": "run-external-processor",
  "targetSpaceId": "output-image",
  "targetRange": { "start": 0, "length": 524288 },
  "integrityDisposition": "recalculate-and-write",
  "processorInvocation": {
    "processorId": "nfc.nt51950.header-crc-v1",
    "contractVersion": "2.0.0",
    "authority": "transform",
    "purpose": "header-and-integrity",
    "allowedReadRanges": [
      { "start": 0, "length": 524288 }
    ],
    "allowedWriteRanges": [
      { "start": 41264, "length": 4 }
    ],
    "parameters": {
      "toolId": "legacy-combiner",
      "toolVersion": "1.10",
      "toolBindingId": "legacy-combiner-1.10",
      "adapterId": "legacy-combiner-inplace-v1"
    },
    "failurePolicy": "fail-closed"
  },
  "reason": "Run approved legacy combiner.exe 1.10 to recalculate and write CRC/header bytes."
}
```

The concrete offsets above are examples only. Supported profiles require owner-approved read ranges, write ranges, preconditions, postconditions, and golden evidence.

## Tool manifest

External combiner tools are declared by `docs/contracts/external-combiner-tool-manifest-v1.md` and its schema. Source control may include manifest examples and internal release manifest entries. Real `combiner.exe` binaries should not be committed unless the owner explicitly approves license, security, storage, and release policy.

Recommended runtime package layout:

```text
external-tools/
  legacy-combiner/
    1.9/
      combiner.exe
      manifest.json
    1.10/
      combiner.exe
      manifest.json
```

## Consequences

- Existing Protocol 1 Python CRC calculation remains useful for pure calculation and tests.
- Protocol 2 staged transform remains the conceptual contract, but production CRC/header transforms can be served by the external combiner runner rather than by Python algorithm code.
- The `tools/crc-worker` package is not the sole CRC/header implementation path.
- Every IC/mode/stage must pin the exact combiner version and allowed write ranges.
- CI and review must reject float versions, direct executable paths in profiles, direct mutation of original BINs, shell command construction, missing SHA-256, and any transform path without independent host diff verification.

## Required tests for first implementation PR

- Tool version `1.10` remains a string and is not normalized to `1.1`.
- Unknown tool binding fails closed.
- Wrong executable SHA-256 fails closed.
- Fake combiner mutating only allowed ranges passes.
- Fake combiner mutating one byte outside `allowedWriteRanges` fails.
- Fake combiner changing file length fails.
- Fake combiner writing unexpected files fails.
- Timeout and non-zero exit fail closed.
- Argument token expansion rejects path traversal and shell metacharacter paths.
- No production project references `refcode` or commits executable payloads.
