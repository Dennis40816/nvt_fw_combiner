# Codex Handoff Plan

## Objective

Codex receives a constrained repository with executable rules and bounded issues, not a prompt to build the whole application. Humans retain authority over firmware semantics, exact header/CRC transform behavior, private golden outputs, and release permissions.

## Start every session

1. Read root and nearest `AGENTS.md`, `SPEC.md`, the issue, relevant ADR/contracts, and matching skill.
2. Run `python scripts/verify.py --structure-only` before architecture work.
3. Install the pinned SDK with `scripts/install-dotnet.ps1` or `.sh` and run `python scripts/verify.py --all` before completion when possible.
4. Apply `polytail` to every non-trivial change.
5. Keep network disabled by default and never expose signing secrets or real firmware to ordinary tasks.

## Branch and PR handoff

- Use the active milestone branch, for example `0.1.0`, or a short-lived `feature/<topic>` branch based on it.
- Do not merge directly to `main` from an agent session. `main` receives changes only by reviewed PR merge.
- Every agent PR description must include:
  - summary;
  - affected layers and contracts;
  - risk class `R0` through `R3`;
  - verification commands and results;
  - firmware/private evidence still missing;
  - required human reviewers;
  - target branch and intended milestone.
- Implementer Polytail and reviewer Polytail are both required before approval.
- `R2` PRs need architecture/contract review. `R3` PRs need firmware-owner review and byte-level evidence before merge.
- If a tool can push commits but cannot create or merge a PR, it must leave a PR-ready handoff instead of treating the branch as complete.
- Squash/rebase/merge commits are allowed only after review and must not hide unreviewed generated files, firmware payloads, or schema/profile drift.

## Review checklist for Codex-authored PRs

1. Confirm the diff follows root and nested `AGENTS.md`.
2. Confirm the change is scoped to the issue and milestone.
3. Confirm no direct writes to `main` are required.
4. Confirm no UI/CLI code duplicates firmware semantics.
5. Confirm every processor/tool mutation has declared read/write ranges and host-side diff validation.
6. Confirm `1.10`-style external tool versions remain strings.
7. Confirm tests cover at least one failure path for every new risky rule.
8. Confirm docs, schemas, C# contracts, and tests agree.
9. Confirm `python scripts/verify.py --all` was run or the blocker is stated explicitly.
10. Confirm `PASS` is not issued when private golden evidence or firmware-owner review is still required.

## Bounded issue sequence

1. **Bootstrap exit** — prove clean clone SDK install, restore/build/test, package locks, CI check names, and app shell smoke.
2. **Dev0 contract close** — DP/TP header split, saved rule schema, operation order/overlap policy, external combiner tool runner contract, and proof primitives.
3. **UI planning** — 0.1.1 demo shell design, terminal/log/report UX, diagnostics model, no firmware semantics in ViewModels.
4. **Range/address-space/region primitives** — checked half-open ranges, region catalog, atomicity and access-policy tests.
5. **Initialization and engine skeleton** — blank/reference initializers feeding one `CompositionEngine`.
6. **Profile/request/report compiler** — strict schemas, semantic validation, stable issues, plan hash.
7. **Protocol 1 CRC calculation** — C# adapter, limits, vectors, contract tests.
8. **External combiner runner** — staged `work.bin`, tool manifest resolution, executable SHA-256 verification, timeout, changed-range diff validation.
9. **Standard Merge parity** — one IC/mode per PR with approved golden evidence. Current owner priority includes NT51950 and NT51951 after memory maps are supplied.
10. **Normal Replace parity** — DP Replace and CtrlRAM Replace workflows first, with IC num `single`/`cascade` modes required in UI/request models, `numeric` reserved for future IC exceptions, and legacy `combiner.exe` CRC/header post-processing once the owner supplies invocation details.
11. **AB Merge parity** — banks, relocation, integrity stages, output comparisons. Deferred until the owner reactivates AB work.
12. **General modes and saved rules** — one mapping model/editor/compiler for Merge and Replace plus rule promotion.
13. **Packaging/security** — minimal Windows package, clean-machine smoke, SBOM/provenance/signing.

Each issue must state acceptance tests, forbidden scope, human gates, and tag/milestone impact.
