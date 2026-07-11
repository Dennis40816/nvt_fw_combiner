# Contributing

This repository uses protected `main`, version integration branches named with the exact planned release (for example `0.8.1`), and short-lived feature branches.

## Branch model

1. Start each release on its owner-selected version integration branch, such as `0.8.1`.
2. Commit tightly coupled release work directly to that version branch in independently verifiable phases.
3. Create `feature/<version>/<topic>` from the version branch only for an independently reviewable feature; merge it back to the same version branch after review.
4. Open the only `main` PR after the version branch reaches its defined scope and final review gates.

## Change sequence

1. Link the change to an issue with explicit acceptance criteria.
2. Identify the version integration branch and, if needed, create the feature branch from it.
3. Read root and nested `AGENTS.md` files.
4. Update an ADR first when the change alters an architectural decision.
5. Implement the smallest coherent change.
6. Add tests at the same time as behavior.
7. After every independently verifiable phase, run its narrow test, review the exact staged files, and create a Conventional Commit. Do not stage unrelated worktree changes.
8. Run the final gate selected by `AGENTS.md`: `python scripts/verify.py --all` for `R1`-`R3`, or `python scripts/verify.py --structure-only` for a qualifying `R0` documentation/governance-only change.
9. Merge feature work to the version branch, then open the final PR from that version branch to `main` using a Conventional Commit style title.

## Pull request evidence

Each PR must state:

- what changed and why;
- affected ICs, modes, profiles, ranges, and contracts;
- tests and exact commands run;
- golden hashes affected or explicitly unaffected;
- compatibility and release impact;
- remaining risks.

Firmware-semantic changes require human byte-level review. Generated output screenshots are not a substitute for golden regression.
