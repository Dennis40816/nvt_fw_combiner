# Merge and Replace Wireframe Plan

This document describes the current production-backed workbench interface for Merge and Replace. UI surfaces render catalog/service state and do not implement firmware behavior in XAML or ViewModels.

## Home launcher

```text
[Header: product + compact navigation]
[Large card: Settings]
[Large card: Replace: DP | CtrlRAM | General]
[Large card: Merge: Normal | General | AB Code disabled]
```

The home launcher must stay clean. It does not show Device context, Memory coverage, reports, diagnostics, or mixed Merge/Replace workflow details.

## Shared page layout

```text
[Fixed Device context: IC | Number where workflow requires Number]
[Left primary workspace: page header, Preview | Build, Memory coverage, mode, slot cards]
[Right inspector: profile, validation, readiness, processor status]
```

Shared controls:

- mode/profile selector;
- shared Device context for IC and Number in the same fixed position on every page;
- support status badge: draft / candidate / supported;
- slot cards using only necessary firmware metadata;
- preview issue list;
- report modal entry after Preview/Build;
- disabled Build button until application core reports a valid preview.
- fixed-position visual-first Memory coverage before/after area shared by Merge and Replace, with table details as secondary support.
- visible active state for top navigation and workflow mode selection.
- right-side inspector for readiness, validation, and processor status.
- separate button treatments for navigation tabs, rounded pill workflow modes, Home command rows, workflow actions, and disabled actions.
- changing IC or Number refreshes profile-dependent workflow state, including mode availability, slot cards, Memory coverage, validation, preview tokens, and Build readiness.

## Visual style guardrails

- Use a modern, minimal, work-focused style.
- Prefer compact labels, status chips, and progressive disclosure over explanatory paragraphs.
- Avoid landing-page or marketing composition.
- Keep Memory coverage visually stable between Merge and Replace so users can compare workflows without relearning layout.
- Keep Memory coverage on a light workbench surface with direct labels and
  inspectable details instead of a dominant dark banner. Add a compact legend
  only when multiple non-obvious states lack an adjacent self-labeling list.
- Keep command rows and disabled actions visually lighter than primary workflow content.
- Keep workflow mode selectors as full-width rounded pills rather than small rectangular buttons.
- Keep Merge and Replace page content independent; reusable components are allowed, mixed workflow content is not.
- Keep display text sourced through a bilingual English/Chinese-ready architecture.
- Use Inter for English/Latin UI text and Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI. Use Cascadia Mono or Consolas only for fixed-width technical values.

## Merge page

### Mode cards

```text
Standard Merge
  Fixed DP/TP/LDC profile-driven merge.

AB Merge
  A/B bank model with declared container views, relocation patches, and external processing. Deferred for the current implementation phase.

General Merge
  Advanced mapping editor that starts from a blank image. Saved-rule controls remain hidden until reviewed.
```

### Standard Merge sections

1. Shared IC/profile context from the fixed Device context row.
2. Slot cards: DP, TP, optional LDC/Extra.
3. Output name/version badge preview from catalog-backed firmware metadata.
4. Visual-first shared Memory coverage before/after preview.
5. Ordered operation preview table as supporting detail.
6. External processor readiness row.
7. Output naming preview.
8. NT51950/NT51951 confirmed TP overlay range and golden-pending status from DP Perspective evidence.

### AB Merge sections

1. DP_AB or DPA/DPB input mode selector.
2. TPA and TPB slot cards.
3. Bank A/B visual summary.
4. Relocation patch table remains deferred while AB Code is disabled.
5. External combiner/header processor readiness.
6. Compare rule summary.

### General Merge sections

1. Shared IC context from the fixed Device context row; Number is hidden because General Merge v1 has no IC-count branch.
2. Output length and blank-fill-byte fields. The output starts as
   blank/reserved bytes and does not clone a reference image; omitted fill
   defaults to `00`.
3. Mapping table:
   - Source Slice or From File Start preset;
   - source start (`0` for From File Start);
   - target start;
   - length;
   - source BIN;
   - optional explicit Use full file length action.
4. Visual-first Memory coverage showing reserved output plus explicit Source BIN writes.
5. Ordered operation preview table as supporting detail.
6. No postbuild command is invoked by General Merge v1. TP-touching edits that require CRC/header refresh belong to Replace, not Merge.
7. Saved-rule controls hidden until the workflow is implemented and reviewed.

## Replace page

### Persona cards

```text
DP Replace
  Initial Code/declared DP parts; LDC replacement stays here when profile-declared.

CtrlRAM Replace
  CtrlRAM named regions only.

General Replace
  Explicit source-to-target mappings inside profile-approved safety envelope.
```

### Replace sections

1. Shared IC Count selector using operator-readable count options rather than
   exposing internal topology vocabulary.
2. Base reference BIN card.
3. Replace persona selector.
4. Replaceable region list.
5. Overlay slot cards; DP Replace may show separate Initial Code and LDC cards.
6. Visual-first shared Memory coverage before/after preview.
7. Processor/tool readiness row, including post-replace combiner.exe CRC/header requirement when declared.
8. Protected range warnings.

## Report modal

Preview and Build open a report modal after completion or failure. The modal owns output hash, mutation summary, diagnostics, sanitized logs, and export/copy actions. It must not become a top-level page.

## Build action behavior

Build remains disabled until application readiness permits it:

```text
Build disabled: run Preview and resolve validation issues first.
```

Preview output must come from application services or a loaded run report.

## No-go patterns

- Do not calculate offsets in XAML or ViewModels.
- Do not directly read BIN files from UI code.
- Do not call Python or legacy combiner tools.
- Do not hide unsupported states behind green status.
- Do not show `General` as a bypass for profile rules.
