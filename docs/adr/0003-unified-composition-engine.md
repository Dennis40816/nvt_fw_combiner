# ADR 0003: Use one composition engine for Merge and Replace

- Status: Accepted for repository bootstrap
- Date: 2026-06-25
- Owners: Product owner + architecture owner + firmware reviewer

## Context

Standard Merge, AB Merge, General Merge, DP Replace, CtrlRAM Replace, and General Replace all initialize and mutate firmware images. Separate executors would duplicate range checks, overlap semantics, processors, reporting, and output atomicity.

## Decision

Implement one typed `CompositionEngine` and one operation algebra. The only fundamental composition split is image initialization:

- Merge creates a blank image of declared capacity and fill byte.
- Replace clones exactly one required reference/base image.

After initialization, every experience compiles to the same deterministic `CompositionPlan`, checked mutation API, external-processor port, validation engine, report contract, and atomic output writer.

Experience identifiers are catalog/UI/profile-policy metadata:

```text
standard-merge
ab-merge
general-merge
dp-replace
ctrlram-replace
general-replace
```

The executor must not branch on these identifiers. General modes produce normal `copy-range` or `replace-range` operations from typed `explicitMappings`; they do not execute scripts or bypass the profile compiler.

## Consequences

### Positive

- One byte-execution semantics and one safety surface.
- Merge/Replace differences remain stable and explicit.
- Persona UX and customer-specific layouts do not create one-off engines.
- Every byte change receives the same trace, validation, and processor policy.

### Trade-offs

- The profile/compiler and canonical region model must precede most UI behavior.
- The mapping editor needs strict round-trip and plan validation.
- Convenience behavior must be modeled as experience policy or profile data, not hidden code.

## Verification

- Architecture tests prohibit mutation logic in UI/CLI and workflow-specific executors.
- Blank initialization contains only fill bytes before operations.
- Reference initialization is byte-identical to the immutable base before operations.
- Equivalent compiled operations produce equivalent output regardless of authoring experience.
- Property/contract tests cover bounds, overlap, ordering, access policy, processors, and failed-run atomicity.
