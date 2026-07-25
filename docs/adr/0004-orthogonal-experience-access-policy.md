# ADR 0004: Keep Experience and Region Access Orthogonal to Execution

- Status: Accepted
- Date: 2026-06-25
- Amended by: ADR 0015

## Context

The product needs Standard/AB/General Merge and DP/CtrlRAM/General Replace. Encoding every page as a workflow-specific executor or closed `workflowFamily` would force core changes whenever a new role is introduced.

## Decision

The core uses orthogonal `compositionKind`, image initializer, experience metadata, layout policy and region access rules. Canonical IC regions are defined once. Persona-specific profiles authorize whole regions, declared parts or explicit ranges. All approved mappings compile to the same operation algebra.

## Consequences

- New experiences normally add catalog/profile/UI policy, not an engine.
- DP Replace, CtrlRAM Replace and General Replace can share one canonical memory map without exposing unsafe regions.
- Profile compiler becomes the enforcement point for atomicity/access.
- Report records experience for audit but executor output cannot depend on UI labels.

## Rejected alternatives

- Separate Merge/Replace/persona executors: duplicates byte semantics.
- UI-only filtering: can be bypassed by CLI/request input.
- Filename-based CtrlRAM/DP detection: not authoritative.
- Arbitrary scripts for General mode: unreviewable and unsafe.
