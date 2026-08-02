# ADR 0043: Resolve NT51928 dual-capacity composition through one capability and selection groups

- Status: Accepted; slot-readiness semantics amended by ADR 0048
- Date: 2026-07-28
- Accepted: 2026-07-28 by the product, architecture, and firmware owner
- Owners: Product owner + architecture owner + firmware owner
- Risk: R2 architecture contract; NT51928 profile and byte migrations remain R3
- Builds on: ADR 0015, ADR 0020, ADR 0041
- Amended by: ADR 0045 for source projection and FlashCode admission; ADR 0046
  for capability-definition versus per-compilation identity and evidence
  status; ADR 0048 for dependent selection readiness

## Context

NT51928 has two owner-confirmed non-NB container forms:

- a `0x40000` form without LDC that follows NT51927 composition; and
- a `0x80000` form with the NT51928-specific LDC region.

NT51927 and NT51928 are not a perfect family. They share only explicitly
referenced Initial Code and TP facts through `SharedFactRelationship` roles
`initial-code-shared` and `tp-shared`. NT51928 retains its distinct LDC,
complete-container, evidence, publication, and requested-member identity.

The previous NT51928 DP Replace candidate fixed the Reference FlashCode and LDC
input to one `0x80000` map and required both Initial Code and LDC replacements.
That shape cannot express a `0x40000` Reference, Initial-Code-only replacement,
or LDC-only replacement without duplicating public routes. Per-slot
`required/cardinality` is also insufficient: two individually optional
replacement slots still need one group-level rule requiring at least one
selection.

## Decision

### One public capability with resolved map variants

NT51928 Standard Merge and DP Replace each remain one public capability and one
Support Matrix row. Container form is a resolved map variant, not a route,
profile, capability, or publication identity.

Resolution never infers the IC from file length. It first resolves the requested
NT51928 capability, then selects only among that capability's declared map
variants.

### Standard Merge

DP and TP remain individually required. LDC is optional.

| LDC authoring state | Resolved variant | Result |
| --- | --- | --- |
| absent | shared Initial Code/TP `0x40000` | use the canonical NT51927 composition facts and emit exactly `0x40000` |
| selected and structurally valid | NT51928 Initial Code/TP/LDC `0x80000` | emit exactly `0x80000` |
| selected but structurally invalid | none | block; never reinterpret the selected LDC as absent |

The `0x40000` variant references the same canonical Initial Code/TP facts and
composition definition as NT51927. It does not copy offsets or operations into
a second NT51928 definition.

### DP Replace

Reference FlashCode is always required. Its accepted immutable snapshot selects
the declared NT51928 map variant:

| Reference length | Resolved variant | Replacement selection |
| --- | --- | --- |
| `0x40000` | shared Initial Code/TP form without LDC | Initial Code only |
| `0x80000` | NT51928 form with LDC | Initial Code, LDC, or both |
| any other length | none | blocked |

Execution clones the complete Reference first. It materializes only operations
whose replacement slots are selected. Every unselected region and every byte
outside a selected operation remains byte-identical to Reference.

For a `0x40000` Reference, the LDC slot remains visible but resolves to
`NotApplicable` with the reason `Reference length does not include LDC`.
Before Reference inspection, both dependent replacement members are
`PendingInput`, return `CanSelect = false`, and ask the operator to load
Reference. After Reference inspection, the `0x40000` variant enables Initial
Code as the sole applicable required member while LDC becomes `NotApplicable`;
the `0x80000` variant enables both members under the declared group. Changing
variants increments the authoring revision, invalidates stale derived results,
and rejects any stale or manually constructed binding.

### Replacement selection group

The canonical profile declares a group instead of marking every member
required:

```text
replacement-selection-group:
  members:
    - initial-code-replacement
    - ldc-replacement
  minimumSelected: 1
  maximumSelected: 2
```

Each member has per-slot cardinality `zero-or-one`. Group cardinality is
evaluated after map applicability:

- on the `0x40000` variant, LDC is `NotApplicable`, so Initial Code is the sole
  applicable member and must be selected;
- on the `0x80000` variant, either member or both satisfy the group.

A supplied path selects an enabled member before validation. Invalid selected
content cannot be silently treated as absent. Application owns the one
readiness result consumed by UI and CLI. The group definition and allowed
variants participate in `CapabilityFingerprint`; the actual resolved variant
and selected members participate in `CompilationFingerprint`; neither creates
another route identity.

This rule applies only to explicitly declared selection groups. It does not
make unrelated multi-input workflows optional; Standard Merge DP and TP remain
required.

### Structural admission and warning-only plausibility

Structural safety remains blocking: unreadable files, unsupported container
lengths, missing selected ranges, wrong binding kind, and out-of-bounds
operations cannot Build.

Reference remains complete-container authoritative and therefore exact to the
resolved `0x40000` or `0x80000` variant. Initial Code and LDC replacement
artifacts are different: each is an address-bearing section source and needs to
cover only its selected canonical source view. The same rule permits a
compatible NT51928 FlashCode to provide either view. Standard Merge TP,
Initial Code/DP, and LDC inputs use the same address-aligned source-view
coverage. Neither a section's outer length nor the presence of LDC defines
FlashCode.

Profiles may separately attach a warning-only `non-uniform-region` plausibility
validation to a canonical Initial Code, DP, or LDC source view. The validation
requires more than one distinct byte across the declared view. A uniform view
emits a typed warning through the shared Application result, UI, CLI, Preview,
Build Report, and inspection surfaces, but never changes map resolution,
selection, execution admission, or output bytes.

The validation is opt-in per canonical profile/fact. It is not inferred for
every DP or LDC artifact and does not use filenames, hashes, or unapproved
signatures as firmware truth.

### Terminology

Canonical public and new serialized names use `LDC`, not `LD`:

- `ldc-input`;
- `ldc-replacement`;
- `ldc-code`;
- CLI `--ldc`.

Compatibility aliases may exist only at an explicitly named migration seam
with a deletion criterion. New Domain, Application, CLI, report, profile, and
UI contracts do not introduce `LD` synonyms.

## Consequences

- One capability covers both container forms without duplicating routes or
  operations.
- Reference identity determines DP Replace container capacity, while Standard
  Merge LDC selection determines whether the optional LDC variant is required.
- UI can explain unavailable LDC precisely instead of hiding it or displaying
  an ambiguous disabled card.
- Plausibility warnings can detect uniform dummy content without denying a
  firmware-owner-authorized build.
- Existing fixed-profile compatibility lowering is migration scaffolding only
  and cannot become the canonical `0.10.x` architecture.

No current project supplies an owner-approved complete golden for this
dual-capacity capability. Standard Merge and DP Replace therefore remain
`ContractOnly`, not `DirectGolden`. The verification byte oracles below are
regression evidence and do not promote publication, support, or golden status.

Ticket #239 owns the headless canonical family/profile, selection-group,
resolution/compiler, Application/CLI/report, and evidence slice. Ticket #194
consumes it during remaining route convergence, and #182 later owns only the
shared Presentation rendering of its typed states.

## Verification

- Standard Merge byte goldens cover exact `0x40000` no-LDC and `0x80000`
  with-LDC outputs.
- DP Replace changed-input oracles cover `0x40000` Initial-Code-only and
  `0x80000` Initial-Code-only, LDC-only, and both-selected cases.
- Every unselected region and byte outside selected ranges remains Reference.
- Neither-selected, stale LDC binding, unsupported Reference length, selected
  invalid input, and out-of-bounds source ranges fail closed.
- Uniform Initial Code/LDC sentinels create warnings in Application, CLI, UI,
  Preview, and Build Report while Build remains executable; non-uniform
  sentinels do not warn. #239 proves the Application, CLI, Preview, and Build
  Report contract without owning Presentation rendering.
- #239 Application tests cover typed Pending Reference, `NotApplicable` at
  `0x40000`, Optional at `0x80000`, warning code/reason/action, and authoring
  revision invalidation. #182 later covers rendering, keyboard/focus,
  assistive descriptions, localization, and visual state while consuming those
  typed results without reconstructing map or firmware-validity rules.

## Non-goals

- No NT51928 NB support.
- No CRC, Header, POSTBUILD, topology, support, or publication change.
- No LDC content signature beyond the owner-confirmed non-uniform plausibility
  rule.
- No reuse of Standard Merge selection state inside DP Replace.
- No permanent compatibility profiles or extra Support Matrix rows.
