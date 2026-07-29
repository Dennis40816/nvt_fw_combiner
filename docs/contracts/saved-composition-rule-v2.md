# Saved Composition Rule Contract 2.0

The executable schema is
[`saved-composition-rule-v2.schema.json`](saved-composition-rule-v2.schema.json). A saved rule is a
reviewed, constrained overlay on one trusted General Merge or General Replace profile. It is not an
alternate executor or script format.

## Exact parent binding

Every rule binds the exact parent bundle, profile, and firmware-family ids, versions, and SHA-256
values plus the canonical map id. A changed bundle, profile, family document, or selected map
invalidates the rule until it is reviewed and republished. A separate `mapContentHash` is forbidden
because a map is not an independently hashed artifact. The rule cannot broaden parent region
access, map write constraints, input policy, processor authority, or output naming.

General Merge additionally closes over one `imageInitialization` object. It declares
`kind = blank`, one exact positive `capacity` in the supported in-memory range, and an optional
`fillByte` in `0..255`; omitted fill is exactly `0`. General Replace cannot declare this object.
Normal rule consumption uses the closed-over value and cannot accept an out-of-band capacity or
fill override. Changing capacity or fill changes the rule and compiled output identity.

`slotTemplates` may add declarative BIN slots. `mappingFragments` compile to the parent's normal
`copy-range` or `replace-range` operation. Source ranges are relative to a named parent/rule slot;
targets are offsets within canonical map regions, never absolute unowned writes. Equal length,
bounds, alignment, overlap, atomicity, protected range, and processor dependencies are checked by
the same profile compiler.

`accessEnvelope` can only narrow the parent profile. It caps mapping count and total write bytes and
limits target region ids. `validationRuleIds` and `processorStageIds` reference definitions already
owned by the parent profile; a saved rule cannot introduce processor parameters, commands, paths,
scripts, or new mutation primitives.

Promotion uses the same monotonic stages as composition profiles. Migration or successful parsing
does not promote a rule. `supported` requires an empty blocker list, exact parent compatibility,
owner/reviewer approval, and required golden evidence. Original input names remain trace metadata;
the parent profile remains the sole output naming policy.
