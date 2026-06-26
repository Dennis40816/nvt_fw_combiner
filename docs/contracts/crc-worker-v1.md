# CRC Worker Protocol 1.0

## Purpose

This protocol lets the C# host request a deterministic checksum from a bundled Python companion process without granting filesystem mutation authority. It is the pure calculation contract only; staged BIN/header mutation is reserved for Protocol 2.0 in `crc-worker-transform-v2-draft.md`.

## Transport

- One process per request.
- UTF-8 JSON request on stdin, followed by EOF.
- Exactly one compact UTF-8 JSON object on stdout.
- Diagnostic text, when necessary, goes to stderr and is never required for machine parsing.
- No JSON Lines stream, banner, progress output, or traceback on stdout.

## Limits

- Decoded payload: at most 4 MiB.
- Host-enforced stdout: at most 64 KiB.
- Host-enforced stderr: at most 64 KiB.
- Host timeout: 5 seconds by default.
- Unknown request fields are rejected.

## Request

```json
{
  "protocolVersion": "1.0",
  "requestId": "018f4eb6-5ef8-7aef-bb46-eaf7fbab41a1",
  "operation": "calculate",
  "algorithmId": "crc-32-mpeg-2",
  "payloadBase64": "MTIzNDU2Nzg5"
}
```

`requestId` is an opaque non-empty correlation string. The host is expected to use a UUID/UUIDv7, but the worker does not generate or reinterpret it.

## Success response

```json
{
  "protocolVersion": "1.0",
  "requestId": "018f4eb6-5ef8-7aef-bb46-eaf7fbab41a1",
  "ok": true,
  "workerVersion": "0.1.0",
  "result": {
    "algorithmId": "crc-32-mpeg-2",
    "valueUnsigned": 58124007,
    "valueHex": "0x0376E6E7",
    "bytesLittleEndianHex": "E7E67603"
  }
}
```

## Error response

```json
{
  "protocolVersion": "1.0",
  "requestId": "request id when recoverable, otherwise empty",
  "ok": false,
  "workerVersion": "0.1.0",
  "error": {
    "code": "CRC_PROTOCOL_INVALID_BASE64",
    "message": "payloadBase64 is not valid base64"
  }
}
```

Stable error codes in v1:

| Code | Exit | Meaning |
| --- | ---: | --- |
| `CRC_PROTOCOL_INVALID_JSON` | 2 | stdin is not one valid JSON object |
| `CRC_PROTOCOL_INVALID_REQUEST` | 2 | missing, unknown, or wrongly typed field |
| `CRC_PROTOCOL_INVALID_BASE64` | 2 | payload is not canonical valid base64 |
| `CRC_PROTOCOL_PAYLOAD_TOO_LARGE` | 2 | decoded payload exceeds 4 MiB |
| `CRC_PROTOCOL_UNSUPPORTED_VERSION` | 3 | protocol major/minor is not supported |
| `CRC_PROTOCOL_UNSUPPORTED_OPERATION` | 3 | operation is not supported |
| `CRC_PROTOCOL_UNSUPPORTED_ALGORITHM` | 3 | algorithm id is not supported |
| `CRC_INTERNAL_CALCULATION_FAILED` | 4 | unexpected calculation failure |
| `CRC_WORKER_SELF_TEST_FAILED` | 5 | packaged worker failed its startup/self-test invariant |

## Algorithm

`crc-32-mpeg-2` uses polynomial `0x04C11DB7`, initial value `0xFFFFFFFF`, no reflection, and no final XOR.

## Compatibility

- A 1.x worker must not silently reinterpret an existing field.
- New optional response fields may be added in a compatible minor release.
- The host must ignore unknown response fields only when its own response contract explicitly permits that behavior; v1 bootstrap should parse strictly.
- A breaking request/response change requires protocol 2.0.
- Protocol 1.x must never accept file paths or mutate a BIN; doing so is an authority change, not a compatible minor extension.
