# ADR 0057: v0.9.16 Black-box Parity Certification

- Status: Accepted
- Date: 2026-08-26
- Owners: Repository owner + firmware owner + release owner
- Amends: ADR 0038, ADR 0046, and ADR 0055

## Context

The canonical capability policy publishes 64 Standard Merge, AB Merge, and
CtrlRAM Replace routes. Runtime closure proves that each route reaches the
shared planner and executor, but executable closure is not independent byte
parity with the stable predecessor. The repository owner requires the
`v1.0.0` candidate to be compared with the exact `v0.9.16` predecessor
before those support claims enter a stable release.

The immutable GitHub release asset
`NvtFwCombiner-v0.9.16-win-x64.zip`, SHA-256
`e55687f9d98ca3a2b02eac5789f4443697a249dcc60b261e3e6cfeae7dc03c84`.
Its release manifest binds stable tag `v0.9.16` to peeled commit
`462590e8b993b8e42d088bc07377571a4bb9f25d`. The annotated tag object is
`578b2614632d6c2affdf2000324b134b5d1a16c1` and its source tree is
`dc46c9aa9ecf00cb898ba3bc287e1b15acdab735`.

The published ZIP exposes only the desktop GUI and is retained as release
provenance/reference, not as a fictional CLI execution surface. The formal
predecessor executor is instead the CLI source-built from the exact annotated
tag authority. Its immutable executor contract binds the tag/commit/tree,
clean source state, resolved SDK `10.0.303`, raw `global.json`, all dependency
locks, external tools, exact locked restore/build commands, and CLI assembly
SHA-256 `e889668c...f9db`. Every predecessor receipt binds that contract and
assembly. NT51920, NT51930, and NT51931 are outside this 64-route selector.

A second lab observation compared one in-scope canonical NT51927 Standard
Merge vector using exact-tag and clean current source-built CLIs. Both outputs
were 262144 bytes and byte-for-byte equal. The two builds resolved their own
manifest leaves, including the current IC-prefixed canonical filename, rather
than sharing the predecessor's generic input name. This demonstrates a viable
bound batch path; it does not certify any route that has not executed
independently.

The lab also reproduced one in-scope NT51950 AB vector at its declared single
topology with byte-identical 524288-byte outputs. The typed validator rejected
the same bytes under cascade because that map requires 1048576 bytes. Parity
therefore binds the selected topology, map, and capacity; no UI IC-count state
may infer or rewrite those facts.

Subsequent bilateral execution closed all eight direct Standard and all three
direct AB canonical cases at identical full bytes. The CtrlRAM batch closed
eleven of twelve full-base cases and showed that report operations legitimately
differ across versions: narrower processor scopes and explicit NF-preserving
subranges are safer than predecessor broad writes even when final bytes match.
The NT51951 FW2.0 cascade-2 case differs from v0.9.16 over exactly 2816 bytes,
including an NF-owned tail. On 2026-08-28 the firmware owner confirmed that
v0.9.16 failed to preserve Diff NF and that the current NF-preserving result is
the required behavior. This is an approved semantic correction, not a request
to weaken exact evidence: the candidate must reproduce the declared output
identity, exact differing-byte count, and all five half-open difference ranges;
every byte outside those ranges must still match v0.9.16.
The payload-free
`docs/contracts/v0916-nt51951-c2-diagnostic-v1.json` records the four ordered
CtrlRAM CLI input identities, the two sources of the immutable full-base
recipe, both exact executor authorities, both reported
output identities, and the five half-open difference ranges. It also enumerates
the nine missing preview/build receipt/report and independent-comparison
artifacts. Its status is deliberately `diagnostic-only-not-admitted`; it cannot
satisfy route evidence or promotion.

Eleven CtrlRAM routes intentionally emit a shortened TP work image while their
paired predecessor route emits a complete FlashCode. A whole-file comparison
cannot compare objects with different declared capacities, and silently
truncating every route would weaken the independent oracle.

## Decision

One immutable certification plan binds the exact canonical policy bytes and
selects routes where authoring is `available`, publication is `supported`, and
workflow is Standard Merge, AB Merge, or CtrlRAM Replace. It must resolve to
exactly 64 unique route/fingerprint pairs: 14 Standard, 6 AB, and 44 CtrlRAM.

The plan separately raw-pins the canonical Golden manifest, its Git blob, and
the canonical-root tree at the candidate implementation commit. Each selected
route must resolve through its exact route-evidence/fingerprint to a governed
case manifest and exact ordered input artifact path/size/SHA-256. A caller
cannot supply or synthesize that authority. The loader materializes only the
pinned Git tree, runs the existing canonical Golden validator, rejects duplicate
route evidence before lookup construction, and joins exact policy fingerprints.
That validator remains the single owner of case substitution, alias
chain/cycle/direct-source, and fact-scope rules; worktree Golden drift cannot
override pinned bytes. Materialized paths must exactly equal the pinned
`git ls-tree -r` inventory, and the existing validator is called exactly once
after materialization and before parity joins. The current manifest binds 37
selected routes and leaves 27 exact route ids without case input authority;
those routes remain in the denominator but block all execution with
`PARITY_FIXTURE_MISSING` until canonical evidence supplies them.

CtrlRAM full-image evidence may not reuse a case's final `expected-output` as
its reference. Each verified baseline/candidate CLI independently builds the
immutable base from the case's DP+TP Standard Merge recipe, and the comparator
binds that typed precursor report and hash before CtrlRAM execution. Current
reports also disclose the resolved map id, so the declared route map is checked
against executor-observed authority instead of being copied back from policy.

Every selected route uses complete-output equality except the eleven exact
route identities declared by the plan. Complete-output equality compares
size, every byte, and SHA-256. A mismatch, missing artifact, changed route
fingerprint, changed package identity, or unknown proof kind fails closed.

The eleven shortened TP routes use the owner-approved transitive proof:

```text
current TP output
  == current paired full-output TP prefix
  == v0.9.16 paired full-output TP prefix
```

Their paired full route must independently pass complete-output equality. The
candidate full output tail `[tpLength, fullLength)` must also equal the same
range in the immutable candidate full-base input. This extra tail check proves
that the TP operation did not acquire authority over bytes outside its
declared work image. All ranges are half-open.

The comparator treats both CLIs as black-box executors. Firmware inputs and
outputs stay in an operator-controlled local evidence directory and never
enter Git. Before execution it verifies the annotated tag object and exact-tag
source baseline contract, clean tree, SDK, global/lock/tool bytes, locked
restore/build commands, and CLI
assembly. It then runs 53 predecessor complete-output scenarios and all 64
candidate scenarios; the 11 shortened rows consume only their declared
already-passing full counterpart. The comparator does not implement firmware
execution. It recomputes the canonical argument digest
from execution-artifact/executor identity, route/scenario, and ordered input
facts. It parses the versioned build report and verifies its
terminal result, output bytes, route/scenario digests, and the normalized
operation/processor projection derived from the existing typed Application
report. The parser consumes the actual Pascal-case
`CompositionRunReportJson`/v0.9.16 `CliRunReportWriter` shape and derives both
operation and mutation projections; a caller cannot substitute an already
normalized report. The comparator does not infer processor identity or ranges.
It still validates the typed report's structural invariants: independently
checked non-empty half-open source, target, mutation, and processor ranges,
exact owning address-space ids, capacity containment, equal copy lengths,
mutation containment and changed-byte bounds, processor allowed-range containment, and
declared overlap policy. Invalid or overflowing report facts fail
`PARITY_REPORT_RANGE_INVALID`; they are never normalized or repaired.
Each receipt binds a `preview` typed-authority invocation and a subsequent
`build` invocation, including separate canonical argument digests, against the
same pinned executor/scenario/ordered inputs. Each baseline/current build
report is validated against the preview authority emitted by its own executor;
cross-version operation equality is deliberately not applied.
TP rows validate their own typed TP report and reuse the paired full reports.
Merely hashing opaque report bytes is insufficient. Every
baseline/current exact comparison binds the same IC, workflow, topology, map,
selection, declared output capacity, and ordered inputs. Capacity is bound to
the typed output and cannot be inferred from a UI chip-count choice. The
predecessor and candidate raw compilation
fingerprints are retained separately and may differ: the candidate fingerprint
is bound to the selected current capability while the predecessor fingerprint
is provenance for its own raw report only. Transitive comparison also requires
identical replacement inputs and
a TP base that is the exact declared prefix of the full base. NT51928 Standard
Merge has one closed version-specific logical-input alias from predecessor
`--ld`/`ld-input` to current `--ldc`/`ldc-input`; no operation/range/reason is
normalized. Equal output
bytes without this provenance cannot pass.

The run manifest contains route requests and one explicit output root, not
caller-authored input identities or externally produced receipts. Only the
canonical input resolver creates the typed authority passed to capture.
Admission derives the closed matrix
from the canonical policy and requires exactly 64 unique route/fingerprint
records and 117 unique executor/route pairs: 53 predecessor and 64 candidate.
Duplicate, missing, wrong-fingerprint, profile, slot, option, input, or order
facts fail before execution. The comparator invokes each verified CLI itself,
preview then build. The resolver first materializes a verified read-only
admitted-source copy under the held output-root lease, so the repository Golden
file is never passed to or mutated by a process. It creates a new empty staging directory per capture with
one Windows controlled-directory lease: a single native `NtCreateFile`
`FILE_CREATE | FILE_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT` operation creates
the directory and returns its no-delete-sharing handle atomically. The lease
remains open through extraction,
capture, and cleanup, so an attacker cannot replace the checked directory
between validation and use. It rejects existing or
ancestor-reparse/path-escaping destinations, copies each input read-only, and
hashes each newly staged copy immediately before each process. The
admitted-source copy is reopened and rehashed before each new staged copy; changing
it after Preview therefore fails before Build starts.
Exclusive/atomic metadata publication never overwrites an existing output;
partial state is removed on failure. It never writes firmware input bytes or
introduces another firmware execution model.

The current release ZIP is a Desktop single-file package and is not described
as containing a CLI. Candidate receipts use `candidate-source-cli`, built from
the exact clean `candidateAuthority` head/tree with the repository-pinned SDK
and locked dependencies. The plan pins
`docs/contracts/v100-candidate-source-executor-v1.json` at raw SHA-256
`035bfacbae7e66436b3fb57179694e5e6642a30b67e3653fcdb00f83bbbb6ae7`,
head `3b73792e605fb1ce48f51d1aae004f8fec6434b4`, tree
`3fac0994d2a7150ff1ea4a3be91c89f95da7811c`, and CLI executable SHA-256
`be33bf8ad050fa5e9ba24d464910ac09e24944ba97ca56a27c7f57001b8521e9`.
The source is materialized into a fresh detached Git worktree at that exact
head; dirty,
ignored, or pre-existing `bin`/`obj` output fails before restore. Candidate
receipts bind the resulting verified executor identity. Candidate
package admission remains a separate canonical proof over the protected
Desktop ZIP, manifest, SBOM, provenance, notes, and checksums from that same
head/tree. Source execution cannot transfer package identity.
Both baseline and candidate source-executor identities are raw contract-file
SHA-256 values carried from their validated loaders; parsed/JCS reserialization
cannot create an alternative identity. Candidate authority, build, receipts,
comparison, and evidence must repeat the same candidate identity exactly.

Candidate authority is not a free-form version string or an inner manifest
claim. The run and evidence bind an `implementationHead`, its complete Git
tree, the canonical-policy digest, and exact Git tree object ids for `src`,
`profiles`, `external-tools`, and `tools/crc-worker`. They also bind the
comparator contract and script digest. Candidate admission reuses
`scripts/release_promotion_policy.py verify-manifest` and
`scripts/smoke-release.ps1 -SkipUiLaunch`; it does not add a reduced ZIP
verifier. The comparator independently queries the declared GitHub Actions run
and artifact and requires the protected-main workflow identity, run/artifact
ids, artifact digest, candidate-manifest digest, ZIP digest, provenance
subjects, source commit, and source tree to agree. A forged ZIP with a copied
`sourceCommit` cannot pass. The artifact's own `workflow_run` owner must also
match the independently queried run, repository, branch, and head SHA.
The authenticated protected-main workflow commit is intentionally independent
of candidate implementation/package head
`3b73792e605fb1ce48f51d1aae004f8fec6434b4`; each retains its own
identity and neither may substitute for the other. The decoded workflow is
validated against the raw-pinned closed semantic workflow contract, including
trigger, read-only permissions, exact parity jobs/dependencies/conditions,
environment, timeouts, SHA-pinned actions, exact commands, and artifact
names/paths/order. The normalized authority is the complete parity subgraph
plus top-level trigger/permissions; swapped jobs/actions, checkout option drift,
or extra parity edges cannot disappear during projection. Substring sentinels or bypass fields fail
`PARITY_WORKFLOW_MISMATCH`.
The exact artifact is streamed and digest-checked using GitHub's
`sha256:<hex>` identity, then boundedly extracted. Canonical package verifiers
consume only those extracted asset bytes; metadata agreement without artifact
bytes is insufficient. The candidate source executor is independently bound
to the same protected implementation head/tree. The artifact
contains exactly the existing canonical six files: Windows ZIP, SPDX SBOM,
provenance sidecar, `RELEASE-NOTES.md`, candidate manifest, and versioned
`assets.sha256`. Admission does not introduce a parity-specific package format.

Each shortened TP route consumes the already-passed ordinary exact evidence
for its declared `fullRouteId`; it cannot provide another baseline/candidate
full receipt pair. The transitive row binds the canonical digest of that exact
row and uses its exact receipts and outputs for both prefix comparisons.

Authority transfer has one closed binding commit. It is the direct child of the
firmware-executor implementation head and may change only
`allowedBindingChildPaths`. The production verifier requires the package source
to be that exact binding commit and reads its Git parent, exact path diff, all
four authority-tree ids, and canonical policy bytes. An extra path, later
descendant, wrong parent, authority drift, or policy drift is
`PARITY_AUTHORITY_MISMATCH`. Terminal evidence is produced and retained by the
protected v1.0.0 workflow; it is not committed through a second source commit.
This chain transfers the byte-parity conclusion only when all four authority
tree ids and the canonical policy digest remain identical. It does not
transfer package certification: the final release ZIP retains its independent
closed-package, hash, SBOM, provenance, smoke, and release-owner gates.

Firmware executor authority and package source authority are separate facts.
The v1.0.0 package uses the exact binding commit while its firmware executor is
the immutable implementation parent. The v1.0.1 upgrade-test package uses the direct
single-parent child of the immutable v1.0.0 tag and that commit changes only
`VERSION`; the normal version-only lineage, package, startup, and upgrade gates
are sufficient. It never reruns or relabels the v1.0.0 parity executor.

The committed result contains only route/scenario identities, ordered input
sizes and hashes, receipt and report hashes, artifact lengths and hashes,
package and Git authority identities, comparator identity, and pass/fail facts.

Firmware-owner approval is a separate payload-free attestation verified by an
independent firmware-owner verifier. The protected `firmware-parity` GitHub
environment sequences the review job, but a deployment status, its `creator`,
and an empty environment payload are not firmware-owner identity. Its canonical
binding covers the plan and policy digests, implementation head/tree,
candidate ZIP and candidate-manifest/artifact digests, the complete 64-route
evidence digest, the exact receipt-set digest, authorized operator logins, the
approver identity, and approval time. One exact protected-main workflow run
uploads the provisional comparison before its pinned parity-attestation job
waits for environment approval. That job downloads the same-run comparison
and may upload the unique attestation only after the independent verifier emits
a verification record bound to the attestation bytes and every certification
digest. Finalization verifies that external record. Through one injected
GitHub reader it also reconstructs the workflow Contents bytes, run, job,
deployment, successful deployment status, and all three downloaded artifact
archives. It requires one repository id/head-repository id, main ref/head,
an in-progress finalizer run or completed successful run, a successful
attestation job, exact run attempt, job-linked status log URL, ordered
timestamps, and artifact `workflow_run` owner ids/branch/head to agree. GitHub
run/job, deployment-status, and artifact facts remain auditable sequencing evidence,
not a substitute owner signature. If the external verifier or record is absent,
the result is `PARITY_OWNER_APPROVAL_REQUIRED`. Cross-run/head/job artifacts
and arbitrary local attestations fail closed. The protected `release`
environment remains independent.

Operationally, the firmware owner downloads the completed same-run comparison
while the attestation job is waiting on `firmware-parity`, independently
creates the bound attestation and verification record, installs their base64
bytes plus the verifier id in that protected environment, and only then
approves the job. Premature approval is recovered by setting the missing
values and rerunning the failed jobs; evidence is never edited into an existing
artifact chain. This is a human approval workflow, not repository automation
that manufactures an owner signature.

Compare and finalize are separate contracts. Compare produces a deterministic
provisional 64-route document without requiring prior approval. Finalize
consumes that exact document and a subsequently approved owner attestation;
only finalized evidence may carry terminal `pass`.

The release gate accepts only a complete passing evidence report bound to the
exact certification-plan digest, canonical-policy digest, official baseline
executor-contract/CLI digest, release-reference digest, and reviewed candidate
identity. Evidence from a different
plan, policy, candidate, package, or incomplete route set cannot be reused.

## Consequences

- v0.9.16 parity becomes an independent, reproducible release gate rather than
  an inferred statement from runtime tests.
- No firmware payload, generated output, credential, or private path is
  committed by the comparator.
- The 11 shortened routes remain first-class Supported routes only when their
  full counterpart, both TP-prefix equalities, and immutable-tail proof pass.
- A route or same-scenario mismatch is evidence to investigate. The expected bytes are never
  regenerated or weakened to match the candidate.
- Firmware-owner and release-owner approval remain R3 human gates even after
  automated parity succeeds.
- The protected-environment workflow does not itself identify the firmware
  owner. Terminal promotion remains blocked until the independent
  firmware-owner verification integration is admitted.

## Non-goals

- No profile, range, processor, CRC/header, naming, or firmware operation
  changes.
- No GUI automation and no claim that the published desktop package contains a
  CLI.
- No representative-case shortcut. All 53 predecessor complete-output
  scenarios and all 64 candidate scenarios must execute before terminal
  certification; transitive reuse is limited to each row's declared full
  counterpart.
- Output naming is not part of the predecessor byte-parity claim; current
  canonical naming contracts and tests remain its sole authority.
- No claim that synthetic fixtures replace the per-route source-baseline/current
  comparison or firmware-owner approval.
- No silent substitution of the later NT51951 hotfix commit for exact v0.9.16.
