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
  -> host resolves a registered invocation profile when the processor selected one, otherwise the manifest default
  -> host runs combiner.exe with the resulting approved argument template
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

A tool manifest declares executable packaging and default launch behavior:

- `toolBindingId`
- `toolId`
- exact string `toolVersion`
- executable name
- SHA-256
- input mode
- argument template
- timeout
- platform

For a V2 `legacy-combiner-v1` stage with an `invocationProfileId`, a closed host-owned invocation registry supplies the stage-specific argument template and input mode. The registry entry must require the same tool binding as the stage and manifest. It is a fixed product contract, not a user command surface and not firmware profile JSON.

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
- missing or invalid output;
- cleanup observed incomplete: the direct child's termination or exit is not
  confirmed, a redirected stream stays open or cannot be read to its end
  within the host cleanup deadline (a timeout or non-zero exit keeps its own
  issue code and mentions the cleanup; a zero exit reports
  `external-tool.process.cleanup-incomplete` before any staged file is read);
- capacity full: a new run refused because external-tool runs that are still
  running or still cleaning up have filled the fixed capacity reports
  `external-tool.process.cleanup-capacity` and asks the user to restart.

## Process lifetime

[ADR 0081](../adr/0081-external-process-cleanup.md) owns the terminal-phase
contract. In short: `SystemExternalProcessRunner` is the single owner; the
caller's cancellation callback only signals; one background termination work
item performs the tree kill; and every host wait after the terminal signal
(termination, exit confirmation, output drain and reader stop) shares one total
5-second deadline, which is not a manifest or profile timeout. The run returns
within that deadline, keeps ownership of any still-running cleanup work, and
reclaims its resources once it settles; it does not claim that no handle
survives the 5 seconds. A process-wide capacity (owner decision 92) counts the
invocations that are running or still cleaning up; a slot is reserved
atomically before each process starts, and at the fixed limit a new run is
refused with a typed error asking the user to restart, so resources cannot
accumulate without bound. Because running invocations also count, normal runs
behave as before only while fewer than the limit are in use at once, which
holds when one external-tool run is started at a time.
`ExternalProcessResult.Cleanup` reports only what the runner observed of
the direct child and its two streams; `Complete` does not prove that every
descendant stopped (a descendant holding no stream is invisible). Caller
cancellation requested before the decision point ends as
`OperationCanceledException`.

## Implementation ownership

- `NvtFwCombiner.Application` owns policy and verdicts.
- `NvtFwCombiner.Infrastructure` owns staging, manifest loading, process execution, hash checks, and diff calculation.
- `NvtFwCombiner.Domain` remains filesystem/process free.
- UI and CLI never call `combiner.exe` directly.

## Current implementation status

The first runner pieces now exist: manifest model, registry, staging workspace, process runner, SHA-256 verification, staged file confinement, independent diff verification, fake-combiner tests, and the Combiner 1.13.0 CtrlRAM postbuild adapter. Selected CtrlRAM replacement bytes are supplied as processor staged source bytes and are not pre-written into the work image before `Combiner.exe`; pasteback is performed by the normalized Combiner command blocks. IC-specific CtrlRAM postbuild command profiles are populated only from owner-approved postbuild/mmap evidence.

Current remaining production gates:

- executable production Replace profiles for every released IC/mode;
- declared allowed write ranges for every real postbuild parity claim;
- private CtrlRAM Replace golden outputs and firmware-owner review;
- clean-package smoke for any release payload that includes `external-tools/` and `reference/`;
- NT51931's tool gate is closed for the AUTO_PRJ-158/PID `0x131B`/cascade-6 regression: registered 1.13.0 `NT51931BASED_NORMAL_MODE CRC8` is byte-identical to both the pre-retirement V1 control and the owner 1.2.0.4 `NT51930BASED_NORMAL_MODE CRC8` control on the same staged case. The 1.2.0.4 binary remains evidence-only and is not packaged; the rejected 1.13.0/51930-based pairing still access-violates. The sole `[1.0.0,infinity)` runtime profile routes both single and generic cascade support-neutrally; direct single output review remains a promotion gate.

Legacy Combiner `MERGE_MODE` may shorten the staging work file to the command coverage. The postbuild adapter can overlay that command output onto the previous full-length staging image only when it still covers the command's declared write ranges. The imported output remains full length and remains subject to independent allowed-write-range diff verification.
