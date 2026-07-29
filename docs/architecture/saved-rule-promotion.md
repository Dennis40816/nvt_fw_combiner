# Saved General Rule Promotion

General Merge and General Replace are core authoring features. They are not disposable manual modes.

A general mapping can be saved, reviewed, and promoted into a reusable rule that appears in normal workflows.

## Terms

| Term | Meaning |
| --- | --- |
| General mapping | User-authored set of source-to-target mappings created in General Merge or General Replace. |
| Saved rule | Versioned profile fragment generated from a validated general mapping. |
| Promotion | Review process that makes a saved rule selectable from normal profile/catalog workflows. |
| Compatibility envelope | Exact bundle/profile/family/map binding plus a parent-narrowing access envelope. |

## Core principle

A saved rule is data, not code.

It must never contain shell commands, executable paths, script bodies, processor definitions, output
naming overrides, or hidden byte mutation logic. Each mapping compiles through the parent profile to:

```text
copy-range
replace-range
```

## Saved rule fields

V2 model:

```text
SavedCompositionRule
  ruleId
  ruleVersion
  displayName
  description
  compositionKind: merge | replace
  sourceExperienceId: general-merge | general-replace
  parentBinding
    exact bundle/profile/family ids, versions, hashes
    canonical mapId
  promotion
  slotTemplates[]
  mappingFragments[]
  accessEnvelope
  validationRuleIds[]
  processorStageIds[]
  owner
  reviewers[]
  evidenceRefs[]
```

## Mapping fragment fields

```text
MappingFragment
  fragmentId
  operationKind: copy-range | replace-range
  parent-slot or rule-slot
  sourceRange
  targetRegionId
  targetOffset
  overlapPolicy
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

The accepted target contract is
[`saved-composition-rule-v2`](../contracts/saved-composition-rule-v2.md). General Merge normal
`--rule` execution consumes the v2 initializer and mapping fragments as one closed draft. The
standalone `saved-rule validate` and `saved-rule mappings` commands retain their v1 compatibility
boundary until their complete v2 inspection replacement lands. V2 execution does not enable UI
authoring or promote an existing rule.

Implemented:

- `saved-rule validate <rule.json>` parses the strict saved-rule shape and rejects unknown fields, duplicate ids, invalid ranges, invalid compatibility, command/script hooks, and unsafe row shapes.
- General Merge saved-rule validation currently accepts only reviewed `copy-range` operation fragments, requires every mapping row to be referenced by exactly one supported operation fragment, preserves the reviewed operation fragment id when materializing report operations, rejects dangling slot-template references, rejects rows outside the current `output-image` / `general-output` / `reject` overlap consumption envelope, rejects unaligned rows when `alignment` is declared, rejects root or fragment processor dependencies, and treats `protectedRangePolicy` as a scalar schema enum only.
- `saved-rule mappings <rule.json>` prints normalized mapping rows and CLI mapping fragments without reading or writing firmware bytes.
- `general-merge preview|build --rule <v2-rule.json> --slot <slot-id=path>` consumes the rule's closed output initializer and General Merge mapping fragments only after explicit slot binding. `--size` and `--fill` cannot override that initializer; the resulting draft compiles through the same General Merge planner/executor as manual mappings.
- General Replace saved-rule mapping projection rejects root or fragment processor dependencies until postbuild-aware rule projection is designed and covered by golden evidence.
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
