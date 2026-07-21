---
name: golden-regression
description: Add, update, or review firmware golden vectors, expected output hashes, private fixture manifests, parity claims, or supported IC/mode promotion. Do not weaken expected bytes to match an unexplained implementation change.
---

# Golden Regression

1. Identify IC, mode, profile version, input hashes, output hash, owner, source, and confidentiality class.
2. Keep real firmware outside public Git unless explicitly approved; commit only permitted manifests and synthetic fixtures.
3. Reproduce the reference tool independently and compare full output bytes, size, naming tokens, mutations, and processor outcomes.
4. Explain every intentional byte difference with a declared operation/range/evidence reference.
5. Separate fixture identity from production authority. Whole-file SHA, filename, exact PID, TP FW/Common FW version, and the fixture's observed chip count describe evidence; they must not become runtime gates merely because they identify a golden case.
6. Resolve an IC family only from the requested IC and owner-declared family or fact-scoped alias membership. Metadata values may validate a selected plan, but they must not select or change the family.
7. Treat owner-provided runtime postbuild profiles as effective-version intervals beginning at Common FW `1.0.0`: one runtime profile covers every version; with multiple runtime profiles, each profile applies until the next profile's effective version. An evidence-only profile never creates a production boundary, and a golden's exact version never narrows an interval.
8. Derive build-plan kinds from distinct owner-provided command plans. `Number > 1` means generic cascade unless independent command evidence declares exact-count or non-overlapping count-range plans; an observed golden count alone cannot create one.
9. Add production-route tests that vary or omit every informational fixture value while keeping declared byte-authoritative facts valid. Test effective-version boundaries and build-plan count boundaries independently from expected-output parity.
10. Never regenerate or edit expected output merely to make a test pass.
11. Add invalid-input and one-byte-boundary cases for ranges, patches, CRC/header stages, and atomic failure.
12. Promotion to `supported` requires owner sign-off and no unknown integrity behavior.
13. Run profile/worker/golden gates and Polytail; report private evidence that could not be executed.
