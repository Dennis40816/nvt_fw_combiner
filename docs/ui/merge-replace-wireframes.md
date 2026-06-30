# Merge and Replace Wireframe Plan

This document describes the first `0.1.1` demo interface for Merge and Replace. It is intentionally low fidelity and does not implement firmware behavior.

## Shared layout

```text
[Top tabs: Settings | Merge | Replace]
[Page header: mode/persona, profile context, support status]
[Primary action row: Preview | Build]
[Slot-card input area]
[Shared visual-first Memory coverage before/after]
[Validation and diagnostics summary]
```

Shared controls:

- mode/profile selector;
- support status badge: draft / candidate / supported;
- slot cards using only necessary firmware metadata;
- preview issue list;
- report modal entry after Preview/Build;
- disabled Build button until application core reports a valid preview.
- fixed-position visual-first Memory coverage before/after area shared by Merge and Replace, with table details as secondary support.

## Visual style guardrails

- Use a modern, minimal, work-focused style.
- Prefer compact labels, status chips, and progressive disclosure over explanatory paragraphs.
- Avoid landing-page or marketing composition.
- Keep Memory coverage visually stable between Merge and Replace so users can compare workflows without relearning layout.
- Keep display text sourced through a bilingual English/Chinese-ready architecture.
- Use Inter for English/Latin UI text and Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI. Use Cascadia Mono or Consolas only for fixed-width technical values.

## Merge page

### Mode cards

```text
Standard Merge
  Fixed DP/TP/LD profile-driven merge.

AB Code Merge
  A/B bank model with declared container views, relocation patches, and external processing. Deferred for the current implementation phase.

General Merge
  Advanced mapping editor that starts from a blank image. Saved-rule controls are hidden in the first UI release.
```

### Standard Merge demo sections

1. IC/profile selector from the supported or candidate catalog.
2. Slot cards: DP, TP, optional LD/Extra.
3. Version token preview placeholder.
4. Visual-first shared Memory coverage before/after preview.
5. Ordered operation preview table as supporting detail.
6. External processor readiness row.
7. Output naming preview.
8. NT51950/NT51951 map-pending status until owner memory maps are supplied.

### AB Merge demo sections

1. DP_AB or DPA/DPB input mode selector.
2. TPA and TPB slot cards.
3. Bank A/B visual summary.
4. Relocation patch table placeholder.
5. External combiner/header processor readiness.
6. Compare rule summary.

### General Merge demo sections

1. Dynamic input list.
2. Mapping table:
   - source input;
   - source range;
   - target address space;
   - target range;
   - overlap policy;
   - reason.
3. Saved-rule controls hidden until the workflow is implemented and reviewed.
4. Saved-rule status omitted from the first UI release.

## Replace page

### Persona cards

```text
DP Replace
  DP whole/declared partitions; LD replacement stays here when profile-declared.

CtrlRAM Replace
  CtrlRAM named regions only.

General Replace
  Explicit source-to-target mappings inside profile-approved safety envelope.
```

### Replace demo sections

1. IC num selector/input using `single` or `cascade`; `numeric` remains reserved.
2. Base reference BIN card.
3. Replace persona selector.
4. Replaceable region list.
5. Overlay slot cards; DP Replace may show separate DP and LD cards.
6. Visual-first shared Memory coverage before/after preview.
7. Processor/tool readiness row, including post-replace combiner.exe CRC/header requirement when declared.
8. Protected range warnings.

## Report modal

Preview and Build open a report modal after completion or failure. The modal owns output hash, mutation summary, diagnostics, sanitized logs, and export/copy actions. It must not become a top-level page.

## Build action behavior

For the demo shell, Build remains disabled and explains which application-core milestone is required:

```text
Build disabled: Composition core is planned for 0.2.0-dev.N.
```

Preview may show static synthetic data but must be labeled as demo data.

## No-go patterns

- Do not calculate offsets in XAML or ViewModels.
- Do not directly read BIN files from UI in `0.1.1`.
- Do not call Python or legacy combiner tools.
- Do not hide unsupported states behind green status.
- Do not show `General` as a bypass for profile rules.
