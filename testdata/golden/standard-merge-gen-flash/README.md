# Standard Merge gen_flash Golden Fixtures

Owner-approved golden fixtures for standard merge parity against `gen_flash_bin_v2/test`.

- Source repository: `Dennis40816/NFCG`
- Source commit: `bb87f183dc8abe3c47ff1a01966805ede5e74115`
- Source path: `testdata/reference/FlashCodeGenerator/gen_flash_bin_v2/test`
- Approval: repository owner requested these `gen_flash_bin_v2` test BIN files be tracked as golden fixtures on 2026-06-30.

Additional owner-approved Standard Merge fixtures for NT51930 and NT51950/NT51951 DP Perspective came from `merge_bin.7z`, supplied by the repository owner on 2026-07-03. The archive SHA-256 and per-file provenance are recorded in `manifest.json`.

Every tracked `.bin` in this directory must be listed in `manifest.json` with source path, source archive/blob identity where available, byte size, and SHA-256. `scripts/validate_repository.py` rejects unlisted BIN files or manifest hash drift.
