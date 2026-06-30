# UI Information Architecture

This document defines the owner-approved UI direction for the first usable NVT FW Combiner interface. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Owner direction

- Style: modern, minimal, and work-focused.
- Information density: show the smallest useful amount of information; reduce reading cost before adding secondary explanations.
- Top-level navigation: only Settings, Merge, and Replace.
- Shared visualization: Merge and Replace use the same Memory coverage before/after area in the same location.
- UI priority: core/Application/CLI behavior leads; UI binds to application services after the C# core is ready.

## Navigation model

| Page | Purpose | First demo content | Must not contain |
| --- | --- | --- | --- |
| Settings | Configure folders, profile packs, strictness, theme, diagnostics access | compact settings groups, profile catalog status, log/report export links | direct GitHub secret editing, firmware mutation logic |
| Merge | Standard / AB / General merge entry point | mode selector, IC/profile selector, input rows, shared memory coverage preview, preview/build actions | hard-coded copy/offset rules |
| Replace | Display / TP HW / TP FW / General replace entry point | persona selector, base/reference input, overlay input rows, shared memory coverage preview, preview/build actions | region authorization logic in UI |

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
- Reports and diagnostics are secondary surfaces opened from Settings/status, not top-level pages.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Terminal/log panes are read-only and sanitized.
- The shared Memory coverage area must support before/after display for both Merge and Replace without moving position between pages.

## Open UI decisions for review

1. Should the three top-level tabs be horizontal top tabs or a compact left rail with only three items?
2. Should diagnostics open as a Settings subpage, a bottom drawer, or a modal export/review panel?
3. Should Saved Rules be hidden until implemented, or exposed only as an action inside General Merge/Replace?
4. Should the Memory coverage area be visual-map-first or table-first for the first demo?
5. Which language set is required for first demo: English, Chinese, or both?
