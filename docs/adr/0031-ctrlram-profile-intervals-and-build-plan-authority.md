# ADR 0031: CtrlRAM routing uses IC identity, profile intervals, and build plans

- Status: Accepted for v0.9.12
- Date: 2026-07-21
- Owner decision: 2026-07-21
- Risk: R3; implementation and release still require firmware-owner review
- Amends: ADR 0030 production-admission decision 2

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
4. Count ranges are non-overlapping and deterministic. If evidence distinguishes `2..13` from the
   next cascade plan, the next interval starts at `14`; an overlapping `13+` declaration is invalid.
5. NT51927 has exactly three production plans: single, 2-chip, and 3-chip. A storage-level generic
   cascade command collection is not a fourth selectable plan.
6. Requested Number/topology selects the plan. A decoded FWConfig chip count may cross-check the
   selected topology, but it does not select family or create a plan. A contradiction with an exact
   plan fails closed; missing count remains informational when the requested selector uniquely
   identifies one plan.

### Production route key

The CtrlRAM V2 route registry is keyed by the already selected IC member, runtime postbuild profile,
build-plan id, and composition/input mode needed by the compiler. It does not contain PID, exact
golden Common FW, TP FW, filename, complete-file SHA, or a generic-cascade fixture count.

Capacity, marker assertions, region containment, processor registration, immutable input handling,
declared write ranges, final validation, and atomic output promotion remain fail-closed execution
checks. They validate the selected execution; they do not identify a family.

## Consequences

### Positive

- New project IDs and later Common FW patch/minor versions use the intended existing profile.
- Adding a future runtime profile creates one explicit version boundary instead of an exact-version
  allowlist.
- Build-plan coverage follows real command differences rather than the available golden sample set.
- Golden regression continues to prove exact bytes without constraining unrelated production input.

### Trade-offs

- The current exact/major version matcher must be replaced by an effective-version interval model.
- The current fixed postbuild branch enum cannot represent general count ranges cleanly and requires
  a typed plan-selector contract.
- Existing exact-case V2 family applicability and route tests must be migrated without weakening
  byte ranges, processor authority, or expected-output evidence.

## Compatibility and migration

- No firmware range, operation order, command argument, checksum rule, output naming token, or
  support state changes merely because of this ADR.
- Historical exact-case documents remain evidence records. Current normative documents and release
  notes must state that their exact metadata identifies fixtures, not production admission.
- Existing reports continue recording Common FW, PID, chip count, filenames, and hashes for
  traceability.
- NT51928 partial-family authority is not widened. Only its separately owner-approved facts and
  plans may route even when another family member has additional plans.

## Verification

1. Architecture tests reject family lookup by metadata and reject PID/hash/filename fields in the
   production route key.
2. Version tests cover one-profile missing/varied Common FW and every multiple-profile interval
   boundary, including `1.0.0`, representative values below/at/above each boundary, malformed
   input, and evidence-only entries.
3. NT51926 tests prove 1.x versions use the 1.4.1-sourced profile and 2.x/later versions use the
   2.0.0-sourced profile. NT51930 tests prove 1.x, 2.x, later, and missing Common FW all select its
   only runtime profile.
4. Plan tests prove NT51927 exposes only single/2-chip/3-chip and that generic cascade accepts every
   declared count without inheriting a golden count.
5. Production-route tests vary PID, filename, preserved bytes/hash, and informational firmware
   fields while keeping IC, selected profile, plan, map, and processor authority valid.
6. Existing full-byte golden, processor write-range, immutable-source, atomic-output, and report
   evidence remain unchanged and must still pass before release.
