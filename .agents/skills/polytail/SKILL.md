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
4. Identify the branch, target branch, PR/review status, and whether the change is allowed to merge to `main`.
5. Inventory every production-admission dimension touched by the change and classify it as `byte-authoritative` or `informational`, with the independent profile/contract/evidence source for each authoritative gate.

## Reject low-quality patterns

Fail the review when any of the following appears without an approved, narrowly documented reason:

- TODO, placeholder, `NotImplemented`, dead branch, mock production result, or silent fallback in a completed feature;
- duplicated merge/replace semantics in UI, CLI, worker, profile compiler, or a one-off script;
- magic offsets, anonymous integer ranges, ambiguous inclusive ends, unchecked arithmetic, or implicit byte order;
- broad `catch`/`except`, swallowed errors, unstable error text in place of issue codes, or success returned after partial failure;
- tests that only assert constants, mirror the implementation, delete coverage, loosen expected output, or omit failure cases;
- IC-family selection influenced by PID, firmware version, hash, filename, chip count, or other metadata instead of the requested IC and owner-declared family membership;
- production routing or validation gated by golden-fixture identity such as a whole-file hash, exact PID, exact TP FW/Common FW version, filename, or observed chip count without independent byte-layout, processor, or profile authority;
- a single runtime postbuild profile narrowed to one Common FW version, an evidence-only profile creating a runtime version boundary, or multiple runtime profiles modeled as exact golden versions instead of ordered effective-version intervals beginning at `1.0.0`;
- generic cascade narrowed to a golden's observed count, overlapping count ranges, or an exact-count/count-range branch with no distinct owner-provided command plan;
- analyzer/lint suppressions, threshold reductions, exclusions, or disabled tests added only to make CI green;
- speculative abstractions with no current caller, service-locator/global mutable state, unnecessary wrappers, or oversized god modules;
- mutation of caller-owned input buffers, direct writes to user firmware by Python or legacy `combiner.exe`, undeclared write ranges, or overlap hidden by copy order;
- generated files, firmware payloads, credentials, release output, or unrelated refactors mixed into the change;
- documentation, schema, profile, report, and implementation semantics that disagree;
- direct or unreviewed merge into `main`, missing PR summary, missing reviewer, or missing required human gate.

## Architecture audit

- Confirm Merge and Replace use the same composition engine; only image initialization differs.
- Confirm UI/CLI only produce typed requests and never implement firmware rules.
- Confirm every operation names source/target address spaces and half-open ranges.
- Confirm external processors can modify only a host-created staging copy and the host independently verifies changed ranges.
- Confirm integrity outcome (`none`, `verify-existing`, `recalculate-and-write`) and processor authority (`calculate`, `transform`) are explicit and separate; `unknown` cannot compile as supported behavior.
- Confirm custom layouts compile to the same profile/operation model and cannot execute arbitrary scripts.
- Confirm external combiner versions such as `1.10` remain exact strings and are resolved only through approved tool manifests.

## Evidence audit

1. Require the narrowest meaningful unit/property/contract/profile/golden tests.
2. Verify negative cases for bounds, overflow, overlap, malformed profile, worker/tool failure, out-of-policy writes, and interrupted output.
3. For each informational value present in a golden case, require a production-route test that varies or omits it while preserving the declared byte-authoritative facts. PID, TP FW, hash, and filename remain informational; Common FW selects only between multiple runtime profiles whose distinct execution is independently established.
4. Verify Common FW selection as ordered effective-version intervals: the first runtime profile starts at `1.0.0`, the next profile starts a new interval, the final profile has no upper bound, and evidence-only profiles do not participate. A single runtime profile must not require Common FW to route.
5. Treat `Number > 1` as cascade classification unless distinct owner-provided commands establish exact-count or non-overlapping count-range plans. Verify both sides of every declared count boundary and do not confuse a golden count with a production plan.
6. Verify family selection depends only on the requested IC plus explicit owner-declared family/alias membership; metadata variation must not change family identity.
7. Run the canonical affected checks, then `python scripts/verify.py --all` before completion when the environment supports it.
8. Do not claim completion when required private golden or clean-machine evidence is unavailable; state the missing gate explicitly.
9. For PR approval, verify implementer and reviewer are distinct or record the explicit owner exception.

## Review output

Return a concise report with:

```text
Risk class
Branch and target branch
PR/review status
Architecture fit
Correctness findings
Test quality findings
Security/release findings
Production admission matrix (dimension, authority, informational variants tested)
Required fixes
Commands and results
Residual evidence gaps
Verdict: PASS | PASS-WITH-HUMAN-GATE | FAIL
```

`PASS` is forbidden while any P0/P1 finding, undeclared firmware mutation, failing check, placeholder, missing required test, unreviewed path to `main`, or missing required human review remains.
