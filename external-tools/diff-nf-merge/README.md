# DiffNFMerge Packages

`DiffNFMerge.exe` is an owner-supplied tool intended for future cascade NF preparation.

Current intake:

- `1.0.0`: owner-supplied assembly version `1.0.0.0`, preserved as `DiffNFMerge.exe` from the transferred filename `未確認 410043.crdownload`.

This package is a hash-pinned repository-only intake. It is deliberately excluded from the release allowlist and is not registered by `ExternalProcessorFactory`.

No functional behavior, arguments, directory layout, or input naming is approved by this intake. The owner expects the production workflow may use an `NF/` directory with names such as `NF_Ctrlram_0.bin`; the exact contract will be supplied and reviewed in the planned v0.12.x integration.

Profiles and UI must continue to request a precompiled `NF_Ctrlram.bin` until those gates close. No arbitrary per-run executable path or argument template is permitted.
