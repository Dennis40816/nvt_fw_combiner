---
name: nfc-golden-regression
description: Add, update, or review firmware golden vectors, expected output hashes, private fixture manifests, parity claims, or supported IC/mode promotion. Do not weaken expected bytes to match an unexplained implementation change.
---

# Golden Regression

1. Identify IC, mode, profile version, input hashes, output hash, owner, source, and confidentiality class.
2. Keep real firmware outside public Git unless explicitly approved; commit only permitted manifests and synthetic fixtures.
3. Reproduce the reference tool independently and compare full output bytes, size, naming tokens, mutations, and processor outcomes.
4. Explain every intentional byte difference with a declared operation/range/evidence reference.
5. Separate fixture identity from production authority. Whole-file SHA, filename, exact PID, TP FW/Common FW version, and the fixture's observed chip count describe evidence; they must not become runtime gates merely because they identify a golden case.
6. Apply [ADR 0031](../../../docs/adr/0031-ctrlram-profile-intervals-and-build-plan-authority.md)
   for family selection, effective-version intervals and build-plan authority.
   Test those production boundaries independently of expected-output parity;
   vary or omit informational fixture values while declared byte facts stay valid.
7. Never regenerate or edit expected output merely to make a test pass.
8. Add invalid-input and one-byte-boundary cases for ranges, patches, CRC/header stages, and atomic failure.
9. Promotion to `supported` requires owner sign-off and no unknown integrity behavior.
10. Run the affected profile/worker/golden gates and skill `nfc-review`; use
    root `AGENTS.md` for the full-suite boundary. Report private evidence that
    could not be executed and retain the R3 owner-review gate.
