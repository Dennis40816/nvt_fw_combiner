# External Processor Protocols

This document expands the safety contract summarized in `SPEC.md` section 9. Versioned wire formats remain canonical in [crc-worker-v1](../contracts/crc-worker-v1.md) and [crc-worker-transform-v2-draft](../contracts/crc-worker-transform-v2-draft.md).

## Protocol Families

| Family | Current status | Purpose |
| --- | --- | --- |
| Python CRC worker Protocol 1.0 | Implemented prototype | Pure CRC calculation; no file mutation |
| Staged transform Protocol 2.x | Reserved concept | Host-created staging copy mutation with independent diff |
| External Combiner runner | Staged adapter for approved Combiner 1.13.0 CtrlRAM postbuild; parity remains profile/golden-gated | Approved legacy `combiner.exe` CRC/header transform |

## Process and Filesystem Safety

- Use `UseShellExecute = false` and `CreateNoWindow = true`; never compose a shell command or show a
  console window. An owner Postbuild command sequence is one logical staged run, while each approved
  Combiner argv remains a directly launched, independently timed and audited child process.
- Installation layout, tool manifest, and release manifest choose the executable; callers cannot supply a per-run executable path.
- The host owns a private staging directory and accepts only approved relative staging tokens.
- Reject separators, `..`, drive/UNC paths, symlink/junction/reparse traversal, extra files, and unexpected length changes.
- Use an allowlisted environment with network disabled. Child processes or plug-ins require explicit tool-manifest approval.
- Default timeout is five seconds and must terminate the complete process tree.
- Bound stdout/stderr and validate machine-readable results against their schema.
- After a transform, independently verify file count, file name, length, SHA-256, changed ranges, and postconditions.
- A processor-owned dynamic placement uses a profile-declared bounded write
  authority plus an independently calculated expected postcondition. For the
  NT51919/NT51929/NT51932 DiffDLM route, postbuild chooses the actual FWConfig
  Backup address; runtime compares it with the count-derived, `0x1000`-aligned
  expected address. An in-authority mismatch is a typed Build Report warning,
  while a missing/ambiguous Backup or out-of-authority mutation fails closed.
- NT51950/NT51951 are not dynamic-placement routes. Their declared End Flag
  `0x36FFC` fixes the canonical FWConfig Backup start at `0x36000`; a different
  marker-derived location fails the fixed postbuild contract.
- Tool failure cannot fall back to a different algorithm or an unreviewed C# rewrite.

## Applicability Model

```text
integrityDisposition = none | verifyExisting | recalculateAndWrite
processorAuthority   = calculate | transform
processorPurpose     = checksum | header | headerAndIntegrity | relocation | compositePostProcess
toolBindingId        = optional exact external tool binding for legacy transforms
```

Planning may use `unknown`; a supported profile may not. Protocol 1 is `calculate`; staged transforms and the legacy runner are constrained `transform` authorities. The evidence matrix is [integrity-processing-matrix](../architecture/integrity-processing-matrix.md).

## Protocol 1 Calculate Example

```json
{
  "protocolVersion": "1.0",
  "requestId": "demo",
  "operation": "calculate",
  "algorithmId": "crc-32-mpeg-2",
  "payloadBase64": "MTIzNDU2Nzg5"
}
```

The response contains `ok`, exact `workerVersion`, algorithm id, unsigned value, hexadecimal value, and little-endian bytes. Protocol 1 never receives file paths or mutates a BIN.

## Transform Reservation and Host Ports

A transform request needs protocol/request id, processor id or tool binding, host-created relative working file, address space/expected length, allowed read/write ranges, and typed parameters. A response supplies before/after hashes, processor id, claimed changed ranges, and checks. The host diff remains authoritative.

```csharp
public interface ICrcCalculator
{
    Task<CrcCalculationResult> CalculateAsync(CrcCalculationRequest request, CancellationToken cancellationToken);
}

public interface IFirmwarePostProcessor
{
    Task<PostProcessResult> TransformAsync(PostProcessRequest request, CancellationToken cancellationToken);
}
```

Application owns policy, diff verdict, and mutation trace. Infrastructure owns process and filesystem details.

## Runtime Dependency Readiness

- A refresh request contains only processor/tool references already selected
  by the compiled capability. Discovery cannot grant processor authority.
- The Application host supplies a positive monotonic environment generation to
  every probe. Snapshots additionally retain the exact route, capability
  fingerprint, catalog resolution token, and authoring revision.
- Ordinary Preview/Build attempts acquire the current generation without
  replacing it, so parallel attempts do not invalidate each other. Only the
  explicit runtime-dependency refresh command advances discovery generation.
- One action acquires its readiness provider and processor executor from the
  same environment generation. Replacing discovery advances the generation;
  an old construction or probe must retry or be rejected as stale rather than
  publish after the replacement.
- Missing manifests, missing executables, hash mismatch, unsupported platform,
  or unavailable staging are typed blocked dependencies. Installing or
  correcting the tool is observed by the next explicit refresh without
  restarting the process.
- Readiness is preflight only. The external processor still validates its
  manifest, executable identity, staging, mutation bounds, and output
  postconditions during the actual execution.

## Acceptance Expectations

- Empty payload yields `0xFFFFFFFF`; `123456789` yields `0x0376E6E7`.
- Tool version `1.10` remains a string token and is never normalized to `1.1`.
- Unknown binding, executable hash mismatch, out-of-range mutation, length change, traversal/reparse attempt, extra file, incorrect mutation claim, crash, or timeout fails closed.
- A fake processor limited to declared ranges passes; one mutating an undeclared byte fails.
- Failures leave original artifacts and final output unchanged, and replay is deterministic.
- A released component is validated on clean Windows without requiring system Python.
