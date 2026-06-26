# Codex Handoff Plan

## Objective

Codex receives a constrained repository with executable rules and bounded issues, not a prompt to build the whole application. Humans retain authority over firmware semantics, exact header/CRC transform behavior, private golden outputs, and release permissions.

## Start every session

1. Read root and nearest `AGENTS.md`, `SPEC.md`, the issue, relevant ADR/contracts, and matching skill.
2. Run `python scripts/verify.py --structure-only` before architecture work.
3. Install the pinned SDK with `scripts/install-dotnet.ps1` or `.sh` and run `python scripts/verify.py --all` before completion when possible.
4. Apply `polytail` to every non-trivial change.
5. Keep network disabled by default and never expose signing secrets or real firmware to ordinary tasks.

## Bounded issue sequence

1. **Bootstrap exit** — prove clean clone SDK install, restore/build/test, package locks, CI check names, and app shell smoke.
2. **Range/address-space/region primitives** — checked half-open ranges, region catalog, atomicity and access-policy tests.
3. **Initialization and engine skeleton** — blank/reference initializers feeding one `CompositionEngine`.
4. **Profile/request/report compiler** — strict schemas, semantic validation, stable issues, plan hash.
5. **Protocol 1 CRC calculation** — C# adapter, limits, vectors, contract tests.
6. **Protocol 2 staged transform** — blocked until owner supplies exact command, fields/order, applicability and minimum ranges.
7. **Standard Merge parity** — one IC/mode per PR with approved golden evidence.
8. **AB Merge parity** — banks, relocation, integrity stages, output comparisons.
9. **Persona Replace** — Display, TP HW, TP FW compiler policies and UI.
10. **General modes** — one mapping model/editor/compiler for Merge and Replace.
11. **Packaging/security** — minimal Windows package, clean-machine smoke, SBOM/provenance/signing.

Each issue must state acceptance tests, forbidden scope, human gates, and tag/milestone impact. Implementer and independent reviewer both run Polytail.
