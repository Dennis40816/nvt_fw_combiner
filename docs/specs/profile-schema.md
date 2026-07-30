# Profile Schema Summary

The accepted target contract is
[composition-profile-v2](../contracts/composition-profile-v2.md), paired with the canonical
[firmware-family-v1](../contracts/firmware-family-v1.md) physical map. Current
production routes load trust-anchored V2 bundles. The
[composition-profile-v1](../contracts/composition-profile-v1.md) contract is
retained as historical migration evidence only and cannot own a new production
route.

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
  "inputSelectionGroups": [],
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

Input slots declare artifact class, role, cardinality, accepted extensions, and
one closed length/normalization policy. Canonical Initial Code, DP, TP, LDC,
TPA, and TPB section sources use `source-view-coverage`: every selected
source/metadata/validation/processor read must be in bounds, while optional
expected outer lengths produce diagnostics only. A compatible same-IC
FlashCode may provide a section window. Whole-container DP AB and Reference
inputs bind an exact declared capacity. `tp-maximum-256k` and
`normal-dp-extract-with-warning` are temporary migration tokens under ADR 0045,
not new-profile vocabulary. Original filenames are always trace metadata and
never IC/range truth.

An input selection group references existing slot definitions and declares
`minimumSelected` plus `maximumSelected`. Member slots retain their own
`zero-or-one` cardinality; the group is evaluated only across members
applicable to the resolved map variant. The resolved plan stores definition
references plus selection/readiness state and does not copy slot or operation
definitions. Group policy participates in capability identity but does not
create another route.

NT51928 is the first dual-capacity case. Standard Merge keeps DP and TP
required. LDC absence selects the shared Initial-Code/TP `0x40000` candidate;
supplied `ldc-input` selects the LDC-capable `0x80000` candidate and must then
pass structural validation. Failure blocks and never falls back to absence.
DP Replace selects those same declared variants from an accepted `0x40000` or
`0x80000` Reference. Its `initial-code-replacement` and `ldc-replacement`
group requires one or two applicable selections. On the `0x40000` variant LDC
is `NotApplicable`, remains visible with the reason that Reference does not
contain LDC, and stale/manual LDC bindings are rejected.
Reference length is exact because Reference is the cloned complete container.
Initial Code and LDC replacement inputs are address-bearing section sources and
need only cover their selected canonical views.

Structural acceptance remains blocking. A profile may separately reference a
warning-only `non-uniform-region` validation over a canonical Initial Code,
DP, or LDC source view. A view containing only one distinct byte emits a typed
warning without changing map resolution, readiness, execution admission, or
output bytes. This validation is explicit profile authority, never a global
inference from artifact class.

Region access is deny-by-default:

- `hidden`: not shown or authorable.
- `read-only`: may be displayed but cannot be mapped.
- `whole`: only complete region replacement/mapping.
- `parts`: only declared sub-regions.
- `explicit-range`: general authoring may create ranges after constraints pass.

The compiler, not UI pre-filtering, is the authority for experience, region policy, atomicity, overlap, and processor dependencies.

Count-dependent DiffDLM composition is a canonical family/profile fact, not a
list of duplicated per-count operations. A profile references the record
policy, target anchor, stride, writable subrange, preservation mask, and IC
Count applicability. Compilation expands only the active records for the
resolved count. Input admission requires every active source record in full,
including its preserved mask bytes. Source records after that complete active
prefix are non-authoritative dummy content and cannot replace inactive target
records. Any separately postbuilt FWConfig Backup placement uses its own
bounded processor placement authority and postcondition; it is not part of the
DiffDLM scatter allowance. After the actual Backup is located, the final
immutable-reference audit permits differences only in the original Reference
Backup envelope and actual Backup envelope, not the entire placement-candidate
range.
Dynamic-placement profiles declare the count-derived postcondition separately.
A fixed-layout profile instead declares its fixed End Flag/Backup fact once;
it must not inherit a dynamic alignment formula by family analogy.

## External Processor Binding

A `run-processor` operation references a closed processor stage. `crc-worker-v1` is calculation-only
with no write views. `legacy-combiner-v1` is transform-only, references a trusted tool binding and
invocation profile, and has at least one declared write view. Paths, commands, scripts, argument
templates, and arbitrary parameter objects are forbidden. Documentation examples never authorize
production ranges; supported profiles require owner-approved ranges and golden evidence.
