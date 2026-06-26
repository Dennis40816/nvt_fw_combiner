# External Combiner Tool Manifest Contract 1.0

This contract describes legacy external `combiner.exe` binaries used for CRC/header post-processing.

The manifest is intentionally separate from composition profiles:

- profiles declare firmware semantics, processor identity, authority, purpose, and allowed read/write ranges;
- manifests declare executable packaging, version, hash, command shape, staging file convention, timeout, and platform.

## Required fields

```json
{
  "schemaVersion": "1.0",
  "toolBindingId": "legacy-combiner-1.10",
  "toolId": "legacy-combiner",
  "toolVersion": "1.10",
  "displayName": "Legacy CRC/Header Combiner 1.10",
  "platform": "win-x64",
  "executableName": "combiner.exe",
  "sha256": "<64 lowercase hex chars>",
  "adapterId": "legacy-combiner-inplace-v1",
  "inputMode": "in-place",
  "argumentTemplate": ["{staging.workBin}"],
  "workingDirectoryPolicy": "staging-directory",
  "timeoutSeconds": 5,
  "allowedExtraOutputFiles": []
}
```

## Version rule

`toolVersion` is a string. Do not parse it as a number. `1.10` must never collapse to `1.1`.

## Input modes

- `in-place`: the combiner mutates `{staging.workBin}`.
- `input-output-file`: the combiner reads `{staging.workBin}` and writes `{staging.outputBin}`.

The host verifies the selected output file length and diff before importing it.

## Allowed argument tokens

Only these tokens are allowed in `argumentTemplate`:

- `{staging.workBin}`
- `{staging.outputBin}`
- `{staging.runDir}`

The host expands tokens after creating the staging directory. Profiles and users do not provide filesystem paths.

## Security rules

- No shell command string is assembled.
- `UseShellExecute` must be false.
- Executable path comes only from installation layout plus manifest.
- SHA-256 of the executable must match before launch.
- Working directory is the private staging directory.
- Timeout is mandatory.
- Any unexpected file, length change, crash, timeout, or out-of-range mutation fails closed.

## Repository policy

Real `combiner.exe` binaries should not be committed to source control unless explicitly approved. The repository may include manifest examples and internal packaging instructions. Release packaging may add approved executables under an internal `external-tools/` layout.
