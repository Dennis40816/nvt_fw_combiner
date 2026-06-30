# UI Information Architecture

This document defines the owner-approved UI direction for the first usable NVT FW Combiner interface. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Owner direction

- Style: modern, minimal, and work-focused.
- Information density: show the smallest useful amount of information; reduce reading cost before adding secondary explanations.
- Home: the first screen is a clean three-card launcher for Settings, Replace, and Merge only.
- Page separation: Replace pages show Replace-only controls; Merge pages show Merge-only controls. Shared components are reused, but workflow content is not mixed.
- Shared device context: IC, IC Num, and IC Num mode live in one prominent fixed row below the header on every page, including Home. Workflow pages consume that same context instead of redefining it.
- Shared visualization: Merge and Replace use the same Memory coverage before/after component in the same page location; the area is visual-first and table-supported.
- Workbench layout: Merge and Replace use a left primary workspace and a right inspector. Readiness, validation, and processor status live in the inspector instead of floating as sparse cards.
- Active state: top navigation and workflow mode selection must visibly show the active page/mode.
- Button system: navigation uses low-noise active tabs, workflow mode uses rounded pill segmented controls, Home quick jumps use command rows, and disabled Preview/Build/Report actions stay visually light.
- Inputs: firmware files are represented as slot cards.
- Replace must consume the explicit shared IC num selector/input before region choices and processor readiness. Initial selector modes are `single` and `cascade`; `numeric` is contract-reserved.
- Reports: Preview/Build opens a report modal for diagnostics and evidence review.
- Saved Rules: hidden in the first UI release until the saved-rule workflow is implemented and reviewed.
- Localization: UI implementation uses a bilingual English/Chinese-ready text architecture, with English as the initial default.
- Typography: Inter for Latin/English UI text; Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI.
- UI priority: core/Application/CLI behavior leads; UI binds to application services after the C# core is ready.

## Navigation model

| Page | Purpose | First demo content | Must not contain |
| --- | --- | --- | --- |
| Home | Clean launcher | three large cards: Settings, Replace quick jumps, Merge quick jumps | Memory coverage, reports, mixed workflow controls |
| Settings | Configure folders, profile packs, strictness, theme, diagnostics access | compact settings groups, profile catalog status, log/report access | direct GitHub secret editing, firmware mutation logic |
| Merge | Normal / AB Code merge entry point | mode selector with Normal enabled and AB Code disabled, shared IC/profile context, slot cards, visual-first memory coverage preview, preview/build actions | Replace controls, hard-coded copy/offset rules |
| Replace | DP / CtrlRAM / General replace entry point | shared IC num context, persona selector, base/reference slot cards, overlay slot cards including separate DP/LD cards when a DP Replace profile requires them, visual-first memory coverage preview, preview/build actions | Merge controls, region authorization logic in UI |

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
  Header: product, page navigation, support state
  Device context: shared IC, IC Num, IC Num mode
  Content region
    Home launcher: Settings | Replace | Merge cards
    Settings page
    Merge page
    Replace page
  Status bar: profile catalog, validation state, diagnostics/log shortcut
```

## Required UX concepts

- Every build-like flow must have Preview before Build.
- Risky processors show readiness before execution.
- Current implementation priority is normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows; AB is deferred.
- IC and IC Num remain in the same fixed Device context location across Home, Settings, Merge, and Replace.
- Replace UI requires shared IC num selection/input before showing profile-specific regions. First UI supports `single` and `cascade`; `numeric` remains reserved for future IC exceptions.
- Saved Rules controls remain hidden in the first UI release.
- Reports and diagnostics are secondary surfaces opened from Preview/Build report modals; Settings may expose diagnostics configuration/export, but not run-specific evidence as a top-level page.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Terminal/log panes are read-only and sanitized.
- The shared Memory coverage area must support before/after display for both Merge and Replace without moving position between pages.
- Memory coverage should read as a light workbench component with labels and legend, not as a dominant dark banner.
- Display strings must be structured for bilingual English/Chinese support.
- Technical fixed-width content such as addresses, byte values, hashes, and terminal snippets should use Cascadia Mono, then Consolas as fallback.

## Resolved UI decisions

- Home uses three large launcher cards: Settings, Replace, and Merge.
- Replace quick jumps are DP, CtrlRAM, and General.
- Merge quick jumps are Normal and disabled AB Code.
- Memory coverage is visual-first with table support.
- Workflow pages use left workspace plus right inspector.
- Navigation and mode controls expose active selection state.
- Buttons use distinct nav, rounded mode-pill segment, command-row, action, and disabled-action styles instead of one generic button treatment.
- Inputs use slot cards.
- Diagnostics and evidence are shown through Preview/Build report modals.
- IC and IC Num use a shared Device context row that stays in the same location on every page.
- Replace consumes the shared IC num selector/input.
- The UI uses a bilingual English/Chinese-ready architecture.
- Saved Rules is hidden in the first UI release.
- The initial default language is English.
- The Avalonia UI font stack is fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, and Segoe UI; technical fixed-width values use Cascadia Mono or Consolas.

## Open UI decisions for review

No open owner decisions are currently recorded for the first demo shell.
