# UI Information Architecture

This document defines the owner-approved UI direction for the first usable NVT FW Combiner interface. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Owner direction

- Style: modern, minimal, and work-focused.
- Information density: show the smallest useful amount of information; reduce reading cost before adding secondary explanations.
- Home: the first screen is a clean three-card launcher for Settings, Replace, and Merge only.
- Page separation: Replace pages show Replace-only controls; Merge pages show Merge-only controls. Shared components are reused, but workflow content is not mixed.
- Workflow device context: IC and Number live in one fixed context area below the header on Merge and Replace pages. IC is the target IC family/model selector; Number is the IC count/variant selector such as `single`/`cascade` or profile-specific numeric choices. Home and Settings hide this context because they do not author firmware operations.
- Breadcrumb navigation: the context bar includes a clickable history path so multi-level Settings pages can return to any earlier level.
- Shared visualization: Merge and Replace use the same Memory coverage before/after component in the same page location; the area is visual-first and table-supported.
- Workbench layout: Merge and Replace use a left primary workspace and a right inspector. Readiness, validation, and processor status live in the inspector instead of floating as sparse cards.
- Active state: top navigation and workflow mode selection must visibly show the active page/mode.
- Button system: navigation uses low-noise active tabs, workflow mode uses rounded pill segmented controls, Home quick jumps use command rows, and disabled Preview/Build/Report actions stay visually light.
- Inputs: firmware files are represented as slot cards.
- Replace must consume the explicit shared Number selector before region choices and processor readiness. Two-option profiles use text choices such as `single`/`cascade`; profiles with three or more concrete count choices use numeric selection, with future room for Other/custom exceptions.
- Reports: Preview/Build opens a report modal for diagnostics and evidence review.
- Report review: the shell may load a structured run report JSON and render it as a readable summary panel. This is a review surface for existing reports, not firmware file execution.
- UI launch control: the desktop process accepts `--page home|settings|merge|replace`, `--load-report <path>` or `--report <path>`, and `--open-report` so repeatable review can open a page or report modal from command line. These arguments only shape UI state and load existing report JSON; they never execute firmware composition or change profile policy.
- Saved Rules: hidden in the first UI release until the saved-rule workflow is implemented and reviewed.
- Localization: UI implementation uses a bilingual English/Chinese-ready text architecture, with English as the initial default.
- Typography: Inter for Latin/English UI text; Microsoft JhengHei UI for Traditional Chinese on Windows, falling back to Noto Sans CJK TC, Noto Sans TC, and Segoe UI.
- UI priority: core/Application/CLI behavior leads; UI binds to application services after the C# core is ready.

## Navigation model

| Page | Purpose | First production content | Must not contain |
| --- | --- | --- | --- |
| Home | Clean launcher | three large cards: Settings, Replace quick jumps, Merge quick jumps | Memory coverage, reports, mixed workflow controls, IC/Number context |
| Settings | Configure folders, profile packs, strictness, theme, diagnostics access | compact settings groups, profile catalog status, log/report access | direct GitHub secret editing, firmware mutation logic |
| Merge | Normal / AB Code merge entry point | mode selector with Normal enabled and AB Code disabled, shared IC/profile context, slot cards, visual-first memory coverage preview, preview/build actions | Replace controls, hard-coded copy/offset rules |
| Replace | DP / CtrlRAM / General replace entry point | shared IC/Number context, persona selector, base/reference slot cards, overlay slot cards including separate DP/LD cards when a DP Replace profile requires them, visual-first memory coverage preview, preview/build actions | Merge controls, region authorization logic in UI |

## Production shell constraints

- UI data for ICs, Number choices, profiles, flash-map regions, and processor readiness must come from application/profile catalogs or application service results.
- ViewModels must not read firmware bytes directly.
- ViewModels must not call `combiner.exe` or Python workers directly.
- ViewModels must not implement range validation.
- Operation language must stay aligned with `SPEC.md` and `docs/architecture/*`.
- Status labels must distinguish wired, pending, disabled, and unsupported production states.

## Page hierarchy

```text
Shell
  Header: product, page navigation, support state
  Context bar: breadcrumb history, workflow IC/Number only on Merge and Replace
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
- IC and Number remain in the same fixed Device context location across Merge and Replace, and are hidden on Home and Settings.
- Breadcrumb history stays visible across Home, Settings, Merge, and Replace so users can return to earlier page levels.
- Replace UI requires shared Number selection before showing profile-specific regions. First UI should render two-option IC count choices as text and render three-or-more concrete count choices as numeric selection, with future room for Other/custom exceptions.
- Changing IC or Number invalidates workflow-local state that depends on profile context: available modes, selected profile, slot cards, memory coverage, validation issues, preview tokens, and build readiness must be refreshed before Preview or Build can run.
- Saved Rules controls remain hidden in the first UI release.
- Reports and diagnostics are secondary surfaces opened from Preview/Build report modals; Settings may expose diagnostics configuration/export, but not run-specific evidence as a top-level page. The report modal may link to a dedicated in-modal History view, but history entries must not be rendered inline above the current report by default. Until the modal/history surface is complete, the shell may provide a non-navigational `Load report JSON` action that renders existing report JSON into a compact review panel.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Merge and Replace report modals must be persisted into a history view. History entries need the operation step list, IC/IC-num context, input/output hashes, external Combiner command sequence, warnings, artifact path, one-click clear, and local size warning/cleanup affordance so a user can audit or prune what happened after closing the modal.
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
- Existing report JSON can be loaded into a readable review panel for audit/debugging.
- IC and Number use a shared workflow Device context row that stays in the same location on Merge and Replace pages.
- Breadcrumb history is the always-visible navigation context for Home, Settings, Merge, and Replace.
- Replace consumes the shared Number selector.
- The UI uses a bilingual English/Chinese-ready architecture.
- Saved Rules is hidden in the first UI release.
- The initial default language is English.
- The Avalonia UI font stack is fonts:Inter#Inter, Microsoft JhengHei UI, Noto Sans CJK TC, Noto Sans TC, and Segoe UI; technical fixed-width values use Cascadia Mono or Consolas.

## Open UI decisions for review

No open owner decisions are currently recorded for the first production shell.
