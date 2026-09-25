# ADR 0075: Bounded bundle preloading inside the required catalog stage

Status: Proposed (design review pending, 2026-09-25).

Amends: [ADR 0049](0049-unified-preload-lifecycle.md), section "Concurrency and
bounds are explicit", only for the required catalog stage.

## Context

The owner requires all startup loading to finish within 2,000 ms of process
launch on every launch (1.1.12 roadmap allocation). On the package-equivalent
build (compressed single-file composite ReadyToRun), the required catalog stage
takes about 1.5 s after the unit 1 schema-validation fix, and startup finishes
2.39-2.57 s after process launch. A sampled thread-time trace shows the stage is
CPU-bound on one worker thread: every built-in profile bundle is loaded,
validated and normalized serially through `BuiltInV2Bundle`'s lazy catalog
while the machine's other cores are idle.

ADR 0049 says: "The required catalog stage runs alone" and "No stage creates an
unbounded queue or nested parallel fan-out." It records no measurement for the
catalog stage itself; its goals are bounded resources, deterministic
publication and failure, truthful route-count progress, and simple
cancellation and last-known-good behavior.

The 19 bundles a load opens are independent except for metadata-provider
bundles, which other bundles resolve through
`BuiltInCanonicalMetadataDefinitionResolver`. Each bundle's catalog is a
`Lazy<TrustedProfileBundleCatalog>` in `ExecutionAndPublication` mode, which
caches its value or its exception.

## Decision

1. The required catalog stage still runs alone: no other preload stage runs
   beside it.
2. Before its serial pass, the catalog source may **preload** the built-in
   bundle catalogs that a catalog load opens, on at most
   `min(4, max(1, ProcessorCount - 1))` worker threads. The bound is fixed in
   code; there is no queue beyond the fixed bundle list.
3. Preloading publishes nothing, reports no progress and decides nothing. It
   only forces each bundle's existing lazy catalog. It ignores every failure;
   failures surface only through the unchanged serial pass.
4. The serial pass is unchanged: route classification, compilation, dynamic
   resolution, disclosure, progress (`completedRoutes / (totalRoutes + 1)`),
   publication order, error selection and messages. Because each lazy catalog
   caches its value or exception, the serial pass observes the same values and
   the same first failure in plan order as without preloading.
5. Preloading observes the load's cancellation token between bundles. A bundle
   whose load has started completes, as it does today in the serial pass.
6. Preloading must stay within the startup memory budget (board decision 11:
   peak private bytes and peak working set not above the `v1.1.11` package
   shape; GC heap after warm-up at most 50 MB).
7. Optional stages keep ADR 0049's limit of two concurrent workers and their
   ordering edges. Nested fan-out stays forbidden everywhere else.

## Consequences

- Wall time of the catalog stage falls on multi-core machines; one core stays
  free for the UI thread. On one- or two-core machines the bound degenerates
  to serial loading.
- Bundle loading code must be safe to run concurrently for different bundles.
  The shared state it touches today: the lazy catalogs themselves, the
  process-wide validated-schema cache (`ConcurrentDictionary`), shared
  `JsonSchema` instances (already shared process-wide for embedded schemas),
  and static registries initialized by the runtime's type initializers.
- Metadata-provider bundles form a dependency graph; a cycle would already fail
  in the serial pass. Without a cycle, concurrent lazy initialization cannot
  deadlock.
- Peak allocation concentrates in time; the memory budget is measured with each
  change.

## Rejected options

- Parallelize route classification, compilation or publication: rejected
  because publication order, progress and first-error selection would then
  depend on timing.
- Start catalog loading before the first window: rejected; it conflicts with
  ADR 0049's first-window rule and adds allocation to the window path.
- Unbounded `Parallel.ForEach` or one task per bundle: rejected; ADR 0049's
  bounded-resource rule stays.

## Verification

- Existing catalog tests pass unchanged, including exact progress sequences,
  cancellation, failure retention and `CatalogLoadScopedCtrlRamTests`.
- New tests: a bundle whose load fails still yields the same first failure and
  message as without preloading; repeated loads give identical snapshots;
  cancellation before and during preloading.
- Golden regression and formal-route closure tests unchanged.
- Package-equivalent startup measurement before and after, including the
  memory budget figures.
