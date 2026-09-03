# Post-v1.1.0 navigation and CtrlRAM first-open handoff

Status: allocated to `v1.1.6` for reuse decision and controlled diagnosis only.

This handoff records two owner-observed usability/performance problems. It is
not part of `v1.1.1`, and does not authorize a UI, Application, preload,
profile, firmware, or support change. `v1.1.6` allocates the reuse decision and
measured diagnosis below; implementation and any performance claim still
require a measured target and separate owner approval.

## 1. Shared first-entry IC selection

### Observation and intended direction

Directly clicking a workflow in the navigation can enter through a different
experience from Home. On a user's first entry, the product should guide them
to select an IC through the same semantic flow used by Home rather than leave
the destination page to invent its own prompt or selection state.

The intended direction, pending design approval, is:

- when the destination workflow has no current, accepted, compatible IC
  context, navigation presents the shared IC-selection/admission experience;
- once that workflow has a current accepted IC context, later navigation may
  skip the chooser and enter the page directly; and
- a stale, removed, unsupported, or destination-incompatible context must not
  silently satisfy the skip rule.

“Already selected” is not yet defined as process-wide, persisted, or
workflow-session state. No implementation may guess that lifetime or infer IC
identity from a filename, cached label, or Presentation-only state.

### Required reuse inventory

Before proposing code, identify the existing Home entry command, shell
navigation seam, `AuthoringSessionState` transition, canonical capability
selection/admission result, and accepted-session publication already used by
the six workflows. Extend that owner when its contract is insufficient. Do not
add a second IC catalog, remembered-selection store, workflow-specific
selection service, or UI-only admission rule.

### Decisions to close

1. Whether a remembered selection is scoped to one workflow session, the
   current application session, or an explicitly persisted preference.
2. Whether an accepted IC may be reused across workflows and how destination
   capability/IC Count compatibility is revalidated.
3. Which events invalidate the shortcut, including catalog reload, IC or IC
   Count change, route removal, and session reset.
4. Back, Cancel, explicit “Change IC”, deep-link/direct-navigation, keyboard,
   focus, screen-reader, and localization behavior.
5. Whether selection and destination navigation form one atomic transition or
   a resumable two-step flow when loading or validation fails.

### Future acceptance outline

- First direct entry with no valid context prompts through the shared flow.
- A current compatible accepted context enters directly without another
  prompt.
- Invalid or stale context prompts again and cannot publish a partial page
  state.
- Home and direct navigation exercise the same Application-owned transition
  and produce equivalent typed outcomes.
- Switching IC remains visible and reversible; skipping the chooser never
  removes the user's way to correct an accidental selection.

## 2. CtrlRAM Replace cold first-open performance

### Observation

The owner observes that opening CtrlRAM Replace for the first time is
materially slow. There is currently no controlled baseline or stage timing, so
this handoff makes no root-cause or improvement claim.

### Investigation contract

Use the repository bug/performance diagnosis loop before changing production
code:

1. Reproduce on a named build/package and controlled Windows machine. Record
   fresh-process first navigation separately from the second/warm navigation.
2. Hold the selected IC, IC Count/topology, catalog/profile identity, window
   size, theme, and preload state constant; record median and individual runs.
3. Capture elapsed work at existing ownership seams: navigation/admission,
   accepted-session/capability resolution, ViewModel construction, XAML/control
   materialization, slot/card population, Memory Layout/selector projection,
   and deferred preload/dispatcher work.
4. Determine whether work is duplicated, synchronously awaited, performed
   before visible content is necessary, or already available from the unified
   preload and immutable session publications.
5. Add a red-capable regression at the narrowest stable seam before the fix.
   Prefer deterministic work-count or lifecycle evidence; make elapsed time a
   gate only after a reproducible baseline exists.

The implementation must reuse the existing navigation, accepted-session,
capability, projection, and unified-preload owners. It may defer safe
presentation materialization or reuse immutable results, but cannot hide
loading, bypass catalog/profile validation, duplicate firmware facts in UI,
preselect support, or weaken Preview/Build readiness.

### Evidence and completion criteria to define after diagnosis

- exact cold and warm baselines plus the stage that owns the dominant delay;
- an owner-approved target derived from those measurements, not an invented
  universal threshold;
- equivalent IC/workflow/slot/readiness state before and after optimization;
- no additional semantic owner or unbounded cache;
- focused lifecycle/UI evidence and the risk-proportionate canonical verifier
  gate; and
- a fresh packaged-Windows observation if the fix depends on XAML, trimming,
  dispatcher scheduling, or package startup behavior.

## Handoff boundary

The two items may share discovery of shell navigation and preload behavior but
are not one implementation ticket by default. The first is an interaction and
state-lifetime decision; the second is a measured performance diagnosis. Split
their implementation and review unless evidence shows that one existing owner
and one bounded change safely resolves both.
