# ADR 0028: Capability-Driven Shared Hex Viewport

- Status: Proposed for `v0.9.11`; implementation is not authorized by this ADR
- Date: 2026-07-19
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

The two surfaces currently duplicate renderer geometry and row presentation.
Merging their host ViewModels or Application models would incorrectly combine
raw-document mutation semantics with report-snapshot semantics. Keeping both
renderers indefinitely would preserve that separation but retain a growing
Presentation maintenance and code-size cost.

## Decision

`v0.9.11` may introduce one Presentation-owned inner Hex viewport foundation.
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

| Dimension | Closed values for `v0.9.11` |
| --- | --- |
| Columns | `address`, `hexadecimal`, `ascii` |
| Interaction | `inspect`, `select`, `overwrite`, `structural-edit` |
| Comparison | `none`, `optional-original-rows` |
| Navigation | `address-jump`, `document-scroll`, `semantic-ranges` |
| Decorations | `data-change`, `structural-change`, `search`, `semantic-verdict` |
| Row budget | Validated bounded initial and maximum row counts |

Only two named profiles ship initially:

| Profile | Capabilities |
| --- | --- |
| `RawEditor` | Address, hex, and ASCII; selection; document scrolling and address jump; search plus data/structural decorations; optional original rows; explicit edit-action adapter for overwrite and structural edits; initial 12 and maximum 28 current display rows, preserving the existing 300-720 px bounded viewport. |
| `ReportDiff` | Address, hex, and ASCII; inspect-only; address jump and semantic ranges; data/verdict decorations; optional original rows; no edit-action adapter; initial 48 and maximum 128 current rows. |

Bytes per row remains fixed at 16. Arbitrary 8/16/32 layouts,
user-authored profiles, and a general plug-in capability surface are outside
this decision.

### Fail-closed construction

Profile, source, and action adapters are validated before the viewport can be
used. At minimum:

- overwrite or structural-edit authority requires an edit-action adapter;
- `ReportDiff` rejects every edit-action adapter;
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

Change Report keeps the approximately two-thirds viewport and one-third
reason/verdict/evidence/hash/range workspace. Its adapter projects the verified
session snapshot and semantic ranges. Reopened persisted reports continue to
show their stored facts and bounded preview fallback; neither the host nor the
viewport rereads source firmware paths or fabricates missing full bytes.

## Delivery constraint

Migration follows the independently reviewed U0-U5 slices in the
[0.9.x Completion Roadmap](../architecture/0.9.x-completion-roadmap.md). The
legacy Raw Editor renderer and Report Diff template remain available through
U3 and U4 for rollback and parity comparison. Duplicate renderer/template/style
code is removed only in U5 after both hosts pass their complete behavior,
performance, accessibility, and read-only gates.

The temporary dual-renderer period is not permission to raise the current
60,000-line production ceiling or either named partial ratchet. U0 records the
exact final `v0.9.10` predecessor baseline and the deletion budget. U5 must
leave the shared-viewport slice net negative and lower the applicable
`v0.9.11` descending ratchets; no safety test or evidence may be removed to
manufacture a reduction.

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
final `v0.9.10` protected-main/tag predecessor. This planning ADR does not
authorize implementation from a candidate commit.
