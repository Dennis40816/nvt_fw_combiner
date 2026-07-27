# ADR 0031: CtrlRAM routing uses IC identity, profile intervals, and build plans

- Status: Accepted for v0.9.12
- Date: 2026-07-21
- Owner decision: 2026-07-21; DiffDLM/NF amendment 2026-07-27
- Risk: R3; implementation and release still require firmware-owner review
- Amends: ADR 0030 production-admission decision 2
- Amended by: ADR 0042 for the `0.10.x` retirement of NT51920, NT51925,
  NT51930, and NT51931

## Context

The migrated CtrlRAM Replace V2 routes were admitted by tuples copied from exact golden cases:
IC, processor, Common FW version, chip count, PID, selector mode, and sometimes a complete input
hash. Removing the hash gate did not correct the underlying authority error. A golden fixture proves
one reproducible byte result; its PID, exact version, filename, and observed chip count do not define
the complete production population for that IC and command plan.

Three separate questions were conflated:

1. Which owner-declared firmware family contains the requested IC?
2. Which owner-provided postbuild revision applies to this Common FW generation?
3. Which postbuild command plan applies to the requested IC-number topology?

The runtime model must answer those questions independently.

## Decision

### IC family identity

1. Family membership is selected only from the normalized requested IC id and explicit
   owner-declared member or fact-scoped alias declarations.
2. PID, Common FW, TP FW, FWConfig chip count, filename, complete-file hash, capacity, and decoded
   payload metadata never select or change family identity.
3. Metadata may validate a map or selected plan only when an independent owner-reviewed physical
   or command difference grants that predicate authority.

### Common FW profile intervals

1. Only runtime postbuild profiles participate in production selection. Evidence-only profiles are
   retained for regression and investigation but never create a production version boundary.
2. The minimum production Common FW generation is `1.0.0`.
3. One runtime profile for an IC covers the complete `[1.0.0, infinity)` interval. Common FW is not
   required to select that profile; a readable value below `1.0.0` is invalid, while missing or
   unreadable informational metadata does not by itself block the only plan.
4. With multiple owner-provided runtime profiles, sort their effective versions. The first profile
   covers `[1.0.0, nextEffectiveVersion)`, each following profile starts at its declared effective
   version, and the last interval has no upper bound.
5. When multiple intervals exist, a readable valid Common FW version is required because runtime
   cannot safely choose between distinct byte/command profiles without it. Missing, malformed,
   below-minimum, overlapping, or ambiguous interval selection fails closed with a typed issue.
   The independent FW/bar complement field does not validate or invalidate Common FW selection.
6. The version embedded in a source BAT filename or golden case is provenance. It becomes an
   effective boundary only when the owner supplies that entry as another runtime profile.

Consequently:

- NT51926 uses the profile sourced from 1.4.1 for `[1.0.0, 2.0.0)` and the profile sourced from
  2.0.0 for `[2.0.0, infinity)`.
- NT51930 has one runtime profile sourced from 1.4.0 and therefore uses it for
  `[1.0.0, infinity)`. Its inspected 2.0.0 entry remains evidence-only and creates no boundary.

### Build-plan authority

1. A build plan is one distinct owner-provided ordered command plan plus its topology selector.
2. Supported selector kinds are single, generic cascade (`Number > 1`), exact count, and a
   non-overlapping count range. A count range exists only when a distinct command plan requires it.
3. A golden's observed count never creates an exact-count or count-range plan. Generic cascade is
   not narrowed to the count used by a fixture.
4. Count ranges are non-overlapping and deterministic. NT51930 currently declares only `2..13`;
   count `14` and above remain unavailable until the owner supplies a distinct command plan.
5. NT51927 has exactly three production plans: single, 2-chip, and 3-chip. A storage-level generic
   cascade command collection is not a fourth selectable plan.
6. NT51930 has exactly two selectable plans: single and `cascade_2to13`; the UI labels them `1 IC`
   and `2–13 IC` and does not expose a generic Cascade choice.
7. Requested Number/topology selects the plan. A decoded FWConfig chip count may cross-check the
   selected topology, but it does not select family or create a plan. A contradiction with an exact
   plan fails closed; missing count remains informational when the requested selector uniquely
   identifies one plan.
8. NT51928 non-NB has the same TP layout and single/2-chip/3-chip postbuild plans as NT51927. Its
   distinct DP/LDC content begins at `0x40000` and remains outside CtrlRAM authority, so all three
   matching TP plans route through separate 512 KiB maps. NT51928 NB remains outside this decision.
9. NT51950 and NT51951 each expose single and generic cascade. Their TP ranges, DiffDLM
   outer envelope `[0x33200,0x34600)`, offsets, and postbuild command contract match. The
   owner-confirmed record splits that envelope into writable Diff CtrlRAM `[0x33200,0x33B10)` and
   reference-preserved Diff NF `[0x33B10,0x34600)`. Their `0x40000` and `0x80000` full-image
   capacities remain distinct map facts.
   Unlike NT51928, LDC is already packaged inside each IC's DP payload and therefore creates no
   separate CtrlRAM or DP input. AB behavior is a separate decision.

### DiffDLM and Diff NF write authority

1. When the canonical firmware map declares a DiffDLM record that includes a Diff NF tail, an
   AE-provided DiffDLM artifact uses the same full record stride as the target, but that source NF
   may be uniform `0x00`/`0xFF` filler and is not valid mutation authority. A contiguous artifact
   copy cannot be the final active-record mutation authority. A compatibility implementation may
   copy the declared payload first only when the same compiled/verified sequence restores every
   active Diff NF byte from the immutable reference before integrity processing.
2. A canonical profile owns the source-record length, target-record stride, target DLM subrange,
   preserved NF subrange, IC Count applicability, and evidence. The compiled plan lowers those
   facts into ordinary explicit copy operations; no processor or UI-only mask may expand them.
   Every Diff NF-bearing DiffDLM declaration must bind at least one explicit preservation-mask
   subrange. Writable DLM and preservation-mask subranges must be non-overlapping, remain inside
   the target record, and together cover each complete active record stride. Missing, `unknown`,
   overlapping, or incomplete mask authority makes the affected DiffDLM route unavailable before
   plan compilation. A DiffDLM filename or outer envelope alone does not imply Diff NF coupling;
   DLM-only routes retain their separately declared write geometry.
3. The canonical geometry table in `SPEC.md` owns the 929-like and 950-like record facts. For a
   zero-based slave record `i`, source and target record bases advance by the declared `0x1400`
   stride. The 929-like family writes `0x0B90` and preserves `0x0870`; the 950-like family writes
   `0x0910` and preserves `0x0AF0`. Every required writable source subrange must contain more than
   one distinct byte; the validator checks all required records, not only the first. Zero-based
   block `0` represents IC1; Cascade IC Count `N` has exactly `N - 1` active blocks
   `0..N-2`. Bytes after that active prefix but still inside the profile-declared DiffDLM
   replacement extent are inactive passthrough: selected source bytes replace the same relative
   target bytes without an NF mask. A missing inactive suffix leaves the immutable reference
   unchanged; source overflow is ignored with a warning and never widens target write authority.
4. Preview, execution, mutation audit, and golden evidence must prove every Diff NF byte remains
   identical to the immutable reference for every active record while every requested DLM record is
   placed at its declared target subrange. The mutation audit separately identifies the
   owner-approved inactive passthrough; one undifferentiated contiguous mutation allowance over the
   outer envelope is forbidden.
5. Every independent NF slot is unavailable when the resolved route is Cascade and uses the
   preservation-mask policy. Presentation omits the slot, and the common Application/CLI
   authoring contract rejects stale or manually constructed requests that bind it. This is a
   conservative fence against implying that one NF input is distributed into record-local Diff NF
   tails; it is not UI-only authority. Single routes are not restricted, and full-replacement
   Cascade routes retain their independently declared NF behavior. A future `NF0`/`NF1`/...
   per-record input contract requires a separate owner decision. Direct `DiffNFMerge.exe`
   authoring remains unsupported.
6. NT51950 and NT51951 use their own owner-confirmed `0x0910 + 0x0AF0 = 0x1400` geometry; they do
   not inherit NT51932's split. The current 51950 map defines Cascade as 2 IC with one slave record.
   A future count expansion requires separate IC Count applicability evidence rather than extending
   this record count by analogy.
7. NT51923, NT51926, and the NT51927 TP family use full-artifact DiffDLM
   replacement. A `DiffDLM.bin` filename or an `NF_Diff_*` inventory does not
   grant a preservation mask or change those routes.
8. ADR 0042 retires NT51920, NT51925, NT51930, and NT51931 from the
   `0.10.x` production capability set. Their legacy `0.9.x` evidence may remain
   as historical characterization, but no `0.10.x` mask, family migration,
   authoring route, processor, or support claim is created for them.

### Production route key

The CtrlRAM-only V2 route registry is keyed by the already selected IC member, runtime postbuild
processor profile, and resolved command branch. The resolved plan retains its typed selector; the
registry does not repeat selector mode as another admission dimension. It does not contain PID,
exact golden Common FW, TP FW, filename, complete-file SHA, or a generic-cascade fixture count.

Capacity, marker assertions, region containment, processor registration, immutable input handling,
declared write ranges, final validation, and atomic output promotion remain fail-closed execution
checks. They validate the selected execution; they do not identify a family.

### V2 map admission

1. After the runtime profile interval is selected, its trusted `mapBinding.mapIds` is the complete
   candidate scope presented to canonical family resolution. Maps owned by another Common FW
   interval are not competing candidates for that compile request.
2. Domain still resolves exactly one physical map inside that closed scope from member, mode,
   capacity, and the requested build-plan topology. Neither Profiles nor Bootstrap may select the
   largest, newest, first, or otherwise default map.
3. PID, filename, complete-file hash, exact golden Common FW, and a fixture's chip count do not
   re-enter V2 map admission as metadata predicates. FWConfig decoding remains available for
   display, interval selection when multiple runtime profiles exist, and the requested-topology
   cross-check.
4. Profile/family content hashes, allowed map ids, required regions, processor authority, write
   ranges, and immutable reference capacity remain fail-closed admission checks.

### Candidate version identity

1. The v0.9.12 CtrlRAM bundles are unreleased executable candidates. Admission-only normalization
   does not change firmware ranges, command order, processor authority, or output naming. The later
   owner-authorized NT51928 and NT51950/NT51951 route materialization adds maps/regions, so those
   family compatibility versions advance to `0.3.0`; new topology profiles also use `0.3.0`.
2. Every changed JSON document receives a new content hash, every containing bundle receives a new
   RFC 8785 entry-array hash, and Bootstrap pins that exact bundle hash. Reports and review evidence
   therefore distinguish the revised candidate even when its compatibility version is unchanged.
3. The newly materialized NT51926 Common FW 1.x single profile uses the same `0.2.0` runtime-profile
   contract generation as the adjacent single/cascade runtime profiles. It does not promote support.
4. Reusing a compatibility version is permitted only while the candidate has not shipped. A change
   to a tagged/published profile, or a future range/operation/processor-authority change, requires a
   semantic version bump in addition to new content hashes.
5. The trusted built-in loader entry-count bound rises from 8 to 16 so one IC bundle can contain all
   reviewed interval/plan profiles. Manifest size, per-entry size, JSON depth, path inventory, trust
   anchor, and hash checks remain bounded and unchanged; this does not admit user-authored bundles.

## Consequences

### Positive

- New project IDs and later Common FW patch/minor versions use the intended existing profile.
- Adding a future runtime profile creates one explicit version boundary instead of an exact-version
  allowlist.
- Build-plan coverage follows real command differences rather than the available golden sample set.
- Golden regression continues to prove exact bytes without constraining unrelated production input.

### Trade-offs

- The current exact/major version matcher must be replaced by an effective-version interval model.
- The typed plan-selector contract adds one explicit topology layer above the retained command
  branch enum; storage-only duplicate command collections do not create selectors.
- Existing exact-case V2 family applicability and route tests must be migrated without weakening
  byte ranges, processor authority, or expected-output evidence.

## Compatibility and migration

- No firmware range, operation order, command argument, checksum rule, or output naming token is
  inferred by this ADR. The 2026-07-21 owner follow-up explicitly authorizes the additional
  NT51928 non-NB and NT51950/NT51951 topology routes described above; all remain support-neutral.
- NT51926's already modeled Common FW 1.x single command plan is now materialized as a trusted V2
  route alongside its cascade route; both retain the existing processor and write authority.
- Historical exact-case documents remain evidence records. Current normative documents and release
  notes must state that their exact metadata identifies fixtures, not production admission.
- Existing reports continue recording Common FW, PID, chip count, filenames, and hashes for
  traceability.
- NT51928 partial-family authority is limited to the separately owner-approved NT51927-equivalent
  TP single/2-chip/3-chip plans. The partial alias does not authorize NB or reuse DP/LDC semantics.
- The `0.9.x` route registry covers all 31 historical runtime
  interval/build-plan pairs in the postbuild catalog; there is no
  golden-derived fail-closed exception list. ADR 0042 separately removes the
  four retired ICs rather than migrating that complete inventory into
  `0.10.x`.

## Verification

1. Architecture tests reject family lookup by metadata and reject PID/hash/filename fields in the
   production route key.
2. Version tests cover one-profile missing/varied Common FW and every multiple-profile interval
   boundary, including `1.0.0`, representative values below/at/above each boundary, malformed
   input, and evidence-only entries.
3. NT51926 tests prove 1.x versions use the 1.4.1-sourced profile and 2.x/later versions use the
   2.0.0-sourced profile. NT51930 tests prove 1.x, 2.x, later, and missing Common FW all select its
   only runtime profile.
4. Plan tests prove NT51927 exposes only single/2-chip/3-chip, NT51930 exposes only single/2–13,
   count 14 and generic Cascade are rejected for NT51930, and generic cascade on other ICs accepts
   every count above one without inheriting a golden fixture count.
5. Production-route tests vary PID, filename, preserved bytes/hash, and informational firmware
   fields while keeping IC, selected profile, plan, map, and processor authority valid.
6. Existing full-byte golden, processor write-range, immutable-source, atomic-output, and report
   evidence remain unchanged and must still pass before release.
7. V2 preparation tests prove that two same-capacity maps from different profile intervals are not
   ambiguous after the selected profile's map binding scopes canonical resolution, while an
   unscoped family resolution remains ambiguous.
8. NT51928 tests compile all non-NB single/2-chip/3-chip maps at `0x80000` and retain the DP/LDC
   tail; its DP Replace contract independently requires non-overlapping DP and LDC inputs.
9. NT51950/NT51951 cascade tests retain the distinct `0x40000`/`0x80000` map capacities and use the
   owner-confirmed `0x0910` writable plus `0x0AF0` preserved split. Promotion still waits for direct
   expected-output NF-preservation evidence.
10. NT51919/NT51929/NT51932 DiffDLM tests use distinct sentinels in every DLM and NF subrange, vary
    cascade IC Count across the admitted `2–8` range, prove full-stride source record ordering,
    reject an all-identical required DLM subrange, and require byte-identical preservation of every
    NF subrange. The owner-provided NT51932 4-IC golden must exercise three active DLM records.
11. Mask-policy tests prove an inactive source suffix replaces the same relative
    target bytes, a missing suffix retains the immutable reference, overflow
    cannot widen the declared target extent, every independent NF binding is
    rejected for the affected Cascade route, and a Single route retains its
    separately declared NF behavior.
