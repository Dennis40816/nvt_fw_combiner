# ADR 0075: Bounded bundle preloading inside the required catalog stage

Status: Accepted (owner decision, 2026-09-26, board decision 12: adopted with 4
workers; the owner accepts a 2-4 MB peak private-bytes increase over the
`v1.1.11` package shape, and the other memory gates of decision 8 stand).
Independent design reviews on 2026-09-25: ACCEPT-WITH-CHANGES twice; this
revision answers both.

Amends: [ADR 0049](0049-unified-preload-lifecycle.md), section "Concurrency and
bounds are explicit", only for the required catalog stage.

## Context

The owner requires all startup loading to finish within 2,000 ms of process
launch on every launch, and on 2026-09-25 directed that work run in parallel
wherever possible with no race condition. On the package-equivalent build
(compressed single-file composite ReadyToRun) with the schema, parse and
compilation deduplication units, the required catalog stage takes about
1.49 s and startup finishes about 2.3 s after process launch. The stage is
CPU-bound on one worker thread, dominated by loading, validating and
normalizing built-in profile bundles through each `BuiltInV2Bundle`'s lazy
catalog.

ADR 0049 says: "The required catalog stage runs alone" and "No stage creates an
unbounded queue or nested parallel fan-out." Its goals are bounded resources,
deterministic publication and failure, truthful route-count progress, and
simple cancellation and last-known-good behavior.

A catalog load opens 19 of the 24 trusted bundles (the five General Merge
logical-candidate bundles are not opened). Bundles resolve metadata definitions
from other bundles through `BuiltInCanonicalMetadataDefinitionResolver` inside
their own lazy initialization. On the built output these references form an
acyclic graph with four topological layers over the 19 opened bundles:
L0 = the nine CtrlRAM bundles and `nt51919-nt51929-nt51932-shared-facts`;
L1 = `nt51927-standard-merge`; L2 = the other four Standard Merge bundles,
two shared-facts bundles and `nt51950-ab-merge`; L3 =
`nt51919-nt51929-nt51932-ab-merge`. Each bundle's catalog is a
`Lazy<TrustedProfileBundleCatalog>` in `ExecutionAndPublication` mode, which
caches its value or its exception for the process lifetime.

## Decision

1. The required catalog stage still runs alone: no other preload stage runs
   beside it.
2. Before its serial pass, the catalog source **preloads** the bundle catalogs
   of the fixed layer table, one layer at a time. The first bundle of L0 loads
   alone on the calling thread, so every type initializer, registry and
   embedded schema on the load path is initialized before any worker starts.
   Every remaining bundle of a layer then loads on at most
   `min(4, max(1, ProcessorCount - 1))` workers, and the next layer starts only
   after every bundle of the current layer has finished (a barrier).
3. **No cross-worker waiting, by construction:** a bundle references only
   bundles of earlier layers, which the barrier has completed, so a worker never
   waits for a lazy initialization or type initializer that another worker
   holds. A test recomputes the reference graph from the built output and pins
   it equal to the layer table and acyclic; if the trust index names a bundle
   outside the table, preloading is skipped and the load stays serial.
4. **No shared mutable state between workers, by construction:** each worker
   writes only its own bundle's lazy catalog and that bundle's private file
   snapshots. Process-wide state the workers read is either immutable after the
   serial warm-up (trust index, registries, frozen dictionaries, the resolver),
   a thread-safe collection (the validated-schema `ConcurrentDictionary`; a
   concurrent miss may build a schema twice and keep one, which is a bounded
   cost, not a result difference), or a built `JsonSchema` and its evaluation
   options. A source-level audit of JsonSchema.Net 8.0.5 (source commit
   `3520d7ac43e5c6c9b91abeac5af992eb82ffbf63`, 2026-09-25) found evaluation
   of a fully resolved schema read-only, including shared evaluation options;
   the Draft 2020-12 meta-schema reads the global schema registry; and a build
   writes only its own registry unless its `$schema` is unknown or it points
   into the global registry. The conditions are enforced: bundle schemas
   accept only local references (so every built schema is fully resolved) and
   the Draft 2020-12 dialect; meta-validation and build run under one lock; an
   architecture test forbids production writes to the library's global
   registries and requires a private registry for every schema build. The
   concurrency stress test adds observed-execution evidence.
5. Preloading publishes nothing, reports no progress and decides nothing. A
   worker catches every failure of its bundle and leaves it cached in the lazy
   catalog for the serial pass to report. Cancellation of the load's token
   stops scheduling further bundles; `Parallel.ForEach` waits for started
   bundles, then the cancellation propagates as the load's cancellation, which
   takes precedence over any preload failure, exactly as in the serial pass.
6. The serial pass is unchanged: route classification, compilation, dynamic
   resolution, disclosure, progress (`completedRoutes / (totalRoutes + 1)`),
   publication order, error selection and messages.
7. Equivalence holds for the package's fixed, hash-verified, re-readable
   inputs: the serial pass observes the same values and the same first failure,
   code and message in plan order. Preloading can initialize a bundle that the
   serial pass would not reach because an earlier route failed; a transient
   input failure in such a bundle then stays cached for the process lifetime and
   affects a later reload. This is accepted for built-in package inputs and is
   pinned by a test. Exception stack traces may differ; no catalog consumer
   reads them.
8. Adoption requires, on the package shape and per scored launch (never a
   median alone): peak working set not above the `v1.1.11` package baseline
   (335 MB) and GC heap after warm-up at most 50 MB (board decision 11). Peak
   private bytes may exceed the `v1.1.11` baseline (330 MB) by at most 4 MB
   (owner exception, board decision 12). The frozen candidate is measured on a
   quiet machine; a failing launch goes back to the owner.
9. Optional stages keep ADR 0049's limit of two concurrent workers and their
   ordering edges. This ADR is the only exception to ADR 0049's "no nested
   parallel fan-out" rule, limited to the fixed bundle layers of the required
   catalog stage.

## Parallel scope evaluated (owner directive: parallel wherever possible)

Computation may be parallel while publication stays ordered: workers write
private result slots and one committer reads them in plan order. Per stage:

| Stage | 1.1.12 decision |
| --- | --- |
| Bundle catalogs (dominant cost) | Parallel by layer (this ADR). |
| Static route compilation (7 routes) | Deferred: feasible with ordered result slots, but it reaches the same bundles and compiler after preloading, where the remaining gain is unmeasured; revisit with a trace after this ADR lands. |
| Route classification | Not parallelized: small work; scheduling cost would exceed the gain. |
| CtrlRAM definition expansion (per load) | Deferred: feasible only as one load-private complete expansion; high risk to `CatalogLoadScopedCtrlRamTests` guarantees. |
| Disclosure | Not parallel with routes: it consumes completed route definitions. |
| Full-image metadata plans | Deferred: low gain; provider bundles are already preloaded. |

## Consequences

- Wall time of the catalog stage can fall on multi-core machines. The worker
  bound leaves capacity for the UI thread but does not guarantee CPU
  scheduling. On one- or two-core machines the bound degenerates to serial
  loading.
- Profile or trust-index changes that alter metadata references must update the
  layer table, or the graph test fails.
- Peak allocation concentrates in time; the memory gate is measured with each
  change.
- When ADR 0075 is accepted, ADR 0049 receives the reciprocal `Amended by` link.

## Rejected options

- Parallel publication, progress reporting or error selection: rejected because
  their order would depend on timing. (Parallel computation into ordered slots
  is evaluated above, not rejected.)
- Start catalog loading before the first window: rejected; it conflicts with
  ADR 0049's first-window rule and adds allocation to the window path.
- Unbounded `Parallel.ForEach`, one task per bundle, or all 19 bundles in one
  wave: rejected; they break the resource bound or the no-waiting guarantee.
- `LazyThreadSafetyMode.PublicationOnly` for bundle catalogs: rejected for
  1.1.12; it would change today's cached-failure behavior for every caller.

## Verification

- Existing catalog tests pass unchanged, including exact progress sequences,
  cancellation, failure retention, retry and last-known-good, and
  `CatalogLoadScopedCtrlRamTests`.
- New tests: the layer table equals the recomputed reference graph and is
  acyclic; every preloaded bundle is opened by the serial pass and the excluded
  bundles are exactly the General Merge logical candidates; the same first
  failure, issue code and message with and without preloading, including
  several failing bundles and an earlier route failure; cancellation before,
  during and after preloading; concurrent schema evaluation equivalence.
- A pinned digest of the complete published catalog snapshot, equal with and
  without preloading and across repeated loads.
- Golden regression and formal-route closure tests unchanged.
- Package-shape startup measurement before and after, with the per-launch
  memory figures of decision 8.
