# 2026-08-23 Functional Verification Matrix

This document is the detailed functional-verification appendix of
[the single-implementation and layering audit](2026-08-22-single-implementation-and-layering-audit.md).


The exact route denominator is the 78-row `canonical-capability-policy-v1.json`,
not a hand-maintained test list. Catalog `1.9.0` exposes 64 routes for ordinary
authoring and marks all 14 DP Replace routes unavailable by the 2026-08-24 owner
decision; availability is not certification. Evidence remains 76 `ContractOnly`
and two `DirectGolden`; publication remains 72 `Internal`, three `Candidate`, two `Supported`, and one `TestOnly`.

| Workflow | Exact routes / ICs | DirectGolden | ContractOnly | Executable evidence | Remaining gate |
| --- | ---: | ---: | ---: | --- | --- |
| Standard Merge | 14 / 10 | 1 | 13 | Profile Contract, `BuiltInV2StandardMergeRoutingTests`, executable canonical/950/951 fixture-oracle tests, CLI and selector lifecycle smoke; these tests do not rewrite policy `EvidenceStatus` | Firmware-owner/direct evidence for certification; owner-visible full-flow check |
| AB Merge | 6 / 5 | 0 | 6 | AB runtime admission, topology, executable fixture regression, CLI, six-order readiness and immutable-session tests; these tests do not rewrite policy `EvidenceStatus` | Same-TP authoring contract; direct evidence and firmware-owner certification |
| DP Replace | 14 / 10 | 1 | 13 | Hidden from ordinary authoring; retained V2 routing, changed-input Golden Regression, 950/951 synthetic oracle, and fail-closed product smoke | Owner decision at `1.1.0` to retire or reopen; non-Standard naming gaps and certification evidence remain if reopened |
| CtrlRAM Replace | 33 / 10 | 0 | 33 | V2 registry/plan closure, processor and report metadata, per-family evidence, NT51950 Normal-CtrlRAM non-termination, immutable-session tests | Typed Base discovery; trusted IC provenance; direct firmware/output evidence and R3 sign-off |
| General Merge | 10 / 10 | 0 | 10 | Candidate-profile, CLI, Saved Rule, initializer/mapping/engine tests | R3 owner/evidence decision for stale promotion-blocker metadata; owner-visible mapping/Build check |
| General Replace | 1 / 1 | 0 | 1 | Candidate profile, postbuild readiness, patch, ADR 0044 plan-only Diagnostic Preview, memory projection and UI tests | Owner-visible POSTBUILD/tool-unavailable and output-delivery check; ADR 0044 grants no fixed-workflow Diagnostic Preview authority |

The route-policy `DirectGolden` column above is a publication/evidence-status
fact and is not the physical canonical fixture count. A separate point check at
`7f853623` found 46 canonical cases and 248 physical artifacts totaling
50,904,224 bytes. Every declared physical path, size, SHA-256, case identity,
workflow, and alias source resolved without error:

| Canonical workflow | Direct Golden | Direct input evidence | Fact-scoped alias | Current executable disposition |
| --- | ---: | ---: | ---: | --- |
| Standard Merge | 11 | 0 | 2 | The data-driven workbench runner executes all 11 direct cases byte-for-byte and both approved aliases. |
| AB Merge | 3 | 0 | 4 | Bootstrap Golden tests execute the NT51929 direct case, both NT51950 direct cases, approved NT51919/NT51932 alias parity, and the separately scoped NT51951 synthetic plan. |
| CtrlRAM Replace | 18 | 2 | 6 | Canonical-backed per-family tests cover most active cases, but there is no closed manifest-to-runner coverage gate and some consumers still read the legacy CtrlRAM evidence root. |

Focused evidence passed on 2026-08-23: canonical Python validation 57/57,
`NvtFwCombiner.GoldenRegression.Tests` 17/17, and AB Golden regression 8/8.
This does not yet prove every retained canonical case has the right executable
test. No current C# canonical runner names the two NT51920 direct cases, the
NT51930 direct case, or the NT51931 direct case. The owner explicitly retired
NT51920, NT51930, and NT51931 on 2026-08-23, so these gaps must be removed with
their active catalog, test, package, and Golden residue rather than filled with
new runners. The handoff records that bounded R3 evidence cleanup as
`RET-IC-01`; immutable `refcode/` and accepted historical provenance remain.
GOLD-01 closes this finding: the canonical denominator is 39 cases, every case
has one fail-closed typed disposition, and NT51926 FW 1.4.1 selective-VN
regression evidence is a supporting provenance artifact in its canonical case.
The closed GOLD-01 declaration ledger is 175 artifact facts totaling
41,406,330 declared bytes; the physical canonical payload tree is 172 files
and 40,693,626 bytes because repeated case facts do not create copied payloads.
Bundle CLI, CtrlRAM CLI, candidate, and UI tests now use the canonical loader;
the active legacy manifest, fail-open verifier, and duplicate payload roots are
retired. The separate 20260717 diagnostic quarantine remains non-executable.

`RET-IC-01` candidate result on 2026-08-23 removes the seven retired Direct
Golden cases and all retired-only active evidence consumers. The resulting
closed inventory is 39 cases, 174 artifacts, and 41,160,570 bytes: Standard
Merge 8 Direct Golden + 2 aliases; AB Merge unchanged at 3 Direct Golden + 4
aliases; CtrlRAM Replace 14 Direct Golden + 2 Direct Evidence + 6 aliases. A
pre-delete survivor ledger now locks the complete normalized JSON of all 39
case manifests and the exact 174 `(caseId, artifactId, role, path, size,
sha256)` facts. Raw map/Postbuild/source-generator snapshots remain
byte-identical historical provenance; they are not runtime, selector,
publication-policy, or executable-Golden authority.

The required closure is data-driven and fail-closed: every canonical case must
be claimed as exactly one of active direct full-output execution, explicitly
explained allowed-byte-difference execution, retired/non-executable artifact
integrity plus route-blocking evidence, input-only direct evidence, or
fact-scoped alias resolution. A new canonical case without a declared runner
disposition must fail verification. Alias and input-only evidence must never be
promoted to direct product parity.

The route-level matrix is supplemented by the following cross-cutting surface
matrix. `Automated green` means the frozen verifier protects the declared
behavior; it never promotes a route or replaces a visual/OS/firmware-owner
gate.

| Surface | Current automated evidence | Current disposition / open gap |
| --- | --- | --- |
| Canonical catalog and Support Matrix | Policy/profile materialization, fingerprint, Support Matrix Application/Bootstrap/UI/architecture tests | Automated green for 78-row identity and separation; 64 authoring-available, 14 DP Replace unavailable, and 76 rows remain ContractOnly |
| Selector lifecycle and immutable input identity | Standard/AB/CtrlRAM ordering, Checking/Error/recovery, cancel no-op, 100 MB intake, same-path binding, post-Verified mutation | Automated green for approved slice; exact-one-file drop, same-TP authoring, per-slot Clear and CtrlRAM Base discovery remain open |
| Preview/Build and firmware execution | Shared engine, accepted session, operation/trace/report, golden and processor write-range suites | Automated green; ADR 0044 authorizes only General Replace plan-only Diagnostic Preview, not fixed-workflow execution/re-labelling of invalid selected bytes; no firmware byte/range/name/CRC change is authorized by this audit |
| Output naming and delivery | Naming parity, bundle proposal/admission, atomic loose/bundle writers, AB additional delivery, UI/CLI/report parity | Automated green for declared rules; DP/CtrlRAM non-Standard naming requires its firmware-owner/typed-artifact contract decision; native Save As/bundle owner check remains manual |
| Memory Layout | Application projector, Bootstrap convergence, Merge/Replace/UI template and interaction smoke | Automated green for semantic projection and approved Option B; final owner visual check remains manual across theme/language/compact widths |
| Reports and history | Typed report, JSON compatibility, Hex Diff/replay, bounded history persistence and report UI tests | Automated green; OS file reveal/import-export owner check remains manual |
| System Activity / diagnostics | Application bounded/privacy-filtered activity, exporter, Message Center and reference-template tests | Startup-duration event and narrow-window responsiveness remain open |
| Settings/preferences | Modal isolation, bounded preference storage and UI tests | Automated behavior green; remaining Settings positioning/scroll/typography work is visual and requires reference |
| Version management / launcher | Catalog validation, package/install integrity, state store, concurrency, activation rollback, local lab and Version UI tests | Automated local boundary green; UNC/network repository, real side-by-side launcher/restart and update prompt require owner-visible Windows checks |
| Localization/theme/accessibility | Resource/architecture, Light/Dark, focus/keyboard/reduced-motion and UI-smoke contracts | Automated contracts green; Traditional Chinese/English narrow-window and high-contrast visual review remains manual |
| Hex Editor / read-only viewport | Application editor/search, UI edit/cancel/style and report viewport tests | Automated green; large-file interaction and native Save As remain owner-visible OS checks |
| Packaging/release | Structure, package/release policy and managed package security tests | Clean Windows package, provenance/signing, release-owner approval, and the exact `1.0.0` IC/workflow support-subset firmware-owner sign-off remain outside this audit |

### Owner-visible functional-freeze checklist

This dated checklist is the manual half of the matrix. It remains unchecked
until the owner performs or explicitly accepts each observation on the exact
candidate build; headless evidence cannot auto-complete it.

Before checking any item, record `candidate build / commit`, `executor`,
`execution date`, `result`, and an evidence reference (screenshot, report,
hash, or signed review note) next to that item. Blank metadata is a failing
gate, not an implicit pass.

- [x] Standard Merge: representative ordinary, TP-first and NT51950 DP-first
  selection through native Browse/drop, Preview, Build, output name and report.
  **Owner-visible acceptance recorded 2026-08-23:** candidate production
  baseline `0.10.6@c660e84e` under docs-only audit head `7f853623`; executor
  repository owner Dennis; result PASS for the currently exposed Standard Merge
  flow; evidence reference is the owner's explicit acceptance in this task,
  supplemented by the canonical Standard full-byte regression suite.
- [x] AB Merge, currently exposed surface: exposed topology selection, two
  independent TP cards, additional A delivery, output name and report.
  **Owner-visible acceptance recorded 2026-08-23:** candidate production
  baseline `0.10.6@c660e84e` under docs-only audit head `7f853623`; executor
  repository owner Dennis; result PASS for the currently exposed independent-
  TP flow; evidence reference is the owner's explicit acceptance in this task,
  supplemented by focused AB Golden regression 8/8. The approved future
  `Use the same TP for A and B` option is not yet an exposed surface and remains
  separately open in the selector TODO; it requires its own automated and
  owner-visible acceptance after implementation.
- [ ] DP Replace: representative retained route, exact-capacity failure,
  Preview/Build and output name, without treating AB FlashCode as Normal DP
  authority. Final owner keep/gate/retire decision remains separate.
- [ ] CtrlRAM Replace: representative family/topology including NT51950 Normal
  CtrlRAM, Base discovery, Warning/Error, firmware-version Preserve/Edit,
  POSTBUILD tool readiness, Build and report. Base discovery remains blocked
  on its typed non-terminal-state implementation.
- [ ] General Merge and General Replace: mapping edits, overlap/protected-range
  failures, Saved Rule, General Replace plan-only Diagnostic Preview, tool-
  unavailable state, Build and output delivery.
- [ ] Selector/native OS interaction: cancel exact no-op, replacement while
  Checking, invalid extension recovery, multi-file drop rejection after the
  exact-one contract lands, and no cross-page/session leakage.
- [ ] Memory Layout: approved Option B hover-only map, card overflow, correlated
  row state and no white edge in Light/Dark, English/Traditional Chinese and
  compact widths.
- [ ] Output/report OS integration: editable locked-name flow, loose versus
  bundle folder, collision suffix, native Save As, report import/export and
  Explorer reveal.
- [ ] Settings, System Activity and accessibility: workflow inputs survive the
  modal; narrow-window scrolling does not overlap; startup duration appears
  after implementation; keyboard/focus/reduced-motion/high-contrast behavior
  is readable in both languages/themes.
- [ ] Version management: moved local source, offline installed switching,
  damaged-version delete, retention prompt, real UNC/network source, update
  consent, side-by-side launcher ready handshake and rollback/restart.
- [ ] Hex Editor: representative large-file edit/search/undo/redo/cancel and
  native Save As; read-only report/BIN viewports retain bounded interaction.
- [ ] Candidate package: clean Windows smoke, package allowlist/integrity,
  provenance/SBOM/signing as applicable, and exact `1.0.0` support-subset plus
  firmware/release-owner approval.

The full functional-verification TODO therefore remains open. The automated
baseline is coherent, but accepting the freeze still requires the named manual
Windows/visual checks and the firmware/evidence decisions above. Refactoring
may start only after the owner accepts that gap ledger; a green test count alone
is insufficient.

