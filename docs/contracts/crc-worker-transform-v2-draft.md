# CRC / Header Worker Transform Protocol 2.0 — Draft Reservation

Status: **Reserved; exact processor parameters await firmware-owner instructions.**

## Purpose

Protocol 1.0 remains a pure CRC calculation contract. Protocol 2.0 reserves a controlled mode in which Python may update CRC/header bytes in a host-created staging copy. Python never receives or overwrites the user's original BIN or final output path.

## Authority model

1. The host creates a new isolated run directory.
2. The host copies the selected immutable artifact/work buffer to `work.bin`.
3. The host starts the bundled worker with that directory as its working directory and no shell/network.
4. The request references only a validated relative filename.
5. The worker may mutate only that staging file.
6. The worker returns hashes, checks, and claimed changed ranges.
7. The host independently computes a byte diff and rejects any changed byte outside profile-declared `allowedWriteRanges`.
8. Only after all checks pass does the host import the result into the named work buffer/output and continue the composition plan.
9. Crash, timeout, malformed response, extra file creation, or out-of-policy mutation discards the run directory and fails closed.

## Draft request envelope

```json
{
  "protocolVersion": "2.0",
  "requestId": "018f4eb6-5ef8-7aef-bb46-eaf7fbab41a1",
  "operation": "transform",
  "processorId": "nfc.nt51950.tpb-header-crc-v1",
  "workingFile": "work.bin",
  "addressSpaceId": "tpb-work",
  "expectedLength": 262144,
  "allowedReadRanges": [
    { "start": 41216, "length": 48 }
  ],
  "allowedWriteRanges": [
    { "start": 41216, "length": 52 }
  ],
  "parameters": {}
}
```

`parameters` remains processor-specific and cannot be finalized until the owner supplies the exact header invocation and field rules.

## Draft success response

```json
{
  "protocolVersion": "2.0",
  "requestId": "018f4eb6-5ef8-7aef-bb46-eaf7fbab41a1",
  "ok": true,
  "workerVersion": "0.2.0",
  "result": {
    "processorId": "nfc.nt51950.tpb-header-crc-v1",
    "beforeSha256": "...",
    "afterSha256": "...",
    "claimedChangedRanges": [
      { "start": 41264, "length": 4 }
    ],
    "checks": []
  }
}
```

The host never trusts `claimedChangedRanges` without its own diff.

## Path and filesystem restrictions

- `workingFile` must be one plain relative filename; no separators, `..`, drive letters, UNC path, symlink, junction, or reparse-point traversal.
- The staging directory may contain only the request, one working file, and bounded worker result/diagnostic files.
- The worker cannot create executables, load plugins, spawn children, or enumerate outside the run directory.
- The host verifies file count, names, length, and hashes before importing output.

## Compatibility

This is a major protocol because it grants constrained file mutation authority. It must not be silently added to a 1.x worker. The released executable may support both protocol 1.x calculation and protocol 2.x transformation during migration.

## Open items before implementation

- exact Python entry point and command shape;
- header fields and ordering;
- per-IC processor ids and parameters;
- whether TP A is verify-only or also rewritten for each profile;
- complete allowed read/write ranges;
- expected pre-values and postconditions;
- golden fixtures and failure behavior.
