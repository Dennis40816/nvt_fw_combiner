# Profile Schema Summary

The accepted target contract is
[composition-profile-v2](../contracts/composition-profile-v2.md), paired with the canonical
[firmware-family-v1](../contracts/firmware-family-v1.md) physical map. The current production loader
continues to use [composition-profile-v1](../contracts/composition-profile-v1.md) during compatibility
migration. V1 becomes migration evidence only after v2 trusted-bundle loading and parity gates pass.

## Top-Level Shape

```json
{
  "schemaVersion": "2.0",
  "profileId": "nt51950-dp-replace",
  "profileVersion": "1.0.0",
  "promotion": {},
  "compositionKind": "replace",
  "experience": {},
  "mapBinding": {},
  "inputSlots": [],
  "spaces": [],
  "views": [],
  "metadataBindings": [],
  "regionAccessRules": [],
  "operations": [],
  "validations": [],
  "processorStages": [],
  "output": {},
  "evidenceRefs": []
}
```

Physical regions, capacities, metadata locators, capability facts, and aliases exist only in the
firmware-family document. A profile references their stable ids and cannot relax map safety.

## Supported Experiences

| Experience | Composition | Initializer | Audience | Layout |
| --- | --- | --- | --- | --- |
| `standard-merge` | Merge | blank | system | fixed |
| `ab-merge` | Merge | blank | system | fixed |
| `general-merge` | Merge | blank | advanced | user-defined |
| `dp-replace` | Replace | reference | dp | constrained |
| `ctrlram-replace` | Replace | reference | ctrlram | constrained |
| `general-replace` | Replace | reference | advanced | user-defined |

The table is a product catalog baseline, not an executor enum. New experiences reuse the same orthogonal fields.

## Inputs and Region Access

Input slots declare artifact class, role, cardinality, accepted extensions, and one closed length/
normalization policy. TP firmware is capped by `tp-maximum-256k`; Normal DP extraction warns on outer
size mismatch but requires every declared view in bounds; whole-DP and reference inputs bind exact
resolved-map capacity. Original filenames are always trace metadata and never IC/range truth.

Region access is deny-by-default:

- `hidden`: not shown or authorable.
- `read-only`: may be displayed but cannot be mapped.
- `whole`: only complete region replacement/mapping.
- `parts`: only declared sub-regions.
- `explicit-range`: general authoring may create ranges after constraints pass.

The compiler, not UI pre-filtering, is the authority for experience, region policy, atomicity, overlap, and processor dependencies.

## External Processor Binding

A `run-processor` operation references a closed processor stage. `crc-worker-v1` is calculation-only
with no write views. `legacy-combiner-v1` is transform-only, references a trusted tool binding and
invocation profile, and has at least one declared write view. Paths, commands, scripts, argument
templates, and arbitrary parameter objects are forbidden. Documentation examples never authorize
production ranges; supported profiles require owner-approved ranges and golden evidence.
