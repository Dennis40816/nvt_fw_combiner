# ADR 0049: Unify observable preload lifecycle without unifying feature semantics

- Status: Accepted
- Date: 2026-08-13
- Accepted: 2026-08-13 by the product and architecture owner; the owner
  explicitly delegated routine roadmap decisions and continuation to the agent
  in the active session
- Owners: Product owner + architecture owner
- Risk: R2 cross-layer lifecycle, Presentation, filesystem, and process-runtime
  boundary; any firmware, support, trust, or release-policy delta retains its
  normal R3 owner gate
- Builds on: ADR 0015, ADR 0026, ADR 0027, ADR 0038, ADR 0046, and ADR 0048
- Amends: ADR 0027's process-restart-only external-environment refresh boundary,
  the `0.10.x` maintainability specification, and the `v0.10.5` roadmap

## Context

`v0.10.4` established a truthful foreground catalog-loading surface after the
first window and moved the seven Report dictionaries under `ReportModal`, so
Report resources are no longer merged by `MainWindow`. The remaining startup
path is still fragmented:

- `MainWindow` owns catalog-specific cancellation, attempt, loading, retry, and
  deferred-completion fields;
- startup report history, command-line report loading, Message Center refresh,
  and five deferred view materializations run as one serial `try` block, where
  one failure skips later optional work;
- report file reads and history restore do not all provide cooperative bounded
  cancellation;
- selected-file inspection has a separate generation/loading/cache lifecycle;
  obsolete work may continue even after its publication becomes stale; and
- external-tool environment discovery is a process-global lazy Bootstrap
  operation with an unused refresh seam and no observable operator action.

These operations do not share semantic authority. The canonical catalog owns
capability publication and last-known-good behavior; Report owns report/history
interpretation; Application inspection owns accepted artifact health and
metadata; and the external-tool runtime owns manifest trust, generation, and
readiness. A generic service that interprets their results would recreate the
facade architecture retired in `v0.10.3`.

The owner nevertheless requires every actual preload and related preparation
operation to be observable, cancellable, bounded, and user-controllable, with
one lifecycle owner and one production execution path for each operation.

## Decision

### Define preload narrowly

A **preload** is work intentionally started before the operator requests the
completed feature result, to reduce later latency or establish a required
startup prerequisite. A **preparation operation** starts in response to an
operator selection but may share the same lifecycle guarantees.

The current operations are classified as follows:

| Operation | Classification | Semantic owner | Lifecycle disposition |
| --- | --- | --- | --- |
| Canonical catalog materialization and publication | Required startup preload | Application catalog | Required foreground stage |
| Startup report-history restore | Optional startup preload | Report Presentation + bounded filesystem store | Optional stage |
| Explicit command-line startup report | Requested startup preparation | Report Presentation + bounded filesystem store | Retry or skip; never silently ignored |
| Message Center system snapshot refresh | Optional startup preload | Application diagnostics port | Optional stage |
| Deferred page/view materialization | Optional startup preload | Presentation | Optional, UI-thread, serial stage |
| External-tool manifest environment discovery | Optional background preload and explicit refresh | Infrastructure runtime adapter | Optional stage plus operator refresh |
| Selected-file metadata and firmware inspection | Selection-triggered preparation, not startup preload | Application inspection + Infrastructure file adapter | Per-workflow single-flight lifecycle |
| Shell preferences | First-frame prerequisite | Presentation bounded local store | Remains before window; trace-only, not skippable preload |
| Preview, Build, processor execution, CtrlRAM metadata needed by a run | Per-run execution | Existing Application execution owners | Excluded; retain existing typed progress/cancellation |

Code does not acquire preload status merely because it uses `Task.Run`, a cache,
or lazy initialization. Ordinary demand-driven computation remains demand
driven.

### One lifecycle owner, typed semantic owners

Presentation owns one shell-level preload session. It owns only:

- one monotonically increasing attempt generation;
- a linked cancellation token and a bounded shutdown/drain deadline;
- a closed, ordered stage plan and a maximum concurrency budget;
- an immutable observable stage collection, selected summary stage,
  current-stage work-unit progress, and admitted-stage position;
- required/optional status, retry, skip, cancel, and completion commands; and
- projection into the reusable foreground loading state and non-blocking
  background status surface.

The shell attempt generation is allocated immediately before an admitted
attempt starts. It is monotonically increasing and is never reused after
success, failure, replacement, or cancellation. Report projection and
selection-triggered inspection generations are likewise request identities:
they are consumed when their request starts so late work cannot match a newer
request. External-environment loads have their own monotonically increasing
request generation for the same stale-work arbitration. By contrast, catalog
publication and external-environment publication generations are semantic
publication identities. Their typed owners increment them only when a fully
validated immutable candidate commits. Cancellation or failure before that
commit consumes the already allocated attempt/request generation, but not a
semantic publication generation.

The session does not cache feature data, parse report/catalog/tool results,
infer firmware facts, or apply a result. Each stage calls its existing typed
owner, and that owner validates and publishes its own result. Bootstrap remains
composition-only. There is no service locator, plugin registry, ambient global
progress slot, or generic result DTO.

Selection-triggered inspection has a separate per-workflow session lifetime,
because it is caused by the selected authoring state rather than shell startup.
It uses the same lifecycle invariants—one generation, cooperative cancellation,
bounded work, observable progress, exactly one typed terminal result—but does
not join the shell startup stage list or its percentage.

### Required and optional stages have different failure policy

The canonical catalog is the only required startup stage. Until its typed
success is applied, workflow selection and Build remain disabled. Failure keeps
the blocking foreground surface and offers `Retry` and `Cancel startup`/exit;
it cannot be skipped and cannot turn retained last-known-good data into a false
cold-start success.

After catalog success, shell interaction is enabled. Optional stages no longer
block the shell. Each optional failure is recorded with its typed feature
diagnostic, then later independent stages continue. The operator may retry the
failed stage, skip the current optional stage, or cancel remaining optional
preloads. Cancel and skip never cancel an active Preview/Build or reinterpret a
feature result.

An explicit `--load-report` request is visible requested work. A read or parse
failure offers retry/skip and remains in report diagnostics; it does not abort
history, Message Center, or deferred-view preload.

The post-catalog dependency order is closed. Launch-page selection is applied
first. Report history completes before an explicit startup report so the
operator-requested report remains the current report; `--open-report` follows
that report chain. Diagnostics may run beside the report chain. Deferred view
materialization may run beside non-UI work but remains internally serial on the
dispatcher. External environment discovery is admitted only when the reviewed
tools root exists and runs as optional background work. The maximum-concurrency
budget never violates these edges merely to make a percentage advance.

### Progress is truthful work completion, not elapsed-time prediction

Every stage reports one of:

- determinate completed work units and an exact total;
- a validated monotonic stage-local fraction derived from those units; or
- indeterminate progress when the owner cannot know a truthful total.

The blocking surface displays the required stage. After shell enablement, a
compact background surface exposes every admitted optional stage and its state;
its summary selects the earliest incomplete stage in deterministic plan order.
The summary shows the selected stage name, `stage index / admitted stage count`,
and that stage's numeric percent when determinate. Heterogeneous route, byte,
manifest, and view units are never summed into a false overall percent. The
stage index communicates lifecycle position; the percentage communicates only
completed work in the named stage and is not an estimate of wall-clock time. No
timer, synthetic smoothing, or guessed duration may invent progress.

Catalog progress retains its exact route-count grammar. Report reads use bytes
read against a validated file-length bound. Deferred views use completed view
count. A stage with no exact denominator stays indeterminate. Progress is
monotonic within one generation; stale-generation, decreasing, non-finite,
duplicate-terminal, and post-terminal updates are ignored or rejected at the
contract boundary as appropriate.

Accessibility announcements are phase/state changes, failures, and decile
transitions only. Raw high-frequency progress events do not flood the polite
live region. Reduced motion changes animation only, never work, cancellation,
or numeric progress.

### Concurrency and bounds are explicit

The required catalog stage runs alone. Optional non-UI stages use a maximum of
two concurrent workers. Deferred Avalonia view materialization stays serial on
the UI dispatcher at background priority and yields to active operator work.
No stage creates an unbounded queue or nested parallel fan-out.

Every file input has an explicit maximum length before allocation. Reads use
streaming/cooperative cancellation and reject growth, truncation, or identity
change according to the owning feature contract. A standalone report selected
through the storage provider or supplied by `--load-report` may remain outside
application-managed roots, but its raw input is limited to 20 MiB
(`20,971,520` bytes) and keeps its user-selected identity. This ceiling leaves
room for worst-case JSON-string escaping plus bounded entry metadata so the
newest maximum-size report still round-trips inside the separate 64 MiB history
file. Before atomic save, the history writer validates the complete serialized
v1 envelope. When duplicating derivable UI summary metadata would cross the
bound, it persists that entry with the schema-v1 empty-metadata representation;
the existing reload path rematerializes the same summary from the report JSON.
It never drops the newest report merely because optional derived metadata would
duplicate it beyond the file bound. Report history retains its separate 64 MiB
persisted-file bound and 12-entry limit. Its 16 MiB
retained-payload limit is a soft budget: the newest valid entry is always kept,
even when that entry alone exceeds the budget, and older entries are evicted.

External-tool discovery is confined to the reviewed tools root and does not
follow reparse-point directories or accept a manifest that resolves outside
that root. One discovery visits at most 4,096 filesystem entries at nesting
depth 16, admits at most 256 `manifest.json` candidates, reads at most 1 MiB
(`1,048,576` bytes) per manifest and at most 16 MiB (`16,777,216` bytes) of
manifest data in total, and orders accepted candidates by normalized path.
Exceeding any bound is one typed environment-load failure and publishes no
candidate. Discovery never launches an executable during preload.

Window close cancels the active preload generation and waits no more than five
seconds for cooperative drain before continuing the existing close policy.
Cancellation observed before semantic publication cannot publish, increment a
semantic publication generation, or enable an action. Any already allocated
shell, report, or inspection request generation remains consumed and cannot be
reused. The ordinary cooperative race after an owner's final cancellation check
retains that owner's existing contract.

### Preserve one production path and cache identity

- Catalog startup, Message Center reload, and CLI diagnostics use the same
  Application catalog loader/publication owner; lifecycle code never reloads by
  an alternate path.
- Report/history startup and manual load share one bounded filesystem reader
  and one report parser/projection path.
- Selected-file inspection returns coherent content identity and stability from
  the filesystem adapter. Presentation owns selection generation and display
  only; path probing and duplicate semantic caches are removed.
- The existing IC-only compiled-classification cache must become bounded and
  bind canonical capability/publication identity, or be deleted in favor of
  canonical recomputation.
- Batch inspection invokes only the authoring strategies identified by the
  typed workflow roles on its inputs. It retains one distinct-path read cache,
  input/result order, diagnostics, and cache-off parity; it cannot skip a
  strategy by guessing from filenames, bytes, or IC identity.
- External-tool discovery moves behind one Infrastructure environment loader;
  Bootstrap wires its generation lease but does not parse manifests or own a
  second refresh implementation.

ADR 0027's restart-only external-environment boundary is superseded for
`v0.10.5`. Explicit refresh materializes a candidate behind that one
Infrastructure owner. A successful candidate atomically publishes the next
generation for future acquisitions; a failed or cancelled refresh retains the
current generation and its diagnostic. Every already-acquired run lease remains
bound to its immutable generation. Process restart remains a valid reset, but
is no longer the only refresh mechanism.

Environment loads are single-flight. Every startup discovery or explicit
refresh allocates an external request generation before waiting for the load
gate. A newer request cancels and supersedes the older request; the owner drains
that request before starting the newer materialization. Commit requires both
the current request generation and a final cancellation check. Therefore an
older request cannot publish after a newer refresh was admitted, while failure
of the newer request still retains the last successfully published environment.

Cache disable, cancellation, clear, retry, or refresh may cause canonical
recomputation only. It cannot change bytes, naming, readiness, diagnostics,
support, evidence, or processor authority.

## Rejected options

- One global preload service that returns untyped objects: rejected because it
  would absorb feature semantics and recreate a facade/service locator.
- Include Preview/Build and selected-file inspection in the startup queue:
  rejected because their trigger, identity, and cancellation scopes differ.
- Move shell preferences behind the visible loading surface: rejected because
  first-frame language, theme, and reduced-motion settings would regress.
- Equal-duration or timer-smoothed percentage: rejected because it represents
  guessed time rather than completed work.
- Prelaunch external processors: rejected because preload has no authority to
  execute tools or stage firmware.
- Serialize every stage: rejected because independent bounded I/O stages can run
  concurrently without changing semantic publication order.
- Continue after a required catalog failure: rejected because workflow actions
  require one current canonical publication.

## Consequences

- Operators can see what is running, how much exact work completed, and can
  retry, skip, or cancel within the stage's safety class.
- Optional startup failures no longer suppress unrelated later work or leave no
  visible status.
- Feature owners remain typed and independently testable; lifecycle convergence
  does not create a second semantic model.
- The implementation must migrate and delete the catalog-specific MainWindow
  fields/coordinator and unused external refresh seam rather than wrap them
  indefinitely.
- Some work moves from best-effort synchronous helpers to cancellable streaming
  adapters. This is a behavior-compatible reliability/performance change, not a
  firmware or support change.

## Verification and release impact

- Characterization tests lock the current catalog, history, report, diagnostics,
  deferred-view, inspection, and external-tool owners before migration.
- Behavioral tests cover required failure, optional failure isolation,
  retry/skip/cancel, stale generation, exact terminal cardinality, bounded
  concurrency, shutdown drain, and progress monotonicity.
- UI tests cover keyboard/focus, blocking versus non-blocking state, localized
  status/actions, reduced motion, percent text, and bounded live announcements.
- Architecture tests reject a second coordinator, feature-result interpretation
  in the lifecycle owner, Bootstrap manifest semantics, Presentation filesystem
  identity probing, and alternate execution paths.
- Performance evidence compares the exact packaged predecessor and candidate on
  the same controlled machine: first-window handle, usable-after-catalog time,
  optional-preload completion, peak working set/private bytes, and work counts.
- The canonical verifier, all 17 Golden cases, report wire, CLI behavior, BIN
  bytes/hashes/naming, profiles, schemas, external protocols, and support truth
  remain unchanged.
- The release remains support-neutral. Any firmware, support, trust-source,
  network, signing, or update-policy change is outside this ADR and stops for
  its normal owner gate.
