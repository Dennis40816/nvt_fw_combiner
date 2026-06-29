# Standard Merge gen_flash Golden Fixtures

Owner-approved golden fixtures for standard merge parity against `gen_flash_bin_v2/test`.

- Source repository: `Dennis40816/NFCG`
- Source commit: `bb87f183dc8abe3c47ff1a01966805ede5e74115`
- Source path: `testdata/reference/FlashCodeGenerator/gen_flash_bin_v2/test`
- Approval: repository owner requested these `gen_flash_bin_v2` test BIN files be tracked as golden fixtures on 2026-06-30.

Every tracked `.bin` in this directory must be listed in `manifest.json` with source path, Git blob SHA, byte size, and SHA-256. `scripts/validate_repository.py` rejects unlisted BIN files or manifest hash drift.
