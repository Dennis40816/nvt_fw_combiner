---
name: golden-regression
description: Add, update, or review firmware golden vectors, expected output hashes, private fixture manifests, parity claims, or supported IC/mode promotion. Do not weaken expected bytes to match an unexplained implementation change.
---

# Golden Regression

1. Identify IC, mode, profile version, input hashes, output hash, owner, source, and confidentiality class.
2. Keep real firmware outside public Git unless explicitly approved; commit only permitted manifests and synthetic fixtures.
3. Reproduce the reference tool independently and compare full output bytes, size, naming tokens, mutations, and processor outcomes.
4. Explain every intentional byte difference with a declared operation/range/evidence reference.
5. Never regenerate or edit expected output merely to make a test pass.
6. Add invalid-input and one-byte-boundary cases for ranges, patches, CRC/header stages, and atomic failure.
7. Promotion to `supported` requires owner sign-off and no unknown integrity behavior.
8. Run profile/worker/golden gates and Polytail; report private evidence that could not be executed.
