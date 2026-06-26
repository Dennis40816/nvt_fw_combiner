# Polytail Quality Gate

Status: **Confirmed repository skill and mandatory outcome**

## Definition

Polytail is the repository's anti-slop Agent Skill located at:

```text
.agents/skills/polytail/SKILL.md
```

It exists to prevent AI-assisted development from producing low-quality code: duplicated semantics, architecture drift, magic firmware constants, fake tests, placeholders, silent failures, broad suppressions, speculative abstractions, oversized unrelated diffs, or documentation/schema drift.

Polytail is not a public dependency, not an external package requirement, and not an alias for Pylint. Pylint remains one analyzer inside the Python quality toolchain.

## Required outcomes

A mergeable change must prove:

1. deterministic formatting and zero unsuppressed compiler/analyzer/lint/type errors;
2. dependency/architecture boundaries remain valid;
3. Merge and Replace still use one composition engine;
4. all firmware operations have typed address spaces and half-open ranges;
5. Python staging mutations stay within declared write ranges and original files remain unchanged;
6. meaningful positive/negative/unit/property/contract/profile/golden tests pass as applicable;
7. schema, generated files, locks, docs, reports, and code agree;
8. no firmware payload, secret, cache, build output, or unapproved binary is committed;
9. package contents match the closed allowlist;
10. any unavailable private evidence is explicitly reported rather than claimed complete.

Required CI status check:

```text
policy / polytail
```

Canonical command:

```text
python scripts/verify.py --all
```

The skill adds architectural and test-quality review beyond merely running this command. CI cannot replace an agent/human semantic review: the required status check enforces the deterministic subset, while the PR records the skill verdict, findings, commands, and residual human gates.

## Prohibited ways to pass

- lower thresholds or expand exclusions;
- disable analyzers/tests or add broad suppressions;
- delete negative cases;
- update golden outputs without approved firmware evidence;
- turn errors into warnings or silent fallback;
- duplicate core logic in UI/CLI/worker;
- claim a TODO/placeholder as finished;
- split code only cosmetically while preserving a god object;
- conceal an R3 change inside formatting/generated noise.

## Waivers

A waiver requires rule/tool, scope, reason, risk, owner, issue, approver, creation/expiry date, and removal condition. No permanent waiver is allowed for firmware range safety, processor write ranges, checksum/header ordering, secrets, signing material, or release allowlists.
