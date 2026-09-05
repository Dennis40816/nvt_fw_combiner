# Test Data Instructions

These rules apply to `testdata/`.

- Normal Git may contain public synthetic fixtures, manifests, hashes, access instructions, and owner-approved golden fixtures under `testdata/golden/`.
- Do not add real/proprietary firmware, expected-output BIN files, decrypted archives, or credentials outside approved `testdata/golden/` fixture directories.
- Each golden or private sample manifest must identify its applicable profile id/version, input hashes/sizes, source classification, provenance, approvals, and evidence scope. Output Goldens require expected-output hash/size; input-only evidence explicitly declares that no expected output exists and cannot claim output parity.
- Each tracked `.bin` under `testdata/golden/` must be listed in a manifest and pass size/SHA-256 validation.
- Synthetic fixtures must be clearly marked and must not be presented as hardware validation.
- Changing an expected hash requires the same human gate as changing firmware semantics.
