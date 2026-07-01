# Merge and Replace Wireframe Plan

This document describes the first `0.1.1` demo interface for Merge and Replace. It is intentionally low fidelity and does not implement firmware behavior.

## Home launcher

```text
[Header: product + compact navigation]
[Fixed Device context: IC | IC Num | mode]
[Large card: Settings]
[Large card: Replace: DP | CtrlRAM | General]
[Large card: Merge: Normal | AB Code disabled]
```

The home launcher must stay clean. It does not show Memory coverage, reports, diagnostics, or mixed Merge/Replace workflow details.

## Shared page layout

```text
[Fixed Device context: IC | IC Num | mode]
[Left primary workspace: page header, Preview | Build, Memory coverage, mode, slot cards]
[Right inspector: profile, validation, readiness, processor status]
```

Shared controls:

- mode/profile selector;
- shared Device context for IC, IC Num, and IC Num mode in the same fixed position on every page;
- support status badge: draft / candidate / supported;
- slot cards using only necessary firmware metadata;
- preview issue list;
- report modal entry after Preview/Build;
- disabled Build button until application core reports a valid preview.
- fixed-position visual-first Memory coverage before/after area shared by Merge and Replace, with table details as secondary support.
- visible active state for top navigation and workflow mode selection.
- right-side inspector for readiness, validation, and processor status.
- separate button treatments for navigation tabs, rounded pill workflow modes, Home command rows, workflow actions, and disabled actions.

## Visual style guardrails

- Use a modern, minimal, work-focused style.
- Prefer compact labels, status chips, and progressive disclosure over explanatory paragraphs.
- Avoid landing-page or marketing composition.
- Keep Memory coverage visually stable between Merge and Replace so users can compare workflows without relearning layout.
- Keep Memory coverage on a light workbench surface with labels and legends instead of a dominant dark banner.
- Keep command rows and disabled actions visually lighter than primary workflow content.
- Keep workflow mode selectors as full-width rounded pills rather than small rectangular buttons.
- Keep Merge and Replace page content independent; reusable components are allowed, mixed workflow content is not.
- Keep display text sourced through a bilingual English/Chinese-ready architecture.
- Use Inter for English/Latin UI text and Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI. Use Cascadia Mono or Consolas only for fixed-width technical values.

## Merge page

### Mode cards

```text
Normal Merge
  Fixed DP/TP/LD profile-driven merge.

AB Code Merge
  A/B bank model with declared container views, relocation patches, and external processing. Deferred for the current implementation phase.

General Merge
  Advanced mapping editor that starts from a blank image. Saved-rule controls are hidden in the first UI release.
```

### Normal Merge demo sections

1. Shared IC/profile context from the fixed Device context row.
2. Slot cards: DP, TP, optional LD/Extra.
3. Version token preview placeholder.
4. Visual-first shared Memory coverage before/after preview.
5. Ordered operation preview table as supporting detail.
6. External processor readiness row.
7. Output naming preview.
8. NT51950/NT51951 overlay/golden-pending status from DP Perspective evidence.

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

1. Shared IC num selector/input using `single` or `cascade` by default; `numeric` appears only for approved profile exceptions.
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
