# NFC CRC Worker Prototype

The current Python worker implements only the pure Protocol 1.0 CRC calculation path. It is not a release binary and it is not the production legacy `combiner.exe` runner.

Production CRC/header transforms may require different approved legacy `combiner.exe` versions such as `1.9` and `1.10`. Those transforms are planned through the External Combiner Tool Runner described in `docs/adr/0006-external-combiner-tool-runner.md` and `docs/contracts/external-combiner-tool-manifest-v1.md`.

The Python worker may remain useful for pure checksum calculation, synthetic tests, or a future adapter, but it must not be treated as the only CRC/Header implementation path.

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

## Transform rule

Any transform path, whether implemented by Python or by legacy `combiner.exe`, operates only on a host-created staging copy such as `work.bin`. The host must verify executable identity, expected length, SHA-256, file count, and changed ranges before importing the result.
