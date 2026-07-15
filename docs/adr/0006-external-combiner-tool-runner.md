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

The exact tool path, executable file name, SHA-256, timeout, and platform are supplied by an external combiner tool manifest. A manifest also declares the default argument template and input/output mode for invocations that do not select a registered invocation profile.

When a V2 processor stage declares an `invocationProfileId`, the profile selects a closed, host-owned invocation contract by that id. That contract supplies the exact argument template and input/output mode for the stage, and must require the same `toolBindingId` as both the processor stage and resolved manifest. It can only use approved host staging tokens. It cannot contain an executable path, user path, shell fragment, or arbitrary runtime parameter. This allows one audited Combiner package to expose distinct, firmware-owner-approved commands without embedding command lines in firmware profile JSON.

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
10. Infrastructure verifies file count, expected names, final imported length, after SHA-256, and byte diff.
11. Application accepts the result only if every changed byte is inside the profile-declared `allowedWriteRanges` and all compiled output assertions pass before the output is committed.
12. The validated bytes are imported into the current work buffer/output image.
13. Any timeout, crash, malformed output, path escape, unexpected file, unexpected final length change, SHA mismatch, or out-of-range mutation fails closed.

Some owner-provided Combiner `MERGE_MODE` postbuild commands rewrite the staging work file to the command output length rather than the full firmware image length. The adapter may normalize that known shortened command output by overlaying it back onto the original-length staging bytes only when the shortened file still covers the command's declared write coverage. The final imported firmware image length must remain unchanged and the independent byte diff must still pass `allowedWriteRanges`.

The temporary firmware file is an implementation detail of the host. Profiles should name address spaces and ranges, not host filesystem paths.

## Profile expression

`composition-profile-v1` keeps its existing adapter shape only for compatibility migration.
`composition-profile-v2` replaces arbitrary `processorInvocation.parameters` with a closed
`legacy-combiner-v1` stage. The stage references a trusted tool binding and registered invocation
profile. The V2 profile itself never carries executable paths or command arguments; the registered
invocation contract is host-owned and must bind to the resolved manifest's exact tool binding.

```json
{
  "processorStageId": "recalculate-crc-header",
  "kind": "legacy-combiner-v1",
  "toolBindingId": "legacy-combiner-1-13",
  "invocationProfileId": "approved-postbuild-profile",
  "targetSpaceId": "output-image",
  "authority": "transform",
  "purpose": "header-and-integrity",
  "integrityDisposition": "recalculate-and-write",
  "allowedReadViewIds": ["firmware-image"],
  "allowedWriteViewIds": ["approved-header-and-crc-fields"],
  "stagedSourceBindings": [],
  "evidenceRef": "owner-approved-postbuild-evidence",
  "failurePolicy": "fail-closed"
}
```

The referenced views resolve through the canonical firmware map. A postcondition is compiled as an
exact output assertion over a declared target-image range; it verifies returned staging bytes but
does not grant an additional write range. Supported profiles require owner-approved read/write
regions, invocation behavior, preconditions, postconditions, and golden evidence. The profile
cannot inline command arguments or host paths.

## Tool manifest

External combiner tools are declared by `docs/contracts/external-combiner-tool-manifest-v1.md` and its schema. Source control may include manifest examples and internal release manifest entries. Real `combiner.exe` binaries should not be committed unless the owner explicitly approves license, security, storage, and release policy. Combiner `1.13.0` is owner-approved for this repository and is pinned by manifest SHA-256.

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
