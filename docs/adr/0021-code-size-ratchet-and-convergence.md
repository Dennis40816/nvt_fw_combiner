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
production-runtime reduction objective. This amendment remains the single
canonical definition of that measurement; its original numeric completion
target is retained below as dated decision history and is superseded by the
2026-08-08 amendment:

- baseline release: stable `v0.9.16`, peeled commit
  `462590e8b993b8e42d088bc07377571a4bb9f25d`;
- counted C#: nonblank physical lines in `src/**/*.cs`, excluding
  `src/NvtFwCombiner.Presentation.Avalonia/**` and generated/build directories;
- counted Python: nonblank physical lines in
  `tools/crc-worker/src/**/*.py`, excluding generated/cache directories;
- excluded: tests, UI/Presentation, scripts, packaging, generated/build output,
  `refcode`, vendored code, and declarative profiles; and
- baseline and then-current target: 45,214 nonblank lines across 405 files at
  baseline,
  reduced below that baseline to 44,000 lines or fewer at final `0.10.x`
  integration. The four engineering slice caps allocate 43,000 lines and leave
  1,000 lines unallocated for integration. Later added or removed files in the
  counted paths remain part of the measurement.

Nonblank means a physical line whose Unicode text is not empty after whitespace
trimming; files are decoded as UTF-8 with an optional BOM. The target cannot be
met by deleting tests, compressing formatting, moving runtime logic into an
excluded path, generating an equivalent hidden owner, reducing coverage, or
weakening behavioral, architecture, firmware, security, or release gates.

Issue #171 made this exact metric executable through the existing
`scripts/verify.py` path. During prerequisite migration it reports the measured
runtime-production total while the existing `src/` C#/AXAML capacity and
partial/duplicate ratchets remain active. Core Convergence later enables the
descending slice ratchets defined below. The original plan assigned #197 the
44,000-line hard final integration gate; the 2026-08-08 amendment replaces that
numeric activation with reviewed-ledger integration. No transition adds another
verification entry point.

This is a convergence control, not permission to delete safety. Tests, golden
vectors, evidence manifests, documentation, firmware-owner gates, and useful
comments are outside the production-source metric. A change must not weaken
validation, byte parity, immutable-input handling, self-contained packaging,
or human review merely to satisfy a number.

Owner decision, 2026-07-27: the first hard final integration gate was set after
the measured `0.10.x` integration tree grew to 53,266 counted nonblank lines.
The 2026-08-06 amendment first replaced its numeric target, and the 2026-08-08
amendment supersedes both fixed numeric gates. These paragraphs preserve the
decision sequence; the current completion authority is the reviewed-ledger and
exact-ratchet contract below.
Code-size convergence therefore has two implementation responsibilities in
addition to the final #197 check:

- every migration slice records its counted production delta and deletes each
  superseded owner as soon as caller parity and the required R2/R3 evidence
  permit; and
- after Workbench/parallel-catalog deletion and legacy-runtime retirement, one
  dedicated **Canonical Core Convergence** phase owns the remaining measured
  simplification of Domain, Application, Profiles, Infrastructure, Bootstrap,
  Contracts, CLI, and the CRC worker before #197 may close.

The dedicated phase must use explicit layer measurements, zero-caller/deletion
evidence, behavior and byte parity, coverage, and dependency-direction tests.
It may consolidate duplicate DTOs, resolution states, compiler paths,
fingerprints, validators, and registration projections, but it may not replace
them with an equally broad facade, generated hidden owner, excluded-path
implementation, or weaker contract. The original decision blocked #197 above
44,000 lines; the 2026-08-08 amendment replaces that numeric condition without
weakening the other gates.

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
reduction. This exception does not increase the final gate, the integration
reserve, or any other issue budget.

Verifier evidence for #254 records a pre-existing measurement mismatch rather
than silently changing that owner amendment. The exact reviewed #208 merge
checkpoint `604199ab4a6bdb2f2da3f976851ce1c00b467bc8` (tree
`35d83c477fff39f0fc11404f5dbcee7f007a3f4d`) measured 76,633 runtime nonblank
lines with the canonical verifier, while the amendment expected 75,481. The
#254 candidate measures 75,638 after the explicit General Replace Preview/Build
boundary and shared Saved Rule schema gate, an exact reduction of 995 lines from that
observed merge tree. The 1,152-line baseline discrepancy creates no allowance,
does not alter the 75,481 authority, and must be reconciled before any later
pre-Core positive growth is accepted.

This envelope is a temporary review ceiling, not a new target, transferable
slice budget, or permission to move runtime logic into excluded Presentation,
generated output, profiles, scripts, or tests. It does not increase the final
gate or the unallocated integration reserve.

Issue #195 candidate measurement, 2026-08-05: deleting the Workbench facade and
parallel support-policy owners while introducing the required focused
Application contracts changes the canonical runtime metric from 76,145
nonblank lines at base commit
`649a84d7ef096b779d9268c24ff2a1e36a9fbbe8` to 76,345, an exact increase of
200 lines. The full production measurement changes from 749 files / 104,641
nonblank lines to 763 files / 105,064 nonblank lines, an increase of 14 files /
423 lines. The earlier 75,329, 76,141, and 76,314 intermediate measurements did not
include the final focused-owner and compiler-slot-identity corrections and are
not release evidence. The retired 6,905-line Workbench aggregate is not
renamed: its largest focused successor is `CompositionExecutionAdapter` at
2,601 nonblank lines, followed by `CompositionPlanningAdapter` at 1,719 and
`CompositionMemoryProjection` at 1,116. A 3,000-line architecture and policy
ceiling prevents the execution owner from regrowing. This candidate is not a
reduction and therefore cannot consume an implicit allowance under the
pre-Core policy; merge requires an explicit owner amendment for the 200-line
counted-positive delta. At that checkpoint the final 44,000-line gate was
unchanged; the 2026-08-08 amendment later superseded it.

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

Each slice owns an exact descending ratchet, caller inventory, line-addressed
candidate/residual ledger, architecture tests, and measured delta. It may merge
independently to the approved integration branch and may not carry a new
product feature or UI layout change. Shared registries, schemas, and trust
manifests retain one writer. The umbrella closes only after all four reviewed
ledgers contain no unclassified or still-eligible candidate and all retained
gates pass.

Owner amendment, 2026-08-06: the exact reviewed integration head
`d444040403dcea796636ee11839ee9c251de5c19` measures 524 files / 74,325
nonblank runtime lines. The owner replaced the earlier percentage-derived
target with a behavior-preserving outcome: final production must remain below
the formal `v0.9.16` baseline of 45,214 lines, the expected engineering landing
point is 43,000 lines, and the hard final integration gate is 44,000 lines.
This replacement reflects the required canonical/headless responsibilities;
it does not weaken any architecture, byte, golden, processor, coverage,
security, review, or anti-gaming invariant.

The 2026-08-06 planning caps and exact then-remaining reductions were:

| Slice | Measured 2026-08-06 | Final cap | Required reduction |
| --- | ---: | ---: | ---: |
| Domain + Profiles | 24,953 | 18,000 | 6,953 |
| Application | 23,671 | 12,000 | 11,671 |
| Bootstrap + CLI | 18,670 | 7,500 | 11,170 |
| Infrastructure + Contracts + CRC worker | 7,031 | 5,500 | 1,531 |
| Allocated total | 74,325 | 43,000 | 31,325 |

From the same 74,325-line head, that plan required at least
30,325 lines of total reduction; meeting all four engineering caps requires
31,325 lines.

The plan treated the remaining 1,000 lines as an unallocated integration
reserve rather than growth budget owned by any slice. The 2026-08-08 amendment
below supersedes these numeric completion gates while retaining their measured
history and every anti-gaming invariant.

The same amendment freezes exact descending ratchets at the five measured
values above: total 74,325, Domain + Profiles 24,953, Application 23,671,
Bootstrap + CLI 18,670, and Infrastructure + Contracts + CRC worker 7,031.
Every Core PR must lower each affected ratchet in the same commit except for a
dated, named, non-transferable owner amendment such as PL-02 or PL-04; therefore
an equivalent cross-slice relocation fails on the receiving slice. Each named
exception freezes its exact replacement ratchets immediately and cannot fund a
later PR. The four slice
measurements must also sum exactly to the runtime total, so a new unallocated
runtime project fails rather than becoming a fifth ownership bucket.

Owner amendment, 2026-08-08: at the audited #230 entry checkpoint
`3451d945`, the repository measured 69,730 runtime lines and 20,392 Domain +
Profiles lines.
A line-addressed, caller-resolved audit found only 160–182 lines of immediately
safe Domain + Profiles reduction and an evidence-backed R2 upper bound of
234–292 lines. The earlier 18,000 slice cap and 44,000 total gate therefore
cannot be defended as completion criteria without speculative rewrites,
cross-slice relocation, durable-contract breakage, or disproportionate
firmware/evidence risk. The owner replaces all four numeric slice caps and the
44,000 total gate with evidence-backed maximum practical convergence for #229
through #233. The old numbers remain dated planning history, not current
acceptance or release gates.

The completion contract is now:

- exact total and four-slice descending ratchets remain mandatory, and every
  Core PR lowers each affected slice and the total in the same commit except
  for an explicitly named, dated, non-transferable owner amendment;
- each slice maintains a line-addressed, non-overlapping candidate ledger;
- every mutually compatible, evidence-backed, in-scope candidate with a
  proportionate implementation, verification, and evidence cost and a
  net-negative result is completed; a safe candidate is not skipped solely
  because its individual reduction is small;
- an unimplemented de minimis candidate must show that its total implementation,
  verification, and evidence cost is disproportionate to its maintenance
  benefit; every other retained candidate is classified as a durable contract,
  another ticket's authority, or evidence/R3 blocked, and a cross-scope
  disposition names its owning layer and existing issue when one exists;
- completion requires zero unclassified or still-eligible candidates, full
  behavioral/golden/architecture evidence, and exact cumulative measurements;
  it is not inferred from elapsed time or a percentage; and
- #197 verifies completion of the four reviewed ledgers, exact ratchets,
  slice-sum integrity, and all existing release gates. It does not activate a
  separate numeric final-target mode.

This amendment does not relax measurement coverage, no-growth ratchets,
anti-relocation rules, byte/golden/processor/coverage/security evidence, or
independent review. It changes only the disproven numeric landing assumptions
and removes their dormant verifier activation path.

### 2026-08-09 complete-retirement assessment amendment

The owner-approved legacy-architecture retirement scope is measured from the
reviewed #352 predecessor commit
`6ba7217299c3e2ddb1c38467e6f288d5710ffef1`. That tree contains 68,767 runtime
production nonblank lines and 97,498 full-production nonblank lines including
Presentation. The audited direct Workbench/Bootstrap/Application migration
surface is 14,543 nonblank lines. Required canonical contract additions, unique
trust/profile/diagnostic/delivery behavior, CLI relocation, and retained
platform adapters mean that gross surface is not a deletion promise.

The pre-implementation assessment was a net reduction of 7,800–10,200
nonblank production lines, with a descriptive range of 58,600–61,000 runtime
lines and 87,300–89,700 full-production lines. Terminal implementation
evidence supersedes that estimate: 67,433 runtime and 96,044 full-production
lines, respectively 1,334 and 1,454 below the predecessor. The estimate
overstated the duplicate share because unique trust, route, admission,
reporting, delivery, and processor behavior had to move to its canonical owner
and the one-path boundary required explicit accepted-identity contracts.

These measured values are exact descending ratchets, not new final targets or
reusable budgets. In particular, 25,000 lines for the entire runtime is not a
credible behavior-preserving target. The exact method, gross/add/net ledger,
and inventory are recorded in
[`0.10.x-legacy-architecture-retirement-size-assessment.md`](../governance/0.10.x-legacy-architecture-retirement-size-assessment.md).

Implementation remains governed by the exact descending ratchets and
candidate-ledger contract above. Each vertical retirement slice reports gross
removal, required additions by receiving owner, cross-assembly relocation, and
net runtime/full-production deltas; migrates every named live caller; deletes
the displaced semantic owner in the same reviewed aggregate; and contributes
to a net-negative terminal runtime and full-production result. The measured
result does not complete LAR-00 until the terminal frozen diff and all
byte/golden/architecture/release evidence pass.

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
fixed golden, approved synthetic oracle, or contract sentinel. Production-code
convergence never lowers coverage, golden, firmware-owner, or independent-review
gates.

The repository continues to measure and report each slice. After
Workbench/parallel-catalog deletion and legacy-runtime retirement, the Core
Convergence entry change freezes the measured baseline for all four slices and
enables exact descending slice ratchets through the existing canonical
verifier. Every later Core PR lowers each affected slice ratchet and the total
metric. Moving equivalent code to another slice cannot satisfy either check.

Owner amendment on 2026-08-06 permits a bounded Application caller migration
before #230 closes only as a #230-owned same-PR exception when that caller is
the last blocker to deleting a named Domain + Profiles compatibility surface.
The bounded caller migration and deletion must land in the same PR, the deleted
surface must have zero remaining callers, no measured slice may grow, and both
the deleted-owner ratchet and runtime total must fall. This exception is not
independent #231 execution; broad #231 implementation begins only after #230
closes and the owner separately approves its intake. It does not authorize an
Application semantic mirror, UI or Bootstrap behavior work, cap reallocation,
or a caller-migration PR that leaves the superseded surface alive.

Final integration enforces the latest exact total and four-slice ratchets,
slice-sum integrity, completed candidate dispositions, and existing behavioral
and release gates. These checks remain modules invoked by `scripts/verify.py`;
no second code-size command, validator, or CI entry point is introduced.

### 2026-08-13 PL-02 ownership-accounting amendment

PL-02 (#374) deletes the 216-line Presentation-local filesystem/JSON I/O owner
and moves bounded stable reading plus atomic writing to an Application port and
Infrastructure adapter. Presentation is included in full-production
measurement but excluded from the four-slice runtime metric, so requiring both
measurements to descend would forbid the approved dependency correction by
construction.

The owner therefore accepts one non-transferable rebaseline: full production
falls from 97,306 to 97,303 nonblank lines, while runtime changes from 67,186 to
67,371. The affected slice values become Application 29,404, Bootstrap plus CLI
plus Desktop 3,267, and Infrastructure plus Contracts plus CRC worker 14,081;
Domain plus Profiles remains 20,619. The deleted Presentation owner and all
three report/history input callers migrate in the same PR.

This is not temporary deletion debt or a reusable allowance. The new runtime
and slice values are exact descending ratchets immediately after PL-02. Later
work cannot reclaim the 190-line classification increase, repeat an
excluded-to-counted relocation without a new owner decision, or satisfy the
full-production reduction through minification, test/evidence deletion, or
hidden code. All architecture, report-wire, coverage, firmware, Golden, and
release gates remain intact.

### 2026-08-13 PL-04 truthful-progress amendment

PL-04 (#376) must show startup-report bytes over the bounded local-file read's
stable admitted total. The PL-02 port and adapter owned the read but exposed no
progress contract, so satisfying ADR 0049 requires a focused typed observer in
the counted Application and Infrastructure slices rather than a duplicate
Presentation reader.

The owner accepts this one non-transferable rebaseline: preserving the complete
lifecycle implementation and the non-obvious contract documentation required
by Decision 31 changes full production from 97,297 to 97,426, while runtime
changes from 67,371 to 67,404.
Application becomes 29,410 and Infrastructure plus Contracts plus CRC worker
becomes 14,108; Domain plus Profiles remains 20,619 and Bootstrap plus CLI plus
Desktop remains 3,267. The added contract also keeps observer exceptions
distinct from source failures. These values are exact descending ratchets after
PL-04. The +129 full-production and +33 runtime deltas create no reusable
allowance, authorize no cross-slice relocation, and do not weaken firmware,
report-wire, Golden, coverage, or release gates. Every later PR resumes from
these exact descending ratchets.

### 2026-08-13 PL-05 external-environment lifecycle amendment

PL-05 (#377) deletes Bootstrap's synchronous lazy external-processor factory
and moves discovery behind one bounded Infrastructure loader and one typed
Application lifecycle. The original 170/145 planning envelope did not include
the required filesystem depth/entry/count/byte bounds, stable regular-file
identity and cancellable SHA validation, separate request/publication
generations, last-known-good publication, generation-zero blocked readiness,
or the shared shell, Message Center, and CLI adoption.

The owner accepts one named, non-transferable rebaseline. The fixed
implementation removes 250 and adds 984 physical nonblank production lines,
net +734. Full production changes from 97,426 to 98,160 and runtime from
67,404 to 68,018. Runtime is exactly 20,619 Domain plus Profiles + 29,585
Application + 3,074 Bootstrap plus CLI plus Desktop + 14,740 Infrastructure
plus Contracts plus CRC worker. These values become descending ratchets
immediately. This exception is PL-05-only, cannot offset PL-06/07/00, and does
not authorize minification, cross-slice relocation, process/protocol changes,
or weaker firmware, Golden, report-wire, coverage, or release evidence.

### 2026-08-13 Message Center readability amendment

The owner separately requested a visual-only readability pass for Message
Center. Presentation XAML now renders System Information as four aligned
two-column fact cards and separates Current Report from Report History into two
equal action cards. The exact physical nonblank production delta is 67 added
and 38 removed, net +29. Full production changes from 98,160 to 98,189;
runtime remains 68,018 and every counted runtime slice remains unchanged.

This named exception is non-transferable and cannot fund PL-06, PL-07, PL-00,
or later work. It changes no report or diagnostics data, firmware behavior,
profile/schema/processor contract, Golden evidence, persistence, or release
gate. The new 98,189 value immediately becomes the descending full-production
ratchet.

### 2026-08-13 PL-06 coherent-inspection convergence

PL-06 (#378) resumes ordinary descending enforcement. It deletes the separate
before/after file-stamp probe, Presentation path/base caches, and the unbounded
IC-only compiled-classification cache, while adding one coherent cancellable
content identity and typed selective-dispatch path. The exact physical nonblank
ledger is 403 removed and 289 added, net -114.

Full production changes from 98,189 to 98,075 and runtime changes from 68,018
to 68,016. Runtime is exactly 20,619 Domain plus Profiles + 29,584 Application +
3,074 Bootstrap plus CLI plus Desktop + 14,739 Infrastructure plus Contracts
plus CRC worker. These values become descending ratchets immediately; no
accounting exception, excluded-path relocation, minification, firmware semantic
change, or evidence reduction is accepted.

Documentation consolidation is legitimate only when it removes boilerplate or
moves repeated information to its canonical owner. XML summaries that merely
repeat a member/type name, forwarding properties/constructors, migration DTO
mirrors, and accidental-public implementation surfaces need no duplicated
comment. Canonical persisted contracts and reusable public boundaries retain
their useful documentation.

### 2026-08-14 PL-07 six-workflow inspection lifecycle

PL-07 (#379) removes the Presentation firmware-reader wrapper, global loading
flag, separate General preparation queues/tasks, and duplicated workflow
lifecycle traversal while adding one request-scoped observable lifecycle used by
all six Merge/Replace workflows. Application and Infrastructure continue to own
typed inspection facts and report exact work through `IProgress<T>`; Presentation
owns only generation, cancellation/drain, stale rejection, and loading projection.

The exact physical nonblank ledger is 950 removed and 949 added, net -1. Full
production changes from 98,075 to 98,074 and runtime changes from 68,016 to
67,997. Runtime is exactly 20,619 Domain plus Profiles + 29,571 Application +
3,074 Bootstrap plus CLI plus Desktop + 14,733 Infrastructure plus Contracts
plus CRC worker. These values become descending ratchets immediately. No
firmware semantic change, excluded-path relocation, physical-line compression,
or weakened behavioral evidence is accepted.

Firmware ranges/coordinates, CRC/Header and mutation authority, owner evidence,
known limitations, fail-closed rationale, non-obvious algorithms, security,
processor, lifetime, and concurrency invariants remain documented. Removing
that information, compressing multiple statements onto one physical line, or
moving documentation/logic into excluded generated output is anti-gaming, not
convergence.

`scripts/verify.py` remains the only canonical verification entry point. Code
size measurement modules have no independent command-line entry point and are
invoked by the existing repository structure validator.

### 2026-08-14 PL-00 semantic-control amendment

The owner explicitly requires the startup Cancel control, adjacent semantic
buttons, and shell navigation to use only project-owned normal, hover, pressed,
keyboard-focus, and disabled visuals. The shared semantic style had disabled
Avalonia's default focus adorner while only the secondary role supplied a
replacement, several other roles still relied on framework pointer-state
rendering, and the startup-completion Home focus exposed Avalonia's default
navigation focus rectangle. Closing those accessibility and visual-consistency
gaps is required release work, not a reusable size allowance.

PL-00 therefore removes 295 and adds 358 physical nonblank production lines,
net +63. Full production changes from 98,074 to 98,137; runtime still descends
from 67,997 to 67,981 = 20,619 Domain plus Profiles + 29,555 Application + 3,074
Bootstrap plus CLI plus Desktop + 14,733 Infrastructure plus Contracts plus CRC
worker. The same fixed diff preserves the current CtrlRAM inspection projection
while relocalizing its memory state instead of reconstructing input slots.

This named Presentation exception is non-transferable, changes no firmware,
profile, schema, report wire, persistence, processor, CLI, Golden, or support
semantics, and cannot fund later work. Full 98,137 and the unchanged runtime
slice values become descending ratchets immediately.

### 2026-08-18 Settings, input, button, and memory-readability amendment

The owner approved completing the current Settings and workflow-readability
surface without deleting localization, accessibility, truthful memory facts, or
responsive-layout evidence to preserve the preceding numeric ratchet. The
change makes every Button use the project theme with pointer/keyboard focus
semantics, keeps input facts visible at narrow widths, localizes real typed slot
titles and actions, and renders memory source, range, length, pending, conflict,
and protected-region facts in aligned English and Traditional Chinese views.

The exact physical nonblank ledger is 673 removed and 2,103 added, net +1,430.
Full production changes from 98,133 to 99,563. Runtime changes from 67,981 to
68,109 through typed canonical CtrlRAM family roles and Application memory
projection facts. Domain plus Profiles changes from 20,619 to 20,634, and
Application changes from 29,555 to 29,668. Bootstrap plus CLI
plus Desktop remains 3,074, and Infrastructure plus Contracts plus CRC worker
remains 14,733.

These exact values become descending ratchets immediately. This named R2
presentation/readability amendment includes one-item-per-source range grouping,
is non-transferable, and cannot fund later
work. It changes no profile, range, firmware byte, operation order, support
truth, Golden evidence, report wire/history, processor protocol, CLI, or release
gate. Later work resumes ordinary descending enforcement from these values.

### 2026-08-20 remaining-TODO reliability and Build Settings amendment

The owner directed completion of the remaining authorized TODOs for review and
then consolidated every mode-specific pre-Build choice into one shared Build
Settings surface. The same fixed work adds an exact accepted-session output
bundle admission, atomic primary/additional/source promotion, typed commit
receipts and additive bundle provenance, the six-profile DP Replace naming-safe
frontier, a route-independent Settings modal, and lifecycle-safe Memory Layout
interaction. AB A-only output is selected from the compiled delivery plan and
commits inside the same atomic bundle rather than through a second loose path.

The exact frozen measurement changes full production from 100,157 to 102,977
and runtime from 68,174 to 70,057 nonblank lines. Domain plus Profiles remains
20,632; Application becomes 30,691; Bootstrap plus CLI plus Desktop becomes
3,378; and Infrastructure plus Contracts plus CRC worker becomes 15,356. The
remaining 937-line delta is Presentation. These values become descending
ratchets immediately.

This named amendment is non-transferable and cannot fund later work. It is
limited to accepted-session identity, host delivery/receipt/report provenance,
mode-aware pre-Build UI, localization/accessibility, and fail-closed regression
evidence. It changes no firmware range, operation order, output byte,
CRC/header behavior, processor protocol, support claim, or Golden expected
bytes. DP Replace naming changes only the six recorded safe typed publications;
all unresolved provider, NT51928, CtrlRAM, and General naming gates remain
fail-closed under their recorded authority.

### 2026-08-22 v0.10.6 independent-R2 correction amendment

The fixed independent review of `e1212c11..67a83a23` found four P1 and three P2
cross-boundary defects in the internal managed-version candidate. The required
correction converges the managed verifier with the production release manifest,
adds actual-byte decompression limits, durable install/delete/activation
recovery, admitted versus recovery inventory, launcher-handoff failure
retention, exact runtime catalog schema admission, and the approved reduced-
motion checking ring. These are corrections to the already approved v0.10.6
scope, not a reusable feature-growth budget.

Relative to reviewed HEAD `67a83a23`, the exact physical production ledger is
1,655 nonblank lines added and 318 removed, net +1,337. Full production changes
from 107,237 to 108,574 and runtime changes from 73,555 to 74,635. Domain plus
Profiles remains 20,632; Application changes from 32,493 to 33,041 (+548);
Bootstrap plus CLI plus Desktop host changes from 3,501 to 3,502 (+1);
Infrastructure plus Contracts plus CRC worker changes from 16,929 to 17,460
(+531); Presentation accounts for the remaining +257.

The executable allowances therefore become exactly 5,677 full production,
4,578 runtime, 2,350 Application, 124 Bootstrap/CLI/Desktop host, and 2,104
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets.
They are non-transferable and become exact descending ceilings immediately.
This amendment changes no firmware profile, range, output byte, CRC/header,
processor, support, or Golden authority. It also does not close the separate
repository-wide unused-module and code-size investigation TODO; that audit must
still identify removable ownership and lower these ratchets in its own reviewed
change.

### 2026-08-22 v0.10.6 second independent-R2 correction amendment

The independent review of the first correction boundary
`67a83a23..6ca27508` confirmed the original seven findings but exposed one P1
cross-process writer race plus four P2 convergence/evidence gaps. Closing that
fixed review scope adds the exact state-path plus managed-root OS writer lease,
durable reload after lease acquisition, idempotent already-absent delete,
Application-owned recovery classification, production packager schema
evaluation, and real-adapter underreported-ZIP/installed aggregate-byte tests.

Relative to first-correction HEAD `6ca27508`, the exact physical production
ledger grows by 302 nonblank lines. Full production changes from 108,574 to
108,876 and runtime changes from 74,635 to 74,937. Domain plus Profiles remains
20,632; Application changes from 33,041 to 33,225 (+184); Bootstrap plus CLI plus
Desktop host changes from 3,502 to 3,503 (+1); and Infrastructure plus Contracts
plus CRC worker changes from 17,460 to 17,577 (+117). Presentation is unchanged.

The executable allowances therefore become exactly 5,979 full production,
4,880 runtime, 2,534 Application, 125 Bootstrap/CLI/Desktop host, and 2,221
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets.
They are non-transferable exact descending ceilings. This correction changes no
firmware/profile/range/output/CRC/processor/support/Golden authority and does not
close the separate unused-module and repository-size investigation.

### 2026-08-22 Settings reference-fidelity and circular Close-control amendment

The owner then approved two bounded Presentation corrections: the Settings
Version surface must match the accepted 1,584 by 997 reference geometry in both
Light and Dark themes, and every upper-right Close/Exit entry must use one shared
40 by 40 true-circle control with a centered vector glyph. The implementation
also replaces the source-status font glyph with an accessible vector icon and
retains the existing typed version-management commands, offline policy,
localization, reduced-motion checking indicator, and explicit install/delete
consent flows.

Relative to the second-correction HEAD, the exact physical production ledger is
515 nonblank lines added and 176 removed, net +339, entirely under Presentation.
Full production changes from 108,876 to 109,215. Runtime remains 74,937; Domain
plus Profiles remains 20,632; Application remains 33,225; Bootstrap plus CLI plus
Desktop host remains 3,503; and Infrastructure plus Contracts plus CRC worker
remains 17,577.

The executable full-production allowance therefore becomes exactly 6,318 above
the frozen pre-v0.10.6 base ratchet; every runtime and slice allowance remains
unchanged. This owner-requested visual correction is non-transferable and becomes
an exact descending ceiling immediately. It changes no firmware/profile/range,
output byte, CRC/header, processor, support, Golden, update-package verification,
installation, activation, or deletion authority. It also does not close or fund
the separate unused-module and repository-size investigation.

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
