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

### Outcome indicators and report warning text

The fifth implementation phase promotes only exact visual roles: the report
timeline reuses `UiBrush.Border`; three positive-status indicators use
`UiBrush.SuccessIndicator`; and the recurring report warning detail and
warning meta text use `UiBrush.WarningDetail` and `UiBrush.WarningMeta`.
The firmware IC mismatch modal shares the warning-meta role. It does not
change text, bindings, visibility, automation names, or warning severity.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 75 | 68 |
| `UiBrush.*` dynamic-resource references | 339 | 349 |
| Shared semantic palette resources | 32 | 35 |

### Navigation and utility identity

The sixth implementation phase promotes the checked top-level navigation
surface and Util Tools identity text. `UiBrush.NavigationSelectedSurface`
applies only to the checked navigation control and its content presenter.
`UiBrush.UtilityAccent` applies only to recurring Util Tools labels. The
disabled AB pending capsule intentionally remains a direct one-off value even
though it currently shares the navigation surface color.

| Metric | Before | After |
| --- | ---: | ---: |
| Direct hex color literals in Avalonia XAML | 68 | 66 |
| `UiBrush.*` dynamic-resource references | 349 | 353 |
| Shared semantic palette resources | 35 | 37 |

### Firmware fact presentation ownership

The seventh implementation phase retires the eight color literals and four
`IBrush` presentation properties from `FirmwareSlotFactViewModel`. The record
now exposes only `Label`, `Value`, and `IsWarning`; `Border.firmwareSlotFact`
and its warning selector own the exact normal and warning presentation in the
shared style library. The existing fact text, binding values, and warning state
remain unchanged.

| Metric | Before | After |
| --- | ---: | ---: |
| C# presentation color literals | 68 | 60 |
| Direct hex color literals in Avalonia XAML | 66 | 67 |
| `UiBrush.*` dynamic-resource references | 353 | 361 |
| Shared semantic palette resources | 37 | 39 |

The XAML literal count increases by one because the two exact resources are
declared in the canonical library while one pre-existing changed-badge border
is replaced. This is an ownership migration, not a literal-count reduction.

### Firmware slot completion-state ownership

The eighth implementation phase removes the completion-state brush API and
color literals from `FirmwareSlotViewModel`. `FirmwareSlotCard` now exposes
only its existing `HasFile` and `IsOptional` values as XAML classes. The
shared style library owns the missing-required, selected-required, and
optional visual variants. The optional selectors follow the selected selectors
so an optional slot remains neutral after a file is selected. These are
presentation-only completion states; they do not encode firmware policy.

`UiBrush.ErrorSurface` replaces the exact existing error surface used by both
the missing-required slot and the invalid Hex editor cell. The remaining new
brushes describe the distinct required-input border and badge roles. The
selected required text uses the shared success-emphasis role.

| Metric | Before | After |
| --- | ---: | ---: |
| C# presentation color literals | 60 | 45 |
| Direct hex color literals in Avalonia XAML | 67 | 72 |
| `UiBrush.*` dynamic-resource references | 361 | 377 |
| Shared semantic palette resources | 39 | 45 |

This is a net source reduction despite the explicit style declarations: the
removed ViewModel state partial contained 55 lines, while the canonical style
library adds 34 nonblank XAML lines. UI smoke coverage keeps the required and
optional slot semantics, while the XAML style contract keeps the selector
binding and resource ownership explicit.

### Firmware slot icon ownership

The ninth implementation phase leaves `SlotKind`, the icon vector path, and
the accessible tooltip in `FirmwareSlotViewModel`. It removes only the icon
brush properties. `FirmwareSlotCard` uses Avalonia's built-in equality
converter to expose the existing enum as `bin`, `base`, `dp`, `tp`, and
`ctrlRam` classes. The shared style library owns each existing icon variant.
`Unknown` remains the generic BIN treatment.

The green emphasis shared by the selected-required badge and TP icon is named
`UiBrush.SuccessStrong`. The generic BIN icon, report preview state, and Hex
changed-block navigator reuse the exact existing caution surface/text roles.
This expands visual ownership only; it does not introduce a converter class,
new ViewModel state, or firmware policy.

| Metric | Before | After |
| --- | ---: | ---: |
| C# presentation color literals | 45 | 30 |
| Direct hex color literals in Avalonia XAML | 72 | 78 |
| `UiBrush.*` dynamic-resource references | 377 | 395 |
| Shared semantic palette resources | 45 | 54 |

The ViewModel loses 50 physical lines and the affected production C# total
falls by 46 nonblank lines. The canonical XAML styles and bindings add 47
nonblank lines, for a one-line total increase. The icon path, accessible
tooltip, and every enum-to-class mapping are covered by UI smoke and XAML
style-contract tests.

## Code-Size Audit

The code-size audit uses Git-tracked files under `src/` only. It excludes
generated `bin` and `obj` content so an SDK build cannot distort a source-size
decision. The `a9ae568f` baseline and the current recorded phase state measure:

| Scope | Baseline nonblank lines | Current nonblank lines | Delta |
| --- | ---: | ---: | ---: |
| Production C# | 53,753 | 53,629 | -124 |
| Avalonia C# | 12,881 | 12,757 | -124 |
| Avalonia XAML | 3,961 | 4,103 | +142 |
| Total tracked production source | 57,714 | 57,732 | +18 |

The XAML increase is the explicit shared palette declarations and their
references. The firmware-fact and slot-completion slices removed ViewModel
presentation state while moving the visual roles into the single shared style
library. No phase adds a C# wrapper, second token system, or firmware behavior.
The repository architecture test rejects source, test, and
documentation files over 700 lines; the largest tracked production source file
is currently 632 lines.

The requested approximate 30,000-line target would require removing about
27,756 tracked production lines, or 48 percent of the current source. UI token
cleanup cannot honestly produce that reduction. It must not be attempted by
deleting tests, golden evidence, manifests, or still-live legacy paths. Any
future reduction of that scale needs a separately approved workflow-retirement
decision with exact V2 replacement and regression evidence.

No current large file is an approved deletion candidate. The V2 compiler,
composition fingerprint, firmware-map models, and Hex Editor files each own
distinct execution or interaction responsibilities. The shared style library
also contains visually similar but role-distinct controls; merging them merely
because their current setters resemble each other would weaken accessibility
and visual-state contracts.

No remaining repeated template/style XAML value outside the shared palette has
an approved semantic match. The disabled AB pending badge remains an
intentional one-off even though it currently uses `#F1F5F9`.

The Presentation C# audit remains open by design.
`MemoryCoverageSegmentViewModel` keeps its data-bound `FillBrush` because it
represents projected coverage evidence, but its changed/kept badge and outline
can move to XAML state selectors. The Hex Editor stays a custom immediate-mode
renderer; a later slice may feed its stable brushes through Avalonia styled
properties while retaining its geometry, hit testing, caches, and transient
procedural feedback. These areas must not create ViewModel resource keys, a
theme service, or firmware-derived token names.

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
