# Contributing

This document is a proposed repository baseline. The repository is expected to use trunk-based development with protected `main` and short-lived branches.

## Change sequence

1. Link the change to an issue with explicit acceptance criteria.
2. Read root and nested `AGENTS.md` files.
3. Update an ADR first when the change alters an architectural decision.
4. Implement the smallest coherent change.
5. Add tests at the same time as behavior.
6. Run `python scripts/verify.py --all`.
7. Open a PR using a Conventional Commit style title.

## Pull request evidence

Each PR must state:

- what changed and why;
- affected ICs, modes, profiles, ranges, and contracts;
- tests and exact commands run;
- golden hashes affected or explicitly unaffected;
- compatibility and release impact;
- remaining risks.

Firmware-semantic changes require human byte-level review. Generated output screenshots are not a substitute for golden regression.
