# NFC Roadmap

Status: active owner roadmap, amended 2026-07-24.

This is the single canonical roadmap for future NFC work.  The dated roadmap
files under this directory remain release-history and decision evidence only;
they must not receive new future-scope assignments.  A later milestone starts
only from its exact official predecessor release tag.

## Non-negotiable planning rules

- A roadmap item never selects an IC, profile, topology, processor, or memory
  map.  Requested IC plus compiled profile and explicit topology remain the
  only execution authority; PID, filename, version, hash, and inspected input
  metadata are informational.
- Code size is a review metric, not a gate.  Readability, ownership, firmware
  safety, evidence, tests, and human review remain gates.
- The product keeps one typed composition engine.  UI and CLI project typed
  requests/results; they do not acquire firmware semantics.
- R2/R3 work keeps its architecture, firmware-owner, golden, package, and
  release gates even if its planning version changes.

## Historical 0.9.x release index

| Version | Recorded outcome |
| --- | --- |
| `0.9.0` | Raw-BIN Hex Editor and UAT fixes. |
| `0.9.1` | Firmware-model V2 migration. |
| `0.9.2` | Trust-preserving profile-bundle consolidation. |
| `0.9.3` | Evidence-gated AB Code/CtrlRAM version-edit work. |
| `0.9.4` | Deterministic candidate-IC intake. |
| `0.9.5` | Reviewed V2 workflow convergence. |
| `0.9.6` | Support/release evidence consolidation. |
| `0.9.7` | UI-token and exactly-replaced duplication consolidation. |
| `0.9.8` | Feature-frozen code-size convergence. |
| `0.9.9` | Legacy convergence and patch closure. |
| `0.9.10` | Measured performance and Change Report remediation. |
| `0.9.11` | Reconstruction/stabilization. |
| `0.9.12` | CtrlRAM production routing and interaction stabilization. |
| `0.9.13` | Urgent UI/release stabilization. |
| `0.9.14` | AB re-admission, minimum authoring UI, and CI-owned release promotion. |
| `0.9.15` | Final 0.9.x release: AB function opening, output/delivery usability, and review handoff. |

## `0.9.15` terminal release record

`0.9.15` is the final 0.9.x version.  It is based on official `v0.9.14` tag
`58f5bbf4cdbfb4e02036c8c1b40c48aa88fa21f7`, peeled commit
`9b15d8757ccb44167c471ca4e602036066bcdea9`.

It function-opens only NT51919/NT51929/NT51932 AB, NT51950 AB `1 IC`/
`Cascade`, and selector-free NT51951 AB.  It includes typed A/B FlashCode
naming, optional A-only FlashCode delivery for the Perfect-family route, and
delivery-to-review evidence collection.  It does not certify those routes.

Before publishing the stable release, the exact reviewed `0.9.15` head still
requires protected CI, independent Codex review, firmware-owner/release-owner
approval, release-package/provenance evidence, and a clean-Windows package
smoke.  Missing direct AB golden remains a certification debt; its closure is
scheduled below and is not represented as an existing support claim.

## `0.10.x`: support visibility, issue/report experience, and maintainability

`0.10.0` begins only from the official reviewed `v0.9.15` tag.  The complete
`0.10.x` sequence owns the three R2 product tracks below and finishes the
skills/documents/test/production-maintainability program before `0.11.0`
begins.  The ordered minor allocation is a release boundary, not permission to
mix unreviewed refactors into a product slice:

| Version | Ordered scope | Exit boundary |
| --- | --- | --- |
| `0.10.0` | M0/M1 inventory, documentation/skill rationalization, reviewed Matt Pocock active-skill adoption, and Settings Support Matrix. | Exact `v0.9.15` baseline; one reviewed ownership/skill inventory; no route-admission change. |
| `0.10.1` | M2 test-harness convergence and Error experience unification. | Exact `v0.10.0` baseline; characterization remains intact and stable issue codes remain authoritative. |
| `0.10.2` | M3/M4 production refactoring and Report detail/layout/function review. | Exact `v0.10.1` baseline; byte/report/UI parity and all R2/R3 gates remain explicit. |

The product tracks are:

| Track | Required outcome | Boundary |
| --- | --- | --- |
| Settings Support Matrix | Add a read-only Settings entry showing each IC/workflow/topology's function availability, certification state, direct/reusable golden basis, missing-golden debt, firmware-owner gates, and automated-test connection. | It projects typed catalog/profile/evidence facts only; it never selects a route, exposes private BINs, or waives a gate. |
| Error experience unification | Present what failed, affected scope, concise cause, and next action at first glance; retain stable issue codes, severity, raw diagnostics, bilingual text, and accessibility. | It does not swallow failures, replace stable issue codes, or move firmware diagnosis to XAML/code-behind. |
| Report detail, layout, and functional review | Establish a one-glance summary plus progressive diagnostics, output/provenance, range explanation, warning/error distinction, persistence/export, responsiveness, and accessibility. | It preserves typed facts/backward reading and cannot hide a safety failure, fabricate bytes, reread a source BIN, or change execution semantics. |

### Matt Pocock skills transition contract

The upstream source is
[`mattpocock/skills`](https://github.com/mattpocock/skills); a pinned upstream
revision, Codex compatibility review, local routing, and repository-rule
precedence must be recorded before M1 adoption.  M1 is completed within
`0.10.0`, so the later 0.10 work uses the adopted workflow as its development
backbone rather than postponing skill adoption to `0.11`.

The reviewed upstream active inventory contains 22 skills:

| Group | Invocation | Skills |
| --- | --- | --- |
| Engineering | user-invoked | `ask-matt`, `grill-with-docs`, `triage`, `improve-codebase-architecture`, `setup-matt-pocock-skills`, `to-spec`, `to-tickets`, `implement`, `wayfinder` |
| Engineering | model-invoked | `prototype`, `diagnosing-bugs`, `research`, `tdd`, `domain-modeling`, `codebase-design`, `code-review`, `resolving-merge-conflicts` |
| Productivity | user-invoked | `grill-me`, `handoff`, `teach`, `writing-great-skills` |
| Productivity | model-invoked | `grilling` |

Deprecated, in-progress, personal, and miscellaneous upstream directories are
inventory-only; none is silently adopted.  Repository firmware skills remain
authoritative: Matt Pocock workflows must not weaken `AGENTS.md`, Polytail,
profile/CRC-worker contracts, golden regression, supervised branch development,
release readiness, or R2/R3 human gates.

### Maintainability sequence

| Phase | Scope | Exit gate |
| --- | --- | --- |
| M0 | Reconcile to exact `v0.9.15`; inventory module ownership/dependencies, public contracts, tests, documentation, ADRs, and installed skills. | One reviewed baseline report names every item, authority, consumer, overlap, and disposition. No production behavior change. |
| M1 | Rationalize documentation/skills and adopt the reviewed active Matt Pocock skills with a pinned upstream revision and explicit invocation/routing policy. | Every keep/merge/replace/delete choice records retained authority, inbound-link migration, compatibility, and representative workflow validation. |
| M2 | Simplify the test harness before production restructuring: exact duplicate setup/builders/fixtures/assertions, slow/flaky seams, and test-only production seams. | Characterization, contract, architecture, property, integration, and golden responsibilities remain explicit; no evidence is deleted for a metric. |
| M3 | Refactor production in small vertical slices around reviewed ownership seams. | Each slice has a fixed baseline, narrow behavioral/architecture tests, migration/rollback seam, Polytail, independent review, and canonical verification. |
| M4 | Remove superseded adapters/projections/compatibility paths only after all callers move and byte/report/UI parity is proven. | No dormant parallel owner remains; all R3 gates are closed or the route remains blocked. |

The first M3 seams are **canonical presentation semantics and shared semantic
projection contracts**, plus **cohesive repeated-data models**.  Domain owns
facts/invariants; Application owns normalization, stable status/severity, and
capability-sized projections; Presentation owns localization, visual roles,
and accessibility.  Views/ViewModels must not independently derive the same
state, fallback policy, or formatter.  Version rendering is one example, not
the goal.  Similar values with different authority, lifecycle, address space,
or firmware semantics remain deliberately distinct; no universal DTO or god
formatter is introduced.

## `0.11.0`: AB certification and family evidence model

`0.11.0` begins only from the official reviewed `v0.10.2` tag.  It has two
independent certification tracks; neither may use the other as evidence.

1. Close NT51950 AB `1 IC` and `Cascade` direct-golden and firmware-owner
   certification independently.  A `1 IC` vector never certifies `Cascade`.
2. Close selector-free NT51951 AB direct-golden and firmware-owner
   certification independently.  NT51950 evidence never certifies NT51951.
3. Establish the Perfect-family and `ldc-tp-only` evidence model: covered
   workflow/topology, provenance, excluded DP facts, support-matrix/report
   vocabulary, and denied-scope contract tests.  Perfect-family sharing is
   like-for-like only.  LDC TP evidence may cover only approved TP/CtrlRAM
   facts; it never covers DP Replace, Standard Merge, AB Code, DP layout,
   header, command, range, or output size.

## Later `0.11.x` owner queue

These items moved from the former 0.10/future backlog.  They receive an exact
minor version only after the preceding contract and release baseline are
approved:

- Selection-change validation lifecycle: selection changes invalidate dependent
  inspection state; a ready input is valid only for its execution snapshot.
- Unified selection/admission/input-check projection: reusable orchestration
  and presentation, while profile/Application retain firmware authority.
- All-IC/all-mode workflow documentation and Mermaid source retention.
- All-IC/all-mode execution convergence, only after equivalence proof.
- Evidence-backed code reuse and size reduction.
- Customized-plan reuse/import with typed mappings and no arbitrary scripts.
- IC family/rule authoring UI, only after the `0.11.0` evidence model and
  dedicated owner/firmware/architecture workshops.
- Deferred cross-workflow Header/Evidence/Memory coverage work, shared Hex
  viewport, Changes redesign, and global Button feedback.

## Migration rule for older planning files

`0.9.x-completion-roadmap.md`, `v0.9.14-roadmap-and-release-gates.md`,
`v0.9.15-0.9.17-roadmap.md`, and any prior post-`0.10.0` planning document are
historical evidence.  New roadmap entries, resequencing, or time estimates go
only in this file.  Historical documents may link here but do not define active
future scope.
