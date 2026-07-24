# ADR 0001: Use Clean Architecture with explicit firmware-domain boundaries

- Status: Accepted
- Date: 2026-06-25
- Owners: Product owner + architecture reviewer
- Supersedes: None

## Context

The existing tools prove useful firmware behavior, but rules are distributed across Python scripts, TypeScript services, profiles, and UI/runtime surfaces. Memory ranges, copy order, patches, version extraction, CRC, and replace policies must remain deterministic as more ICs are added.

## Decision drivers

- Byte-level correctness and traceability.
- One execution core shared by UI, CLI, and tests.
- Ability to add IC/mode support primarily through profiles.
- Testability without filesystem, process, or UI dependencies.
- Clear ownership for external Python execution.

## Decision

Use these inward dependencies:

```text
Presentation / CLI / Bootstrap -> Application -> Domain
Infrastructure -----------------> Application ports / Contracts
Profiles -----------------------> Domain profile model
```

Rules:

- Domain is pure and deterministic.
- Application owns workflow order, validation, mutation policy, and reports.
- Ports declare external capabilities such as artifact access, clock, output storage, and CRC calculation.
- Infrastructure implements ports for filesystem, JSON, process execution, and packaging.
- Profiles define declarative firmware facts but do not execute code.
- Composition happens only in Bootstrap.
- Architecture tests enforce project references and prohibited namespaces.

## Rejected options

- UI-centric implementation: duplicates semantics and is difficult to regression-test.
- A collection of per-IC scripts: scales poorly and hides shared invariants.
- Direct TypeScript reuse from NFCG: creates a second runtime and weakens the C# architecture boundary.
- Generic plugin execution in v1: expands the attack surface before a plugin security model exists.

## Consequences

### Positive

- Firmware semantics remain independently testable.
- UI can evolve without changing byte behavior.
- External CRC implementation can be replaced behind a port.
- Profiles and golden tests become reviewable product logic.

### Trade-offs

- More projects and mapping types at bootstrap.
- Strict translation between DTO/schema and domain models.
- Architecture tests and ADR discipline are required.

## Verification

- Project-reference architecture tests.
- Namespace/assembly dependency tests.
- UI/CLI parity tests against the same application services.
- Golden regression from profile through output report.
