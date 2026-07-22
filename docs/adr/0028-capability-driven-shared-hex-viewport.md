# ADR 0028: Capability-Driven Shared Hex Viewport

- Status: Proposed for `v0.9.15`; implementation is not authorized by this ADR
- Date: 2026-07-19
- Last amended: 2026-07-22
- Owners: Architecture owner, UI owner
- Risk class when implemented: R2

## Context

The standalone Raw Hex Editor and the Change Report Hex Diff both render
16-byte rows with address, hexadecimal, and ASCII columns. They also share the
same technical-font and theme language, but they intentionally have different
state and authority:

- Raw Hex Editor uses `HexEditorViewportControl` with
  `HexEditorWorkspaceViewModel`. It owns an Application document, history,
  search, structural identity, original-row projection, edit actions, context
  menus, and Save As.
- Change Report Hex Diff uses `ReportHexDiffViewModel` with an
  `ItemsControl`/`DataTemplate`. It owns read-only output/reference snapshots,
  bounded range navigation, address jump, and synchronized
  reason/verdict/evidence/hash details.

The Change Report description above records the `v0.9.10` candidate inspected
on 2026-07-19. Owner direction on 2026-07-20 expects final `v0.9.10` Changes to
return to the `v0.9.9` presentation. That rollback is not part of this ADR and
must not be performed from a later feature branch. U0 re-inspects the exact
released predecessor that exists when U0 begins.

The two surfaces currently duplicate renderer geometry and row presentation.
Merging their host ViewModels or Application models would incorrectly combine
raw-document mutation semantics with report-snapshot semantics. Keeping both
renderers indefinitely would preserve that separation but retain a growing
Presentation maintenance and code-size cost.

## Decision

`v0.9.15` may introduce one Presentation-owned inner Hex viewport foundation.
It owns only:

- address, hexadecimal, and ASCII geometry with 16 bytes per row;
- bounded current/original row rendering;
- focus, selection, hit testing, and keyboard navigation;
- shared theme, high-contrast, and technical-font tokens; and
- accessible row and cell projection.

It does not own file access, raw document/history/search semantics, report
snapshots, semantic range meaning, hashes, Save As, firmware meaning, IC or
profile selection, address-space inference, or expected-change classification.
Raw Editor and Report Diff keep separate host ViewModels and separate source
and action adapters. No common Application binary model is introduced.

### Closed capability profile

Construction requires one immutable, validated
`HexViewportCapabilityProfile`. The profile uses typed dimensions rather than
independent XAML Boolean switches:

| Dimension | Closed values for `v0.9.15` |
| --- | --- |
| Columns | `address`, `hexadecimal`, `ascii` |
| Interaction | `inspect`, `select`, `overwrite`, `structural-edit` |
| Comparison | `none`, `optional-original-rows` |
| Navigation | `address-jump`, `document-scroll`, `semantic-ranges` |
| Decorations | `data-change`, `structural-change`, `search`, `semantic-verdict` |
| Row budget | Validated named-profile document or contextual-segment budget |

Only two named profiles ship initially:

| Profile | Capabilities |
| --- | --- |
| `RawEditor` | Address, hex, and ASCII; selection; document scrolling and address jump; search plus data/structural decorations; optional original rows; explicit edit-action adapter for overwrite and structural edits; initial 12 and maximum 28 current display rows, preserving the existing 300-720 px bounded viewport. |
| `ReportDiff` | Address, hex, and ASCII; inspect-only; semantic-range selection as its only public jump source; data/verdict decorations; optional original rows defaulted off; no edit-action, public address-jump, document-scroll, or search adapter. It renders bounded context segments around report ranges with an ellipsis between discontinuous segments. The fixed before/after row count `N` and hard materialization cap require owner confirmation before U1. |

Bytes per row remains fixed at 16. Arbitrary 8/16/32 layouts,
user-authored profiles, and a general plug-in capability surface are outside
this decision.

### Fail-closed construction

Profile, source, and action adapters are validated before the viewport can be
used. At minimum:

- overwrite or structural-edit authority requires an edit-action adapter;
- `ReportDiff` rejects every edit-action adapter;
- `ReportDiff` rejects public address-jump, document-scroll, and search sources;
- original-row projection requires comparison bytes from the source adapter;
- structural, search, and semantic-range capabilities require their
  corresponding typed sources; and
- invalid row budgets or unsupported combinations fail construction.

Hiding controls or commands in XAML is not a read-only boundary. Negative
architecture and UI tests must prove that `ReportDiff` has zero edit authority.

### Host ownership

Raw Editor keeps its file, search, write, history, context-menu, and Save As
workspace. Its adapter translates the existing Application document state and
explicit edit actions without moving those semantics into Presentation.

Change Report keeps the approximately two-thirds read-only viewport and
one-third `Changed ranges` workspace. The host owns the virtualized/windowed
range collection and accordion state: an unselected card stays compact, the
selected card expands in place to show user-facing Why, Result, and
`Before → After` bytes, and selecting another card collapses the former
selection. The
range selection positions the viewport and only the selected range's modified
bytes receive change highlighting.

The viewport does not expose edit, Go to address, ASCII search, or continuous
document scrolling for `ReportDiff`. It renders only bounded context around
diff ranges and inserts an ellipsis between discontinuous contexts. `Show
original bytes` is off initially and adds verified comparison rows only when
enabled. SHA values, evidence ids, duplicate addresses, audit jargon, and
Previous/Next pagination are not primary Changes UI. Their omission from this
view does not remove typed report facts, persisted compatibility, or complete
export evidence.

The adapter still projects the verified session snapshot and semantic ranges.
Reopened persisted reports continue to show their stored facts and bounded
preview fallback; neither the host nor the viewport rereads source firmware
paths or fabricates missing full bytes.

## Delivery constraint

Migration follows the independently reviewed U0-U5 slices in the
[0.9.x Completion Roadmap](../architecture/0.9.x-completion-roadmap.md). The
legacy Raw Editor renderer and exact final-predecessor Changes presentation
remain available through U3 and U4 for rollback and parity comparison.
Duplicate renderer/template/style code is removed only in U5 after both hosts
pass their complete behavior, performance, accessibility, and read-only gates.

The temporary dual-renderer period is not permission to evade the code-size
policy active when U0 begins. U0 records the exact released predecessor at that
time, then-current ratchets, and deletion budget without
inventing a universal line target. U5 must delete only proven duplicate
renderer/template/style ownership; no safety test or evidence may be removed
to manufacture a reduction.

## Alternatives rejected

- Keep both renderers permanently: retains duplicated geometry, tokens, and
  accessibility work.
- Merge the two host ViewModels or Application models: leaks document mutation
  semantics into report review and report semantics into the raw utility.
- Give one control all search, history, Save As, ranges, verdicts, and evidence:
  creates a god control with incompatible responsibilities.
- Configure behavior with unrelated Boolean properties: permits invalid
  combinations and makes edit authority difficult to audit.
- Enforce Report Diff read-only behavior only through hidden UI: leaves an
  action path available below the visual surface.

## Consequences

- Equivalent states can share geometry, tokens, focus rules, accessibility,
  and bounded materialization without sharing firmware or document meaning.
- The Changed-ranges accordion remains host-owned; adding it to the common
  renderer would recreate the god-control boundary this ADR rejects.
- Adapters add temporary migration code, so U1-U4 require explicit source-size
  accounting and may need rescoping if the existing ceiling has insufficient
  headroom.
- The shared renderer remains a Presentation implementation detail; it does not
  change firmware output, ranges, profiles, processors, Combiner invocation,
  golden evidence, support exposure, or report serialization.
- No new runtime dependency is approved.

## Required review and evidence

Implementation requires independent architecture and UI review, Polytail, the
canonical final verifier, and the manual accessibility/performance evidence
listed in the roadmap. The implementation branch must start from the exact
released predecessor when U0 begins. This planning ADR does not authorize
implementation from a candidate commit.
