# NFC CRC / Header Worker Prototype

The current source prototype implements the pure Protocol 1.0 CRC calculation path. It is not a release binary and does not yet implement the reserved Protocol 2.0 staged transform.

## Development run

```powershell
$env:PYTHONPATH = "src"
'{"protocolVersion":"1.0","requestId":"demo","operation":"calculate","algorithmId":"crc-32-mpeg-2","payloadBase64":"MTIzNDU2Nzg5"}' |
  python -m nfc_crc_worker
```

Expected CRC: `0x0376E6E7`.

## Tests

```powershell
python -m pytest
```

The production transform path will operate only on a host-created staging copy and will be implemented after the exact header command, fields, ordering, and per-IC write ranges are supplied and approved. It must conform to `docs/contracts/crc-worker-transform-v2-draft.md`.
