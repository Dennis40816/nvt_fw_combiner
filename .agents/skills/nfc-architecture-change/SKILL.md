---
name: nfc-architecture-change
description: Review or design changes that affect NVT FW Combiner layers, dependencies, domain boundaries, ports/adapters, public contracts, or ADRs. Do not use for a local implementation-only edit with no architecture impact.
---

# NFC Architecture Change

1. Read root `AGENTS.md`, the nearest nested instructions, the implementation spec, and existing ADRs.
2. Identify the use case, owning layer, input/output contracts, and dependency direction.
3. List invariants: range semantics, deterministic output, mutation ownership, offline behavior, and traceability.
4. Check whether the change can be expressed through an existing profile, operation, port, or adapter before adding a new abstraction.
5. Reject UI-owned firmware logic, infrastructure-owned business rules, and production dependencies on `refcode/`.
6. Write or update an ADR when the change is durable, cross-cutting, difficult to reverse, or changes a public contract.
7. Define architecture tests and narrow behavioral tests before implementation.
8. Produce: decision, alternatives, consequences, migration path, test plan,
   and release impact.
9. Require an independent architecture/contract reviewer for R2 changes. Run
   the narrow architecture tests, `$polytail`, and the final canonical gate.
