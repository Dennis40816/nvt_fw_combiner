# Saved General Rule Promotion

General Merge and General Replace are core authoring features. They are not disposable manual modes.

A general mapping can be saved, reviewed, and promoted into a reusable rule that appears in normal workflows.

## Terms

| Term | Meaning |
| --- | --- |
| General mapping | User-authored set of source-to-target mappings created in General Merge or General Replace. |
| Saved rule | Versioned profile fragment generated from a validated general mapping. |
| Promotion | Review process that makes a saved rule selectable from normal profile/catalog workflows. |
| Compatibility envelope | IC/mode/profile conditions under which the saved rule may be used. |

## Core principle

A saved rule is data, not code.

It must never contain shell commands, executable paths, script bodies, or hidden byte mutation logic. It compiles to the same operation algebra as any built-in profile:

```text
copy-range
replace-range
fill-range
patch-scalar
run-external-processor
assert-range
validate-checksum
```

## Saved rule fields

Recommended model:

```text
SavedCompositionRule
  ruleId
  ruleVersion
  displayName
  description
  compositionKind: merge | replace
  sourceExperience: general-merge | general-replace
  compatibleProfileIds[]
  compatibleIcIds[]
  requiredInputSlotTemplates[]
  mappingRows[]
  operationFragments[]
  processorDependencies[]
  validationRuleIds[]
  protectedRangePolicy
  owner
  reviewers[]
  supportStatus: draft | candidate | supported | deprecated
  evidenceRefs[]
```

## Mapping row fields

```text
MappingRow
  rowId
  sourceBindingId or sourceSlotTemplateId
  sourceRange
  targetAddressSpaceId
  targetRange
  targetRegionId?
  overlapPolicy
  alignment
  reason
```

## Promotion gates

A general mapping can become a saved rule only after:

1. The profile compiler validates every range and operation.
2. It does not cross forbidden/protected regions.
3. It satisfies persona access policy for the target normal workflow.
4. It has no undeclared overlap.
5. It declares processor/tool dependencies if it changes any integrity/header range.
6. It has deterministic operation order.
7. It passes synthetic tests.
8. It has golden evidence when firmware semantics are affected.
9. It is reviewed by the relevant owner for `R2`/`R3` risk.

## Catalog behavior

A promoted saved rule may appear as:

- a named option inside Standard Merge;
- a named option inside AB Merge;
- a named option inside DP Replace / CtrlRAM Replace;
- an advanced preset inside General Merge / General Replace.

The rule is available only when its compatibility envelope matches the current IC/profile/mode.

## Current implementation status

As of `0.7.3`, saved-rule support is intentionally limited to data validation, mapping projection, operation provenance, and General Merge CLI consumption.

Implemented:

- `saved-rule validate <rule.json>` parses the strict saved-rule shape and rejects unknown fields, duplicate ids, invalid ranges, invalid compatibility, command/script hooks, and unsafe row shapes.
- General Merge saved-rule validation currently accepts only reviewed `copy-range` operation fragments, requires every mapping row to be referenced by exactly one supported operation fragment, rejects dangling slot-template references, rejects rows outside the current `output-image` / `reject` overlap consumption envelope, rejects root or fragment processor dependencies, and treats `protectedRangePolicy` as a scalar schema enum only.
- `saved-rule mappings <rule.json>` prints normalized mapping rows and CLI mapping fragments without reading or writing firmware bytes.
- `general-merge preview|build --rule <rule.json> --slot <slot-id=path>` consumes General Merge saved-rule rows only after explicit slot binding, then compiles them through the same General Merge planner/executor as manual mappings.
- Reports mark rule-driven operations with `Provenance.Kind = "saved-rule"` plus rule id/version.

Not implemented yet:

- UI Saved Rules navigation or rule authoring.
- Promotion into Standard Merge, DP Replace, CtrlRAM Replace, or any normal workflow catalog.
- General Replace saved-rule execution. TP-touching Replace still needs explicit postbuild policy and golden evidence before rule consumption is enabled.

## Versioning

Saved rules use semantic versions. Any change to target ranges, processor dependencies, overlap policy, or output bytes is at least a minor version bump and may require golden re-approval. Breaking compatibility with existing profile contexts requires a major version bump or a new rule id.

## Audit

Reports must show whether an operation came from:

- built-in profile;
- runtime general mapping;
- saved rule;
- external processor.

This prevents a promoted rule from becoming invisible magic.
