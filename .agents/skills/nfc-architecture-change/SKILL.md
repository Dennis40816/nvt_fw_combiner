---
name: nfc-architecture-change
description: Review or design changes that affect NVT FW Combiner layers, dependencies, domain boundaries, ports/adapters, public contracts, or ADRs. Do not use for a local implementation-only edit with no architecture impact.
---

# NFC Architecture Change

1. Read root and nearest `AGENTS.md`, then the specification/ADRs for the
   affected authority. Follow direct dependencies; expand only when a concrete
   dependency, contradiction or missing decision requires it.
2. Identify the use case, owning layer, input/output contracts, and dependency direction.
3. Identify applicable invariants and failure cases. Include range semantics,
   deterministic output, mutation ownership, offline behavior and traceability
   wherever the change touches them; do not invent a firmware matrix for
   unrelated governance or tooling work.
4. Complete and review the fail-closed
   [capability-reuse gate](../../../docs/governance/development-execution-workflow.md#capability-reuse-gate-fail-closed).
   Confirm the current producer, caller path, port/adapter, tests, proposed
   owner, and dependency direction before adding, changing, moving, wrapping,
   splitting, replacing, or refactoring production behavior, a semantic branch,
   or an owner contract.
   Cross-layer projections may translate typed results but cannot re-derive
   their facts.
5. Reject UI-owned firmware logic, infrastructure-owned business rules, and production dependencies on `refcode/`.
6. Write or update an ADR when the change is durable, cross-cutting, difficult to reverse, or changes a public contract.
7. Define architecture tests and narrow behavioral tests before implementation.
8. Record the decision and consequences in the existing owner. Include
   alternatives, migration, tests and release impact where applicable; use a
   brief not-applicable reason instead of inventing a migration or artifact.
9. Require an independent architecture/contract reviewer for R2 changes. Run
   the narrow architecture tests, `$polytail`, and the final canonical gate.
