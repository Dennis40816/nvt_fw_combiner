# ADR 0077: Pre-built profile catalog from build-time bundle admission

- Status: Accepted (owner decision 46, 2026-09-26), after the independent
  design review by `codex/gpt-6-astra` (ACCEPT-WITH-CHANGES at `377783ec7`,
  rebuilt as `ba6d0128f` with the same tree), after the owner's decisions of
  the same day (see "Owner decisions") and after the re-review
  (ACCEPT-WITH-CHANGES at `108bcfb0e`, no P1; its two P2 findings, F-6 and
  F-7, are answered here). Implementation starts with the Step 0 attribution
  measurement and continues only past its gate; the R3 trust policy of
  decision 5 still needs the owner's exact-head attestation at the
  implementation head.
- Date: 2026-09-26
- Owners: Product owner, architecture owner, release owner
- Decision source: owner decision 38 (2026-09-26) on the
  [1.1.12 board](../handoff/1.1.12.md), scheduled as a wave 3 item on the
  [1.1.13 board](../handoff/1.1.13.md)
- Risk: R3 trust policy for built-in bundle admission (schema admission moves
  to the build; runtime acceptance and the handling of damaged sources change;
  the bundle contract changes) and R3 release package and smoke policy. R2 for
  the codec, the build tool, the build admission identity, the headless probe,
  the startup trace and the measurement runner. No firmware-semantic authority
  changes.
- Builds on: [ADR 0046](0046-capability-and-compilation-fingerprint-boundary.md),
  [ADR 0049](0049-unified-preload-lifecycle.md),
  [ADR 0068](0068-derived-file-synchronization.md),
  [ADR 0075](0075-bounded-catalog-bundle-preload.md)
- Amends: ADR 0049 and ADR 0075 as listed in decision 8; each received the
  reciprocal `Amended by` link on acceptance (2026-09-26, decision 46)
- Supersedes: None

## Context

Owner decision 38: built-in profiles stay reviewed JSON under the hash-pinned
trust index; the build derives a pre-built catalog with the same catalog code
and ships it; a profile update regenerates it. A local database is not adopted
for built-in profiles (not reviewable as diffs, not byte-stable for hash pins,
no query need). Online updates stay possible through the trust index.
Acceptance is by measured startup time and by equivalence of the catalog loaded
from the pre-built catalog and from JSON.

Measured facts (package shape, `v1.1.11` measured in the same quiet session,
one warm-up and five scored launches;
[WS-PERF](../handoff/1.1.12/WS-PERF.md), [CHANGELOG](../../CHANGELOG.md)):

- 1.1.12 final source `dc773e967`: first window 724-733 ms; all startup loading
  done 2,163-2,215 ms (median 2,196) after launch. The owner's targets are
  500 ms and 2,000 ms; neither is met.
- The required catalog stage took 1,487 ms serially after the 1.1.12 units 1-3
  and 1,184 ms with the ADR 0075 preload on four workers, which raised peak
  private bytes by about 7 MB.
- Peak private bytes 331.9-334.8 MB, peak working set 331.3-335.6 MB, GC heap
  after warm-up at most 34.9 MB. `NvtFwCombiner.exe` is 76,350,820 bytes
  against the 80,000,000-byte ceiling ([tests README](../../tests/README.md),
  [ADR 0060](0060-temporary-release-package-size-ceiling.md)).
- The sampled trace in the 1.1.12 unit 1 evidence put 81% of catalog loading
  in `ProfileBundleSchemaValidator.ValidateEntries` and 46% in schema parsing.
  Unit 1 then removed duplicate schema builds (35 builds for 10 distinct
  contents); every launch still meta-validates and builds those ten schemas
  and evaluates every document. No attribution has been measured since.

On every launch the catalog stage repeats the JSON admission of 19 of the 24
trusted bundles (the five General Merge logical candidates open on demand): it
opens 118 files (2.3 MB, including the schema copies), hashes them, verifies
the closed inventory, validates the manifest, meta-validates and builds the
entry schemas, evaluates each family and profile document, rereads the
manifest and checks DTO compatibility. It then normalizes families and profiles
(`TrustedProfileBundleCatalogFactory`), resolves cross-bundle metadata
(`BuiltInCanonicalMetadataDefinitionResolver`), compiles routes and publishes.
For a given trust index and application build, the admission results are the
same on every launch.

Constraints:

- [SPEC](../../SPEC.md) §15.5 item 22 allows at most three firmware-semantic
  forms (Contracts DTO, Domain definition, resolved or compiled reference) and
  forbids persisting a second semantic model. Item 24 requires trust admission
  to verify the pinned SHA-256 of the exact raw document bytes before
  deserialization.
- The SPEC section "Capability authority, admission, and portable onboarding":
  data-only onboarding through the hash-pinned trust index, no runtime
  self-promotion, one `Reload Catalog` command, last-known-good retention, and
  a cold start without a valid catalog blocks Build.
- The [trust-index contract](../contracts/profile-bundle-package-trust-index-v1.md)
  makes the index the sole admission list; it pins each bundle's canonical
  entry-array hash, not the manifest bytes; a catalog reload does not re-read
  bundle bytes. The [bundle contract](../contracts/profile-bundle-v1.md) puts
  shape validation in the loader: normalizers consume only schema-validated
  shape (architecture test
  `CompositionProfileShapeValidationStaysAtTheTrustedSchemaGateway`).
- ADR 0049 bounds every file input; ADR 0060 grants no new package file.

Terms: the *pre-built catalog* is the file this ADR defines (the owner's
"pre-built catalog snapshot"). It is unrelated to
`CanonicalCapabilityCatalogSnapshot`, the published capability catalog, and to
the update Catalog of version management.

## Decision drivers

- Stop repeating build-invariant work on every launch without changing any
  published result.
- One implementation of every semantic step and no new firmware-semantic form.
- Reviewed JSON stays the only source of truth; derived bytes are
  deterministic and reproducible.
- No new trust anchor; data failures stay fail-closed; inputs stay bounded.
- The first window, the EXE ceiling and the memory gates are not made worse.
- Online updates through the trust index stay possible.

## Considered options

1. Keep JSON loading and add parallelism (extend ADR 0075).
2. Load bundles and routes lazily on demand.
3. A local database for built-in profiles.
4. A pre-built catalog of normalized or compiled objects.
5. A pre-built catalog of admitted documents. **Selected.**
6. A build-verified schema skip without a shipped artifact.
7. The admitted-document catalog embedded in the executable.

## Decision

### 1. The pre-built catalog carries admitted documents

The pre-built catalog is one package file that carries, for every bundle in
the package trust index, the exact raw bytes of `profile-bundle.json` and of
every `firmware-family` and `composition-profile` entry, with the identities
the JSON admission path verified. It carries no schema bytes, parsed trees,
normalized definitions, compiled compositions, fingerprints or routes.

- The carried bytes are the reviewed Contracts-owned documents themselves, so
  no firmware-semantic form is added (SPEC §15.5 item 22) and exact-byte
  admission before deserialization is kept (item 24). Serializing normalized
  Domain definitions or compiled compositions would persist a second semantic
  model and need a second construction path for every Domain type, including
  cross-bundle object identity: resolved metadata definitions are shared by
  reference between bundles.
- Admission is the part that is invariant for a given trust index and build,
  and it was the dominant measured cost. Normalization, metadata resolution,
  compilation and publication keep running in their existing owners.
- A snapshot of the published catalog would not remove bundle loading: Build,
  inspection, AB and CtrlRAM authoring, dynamic routes and Saved Rule admission
  compile on demand from the bundle catalogs, so the cost would only move to
  the first operation.

If the step 0 attribution (Measurement plan) shows that normalization or
compilation dominates the remaining catalog stage, this ADR does not authorize
serializing them; that needs a new decision.

### 2. Format and determinism

- Package path `profiles/built-in/prebuilt-profile-catalog.pack`, beside
  `package-trust-index.json` and outside the single-file bundle. The extension
  is not `.bin`, which package tooling treats as firmware.
- Bytes `[0, 8)` are the ASCII magic `NFCPBCAT`; `[8, 12)` the header length
  `N` as an unsigned 32-bit little-endian integer, `1 <= N <= 1,048,576`;
  `[12, 12 + N)` the header; `[12 + N, end)` the body.
- The header is UTF-8 JSON without BOM in RFC 8785 canonical form (strings and
  non-negative integers only), closed at every level:
  - `formatVersion`: `1`;
  - `trustIndex`: `sha256` of the exact trust-index bytes, `trustIndexId`,
    `trustIndexVersion`, `trustAnchorBindingId`;
  - `bundles`: one per trust-index bundle, ordinal by `bundleDirectory`, each
    with `bundleDirectory`, `bundleVersion`, `contentHash`, `manifest`
    (`offset`, `length`, `sha256`) and `documents`, the manifest's family and
    profile entries ordinal by `entryId`, each with `entryId`, `kind`, `path`,
    `schemaId`, `contentHash`, `offset` and `length`;
  - `body`: `length`, `sha256`.
- The body is the carried byte ranges concatenated in header order, with no
  gap, padding or trailing byte; offsets are body-relative.
- The file is a pure function of the trust-index bytes, the materialized
  bundle bytes and the format version. It contains no time, no machine-specific
  or absolute path (the header's `path` values are the manifests' bundle-relative
  entry paths), and no machine, user, culture, build, commit or product-version
  data; generation is single-threaded and ordinal. Its SHA-256 therefore
  changes only when the trust index, a bundle or the format changes.
- The format is language-neutral: a byte container with a canonical JSON
  header, no .NET serializer and no reflection.
- The file is at most 4,194,304 bytes (4 MiB, about four times the current
  size); the generator fails the build above it and the reader rejects before
  allocation. Any layout or header change increments `formatVersion`.

### 3. Generation runs the runtime admission code

- A build-only tool project under `eng/` runs after profile materialization in
  every build that materializes the built-in profiles (today the Bootstrap
  build, inherited by the Desktop, CLI and test hosts). It runs as its own
  process, so no product assembly loads into MSBuild. It is not shipped and no
  shipped assembly references it.
- It runs the runtime code, not a restatement of it:
  `ProfileBundlePackageTrustIndexLoader.Load` on the materialized index, then,
  for every trust-index bundle, `ProfileBundleLoader.Load` with the directory
  source, the trust anchor and the load limits of `BuiltInV2Bundle` (one shared
  definition), then `CreateDocumentProjection`. It also computes the build
  admission identity (decision 4) and requires it to equal the identity the
  build recorded. Any failure fails the build and writes no file.
- It encodes the result with the Infrastructure-owned codec whose decoder the
  runtime uses, so the format has one owner. It performs no normalization,
  compilation or publication.
- Its incremental inputs are the trust index, the materialized profiles and
  the generator's complete assembly closure (the admission code, the Contracts
  DTOs and the JSON Schema library); the load limits and admission settings are
  constants inside that closure. Any change of data or admission code re-runs
  admission, so a pre-built catalog never outlives the code that admitted it.
- The file is a build output like the materialized bundles and is not
  committed. The existing materializer keeps its independent build-time
  validation ([audit A8](../architecture/2026-08-22-single-implementation-and-layering-audit.md));
  the generator adds admission by the runtime code and replaces nothing.

### 4. Runtime acceptance and trust binding

- Build admission identity. Before `NvtFwCombiner.Infrastructure` compiles,
  the build records two values from the reviewed source files as its assembly
  metadata:
  1. the SHA-256 of the exact `profiles/built-in/package-trust-index.json`
     bytes;
  2. the manifest-set digest: the SHA-256 of the RFC 8785 canonical array of
     `{bundleDirectory, manifestSha256}` over every trust-index bundle, ordinal
     by `bundleDirectory`, where `manifestSha256` is the SHA-256 of the exact
     reviewed `profile-bundle.json` bytes (materialization copies them
     verbatim).

  The trust index does not pin manifest bytes, so value 2 is what binds
  manifest-only fields such as `bundleId` to the build; a header field or a
  package-local checksum could not. Edits to any input recompute both values.
  How the build enumerates the trust-index bundles is a B2a implementation
  choice, which the generator re-proves with the runtime code.
- Acceptance. Before it serves any built-in bundle catalog,
  `BuiltInV2BundleRegistry` decides once whether to accept the pre-built
  catalog. It accepts only when every check passes:
  1. the file is a regular file under the application root (the path and
     handle guards used for bundle files), is at most 4 MiB and is read as one
     bounded, length-stable snapshot;
  2. magic, header bound, strict header parse (duplicate keys rejected, depth
     16, closed shape) and `formatVersion` 1, with every offset and length
     checked by overflow-safe arithmetic;
  3. body length and SHA-256, with no trailing byte;
  4. `trustIndex.sha256` equals the SHA-256 of the exact bytes the trust-index
     loader parsed and identity value 1, and the other trust-index fields
     match;
  5. the bundle set equals the trust index's (directory, version, content
     hash), and the ranges lie in the body in order, without overlap or gap;
  6. each carried manifest hashes to its `manifest.sha256`, and the
     manifest-set digest recomputed from the carried manifests equals identity
     value 2;
  7. per bundle: the manifest passes the JSON path's manifest steps unchanged
     (strict parse, embedded manifest schema, normalization including the
     canonical entry-array hash, trust anchor and version); the carried
     documents are exactly the manifest's family and profile entries; each
     hashes to its manifest `contentHash`; each `schemaId` names a schema entry
     of that manifest; the entry-count and per-entry size limits hold (the
     aggregate limit, which also counts schema bytes, was enforced by the
     build).
- Accepted result only. Acceptance yields one immutable accepted-catalog
  object that only the acceptance code can construct. `ProfileBundleLoader`
  stays the only constructor of `TrustedProfileBundle`; its pre-built mode takes
  that object, never a general "skip validation" switch, builds from an
  in-memory snapshot source and omits only entry schema meta-validation and
  evaluation. Strict parsing, DTO compatibility, normalization, metadata
  resolution and every later step run unchanged, lazily per bundle as today.
- Soundness. The omitted verdict is a pure function of the document bytes, the
  schema bytes and the admission code. Every carried byte is bound to the
  build: the manifests by identity value 2; the documents by their manifest
  content hashes and the canonical entry-array hash, which also fixes the
  schema hashes; that hash by the trust index, whose bytes identity value 1
  binds. The build that recorded the identity ran the admission code on exactly
  these bytes and succeeded, because generation belongs to the same
  materialization build and fails it on any error; packaging proves it again by
  regeneration. Normalizers still receive only schema-validated shape. For any
  other trust index, including a future online update, the pre-built catalog
  is not used.
- No new trust anchor. The pre-built catalog cannot add, remove or replace a
  bundle, registration or document; the trust index remains the sole admission
  list, and its own runtime acceptance does not change. The file's SHA-256 is
  pinned where every package file is pinned, in `RELEASE-MANIFEST.json` and
  `SHA256SUMS.txt`, and packaging regenerates the file and requires byte
  equality (decision 9). At runtime the body digest only detects corruption;
  eligibility comes from the build admission identity. A checked-in pin is not
  needed because the build derives the identity from reviewed files; if one
  were preferred, the existing reviewed-source provider of `sync_derived.py`
  would be extended (ADR 0068), not a new writer added.

### 5. Failure handling, damaged sources and last-known-good

- The selection is fixed for the process. The acceptance result, accepted or
  rejected with a reason code, is computed once; cancellation, retry,
  `Reload Catalog` and concurrent first access never re-select a source or mix
  sources.
- A rejection sends every bundle through the JSON admission path, which keeps
  its complete validation and fail-closed behavior. It is recorded once as a
  typed fact and shown as a Warning `ActionableSystemDiagnostic` in the
  existing `CapabilityCatalog` category (a new code such as
  `system.catalog.prebuilt-unused`) in UI and CLI System Information. It is not
  a catalog issue and changes no publication, progress or issue code.
- A rejection is not a catalog integrity failure: the pre-built catalog is a
  derived delivery form, the SPEC integrity rule governs the source data, and
  the JSON path still verifies that data in full. Blocking Build for a damaged
  derived file would turn a performance defect into an outage. So that a
  fallback cannot hide a packaging defect, CI requires the build output to take
  the pre-built path, packaging and smoke verify the file and its runtime use,
  and the measurement runner requires the admission-source marker (decision 8).
- Damaged sources after acceptance: the verified pre-built catalog decides
  (owner decision of 2026-09-26, Option A; R3 trust policy). After acceptance
  the runtime reads no bundle JSON or schema file, so their current state no
  longer decides admission. Packaging, smoke and release gates verify them
  against the pre-built catalog. An installation whose JSON or schema copies
  change later keeps working with the build-admitted catalog: damage that the
  JSON admission rejects no longer stops it, and a manifest edit that the JSON
  admission still accepts no longer changes its bundle identity. The owner
  approved this policy; its implementation (B2b) needs the owner's exact-head
  attestation. The rejected alternative, keeping the packaged JSON mandatory
  after acceptance, is listed under "Rejected options".
- Disposition matrix. Two kinds of change to the packaged bundle files are
  distinguished. *Rejected damage* is any change the existing JSON admission
  rejects: a missing or changed listed file, an unlisted extra file, a
  manifest whose trust anchor, version or canonical entry-array hash no longer
  matches, or an invalid manifest. An *admissible manifest edit* is a manifest
  change the existing JSON admission still accepts, such as a changed
  schema-valid `bundleId`: `ProfileBundleLoader` checks only the trust-anchor
  binding and the content hash, and the canonical entry-array hash excludes
  `bundleId`. "The build's" trust index is the one identity value 1 names.

| Pre-built catalog | Trust index | Bundle JSON and schema copies | Outcome | Compared with the JSON path today |
| --- | --- | --- | --- | --- |
| Accepted | The build's | Intact | Pre-built path | Same published state |
| Accepted | The build's | Rejected damage | Pre-built path; the JSON state is not read | Changed: today the affected bundle fails when it is opened |
| Accepted | The build's | Admissible manifest edit | Pre-built path with the build's manifest identity; the JSON state is not read | Changed: today the edited manifest and its identity are admitted |
| Absent or rejected | The build's | Intact | JSON path, with the Warning | Same published state |
| Absent or rejected | The build's | Rejected damage | JSON path fails, with the Warning | Unchanged |
| Absent or rejected | The build's | Admissible manifest edit | The existing JSON admission decides, with the Warning; the edited identity is admitted as today, not the build's | Unchanged |
| Any | Not the build's | Any | Not eligible; the JSON path decides, with the Warning | Unchanged |
| Any | Missing or invalid | Any | Trust-index load fails: no publication, Build blocked | Unchanged |

The fallback rows keep today's JSON admission exactly: it neither guarantees a
failure nor restores the build's identity, and this ADR does not tighten it.

- After acceptance, a failure while building one bundle catalog (strict parse,
  DTO compatibility, normalization) is that bundle's load failure with the
  JSON path's codes and messages; it does not fall back.
- Last-known-good and `Reload Catalog` do not change. Built-in bundle catalogs
  live for the process; a reload re-runs publication and never re-reads the
  trust index, the bundles or the pre-built catalog. A cold start without a
  valid publication keeps today's behavior: Build blocked,
  `system.catalog.unavailable`.
- The acceptance is bounded, runs once and has no internal cancellation point,
  like one bundle load today.

### 6. Equivalence is proven by permanent contract tests

Equivalence is claimed only for the same complete, matching package inputs:
the pre-built catalog, trust index, bundle JSON and schema copies of one build.
Decision 5 defines every other state. Both admission sources are permanent (the
JSON path is the reference, the generator and the fallback), so these are
durable contract tests, not temporary old/new differentials. The bundle
registry and bundle catalogs are process-static, so every cross-source
comparison runs each source in its own process over its own copy of the build
output (the JSON-source copy lacks the pre-built file); each process reports
its admission source, and the test asserts it.

1. Admitted documents: for all 24 bundles both sources give the same manifest
   SHA-256, bundle identity, document identities and exact document bytes.
2. Normalized identities: for all 24 bundles the same bundle, family and
   profile identities (ids, versions, content hashes, family members), and the
   same Saved Rule parent for every registered General Merge and General
   Replace profile.
3. Published state: the pinned `CanonicalCatalogSnapshotDigest` (whole text and
   its seven sections) is the same on both sources. This proves the published
   state the digest covers (route identities, capability fingerprints,
   static-route compilations and their fingerprints, dynamic-route compilation
   contracts, full-image plans, disclosure, selector and certification), not
   on-demand compilation.
4. On-demand compilation: representative success and failure contracts run on
   both sources for Standard Merge map selection, AB Merge topology and
   selection, CtrlRAM Replace processor branch and report metadata, General
   Merge logical output and General Replace runtime reference replace. Successes
   have equal `CompilationFingerprint` and plan identity; failures have equal
   issue codes and messages.
5. Reproduction: the CI test run regenerates the pre-built catalog from the
   JSON in the test output through the generator's library entry and requires
   byte equality with the build output, also under another culture and time
   zone and on a second generation. Packaging repeats this on the published
   files (decision 9).
6. Codec: independent RFC 8785 vectors for the header, not produced by the
   codec itself; offset and length arithmetic at its bounds; truncated files,
   trailing bytes and overlapping, gapped or out-of-range ranges, each
   rejected.
7. Rejection: each acceptance check falls back with one diagnostic while the
   published digest stays pinned. A manifest whose schema-valid `bundleId`
   alone changes, with recomputed manifest and body digests, is rejected by
   identity value 2. With the JSON path also broken, today's failure results.
   The disposition matrix of decision 5 is tested row by row, including both
   kinds of change on disk: rejected damage (a missing listed file, changed
   document bytes, a broken entry array) and an admissible manifest edit (a
   changed schema-valid `bundleId`). With an accepted pre-built catalog the
   edit leaves the build's identity in force; with the pre-built catalog
   removed, the existing JSON admission admits the edited identity exactly as
   today.
8. Golden regression passes unchanged on the pre-built path, the default in
   every test host.
9. Architecture: `ProfileBundleLoader` remains the only `TrustedProfileBundle`
   constructor and its pre-built mode accepts only the accepted-catalog object;
   the schema-gateway test is restated as "entry schema evaluation precedes
   every construction except one from an accepted pre-built catalog bound to
   the build admission identity"; the codec references no JSON Schema,
   normalization, compilation or Application type; only the generator and tests
   reference the encoder; the pre-built path names no bundle, IC, family or
   workflow.
10. ADR 0075: the acceptance completes on the warm-up thread before any worker
    starts; the existing preload, progress, cancellation and failure tests pass
    unchanged.

### 7. Profile updates and future online updates

A profile update: (1) reviewed JSON, manifest hashes and the trust index change
under the profile's existing R3 gates; (2) every build recomputes the build
admission identity and regenerates the pre-built catalog, with no manual step
and nothing new to commit; (3) CI runs the tests of decision 6, and the pinned
digest changes only when the published catalog changes, reviewed as today;
(4) packaging regenerates and compares, the release manifest records the new
file hash, and smoke verifies the correspondence and the runtime use.
Onboarding an IC in the existing vocabulary gains no step
([onboarding guide](../architecture/adding-ic-merge-replace-workflow.md)).

Online updates are outside this ADR. Today profile changes reach users only in
a new application version ([ADR 0051](0051-managed-version-installation-and-activation.md)),
whose release build regenerates the pre-built catalog. Two directions exist for
a later data-only update through the trust index:

- (a) The update carries the JSON and a pre-built catalog made by the release
  pipeline. Not preferred: the catalog's validity depends on the admitting
  build's code, so the update channel would have to authenticate a derived
  artifact for each application build.
- (b) The running application admits the new JSON once through the complete
  JSON path, off the startup path, and keeps a local pre-built catalog bound to
  the new trust index and to its own build. Preferred as a direction: reviewed
  JSON stays the only delivered authority, and the same codec and admission
  code are reused. The codec therefore lives in Infrastructure, not in the
  build tool.

Both are recorded only as future options. This ADR authorizes no local write,
cache, hot reload or `Reload Catalog` change. The update's own ADR decides the
authentication of the new index, storage and atomic replacement, the build
binding of a locally written catalog (the build admission identity does not
cover a newer index) and whether a reload may observe it; a local catalog
would be a cache of an admitted index, never an admission or a promotion.

### 8. Startup lifecycle: amendments to ADR 0049 and ADR 0075

ADR 0049, "Preserve one production path and cache identity": built-in bundle
admission may be served by the accepted pre-built catalog inside the existing
Infrastructure owner; a rejection causes canonical recomputation through the
JSON admission path in the same catalog attempt and adds only the diagnostic of
decision 5. "Verification and release impact": the startup trace records one
closed marker naming the admission source of the built-in bundles (`prebuilt`
or `json`), and the measurement runner can require it. The bounded-input rule
applies unchanged (4 MiB).

ADR 0075: decision 2, the first layer-0 bundle, which loads alone, also
completes the pre-built catalog acceptance, so it finishes before any worker
starts; decision 4, the accepted pre-built catalog is immutable shared state,
each worker reads only its own bundle's ranges, and on the pre-built path
built-in entries reach no JSON Schema evaluation (the lock stays for the JSON
path); decision 7, equivalence holds for both sources as scoped in decision 6
of this ADR, and the transient-input caveat narrows to the one file, whose
failure selects the JSON path for the process; decision 8, the gates of this
ADR replace it for this change.

The preload is kept initially. If the preload probe (Measurement plan) shows
no per-launch gain beyond the same-session spread on the pre-built path, the
owner is asked to retire ADR 0075 in a separate R2 batch. That restores ADR
0049's unamended concurrency rule, returns the preload's private-bytes
allowance and removes the bundle-named layer table that every new bundle must
otherwise join to keep the preload.

### 9. Size, packaging and smoke

- Size (local build output of this source, 2026-09-26): 24 manifests of
  49,898 bytes and 79 family and profile documents of 978,398 bytes, about
  1.06 MB with the header; 84,928 bytes after `gzip -9`, so about 0.1 MB in the
  ZIP. The 1.85 MB of schema copies are not carried.
- `NvtFwCombiner.exe` changes only by the codec and probe code. The file sits
  beside the executable, not in the single-file bundle, so it adds nothing to
  pre-window decompression and does not count toward the 80,000,000-byte
  ceiling. The ZIP grows by about 0.1 MB against its 134,217,728-byte ceiling.
  The installed footprint grows by about 1.06 MB; the JSON files stay shipped
  as the reference path and the fallback.
- [`scripts/package.ps1`](../../scripts/package.ps1) needs a bounded R3 change,
  because the package is a closed allowlist and ADR 0060 grants no new file:
  admit exactly the one path `profiles/built-in/prebuilt-profile-catalog.pack`,
  never by extension or directory; regenerate it from the published profiles
  and require byte equality; require its header to bind the reviewed trust
  index and the published bundle set; list it in the release manifest with the
  existing `builtInProfile` role (its path is under `profiles/built-in/`, so the
  manifest schema does not change); update the package README text; add
  dry-run probes for a missing, extra, altered or stale file, including an
  altered file whose body digest and outer checksums (release manifest,
  `SHA256SUMS.txt`) were recomputed, which must still be rejected.
- [`scripts/smoke-release.ps1`](../../scripts/smoke-release.ps1) needs a bounded
  R3 change. It requires exactly one such file with that role, at most 4 MiB,
  and the complete correspondence between the pre-built catalog and the
  packaged JSON: a canonical, closed header; a bundle set equal to the packaged
  trust index; every carried manifest and document byte-identical to its
  packaged file and equal to its manifest hash; contiguous ranges; the body
  digest; and a trust-index SHA-256 equal to the approved pin. It then runs the
  packaged `NvtFwCombiner.exe` through the headless probe and requires
  `admission-source=prebuilt`. A pre-built catalog altered after packaging,
  with its body digest and outer checksums recomputed, must fail smoke.
- Headless probe. The packaged executable has no headless entry that loads the
  catalog today: its only headless entry is the toolchain runtime-trust probe,
  the CLI with `doctor` is not packaged, and release smoke runs with
  `-SkipUiLaunch`. B2a therefore adds one closed, read-only probe argument to
  the shipped executable, handled before any UI like the runtime-trust probe. It
  composes the host, runs the normal catalog load and prints one path-free JSON
  line with the admission source and any rejection code; it writes no file,
  opens no window and uses no network. It is a test surface, not a user
  feature.
- [`docs/ci/release-package.md`](../ci/release-package.md) and the two profile
  contracts are updated. No workflow change is expected, because the release
  workflow already runs `package.ps1` and `smoke-release.ps1`; if one is
  needed, it is R3 and joins that record.
- The local package and smoke of B3 do not replace protected CI, fresh Golden
  execution on the candidate, clean-Windows validation or the release-owner
  gates.

### 10. User-authored data stays outside

- Only trust-index bundles are carried. User files, such as General Merge and
  General Replace Saved Rule v2 documents, toolchain and event-buffer
  configuration, reports and history, never enter the pre-built catalog and
  keep their runtime validation by their existing owners. Saved Rule admission
  (`SavedCompositionRuleV2Admission`) validates against the embedded saved-rule
  schema and binds the exact trusted parent identity; decision 6 item 2 proves
  both sources give the same General Merge and General Replace parents, so
  Saved Rule acceptance, staleness and messages do not change.
- General Merge is hidden and will be reimplemented (owner decision 37;
  [roadmap](../architecture/nfc_roadmap.md) `1.3.0` authoring, `1.3.2` saved
  and custom rules). Its five logical-candidate bundles are trusted, so they
  are carried, but open on demand only. When their profiles change, the
  pre-built catalog regenerates, and rules bound to earlier parent hashes
  become stale exactly as today, which decision 37 accepted.
- A future maintainer-authored IC candidate (roadmap `1.3.3`) stays untrusted
  until the trust index promotes it; the runtime never writes a pre-built
  catalog for a candidate.
- The capability policy and the `ctrlram-postbuild-v2` runtime catalogs keep
  their pinned JSON loaders; carrying them needs step 0 evidence and a new
  decision.

### 11. Risk, gates and implementation batches

Records and reviews follow the governance in force at each admission (today,
capability-reuse records bound to the latest evidence checkpoint; the WS-GOV
reset may replace the record mechanism, not the substantive reviews and
evidence). Moving schema admission to the build and no longer letting the
packaged JSON state decide admission is an R3 trust-policy change, approved by
the owner on 2026-09-26 and attested at the exact implementation head; the
codec and the other general architecture are R2; the package and smoke policy
is R3.

| Batch | Scope | Risk | Gate |
| --- | --- | --- | --- |
| B0 | This ADR | R3 trust-policy decision with R2 architecture | Independent design review (ACCEPT-WITH-CHANGES at `377783ec7`); owner decisions of 2026-09-26 recorded; re-review (ACCEPT-WITH-CHANGES at `108bcfb0e`, no P1, P2 answered); owner approval to Accepted |
| B1 | Step 0 go/no-go; no repository change | - | At least 100 ms of avoidable wall time, clearly above noise on the projected intervals that include the acceptance cost; otherwise reported to the owner, who decides deferral or redesign; B2 never starts automatically |
| B2a | Codec, generator and build wiring; build admission identity; diagnostic; headless probe; trace marker and measurement-runner check; validator project list; tests of decision 6 | R2 | Architecture admission before implementation; independent fixed-head review; scoped Polytail; narrow tests (Infrastructure, Bootstrap, Architecture, Golden regression, UiSmoke, script tests); `verify.py --all` on the frozen candidate |
| B2b | Loader pre-built mode; registry acceptance and fallback; the damaged-source policy of decision 5; bundle and trust-index contract prose | R3 trust policy | Owner's exact-head trust-policy attestation; independent review; the disposition matrix tested row by row |
| B3 | `package.ps1`, `smoke-release.ps1`, their script tests, `docs/ci/release-package.md` | R3 release | Release-owner review and release-policy evidence (dry-run and negative probes, local package and smoke of the frozen candidate); decision 9 lists the gates it does not replace |
| B4 | Acceptance measurement of the frozen candidate | - | Measurement plan gates; owner acceptance |
| B5 | Optional preload retirement | R2 | Preload probe result; owner decision |

B2a, B2b and B3 are completed on one frozen candidate and integrated together.
No firmware-owner gate applies: no profile, schema, range, byte, CRC, Header or
naming changes. Golden regression and release Golden execution stay required.

## Measurement plan

Step 0, the go/no-go gate for B2 (no product change is committed):

- Scope: the 19-bundle startup critical path, measured as catalog-stage wall
  time under the actual ADR 0075 schedule. The five General Merge candidates
  are reported separately as on-demand cost. Sampled thread time is supporting
  attribution only; with four workers it does not convert to wall time.
- Builds: (a) a package-equivalent publish of the `1.1.x` head of the day, with
  the exact application flags of `scripts/package.ps1` (self-contained win-x64,
  compressed single file, composite ReadyToRun, untrimmed); (b) the same source
  with a local, uncommitted measurement patch that omits entry schema
  meta-validation and evaluation and serves the bundle files from one pre-read
  buffer. Build (b) is an upper bound of the avoidable work and is never
  shipped.
- Method: one quiet session, two passes in reverse build order, one warm-up and
  five scored launches per build and pass; one `dotnet-trace` sampled trace per
  build for attribution; a probe timing the one-time acceptance work (one
  bounded 1.06 MB read and hash, 24 manifest validations and the manifest-set
  digest) in at least ten fresh processes, reported as median and range.
- Projection: the projected candidate catalog interval is a (b) interval plus
  the acceptance cost. Its projected range runs from the smallest (b) interval
  plus the smallest acceptance time to the largest (b) interval plus the
  largest acceptance time, so the acceptance cost enters with its measured
  uncertainty. The avoidable wall time is the (a) median minus the sum of the
  (b) median and the acceptance median.
- Report attached to the B2 admission: the (a) and (b) intervals and the
  acceptance cost, each as median and range; the projected candidate range;
  the avoidable wall time; the same-session noise; and the projected
  all-loading-done time per launch.
- Gate (owner decision of 2026-09-26): B2 is admitted only when the avoidable
  wall time on the 19-bundle startup critical path is at least 100 ms and
  clearly above noise, meaning that in both passes the whole projected
  candidate range lies below the smallest (a) interval. When either condition
  fails, or the measurements cannot show the net gain separated from noise,
  the result goes to the owner, who decides deferral or redesign; B2 is never
  entered automatically.

Acceptance (B4) on the frozen candidate:

- Variants in one quiet session on the owner machine: A, the published
  `v1.1.12` package; B, the candidate package; C, the B package with the
  pre-built catalog removed (the JSON path of the same binary).
- [`scripts/measure-startup.ps1`](../../scripts/measure-startup.ps1)
  `-Page home -WarmupRuns 1 -Runs 5 -TimeoutSeconds 30 -RequirePreloadLifecycle`
  with the admission-source requirement (B `prebuilt`, C `json`); two passes,
  the second in reverse variant order; then three `dotnet-counters` runs per
  variant for the GC heap after warm-up.
- Gates, per scored launch unless a median is stated; gates 3 to 5 are the
  owner's decisions of 2026-09-26:
  1. Equivalence: the tests of decision 6 pass on the frozen candidate.
  2. Cause: in each pass every B catalog interval (`main-window.opened` to
     `startup-warmup.catalog-state.applied`) is below every C interval.
  3. Target: every B launch finishes startup loading (launch to
     `startup-warmup.completed`) within 2,000 ms. Any launch above 2,000 ms
     goes to the owner with the measured gain and its blocker, and the owner
     decides case by case; the target stays.
  4. First window: B's median is at most A's median plus 20 ms. This is a
     regression guard only; it does not claim the 500 ms target.
  5. Memory: every B launch's peak private bytes and peak working set are at
     most the same-session maximum of A (`v1.1.12`, which already includes the
     ADR 0075 allowance) plus 1 MB. GC heap after warm-up at most 50 MB (board
     decision 11).
  6. Size: EXE at most 80,000,000 bytes, ZIP at most 134,217,728 bytes,
     pre-built catalog at most 4 MiB.
- Evidence in the test area under `evidence/v1113-pbc-*`, summarized in the
  handoff and the release notes.

Preload probe (input to B5): B against the same source with the preload
disabled by a temporary local patch, by the same method.

## Rejected options

- Local database: rejected by owner decision 38; it would also add a second
  persisted model and schema, a native dependency in the single-file package,
  and migrations.
- More parallelism only: four workers cut the catalog stage from 1,487 to
  1,184 ms and added about 7 MB of peak private bytes; schema meta-validation
  and build stay serialized (ADR 0075 decision 4); one- or two-core machines
  load serially; total CPU and allocation stay; 1.1.12 missed 2,000 ms on every
  launch.
- Lazy loading: it moves the wait to the first workflow selection, which the
  1.1.12 startup envelope forbids, and publication must validate a complete
  candidate catalog (SPEC `Reload Catalog` and exact-route uniqueness); partial
  catalogs complicate disclosure and last-known-good.
- Normalized or compiled objects: see decision 1.
- A build-verified schema skip without an artifact: the same trust argument
  without the single-file read. Every launch would still open 118 files, and
  the admitted result would not be an artifact that CI and packaging can
  reproduce byte for byte; the owner chose a shipped pre-built catalog.
- Keeping the packaged JSON mandatory after acceptance (Option B of the design
  review): rejected by the owner on 2026-09-26. The runtime would re-capture
  every bundle directory and require it to match the pre-built catalog, so the
  per-launch file I/O would stay, the pre-built catalog would carry little more
  than a bound verdict, and an edited manifest-only field that today's JSON
  path accepts would fail.
- Embedding in the executable: it would be decompressed before the first
  window on every launch, stay resident, count toward the EXE ceiling that the
  1.1.13 wave 4 item works against, stay hidden from the package inventory, and
  could not be replaced by a data-only update.
- Committing the file to Git: an unreviewed generated payload on every profile
  change; build-time generation with byte-equal reproduction gives the same
  assurance.
- Rejection as a catalog integrity failure: see decision 5.

## Consequences

### Positive

- Launches stop repeating build-invariant schema work and opening 118 files;
  the catalog stage keeps its semantics and results for matching package
  inputs. The size of the gain is measured by step 0 and B4, not assumed.
- One admission implementation serves the build and the runtime; normalization
  and compilation keep one owner; no firmware-semantic form is added.
- An admission failure of a built-in bundle stops the build instead of
  reaching a user's machine, and every carried byte is bound to the build.
- The preload and its bundle-named layer table may become removable.

### Negative / trade-offs

- A build tool, a codec, a build admission identity, a headless probe and a
  runtime acceptance path to maintain; the build runs product code.
- Two admission sources at runtime, held equal by tests, and a fallback that
  can hide a slower start if its diagnostic is ignored.
- While the pre-built catalog is accepted, damage to the packaged JSON or
  schema copies after installation no longer stops the application, and an
  admissible manifest edit no longer changes a bundle identity; this is the
  owner-approved trust policy of decision 5.
- One more package file (about 0.1 MB compressed, 1.06 MB installed) and R3
  changes to packaging and smoke.
- The schema-gateway architecture invariant becomes conditional.

### Risks and mitigations

- A build that skips the generator ships no file -> JSON path with the
  Warning; CI requires the pre-built path; packaging and smoke require the
  file and its runtime use.
- Nondeterministic generation -> reproduction tests and packaging regeneration.
- A stale or edited file beside a newer build -> the build admission identity
  (trust index and manifest set); incremental inputs include the complete
  admission closure.
- Admission code changes without data changes -> the generator re-runs and a
  failure fails the build.
- Encoder and decoder drift -> one codec owner, round-trip tests and
  independent RFC 8785 vectors.
- A hidden packaging defect -> the CI, packaging, smoke and measurement checks
  of decision 5.

## Compatibility and migration

- No profile, schema, trust-index, firmware-byte, range, CRC, Header, naming,
  report or support change; the pinned published-catalog digest stays. The
  shipped executable gains only the hidden headless probe argument.
- The package gains one file under `profiles/built-in/`. Older packages lack it
  and are unaffected; a package without it starts through the JSON path with
  the Warning.
- No saved-data migration. Rollback: the previous package, or removing the
  file (JSON path).
- With B2b and B3: the bundle contract describes the pre-built admission mode
  and the owner's damaged-source policy; the trust-index contract lists the
  file among the package contents with its binding; the release-package
  document describes the file, its correspondence checks and the probe; and
  the onboarding guide notes that no step is added. ADR 0049 and ADR 0075
  already carry the reciprocal `Amended by` links, completed on acceptance
  (2026-09-26, decision 46).

## Verification

Before acceptance: the re-review at `108bcfb0e` closed every P1; this revision
answered its two P2 findings (F-6, the disposition matrix; F-7, the step 0
noise test); the owner then approved the ADR as decision 46 (2026-09-26).
After acceptance: step 0, the tests of decision 6, the owner's exact-head
attestation of the B2b trust policy, the B3 dry-run, negative and smoke
probes, and the B4 gates.

## Owner decisions

Recorded on 2026-09-26:

1. Damaged sources: Option A of decision 5. When a verified pre-built catalog
   is used, the current state of the packaged JSON no longer decides
   admission. The owner approved this R3 trust policy; the implementation head
   still needs the owner's exact-head attestation.
2. Step 0 gate: B2 starts only with at least 100 ms of avoidable wall time on
   the 19-bundle startup critical path, clearly above noise (judged as the
   Measurement plan defines, with the acceptance cost included); otherwise the
   result goes back to the owner, who decides deferral or redesign.
3. Acceptance guards: B's first-window median at most `v1.1.12` plus 20 ms
   (regression guard only); peak memory at most the same-session `v1.1.12`
   maximum plus 1 MB; any startup load above 2,000 ms decided case by case by
   the owner.

Owner approval of the ADR as Accepted was completed on 2026-09-26 as decision
46. Still open: B5 is decided after its probe.

## Resolved by the design review

- Layer: admitted documents only (decision 1), adopted.
- Rejection: fallback to the complete JSON path with one Warning (decision 5),
  adopted; damaged sources follow owner decision 1 above.
- Risk: the admission policy is R3 trust policy; the general architecture is
  R2 (decision 11).
- Binding: the build-derived admission identity, including the manifest-set
  digest (decision 4); no checked-in pin.
- Package role: the existing `builtInProfile` role for one exact path
  (decision 9).

## Added in this revision for re-review

- The manifest-set digest as part of the build admission identity (F-1,
  decision 4); how the build enumerates the bundles is left to B2a.
- A hidden, read-only headless probe in the shipped executable (F-4, decision
  9): the review assumed an existing headless entry, but none loads the
  catalog.
