# Repository Verification Report

Status: historical seed-preparation report for the 2026-06-25 bootstrap
baseline, updated through the stable 0.10.4 post-refactor simplification,
0.10.5 startup-performance work, the 0.10.6 internal managed-version
implementation, the 0.10.7 final-effect checkpoint, the stable 1.0.0 and
version-only 1.0.1 release, the stable 1.0.2 mode-selector hotfix, the 1.0.3
page-publication hardening milestone, the 1.0.4 managed Setup and Launcher
source milestone, the 1.0.5 read-only recovery-diagnosis milestone, and the
1.0.6 tag-only integration milestone, and the 1.0.7 formal Distribution
Launcher release, through the 1.0.8 unpublished Windows release candidate and
the published 1.1.0 manual-only Windows release, the v1.1.1 verification,
test, CI, and release-architecture candidate, and the v1.1.2 support-neutral
repository, selector, evidence, and DPCMI candidate.
Current
verification evidence is produced by the canonical
`python scripts/verify.py --structure-only` and `python scripts/verify.py --all`
commands.

Specification package version: `1.1.2`

## 1.1.2 published release closure

v1.1.2 was published from PR #419. The reviewed head was
`2f016814ff4401a4928f7da2ecfcf4f88aa9c362`; the merged source was
`706ecaa9c4bd877ad2a960fd755efa4425c6335a`, with tree
`0b6760924007476545f87ad7dcb07669d0fd8c66`. CI run `33904444021` passed all
required checks. Release run `33905351419` completed candidate and promote
successfully; its annotated tag object `ce0d572ddefa042d795e42572ffc56db6d629865`
peels to the merged source. The repository's public `v1.1.2` Release was
published at `2026-09-04T18:51:27Z` with exactly 10 assets.

Release closure evidence passed: release notes are exactly 6335 UTF-8 bytes,
anonymous source zip/tar requests returned 200, downloaded sizes and GitHub
digests match, outer and Launcher checksums match, candidate identity matches,
and the fresh-download smoke command with `-SkipUiLaunch` passed. This does not
claim a clean-machine or visible-UI smoke result, signing evidence, or private
Golden publication.

The final DPCMI checkpoint retains exact 4/4 targeted coverage: Standard
NT51919/NT51929/NT51932 resolves solely from CMD1 Page 0 `[0x401A,0x401D)`, AB
keeps `[0x401A,0x401D)` and `[0x4401A,0x4401D)`, and General has no DPCMI reader.
The intended naming correction is `D0200` to `D2004`; the NT51929 Golden output
SHA and output bytes remain unchanged. The accepted code-size artifact has
focused 19/19 coverage and measures full production `136413` and runtime
`98559`.

The workflow job `release / published-smoke` was skipped despite candidate and
promote success: its implicit default `success()` inherited the intentionally
skipped v1.x v0916 dependency through promote, while promote has an explicit
condition and published-smoke does not. Follow up by adding an explicit direct-
prerequisite condition requiring candidate and promote success and not
cancelled, with regression coverage. v0916 jobs remain intentionally skipped
for v1.x.

## 1.1.1 verification and release-architecture candidate

This candidate changes only repository verification, test evidence, Windows CI,
and stable-release admission. It closes the v1.1.0 shadow-root and long-running
history failure modes, replaces wall-clock-sensitive test behavior with bounded
deterministic evidence, preserves one canonical verifier, and centralizes fresh
read-only GitHub admission collection at the candidate, pre-tag, and
pre-Release boundaries. Firmware behavior, supported routes, profiles, output
bytes, UI workflows, and Version deployment are unchanged.

The last local `python scripts/verify.py --all` run passed structure and the
complete Python/worker lane. Its .NET lane completed build and 5,282 tests with
zero failures and two expected Windows skips before reaching the fixed
600-second lane ceiling; Golden Regression and Architecture had not started.
The exact same source head passed the canonical protected `core` shard that
contains Domain, Application, Infrastructure, Profile Contract, Golden
Regression, and Architecture, while the separate Bootstrap and UI shards also
passed. This is exact-head coverage evidence, not a claim that the timed-out
local full run was green.

Post-merge main CI run `33536867503` at that same H4 later failed one
Infrastructure test:
`FileSystemVersionManagerWriteLeaseTests.WindowsAbandonedProcessReleasesWriterForRestartConvergence`.
Its helper marker did not appear within the test's ten-second wait and the test
ended with `TaskCanceledException`. The same H4 passed the test in PR CI run
`33534650943` and in the local full-verifier Infrastructure project. This is one
intermittent observation with an unconfirmed root cause, not a correctness fix
or a green post-merge run; a new exact-head protected CI result and the formal
release workflow remain mandatory.

Formal publication remains gated by a new exact-head PR/main CI result, the
stable workflow's full verifier and package smoke, active main/tag protection
with no bypass actors, zero unresolved P0/P1 findings, protected release-owner
approval, an immutable annotated tag and Release, the exact ten-asset set, and
fresh-download digest/source-archive/package-smoke verification. No tag,
Release, asset, or deployment is claimed by this candidate section.

## 1.1.0 published manual-only Windows release

This release preserves the frozen v1.0.8 Application behavior and changes only
its distribution boundary. Release-manifest schema 1.3 declares
`distributionMode: manual-only`; the package contains no Launcher, Setup,
Bootstrap, Catalog/Registry handoff, automatic update, Version deployment, or
packaged reference/Golden evidence. The managed Version verifier rejects it
fail closed.

### Stable identity and reviewed-tree equivalence

The annotated `v1.1.0` tag object is
`0aaf18b31f8786a38ab05d9bfc7c3bb98b83b685`. It peels to remote `main`
commit `40cf3be5162b3c5b9c266654b4993bc5b9669b5e`, whose tree is
`d5375f30833389bc81f45c3033d0d9a3e17cfc52`. PR #406 reviewed head
`3af68461eb7d9d62c773eddc79ffeeb46b5cac98` has that same tree. PR CI run
`33461699209` and post-merge `main` CI run `33462718850` both completed green.

The published stable Release is tag `v1.1.0` in the canonical NVT FW Combiner
repository.
Repository policy prohibits moving or replacing its stable tag or assets, but a
2026-09-01 GitHub API check reported `immutable: false` for the Release. The
platform therefore does not currently enforce the immutable-Release claim.

### Published assets and source archives

The Release has exactly these three uploaded assets:

| Asset | Size (bytes) | SHA-256 |
| --- | ---: | --- |
| `NvtFwCombiner-v1.1.0-win-x64.zip` | 78,148,699 | `acb8cd8777f055b8594d361f4c0d73c2a7c6845486e6fa8d10347869f329234a` |
| `NvtFwCombiner-v1.1.0-win-x64.spdx.json` | 105,758 | `7c54d6679e060b65c563c25e22e6c1011615afcc270c0c61b781ae91c2a9c7f4` |
| `NvtFwCombiner-v1.1.0-win-x64.provenance.json` | 36,500 | `6bf9638d3a71ec4d32620ce2b2003c2675c578fffda68fb80f3e0f97dddec223` |

The GitHub-generated source ZIP and source tarball each returned HTTP 200.
They are source archives, not product assets.

### Provenance, smoke, and release notes

The published provenance records source commit
`40cf3be5162b3c5b9c266654b4993bc5b9669b5e`, tag `v1.1.0`, builder
`scripts/package.ps1 manual-only operator build`, and RID `win-x64`.
Fresh downloads matched all three hashes. Closed-package smoke and visible
Application startup passed from the fixed `D:\NvtFwCombiner-TestArea` test area
on the current Windows host; this is not a clean-Windows claim. After LF
normalization and outer trimming, the rendered release-note content exactly
matches the equivalently normalized published body; that normalized content has
SHA-256
`2dd77e5715cf8408ba592b59c4b8ba6d8715be649a6013a73c1e66b518ebd67e`.
The exact fresh-download invocation transcript, session/result identity, and
worker self-test result are not preserved in a durable external attestation;
the same-host observation therefore cannot replace a terminal release gate.

### Retry, waiver, review, and residual record

The first long-path local package attempt failed before build. The short-path
retry passed. Release run `33454068996` belongs to the frozen `v1.0.8`
predecessor evidence and failed on shadow-output repository-root discovery; it
was not a `v1.1.0` retry. The local command
`python scripts/verify.py --structure-only`, run from
`C:\Users\liusx\.codex\worktrees\1100\nvt_fw_combiner` between 09:21:50 and
09:31:50 Asia/Taipei, reached the 600-second capability-history timeout. That
local timeout is distinct from post-merge `main` CI run `33462718850`, whose
structure lane passed. `python scripts/verify.py --all` was not run. The
managed release workflow and clean-Windows profile-load, synthetic
Preview/Build, and report-generation checks were waived for this single
release by
[`REL-110-FULL-VERIFY-OWNER-WAIVER-01`](../governance/waivers/REL-110-FULL-VERIFY-OWNER-WAIVER-01.md).
Those gates are not claimed green, and their historical JSON record and dated
waiver remain unchanged.

The release is published, but its terminal closure is **not fully green**. A
2026-09-01 live GitHub check returned `protected: false` for `main` and an empty
repository ruleset list. The mandatory protected-main gate in the REL-110
terminal contract was therefore not proved and is not covered by the waiver.
Together with the Release API's `immutable: false` result and absence of a tag-
protection ruleset, this is an unwaived historical release-closure failure, not
a passed gate or an ordinary deferred optimization. The currently observed tag,
tree, asset count, sizes, and digests remain exact; platform enforcement is the
missing fact.

PR #406 Codex review raised three P1 observations. The evidence-ancestry
observation is technically dispositioned as a false positive: reviewed head
`3af68461eb7d9d62c773eddc79ffeeb46b5cac98` has parent
`c24ea50a1e5d59d6aa55250553b0f1f9fbaea7f2`, which has parent
`ab1629a141b759aaa312b65cf3e36654ca97b7d0`; `git merge-base --is-ancestor`
returned 0 for both ancestors against the reviewed head, and post-merge `main`
CI run `33462718850` passed structure/policy. The GitHub review thread remains
unresolved, so technical disposition is not represented as platform closure.
Two substantive P1 residuals were open at publication: stale user-facing
`At 1.1.0` DP retirement-decision text and the fact that non-`ManualOnly`
package invocation for semantic version `1.1.0` is not globally fail closed.
This roadmap rebaseline corrects the active DP wording to owner-unallocated;
that documentation correction does not retroactively make the `v1.1.0`
release closure green. The non-`ManualOnly` invocation guard remains open.

`v1.1.1` must add a fail-closed pre-tag release preflight that verifies the
exact remote-main SHA, active branch protection/ruleset, required checks, and
zero unresolved release-blocking P0/P1 findings, with protected and unprotected
fixture coverage. It must also reject `1.1.0` package invocation without
`ManualOnly`. Post-publication verification must inspect tag peeling, Release
flags, exact assets, and digests, and must never generate an immutable claim
when the GitHub API reports `immutable: false`. The same milestone explicitly
owns measured CI-flow optimization across PR, post-merge `main`, package
preview, and release-candidate lanes while retaining the one canonical
verifier, bounded independent-lane concurrency, isolated logs, and a serial
fallback.

Two additional observed flaky tests are `v1.1.1` diagnosis inputs, not failed
publication gates. In run `33396845258`,
`ManagedLauncherEntryCoordinatorTests.CandidateTimeoutThenRollbackReadyCompletesWithinOuterBudget`
failed after 107 ms at assertion line 405 with expected `LaunchInstalled` and
actual `TerminationUnconfirmed`. In run `33460733265`,
`ReplicatedUpdateSourceRegistryTests.ConcurrentTimeoutsKeepOnePhysicalReadPerReplica`
failed. Both tests passed in green runs `33461699209` and `33462718850`; their
intermittent observations and root causes are not claimed closed.

## 1.0.8 unpublished Windows release candidate

This candidate preserves firmware behavior while adding strict Catalog v2
`manual-only` / `notify` policy with Catalog v1 fallback, a bounded Windows
update-source deployment wrapper over the existing Catalog publisher,
Windows-only CI, and fixed-test-area confinement for canonical verification.
The approved package snapshot contains 25 direct Golden cases and nine self-
contained fact-scoped aliases. Fresh preference state now selects Light while
persisted theme choices remain authoritative, and the Launcher aligns its
existing typed Setup progress presentation.

Focused tests and independent reviews cover the Catalog v1/v2 contracts,
publisher mutation ordering, deployment-wrapper custody and failure paths,
fixed-area confinement, Windows CI projection, canonical Golden package
policy, Light preference behavior, and Launcher layout. These are scoped
implementation evidence, not formal release authorization.

Frozen-head `python scripts/verify.py --all`, final package and exact asset
closure, visible and clean-machine smoke, hashes/SBOM/provenance, the real
installed v1.0.7-to-v1.0.8 Catalog-v1 canary, staged Catalog-v2 canary,
protected release-owner approval, immutable tag and GitHub Release, and fresh-
download verification remain mandatory gates. No completed canary, package,
tag, live Registry cutover, or publication is claimed here.

## 1.0.7 formal Distribution Launcher release

The release preserves all firmware behavior while closing the managed
Launcher delivery path. Normal entry retains a 250 ms local state/root health
cutoff and uses a bounded five-second operation budget for exact Root Bootstrap
and version Launcher hashing plus process admission. First installation starts
its independent 30-second admission budget before promoted Bootstrap custody
acquisition. Packaged smoke reads terminal typed UI diagnostics and waits for
the exact managed process paths before offline relaunch and cleanup.

The current source ledger is 136,056 production lines and 98,226 runtime lines:
Domain/Profiles 20,632, Application 41,951, Bootstrap/CLI/Desktop host 5,039,
and Infrastructure/Contracts/CRC worker 30,604. Final evidence still requires
two consecutive exact packaged install-to-READY/source-offline relaunch passes,
fixed-head review, immutable tag, and protected publication.

## 1.0.6 manual-only integrated Setup/Recovery and Launcher delivery candidate

This unpublished candidate integrates the reviewed managed Setup, v1.0.5
read-only recovery diagnosis, and v1.0.6 explicit confirmed recovery
execution/composition with the canonical package entrypoint and a separate
closed five-asset Distribution Launcher delivery set. It is support-neutral
and changes no firmware route, profile, range, processor, integrity behavior,
output naming, evidence rank, or output byte.

The frozen code-size ledger is 134,149 production lines and 96,337 runtime
lines: Domain/Profiles 20,632, Application 41,047, Bootstrap/CLI/Desktop host
4,854, and Infrastructure/Contracts/CRC worker 29,804. These measurements are
source evidence, not release authorization. Final application and Launcher
package construction, exact asset/hash verification, clean-machine Windows
Setup/Recovery and Launcher smoke, signing/legal review where applicable,
protected publication, release-owner approval, and the stable tag remain
mandatory gates. No package, Catalog/Registry publication, release asset, or
stable v1.0.6 tag is claimed by this candidate section.

## 1.0.5 read-only managed Setup recovery-diagnosis milestone

This identity milestone records RECOVERY-105A, which was already present in the
v1.0.4 source tree. That reviewed owner provides one Application terminal
diagnosis across exact state, root, transaction marker, and Bootstrap/
Application/Launcher lifetime facts. Infrastructure reuses the sole strict
marker codec and stable no-follow custody; observation performs no mutation,
process start, source access, repair, cleanup, rebind, or migration. The v1.0.5
identity itself adds no runtime behavior.

Exact-head evidence passed 193 Application recovery tests, 51 focused
Infrastructure tests, the complete Infrastructure suite, Architecture 247/247,
and independent R2 semantic/governance review with no P0/P1/P2. This remains an
unpublished tag-only source milestone and creates no package, Catalog/Registry
row, release asset, support promotion, or firmware-byte claim.

## 1.0.4 managed Setup and Launcher source milestone

The committed candidate completes the secure managed first-install backend and
the bounded distribution Launcher source path. It reuses the existing typed
Application policy, Infrastructure adapters, immutable Bootstrap trust anchor,
strict payload descriptor, exact-root recovery probes, and shared repository
verifier; it does not add a second updater, parser, state writer, or firmware
execution path.

Focused evidence includes 182 affected real-process and repository tests, 943
passing Infrastructure tests with two declared platform skips on the isolated
candidate, complete Application and coverage-owner checks, structure validation,
and two independent exact-diff reviews with no P0/P1/P2 findings. A Windows
test-host race was closed by placing every inheritable-handle test owner in the
existing nonparallel process collection and by using test-local state identities
plus the canonical bounded temporary-workspace cleanup.

This is an unpublished tag-only source milestone. It creates no GitHub Release,
package, Catalog/Registry row, support promotion, firmware-byte change, or
delivery claim. Production inherited-handle containment, package/SBOM/
provenance/signing, clean-machine smoke, and integrated Setup recovery remain
open gates before Launcher delivery in 1.0.6.

## 2026-08-25 formal-support policy checkpoint

Commit `ed834f9c` reconciles canonical policy catalog `1.10.0` against an
independently frozen 89-route denominator and the current executable route
fingerprints. The 2026-08-25 owner decision publishes all 64 exact Standard
Merge, AB Merge, and CtrlRAM Replace routes as `Supported` and makes ordinary
authoring `Available`. All 14 DP Replace routes remain `Internal` and
`Unavailable`; published `v1.1.0` made no retirement-or-reopening decision, so
that decision remains owner-unallocated. General retains its existing ten
internal and one test-only routes.

Evidence remains a separate fact. The complete catalog contains 28 Direct
Golden, nine Approved Alias, five Synthetic Oracle, and 47 Contract Only
routes. This includes 11 exact TP-prefix CtrlRAM base routes. Original-TP
execution now proves NT51950 single, NT51951 single, and NT51951 two-IC can
build, but those outputs do not match the owner full-Flash expected prefixes;
the full prefixes contain DP-origin bytes and cannot stand in for independent
TP-only expected outputs. Those three routes therefore remain Supported while
their evidence is honestly Contract Only. Input-only multi-IC cases remain
Contract Only, and NT51929 DP is Contract Only because its retained direct
Golden case belongs to Standard Merge. No evidence type is inferred from a
filename.

The exact LF policy hash is
`6207923baf537c4031f2095942d363660c7a1c5cbd35e704ec14b28c509aef0f`.
The 26-bundle package trust index is `0.10.6.2` with SHA-256
`d680ef81830e3ad1870b8ef6bb79b5e6a251590301afcd0a400c9a9d3f4369b2`.
Focused local checks passed: policy loader 20/20, canonical host/route
convergence 7/7, Support Matrix projection 8/8, package-trust architecture
17/17, release-package policy 30/30, and the previously blocked CtrlRAM
ProductServices paths 3/3.

Follow-up commit `851ffad7` adds one strict route-evidence cross-link for every
exact policy route. The later TP execution reconciliation preserves that exact
89-route join while correcting the three evidence ranks above. Policy/manifest
comparison has zero missing, extra, kind, route-id, or fingerprint mismatches.

This remains neither full verification nor release authorization. Full
frozen-tree verification, independent R3/firmware review,
packaged/clean-machine smoke, signing, provenance, protected CI, and
release-owner approval remain open.

## 0.10.7 internal final-effect candidate

The owner approved hiding ordinary DP Replace authoring in `0.10.7` and the
initial `1.0.0`, while retaining its profiles, execution semantics, publication
and evidence decisions, fingerprints, and Golden regressions. Product UI/CLI
discovery is fail-closed through canonical policy catalog `1.10.0`. Published
`v1.1.0` made no retirement-or-reopening decision; that decision remains owner-
unallocated. This is not a firmware-byte or support promotion change, and no
package or tag is authorized by this report.

## 0.10.6 internal managed-version candidate

The internal candidate adds a stable launcher, content-verified side-by-side
versions, atomic activation state, one-use ready supervision, bounded rollback,
and the Settings Version experience defined by ADR 0051. Focused automated
evidence covers closed package/catalog/path validation, staged install and
failure cleanup, exact inventory damage, state corruption and atomic saves,
real process ready/exit/timeout behavior, update-source relocation, managed-root
mismatch rejection, offline switching,
rollback, explicit update consent, and non-stacking confirmations.

The owner explicitly approved advancing this internal identity on 2026-08-21
after the original independent reviewer could not be scheduled. A later
independent R2 review failed the original candidate and two correction
checkpoints. All reported findings, including the final ZIP64 arithmetic bound
and seed-import lease regressions, are corrected. Fresh independent R2 review
of exact HEAD `248ab804` passed on 2026-08-22 with no P0/P1/P2/P3 findings.
No 0.10.6 tag or public package is authorized; production signing, provenance
migration, clean-Windows smoke, and release/security approval remain gates for
the first public `1.0.0`.

The latest frozen correction tree passes `python scripts/verify.py --all`:
Python 391 with four platform skips; CRC worker 30/30 with 100% line/branch
coverage; and .NET 3,834 total with 3,832 passing plus two platform skips,
including Application 673, Infrastructure 550 plus two skips, Bootstrap 974, UI
smoke 604, Architecture 216, and all 17 Golden regressions. The exact reviewed-
HEAD rerun measured aggregate .NET coverage at 88.82% lines and 79.06%
branches. Fresh local-folder evidence passes
catalog/package install, update-source relocation, 0.10.6 activation, offline
switching, rollback/deletion guards, and the freshly published stable launcher.
An installed managed-root relocation now fails closed; adopting a new root
requires a future explicit verified rebind transaction.

## 0.10.5 unified preload lifecycle release candidate

The `0.10.5` integration identity starts from stable `v0.10.4` and implements
the owner-approved bounded lifecycle specification in ADR 0049. PL-01 through
PL-07 are complete. PL-00 retains the frozen-tree verifier, Golden, package,
performance, accessibility, CI, provenance, protected-workflow, and release-
owner gates; no tag or stable asset is admitted before those gates close.

The implementation is support-neutral and changes no profile, schema, report
wire, processor protocol, Golden fixture, expected BIN byte/hash/name, or stable
`v0.10.4` asset. Exact release evidence is appended only after the corresponding
frozen-tree and external human gates pass.

## 0.10.4 post-refactor simplification and startup-performance stable release

The candidate retains the complete `v0.10.3` canonical execution architecture
while making the meaningful Home window visible before catalog publication,
streaming request-scoped route progress into an input-blocking accessible
loading surface, repairing Dark-theme and reduced-motion presentation, and
removing further evidence-backed duplicate runtime paths. It remains support-
neutral and changes no profile/schema version, firmware authority, expected BIN
byte or SHA-256 result, report wire, output naming, CRC success payload, or
external-processor protocol.

The exact production package before final integration passed closed-package
smoke and measured five scored Home launches between 665.410 and 695.633 ms
with a 687.180 ms median after one unscored warm-up; the separately recorded
first cold launch was 1040.087 ms. The complete verifier passes with 3,349 .NET
tests and two declared platform skips, Python 378 with four declared skips, CRC
Worker 30/30, and all
17 existing BIN golden regressions. Full production is 97,306 nonblank lines
and counted runtime production is 67,186; the exact four runtime slices remain
20,619 / 29,383 / 3,255 / 13,929.

PR #356 reviewed head `c808e9eb958b433665b62c8bcd3f2e29818059de`
passed two fresh complete CI attempts within five minutes and merged into
`0.10.x` as `8921fa6c0a21af2a1766a491d47118072f711026`.
The reviewed and merged trees both equal
`f60ddfadef87736b2bb1fbc7d99e0230be5f52d9`. Final protected-`main`
integration PR #369 then merged the final reviewed tree to protected `main` as
`8c3cc51dc95cea2fbae8ec5ac0287db730d1b37b`; its tree is
`48ee0fd87d942674acf83c910de78681db748458`. Exact-main CI passed, and release
run `31623880051` attempt 2 completed deterministic package regeneration,
candidate smoke, immutable promotion, five-asset verification, and downloaded-
package smoke. Stable tag and Release `v0.10.4` are published from that exact
commit and tree.

The exact downloaded ZIP also passes visible local smoke. Its two controlled
post-publication startup observations measured 777.090 ms and 806.930 ms
medians, so the official-package repeat did not reproduce the 700 ms absolute
gate. In the same current environment, the unchanged prior 656.444 ms control
package measured 782.368 ms; this shows an environment-wide slowdown and no
evidence specific to the final queue correction, but it does not convert the
absolute miss into a pass. The stable tag remains immutable, and the startup
residual is recorded in
[`v0.10.4-startup-700ms-evidence.md`](v0.10.4-startup-700ms-evidence.md).

## 0.10.3 canonical architecture and legacy-retirement release candidate

The candidate advances the stable line from `v0.10.2` through Canonical Core
Convergence and LAR-01 through LAR-12. Standard Merge, AB Merge, General Merge,
DP Replace, CtrlRAM Replace, and General Replace now use one Application-owned
accepted-session path for authoring, naming, execution, reports, delivery, and
processor planning. Presentation and CLI consume the same typed use cases;
Bootstrap contains four composition-root and lifetime files. The migration-era
Workbench graph and renamed parallel production owners are absent.

The release is support-neutral. It changes no profile/schema version, built-in
firmware authority, expected BIN bytes or SHA-256, report wire, CRC success
payload, or Legacy Combiner protocol. Both canonical full-verifier modes pass
on the frozen source snapshot, including all 17 existing BIN golden
regressions. The runtime metric is 67,433 nonblank lines and full production is
96,044; exact slice ratchets and slice-sum closure pass. The final integration
commit and tag must still be identical to the reviewed PR/main tree and pass
protected CI, deterministic Windows x64 packaging, release-owner approval,
five-asset verification, visible clean-Windows smoke, and downloaded-package
smoke before `v0.10.3` publication.

Integration PR #354 merged reviewed `0.10.x` head
`b99ffff480a3b0c14aa8a6f28fc418c066431026` into protected `main` as
`9ad1b02eff0f13317db8f0a8e78d1ffc74f36927`; the precomputed and merged tree
both equal `89ef80bbf5c9e70c2b512d71627b5043ba07fbd6`. PR merge-ref CI run
`31446543768` and protected-main CI run `31447003029` passed policy/Polytail,
Python/worker, and .NET build/test gates without retry. The main-only difference
from the reviewed `0.10.x` tree is the already-reviewed v0.9.19 maintenance
release-policy admission. This report-only closure changes no runtime source,
profile, schema, expected golden value, or package payload; its exact reviewed
main merge commit becomes the stable release-workflow source.

## 0.10.2 canonical desktop-adoption release candidate

The candidate advances the stable line from `v0.10.1` through reviewed PRs
#279, #281–#287. All six desktop Merge/Replace workflows consume the canonical
Application readiness/inspection and exact-compilation contracts; the main
window is a shallow shell; Raw Hex Editor, Report Diff, and BIN Inspector share
one read-only Hex viewport; and superseded General/Saved Rule v1 production
owners are deleted. NT51950/NT51951 DP Replace requires exact selected Base
capacity. Support, publication, and golden status remain unchanged and
explicit.

The exact integrated product tree before version/release documentation is
commit `235db183804ba097ebb26a002d0365aaedce910a`, tree
`21a17fb7b5439bb1f1b411056ca99044c00386a5`. The release candidate must pass
the canonical full verifier at its frozen version-branch head, exact-main PR
CI/review, deterministic Windows x64 packaging, visible local startup smoke
where a desktop is available, protected-environment promotion, exact five-asset
verification, and downloaded-package smoke. The canonical runtime metric is
75,638 nonblank lines. Remaining deferred UI, Workbench deletion, Core
convergence, and the hard 44,000-line #197 gate are allocated to `v0.10.3`, not
claimed by this release.

## 0.10.1 headless canonical foundation release candidate

The candidate integrates the reviewed `0.10.x` branch into the protected-main
lineage after #194 completed all 78 admitted headless routes. Canonical
capability resolution, exact compiled-composition identity, isolated authoring,
General/Saved Rule v2 behavior, typed metadata, NT51928 dual capacity,
NT51929-family symmetric AB, NT51950/NT51951 DiffNF preservation and AB routes,
and retirement of NT51920/NT51925/NT51930/NT51931 are included. Publication and
evidence state remain independent; unavailable direct AB and NT51928 goldens
stay explicit and no support certification is inferred.

The release candidate must pass the canonical full verifier at its frozen
version-branch head, exact-main pull-request CI and review, deterministic
Windows x64 packaging, visible local startup smoke where a desktop is
available, protected-environment promotion, exact five-asset verification, and
downloaded-package smoke. Private-runner golden evidence and organizational
license/legal approval remain separate attestations rather than inferred CI
results. The published release notes record the 72,196-line runtime metric and
the deferred UI/deletion/Core-convergence scope.

## 0.10.0 maintainability planning-release candidate

The candidate is a support-neutral documentation, governance, terminology, and
evidence-reference release. It records the IC-first architecture, the initial
dependency graph (later extended by explicitly approved tickets), ADR lifecycle handling, and the
owner-updated FlashMap workbook provenance. It does not change production
firmware behavior, supported routes, processor authority, IC promotion, or
golden outputs.

The final PR/main candidate must still pass protected CI, exact-head independent
and Codex review, package/provenance generation, downloaded-asset verification,
and release-owner approval. Visible clean-Windows UI smoke and release-workflow
annotated-tag newline hardening remain explicit later `0.10.x` work; this
candidate does not describe either as passing.

## 0.9.16 CRC/header hot-fix — published stable predecessor

The `0.9.16` branch starts from official stable `v0.9.15`, peeled to
`008333a9c96ea65454a334824d349f3574373edd`. Feature PR 168 reviewed head
`a275d61a95e74b729e9cc6ec26580524db1c363f` and version-branch merge
`f0dd1cf222a468d4ae10ec08b89cdffaba82e3ed` share exact tree
`1da8c2781fc4b0c90ca24a1e471b26807d9cf857`. Exact-head Codex review found no
major issues and the feature PR's policy/Polytail, Python, and .NET checks
passed.

The hot-fix authorizes only classified CRC/header write words, keeps
cascade-only DLM CRC authority out of single-IC profiles, corrects AB Memory
coverage and independent Replace device context, and skips DP metadata
inspection for TP firmware. The direct NT51929 single Andes golden locks four
Header/Header Copy CRC words, 16 changed bytes total, production-route output
SHA-256
`b426125b966901ee8a0efc49ec598ebb7a6641a4391cc0f7c122d764f9f8464f`,
and unchanged inputs. The private evidence is not selected for release
redistribution and does not promote runtime support.

Local `python scripts/verify.py --all` passed at version-branch code/test head
`9584defc21e9288a3629aaf16fcda9bcd415f704`: 158 repository Python tests,
28 CRC-worker tests at 98.88% coverage, Domain 361, ProfileContract 359,
Application 222, GoldenRegression 19, Architecture 106, Infrastructure 273
with two declared Unix-only skips, Bootstrap 752, and UI Smoke 351. Release
build completed with zero warnings and zero errors. The historical release was
published as stable `v0.9.16`; the visible clean-Windows UI smoke and
annotated-tag newline comparison defect remain explicit later `0.10.x`
follow-up work and are not described here as passing.

## 0.9.15 stable release baseline

The `0.9.15` candidate is derived from the official `v0.9.14` release tag,
peeled to `9b15d8757ccb44167c471ca4e602036066bcdea9`. It opens the declared
AB Code routes through the shared executor: NT51919/NT51929/NT51932,
NT51950 `1 IC`/`Cascade`, and selector-free NT51951. Its output identity,
topology confirmation, TPB-only staged postbuild, direct-golden debt ledger,
and read-only delivery-to-review collector are part of the candidate scope.

Stable tag `v0.9.15` peels to
`008333a9c96ea65454a334824d349f3574373edd` and is the immutable baseline for
the `0.9.16` hot-fix. Its function availability remains distinct from
support-certification: direct AB golden and firmware-owner promotion debt stay
explicit for the affected routes.

## 0.9.14 AB pilot and CI-owned release candidate

The `0.9.14` branch starts from stable `v0.9.13`/`main` commit
`f9f8dbcd979ecdef43f432016787e57763819492`. It adds the separately gated
NT51919/NT51929/NT51932 AB pilot through the shared composition engine, typed
load diagnostics, the minimum AB authoring UI, IC details, targeted interaction
fixes, and protected CI-owned release promotion. Exact-head canonical
verification, packaged Release EXE visual/startup checks, independent local and
GitHub Codex review, firmware-owner confirmation, and release-owner approval
remain required before stable publication.

## 0.9.13 support-neutral stabilization release candidate

The `0.9.13` branch starts from the exact latest `origin/0.9.12` head
`c34c45f7df06bdc5552db6b8157c2da11922d298`. Its scope is limited to the UI,
file-reveal, TP-version naming, OneDrive diagnostic, terminology, and release
fixes listed in `CHANGELOG.md`; it changes no firmware execution authority or
support stage. Canonical verification, exact candidate packaging, visible
package smoke, Release-EXE startup measurement, and independent exact-head
review are required before handoff.

## 0.9.12 CtrlRAM routing and interaction-stabilization release candidate

The reviewed feature head is
`34e97dfb73f261fb820ac34b9fd4aabaf5d45c1b`. Pull request 156 merged it into
the `0.9.12` integration branch as
`bf3805ab445dc0d1c066b5888a9c6d4d50d12086`; the merge tree
`f9fe776e4078a8cb2aa2fe57fafad6f81a49cd54` exactly equals the reviewed
feature tree. Thread-aware review found no unresolved comments, exact-head
Codex review found no major issues, and independent local review reported no
P0-P3 findings.

Clean detached `python scripts/verify.py --all` passed at the reviewed head:
123 repository script tests, 28 CRC-worker tests at 98.88% coverage, and 2,221
.NET tests passed with two declared Unix-only skips. Release build, formatting,
analyzers, profile/golden inventory, fixture validation, Polytail, and all three
protected feature CI jobs passed. A real NT51926 cascade Build is byte-identical
to its manifest expected output; that one case is evidence, not support
promotion for every routed plan.

The release remains support-neutral. Per-plan firmware-owner evidence is a
future support-promotion gate, not a claim or blanket blocker for this release.
Exact final-`main` CI, portable packaging, clean-machine smoke, annotated tag,
published hashes/SBOM/provenance, and downloaded-asset verification remain the
stable-release gates.

Repository visibility was reviewed before final integration. The owner decided
on 2026-07-22 that the repository remains Public through stable `v1.0.0` and
becomes Private afterward; the full impact and release inventory are recorded
in `docs/governance/v0.9.12-public-visibility-review.md`. This supersedes the
earlier post-`v0.9.11` schedule without claiming that a later privacy change can
retract public history, caches, or forks.

## 0.9.11 reconstructed stabilization release candidate

The candidate is reconstructed from the exact final `v0.9.10` predecessor
`b0266f312a67d644475731153b1af82f7eadcc95`; the premature integration lineage
is not release authority. Reconciliation retained the nine effective
`v0.9.10` behavior groups recorded in
`docs/governance/v0.9.11-baseline-reconstruction.md` and reapplied only the
reviewed DP/LDC authoring, startup/package, IC Number, topology-group, spacing,
and Build-rail intent.

The exact M3 package application commit is
`b73f8876ad3af41ec792141a929b866c59df4462`. Its self-contained compressed
composite-ReadyToRun EXE is 69,990,762 bytes with SHA-256
`bf5f7e3e5d035e9a1cd2dbeff85cb0d137aa20ca1a8270304c4f147d71fd707e`;
the 75,358,293-byte ZIP SHA-256 is
`36d18b6e03a6569b744187ab924f48860ce424103c12e649c618136dd520587a`.
Visible release smoke and the closed package/external-tool policy gates passed.

The same package also passed a 2026-07-21 visible UI inspection. It opened Home
without crashing and displayed `0.9.11 workbench`; Home-to-Replace navigation
retained the IC Number ComboBox. NT51950 CtrlRAM projected `Common` in single
mode and separated `DIFF CtrlRAM` into `Cascade` while NF/Normal/VN remained in
`Common` after selecting Cascade. Input and Output layout used the same groups,
the right-side memory visualization remained present, nested sections retained
their spacing, Base firmware displayed `FlashCode / TP FW`, and the disabled
bottom-right Build rail exposed its blocking reason without occluding content.
This inspection does not substitute for the owner, Narrator/NVDA, effective
high-contrast, scaling, or reduced-motion human gates.

One warm-up plus five exact-package Home launches measured a 908.146 ms
process-to-window median (901.461 ms minimum, 932.563 ms maximum). The recorded
first-frame synchronous UI median is 412.455 ms; background UI materialization
totals 53.388 ms with a 25.528 ms longest interval. Median working set is
220,708,864 bytes when the Home window appears and 286,752,768 bytes after
warm-up. The largest synchronous Home interval is root DataContext assignment,
which causes the visible template to instantiate and bind: 276.016 ms and
33,824,392 cumulative allocated bytes. No firmware/profile/user-file/processor
I/O occurs in that interval. These are same-machine observations, not universal
wall-clock or live-managed-heap claims.

UI Smoke `271/271`, Architecture `99/99`, release package policy `14/14`,
external-tool policy dry run, package construction, and visible package smoke
passed during M3. A clean short-path detached worktree at `c58711ce` passed
`python scripts/verify.py --structure-only`, including Polytail fast checks.

The exact code and metadata candidate `22f8f517` then passed the canonical
`python scripts/verify.py --all` gate in clean short-path detached worktree
`C:\n11v-all-22f8`. Release build completed with zero warnings and errors.
Repository policy tests passed `107/107`; Python CRC Worker passed `28/28` at
98.88% branch coverage. .NET results were Domain `357/357`, Application
`186/186`, ProfileContract `351/351`, Architecture `99/99`, GoldenRegression
`19/19`, Infrastructure `245/245` with two expected Windows platform skips,
Bootstrap `622/622`, and UI Smoke `271/271`. The CtrlRAM fixture manifest and
payload hashes also passed.

Independent R2/R3 and owner review, the existing `總代理` conversation review,
protected CI, reviewed-main packaging, clean Windows x64 execution without
separately installed .NET/Python, representative firmware UAT,
accessibility/visual review, signing, and immutable release publication remain
mandatory. This evidence-only follow-up does not change the verified executable
tree or self-approve those gates.

The owner classified `v0.9.11` as support-neutral on 2026-07-21. It promotes no
IC/workflow support stage. Existing golden, alias, same-build, and private
CtrlRAM evidence retain their documented scope; missing IC-specific direct
parity, including the separately recorded NT51930 DP Replace package, remains a
future support-promotion gate rather than a blanket `v0.9.11` release blocker.
No firmware behavior or evidence result is changed by this classification.

## 0.9.10 performance-remediation stable release candidate

Stable Node B is protected-main `v0.9.9` commit
`32c37e254271de507be49d0f5ef38faaa122dba6`; optimized production Node C is
`6f3698ddeb0ec50a9ca46057af21e93bfbebd55f`. The 130-commit performance stack
was replayed onto Node B with 130 equal range-diff patches and zero conflicts.
Byte-identical external-tool/golden trees, DP and NT51926 filters, physical
real-tool/headless tests, and command catalog/argv tests pass at both nodes.

One authoritative Build replaces the prior duplicated execution. Legacy
Combiner commands remain exact and sequential, while complete staging reads
change from `2C+1` to one final read plus only the evidenced selective-tail
short-output exception. The NT51926 two-command expected output SHA, the
10,000-difference output/JSON hashes, mutations, validation, issue, command,
and report facts remain unchanged. The release makes no support, profile,
range, CRC/header, processor-command, or product-golden promotion.

The frozen reviewed branch head
`87a84ecd1f03e7257a40ddaf0b5531a3e66aaf30` passed
`python scripts/verify.py --all` in a clean detached worktree in 186.8 seconds.
Release build completed with zero warnings and errors: Domain 357, Application
186, ProfileContract 350, Architecture 95, GoldenRegression 9, Infrastructure
245 with 2 platform skips, Bootstrap 608, and UI Smoke 237 passed; Python CRC
worker tests passed 28/28 at 98.88% coverage. Polytail and repository structure
validation passed.

The exact metadata-aligned package, clean-machine Narrator/NVDA and effective
Windows contrast checks, explicit firmware-owner R3 approval, required PR/main
CI, protected-main tag, and immutable GitHub Release verification remain
release gates. Development-package observations do not substitute for them.

## 0.9.9 legacy-convergence stable release candidate

The reviewed `v0.9.9` code milestone retires the production V1 composition
compiler after exact V2-route and fail-closed coverage. The constrained Legacy
Combiner executable and runner remain the sole legacy execution exception.
Production C#/AXAML is below the owner-approved 54,000-nonblank-line ceiling,
and runtime availability, golden verification, and product-support promotion
remain separate states.

Independent clean-tag verification of code milestone commit
`270e803e1f043ffd56d8568c7e80c7f771a35d7e` passed
`python scripts/verify.py --all` in 165.2 seconds with zero build warnings and
errors: Domain 351, Application 156, ProfileContract 350, Architecture 84,
GoldenRegression 7, Infrastructure 221 with 2 platform skips, Bootstrap 575,
and UI Smoke 131 all passed.

The subsequent canonical golden and external-tool consolidation was reviewed
through PR #147 and merged as
`0589ba3a644bba149d7f42b222bcde68efc52bb2`. Required CI run 29678730641
passed the .NET build/test, repository policy/Polytail, and Python verification
jobs. Independent R3 review reported no P0-P3 findings. The consolidation does
not change firmware bytes, expected outputs, runtime routes, profiles, package
allowlists, or support status.

The earlier `v0.9.9` tag incorrectly pointed to an internal milestone tree that
still reported package version `0.9.8`. The owner explicitly approved replacing
that tag only after this metadata-aligned tree passes review and required CI and
is merged to `main`. The `v0.9.9.5` tag remains an internal predecessor node and
is not stable package authority. The replacement `v0.9.9` tag must identify the
exact reviewed `main` commit and pass the canonical release workflow.

## 0.9.8 convergence candidate

The 0.9.8 integration branch is feature-frozen and support-neutral. It retains
the 0.9.7 firmware behavior and evidence gates while lowering the owner-accepted
production ratchet to 56,742 nonblank C#/AXAML lines, exact duplicate JSON to
1,156 lines, `WorkbenchCompositionService` to 4,483 lines, and
`MainWindowViewModel` to 2,847 lines. The portable Windows package remains
bounded by the reviewed 58,076,715-byte maximum. Canonical verification and
package smoke do not replace CtrlRAM owner golden outputs/sign-off, signing and
legal approval, protected remote CI, or clean-machine evidence.

## 0.9.7 integration candidate

The 0.9.7 integration branch combines the reviewed 0.9.6 lineage with the
fact-scoped AB evidence forward-port, compiled final-output validation and the
non-routed NT51926 CtrlRAM V2 candidate, and the audited semantic UI token
consolidation. Phase-local AB, Application, Domain, Bootstrap, Architecture,
and UI smoke tests passed on 2026-07-15.

On 2026-07-15, `python scripts/verify.py --all` passed with zero build warnings
and errors: 12 repository Python tests, 28 CRC-worker tests at 98.88% coverage,
Domain 345, Application 244, ProfileContract 362, Architecture 71,
GoldenRegression 6, Bootstrap 260, Infrastructure 180 with 2 Unix-only skips,
and UI Smoke 119. Polytail and the post-`v0.9.2` Conventional Commit audit also
passed; all 98 integration commits present before this evidence-only update had
clean phase-scoped subjects and no WIP/fixup markers.

This candidate does not promote AB or CtrlRAM runtime support and does not
replace the remaining firmware-owner, product-golden, signing, protected-CI,
or clean-machine release evidence. Stable package smoke is performed only from
the reviewed `main` commit, not from this pre-merge integration branch.

## 0.9.2 consolidation evidence

The 0.9.2 integration branch materializes each built-in V2 profile bundle from the content-addressed schema source inventory and removes the Standard Merge legacy runtime fallback. It retains the trusted loader boundary and all existing firmware behavior. On 2026-07-13, `python scripts/verify.py --all` passed with zero build warnings and errors: Python worker 28, Domain 335, Application 219, ProfileContract 347, Architecture 69, GoldenRegression 9, Bootstrap 192, Infrastructure 138 passed with 2 Unix-only skips, and UI Smoke 106. This local-verification milestone does not publish a package, promote IC support, or authorize AB Code behavior.

## 0.9.1 migration evidence

The 0.9.1 release branch retains the documented legacy comparison and golden evidence while routing the covered Normal/Standard Merge and NT51950/NT51951 DP Replace paths through the V2 family/map/profile compiler boundary. On 2026-07-13, `python scripts/verify.py --all` passed with zero build warnings and errors: Python worker 28, Domain 335, Application 219, ProfileContract 347, Architecture 68, GoldenRegression 9, Bootstrap 188, Infrastructure 138 passed with 2 Unix-only skips, and UI Smoke 105. This source-branch evidence does not establish packaged-install trust, IC product support, or AB Code behavior.

## Bootstrap assertions

- Distribution owner identity is `MSP/FW3`; the private source is identified as `urn:msp-fw3:nvt-fw-combiner:source`, under MIT.
- `global.json` pins .NET SDK `10.0.301` and installers consume that value.
- Avalonia packages are centrally pinned to `12.0.5`.
- Root `SPEC.md`, layered AGENTS, Codex configuration and nine skills are present.
- Replace experiences are DP Replace, CtrlRAM Replace and General Replace; Merge experiences are Standard, AB and General.
- `refcode/` contains exactly the two approved Python snapshots and their hashes remain validated.
- No production project references `refcode/`.
- Init node is `v0.1.0-dev.0` and does not claim firmware parity.

## Commands

```text
python scripts/verify.py --structure-only
python scripts/verify.py --all
```

The full command requires the pinned .NET SDK and Python worker development dependencies. Private golden and clean-machine release evidence are intentionally absent at init and remain milestone gates.

## Seed preparation evidence

Executed successfully on 2026-06-25 before the init commit:

- `python scripts/verify.py --structure-only` — repository structure, schemas, policy, source manifests, layered agent files and Polytail fast gate passed.
- `bash -n scripts/install-dotnet.sh scripts/bootstrap.sh scripts/verify.sh scripts/publish-github.sh` — shell syntax passed.
- `python -m pytest --cov=nfc_crc_worker --cov-branch --cov-report=term-missing` — 28 tests passed with 100% line and branch coverage.

Not executed in the seed-preparation container:

- `.NET restore / format / build / test`, because the pinned .NET SDK was not installed and the container could not download it. The repository installers and the `dotnet / build-test` CI job own this gate.
- Ruff, Pyright and Pylint, because those development modules were not present locally. The `python-worker / verify` CI job installs and runs them.
- Windows release packaging, signing and clean-machine smoke; these remain release milestone gates.
