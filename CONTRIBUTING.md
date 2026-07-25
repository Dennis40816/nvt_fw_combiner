# Contributing

This repository uses protected `main`, version integration branches named with
the exact planned release (for example `0.8.1`), and short-lived feature
branches. ADR 0038 defines one bounded exception for the `0.10.x`
maintainability program.

## Branch model

1. Start each release on its owner-selected version integration branch, such as `0.8.1`.
2. Commit tightly coupled release work directly to that version branch in independently verifiable phases.
3. Create `feature/<version>/<topic>` from the version branch only for an independently reviewable feature; merge it back to the same version branch after review.
4. Open the only `main` PR after the version branch reaches its defined scope and final review gates.

For the ADR 0038 maintainability-program exception, `0.10.x` is a non-release
program integration branch. A subordinate exact-version stage such as `0.10.1`
branches from `0.10.x`; its feature PRs return to that stage, and the completed
stage merges by reviewed PR into `0.10.x`. A bounded program-wide feature may
target `0.10.x` directly. Only the final owner-approved `0.10.x` PR targets
`main` and enters release workflow.

## Change sequence

1. Link the change to an issue with explicit acceptance criteria.
2. Identify the version integration branch and, if needed, create the feature branch from it.
3. Read root and nested `AGENTS.md` files.
4. Update an ADR first when the change alters an architectural decision.
5. Implement the smallest coherent change.
6. Add tests at the same time as behavior.
7. After every independently verifiable phase, run its narrow test, review the exact staged files, and create a Conventional Commit. Do not stage unrelated worktree changes.
8. Run the final gate selected by `AGENTS.md`: `python scripts/verify.py --all` for `R1`-`R3`, or `python scripts/verify.py --structure-only` for a qualifying `R0` documentation/governance-only change.
9. Merge feature work to its owner-approved integration branch. Normally the
   completed exact-version branch then opens its final PR to `main`; under ADR
   0038, a subordinate stage opens its integration PR to `0.10.x`, and only the
   completed program branch opens the final PR to `main`.

## Pull request evidence

Each PR must state:

- what changed and why;
- affected ICs, modes, profiles, ranges, and contracts;
- tests and exact commands run;
- golden hashes affected or explicitly unaffected;
- compatibility and release impact;
- remaining risks.

Firmware-semantic changes require human byte-level review. Generated output screenshots are not a substitute for golden regression.
