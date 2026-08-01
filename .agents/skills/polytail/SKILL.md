---
name: polytail
description: Audit an NFC diff for correctness, architecture drift, duplication, unsafe mutation, fake tests, and missing evidence.
---

# Polytail

Follow the mandatory verdict and waiver policy in
[docs/policies/polytail.md](../../../docs/policies/polytail.md).

1. Pin the issue/spec, fixed diff/base, branch/target, risk, nearest
   instructions, and touched authorities.
2. Inspect for correctness defects, duplicate semantics, placeholders, silent
   fallback, broad suppressions, speculative abstraction, unsafe mutation,
   private/generated payloads, and code/document/schema drift.
3. Verify the narrow tests exercise behavior and failure cases rather than
   mirroring constants or weakening expected output.
4. Route authority-specific checks:
   - architecture/contracts → `$nfc-architecture-change`;
   - profiles/ranges/processors → `$firmware-profile-authoring`;
   - CRC/header worker → `$crc-worker-contract`;
   - golden evidence → `$golden-regression`;
   - UI → `$ui-experience-change`;
   - release/package → `$release-readiness`.
5. Expand the production-admission audit only when the diff touches route,
   profile, processor, support, or evidence admission. R0/R1 documentation,
   tooling, or visual-only changes do not invent a firmware matrix.

Return findings with P0-P3 severity, path/line, required correction, and
evidence. End with `PASS`, `PASS-WITH-HUMAN-GATE`, or `FAIL`, commands/results,
and residual gates. No P0/P1, failing check, undeclared mutation, fake test, or
missing mandatory reviewer may receive `PASS`.
