# UI Information Architecture

This document defines the owner-approved UI direction for the first usable NVT FW Combiner interface. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Owner direction

- Style: modern, minimal, and work-focused.
- Information density: show the smallest useful amount of information; reduce reading cost before adding secondary explanations.
- Top-level navigation: top tabs with only Settings, Merge, and Replace.
- Shared visualization: Merge and Replace use the same Memory coverage before/after area in the same location; the area is visual-first and table-supported.
- Inputs: firmware files are represented as slot cards.
- Reports: Preview/Build opens a report modal for diagnostics and evidence review.
- Localization: UI implementation uses a bilingual English/Chinese-ready text architecture.
- UI priority: core/Application/CLI behavior leads; UI binds to application services after the C# core is ready.

## Navigation model

| Page | Purpose | First demo content | Must not contain |
| --- | --- | --- | --- |
| Settings | Configure folders, profile packs, strictness, theme, diagnostics access | compact settings groups, profile catalog status, log/report access | direct GitHub secret editing, firmware mutation logic |
| Merge | Standard / AB / General merge entry point | mode selector, IC/profile selector, slot cards, visual-first memory coverage preview, preview/build actions | hard-coded copy/offset rules |
| Replace | Display / TP HW / TP FW / General replace entry point | persona selector, base/reference slot cards, overlay slot cards, visual-first memory coverage preview, preview/build actions | region authorization logic in UI |

## Demo shell constraints

- Demo may use static sample text or synthetic typed data.
- Demo must not read firmware files.
- Demo must not call `combiner.exe` or Python workers.
- Demo must not implement range validation in ViewModels.
- Demo must keep all operation language aligned with `SPEC.md` and `docs/architecture/*`.
- Demo status labels must clearly distinguish planned, disabled, synthetic, and production-ready states.

## Page hierarchy

```text
Shell
  Top-level tabs: Settings | Merge | Replace
  Header: product, selected profile/mode, support state
  Content region
    Settings page
    Merge page
    Replace page
  Status bar: profile catalog, validation state, diagnostics/log shortcut
```

## Required UX concepts

- Every build-like flow must have Preview before Build.
- Risky processors show readiness before execution.
- General mapping rows can be saved as rules after validation/review.
- Reports and diagnostics are secondary surfaces opened from Preview/Build report modals; Settings may expose diagnostics configuration/export, but not run-specific evidence as a top-level page.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Terminal/log panes are read-only and sanitized.
- The shared Memory coverage area must support before/after display for both Merge and Replace without moving position between pages.
- Display strings must be structured for bilingual English/Chinese support.

## Resolved UI decisions

- Navigation uses top tabs.
- Memory coverage is visual-first with table support.
- Inputs use slot cards.
- Diagnostics and evidence are shown through Preview/Build report modals.
- The UI uses a bilingual English/Chinese-ready architecture.

## Open UI decisions for review

1. Should Saved Rules be hidden until implemented, or exposed only as an action inside General Merge/Replace?
2. What exact language should be the initial default: English or Chinese?
