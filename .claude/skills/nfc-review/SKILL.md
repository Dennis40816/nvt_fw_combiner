---
name: nfc-review
description: Review a fixed NFC diff or a scoped repository snapshot for correctness, architecture, and test evidence.
---

# NFC Review

Apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md).
For a change review, pin the exact commit or merge-base diff and originating
issue/spec; an empty or unresolved diff cannot establish change correctness.
For a current-state audit, pin the source commit, exact files or subsystem,
and the owner's audit question. This needs no new diff and does not substitute
for a change's admission or fixed-diff review. Keep unrelated findings separate.

Use three lenses in one findings list:

1. **Spec correctness** — missing, incorrect, or unrequested behavior.
2. **Runtime, safety, and architecture** — dependency direction, firmware
   authority, ranges/order, immutable inputs/staging, compatibility, security,
   and release impact.
3. **Tests and evidence** — behavior and failure coverage, non-mirrored tests,
   golden independence, and residual human evidence.

Apply the Polytail policy below to the same fixed scope and add the matching
NFC authority skill when its surface is touched. Spawn read-only subagents only when the diff
has genuinely independent, read-heavy areas; ordinary reviews stay in one
pass. Tooling output does not replace semantic review.

Report findings first in this form:

```text
[P0-P3] Title
Path:line
Observed behavior
Why it matters
Required correction
Evidence/test
```

Then give the Polytail verdict, commands inspected, and remaining human gates.
Do not duplicate the same finding under multiple review headings.

## Polytail audit

Follow the mandatory verdict and waiver policy in
[docs/policies/polytail.md](../../../docs/policies/polytail.md).

1. Pin the issue/spec, fixed diff/base, branch/target, risk, nearest
   instructions, and touched authorities.
2. Inspect for correctness defects, duplicate semantics, placeholders, silent
   fallback, broad suppressions, speculative abstraction, unsafe mutation,
   private/generated payloads, and code/document/schema drift.
   A diff that adds, changes, moves, wraps, splits, replaces, or refactors
   production behavior, a semantic branch, or an owner contract without the
   [pull request admission evidence](../../../docs/governance/development-execution-workflow.md#admission)
   is a P1 finding even when its tests pass. Confirm the owner search,
   disposition, and risk-appropriate review and approval evidence.
   For every added readiness calculation, validator, normalizer, or policy
   branch, trace its canonical producer. Treat re-deriving profile/compiler,
   inspector, session, or processor facts in Application/Bootstrap/UI as
   duplicate semantics even when the new tests pass; require reuse of the
   existing typed result or an explicit missing-owner decision.
3. Verify the narrow tests exercise behavior and failure cases rather than
   mirroring constants or weakening expected output.
4. Route authority-specific checks:
   - architecture/contracts → `$nfc-architecture-change`;
   - profiles/ranges/processors → `$nfc-firmware-profile-authoring`;
   - CRC/header worker → `$nfc-crc-worker-contract`;
   - golden evidence → `$nfc-golden-regression`;
   - UI → `$nfc-ui-experience-change`;
   - release/package → `$nfc-release-readiness`.
5. Expand the production-admission audit only when the diff touches route,
   profile, processor, support, or evidence admission. R0/R1 documentation,
   tooling, or visual-only changes do not invent a firmware matrix.

Return findings with P0-P3 severity, path/line, required correction, and
evidence. End with `PASS`, `PASS-WITH-HUMAN-GATE`, or `FAIL`, commands/results,
and residual gates. No P0/P1, failing check, undeclared mutation, fake test, or
missing mandatory reviewer may receive `PASS`.
