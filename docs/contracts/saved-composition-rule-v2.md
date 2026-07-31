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

`parentBinding` is an exact identity/reference, not an embedded copy of the
trusted profile, family, map, or bundle. Moving or importing a rule alone never
imports trust. The separately installed Trusted Catalog must resolve every
declared id, version, and content hash to the exact parent snapshot; a missing
or changed parent makes the rule incompatible and review-required rather than
falling back to a similar profile. Portable distribution therefore transfers
the versioned trusted bundle through its own verification path and the saved
rule through its own rule path.

## Editable file and immutable publication lifecycle

The local JSON path is not part of saved-rule identity. An authoring client may
load a rule, modify it, and save it back to the same path. This overwrites only
the editable working document; it does not mutate an installed Trusted Catalog
entry or a previously published snapshot.

Canonical published identity consists of `ruleId`, `ruleVersion`, the
canonical rule content hash, and the exact `parentBinding`. Any semantic edit
changes the content hash and returns the working document to `Draft`. Previous
approval, review, evidence, promotion, and trusted status are invalid for the
edited bytes. The draft may retain its logical `ruleId` and display name, but
it must not resolve as the installed version with the old hash, and it must
receive a new `ruleVersion` before republication.

Installed versions are immutable. Build/report provenance records the exact
rule id, version, content hash, and parent binding that were resolved at
compile time. Reloading or overwriting the original authoring path cannot
retroactively change an earlier report or trusted version.

The canonical content hash is SHA-256 over the schema-valid semantic JSON
projection. Object properties are written in ordinal name order, array order
is preserved, and JSON scalar values are emitted in normalized form. The
root `displayName` is excluded; host path, whitespace, indentation, and source
property order are not inputs. Every other declared field is included.

Catalog-managed rule paths are read-only. An editor that opens an installed
Trusted Catalog entry must create a separate editable working copy before
accepting changes. That copy may retain the logical `ruleId` and display name,
but it becomes a Draft when its semantic bytes change and requires a new
`ruleVersion` before republication. Save-in-place authority applies only to an
ordinary user-owned/imported authoring path, never to Catalog storage.

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

The General Merge/General Replace authoring contract currently admits only
`overlapPolicy = reject`. Any intersection between two user-authored target
ranges is an order-independent blocker even though the wider profile operation
vocabulary contains reviewed overlap policies for other built-in workflows.
The pinned v2 schema's broader enum is not permission for a General Saved Rule
to use `allow-declared` or `replace-existing`; such a rule is not executable or
promotion-eligible.

`accessEnvelope` can only narrow the parent profile. It caps mapping count and total write bytes and
limits target region ids. `validationRuleIds` and `processorStageIds` reference definitions already
owned by the parent profile; a saved rule cannot introduce processor parameters, commands, paths,
scripts, or new mutation primitives.

## Resource-limit resolution

Resource limits have three authorities:

1. Application owns product-wide technical ceilings that protect parsing,
   memory, and execution resources.
2. The exact Trusted Parent owns firmware-semantic limits and each slot's
   exact, minimum, or maximum accepted file length.
3. The Saved Rule `accessEnvelope` may only narrow the Parent's maximum mapping
   count, maximum total write bytes, allowed regions, and per-slot length
   envelope.

The compiler uses the intersection of all applicable limits. No UI, CLI, or
consumer adapter may substitute a different limit or treat a Saved Rule value
as permission to exceed its Parent. A limit failure reports the authority,
bound, and observed value through the common Application issue model.

Bytes outside a mapping's declared source range are not rejected solely
because they form an unreferenced file tail. The complete file must still fit
the Application technical ceiling and resolved slot length contract, while
every mapped range must independently fit the accepted immutable input.

The pinned v2 schema currently represents `maximumMappingCount` and
`maximumTotalWriteBytes`, but does not carry per-slot input-length narrowing.
That capability requires a versioned schema revision plus strict
loader/compiler round-trip and negative tests; it is not inferred from local
UI or CLI state.

## General Replace execution boundary

A General Replace rule may execute only through its exact resolved Trusted
Parent. The parent remains the sole owner of physical region access,
protected-range policy, conditional POSTBUILD selection, processor/tool
binding and parameters, stage order, and allowed-write ranges. Rule mappings
and `accessEnvelope` may narrow that authority but cannot introduce, replace,
omit, or broaden it. A referenced stage id is compatibility evidence for an
already parent-owned stage, not a rule-defined processor.

The parent compiler determines from the accepted target ranges whether
POSTBUILD is required. A processor-free shape executes only when the parent
admits it. A TP-touching shape fails closed unless the exact parent declares
the reviewed final stage and its golden/evidence and firmware-owner gates are
satisfied. There is no projection-only fallback that executes bytes under a
weaker profile.

The current runtime remains validation/mapping-projection only for General
Replace saved rules. Enabling execution requires strict loader/compiler
round-trip tests, parent-authority negative tests, processor diff auditing,
full-output golden evidence for applicable TP routes, and explicit migration
of the current compatibility boundary.

Promotion uses the same monotonic stages as composition profiles. Migration or successful parsing
does not promote a rule. `supported` requires an empty blocker list, exact parent compatibility,
owner/reviewer approval, and required golden evidence. Original input names remain trace metadata;
the parent profile remains the sole output naming policy.

## General Merge output initialization

A reproducible General Merge saved rule owns one exact logical-output capacity,
one blank fill byte, and its mapping fragments as one reviewed unit. The fill
defaults to `0x00` only when the versioned rule declaration omits it under a
schema rule that explicitly defines that default; an explicit value may be any
byte in `0x00..0xFF`. Rule consumers cannot supply a hidden capacity or fill
override. Either change creates a new rule revision and changes the
compilation/Preview identity.

General Replace never declares this logical-output initializer. Its output
capacity and initial bytes come from the required immutable Reference and the
exact resolved parent map.

The pinned v2 JSON schema carries this initializer as
`imageInitialization`. Strict loader/round-trip support defaults an omitted
`fillByte` to `0x00`; normal rule consumption rejects out-of-band `--size` and
`--fill` overrides. The initializer, canonical rule hash, exact Parent, and
accepted input snapshots participate in Preview/Build identity.
