# v0.9.16 parity certification contract v1

`v0916-parity-certification-v1.json` is the immutable, payload-free plan for
the `v1.0.0` predecessor comparison. Its normative schema is
`v0916-parity-certification-v1.schema.json`.

The plan binds one exact canonical capability policy by path and SHA-256. The
comparator selects only routes whose authoring decision is `available`, whose
publication decision is `supported`, and whose workflow appears in
`includedWorkflows`. The selected denominator and per-workflow counts must
equal the plan. Duplicate route ids or capability fingerprints fail closed.

Active Golden is rolling: the canonical manifest plus canonical policy at the
exact candidate source commit are the one active reference. An annotated stable
tag freezes that snapshot, and the next owner-approved Golden change makes the
next candidate commit active. Historical tags, commits, and this v0.9.16 plan
are immutable observations, not active policy. Historical plan loading reads
both `policyBinding.path` and the canonical manifest from the declared
`canonicalInputAuthority.repositoryCommit`; it never falls back to current
worktree policy. The current canonical Golden validator at candidate HEAD is
the sole active-Golden validator. Candidate-generated output and input-only
evidence can never self-rebaseline expected output bytes or constitute terminal
parity.

Every selected route not listed in `transitiveRoutes` uses `exact-output`:
baseline and candidate outputs must have equal lengths and identical complete
bytes. `transitiveRoutes` is a closed exception list, not a pattern. Each row
binds the shortened route id and fingerprint, its selected complete-output
counterpart id and fingerprint, and a positive TP prefix length. The comparator
enforces:

1. the counterpart's ordinary exact-evidence row passes and its canonical row
   digest matches `fullEvidence.evidenceSha256`;
2. candidate TP output length equals `tpLength`;
3. candidate TP bytes equal `[0, tpLength)` of the candidate full output;
4. candidate TP bytes equal `[0, tpLength)` of the v0.9.16 full output; and
5. candidate full output `[tpLength, fullLength)` equals the candidate full
   base input over the same half-open range.

The eleven shortened routes are three NT51917, two NT51923, three NT51927, and
three NT51928 TP-only work images. Their declared 212 KiB or 240 KiB outputs
are proved only by the complete counterpart plus the prefix and immutable-tail
checks above; they are not shortened whole-file parity.

The operator provides a local run manifest conforming to
`v0916-parity-run-v1.schema.json`. It provides an explicit output root and
route requests, never caller-authored input identities or precomputed
receipts. Only the raw-pinned canonical input resolver can construct the typed
input authority consumed by capture. Exact routes provide one
ordered baseline/candidate execution pair; a transitive route provides only
its candidate TP execution and references the already-passed exact
`fullRouteId`. The comparator itself drives the verified CLIs, captures their
preview/build reports and outputs, and creates receipts conforming to
`v0916-parity-receipt-v1.schema.json`. Their report content conforms
to `v0916-parity-build-report-v1.schema.json`. Paths may be absolute or relative
to their manifest, but resolved paths must be regular files. Candidate ZIP,
SPDX SBOM, provenance sidecar, release notes, candidate manifest, and outer
checksum file are all explicit local artifacts; none is discovered from a
filename convention. These are the existing canonical six candidate files,
not a parity-specific package surface.

The published v0.9.16 Windows package has one desktop executable and no working
CLI: `--help` and `standard-merge --help` open the GUI without CLI output. It is
therefore retained only as immutable release provenance/reference, with ZIP
SHA-256 `e55687f9...03c84`; it is not misrepresented as the baseline execution
surface.

The formal predecessor executor is the source-built CLI from the exact
annotated-tag authority. `v0916-baseline-executor-v1.json` binds tag object,
peeled commit, source tree, clean-tree requirement, resolved SDK `10.0.303`, raw
`global.json`, every CLI dependency lockfile, the complete external-tool
inventory, exact locked restore/build commands, and the resulting CLI assembly
size/SHA-256. The plan binds that executor contract by raw size and SHA-256.
Before any route executes, the comparator resolves the declared annotated tag
to its exact tag object, checks the detached repository HEAD and tree, requires
no dirty paths, hashes every declared file, performs the
exact locked restore and no-restore Release build, and re-hashes the CLI
assembly. Each predecessor receipt binds both the raw executor-contract digest
and the CLI assembly digest. Dirty source, SDK/global/lock/tool/DLL drift or a
nonzero restore/build result fails `PARITY_AUTHORITY_MISMATCH`.
The executor-contract identity is always the SHA-256 of the exact raw contract
file bytes returned by the validated loader. A digest recomputed from parsed or
JCS-reserialized JSON is not equivalent and is rejected everywhere it appears.

The source baseline executor runs all 53 declared complete-output predecessor
scenarios independently. The candidate runs all 53 exact routes and all 11
shortened TP routes independently. Each shortened row may consume only its
plan-declared already-passing full counterpart; no representative case is
copied to an unrelated route. NT51920, NT51930, and NT51931 are outside this
selector. Existing reference outputs and the bilateral lab observations below
are feasibility evidence, not a substitute for the complete release run.

Execution inputs are not supplied by a caller and are never synthesized from
route shape. `canonicalInputAuthority` raw-pins the canonical Golden manifest
at implementation commit `1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220`, canonical-root tree
`0bd5df0f...8936`, manifest blob `d0dcae90...0dd`, size, and SHA-256. For each
selected route the resolver requires the exact route-evidence row and
capability fingerprint, follows only its manifest-declared direct case or
single fact-scoped alias source, reads the case manifest from that pinned tree,
and admits only its ordered input artifacts after exact path, size, and
SHA-256 verification. It then makes a verified, read-only admitted-source copy
under the held output-root lease; capture never mutates or uses the repository
Golden file as a process input. A raw dictionary, arbitrary tiny file, filename match,
contract-only row, or substituted slot cannot construct the typed canonical
input authority accepted by capture.

The loader materializes the pinned Git tree, not current worktree Golden bytes,
and runs the existing `scripts/canonical_golden_validation.py` validator over
that materialized snapshot before parity-specific joining. It rejects duplicate
`routeEvidence` identities before building lookup maps, then requires each row's
capability fingerprint to equal the canonical policy route. Existing validator
rules remain authoritative for case substitution, direct-source-only aliases,
alias chain/cycle rejection, and exact fact scope. Worktree drift is ignored as
input and cannot override a pinned Git object; any pinned-tree blob/hash drift
fails `PARITY_AUTHORITY_MISMATCH`. The Git reader enumerates the pinned commit
with `git ls-tree -r`; the materialized file inventory must equal that list
exactly, and every listed file is read from the same commit. The parity module's
single `_validate_canonical_golden` adapter invokes the existing validator
exactly once on that completed destination before any parity lookup is built.

The current canonical manifest governs 37 of the 64 selected route input sets;
27 selected routes have no case-bound input identity. Those exact route ids
are recorded in the plan and currently block execution with
`PARITY_FIXTURE_MISSING`. They are not filled from a representative case.
Terminal certification remains impossible until independently governed
canonical case/input records cover all 64 routes; the denominator is not
reduced. By explicit release-owner version deferral, the 1.x protected workflow
does not invoke this terminal comparator. Those 27 routes remain parity
evidence debt, do not become `Verified`, and do not block selector, Build, or
output. The complete 64-route terminal promotion gate is deferred to 2.0.0,
where the candidate authority must first be rebound to the exact 2.0.0
implementation and then pass this unchanged fail-closed evidence contract.

The candidate execution surface is likewise the CLI source-built from the
exact `candidateAuthority` implementation head/tree, not the portable release
ZIP. The plan pins
`docs/contracts/v100-candidate-source-executor-v1.json` at 4680 raw bytes and
SHA-256
`9b47da4bf75cfdb86c976ebcce18b0dcba42fcfb35a580e4e9b0781936c5476d`.
That contract pins head `dfaedec4519fae185e5934b79a225b6c67c7d547`, tree
`03f0041524c85b13e366baf0951b4a45850233ea`, SDK `10.0.303`, the complete
lock/tool inventory, and CLI SHA-256
`ba84481f06698f89f0831246443af6bf7592937457530ecd1401fe2fbbd1bbc4`.
The build command pins `ContinuousIntegrationBuild=true` and maps the detached
worktree root to `/_/src`; two independent detached worktrees produced the same
162304-byte executable and SHA above, plus an identical 384-file,
92147320-byte runtime closure with SHA-256
`b688a055f5885103be51447108020de518627ab41aa32127f51fc7b35afea165`.
The renewed lock inventory includes the transitive `NvtFwCombiner.Platform`
project. The prior 384-file closure `31a81718...` has the same count and size
but is not an accepted alternate: its managed runtime bytes carry the prior Git
head through commit-bound SourceLink/provenance, while the apphost executable
remains byte-identical. The historical 382-file closure is likewise not an
accepted alternate for this head. The still-earlier closure `858e21e7...` was superseded because its nine
first-party managed DLLs embedded SourceLink for binding head `b13f0e4d`, not
the implementation head declared by that contract. It is not an accepted
alternate. Omitting either deterministic-build input is authority drift.
The comparator requires a fresh detached Git worktree at the pinned head, rejects
dirty or ignored/stale `bin`/`obj` content before restore, uses locked restore
and Release build, and records the resulting CLI identity as
`candidateSourceExecutorIdentitySha256`; every candidate receipt binds that
identity and uses interface `candidate-source-cli`. The canonical package and
provenance verifiers separately prove that the Desktop release ZIP comes from
the same protected implementation head/tree. This is one candidate planner /
executor semantics path with two evidence concerns; it does not invent a CLI
inside the Desktop single-file package or let source execution certify a
different package.
The candidate source-executor schema is plan-pinned. Its validated raw-file
SHA-256 is the one typed identity repeated in candidate authority, candidate
build, every candidate receipt, provisional comparison, and terminal evidence.
Head/tree, authority-subtree, dirty-tree, SDK, global/lock/tool, restore/build,
CLI assembly, or repeated-identity drift fails closed; no second candidate
executor identity is derived from parsed contract content.
Successful source verification returns one indivisible typed
`VerifiedSourceExecutor` containing the fresh source root, exact head/tree,
raw contract identity, CLI path/size/hash, fixed argv prefix, and fresh-build
fact. Capture accepts only that type and executes its bound CLI path; a caller
cannot pass a parallel dictionary or replace the DLL/path/argv after
verification.

The same lab has also compared one in-scope canonical NT51927 Standard Merge
vector between the exact-tag source-built CLI and a clean current CLI: both
produced 262144 bytes and were byte-for-byte equal. Each side resolved its own
canonical manifest leaf; the current IC-prefixed leaf must not be replaced by
the predecessor's old generic filename. This proves that the bound execution
path can reproduce an in-scope canonical case; it does not certify the
remaining 63 routes.

A second in-scope lab vector, `nt51950-ab-boe-d82t80`, produced byte-identical
524288-byte outputs from both source-built CLIs for the declared single
topology. Requesting cascade was correctly rejected because that map requires
1048576 bytes. This is why every receipt binds topology, map, and output
capacity explicitly; the comparator must not reinterpret an AB UI selection.

The completed direct Standard/AB batch produced exact full-output parity for
all eight direct Standard and all three direct AB canonical cases. It also
confirmed two report-shape differences that must remain visible: NT51928 uses
the predecessor `--ld`/`ld-input` identity where current uses
`--ldc`/`ldc-input`, and NT51929 uses different descriptive `Reason` wording
for the same three scalar operations. The former is the single closed logical
input alias described below; the latter requires no exception because reports
are validated independently and complete output bytes remain exact.

The CtrlRAM batch produced exact complete output bytes for eleven of twelve
full-base cases while demonstrating why cross-version operation equality is
unsafe: NT51923 and NT51927 use different processor scopes, and NT51932 replaces
one legacy broad DiffDLM operation with two narrower NF-preserving operations.
The NT51951 FW2.0 cascade-2 route has an owner-approved semantic correction.
Its predecessor and current full-output SHA-256 values differ
(`7d657a3d...` versus `1536d344...`) over exactly 2816 bytes because v0.9.16 did
not preserve Diff NF. The firmware owner confirmed on 2026-08-28 that the
current NF-preserving output is correct. Admission therefore requires the
declared candidate hash, exact differing-byte count, and exact five half-open
ranges while requiring every byte outside those ranges to equal v0.9.16.
The closed payload-free diagnostic is a historical observation; it is not
mechanically rebound when the candidate executor changes. It must retain the
exact executor that produced the reported bytes until the current candidate is
rerun. The diagnostic
`v0916-nt51951-c2-diagnostic-v1.json` pins the four ordered CtrlRAM CLI input
size/hash facts and both full-base recipe sources, both source-executor
contracts/head/tree/CLI identities,
reported 524288-byte output hashes, exact 2816-byte difference count, and the
five observed half-open ranges. It explicitly lists every missing baseline and
candidate preview/build receipt/report plus the missing independent comparison
record. Consequently the historical diagnostic remains
`blocked-incomplete-independent-observation` and cannot itself prove the
correction. The same-run comparison is the only admissible proof and fails
`PARITY_EXACT_MISMATCH` if any output identity, count, or range escapes the
owner-approved boundary.

The compare run manifest intentionally contains no owner attestation. A
successful compare writes deterministic JCS bytes conforming to
`v0916-parity-comparison-v1.schema.json` with verdict `provisional`.
Finalization is a distinct invocation conforming to
`v0916-parity-finalize-v1.schema.json`; it supplies the exact raw comparison
artifact and the separately approved owner-attestation artifact. Only its
output conforms to terminal `v0916-parity-evidence-v1.schema.json` and may have
verdict `pass`. This removes any circular requirement to approve evidence
before compare has produced its immutable digest.

The same hash-pinned comparator script prepares and finalizes the canonical
invocation record. Each receipt contains two independently bound invocations
against the same pinned executor, scenario, and ordered inputs: `preview`
produces the typed compilation authority and `build` produces the executed
run. Their complete version-specific argv are committed by separate
`authorityArgumentsSha256` and `argumentsSha256` values. Preview must complete
before build starts. Except for the operation token and output/report
destinations, their logical input facts must be identical. A preview report
cannot be supplied by a different executable, scenario, input set, or run.
For every full-image CtrlRAM case, the same verified source-built CLI first
executes a typed Standard Merge Preview/Build from the case's governed DP and
TP artifacts. Its two raw reports, resolved profile, compilation fingerprint,
source hashes, and output hash are written as a separately hashed base-precursor
proof. Only that staged output may become `replace-base`; an `expected-output`
artifact is never admitted as a base. TP-work cases continue to use their
governed TP input directly. Base kind is never inferred from a map/profile
name. `ctrlRamBaseRoutes` explicitly binds every currently fixture-backed
CtrlRAM route and current capability fingerprint either to `tp-input` or to
one exact same-IC Standard Merge route, capability fingerprint, and map
variant. The precursor Preview and Build must report that exact map; a
same-capacity or similarly named route is not interchangeable.
The comparator recomputes both argument digests from JCS bytes containing
execution-artifact and executor-identity SHA-256 values, route/fingerprint,
full scenario, and ordered path-free input
facts. It then reads the raw typed Application report named by the capture
record and derives the `compiledOperations` projection; caller-provided
processor facts are not accepted. Every projected operation retains id,
sequence, kind, terminal status, nullable source address-space/range, target
address-space/range, overlap policy, reason, provenance, and, when present, the
typed processor id, tool binding, allowed half-open read/write ranges, and
runtime commands. The raw Application report hash, compilation fingerprint,
terminal success, and output hash/size must agree with the capture record.

The raw report root is also authoritative. `IcId` must equal the selected
policy route; `ModeId`, `ExperienceId`, and `CompositionKind` must equal the
plan's closed workflow report binding. Current typed reports must also disclose
the exact resolved `MapId`, which must equal the route's declared `mapVariant`;
the immutable v0.9.16 report format predates this field and is accepted only on
the baseline side. Ordered `Inputs` must equal the
receipt's ordered slots by address-space id, size, and SHA-256. `Output` must
be committed and match the receipt artifact, `Issues` must be empty, and raw
start/completion times must equal the successful invocation and terminal
capture after normalizing the serializer's UTC `+00:00` spelling to canonical
contract `Z` without changing the instant. Every field is parsed from the
actual Pascal-case report; a parallel
caller-supplied root summary is not authority.

Before exact or transitive comparison, the comparator reopens the separately
hashed receipt artifact, requires it to equal the public in-memory receipt,
and reruns complete receipt/report/raw-report/input/output/base-precursor
validation. Its comparison consumes only the bytes captured by that final
hash check and reconstructs projection/compiled authority from the persisted
raw reports. A post-receipt output, input, report, precursor proof, precursor
source, or precursor report modification or path swap therefore fails
`PARITY_PROVENANCE_INVALID`; comparison never trusts stale private fields or
reopens an unverified path after validation.

The raw report is the actual `CompositionRunReportJson`/v0.9.16
`CliRunReportWriter` projection, not a comparator-shaped substitute. The parser
requires the serializer's Pascal-case root and nested names, including
`Operations[].OperationId/Sequence/Kind/Status/SourceSpaceId/SourceRange/
TargetSpaceId/TargetRange/OverlapPolicy/Reason/Provenance`, nullable
`ProcessorId`/`ToolBindingId`, both processor range arrays, and non-empty
`ExecutedCommands` for a processor operation, plus
`Mutations[].OperationId/Kind/TargetSpaceId/TargetRange/ChangedByteCount/
BeforeSha256/AfterSha256/Reason`. `ByteRange` is read from its serialized
`Start`/`Length`/`EndExclusive` triple and checked for exact half-open
consistency. The capture record's lower-case `compiledOperations` and
`compiledMutations` are deterministic projections of those raw Pascal-case
objects. Mutating either the raw operation/processor authority or raw mutation
range/hash without changing the capture projection fails provenance.
Before projection is admitted, every range is validated as a non-empty
half-open `[start, endExclusive)` interval using checked 64-bit arithmetic and
must fit its typed address-space capacity. Every source, target, mutation, and
processor allowed-read/write range must name the exact typed address-space id
declared by its owning operation; overflow is checked independently for every
range family. Copy source/target lengths must
match. Mutation ranges must be contained by their operation target and, for
processors, by one declared allowed-write range; `ChangedByteCount` cannot
exceed the mutation length. Processor read/write ranges must be non-empty,
contained, and non-overlapping, and two operation targets with `Reject`
overlap policy may not intersect. Empty, negative, overflowing, out-of-space,
undeclared-overlap, or count/range drift fails
`PARITY_REPORT_RANGE_INVALID`; the comparator does not repair or infer a
different range.
Runtime commands retain sequence, package-relative executable identity,
host-staging working-directory classification, argument count, and the JCS
digest of argv after only declared package/staging/input/output roots are
replaced with fixed tokens. The raw report remains local; absolute package,
input, output, or staging paths are never copied into payload-free evidence.
An executable outside the verified package, a working directory outside the
host-created staging root, or an unrecognized absolute argument fails closed.
Every capture starts in a newly created empty route staging directory beneath
the explicit output root. On Windows the comparator uses one controlled-write
primitive: one native `NtCreateFile` call with
`FILE_CREATE | FILE_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT` atomically creates
a cryptographically named child and returns a no-delete-sharing directory
handle. The comparator validates the final path and reparse state from that
held handle and retains it until
cleanup. There is no separate check-then-create window. Extraction and capture
destinations must not already exist, escape the allowed root, or have a reparse
ancestor. Each process uses a new read-only staged copy of every ordered input;
the comparator captures each admitted source exactly once, validates that
capture against the declared size/hash, and reuses those immutable bytes for
both Preview and Build. After acquiring no-share read handles over the staged
runtime closure and staged inputs, it re-hashes both inside custody immediately
before launch. Mutation of the admitted path after its capture cannot change
either invocation; mutation of a staged runtime/input fails closed before it is
consumed. The repository Golden source remains read-only and is never the
mutation target. Metadata/report publication uses exclusive create and
atomic replacement; an existing final path is a conflict, not an overwrite.
Partial metadata and staging files are cleaned after process or extraction
failure. The comparator never writes or edits firmware input bytes; only the
existing CLI may create its declared output inside the bounded staging root.

Baseline and current operation/mutation projections are not compared to each
other. For each side, raw preview and build reports are retained and hashed.
The build operation projection must exactly match the preview typed authority
from that same executor and invocation pair; build mutations must name
preview-declared operations and remain within their declared half-open target
and processor write ranges. Mutation changed-byte counts cannot exceed their
ranges, and hashes must be well formed, but preview does not predict runtime
before/after hashes or descriptive mutation reasons. The preview report is
required to be successful but uncommitted; only the build report may declare
`Output.Committed=true`. This is
essential when a safer current profile narrows a processor range or splits a
legacy broad operation while preserving final bytes. Missing, extra, duplicate,
reordered, failed, or range/identity-different facts relative to that report's
own authority fail provenance. Evidence records both raw-report, projection,
and compiled-authority hashes and explicitly states that cross-version
operation comparison was not applied. A transitive route reuses the
independently validated full reports and separately validates its TP typed
report against its TP compilation fingerprint. The comparator never predicts a
processor or reconstructs firmware semantics.

Exact routes still require identical cross-version-stable IC, workflow,
topology, map, selection, declared output capacity, and ordered logical input
facts. Capacity must equal the actual typed output size; it is never inferred
from a UI IC-count selector or another route. Raw invocation arguments and raw
report input ids remain version-specific authority. The only input-identity
alias is the plan-pinned NT51928 Standard Merge mapping `--ld`/`ld-input` to
`--ldc`/`ldc-input` under logical input `ldc`; logical-input sizes, hashes, and
order plus final output bytes remain exact. No operation id, range, or reason
text is normalized across versions.
Compilation fingerprints are intentionally excluded from same-scenario
equality: the baseline and current raw fingerprints are retained separately and
may differ. The current fingerprint is associated with the selected current
route/capability and must match its raw report; the baseline fingerprint is
predecessor provenance only and must match its own raw report. A TP route must keep
replacement inputs identical and use a TP base equal to the declared prefix of
the full base. Firmware paths remain local and are omitted from evidence.

`candidateAuthority` binds the reviewed implementation head and tree plus exact
Git tree object ids for `src`, `profiles`, `external-tools`, and
`tools/crc-worker`. Candidate admission reuses the canonical candidate-manifest
verifier and release smoke verifier. It verifies the GitHub-observed
protected-main workflow run and artifact id/digest, candidate manifest and ZIP
digests, sidecar provenance subjects, source commit, and source tree. The
candidate ZIP's inner `RELEASE-MANIFEST.json` is necessary but never sufficient.
The plan's `authorityTransfer` owns one closed exact list. H2 must be the direct
child of firmware-executor H1 and change precisely `allowedBindingChildPaths`.
`finalEvidenceTail` separately predeclares one exact `M` final-record path set
and one exact `A` external-attestation path set. Package admission requires the
sole ordered H1→H2→H3→H4 chain, canonical governance semantics for H3/H4,
identical authority trees/policy bytes at all four heads, and
`packageSourceHead == H4`. H2-only, H3-only, a merge, squash, reorder, extra
path, or later H4 descendant fails. This transfers parity authority, never
package certification.
The final ZIP still passes the normal release package gates independently.

Firmware executor, binding, and package source authority are separate inputs
to package admission. v1.0.0 binds immutable H1, exact H2, and terminal H4;
the candidate workflow validates exact H4 before packaging. v1.0.1 is only an
upgrade-test package and must be the direct single-parent child of the
immutable v1.0.0 H4 tag, while its repository diff remains exactly `VERSION`,
and its package may differ only at the closed version-bearing paths. It uses
the normal package/startup/upgrade gates and does not rerun the v0.9.16 parity
executor under a false v1.0.1 source identity. This is an explicit release-owner
version deferral, not terminal parity evidence. Parity jobs and parity-specific
promotion steps run only for 2.0.0; all 1.x versions require them to be skipped.
No skipped job may create a pass artifact or promote parity evidence debt to
Verified.

The output conforms to `v0916-parity-evidence-v1.schema.json`. It contains
only package, Git authority, policy, comparator, scenario, input, receipt,
report, output-length/hash, proof, and terminal-verdict facts. It does not
contain firmware bytes or local paths. The comparator identity is its fixed
contract version and exact script SHA-256, not a free-form executor name. A
committed or release-supplied report is not approval by itself. A terminal
`pass` requires a document conforming to
`v0916-parity-owner-attestation-v1.schema.json` plus an independently verified
firmware-owner record. The protected `firmware-parity` environment sequences
the review job but neither a deployment status nor its `creator` establishes
firmware-owner identity. The attestation binds plan/policy, implementation head/tree,
candidate package/manifest/artifact, complete 64-route evidence, exact receipt
set, authorized operators, approver identity, and approval time. The protected
`release` environment remains an independent release-owner gate.

Candidate build admission is active verification, never a caller-supplied
`passed` flag. Through injected testable process/GitHub ports, the comparator
queries the exact repository workflow run and artifact ids, then executes from
the repository root:

```text
<python> scripts/release_promotion_policy.py verify-manifest --asset-dir <asset-dir> --manifest <candidate-manifest> --source-sha <implementation-head> --source-tree <implementation-tree> --run-id <run-id> --workflow-sha <workflow-sha> --workflow-ref refs/heads/main
pwsh -NoProfile -File scripts/smoke-release.ps1 -PackagePath <candidate-zip> -SkipUiLaunch
```

The script files' raw SHA-256 values must equal the run/evidence declarations
before either process starts. Exact argv, repository working directory,
zero exit status, candidate-manifest digest, package/provenance subject hashes,
GitHub workflow/head/ref/conclusion, and non-expired artifact id/name/digest are
all required. The GitHub artifact response must itself carry a `workflow_run`
owner whose run id, head SHA, head branch, and repository equal the separately
queried protected workflow run; a same-named or same-digest artifact owned by
another run/head is rejected. Missing local artifacts use
`PARITY_ARTIFACT_MISSING`; GitHub or
protected-build identity failures use `PARITY_AUTHORITY_MISMATCH`; nonzero
canonical verifier results and package/provenance byte drift use
`PARITY_PACKAGE_MISMATCH`.
Workflow identity is three distinct facts: the workflow commit SHA used for the
Contents query, the Git blob SHA returned for that path at that commit, and the
SHA-256 of decoded raw workflow bytes. None may substitute for another.
The authenticated protected-main workflow commit is independent of the pinned
candidate implementation/package head
`dfaedec4519fae185e5934b79a225b6c67c7d547`; equality is neither required
nor authority. The blob and raw digests independently prove which workflow
bytes occupied `.github/workflows/release.yml` at the workflow commit, while
the candidate manifest/provenance and source-executor contract continue to bind
the candidate head/tree. Neither identity may substitute for the other.

GitHub authority is reconstructed only from realizable Actions workflow-run,
job, Contents, Deployments/statuses, and artifact metadata/download facts. The
pinned workflow bytes are parsed and compared with the raw-pinned
`v0916-parity-workflow-v1.json` semantic contract. It owns the exact trigger,
read-only permissions, job ids/names/dependencies/order, conditions,
environments, timeouts, SHA-pinned actions, action inputs, command/argv text,
and artifact names/paths/retention/upload-download sequence. Substring
sentinels, unpinned actions, `continue-on-error`, `always()`, write permissions,
alternate commands, extra parity jobs, or artifact drift fail
`PARITY_WORKFLOW_MISMATCH`. Normalization retains the complete three-job parity
subgraph plus top-level trigger and permissions: compare/attestation/finalize identity,
order, and action map cannot be swapped, and checkout `ref` or any checkout
option cannot drift. The other three release jobs are outside the detailed
parity-field projection, but `jobInventorySha256` still binds all six complete
job objects. Changes to candidate, promote or published-smoke require reviewed
inventory-digest synchronization and propagation of the raw workflow-contract
identity to the plan, schemas and fixtures. The separate promotion gate also
binds the complete ordered step list; an extra job or undeclared edge is rejected.
After approved workflow changes, the agent uses
`python scripts/sync_derived.py --write --only v0916-workflow-contract`, reviews
the diff, and runs the default read-only check before formal verification.
This mechanically updates the four derived digests and their existing raw
size/SHA projections together. The parity owner supplies the plan and validates
its unchanged authority; the common runner is the sole writer. CI never writes
these expectations, and synchronization does not approve workflow behavior,
renew historical certification or alter candidate-source/H1-H4 authority.
The repository structure gate selects frozen H2 from `reviewedHead` in the
existing immutable `RELEASE-111-PARITY-AUTHORITY-TRANSFER-09` final record,
after canonical capability governance checks that record. It does not use the
current plan's last-change commit: a live workflow fingerprint update is not
a new source certification. Missing/invalid record or Git authority fails
closed, and the original exact H1/H2 transfer checks remain unchanged. The
separate parity package-source validator still requires its exact H1-H4 chain;
an ordinary later release commit cannot stand in for H4.
A job response is not
assumed to contain an environment, reviewer, or head SHA, and artifact metadata
is not assumed to contain a workflow-job id. A deployment status `creator` is
also not treated as the protected-environment reviewer or firmware owner.
The finalizer consumes those facts through one injected reader and retains the
normalized `protectedRun` in terminal evidence. It requires the queried run's
id/attempt/head SHA/head branch/event/conclusion and repository/head-repository
ids, the job's id/run/attempt/head/branch/name/conclusion and interval, the
deployment's id/SHA/ref/environment/time, the successful status and job log
URL, and all three artifacts' ids/names/digests plus `workflow_run`
run/repository/head-repository/branch/head fields. It streams both archives and
requires their one declared JSON member to equal the local comparison and
attestation bytes exactly. Any cross-run, cross-head, cross-branch,
cross-repository, cross-job, failed/incomplete run, or artifact-owner mismatch
is `PARITY_AUTHORITY_MISMATCH`.
GitHub facts prove sequencing and same-run artifact ownership only. Firmware
owner authority comes exclusively from the independently injected verifier and
its payload-free verification record; unavailable or mismatched verification
fails `PARITY_OWNER_APPROVAL_REQUIRED`.

Metadata equality alone does not bind the operator's local candidate files.
The injected GitHub adapter streams the exact declared `artifactId`; the
comparator accepts only GitHub's lowercase `sha256:<64-hex>` digest form and
rejects missing prefixes, uppercase or other algorithms. It hashes the stream,
requires that digest, and boundedly extracts the archive into a new empty
host-created directory. The archive must contain exactly the existing canonical
six-file surface: Windows ZIP, SPDX SBOM, provenance, `RELEASE-NOTES.md`,
candidate manifest, and versioned `assets.sha256`. The fixture is produced by
the real `release_promotion_policy.py create-manifest`, and admission reuses its
`verify-manifest` command. Extraction rejects a missing or undeclared entry,
case-fold duplicate, forward- or backslash traversal, drive/UNC/root absolute
path, link, per-entry or aggregate size overflow, compression-ratio overflow,
and CRC/read failure. Malformed-archive tests keep the other five canonical
entries legal. Both canonical verifiers use the extracted assets, and all six
operator-local files must match those extracted bytes before a receipt is
admitted.

Certification is therefore two-phase: `compare` deterministically produces the
payload-free route/receipt digests, then `finalize` consumes the protected owner
attestation over those exact digests and emits terminal evidence. A provisional
comparison without that attestation is never a passing release artifact.
Finalization receives independently injected GitHub and firmware-owner
verification readers. One exact
protected-main workflow run first uploads the unique provisional-comparison
artifact. Its pinned `release / v0.9.16 parity attestation` job then waits on
the `firmware-parity` environment, downloads that exact same-run comparison,
and can upload the unique owner-attestation artifact only after the independent
verifier has emitted a record bound to those exact bytes and all plan, policy,
head/tree, package, route, and receipt digests. The environment payload may be
`{}` and is not evidence. The finalizer checks workflow/run/head, the pinned
job interval, same-run artifacts and the external verification record. Missing
owner verification fails `PARITY_OWNER_APPROVAL_REQUIRED`; cross-run,
cross-head, or cross-job substitution fails `PARITY_AUTHORITY_MISMATCH`.

The protected-run operator sequence is deliberately explicit:

1. Let the compare job finish and download that run's
   `v0916-parity-comparison-<run-id>` artifact for independent firmware-owner
   review.
2. Produce the owner attestation and external verification record outside the
   workflow, bound to that artifact id/digest and every declared certification
   digest.
3. Store their base64 bytes and verifier identity in the protected
   `firmware-parity` environment secrets
   `NFC_FIRMWARE_OWNER_ATTESTATION_B64`,
   `NFC_FIRMWARE_OWNER_VERIFICATION_B64`, and
   `NFC_FIRMWARE_OWNER_VERIFIER_ID`.
4. Approve the waiting attestation job only after those exact values are
   present. If the job was approved before the values existed, do not edit or
   synthesize evidence in-place; set the values and rerun the failed jobs so a
   new run owns the complete artifact chain.
5. The finalizer independently downloads and validates the comparison,
   attestation, and verification artifacts. A successful environment approval
   by itself is never sufficient evidence.

All contract timestamps use canonical UTC `YYYY-MM-DDTHH:mm:ss[.fffffff]Z`.
This requirement applies to receipts, capture projections, comparisons,
attestations, and final evidence; the raw typed Application report retains its
serializer-produced UTC `+00:00` spelling and is normalized during projection.
Schema validation enables draft-2020-12 format checking in addition to the
closed lexical pattern. Offset spellings, impossible calendar dates, or more
than seven fractional digits fail. Invocation start is not after completion;
comparison execution and artifact creation precede approval; approval precedes
the pinned job start;
attestation issuance falls inside the job; and the attestation artifact is
created after issuance but before the successful job completes.

Stable failure codes are part of the command contract:

| Code | Meaning |
| --- | --- |
| `PARITY_PLAN_INVALID` | Plan/schema/count/proof classification is invalid. |
| `PARITY_POLICY_DRIFT` | Policy bytes or selected route identity changed. |
| `PARITY_FIXTURE_MISSING` | A selected route lacks an independently governed canonical case/input binding. |
| `PARITY_AUTHORITY_MISMATCH` | Git authority, evidence-child path, or implementation identity changed. |
| `PARITY_WORKFLOW_MISMATCH` | The authenticated workflow graph differs from the closed parity workflow contract. |
| `PARITY_PACKAGE_MISMATCH` | Candidate package/provenance bytes or canonical package verification failed. |
| `PARITY_ARTIFACT_MISSING` | A declared local evidence file is unavailable. |
| `PARITY_WRITE_CONFLICT` | A staging, extraction, or metadata destination is pre-existing, reparse-backed, or cannot be published exclusively and atomically. |
| `PARITY_PROVENANCE_INVALID` | A receipt, invocation, report, or comparator identity is invalid. |
| `PARITY_REPORT_RANGE_INVALID` | A report range is empty, overflowing, out of bounds, overlapping without authority, or inconsistent with changed-byte/processor limits. |
| `PARITY_INPUT_SCENARIO_MISMATCH` | Baseline/current scenario or ordered inputs differ. |
| `PARITY_INPUT_MUTATED` | A staged read-only input changed between its admitted hash and a preview/build process launch. |
| `PARITY_EXACT_MISMATCH` | A complete-output comparison differs. |
| `PARITY_TP_PREFIX_MISMATCH` | A required TP-prefix equality differs. |
| `PARITY_TAIL_MUTATED` | Candidate full-output tail differs from its base. |
| `PARITY_EVIDENCE_INCOMPLETE` | Evidence omits, repeats, or fails a route. |
| `PARITY_OWNER_APPROVAL_REQUIRED` | Independent firmware-owner verification is missing, unavailable, or does not bind the exact attestation and certification digests. |

## Canonical digests

All JSON-derived R3 digests use RFC 8785 JSON Canonicalization Scheme (JCS)
UTF-8 bytes without BOM. Object property names are sorted by unsigned UTF-16
code units, not by Unicode scalar values or host-language default ordering.
Parsers reject duplicate object keys before canonicalization and reject lone
surrogates. This contract deliberately accepts only the I-JSON integer subset
`[-9007199254740991, 9007199254740991]`; fractional/exponent JSON numbers,
non-finite extension tokens, and integers outside that exact range are invalid
rather than relying on a host's floating-point formatter. Property order,
insignificant source whitespace, and source escape spelling therefore do not
change a digest. Array order remains semantic
except for the closed projections below, which are sorted before JCS encoding:

- route evidence rows: ordinal `(routeId, capabilityFingerprint)`;
- receipt-set rows: ordinal `(routeId, roleRank, receiptSha256)`, where role
  rank is `baseline-exact`, `candidate-exact`, then `candidate-tp`;
- provenance subjects: ordinal `(name, sha256)`;
- authorized operator logins: unique ordinal strings.

`fullEvidence.evidenceSha256` is the JCS SHA-256 of the referenced complete
exact route-evidence object, not merely its route id. `routeEvidenceSha256` is
the JCS SHA-256 of the complete sorted 64-row array. `receiptSetSha256` covers
the complete sorted path-free receipt identity projection. The provenance
subject digest covers the complete sorted subject projection. Plan, policy,
candidate manifest, package, script, receipt, report, and owner-attestation
files always use SHA-256 of their exact raw file bytes; they are never parsed
and reserialized for their file identity.

Locked independent vectors (lowercase SHA-256):

| Projection | JCS value | SHA-256 |
| --- | --- | --- |
| Unicode/object | `{"a":1,"items":["β","a"],"z":"中文"}` | `31c7db457f755d371b2733a121f58ed1dd0a1a013f9a0d8e5370258ceffc2d5e` |
| UTF-16 key order | `{"😀":1,"":2}` | `04208f6cdb854e2ab1b07dd3633a39dec854344fe72824cf7f2fdb4e2e33129e` |
| I-JSON integer bounds | `{"max":9007199254740991,"min":-9007199254740991,"zero":0}` | `b7b2401ddca2165824e98c61890c0aaec470258d3119dd265d02be9438bf47e6` |
| receipt set | `[baseline route-a/11.., candidate route-a/22.., TP route-b/33..]` | `c7a4f2c2531fa5787135cd17ae2996e46d7060b740aa3790954303f429d3dfe7` |
| provenance subjects | `[NvtFwCombiner.exe/aa.., profiles/中.json/bb..]` | `b8c72d66da20771f8793b3516611ee720e17c1009fdfc426f282f1518e11884c` |
| route evidence | Complete schema-valid `route-full` exact row plus schema-valid `route-tp` transitive row defined by the locked red fixture | `12c95f88f5454f8b730bf92821d661b49ae34b859b1d960132624afb9f1bc697` |
| complete exact evidence row | Schema-valid `route-full` row including stable scenario, declared output capacity, exact predecessor executor identity, distinct predecessor/current compilation fingerprints, independently validated report-authority hashes, both receipts and outputs, `equal:true`, and `passed:true` | `020e447dcf39250600d35e2f2305e7aa886be674f78b2833a5bd6e740f1025b8` |
| authorized operators | `["dennis40816","fw-owner"]` | `f6267fa1b9a83dabcd5b53b56431b72e6e0d39213e2c58587df8bc516cae651a` |
