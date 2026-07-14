# UI Token Consolidation Inventory

Status: `v0.9.7` candidate baseline. This document records a measured
starting point and the rules for incremental token consolidation. It grants no
firmware behavior, support, or release authority.

## Measurement Baseline

Measured from commit `a9ae568f` with generated, `bin`, and `obj` directories
excluded:

| Scope | Files | Nonblank lines |
| --- | ---: | ---: |
| Production C# under `src/` | 511 | 53,753 |
| Avalonia C# | 131 | 12,881 |
| Avalonia XAML | 29 | 3,961 |

The initial literal scan over Avalonia `.axaml` files found:

| Literal family | Occurrences | Interpretation |
| --- | ---: | --- |
| Hex colors | 382 | Candidate palette consolidation only; geometry, data-bound colors, and one-off visual evidence are not automatic token candidates. |
| Layout and typography attributes | 285 | Candidate shared-role review only; compact, technical, and accessibility-specific roles remain distinct when their behavior differs. |
| Existing resource references | 39 | The existing shared style library is the only token hierarchy to extend. |

The most frequently repeated colors are `#F8FAFC`, `#CBD5E1`, `#E2E8F0`,
`#FFFFFF`, `#0F172A`, `#334155`, `#2563EB`, `#64748B`, and `#94A3B8`.
The most frequent inline text sizes are `12`, `13`, and `11`; the most frequent
corner radii are `999`, `6`, and `8`.

Hex color counts use `#[0-9A-Fa-f]{3,8}(?![0-9A-Fa-f])` over the tracked
Avalonia XAML files. These counts are an inventory, not an acceptance target.
Replacing every literal with a token would create false equivalence and can
increase code size.

## Phase Records

### Shared semantic palette foundation

The first implementation phase adds 26 role-based `UiBrush.*` resources to
`Styles/MainWindowControlStyles.axaml` and migrates only existing shared style
roles in the canonical control, button, and visual libraries. It does not
change templates, layout values, localization, accessibility roles, or firmware
behavior.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 382 | 200 |
| `UiBrush.*` dynamic-resource references | 0 | 208 |

The remaining direct literals are intentionally deferred until a later phase
can establish whether they are one-off visual evidence, a distinct semantic
state, or an exact shared-role duplicate.

### Template palette-reference migration

The second implementation phase reuses the established semantic palette in
shell, report, mapping, slot, modal, and Hex Editor templates. It adds only
`UiBrush.ModalOverlay` and `UiBrush.StrongModalOverlay`, because those overlay
opacities recur across modal surfaces. It does not turn timeline geometry,
mapping-grid evidence, or one-off status colors into generic tokens.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 200 | 85 |
| `UiBrush.*` dynamic-resource references | 208 | 325 |
| Shared semantic palette resources | 26 | 28 |

The XAML style-contract test now rejects direct values for every palette
property-role pair migrated in this phase. It deliberately permits direct
values for geometry and roles that do not yet have an exact shared semantic
token.

### Shared danger-button treatment

The third implementation phase adds the three reusable danger-button roles
`UiBrush.DangerSurface`, `UiBrush.DangerBorder`, and `UiBrush.TextDanger`.
They are used by both text and icon danger-button templates; no warning or
firmware outcome color is folded into this treatment.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 85 | 78 |
| `UiBrush.*` dynamic-resource references | 325 | 335 |
| Shared semantic palette resources | 28 | 31 |

### Shared row dividers

The fourth implementation phase adds `UiBrush.RowDivider` for the identical
low-emphasis separators used by list rows, settings rows, and shared content
panels. It does not merge data-bound or geometric border roles.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 78 | 75 |
| `UiBrush.*` dynamic-resource references | 335 | 339 |
| Shared semantic palette resources | 31 | 32 |

## Consolidation Rules

1. `Styles/MainWindowControlStyles.axaml` remains the single shared token and
   semantic-role library. Do not add a parallel theme, localization, or C# token
   wrapper.
2. Add a palette token only when more than one semantic role currently shares
   the value or a shared role needs a stable state variant. Token names describe
   role, not source color values.
3. Migrate shared styles before templates. A template receives a token only
   when it expresses the same role as an existing style or uses an approved
   palette state.
4. Keep direct values for data-bound colors, geometry, rendered memory-map
   evidence, and genuinely one-off visual requirements. They are not
   hard-coded design-token debt by default.
5. Preserve distinct text, badge, compact, technical, and accessibility roles
   even when their current colors or sizes match. Visual equality alone is not
   semantic equality.
6. Delete a style, setter bundle, or compatibility route only when the retained
   role is exact and UI smoke coverage proves the affected surface unchanged.

## Execution Order

1. Define a compact semantic palette in the canonical style library and migrate
   its existing shared style files without changing visible state behavior.
2. Migrate repeated surface, text, badge, and action-role bundles in templates
   to existing semantic classes or palette resources.
3. Remove only exact duplicate setters or styles after their callers use the
   retained role.
4. Recount source lines and literals after each independently reviewable phase.
   Record both reductions and intentional retained direct values.

## Required Verification

- affected XAML style-contract and UI smoke tests;
- localization and accessibility assertions for every changed surface;
- `git diff --check`, Polytail, and final `python scripts/verify.py --all` for
  implementation phases; and
- an independent review confirming no UI token encodes firmware policy or
  collapses distinct user-facing meaning.
