# Predecessor comparison contract v1

This contract implements [ADR 0078](../adr/0078-predecessor-comparison-for-1x-releases.md)
(Proposed). It defines two separate modes of one comparator, which share the
execution primitives of the ADR 0057 comparator and add no byte semantics of
their own:

- the **rolling mode** compares every 1.x release candidate with the previous
  published stable release and is a release gate;
- the **v0.9.16 1.x mode** checks, at fixed milestones only, that the
  candidate is still consistent with v0.9.16 under the immutable ADR 0057 plan
  and its separate [1.x amendment](v0916-parity-1x-amendment-v1.md).

| File | Role |
| --- | --- |
| `predecessor-comparison-v1.json` and its schema | the two modes, executor recipe, environment policy, per-side safety, typed rejection, interface status, failure codes |
| `predecessor-comparison-scenarios-v1.json` and its schema | the rolling coverage ledger: scenarios, decision 12's debt set, accepted and pending gaps, retired scenarios |
| `predecessor-comparison-declaration-v1.schema.json` | one declaration per release under `predecessor-comparison-declarations/` (in effect, R35-02) |
| `predecessor-comparison-report-v1.schema.json` | the comparator's payload-free report of either mode (in effect, R35-02) |
| `predecessor-published-release-inventory-v1.schema.json` | the commander's complete offline published stable release inventory for rolling mode |

The contract, the amendment and every report state `certification: none` and
`terminal: false`; the ledger and the declarations are inputs and make no
evidence claim. Nothing defined here is ADR 0057 terminal evidence, raises an
evidence rank, changes a capability-policy decision, or updates Golden bytes.
The ADR 0057 plan, its schemas, the parity workflow contract and the terminal
comparator keep their own authority unchanged.

## Modes

| | Rolling | v0.9.16 1.x |
| --- | --- | --- |
| Purpose | release gate for every 1.x release | consistency check at the 1.1.13 final candidate, at the deferred 1.2.0 release-approval milestone executed before the 1.2.2 release, and before RO-1 is decided (board decision 64) |
| Baseline | previous published stable release, rebuilt from its annotated tag | the plan's v0.9.16 tag, built with the amendment's baseline executor |
| Input authority | active canonical Golden and capability policy at the candidate commit, through the ledger | the plan's `canonicalInputAuthority`: policy and manifest read only at its `repositoryCommit` |
| Compared set | the ledger's scenarios | the plan's 37 routes with canonical input; its 27 `currentlyMissingRouteIds` are reported `not-covered` |
| TP-work routes | compared directly on complete outputs | the plan's proof kinds: exact output, or the TP-prefix transitive proof for the plan's transitive routes |
| A difference is acceptable when | a release declaration entry matching it exactly is committed | an exact plan or amendment row is reproduced exactly |
| Declaration | required | not read |
| Amendment | not read | required |
| Result | gate `clear` or `blocked` | `consistent`, `inconsistent` or `invalid`, never `pass` |

Both modes build the candidate from the exact candidate commit with the same
recipe, stage inputs the same way, apply the same per-side safety and compare
bytes the same way. They differ only in the rows above. A mode never reads the
other mode's disposition documents.

## Rolling mode

### Universe and coverage ledger

The universe is every route of the candidate's capability policy whose
authoring is `available` and whose publication is `supported` or `candidate`
(1.1.12 board decision 64). Hidden `internal` and `test-only` routes are
outside it. The universe is not the compared set.

The coverage ledger lists every scenario the rolling mode runs. A scenario is
one comparison unit, identified by
`<icId>:<workflowId>:<icCountVariant>:<mapVariant>:<evidenceCaseId>`, and
declares:

- the route key and its current `routeId`; route identities are stable, so
  the ledger never pins capability fingerprints, which change with reviewed
  profile updates and are reported as information only;
- `binding`: `route-evidence` when the active Golden route evidence names
  `evidenceCaseId` for the route, or `case` when a Golden case is used
  without a route-evidence link (the AB Normal cases, and the NT51950 Hiway
  and Display-OSD cases);
- `inputCaseId`, the direct case whose artifacts are the inputs (the source
  case of an alias);
- `planRouteId`, the ADR 0057 plan route the scenario came from, or `null`;
- the CLI profile and selection option and token, and for CtrlRAM Replace the
  base kind (`tp-input`, or `standard-merge` with the precursor map variant).
  These are declared facts; the comparator never infers them from a map or
  profile name;
- the ordered inputs, each with order, CLI slot, artifact id, size and
  SHA-256, and an `inputRevision` that starts at 1.

The ledger also holds decision 12's debt set: exactly the 27 route ids of
`canonicalInputAuthority.currentlyMissingRouteIds` in the ADR 0057 plan
(1.1.12 board decisions 12, 47 and 60). The exemption follows those exact ids
and passes to no successor, renamed or new route. `acceptedGaps` lists each
other universe route that is compared by no scenario, with the approving
version, the reason, the approval and its declaration entry.
`pendingAcceptedGaps` lists the universe routes still waiting for that
approval; while it is not empty, no run is a report of record.
`retiredScenarios` keeps every scenario removed from comparison, with the
retiring version, the reason, the approval and its declaration entry.

Rules:

1. **Partition**: every universe route is compared by at least one scenario,
   or else is in the debt set, an accepted gap or a pending gap; no route is
   in two of the last three.
2. **Completeness**: every universe route whose active route evidence names a
   case has a `route-evidence` scenario for that case.
3. **Monotonicity**: every scenario active in the baseline tag's ledger is
   still active with the same `inputRevision`, or its retirement or input
   revision is a coverage disposition declared by this release. A Golden input
   change updates the scenario's pinned inputs in the same pull request and
   increments its `inputRevision`.
   Every accepted-gap row must have `approvedInVersion` equal to the release
   it covers. An earlier release's approval carries over to no later release,
   successor or renamed route. A new or renewed gap row is an `accepted-gap`
   coverage change, declared with this release's entry, approval and CHANGELOG
   id (1.1.12 board decision 60, 1.1.13 decision 96, 1.2.x decision 251).
4. A scenario compares only what its inputs make comparable; it never makes a
   route Golden-verified.

The initial ledger has 39 scenarios over 37 routes: the 37 scenarios of the
1.1.12 v0.9.16 alignment, rebound from the plan's route identities to the
current routes (one declared rename, `nt51950-ab-merge-512k` to the single
axis of `nt51950-ab-merge-maps`), and the NT51950 Hiway and Display-OSD cases
(board decision 64). The 11 candidate routes without canonical inputs are
pending gaps until the owner's 1.1.13 approval.

### Baseline

The rolling baseline is the previous published stable release: the highest
annotated `vX.Y.Z` tag below the candidate `VERSION` that has a complete,
published GitHub Release. Its peeled commit must be an ancestor of the
candidate commit. Before R-5, the commander supplies the file through
`rolling --published-release-inventory FILE`, validated against
`predecessor-published-release-inventory-v1.schema.json`. The comparator only
reads duplicate-rejecting JSON; it never calls `gh` or accepts a token.
The declaration names the same tag and tag object.

The commander's GitHub App inventory producer must read every release page
before filtering stable releases, state root `complete: true` and `pagesRead`,
and verify each row's completeness before stating `complete: true`. It must
not substitute a default result limit for complete enumeration or hide a newer
stable release whose checks are incomplete. Credentials are removed before
the wrapper ends and never enter the comparator process. Offline completeness
is a producer statement with provenance; SHA-256 cannot prove that no page was
omitted. Release-owner review checks the pagination and completeness evidence.

The validator refuses a wrong repository, duplicate id or tag, a non-`vX.Y.Z`
tag, missing publication time, draft or prerelease, `complete` other than true,
and publication later than `collectedAtUtc`. Missing or invalid inventory in a
formal run is `PREDECESSOR_BASELINE_INVALID`. Selection starts from the highest
published stable version below the candidate, then requires that exact local
annotated ancestor tag; a missing or invalid tag never falls back to an older
release. A diagnostic run records the given tag without asserting publication.

Rolling reports carry `publishedInventory: {rawSha256, factsSha256}`, which may
be null only in diagnostic runs. `rawSha256` binds the complete file bytes;
`factsSha256` is the JCS SHA-256 of `{repository, releases}`, with the complete
release rows sorted by numeric version, excluding collection time and pagination.

### Inputs

Inputs come only from the active Golden at the candidate commit. The
comparator computes the authority descriptor from Git (commit, canonical root
tree, manifest blob, size, SHA-256), materializes the snapshot through the
existing ADR 0057 materializer and validates it once with
`scripts/canonical_golden_validation.py`. Each input is admitted by exact
path, size and SHA-256 from the ledger; any difference is
`PREDECESSOR_INPUT_INVALID`.

### Outcomes

For every scenario the primary output is compared completely. TP-work
scenarios are compared directly. CtrlRAM full-flash scenarios build their base
on each side with that side's own Standard Merge, and both base identities are
compared; a base difference is a difference even when the outputs are equal.

| Outcome | Meaning |
| --- | --- |
| `equal` | both sides produced the same complete output, and any bases are equal |
| `different` | both sides produced outputs, and the outputs or the bases differ |
| `baseline-rejects` | the baseline rejected the input with a typed product rejection; the candidate produced an output |
| `candidate-rejects` | the candidate rejected the input with a typed product rejection; the baseline produced an output |
| `both-reject` | both sides rejected the input with typed product rejections |
| `invalid` | a crash, timeout, tool, report, per-side safety, custody, identity or environment failure |

A route that no scenario compares is reported in the coverage section with its
reason (`debt-set`, `accepted-gap` or `pending-gap`), not as an outcome.

### Declaration

Each release commits `predecessor-comparison-declarations/<version>.json`
before its report of record is produced, even when its entry list is empty.
It names the candidate version, the baseline tag and tag object, and the raw
SHA-256 of the ledger. Each entry has an id `RP-<version>-NN`, which is also
its release-note token, and one kind.

`expected` binds each side's outcome: its output and its Standard Merge
precursor identities (size and SHA-256, or `null` when the side has none),
and for a rejection its stopping stage and issue codes, the sorted distinct
codes of its `error` issues (an output side has none). `differences` binds
each compared scope on its own: `output` for the primary output and
`precursor` for the precursor, each `null` or an exact difference with its
byte count, range count, range-list digest, the complete range list (which
may be longer than the report's first 32 ranges) and an attribution that
covers exactly those ranges. A precursor difference therefore requires both
precursor identities, and a precursor-only change is declared with
`differences.output` null; `precursor-carried` names a cause, never a
substitute for the precursor's own identities and ranges.

| Kind | Required content |
| --- | --- |
| `byte-difference` | the scenario ids, `expected` with both outputs, and `differences` with an output difference, a precursor difference or both |
| `baseline-rejects` | the scenario ids, `expected` with the baseline stage and issue codes and the candidate output, and a precursor difference when the precursors differ |
| `candidate-rejects` | the scenario ids, `expected` with the baseline output and the candidate stage and issue codes, a precursor difference when the precursors differ, and exactly one of a route withdrawal or a `knownIssue` |
| `both-reject` | the scenario ids, `expected` with both stages and issue codes, a precursor difference when the precursors differ, and a `knownIssue` |
| `scenario-retired` | the retired scenario ids; the ledger records the retirement |
| `input-revision` | the scenario ids whose inputs were replaced; the ledger records the new revision |
| `accepted-gap` | the accepted route ids and no scenario; the ledger records the gap |

Every entry carries the owner's approval as firmware owner: the board
decision, which is the authority, the role `firmware-owner` and the date. The
declaration cites the decision exactly; this citation is the approval form of
a release declaration (board decision 64). It does not replace the owner's exact-head firmware-owner and release-owner approval that an
R3 pull request needs, such as a change to the 1.x amendment (ADR 0080 item 7). The release decision stays with the owner as release owner.
A newly rejected input ships only as a known non-blocking issue approved for
that release; a declared issue never overrides a P0 or P1 bug, a required
Golden case or a protected check (board decision 57).

Each entry's id appears in the candidate version's CHANGELOG section under the
product change that caused it; the comparator checks that the id is present,
and review checks the text.

### Report of record and gate

The declaration and its CHANGELOG lines are committed first. The report of
record then comes from a formal run under the clean-settings policy against
the exact commit being released (board decision 59). It binds the candidate
commit and tree, the baseline tag and both executor identities, the
comparator script SHA-256, the SHA-256 of every contract it applied, and the
ledger and declaration digests. A report from another commit counts only
under a separate, exact and reviewed transfer contract; there is no automatic
fallback to any reduced comparison (board decision 58). Until the release
workflow runs the comparison, the gate is procedural (board decision 61).

Runs with identical source, admitted executors, inputs, disposition documents, published-release facts, settings facts and semantic results must reproduce deterministicSha256. The digest does not identify the complete serialized report or a workflow run. The release-workflow integration (WS-GOV batch R-5) binds that
digest into its run-scoped evidence envelope together with the run, attempt
and artifact identities. Until then the report alone cannot tell evidence of
another run on the same source apart, and this format makes no claim to reject
every wrong-run artifact.

The gate is `blocked`, with the codes of `predecessor-comparison-v1.json`,
when:

1. an outcome other than `equal`, or a coverage change, has no matching
   declaration entry (`PREDECESSOR_UNDECLARED_CHANGE`);
2. an entry is not reproduced exactly (`PREDECESSOR_STALE_DECLARATION`);
3. any scenario is `invalid`, with its own code;
4. the partition, a pending approval or an undeclared coverage change is open
   (`PREDECESSOR_COVERAGE_UNDISPOSED`), or completeness fails
   (`PREDECESSOR_COVERAGE_INCOMPLETE`);
5. an id is missing from the CHANGELOG (`PREDECESSOR_RELEASE_NOTE_MISSING`);
6. the baseline is not the declared previous stable release
   (`PREDECESSOR_BASELINE_INVALID`);
7. the report does not come from the exact release source or does not bind
   the required digests (`PREDECESSOR_SOURCE_MISMATCH`);
8. a formal run is requested while an interface below is proposed or pending
   (`PREDECESSOR_CONTRACT_PENDING`).

A report of record has `formal: true` and gate `clear`, which the report
schema only admits with a declaration digest, both per-user settings files
absent before and after, no pending gap, no failure, no `invalid` scenario and
a declaration entry for every scenario that is not `equal`.

## v0.9.16 1.x mode

For candidate version 1.2.2, the report of record uses milestone "1.2.0-release-approval", the deferred milestone of 1.1.12 board decision 201 and 1.2.x board decision 250. The report must be formal and consistent before 1.2.2 is released; all existing plan and amendment obligations remain mandatory.

The mode is a historical consumer of the ADR 0057 plan, as ADR 0057 permits.
It materializes the plan's canonical input authority with the ADR 0057
materializer, selects the plan's 64 routes from the plan's pinned policy, and
resolves each route's inputs and CLI arguments with the ADR 0057 owners
(`resolve_canonical_route_input`, `_cli_arguments`). The candidate is the
exact commit under test built with the 1.x recipe; the plan's candidate
authority, package admission, protected build and attestations are never
read. The plan's candidate-side capability fingerprints bind its v1.0.0
candidate, so each side's compiled fingerprint and map id are information
only; the plan route id and fingerprint identify the plan row.

Each route gets one proof kind and one result:

| Proof kind | Routes | Consistent when |
| --- | --- | --- |
| `exact-output` | a bound plan route with no row below | both sides produce outputs of equal size and bytes |
| `exact-output-with-approved-semantic-correction` | a route named by the plan's `approvedSemanticCorrections` row or by an amendment row | `compare_approved_semantic_correction_payloads` passes against that exact row: both hashes, the byte count and the complete range list |
| `tp-prefix-transitive` | a plan `transitiveRoutes` route with canonical input (4 today) | the full route is consistent and `compare_transitive_payloads` passes: the candidate TP output equals the candidate and the baseline full-output prefix, and the candidate full-output tail equals its base |
| `canonical-binding-not-applicable-to-v0916` | a route named by an amendment `baselineNotApplicable` row | the run reproduces the row: the same binding, both precursor identities, the baseline's rejecting stage and issue codes, and the candidate output |
| `not-covered` | the plan's 27 `currentlyMissingRouteIds` | never run; neither consistent nor inconsistent |

A route is `inconsistent` when it runs but does not meet its proof: differing
outputs, a rejection or a failed transitive check that no exact plan or
amendment row explains (`PREDECESSOR_UNAPPROVED_DIFFERENCE`), or a row that is
not reproduced exactly (`PREDECESSOR_AMENDMENT_MISMATCH`). A route is
`invalid` when a side fails for a reason that is not a product result, with
the codes of the shared execution failures. The report is `consistent` only
when every run route is consistent; any `invalid` route makes it `invalid`,
otherwise any `inconsistent` route makes it `inconsistent`. The amendment's
plan binding is checked before any route runs; a mismatch stops the mode with
`PREDECESSOR_AMENDMENT_MISMATCH`. Rows are never widened into patterns, and a
row that no longer reproduces needs a new owner decision.

For a `tp-prefix-transitive` route, the proof can run only when the candidate
TP output and both outputs of the plan's full route exist. A runnable proof
is never missing, including on an `invalid` route. If the proof cannot run,
`transitive` is `null` and no computed proof is supplied: a typed rejection
makes the route `inconsistent` with `PREDECESSOR_UNAPPROVED_DIFFERENCE`, and a
shared execution failure makes it `invalid` with its execution failure code.
A `consistent` route carries a passing proof and a consistent full route.
The schema expresses the candidate-side structural conditions and requires a
passing proof for `consistent`; the single validator checks availability
against the full route named by the plan, the TP length and computed evidence.

## Shared execution

### Executors

Every CLI is built by the comparator itself from exact Git authority: the
rolling baseline from its annotated tag, the v0.9.16 baseline with the
amendment's executor, and the candidate from the exact candidate commit, each
in a fresh detached Git worktree. The 1.x recipe is the locked restore and
Release build of `predecessor-comparison-v1.json` (with
`ContinuousIntegrationBuild` and `PathMap`). A pre-existing `bin` or `obj`
path, a dirty tree, a restore that rewrites a lock file, or a failed build is
`PREDECESSOR_EXECUTOR_INVALID`. The source's own `NuGet.config` puts the
restored packages in the top-level folder `.packages` of the worktree, which
Git ignores (`executor.restorePackageFolder`). That folder must be absent
before restore; its files are restore products, identified by the lock bytes,
and are not source files of the v0.9.16 source comparison below. The CLI apphost hash alone never identifies an
executor; the runtime-closure digest does.

Compiler-host pinning and [baseline executor v2](v0916-baseline-executor-v2.md)
are in effect together under decisions 63 and 79. Every comparator-built
executor preflights the selected dotnet installation with `--list-runtimes`
for framework `Microsoft.NETCore.App` exactly `10.0.11` and `--info` for the
same host's `x64` architecture. Restore/build alone receive
`DOTNET_ROLL_FORWARD=Disable`; build appends `-p:UseSharedCompilation=false`
and `-nodeReuse:false` and `-p:RuntimeFrameworkVersion=10.0.11` once; the
`RuntimeFrameworkVersion` value must equal `compilerHost.requiredRuntime.version`.
The `-p:RuntimeFrameworkVersion=10.0.11` argument is an extension of decision
79 that waits for the release owner's decision. `compilerHost.boardDecisions`
records the decision-79 compiler-host pinning; it does not authorize this extension.
Every path restores the previous environment.
Missing runtime or a wrong architecture refuses with
`PREDECESSOR_EXECUTOR_INVALID`, without installing or selecting another host.

Since 1.1.13, the 1.x build runs the framework-dependent prebuilt profile
catalog generator, which requests runtime `10.0.0` and cannot start while
roll-forward is disabled. Fixing the runtime framework version to the pinned
patch lets it run on `10.0.11`. The self-contained CLI already takes runtime
`10.0.11` from the SDK, so its closure is unchanged; the v0.9.16 closure pin
is reproduced with the property: 431 files, 88367164 bytes, SHA-256
`18da112302672766db191872d6b7973ca05effa257293b09161904545eb2c3b0`.

The comparator enables Git's `core.longpaths` for its own Git commands through
Git's environment protocol (`GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_n`,
`GIT_CONFIG_VALUE_n`) and restores the environment afterwards. The Golden
tree has paths of about 200 characters, so a worktree under a temporary root
passes the Windows limit of 260; no configuration file is changed, and
identities come from Git objects.

For a failed executor process or capture, stderr names the command's first
three arguments and shows the last 30 lines of each captured stdout/stderr
stream, decoded as UTF-8 with replacement; these diagnostics never enter the
report, and the existing refusal is preserved.

`scripts/predecessor_pdb_probe.py` reads every managed assembly declared by the
built CLI `.deps.json` project graph in the measured closure. It parses PE
entry type 17, inflates `MPDB` raw deflate and reads portable metadata streams
`#Pdb`, `#~`, `#GUID` and `#Blob`. CustomDebugInformation kind
`B5FEEC05-8CD0-4A83-96DA-466284BB4BD8` supplies null-terminated UTF-8 options.
`runtime-version` must equal the pinned servicing version; `compiler-version`
is the compiler identity, while `version` is the options format version.
Empty graphs, missing or ambiguous PDB/options, corrupt metadata and mixed
or unpinned hosts refuse the executor. The probe starts no process.

This R3 activation requires the firmware owner and the release owner's
exact-head approval of the last push, with byte/Golden, write-range and
release-policy evidence. R35-09 rehearses actual builds before formal reports.

The 1.x identity is measured; the v0.9.16 v2 identity is pinned in its contract.
**Not implemented yet:** comparing the rebuilt rolling baseline identity with
the candidate identity recorded in the baseline release's own report, reporting
a mismatch, and making it a failure once local and hosted builds are shown to
match. No delivery item in the [1.2.2 handoff](../handoff/1.2.2/README.md)
explicitly owns this follow-up.

| Contract `recordedIdentity` | Report `executor` member | Value |
| --- | --- | --- |
| `authorityTrees` | `authorityTrees` | the Git tree ids of `external-tools`, `profiles`, `src` and `tools/crc-worker` at the commit |
| `cliSha256` | `cliSha256` | SHA-256 of the built `cliAssembly` |
| `commit` | `commit` | the built commit (the peeled commit of a tag) |
| `compilerHost` | `compilerHost` | verified `runtimeVersion`, `compilerVersion` and positive `verifiedAssemblyCount` |
| `lockFileSetSha256` | `lockFileSetSha256` | the lock-file set digest below |
| `resolvedSdkVersion` | `resolvedSdkVersion` | the SDK version `global.json` resolves in the worktree |
| `runtimeClosureSha256` | `runtimeClosureSha256` | the ADR 0057 closure digest: JCS SHA-256 of `{path, size, sha256}` for every file under `runtimeClosureRoot`, by relative path |
| `tagObject` | `tagObject` | the annotated tag object, or `null` for an untagged candidate |
| `tree` | `tree` | the commit's root tree |

`lockFileSetSha256` is the JCS SHA-256 of the array of `{path, size, sha256}`
objects, one for every Git-tracked file matching `src/*/packages.lock.json`
at the built commit, taken from the Git blob bytes (so checkout line endings
cannot change it), sorted by path in ascending ordinal order. For 1.x any lock change is `PREDECESSOR_EXECUTOR_INVALID`. For v0.9.16 exactly the seven v2 rewrites are required after restore and build; all other locks stay equal to their blobs. The digest still uses all eight original Git-blob locks, never post-restore bytes.

### Inputs, processes and environment

Every process receives its own read-only staged copy of its inputs, hashed
before and after; the repository Golden file is never a process input, and
firmware inputs and outputs never enter Git or an uploaded artifact.

A report is compared with its staged inputs by position. A CtrlRAM Replace CLI
sorts its bindings by slot id (ordinal) whatever the order of its arguments,
so its report lists `reference-base` first and the replacements by name. The
comparator therefore stages, passes and expects CtrlRAM Replace inputs in that
order, also when the reviewed binding names them in another one. Merge inputs
keep the binding order.

Each CLI process gets fresh comparator-owned temporary directories under a
temporary root of at most 64 characters, and a working directory inside its
staging root. The legacy Combiner fails when an argument path reaches 260
characters, which the CLI's temporary layers reach from a root of about 111
characters (`BUG-20260926-legacy-combiner-long-path`); a longer root is
refused before any process with `PREDECESSOR_ENVIRONMENT_INVALID`.

A 1.x or v0.9.16 CLI finds its external tools by looking for a directory named
`external-tools` in its base directory and then in each parent; per-user
settings take no part. Each CLI process therefore gets, beside its staged
runtime closure, a read-only copy of the `external-tools` tree of its
executor's commit, taken from the Git blobs
(`executor.externalToolStaging`). The copy is held under the same custody as
the runtime closure and is hashed before and after the process; a change is
`PREDECESSOR_EXECUTOR_INVALID`. The directory is created even when the commit
has no tool file, so the search never reaches a directory outside the staging
root. The closure digest and the `authorityTrees` identity are unchanged: the
tree is already identified by its Git tree id.

A side is a **typed product rejection** only when a Preview or Build process
exits with a nonzero code and a written report that carries at least one
`error` issue, and no issue code of the report is a process failure listed in
`typedRejection.processFailureIssueCodes`. The list holds
`external-tool.process.failed` and `external-tool.process.start-failed`, at
any severity. The P-0.5 spike saw the former inside written reports when an
argument path reached 260 characters. A process that crashes,
times out, exits without a report or reports a process failure is
`PREDECESSOR_PROCESS_FAILED`; it is never a rejection and can never be
declared or approved.

The CLI reads `event-buffer-format.v1.json` and `toolchain-runtime.v1.json`
from the local application-data folder. The P-0.5 spike confirmed that
`LOCALAPPDATA` or `APPDATA` alone does not redirect that folder and that
`USERPROFILE` does only on some machines, so the comparator never redirects
it. A formal run requires both files to be absent before and after the run; a
run with either file present is diagnostic only, and a file that changes
during any run is `PREDECESSOR_ENVIRONMENT_INVALID` (board decision 59). The
check reads the two files, not the folder: other programs of the same user can
write to the folder during a run, as the P-0.5 spike observed. A 1.x CLI that
refuses a malformed settings file exits without a report and names its code
only on stderr (the spike saw `AB_FORMAT_CONFIGURATION_INVALID` and
`capability.readiness.runtime-dependency-blocked`); the side is invalid, never
a rejection, with `PREDECESSOR_ENVIRONMENT_INVALID` when a settings file is
present and `PREDECESSOR_PROCESS_FAILED` otherwise.

### Per-side execution safety

Each side must pass per-side execution safety before its result counts. The
owners of the shared sequence, compiled-authority and range checks are the ADR
0057 functions in `scripts/v0916_parity_certification.py`, reused without a
second implementation. The comparator's staged-command identity check
(`_executed_command_failures`, item 4) and processor write-range audit
(`_processor_write_audit_failures`, item 5) live in `scripts/predecessor_validation.py`:

1. **Independent compiled authority.** A typed Preview of the same executor
   with the same staged inputs and arguments produces the compiled operations
   (the authority). `validate_report_sequence` requires the Build report's
   operations to be the Preview's, in order, and its mutations to follow that
   order, and `validate_report_projection_against_compiled_authority` requires
   the Build projection's compiled operations to equal the authority's
   exactly and every mutation to lie inside its own operation. A report that
   widens its own operation or processor ranges together with its mutations
   is therefore rejected even though it is self-consistent. A processor's
   writes are held to the authority's allowed write ranges by the audit of
   item 5, because its written mutation row is its whole operation target;
   the comparator asks the range function for that with
   `audited_processor_writes`, and the terminal path keeps its default. A written
   mutation row names its operation and carries no sequence, so the validator
   gives each row the sequence its own report declares for that operation
   before the order check; a row that names an undeclared operation fails it.
2. **Report ranges.** `validate_semantic_report_ranges` checks that every
   range is half-open, inside its address space's capacity and contained in
   its operation, and that a processor's read ranges and its write ranges do
   not overlap among themselves. Operation targets are compared inside their
   own address space, in strictly increasing integer `sequence` order: a target may overlap an earlier
   target only when its operation declares the `ReplaceExisting` overlap
   policy, as a map does when it copies the DP container and writes the TP
   over part of it. `ReplaceExisting` with no earlier overlapping target in
   that address space is refused, as is any other overlap. The comparator asks for
   this rule with the function's `declared_overlap` option; the ADR 0057
   terminal path keeps its default, which refuses every overlap.

   A report declares no capacity for the work address spaces of AB Merge,
   `tp-b-work` and `ab-combiner-work` (`perSideSafety.writtenReportRules`,
   1.2.x board decision 261). No capacity is invented for them: a range in
   such a space must lie inside one range the same side's Preview declares
   there, as an operation source or target or a processor read or write
   range. The Preview's own ranges are the declaration; a Build range
   outside them, and a range in any other address space that is neither an
   input nor `output-image`, is refused. A Preview's `output-image` bound is
   the extent of its targets in `output-image` only.
3. **Capture.** The report agrees with the captured inputs and output, and a
   side that produced an output carries no `error` issue. A Preview, or a run
   that stops before it writes, describes the output it would write with
   `Committed: false` and leaves no file; that description is not a file
   identity and is not compared with a capture. A described output without a
   file and with any other `Committed` value, or an output file whose report
   is not `Committed: true`, is `PREDECESSOR_REPORT_INVALID`.
4. **Executed commands** (decision 261). The executable of every executed
   command must be one of the external tool files the comparator staged for
   that process and checked by hash before and after it; the path text alone
   is not trusted. Its working directory must lie below the temporary
   directory the comparator created for the process. Its file arguments
   (the absolute paths among its arguments) must lie in the working directory
   or below it, and no path may step back with `..`. `Arguments` must be a list
   of strings. Any other argument is a
   plain token: one that could name a file elsewhere (a path separator, a
   drive colon, `.` or `..`, a reserved device name with or without an
   extension in any case, a leading `@`, or any `%`) is refused. A repeated command is
   kept, and the Build's commands are compared with the Preview's in the
   order they appear. A processor operation without an executed command is
   still refused.
5. **Write-range audit of an external processor** (decision 261). A written
   mutation row of a processor is its whole operation target, so the audit
   reads the ranges the report lists under `OutputDifferences`, and only the
   ranges: the content previews of those rows are never read or kept. Each
   listed range must lie inside one write range that the Preview allows a
   processor in `output-image`. A processor whose mutation row reports
   changed bytes must have a listed range inside its own allowed write
   ranges; a report that lists none for it is refused, and so is a processor
   that changed bytes in another address space, whose changes the output
   differences cannot show. The comparator computes no byte difference of its
   own for this audit.

   **Current refusal is stricter than decision 261:** the audit holds every
   `OutputDifferences` row to processor write ranges, including a non-processor
   row such as `DeclaredReplacement`. It does not filter rows by their producer.
   The product's `CompositionRunService.CreateOutputDifferences` returns an
   empty list for every Merge and whenever output and reference lengths differ.
   The open work-space-processor evidence question therefore extends beyond
   NT51950, and these refusals remain until the firmware owner decides it
   ([bug record](../handoff/bugs/BUG-20261002-predecessor-work-space-processor-lists-no-output-difference.md)).

A failure is `PREDECESSOR_REPORT_INVALID` and makes the scenario or route
`invalid`. The versioned report reader only converts a report version's
format into the normalized projection these functions read. It may not relax,
skip or reorder these checks. The reader and the report and declaration
schemas and both executor interfaces are in effect
([Interface status](#interface-status)).

#### Report reader v1

`scripts/predecessor_report_reader.py:read_cli_report` takes an already-loaded
written JSON report and the executor's declared `report_version` (`v0916` or
`1x`), never inferred from the payload. JSON is loaded by the ADR 0057
duplicate-rejecting loader. The returned `ReadReport` carries:

- `reader_version`: `cli-v0916-v1` or `cli-1x-v1`;
- `projection`: `compiledOperations`, `compiledMutations` and
  `compilationFingerprint`, consumed by the existing per-side checks;
- `context`: report identity, input/output identities, times, composition
  kind and optional `mapId`, the executable and working directory of each
  executed command, and the range of each output difference, for the
  caller's capture, command and audit checks;
- `issues`: each report issue's exact code and lower-case severity (`error`,
  `warning`, `info` or `unspecified`), with `source: report`;
- `unknown_members`: sorted JSON pointers naming unknown optional members,
  without their values. JSON pointer escaping uses `~0` and `~1`.

The required top-level members are the ADR 0057 report members; `MapId` is
optional on both formats and is `null` in the context when absent. No map id
is invented for v0.9.16. The 1.x extensions `AbMergeFormat` and
`SourceEnvelope` are unknown optional members recorded by name, without
becoming authority. Unknown members in input, output and issue objects are
likewise recorded and excluded. An unknown member in an operation or mutation
object, or in a range, provenance or command object inside one, is refused
with `PREDECESSOR_REPORT_INVALID`: those rows carry write authority, and the
ADR 0057 normalizers' exact-member rule is not relaxed. Known presentation
members (`OriginalFileName`, `FileName`, `Message` and an issue's
`OperationId`) are ignored; `Validations` and `OutputNaming` remain
unconsumed and supply no authority. Of an `OutputDifferences` row only
`Range` is read, as an exact `Start`/`Length`/`EndExclusive` triple; a row
without one is refused, and no other member of the row, in particular no
content preview, is read or kept.
Required members are never supplied by an optional extension.

The reader reuses `normalize_raw_operation` and `normalize_raw_mutation`, the
ADR 0057 public aliases, without copying their implementations. It asks the
operation normalizer for the command shape a CLI writes (`written_commands`):
the executable stands below a directory named `external-tools` and is
identified from that component, and absolute arguments stand in the working
directory or below it. The ADR 0057 terminal path keeps its default. It preserves
operation/mutation/input/issue order, named address spaces and half-open
ranges. It does no file, process or Git access and makes no semantic verdict.
A malformed format, unsupported version or absent report is
`PREDECESSOR_REPORT_INVALID` at this format boundary; the process runner owns
the contract's classification of an absent report as a process/environment
failure. In particular, `AB_FORMAT_CONFIGURATION_INVALID` or
`capability.readiness.runtime-dependency-blocked` appearing only on stderr
does not create a report or a typed rejection. Stderr issues, when captured,
stay separate and cannot satisfy a written report's `error` requirement.

### Comparison and attribution

Outputs are compared in the output file's offset space: size, every byte and
SHA-256. When the sizes differ, the bytes beyond the shorter output count as
different. Differences are reported as half-open ranges `[start,
endExclusive)` with the byte count, the JCS SHA-256 of the complete range list
and at most the first 32 ranges. Differences between versions in operations,
issue codes, resolved map id or capability fingerprint are recorded as
information beside the byte result; they never pass or fail a scenario or
route on their own and are never normalized.

Every declared difference and every approved correction row is explained
range by range with one of four mechanisms: `writes-different-bytes`,
`stopped-write-preserved-bytes` (the candidate no longer writes a range, which
keeps its base or reference bytes), `precursor-carried` (a change that enters
through the Standard Merge precursor) and `derived-field` (a CRC or similar
field that changes with the content it covers). Each attribution states
whether its cause is `supported-by-cited-evidence`, with the evidence, or
`not-independently-verified`; an unverified cause accepts only the exact
bytes, never a wider range or a pattern. Per-side safety is a separate check,
and the union of both sides' write ranges is not an attribution.

## Validation

The schemas hold every rule that can be decided inside one document, including
the conditional ones: a formal report needs both settings files absent; a
clear gate needs no failure, no pending gap, no `invalid` scenario, a
declaration entry for every scenario that is not `equal`, and, for a formal
report, a declaration digest; each outcome and proof kind needs the side
results, comparison and disposition that define it; a side that produced an
output has only zero exit codes, unchanged inputs, no `error` issue and no
process-failure code at any severity; every process of a rejected side
finished without a timeout and wrote a report, and the report carries an
`error` issue and no process-failure code; a precursor
comparison needs both precursor identities; each declaration entry kind needs
its expected outcomes, output or precursor differences with their
attributions, known issue or withdrawal. The repository validates the schemas
and these relations with its Draft 2020-12 engine (JsonSchema.Net) in the .NET
contract tests, including a counterexample for each relation.

Everything that spans documents belongs to the comparator's single semantic
validator (P-2), which fails closed:

- report and source: candidate commit and tree, baseline tag object and
  peeled commit, the contracts', ledger's and declaration's digests at the
  release source, and the comparator script digest;
- coverage: the partition, completeness and monotonicity against the
  baseline tag's ledger, the `notCovered` list and the counts;
- declaration, ledger and report: every entry's scenarios exist; every
  outcome that is not `equal` and every coverage change matches exactly one
  entry, reproduced exactly (output and precursor sizes and hashes, each
  scope's complete computed range list, byte count and range-list digest,
  stages, issue codes), with every attribution covering exactly its scope's
  ranges; the report's ranges are the first 32 of that complete list; ids are
  unique; each id is in the CHANGELOG section;
- inside a report where a schema cannot compare values: equal outputs for
  `equal`, sorted disjoint ranges whose lengths sum to the byte count, the
  range-list digest, and `rangesTruncated` exactly when there are more than
  32 ranges;
- the v0.9.16 1.x mode: the amendment's plan binding, the plan's 64 routes,
  each route's proof kind from the plan and the amendment, and each row
  reproduced exactly.

The repository's Python lane has no Draft 2020-12 validator: `jsonschema` is
optional in `scripts/validate_repository.py` and absent from the CI
dependencies. The comparator batch (P-2) therefore either adds a reviewed
validator dependency or keeps schema validation in the .NET contract tests and
implements the semantic validator directly; it never grows a hand-written
schema subset.

## Canonical digests

JSON-derived digests use the RFC 8785 JCS rules of the ADR 0057 parity
contract and its existing implementation (`canonical_json_sha256`).
`rangeListSha256` is the JCS SHA-256 of the complete list of differing ranges
in ascending order. Ledger, declaration, amendment and contract identities are
SHA-256 values of the raw file bytes.

deterministicSha256 is the RFC 8785 SHA-256 of the report after removing only the members listed below. All other members, values and array order are retained. Excluded members remain required and validated run evidence; exclusion does not relax any execution or report check.

The exact excluded JSON pointer patterns (`*` selects each array member) are
also declared by `comparison.deterministicDigestExcludedPaths` in
`predecessor-comparison-v1.json`:

- `/deterministicSha256`
- `/environment/temporaryRootLength`
- `/scenarios/*/baseline/processes/*/stdoutSha256`
- `/scenarios/*/baseline/processes/*/stderrSha256`
- `/scenarios/*/baseline/processes/*/report/size`
- `/scenarios/*/baseline/processes/*/report/sha256`
- `/scenarios/*/candidate/processes/*/stdoutSha256`
- `/scenarios/*/candidate/processes/*/stderrSha256`
- `/scenarios/*/candidate/processes/*/report/size`
- `/scenarios/*/candidate/processes/*/report/sha256`
- `/routes/*/baseline/processes/*/stdoutSha256`
- `/routes/*/baseline/processes/*/stderrSha256`
- `/routes/*/baseline/processes/*/report/size`
- `/routes/*/baseline/processes/*/report/sha256`
- `/routes/*/candidate/processes/*/stdoutSha256`
- `/routes/*/candidate/processes/*/stderrSha256`
- `/routes/*/candidate/processes/*/report/size`
- `/routes/*/candidate/processes/*/report/sha256`
- `/gate/failures/*/detail`
- `/failures/*/detail`
- `/publishedInventory/rawSha256`

Null sides and null process reports remain null. Report presence, reader version,
unknown members, stage, exit code, timeout, input custody, issues, settings
hashes, executor identity, output and precursor hashes, ranges, informational
values, inventory facts, gate and result are retained. No general removal by
member name is permitted.

## Interface status

| Interface | Status | Until it is in effect |
| --- | --- | --- |
| declaration schema | in effect (R35-02) | — |
| report schema | in effect (R35-02) | — |
| report reader | in effect (R35-01) | — |
| compiler-host pinning | in effect (board decision 79) | runtime preflight and embedded PDB verification |
| v0.9.16 baseline executor | in effect through the amendment binding to v2 (board decisions 63 and 79) | exact source, recipe, lock rewrites and closure pins |

A formal run requested while any of these is not in effect fails with
`PREDECESSOR_CONTRACT_PENDING`.
