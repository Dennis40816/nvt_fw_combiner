# ADR 0075: Bounded bundle preloading inside the required catalog stage

Status: Proposed (design review ACCEPT-WITH-CHANGES on 2026-09-25; this revision
answers findings GR-1 to GR-7).

Amends: [ADR 0049](0049-unified-preload-lifecycle.md), section "Concurrency and
bounds are explicit", only for the required catalog stage.

## Context

The owner requires all startup loading to finish within 2,000 ms of process
launch on every launch (1.1.12 roadmap allocation). On the package-equivalent
build (compressed single-file composite ReadyToRun), the required catalog stage
takes about 1.5 s after the schema-validation fix, and startup finishes
2.39-2.57 s after process launch. A sampled thread-time trace shows the stage is
CPU-bound on one worker thread: built-in profile bundles are loaded, validated
and normalized serially through `BuiltInV2Bundle`'s lazy catalog.

ADR 0049 says: "The required catalog stage runs alone" and "No stage creates an
unbounded queue or nested parallel fan-out." It records no measurement for the
catalog stage itself; its goals are bounded resources, deterministic
publication and failure, truthful route-count progress, and simple
cancellation and last-known-good behavior.

A catalog load opens 19 of the 24 trusted bundles today (the five General
Merge logical-candidate bundles are not opened). Some of them resolve metadata
definitions from metadata-provider bundles through
`BuiltInCanonicalMetadataDefinitionResolver`, inside their own lazy
initialization. Each bundle's catalog is a `Lazy<TrustedProfileBundleCatalog>`
in `ExecutionAndPublication` mode, which caches its value or its exception.

## Decision

1. The required catalog stage still runs alone: no other preload stage runs
   beside it.
2. Before its serial pass, the catalog source may **preload** the bundle
   catalogs that the serial pass opens, in two layers: first every
   metadata-provider bundle, serially; then the remaining bundles on at most
   `min(4, max(1, ProcessorCount - 1))` worker threads. The bound is fixed in
   code and the work list is the fixed bundle set; there is no queue.
3. The preload set is computed from the trust index. A test pins it equal to the
   set of bundles the serial pass opens (all consumers: route classification,
   compilation, dynamic resolution, disclosure, full-image metadata, General
   Replace).
4. Layering prevents cross-thread waits: a test pins that the metadata
   references of every non-provider bundle resolve only to provider bundles, and
   that provider bundles reference no other bundle. With providers already
   initialized, a worker initializing one non-provider bundle never waits for a
   lazy initialization held by another worker. This is a checked precondition,
   not a general acyclicity argument.
5. Preloading publishes nothing, reports no progress and decides nothing. It
   only forces existing lazy catalogs, and it leaves every failure to the serial
   pass. A cancellation of the load's token stops scheduling further bundles and
   propagates as the load's cancellation; a bundle whose load has started
   completes, as in the serial pass.
6. The serial pass is unchanged: route classification, compilation, dynamic
   resolution, disclosure, progress (`completedRoutes / (totalRoutes + 1)`),
   publication order, error selection and messages.
7. Equivalence holds for the package's fixed, hash-verified, re-readable inputs:
   each lazy catalog caches its value or exception, so the serial pass observes
   the same values and the same first failure, code and message in plan order
   as without preloading. A transient input failure is cached whether it occurs
   during preloading or during the serial pass, as today; preloading can only
   move it earlier. Exception stack traces may differ; no catalog consumer reads
   them.
8. Adoption requires, on the package shape and per scored launch (never a
   median alone): peak private bytes and peak working set not above the
   `v1.1.11` package baseline (330 MB and 335 MB), and GC heap after warm-up at
   most 50 MB (board decision 11).
9. Adoption also requires evidence that shared, already-built `JsonSchema`
   instances, the Draft 2020-12 meta-schema and the shared evaluation options
   give identical verdicts under concurrent evaluation (a concurrency stress
   test against the real built-in schemas and valid and invalid documents),
   because the library documents thread safety only for its registries.
10. Optional stages keep ADR 0049's limit of two concurrent workers and their
    ordering edges. This ADR is the only exception to ADR 0049's "no nested
    parallel fan-out" rule, limited to the fixed bundle set of the required
    catalog stage.

## Consequences

- Wall time of the catalog stage can fall on multi-core machines. The worker
  bound leaves capacity for the UI thread but does not guarantee CPU
  scheduling. On one- or two-core machines the bound degenerates to serial
  loading.
- Bundle loading must stay safe to run concurrently for different bundles. The
  shared state it touches today: the lazy catalogs, the process-wide validated
  schema cache (`ConcurrentDictionary`), shared `JsonSchema` instances and
  evaluation options, and static registries initialized by type initializers.
  The design review found no other shared mutable cache in the loader, document
  projection, catalog factory, normalizers or strict reader.
- Peak allocation concentrates in time; the memory gate is measured with each
  change.
- When ADR 0075 is accepted, ADR 0049 receives the reciprocal `Amended by` link.

## Rejected options

- Parallelize route classification, compilation or publication: rejected
  because publication order, progress and first-error selection would then
  depend on timing.
- Start catalog loading before the first window: rejected; it conflicts with
  ADR 0049's first-window rule and adds allocation to the window path.
- Unbounded `Parallel.ForEach` or one task per bundle: rejected; ADR 0049's
  bounded-resource rule stays.
- `LazyThreadSafetyMode.PublicationOnly` for bundle catalogs: rejected for
  1.1.12; it would change today's cached-failure behavior for every caller.

## Verification

- Existing catalog tests pass unchanged, including exact progress sequences,
  cancellation, failure retention, retry and last-known-good, and
  `CatalogLoadScopedCtrlRamTests`.
- New tests: preload-set equality; the provider-layer precondition; the same
  first failure, issue code and message with and without preloading, including
  several failing bundles; cancellation before, during and after preloading;
  concurrent schema evaluation equivalence.
- Golden regression and formal-route closure tests unchanged.
- Package-shape startup measurement before and after, with the per-launch
  memory figures of decision 8.
