# Test Data Instructions

These rules apply to `testdata/`.

- Normal Git may contain only public synthetic fixtures, manifests, hashes, and access instructions.
- Do not add real/proprietary firmware, expected-output BIN files, decrypted archives, or credentials.
- Each private sample manifest must identify profile id/version, input hashes/sizes, expected output hash/size, source classification, and approvals.
- Synthetic fixtures must be clearly marked and must not be presented as hardware validation.
- Changing an expected hash requires the same human gate as changing firmware semantics.
