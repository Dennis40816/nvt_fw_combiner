# UI Information Architecture

This document defines the owner-approved UI direction for the first usable NVT FW Combiner interface. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Owner direction

- Style: modern, minimal, and work-focused.
- Information density: show the smallest useful amount of information; reduce reading cost before adding secondary explanations.
- Top-level navigation: top tabs with only Settings, Merge, and Replace.
- Shared visualization: Merge and Replace use the same Memory coverage before/after area in the same location; the area is visual-first and table-supported.
- Inputs: firmware files are represented as slot cards.
- Replace must include an explicit IC num selector/input before region choices and processor readiness.
- Reports: Preview/Build opens a report modal for diagnostics and evidence review.
- Saved Rules: hidden in the first UI release until the saved-rule workflow is implemented and reviewed.
- Localization: UI implementation uses a bilingual English/Chinese-ready text architecture, with English as the initial default.
- Typography: Inter for Latin/English UI text; Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI.
- UI priority: core/Application/CLI behavior leads; UI binds to application services after the C# core is ready.

## Navigation model

| Page | Purpose | First demo content | Must not contain |
| --- | --- | --- | --- |
| Settings | Configure folders, profile packs, strictness, theme, diagnostics access | compact settings groups, profile catalog status, log/report access | direct GitHub secret editing, firmware mutation logic |
| Merge | Standard / AB / General merge entry point | mode selector, IC/profile selector, slot cards, visual-first memory coverage preview, preview/build actions | hard-coded copy/offset rules |
| Replace | Display / TP HW / TP FW / General replace entry point | IC num selector/input, persona selector, base/reference slot cards, overlay slot cards, visual-first memory coverage preview, preview/build actions | region authorization logic in UI |

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
- Current implementation priority is normal Merge and normal Replace for DP and TP CtrlRAM department workflows; AB is deferred.
- Replace UI requires IC num selection/input before showing profile-specific regions.
- Saved Rules controls remain hidden in the first UI release.
- Reports and diagnostics are secondary surfaces opened from Preview/Build report modals; Settings may expose diagnostics configuration/export, but not run-specific evidence as a top-level page.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Terminal/log panes are read-only and sanitized.
- The shared Memory coverage area must support before/after display for both Merge and Replace without moving position between pages.
- Display strings must be structured for bilingual English/Chinese support.
- Technical fixed-width content such as addresses, byte values, hashes, and terminal snippets should use Cascadia Mono, then Consolas as fallback.

## Resolved UI decisions

- Navigation uses top tabs.
- Memory coverage is visual-first with table support.
- Inputs use slot cards.
- Diagnostics and evidence are shown through Preview/Build report modals.
- Replace includes an IC num selector/input.
- The UI uses a bilingual English/Chinese-ready architecture.
- Saved Rules is hidden in the first UI release.
- The initial default language is English.
- The Avalonia UI font stack is fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, and Segoe UI; technical fixed-width values use Cascadia Mono or Consolas.

## Open UI decisions for review

No open owner decisions are currently recorded for the first demo shell.
