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
  outputInitialization?                 # General Merge only
    capacity
    fillByte
  promotion
  slotTemplates[]
  mappingFragments[]
  accessEnvelope
    maximumMappingCount
    maximumTotalWriteBytes
    per-slot input-length narrowing
  validationRuleIds[]
  processorStageIds[]
  owner
  reviewers[]
  evidenceRefs[]
```

For General Merge, the saved rule closes over the exact logical-output
capacity and blank fill byte together with its mappings. A consumer cannot
silently override either value. Changing capacity or fill produces a new rule
revision because it changes bounds, the compilation fingerprint, Preview
identity, and potentially output bytes. A future parameterized rule must
declare a reviewed typed parameter and bounds; an unrelated UI/CLI value is
not an implicit parameter.

General Replace has no saved blank initializer. It continues to clone the
required immutable Reference, and the exact parent/map compatibility plus the
accepted Reference resolves output capacity.

The accepted future General Replace execution boundary is parent-authority
only. A rule may execute only after its exact Trusted Parent resolves and the
parent already owns every authorable region, protected-range decision,
required POSTBUILD stage, processor/tool binding, parameter, and allowed-write
range. The rule and its access envelope may narrow those facts but cannot add,
replace, omit, or broaden them. The compiled parent decides whether the
resolved mapping targets require POSTBUILD; the rule cannot turn that stage on
or off. Processor-free mappings execute only when the parent admits that exact
shape, while TP-touching mappings remain blocked until the parent stage,
golden evidence, and applicable firmware-owner gate are satisfied.

The rule stores only the exact parent binding identity; it never embeds or
duplicates the Trusted Profile/Family/Bundle documents. Importing a rule does
not import trust. The exact parent bundle must be installed and verified
through the Trusted Catalog independently, or the rule remains incompatible
and review-required.

## Local authoring and published identity

A local saved-rule file is an editable authoring document. After loading it,
the user may change its contents and save back to the same file path. This is
Save-in-place, not creation of an unrelated rule. The path is storage metadata,
not rule identity, and the rule may retain the same `ruleId` and display name.

Published identity is immutable. It is the reviewed rule id, semantic version,
and canonical content hash together with the exact parent binding. When any
semantic content changes:

1. the canonical content hash changes;
2. the local working copy returns to `Draft`;
3. prior approval, review, evidence attachment, promotion, and trusted status
   do not carry forward;
4. the modified draft cannot impersonate an installed rule with the same id
   and version; and
5. republishing requires a new `ruleVersion` and the applicable promotion
   gates.

Save-in-place may therefore overwrite the editable local JSON file, but it
never mutates an already installed Trusted Catalog snapshot. Installed
versions remain immutable and addressable by their original id, version,
content hash, and parent binding. Historical reports keep those same values,
so later edits to the authoring path cannot change the meaning of an earlier
build.

A rule opened from an installed Trusted Catalog entry is read-only. Choosing
Edit creates an editable working copy outside Catalog-managed storage; it does
not grant Save-in-place authority over the installed file. The copy may retain
the logical `ruleId` and display name, but its first semantic change makes it a
Draft and republication requires a new version. Presentation distinguishes
**Trusted — read only** from **Draft working copy**. Save-in-place applies only
to an ordinary user-owned/imported authoring path.

## Resource-limit authority

Mapping count, total written bytes, and accepted input-file lengths use one
shared limit-resolution model. They are not hard-coded independently by a UI,
CLI command, or Saved Rule consumer:

1. Application owns product-wide technical safety ceilings used to bound
   memory, parsing, and resource consumption. UI and CLI call the same
   readiness/Application service.
2. The exact Trusted Parent owns firmware-semantic and per-slot length
   contracts, including exact, minimum, and maximum lengths when applicable.
3. A Saved Rule `accessEnvelope` may narrow the Parent's mapping count, total
   write bytes, target regions, and per-slot input-length envelope. It cannot
   raise or replace either the Application ceiling or Parent authority.

The effective accepted set is the intersection of all applicable layers.
Diagnostics identify the failed limit, its owner, expected bound, and observed
value. An unreferenced file tail is not rejected merely because it exists; it
is rejected only when the whole file violates the technical ceiling or the
resolved slot length contract. Mapping source ranges still require normal
bounds validation.

The currently pinned v2 Saved Rule schema carries mapping-count and total-write
caps but no per-slot input-length narrowing. Adding that narrowing requires a
versioned schema revision and strict loader/compiler round-trip tests; a
consumer must not invent it from UI state.

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
10. A General Merge rule declares one exact output capacity and blank fill
    byte; a General Replace rule declares neither.
11. Every resolved mapping count, total-write size, and input length is within
    the intersection of Application, Trusted Parent, and rule limits.

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

V1 is read-only compatibility/import authority. No new v1 feature, authoring
flow, or emitted rule is added. Its execution/runtime path may be deleted only
after v2 provides:

1. General Merge output-initializer schema, loader/compiler round trip, and
   deterministic execution;
2. General Replace exact-Trusted-Parent authority, parent-owned conditional
   POSTBUILD behavior, negative tests, and required golden/owner evidence;
3. parity for typed draft, compiled operations, report provenance, and output
   bytes across every production consumer; and
4. an explicit migration outcome for retained v1 documents.

Any temporary v1-to-canonical adapter has one declared purpose, an enumerated
caller set, and executable deletion gates. It translates legacy range syntax
only at the outer import boundary and cannot introduce another executor or
firmware-semantic model.

Implemented:

- `saved-rule validate <rule.json>` parses the strict saved-rule shape and rejects unknown fields, duplicate ids, invalid ranges, invalid compatibility, command/script hooks, and unsafe row shapes.
- Normal General Merge v2 execution first evaluates the complete canonical
  `saved-composition-rule-v2.schema.json` contract, including every governance
  field and closed nested object, then checks exact trusted-parent promotion,
  reviewer, slot-policy, validation/processor reference, and access-envelope
  narrowing. No initializer or mapping fragment materializes into a draft
  before both gates pass.
- General Merge v2 consumption accepts only reviewed `copy-range` mapping
  fragments, preserves their ids in report operations, rejects dangling
  parent/rule slot references, rejects targets outside `general-output`,
  rejects unsupported processor dependencies, and enforces the declared
  mapping-count and total-write limits before compilation.
- `saved-rule mappings <rule.json>` prints normalized mapping rows and CLI mapping fragments without reading or writing firmware bytes.
- `general-merge preview|build --rule <v2-rule.json> --slot <slot-id=path>` consumes the rule's closed output initializer and General Merge mapping fragments only after explicit slot binding. `--size` and `--fill` cannot override that initializer; the resulting draft compiles through the same General Merge planner/executor as manual mappings.
- General Merge v2 rules own exact capacity and arbitrary blank fill. Canonical
  semantic hashing excludes path and display name, while reports retain exact
  rule id/version/hash plus bundle/profile/family/map Parent identity.
- The Application lifecycle use case and constrained atomic filesystem adapter
  perform local Save-in-place and Catalog-to-working-copy writes. Semantic
  edits return to Draft, clear approval/evidence/trust, and require a new rule
  version before publication; configured Catalog roots remain read-only.
- Shared General admission intersects Application, exact Parent, and Saved
  Rule mapping/total-write/resource limits. It independently compares exact
  Parent identity before compilation, so a runner caller cannot forge report
  provenance.
- Selected Saved Rule files retain content-authoritative length/SHA-256
  snapshots and use the shared Reload/Rebind and build-time revalidation path.
- Exact NT51926 single-selector, full-Flash, file-backed General Replace Saved
  Rules may execute DP-only mappings through the existing V2
  runtime-reference compiler and shared `ReplaceRange` engine. Reference is
  immutable, the rule cannot declare processor authority, and every target
  must remain inside the Parent DP region.
- NT51926 TP-touching General Replace remains non-executable. Explicit Preview
  may emit the #253 diagnostic plan-only report for missing Parent stage or
  declared runtime tool; disabled Build exposes readiness without creating a
  Run Report or BIN.
- Reports mark rule-driven operations with `Provenance.Kind = "saved-rule"`
  plus exact rule and Parent identity.

Not implemented yet:

- Trusted Catalog publication/index management and promotion of a local Draft
  into an installed rule snapshot. The implemented storage lifecycle enforces
  configured Catalog immutability but does not grant publication or trust.
- Versioned per-slot input-length narrowing in the serialized v2 schema; the
  current shared admission enforces Parent slot limits and the rule fields
  already defined by the pinned schema.
- UI Saved Rules navigation, rule authoring, and Trusted/Draft state
  presentation.
- Promotion into Standard Merge, DP Replace, CtrlRAM Replace, or any normal workflow catalog.
- General Replace Saved Rule execution outside the exact NT51926 DP-only slice.
  TP/POSTBUILD execution still needs an exact Parent stage, processor
  write-range audit, independent whole-output golden evidence, and
  firmware-owner approval.

## Versioning

Saved rules use semantic versions. Any change to General Merge output
capacity, blank fill byte, target ranges, processor dependencies, overlap
policy, or output bytes is at least a minor version bump and may require
golden re-approval. Breaking compatibility with existing profile contexts
requires a major version bump or a new rule id.

## Audit

Reports must show whether an operation came from:

- built-in profile;
- runtime general mapping;
- saved rule;
- external processor.

Saved-rule provenance includes the exact rule id, version, and canonical
content hash used by the build, not the mutable local file path.

This prevents a promoted rule from becoming invisible magic.
