# External Combiner Tool Runner Planning Note

This note explains how NFC will call multiple legacy `combiner.exe` versions for CRC/header processing.

## Goal

Different IC/mode/stage combinations may require different `combiner.exe` versions such as `1.9`, `1.10`, or the owner-provided `1.13.0`. NFC must support those exact versions without hard-coding executable paths inside profiles and without allowing external tools to mutate user input files directly.

## Core flow

```text
Composition operation reaches run-external-processor
  -> profile selects processor id and exact tool binding
  -> host creates private staging directory
  -> host materializes current work/output image as work.bin
  -> host resolves combiner.exe from manifest and verifies SHA-256
  -> host runs combiner.exe with approved argument template
  -> host reads modified work.bin or declared output file
  -> host normalizes known command-shortened work output back to original length when coverage is complete
  -> host independently computes byte diff
  -> host accepts only changes inside allowedWriteRanges
  -> host imports validated bytes back into the work buffer
```

## Profile responsibility

A profile declares firmware semantics:

- `processorId`
- `integrityDisposition`
- `authority`
- `purpose`
- `allowedReadRanges`
- `allowedWriteRanges`
- tool binding id/version under `processorInvocation.parameters` until the next contract revision

A profile must not declare an absolute executable path or user-selected temporary file path.

## Tool manifest responsibility

A tool manifest declares executable packaging and launch behavior:

- `toolBindingId`
- `toolId`
- exact string `toolVersion`
- executable name
- SHA-256
- input mode
- argument template
- timeout
- platform

See `docs/contracts/external-combiner-tool-manifest-v1.md`.

## Version safety

`1.10` is not a floating point value. Treat every external combiner version as an exact string token.

## Temporary firmware file

The temporary firmware file is always created by NFC host infrastructure. It should normally be named `work.bin` inside a private staging directory. External tools may receive this path only after token expansion by the host.

Allowed manifest tokens:

- `{staging.workBin}`
- `{staging.outputBin}`
- `{staging.runDir}`

## Failure behavior

All external combiner errors fail closed:

- unknown binding;
- wrong executable hash;
- crash;
- timeout;
- path traversal;
- unexpected file;
- unexpected final file length change;
- changed byte outside `allowedWriteRanges`;
- missing or invalid output.

## Implementation ownership

- `NvtFwCombiner.Application` owns policy and verdicts.
- `NvtFwCombiner.Infrastructure` owns staging, manifest loading, process execution, hash checks, and diff calculation.
- `NvtFwCombiner.Domain` remains filesystem/process free.
- UI and CLI never call `combiner.exe` directly.

## First implementation PR scope

The first implementation PR should add the manifest model, registry, staging workspace, process runner, diff verifier, fake combiner tests, and profile compiler validation. IC-specific CtrlRAM postbuild profiles may be added only from owner-approved postbuild/mmap evidence; real firmware golden outputs are still required before declaring end-to-end production parity.

Legacy Combiner `MERGE_MODE` may shorten the staging work file to the command coverage. The postbuild adapter can overlay that command output onto the previous full-length staging image only when it still covers the command's declared write ranges. The imported output remains full length and remains subject to independent allowed-write-range diff verification.
