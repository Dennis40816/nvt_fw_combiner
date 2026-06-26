# Composition Profile Contract 1.0

The canonical profile compiles every Merge and Replace experience into one composition model. The executable schema is [`composition-profile-v1.schema.json`](composition-profile-v1.schema.json).

## Fundamental rule

- `compositionKind = "merge"` requires `image.initializer.kind = "blank"`.
- `compositionKind = "replace"` requires `image.initializer.kind = "reference"` and exactly one base slot.
- `experience` controls catalog/UI authoring policy only; it does not select a different executor.

## Required groups

```text
identity + supportStatus
compositionKind + experience
image
inputSlots[]
addressSpaces[]
regions[]
views[]
operations[]
validations[]
outputNaming
```

Only a `supported` profile is enabled by default in production, and it requires complete owner/evidence/golden gates.

## Canonical regions and access

A region declares half-open range, semantic tags, atomicity, write policy, alignment, and processor dependencies. `experience.regionAccessRules` is deny-by-default and can expose a region as `hidden`, `read-only`, `whole`, `parts`, or `explicit-range`.

Display, TP HW, and TP FW policies are enforced by the compiler, not only by UI visibility. General Replace may author explicit ranges only where the profile enables them and never through protected regions.

## General mappings

General Merge and General Replace use runtime [`explicitMappings`](composition-request-v1.md). Each mapping compiles to a normal `copy-range` or `replace-range` operation and passes the same bounds, equal-length, overlap, alignment, region-access, atomicity, and processor-dependency checks. Scripts, commands, and undeclared processors are forbidden.

## Integrity outcome and processor authority

`integrityDisposition` (`none`, `verify-existing`, `recalculate-and-write`) is distinct from `processorInvocation.authority` (`calculate`, `transform`). `unknown` is evidence-only and rejected by the supported-profile compiler.

## Semantic validation beyond JSON Schema

The compiler also enforces unique ids, reference integrity, source/target length compatibility, checked bounds, address-space mutability, composition/initializer compatibility, experience access rules, deterministic operation order, processor registration/version/read-write authority, and complete output token extraction.
