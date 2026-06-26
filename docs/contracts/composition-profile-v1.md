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

The reusable region contract is [`region-v1.schema.json`](region-v1.schema.json). A profile may inline compatible region objects or reference region fragments compiled into the profile bundle.

DP and TP headers must be modeled separately. Use tags such as `dp-header` and `tp-header`; do not rely on one generic `header` tag for processor/write policy decisions.

Display, TP HW, and TP FW policies are enforced by the compiler, not only by UI visibility. General Replace may author explicit ranges only where the profile enables them and never through protected regions.

## General mappings and saved rules

General Merge and General Replace use runtime [`explicitMappings`](composition-request-v1.md). Each mapping compiles to a normal `copy-range` or `replace-range` operation and passes the same bounds, equal-length, overlap, alignment, region-access, atomicity, and processor-dependency checks. Scripts, commands, and undeclared processors are forbidden.

Validated general mappings may be promoted to saved rules. A saved rule is a versioned profile fragment, not a script. Its schema is [`saved-composition-rule-v1.schema.json`](saved-composition-rule-v1.schema.json). Promotion requires compatibility checks, protected-range policy, deterministic operation fragments, owner/reviewer metadata, and golden evidence when firmware semantics are affected.

## Operation order and overlap

Operation order is declared by `operations[].sequence`; it is not hard-coded by workflow type. AB Merge may copy a DP_AB container first when the source artifact intentionally covers broader layout portions. Normal Merge should normally copy DP and TP from separate input slots into non-overlapping target regions.

Overlap defaults to `reject`. Any overlap must be explicitly declared by `overlapPolicy` and explained by validation rules and preview/mutation reports.

## Integrity outcome and processor authority

`integrityDisposition` (`none`, `verify-existing`, `recalculate-and-write`) is distinct from `processorInvocation.authority` (`calculate`, `transform`). `unknown` is evidence-only and rejected by the supported-profile compiler.

Production CRC/Header transforms may use an external combiner tool binding. Profiles reference logical binding metadata; executable path, SHA-256, argument template, timeout, and platform are defined by the external combiner tool manifest.

## Semantic validation beyond JSON Schema

The compiler also enforces unique ids, reference integrity, source/target length compatibility, checked bounds, address-space mutability, composition/initializer compatibility, experience access rules, deterministic operation order, processor/tool registration/version/read-write authority, protected-range behavior, and complete output token extraction.
