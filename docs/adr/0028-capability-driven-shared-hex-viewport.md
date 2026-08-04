# ADR 0028: Capability-Driven Shared Hex Viewport

- Status: Accepted; Raw Editor foundation implemented by #191, Report Diff adoption remains #192
- Date: 2026-07-19
- Last amended: 2026-08-03
- Owners: Architecture owner, UI owner
- Risk class when implemented: R2

## Context

The standalone Raw Hex Editor and the Change Report Hex Diff both render
16-byte rows with address, hexadecimal, and ASCII columns. They also share the
same technical-font and theme language, but they intentionally have different
state and authority:

- Raw Hex Editor composes the Presentation-owned, always-read-only
  `HexViewportControl` from an immutable `HexViewportSnapshot`. Its host adapter
  keeps the Application document, history, search, structural identity,
  original-row projection, edit overlay, context menus, and Save As authority.
- Change Report Hex Diff uses `ReportHexDiffViewModel` with an
  `ItemsControl`/`DataTemplate`. It owns read-only output/reference snapshots,
  bounded range navigation, address jump, and synchronized
  reason/verdict/evidence/hash details.

The Change Report description above records the `v0.9.10` candidate inspected
on 2026-07-19. Owner direction on 2026-07-20 expects final `v0.9.10` Changes to
return to the `v0.9.9` presentation. That rollback is not part of this ADR and
must not be performed from a later feature branch. U0 re-inspects the exact
released predecessor that exists when U0 begins.

Before #191, the two surfaces duplicated renderer geometry and row presentation.
Merging their host ViewModels or Application models would incorrectly combine
raw-document mutation semantics with report-snapshot semantics. Keeping both
renderers indefinitely would preserve that separation but retain a growing
Presentation maintenance and code-size cost.

## Decision

The owner-selected `0.10.x` #191 milestone introduces one Presentation-owned
inner Hex viewport foundation; #192 adopts it for Report Diff and BIN Inspector.
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

| Dimension | Closed values for the initial Presentation rollout |
| --- | --- |
| Columns | `address`, `hexadecimal`, `ascii` |
| Interaction | `inspect`, `select`, `overwrite`, `structural-edit` |
| Comparison | `none`, `optional-original-rows` |
| Navigation | `address-jump`, `document-scroll`, `semantic-ranges` |
| Decorations | `data-change`, `structural-change`, `search`, `semantic-verdict` |
| Row budget | Validated named-profile document or contextual-segment budget |

Three named profiles ship through #191/#192:

| Profile | Capabilities |
| --- | --- |
| `RawEditor` | Address, hex, and ASCII; selection; document scrolling and address jump; search plus data/structural decorations; optional original rows; explicit edit-action adapter for overwrite and structural edits; initial 12 and maximum 28 current display rows, preserving the existing 300-720 px bounded viewport. |
| `ReportDiff` | Address, hex, and ASCII; inspect/select only; semantic-range selection as its only public jump source; data/verdict decorations; optional original rows defaulted off; no edit action, public address jump, whole-document scroll, or search adapter. One selected range exposes its complete retained diff plus two aligned context rows before and after. Range-local scrolling is a semantic-range action and materializes only 12 to 28 rows at a time. |
| `BinInspector` | Address, hex, and ASCII; inspect/select only; one primary byte plane; semantic metadata-structure/field selection and range-local scrolling; no comparison, edit, save, whole-document scroll, public address jump, or search adapter. It accepts only resolved metadata-structure instances, Application-formatted facts, and immutable bytes for those instances. |

Bytes per row remains fixed at 16. Arbitrary 8/16/32 layouts,
user-authored profiles, and a general plug-in capability surface are outside
this decision.

### Fail-closed construction

Profile, source, and action adapters are validated before the viewport can be
used. At minimum:

- overwrite or structural-edit authority requires an edit-action adapter;
- `ReportDiff` and `BinInspector` reject every edit-action adapter;
- `ReportDiff` and `BinInspector` reject public address-jump,
  whole-document-scroll, and search sources;
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
selected card expands in place to show user-facing Why and Result, and
selecting another card collapses the former selection. Before/output byte
lists do not appear in the card because the shared viewport is the sole byte
comparison surface. Range selection positions the viewport at the range start;
only that selected range's modified bytes receive change highlighting.

The viewport does not expose edit, Go to address, ASCII search, or continuous
whole-document scrolling for `ReportDiff`. A selected large range may scroll
only inside its retained range plus the two aligned context rows on either
side. `Show original bytes` is off initially and adds verified comparison rows
only when enabled. SHA values, evidence ids, duplicate addresses, audit jargon,
and Previous/Next pagination are not primary Changes UI. Their omission from
this view does not remove typed report facts, persisted compatibility, or
complete export evidence.

The live adapter still projects the verified session snapshot and semantic
ranges. Report generation additionally persists one immutable replay segment
per reported difference: the complete before/output bytes for the difference
plus at most two aligned 16-byte context rows before and after, clipped to the
declared output bounds. It does not persist the complete BIN merely to serve
the viewport. Reopened reports reproduce the same range-local rows and
highlighting without rereading source firmware paths. A legacy report without
complete replay bytes says that Diff preview is unavailable; neither host nor
viewport fabricates missing bytes.

Each persisted replay plane carries its own SHA-256, and its changed-range
slice must also match the row's existing before/after evidence hashes. Readers
fail closed when either the context plane or changed slice loses that identity.
If the aligned replay envelope would retain the complete artifact, the report
omits Replay and the viewport states that bytes are unavailable.

The BIN Inspector host is a separate read-only adapter. It receives the exact
resolved metadata-structure instances and immutable structure bytes from the
Application inspection path, presents values from the one common Application
formatter, and uses semantic structure/field selection to position the shared
viewport. It never accepts an IC id, infers a map/topology/slot from bytes, or
duplicates field offsets, encodings, assertions, ranges, or formatter rules in
Presentation.

The reusable desktop host consumes one Application-owned BIN inspection
snapshot. That factory retains the formatter root's resolution token and
authoring revision, verifies the complete artifact identity/hash set evaluated
by the inspection, and only then copies resolved structure slices. Presentation
cannot assemble detached structures or pair metadata from one artifact with
same-length bytes from another. Active workflow route wiring remains owned by
#208.

## Delivery constraint

#191 extracts the immutable snapshot, source-neutral intents, always-read-only
renderer, and Raw Editor adapter without changing Report Diff. #192 may add the
`ReportDiff` adapter only after the #191 editor parity, accessibility,
performance, architecture, Polytail, and final-verifier evidence is accepted.
The Report Diff host remains separately owned until that adoption is complete.

Duplicate renderer/template/style code is removed only after both hosts pass
their complete behavior and read-only gates. No safety test or evidence may be
removed to manufacture a code-size reduction, and no runtime dependency is
added merely to express this Presentation seam.

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
  golden evidence, or support exposure. #192 extends report serialization only
  with backward-compatible immutable replay segments; those segments carry no
  new firmware meaning or execution authority.
- No new runtime dependency is approved.

## Required review and evidence

Implementation requires independent architecture and UI review, Polytail, the
canonical final verifier, and manual accessibility/performance evidence. #191
starts from its approved `0.10.x` integration base and stops at the owner merge
gate; #192 repeats those gates for Report Diff adoption.
