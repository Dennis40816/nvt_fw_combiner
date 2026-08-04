# ADR 0021: Code-Size Ratchet and Convergence

- Status: Accepted
- Date: 2026-07-15
- Owners: Architecture owner

## Context

Production C# and XAML grew from 56,257 nonblank lines at `v0.9.2` to 60,237
at the `v0.9.7` merge. Most package bytes are the required self-contained .NET
and Avalonia runtime, while source growth is concentrated in V2 definitions
stacked beside compatibility paths, repeated built-in registration tables, and
large partial service aggregates. Deleting tests, evidence, comments, or
formatting would hide the problem without reducing runtime responsibility.

## Decision

The canonical repository validator owns exact, reproducible ratchets for:

- nonblank `.cs` and `.axaml` lines under `src/`, excluding generated output;
- nonblank lines in byte-identical JSON copies across `profiles/` and
  `docs/contracts/`; and
- the two existing partial aggregates above the general 2,500-line ceiling.

The initial ratchets are 60,237 production lines, 10,781 redundant exact JSON
lines, 6,033 lines for `WorkbenchCompositionService`, and 3,035 lines for
`MainWindowViewModel`. Growth fails validation. A reduction also requires its
ratchet to be lowered in the same commit, so later changes cannot reclaim the
removed budget. New partial aggregates may not exceed 2,500 lines.

### v0.9.10 owner amendment

Owner decision, 2026-07-19: `v0.9.10` initially used 60,000 nonblank production C#/AXAML
lines as a hard ceiling rather than an exact descending total-source ratchet.
Measured reductions below 60,000 do not require lowering that ceiling during
the performance release. The stable-reconciled candidate at `6f3698dd` measures
59,429 production lines. Its named partial aggregates are frozen as exact
ratchets at 4,404 lines for `WorkbenchCompositionService` and 4,034 lines for
`MainWindowViewModel`; the exact duplicate-JSON ratchet is 1,055 lines, and the
general 2,500-line partial ceiling also remains in force.

Owner amendment, 2026-07-20: release-blocking exact-head review fixes for
same-path firmware identity, final save-dialog revalidation, and workflow-scoped
inspection bring the reviewed tree to 60,050 production lines and 4,185 lines
for `MainWindowViewModel`. The owner explicitly prioritized the correctness fix
over the earlier source target. The temporary total ceiling is therefore
60,100, while the named exact ratchets are 4,419 for
`WorkbenchCompositionService` and 4,185 for `MainWindowViewModel`. This does not
relax the 1,055 duplicate-JSON or general 2,500-line partial gates.

The exception is time-bounded. `v0.9.10` added measured progress,
accessibility, report/Hex Diff, cancellation, persistence, inspection, and
performance evidence while preserving firmware semantics. A dedicated
code-size convergence phase starts only after `v0.9.11`; it will establish a
new measured baseline and lower descending ratchets instead of treating unused
space below 60,100 as a permanent budget. The final reviewed `v0.9.10` tree must
remain at or below 60,100 and must not exceed either named partial ratchet.

### v0.9.11 owner amendment

Owner decision, 2026-07-21: production-source capacity is temporarily widened
to 75,000 nonblank C#/AXAML lines until `1.0.0`; it is a capacity ceiling, not a
target or permission to duplicate firmware semantics. Named large aggregates
remain descending ratchets so reductions cannot silently become new growth
budget. The reconstructed candidate measures 4,418 lines for
`WorkbenchCompositionService` and 4,147 lines for `MainWindowViewModel`, which
become the new exact ceilings. The duplicate-JSON and general partial limits
remain unchanged.

After the approved `v0.9.11` release, branch and version governance will define
the next planned convergence slice. Until then, code-size policy must not force
unrelated refactors into the release candidate or weaken tests, evidence, and
firmware-safety boundaries.

### 0.10.x maintainability-program amendment

Owner decision, 2026-07-25: the `0.10.x` maintainability program has a separate
production-runtime reduction objective. This amendment is the single canonical
definition of that measurement:

- baseline release: stable `v0.9.16`, peeled commit
  `462590e8b993b8e42d088bc07377571a4bb9f25d`;
- counted C#: nonblank physical lines in `src/**/*.cs`, excluding
  `src/NvtFwCombiner.Presentation.Avalonia/**` and generated/build directories;
- counted Python: nonblank physical lines in
  `tools/crc-worker/src/**/*.py`, excluding generated/cache directories;
- excluded: tests, UI/Presentation, scripts, packaging, generated/build output,
  `refcode`, vendored code, and declarative profiles; and
- baseline and target: 45,214 nonblank lines across 405 files at baseline,
  reduced by at least 50% to 22,607 lines or fewer at final `0.10.x`
  integration. Later added or removed files in the counted paths remain part of
  the measurement.

Nonblank means a physical line whose Unicode text is not empty after whitespace
trimming; files are decoded as UTF-8 with an optional BOM. The target cannot be
met by deleting tests, compressing formatting, moving runtime logic into an
excluded path, generating an equivalent hidden owner, reducing coverage, or
weakening behavioral, architecture, firmware, security, or release gates.

Issue #171 made this exact metric executable through the existing
`scripts/verify.py` path. During prerequisite migration it reports the measured
runtime-production total while the existing `src/` C#/AXAML capacity and
partial/duplicate ratchets remain active. Core Convergence later enables the
descending slice ratchets defined below, and #197 applies the 22,607-line hard
final integration gate. No transition adds another verification entry point.

This is a convergence control, not permission to delete safety. Tests, golden
vectors, evidence manifests, documentation, firmware-owner gates, and useful
comments are outside the production-source metric. A change must not weaken
validation, byte parity, immutable-input handling, self-contained packaging,
or human review merely to satisfy a number.

Owner decision, 2026-07-27: the 22,607-line target remains a hard final
integration gate after the measured `0.10.x` integration tree grew to 53,266
counted nonblank lines. It is not an aspirational report and cannot be deferred
to a later release merely because canonical migration temporarily adds code.
Code-size convergence therefore has two implementation responsibilities in
addition to the final #197 check:

- every migration slice records its counted production delta and deletes each
  superseded owner as soon as caller parity and the required R2/R3 evidence
  permit; and
- after Workbench/parallel-catalog deletion and legacy-runtime retirement, one
  dedicated **Canonical Core Convergence** phase owns the remaining measured
  simplification of Domain, Application, Profiles, Infrastructure, Bootstrap,
  Contracts, CLI, and the CRC worker before #197 may close.

The dedicated phase must use explicit layer budgets, zero-caller/deletion
evidence, behavior and byte parity, coverage, and dependency-direction tests.
It may consolidate duplicate DTOs, resolution states, compiler paths,
fingerprints, validators, and registration projections, but it may not replace
them with an equally broad facade, generated hidden owner, excluded-path
implementation, or weaker contract. If the canonical core still exceeds
22,607 lines, #197 remains blocked.

Owner decision, 2026-08-02: merged PR #278 establishes the post-headless,
pre-Core measurement baseline at commit
`4dddf1a2822ee74a343d2b20a565115d745313ae`, with 72,750 counted nonblank
runtime lines. Until #229 freezes the Core-entry slice ratchets:

- all remaining pre-Core PRs together may consume at most 2,000 positive
  counted lines above that baseline;
- a negative delta does not refill this allowance or authorize unrelated
  growth, and the measured pre-Core high-water mark remains 74,750;
- #214 may contribute at most 1,300 counted lines of net growth; and
- each counted-positive PR records its baseline/head measurement, allowance
  consumed, superseded owner, remaining caller/evidence blocker, and deletion
  milestone. The existing verifier remains the only measurement command.

Owner amendment, 2026-08-04: #208 may raise the measured pre-Core high-water
mark once, and only once, from 74,750 to exactly 75,481 counted runtime lines.
The 731-line excess is non-transferable, creates no reusable allowance, and is
not refilled by a later negative delta. #254 is the immediate next
implementation ticket and must record its exact General/Saved Rule compatibility
reduction. This exception does not increase the 22,607 final gate, the 607-line
integration reserve, or any other issue budget.

Verifier evidence for #254 records a pre-existing measurement mismatch rather
than silently changing that owner amendment. The exact reviewed #208 merge
checkpoint `604199ab4a6bdb2f2da3f976851ce1c00b467bc8` (tree
`35d83c477fff39f0fc11404f5dbcee7f007a3f4d`) measured 76,633 runtime nonblank
lines with the canonical verifier, while the amendment expected 75,481. The
#254 candidate measures 75,630 after the explicit General Replace Preview/Build
boundary and shared Saved Rule schema gate, an exact reduction of 1,003 lines from that
observed merge tree. The 1,152-line baseline discrepancy creates no allowance,
does not alter the 75,481 authority, and must be reconciled before any later
pre-Core positive growth is accepted.

This envelope is a temporary review ceiling, not a new target, transferable
slice budget, or permission to move runtime logic into excluded Presentation,
generated output, profiles, scripts, or tests. It does not increase the 22,607
final gate or the 607-line unallocated integration reserve.

Canonical Core Convergence is one umbrella outcome, not one repository-wide
implementation PR. It is delivered through four independently reviewable,
ownership-bounded slices:

1. **Domain + Profiles** removes duplicate DTO, resolution, fingerprint,
   compiler, and normalizer paths.
2. **Application** converges use-case, readiness, inspection, authoring, and
   report models.
3. **Bootstrap + CLI** starts only after #195 has deleted Workbench and the
   parallel support catalogs. It removes remaining composition-root,
   registration, route-dispatch, and CLI projection duplication, retaining
   composition wiring only.
4. **Infrastructure + Contracts + CRC worker** removes duplicate
   adapter/protocol mappings while preserving constrained external-processor
   boundaries.

Each slice owns a final line budget, caller inventory, explicit deletion list,
architecture tests, and measured delta. It may merge independently to the
approved integration branch and may not carry a new product feature or UI
layout change. Shared registries, schemas, and trust manifests retain one
writer. The umbrella closes only after the combined metric is at or below the
hard target.

The owner-approved provisional hard caps are:

| Slice | Measured 2026-07-27 | Final cap |
| --- | ---: | ---: |
| Domain + Profiles | 22,683 | 10,500 |
| Application | 12,199 | 5,000 |
| Bootstrap + CLI | 12,774 | 2,000 |
| Infrastructure + Contracts + CRC worker | 5,610 | 4,500 |
| Allocated total | 53,266 | 22,000 |

The remaining 607 lines are an unallocated integration reserve, not growth
budget owned by any slice. A slice finishing below its cap does not
automatically transfer capacity to another slice. Any reallocation requires an
owner-approved architecture review that proves why the responsibility must
remain in that layer; it cannot raise the 22,607 final gate or weaken a
deletion, dependency, test, firmware, evidence, or release invariant.

Every Canonical Core Convergence PR must reduce both its slice measurement and
the total counted production metric. It cannot create temporary deletion debt.
Before that phase, a firmware/route migration may temporarily add counted code
only when an R3 golden, firmware-owner evidence gate, or not-yet-migrated caller
prevents safe same-PR deletion. The issue and PR execution record must then name
the superseded owners/symbols, remaining callers, added counted lines, exact
blocker, existing deletion ticket, and latest deletion milestone.

This record is planning/review evidence, not another runtime debt model or
parallel repository authority. Unclosed debt blocks the applicable headless
convergence, Workbench deletion, or legacy-runtime retirement milestone. A
generic TODO, unowned follow-up, or implied future cleanup does not authorize
temporary growth.

Tests remain outside the production-line metric, but that exclusion is not
permission to retain duplicate test ownership indefinitely. Convergence
preserves golden bytes/hashes, exact mutation ranges, operation traces,
observable CLI/UI behavior, typed issue codes, failure conditions, coverage
ratchets, and required R3 evidence. Tests coupled only to old implementation
filenames, type names, partial-file counts, source strings, or source positions
are deleted after equivalent behavioral and architecture coverage exists.

Old/new runtime differential tests are migration evidence with a named deletion
milestone. They cannot keep the old production runtime alive after its
authority reaches zero. Before deletion, their durable assertions move to a
fixed golden, approved synthetic oracle, or contract sentinel. The production
target never lowers coverage, golden, firmware-owner, or independent-review
gates.

The final slice caps do not block prerequisite migration before Canonical Core
Convergence begins. The repository continues to measure and report each slice.
After Workbench/parallel-catalog deletion and legacy-runtime retirement, the
Core Convergence entry change freezes the measured baseline for all four slices
and enables exact descending slice ratchets through the existing canonical
verifier. Every later Core PR lowers each affected slice ratchet and the total
metric. Moving equivalent code to another slice cannot satisfy either check.

Final integration enforces both the four maximum slice caps and the 22,607
total. These checks remain modules invoked by `scripts/verify.py`; no second
code-size command, validator, or CI entry point is introduced.

Documentation consolidation is legitimate only when it removes boilerplate or
moves repeated information to its canonical owner. XML summaries that merely
repeat a member/type name, forwarding properties/constructors, migration DTO
mirrors, and accidental-public implementation surfaces need no duplicated
comment. Canonical persisted contracts and reusable public boundaries retain
their useful documentation.

Firmware ranges/coordinates, CRC/Header and mutation authority, owner evidence,
known limitations, fail-closed rationale, non-obvious algorithms, security,
processor, lifetime, and concurrency invariants remain documented. Removing
that information, compressing multiple statements onto one physical line, or
moving documentation/logic into excluded generated output is anti-gaming, not
convergence.

`scripts/verify.py` remains the only canonical verification entry point. Code
size measurement modules have no independent command-line entry point and are
invoked by the existing repository structure validator.

## Consequences

- `v0.9.8` must remove duplicate ownership and unused compatibility code before
  adding production abstractions. On 2026-07-16 the owner accepted its exact
  56,742-line final ratchet, superseding the original 56,000-line stretch gate.
  `v0.9.9` must still exit at or below 54,000 production lines.
- The source ratchet and release-package byte budget are separate. Package
  changes require a reproducible package artifact and release-risk review. The
  owner-approved package maximum is 1% above the 57,501,699-byte baseline.
- Legacy Combiner 1.13 remains an approved constrained external tool. Its
  executable and runner are not code-size retirement targets.
- Non-Combiner legacy paths may be retired only through the Legacy Retirement
  Matrix with equivalent runtime tests and the required R2/R3 evidence.

## Verification

- `python -m unittest discover -s tests/scripts -p test_code_size_policy.py`
- `python scripts/verify.py --structure-only`
- `python scripts/verify.py --all` before the `v0.9.8` handoff
