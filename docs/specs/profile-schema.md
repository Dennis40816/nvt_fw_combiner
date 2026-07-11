# Profile Schema Summary

The canonical versioned contract is [composition-profile-v1](../contracts/composition-profile-v1.md). This document carries the product-level schema expectations summarized from `SPEC.md`.

## Top-Level Shape

```json
{
  "schemaVersion": "1.0",
  "profileId": "nt51950-dp-replace-v1",
  "profileVersion": "1.0.0",
  "supportStatus": "candidate",
  "icId": "NT51950",
  "modeId": "dp-replace",
  "compositionKind": "replace",
  "experience": {},
  "image": {},
  "inputSlots": [],
  "addressSpaces": [],
  "regions": [],
  "views": [],
  "operations": [],
  "validations": [],
  "outputNaming": {}
}
```

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

Input slots declare role, requirement, cardinality, accepted extensions, size/content guards, and compatibility tags. General modes instantiate extensible slots into stable runtime `bindingId` values. A filename is never IC or range truth.

Region access is deny-by-default:

- `hidden`: not shown or authorable.
- `read-only`: may be displayed but cannot be mapped.
- `whole`: only complete region replacement/mapping.
- `parts`: only declared sub-regions.
- `explicit-range`: general authoring may create ranges after constraints pass.

The compiler, not UI pre-filtering, is the authority for experience, region policy, atomicity, overlap, and processor dependencies.

## External Processor Binding

A `run-external-processor` operation declares the exact target address space/range, integrity disposition, processor authority, allowed read/write ranges, tool binding, adapter id, typed parameters, and fail-closed policy. Documentation examples never authorize production ranges; supported profiles require owner-approved ranges and golden evidence.
