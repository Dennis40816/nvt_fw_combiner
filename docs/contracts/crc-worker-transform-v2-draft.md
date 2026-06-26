# Staged Transform Protocol 2.0 — Draft Reservation

Status: **Reserved; exact processor/tool parameters await firmware-owner instructions.**

## Purpose

Protocol 1.0 remains a pure CRC calculation contract for the Python worker. Protocol 2.0 reserves the general controlled-transform model in which an approved external processor updates CRC/header bytes in a host-created staging copy.

The production transform implementation may be either:

- a future Python adapter; or
- the preferred external combiner tool runner for approved legacy `combiner.exe` versions such as `1.9` and `1.10`.

The processor never receives or overwrites the user's original BIN or final output path.

## Authority model

1. The host creates a new isolated run directory.
2. The host copies the selected immutable artifact/work buffer to `work.bin`.
3. The host starts the approved bundled or manifest-resolved processor with that directory as its working directory and no shell/network.
4. The request references only a validated relative filename or approved staging token.
5. The processor may mutate only that staging file or the declared staging output file.
6. The processor returns or allows the host to derive hashes, checks, and changed ranges.
7. The host independently computes a byte diff and rejects any changed byte outside profile-declared `allowedWriteRanges`.
8. Only after all checks pass does the host import the result into the named work buffer/output and continue the composition plan.
9. Crash, timeout, malformed response, extra file creation, length change, or out-of-policy mutation discards the run directory and fails closed.

## Draft request envelope

```json
{
  "protocolVersion": "2.0",
  "requestId": "018f4eb6-5ef8-7aef-bb46-eaf7fbab41a1",
  "operation": "transform",
  "processorId": "nfc.nt51950.tpb-header-crc-v1",
  "toolBindingId": "legacy-combiner-1.10",
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

`parameters` remains processor-specific and cannot be finalized until the owner supplies the exact legacy combiner version, invocation, header fields, ordering, and IC-specific rules.

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
- Manifest token expansion is host-owned and limited to approved staging tokens.
- The staging directory may contain only the request, working file, declared output file, and bounded diagnostic/result files.
- The processor cannot load plugins, spawn children, or enumerate outside the run directory unless explicitly approved by a reviewed tool manifest.
- The host verifies file count, names, length, and hashes before importing output.

## Compatibility

This is a major protocol because it grants constrained file mutation authority. It must not be silently added to a 1.x Python worker. The released package may support pure Protocol 1.x calculation while production CRC/header transform is served by the external combiner runner.

## Open items before production implementation

- exact legacy `combiner.exe` versions per IC/mode/stage;
- tool manifest entries and executable SHA-256 values;
- argument templates and input/output mode;
- header fields and ordering;
- per-IC processor ids and parameters;
- whether TP A is verify-only or also rewritten for each profile;
- complete allowed read/write ranges;
- expected pre-values and postconditions;
- golden fixtures and failure behavior.
