# 0.1.1 UI Information Architecture

`0.1.1` defines the first demo UI shape. It must help users and engineers understand the product model without embedding firmware semantics in XAML, ViewModels, or temporary handlers.

## Product surfaces

```text
Home
Merge
Replace
Saved Rules
Reports
Settings
Diagnostics
```

## Navigation model

| Page | Purpose | First demo content | Must not contain |
| --- | --- | --- | --- |
| Home | Explain status and next actions | profile pack status, recent runs placeholder, support state | byte mutation logic |
| Merge | Standard / AB / General entry point | mode cards, IC selector placeholder, input cards, plan preview placeholder | hard-coded copy/offset rules |
| Replace | Display / TP HW / TP FW / General entry point | persona cards, base image placeholder, replaceable region placeholder | region authorization logic in UI |
| Saved Rules | Manage promoted General mappings | draft/candidate/supported rule cards | scripts, executable paths |
| Reports | Inspect build/preview evidence | output hash, mutation table, issue list placeholder | raw firmware bytes |
| Settings | Configure folders/profile packs/strictness/theme | non-destructive settings layout | direct GitHub secret editing |
| Diagnostics | Terminal/log/report support surface | read-only terminal transcript, structured log summary | arbitrary shell |

## Demo shell constraints

- Demo may use static sample text or synthetic typed data.
- Demo must not read firmware files.
- Demo must not call `combiner.exe` or Python workers.
- Demo must not implement range validation in ViewModels.
- Demo must keep all operation language aligned with `SPEC.md` and `docs/architecture/*`.

## Page hierarchy

```text
Shell
  Sidebar navigation
  Header: product, branch/milestone, support state
  Content region
    Home dashboard
    Merge page
    Replace page
    Saved Rules page
    Reports page
    Settings page
    Diagnostics page
  Status bar: profile catalog, validation state, diagnostics shortcut
```

## Required UX concepts

- Every build-like flow must have Preview before Build.
- Risky processors show readiness before execution.
- General mapping rows can be saved as rules after validation/review.
- Reports must tie UI state, operation trace, external processor invocation, mutation ranges, and output hash via `runId`.
- Terminal pane is read-only and sanitized.

## Open UI decisions for review

1. Should Merge and Replace be separate top-level pages or separate modes inside one Compose page?
2. Should Diagnostics be always visible as a bottom drawer or a separate page?
3. Should Saved Rules be exposed in `0.1.1` demo or only as a disabled navigation item?
4. Should memory map be table-first or visual-map-first for the first demo?
5. Which language set is required for first demo: English, Chinese, or both?
