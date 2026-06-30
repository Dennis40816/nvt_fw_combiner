# Merge and Replace Wireframe Plan

This document describes the first `0.1.1` demo interface for Merge and Replace. It is intentionally low fidelity and does not implement firmware behavior.

## Shared layout

```text
[Top tabs: Settings | Merge | Replace]
[Page header: mode/persona, IC, support status]
[Primary action row: Preview | Build | Save rule where applicable]
[Input area]
[Shared Memory coverage before/after]
[Validation and diagnostics summary]
```

Shared controls:

- profile selector;
- IC selector placeholder;
- support status badge: draft / candidate / supported;
- compact input rows/cards using only necessary metadata;
- preview issue list;
- diagnostics link;
- disabled Build button until application core reports a valid preview.
- fixed-position Memory coverage before/after area shared by Merge and Replace.

## Visual style guardrails

- Use a modern, minimal, work-focused style.
- Prefer compact labels, status chips, and progressive disclosure over explanatory paragraphs.
- Avoid landing-page or marketing composition.
- Keep Memory coverage visually stable between Merge and Replace so users can compare workflows without relearning layout.

## Merge page

### Mode cards

```text
Standard Merge
  Fixed DP/TP/LD profile-driven merge.

AB Code Merge
  A/B bank model with declared container views, relocation patches, and external processing.

General Merge
  Advanced mapping editor that starts from a blank image and can save a validated rule.
```

### Standard Merge demo sections

1. Input cards: DP, TP, optional LD/Extra.
2. Version token preview placeholder.
3. Shared Memory coverage before/after preview.
4. Ordered operation preview.
5. External processor readiness row.
6. Output naming preview.

### AB Merge demo sections

1. DP_AB or DPA/DPB input mode selector.
2. TPA and TPB input cards.
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
3. Save as rule button.
4. Rule promotion status.

## Replace page

### Persona cards

```text
Display Replace
  DP whole/declared partitions; TP whole-only.

TP HW Replace
  TP CtrlRAM named regions only; DP whole-only.

TP FW Replace
  Non-CtrlRAM TP regions; DP whole-only; CtrlRAM hidden by default.

General Replace
  Explicit source-to-target mappings inside profile-approved safety envelope.
```

### Replace demo sections

1. Base reference BIN card.
2. Replace persona selector.
3. Replaceable region list.
4. Overlay input rows.
5. Shared Memory coverage before/after preview.
6. Processor/tool readiness row.
7. Protected range warnings.

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
