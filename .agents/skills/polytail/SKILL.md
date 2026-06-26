---
name: polytail
description: Audit or finish any non-trivial NFC code change to prevent low-quality AI-generated code, architecture drift, duplicated firmware semantics, fake tests, broad suppressions, placeholders, and unreviewable changes. Use before claiming implementation complete or approving a PR; do not use as a substitute for firmware-owner review.
---

# Polytail Quality Gate

`polytail` is the repository's anti-slop engineering skill. It is an instruction workflow, not a third-party package and not an alias for Pylint.

## Required inputs

1. Read the issue scope, acceptance criteria, root and nearest `AGENTS.md`, relevant ADRs/contracts, and the actual diff.
2. Identify affected layers, public contracts, IC/mode/profile, address spaces, ranges, operation order, and release impact.
3. Classify risk as `R0` through `R3`; firmware ranges, post-processing, golden data, security, and release changes are always `R3`.

## Reject low-quality patterns

Fail the review when any of the following appears without an approved, narrowly documented reason:

- TODO, placeholder, `NotImplemented`, dead branch, mock production result, or silent fallback in a completed feature;
- duplicated merge/replace semantics in UI, CLI, worker, profile compiler, or a one-off script;
- magic offsets, anonymous integer ranges, ambiguous inclusive ends, unchecked arithmetic, or implicit byte order;
- broad `catch`/`except`, swallowed errors, unstable error text in place of issue codes, or success returned after partial failure;
- tests that only assert constants, mirror the implementation, delete coverage, loosen expected output, or omit failure cases;
- analyzer/lint suppressions, threshold reductions, exclusions, or disabled tests added only to make CI green;
- speculative abstractions with no current caller, service-locator/global mutable state, unnecessary wrappers, or oversized god modules;
- mutation of caller-owned input buffers, direct writes to user firmware by Python, undeclared write ranges, or overlap hidden by copy order;
- generated files, firmware payloads, credentials, release output, or unrelated refactors mixed into the change;
- documentation, schema, profile, report, and implementation semantics that disagree.

## Architecture audit

- Confirm Merge and Replace use the same composition engine; only image initialization differs.
- Confirm UI/CLI only produce typed requests and never implement firmware rules.
- Confirm every operation names source/target address spaces and half-open ranges.
- Confirm external Python can modify only a host-created staging copy and the host independently verifies changed ranges.
- Confirm integrity outcome (`none`, `verify-existing`, `recalculate-and-write`) and processor authority (`calculate`, `transform`) are explicit and separate; `unknown` cannot compile as supported behavior.
- Confirm custom layouts compile to the same profile/operation model and cannot execute arbitrary scripts.

## Evidence audit

1. Require the narrowest meaningful unit/property/contract/profile/golden tests.
2. Verify negative cases for bounds, overflow, overlap, malformed profile, worker failure, out-of-policy writes, and interrupted output.
3. Run the canonical affected checks, then `python scripts/verify.py --all` before completion when the environment supports it.
4. Do not claim completion when required private golden or clean-machine evidence is unavailable; state the missing gate explicitly.

## Review output

Return a concise report with:

```text
Risk class
Architecture fit
Correctness findings
Test quality findings
Security/release findings
Required fixes
Commands and results
Residual evidence gaps
Verdict: PASS | PASS-WITH-HUMAN-GATE | FAIL
```

`PASS` is forbidden while any P0/P1 finding, undeclared firmware mutation, failing check, placeholder, or missing required test remains.
