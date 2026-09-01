# ADR 0062: Single-entry Launcher and first-run managed Setup

Status: Accepted for `SETUP-104-01`

Amends: ADR 0051 and ADR 0056 only for first-install distribution and entry
routing, plus ADR 0060 only for the managed Launcher/Bootstrap executable
safety ceiling. Their version-package, activation, rollback, and immutable
Bootstrap decisions otherwise remain authoritative.

Amended by: ADR 0064 explicit Windows child-handle containment.

## Context

The managed-version implementation already owns Registry replica selection,
Catalog and package admission, closed ZIP verification, installed inventory,
canonical seed import, durable root binding, stable executable custody, and
READY/LKG activation. The missing capability is a single entry that a user can
always open:

- when a healthy installation exists, open its committed version;
- when no installation exists, show Setup; and
- when installation facts are damaged or ambiguous, route to Recovery.

This must not become a second updater or a second implementation of managed
state. It must also preserve ADR 0056's deliberately small immutable Bootstrap.
Combining Setup UI, Registry/Catalog access, package installation, and recovery
into that Bootstrap would enlarge its trust surface and normal startup cost.

## Decision

The only user-facing entry is **NVT FW Combiner Launcher**, implemented by a
thin distribution Launcher host. Setup is a branch of that host, not another
user-facing executable. Internally, the existing immutable
`NvtFwCombiner.Bootstrap.exe` remains a separate trust role and the existing
version-scoped `versions/<version>/launcher/NvtFwCombiner.Launcher.exe` remains
unchanged.

```text
NVT FW Combiner Launcher
├─ healthy installed facts -> immutable root Bootstrap
│                              -> version Launcher -> Desktop
├─ root and state genuinely absent -> Setup branch
└─ damaged, moved, partial, foreign, busy, or unknown -> typed terminal state
```

Application owns the single entry classification and Setup use case.
Infrastructure supplies bounded state/root observations, stable executable
custody, package/root materialization, and process handoff. Presentation only
renders typed results. The three hosts never reparse Registry, Catalog,
package, state, or launcher semantics.

The public `NvtFwCombiner.DistributionLauncher.exe` is only a thin process and
composition root. Bootstrap constructs one shared state store, exact-root
probe, embedded-payload source, Registry/Catalog/package experience, root
materializer, and immutable Bootstrap handoff from `Environment.ProcessPath`
and the two release-owned Registry defaults. Downloaded delivery media uses the
deterministic `NvtFwCombiner` child of the executable directory while durable
bound state remains the authority for an installed or user-selected root. A
development build without both exact embedded resource logical names returns a
typed payload-unavailable terminal result; it never invents adjacent payload
inputs. The existing embedded-payload source is the sole descriptor parser and
projects both the descriptor-owned Launcher version and immutable Bootstrap
identity. The Application entry coordinator admits that projection inside its
one local-health budget and requires the declared version to equal the running
Launcher's informational version. The public host only wires resource stream
openers; it does not read, parse, hash, cache, or separately time payloads. The
initial public-host slice intentionally has no Setup UI, shortcut, or release
packager.
The conditional MSBuild resource inputs keep ordinary development builds
possible and fail closed at runtime; the later release-packager slice must add
the protected publish gate that proves both exact logical resources are present
before any delivery artifact is accepted.

### Entry classification

Every invocation first validates the embedded payload-admission descriptor,
then observes the canonical per-user state path and exact managed-root binding.
It returns one of these terminal intents:

| Complete observation | Intent |
| --- | --- |
| Exact bound root/state, no conflicting journal, admitted Active/LKG, exact Bootstrap | `LaunchInstalled` |
| State and root both genuinely absent | `ShowSetup` |
| Any first-install marker or residue from an earlier process | `RecoveryRequired` |
| Another writer owns the state path | `Busy` |
| Malformed, unreadable, moved, partial, foreign, mismatched, or unverifiable facts | `RecoveryRequired` or `HealthUnavailable` |

`Missing state + existing unexplained root` and `existing state + missing root`
are never treated as uninstalled. The Launcher never scans other directories,
infers a root from a folder name, selects a version, or silently rebinds a
moved root.

Owner decision A (2026-08-29): the distribution Launcher is delivery media,
not managed-installation provenance. If the exact state, managed root,
transaction marker, and staging container are all absent, the observation is a
genuine first install even when the distribution Launcher itself remains. No
provenance sidecar or registration is added to the startup path. Before any
install mutation, the existing state-path writer lease performs the same exact
state/root/residue recheck, so a concurrent or partially restored installation
cannot be overwritten.

The embedded descriptor binds the exact immutable Bootstrap filename, size,
SHA-256, protocol, and source commit. A newer downloaded Launcher may hand off
to an existing root only when that Bootstrap identity remains compatible. A
Bootstrap replacement is an explicit installer/recovery migration, never an
ordinary startup side effect.

### Bounded startup admission

Normal startup performs only the local checks required to launch exact code:

1. read at most 64 KiB plus one rejection probe from the strict embedded
   descriptor, admit a declared Bootstrap from 1 through 200,000,000 bytes, and
   compare only the exact embedded resource's existence and length;
2. read bounded strict per-user app and launcher state;
3. compare the canonical root binding and non-reparse path facts;
4. parse only the admitted Active version's bounded release manifest;
5. acquire the exact root Bootstrap and version Launcher through stable
   deny-write/delete handles; and
6. calculate each executable SHA-256 once, hold its handle through
   `Process.Start`, and reuse the existing READY protocol.

The descriptor projection, state load, and exact-root observation consume one
absolute 250 ms local-health budget. Healthy entry and the decision to show
Setup read zero bytes of embedded Bootstrap content and do not hash it. Only an
explicit Setup payload inspection or exact capture reopens that resource,
streams an exact-length SHA-256 check, probes for truncation/extra bytes, and
retains at most the declared executable ceiling for materialization. This is a
safety ceiling shared with existing executable admission, not a package-size or
download-size optimization gate.

The stable Root Bootstrap handoff also carries the exact descriptor-bound
filename, positive length, and lowercase SHA-256 in one bounded inherited
context. Desktop consumes and clears that context only after managed lifetime
capture succeeds. A missing, malformed, or manually supplied context cannot
authorize restart and does not prevent the otherwise healthy Desktop session.

The critical path never accesses Registry replicas, SharePoint, `G:`, UNC,
Catalog, package downloads, release notes, SBOM/provenance, firmware self-test,
or full installed-version inventory. It also never trusts a prior healthy
cache instead of verifying the executable that will run.

Installation health is a bounded admission gate, not an excuse to finish
background maintenance before launch. Any deferrable work must yield to the
healthy Bootstrap handoff and begin only after the first rendered frame.

The release acceptance targets are:

- local state/exact-root health classification P95 at or below 100 ms, with a
  250 ms hard cutoff;
- show a non-blocking `Checking installation...` indicator only after 250 ms;
- healthy handoff to root Bootstrap P95 at or below 300 ms;
- verified `Process.Start` of the version Launcher P95 at or below 750 ms;
- writer-lease startup wait no longer than 250 ms; and
- a 5.5-second end-to-end hard deadline for local installation admission,
  split into at most five seconds of operation, including exact Bootstrap hashing
  on cold or antivirus-scanned storage, and at least 0.5 seconds reserved for
  process-tree cleanup.

P95 targets affect telemetry and release evidence only. The 250 ms local-health
cutoff and the larger admission/completion limits are hard. A hard timeout,
busy writer, I/O failure, hash/PE mismatch, or unknown fact fails closed as a
typed result; it never falls through to Setup. Cleanup consumes the same
absolute monotonic budget rather than starting a new timeout. If complete
process-tree termination cannot be proved inside the reserved cleanup window,
the result is typed `TerminationUnconfirmed`; the Launcher never reports an
ordinary timeout while late process-creation authority remains. The existing
child READY deadline is a separate activation/rollback gate and is not reported
as health-inspection time.

The distribution Launcher starts Root Bootstrap inside one outer Windows Job
whose deterministic state-path prefix is extended by a fresh per-invocation
identity. The exact state-path writer lease remains the serialization authority,
so cleanup of one failed invocation cannot terminate a previously accepted
Desktop tree. A one-use parent-to-child `START` gate must be authorized before Root Bootstrap
may seed state or start a Launcher; the existing child-to-parent `ADMITTED`
receipt remains separate authority proving that the exact version Launcher
successfully started. Timeout closes `START` permanently, and ordinary timeout
is returned only after the start worker is terminal and the complete Job is
proved empty.

Root Bootstrap captures and clears the ambient descriptor-bound Bootstrap
identity on every invocation. Only a complete inherited distribution-Launcher
startup context may retain that captured value for explicit propagation.
Legacy/direct, incomplete, or malformed startup clears it and grants no restart
authority. The version Launcher likewise removes any ambient copy before child
creation and propagates only the explicit trusted identity received from Root
Bootstrap; a manually supplied environment value therefore cannot be laundered
through a newly created managed child lifetime.

Once Root Bootstrap reports `ADMITTED`, the local-admission deadline is retired
and the existing READY policy takes over. Candidate READY and the single LKG
rollback attempt retain their independent 20-second limits; the enclosing
Bootstrap completion receipt is bounded to 45 seconds, split into 44.5 seconds
of operation and a 0.5-second process-tree cleanup reserve, so both attempts
plus commit overhead remain possible. Receipt arrival completes immediately and
never adds a fixed delay to a healthy launch. Only accepted terminal READY or
rollback success releases the outer Job; every failure path terminates and
confirms the complete tree or returns `TerminationUnconfirmed`.

Potentially blocking OS metadata observation runs as a read-only task behind
the hard deadline. Timing out abandons only the wait; the isolated observation
cannot mutate state, access update sources, or start a child later.

After Desktop READY, durable launch commit, and one rendered frame, the
existing inventory/update owners may run deep local verification and then
network update checks in the background. Network failure means `Offline`, not
local installation damage. A post-start Active integrity failure blocks new
firmware-producing and version-mutating actions and is rechecked synchronously
on the next launch.

### Setup eligibility and source access

Setup source access begins only after the entry classifier proves
`ShowSetup`. The Launcher then asks the existing
`VersionManagementExperience` for one read-only fresh-install candidate. The
candidate is selected by the existing ordered Registry replicas and admitted
Catalog; it may equal or exceed the distribution Launcher version and is never
inferred from a filename.

The immutable token binds Registry revision/digest, Catalog digest/publication,
exact source root, application version, package relative path/size/SHA-256,
release-manifest SHA-256, and entry identity. Installation re-reads and
re-verifies that exact token under the existing state-path writer lease; it
never silently selects a different publication.

Fresh Setup admits only a managed `1.0` package whose release manifest is
schema `1.2` and contains the current supported version-Launcher protocol and
exact launcher identity. Schema `1.1` remains readable for historical installed
inventory, but it cannot become a first-install candidate that Bootstrap would
be unable to launch.

An ordinary development build without a release-built embedded payload returns
`PayloadUnavailable`. A rejected destination or foreign state produces no
Registry, Catalog, package, or persistent mutation.

### Destination and root transaction

Setup and managed launch reuse the same no-follow stable-path custody
primitive. Setup retains its separate mutation policy, and it does not assume
that directory handles prevent namespace additions. After closed-root proof it
revalidates the captured identity and exact child-name topology immediately
before every irreversible marker phase advance or delete. A late child,
replacement, reparse, type change, or unavailable identity preserves recovery
evidence and returns `RecoveryRequired`.

Every nested Setup package directory and file is created relative to the one
already held staging/version-directory handle. The Windows native owner uses
`OBJ_DONT_REPARSE` for each relative component and retains the resulting
handles; no path-based fallback or second nested-package writer is permitted.
The ordinary update path and Setup still share the existing managed-version
repository as the sole package-plan, extraction, admission, and verification
owner; Setup only substitutes the held relative destination adapter.

Ordinary update captures immutable custody of its promoted version directory
inside that repository operation. Setup instead verifies the package content
inside the already held staging root and defers complete immutable-tree custody
until whole-root promotion. The exact staging-root handle remains open across
the same-volume rename. While it still blocks replacement, the stable-custody
owner opens a read-only bridge by exact final name relative to the retained
parent and proves identical root identity. It then closes the delete-capable
source, reopens the exact relative name under the final immutable no-delete
sharing contract, proves identity again, releases the bridge, and captures the
closed tree. Ordinary absolute-path reopen is forbidden; any bounded-transition
substitution fails the identity or topology proof.

The repository's empty `.staging` child is removed before whole-root promotion.
Setup first binds that exact plain directory identity through the held staging
root. Only native sharing contention while opening that same empty identity for
deletion may receive a bounded retry: 20 attempts with at most 19 cancellable
250 ms delays. Every later open must match the original identity and remain
plain and empty. Before Setup has established ownership of deletion, any
content, disappearance, replacement, reparse/type change, access denial,
delete-pending, or unclassified failure retains the transaction as recovery
evidence. This is contention handling within the same in-flight Setup
transaction, not a later resume or broad cleanup path; the conflicting handle
may belong to an external scanner or indexer.
If Setup itself successfully marks that identity-matched empty child for
deletion but an already-open external handle delays disappearance, the same
bounded budget may wait for the owned delete-pending object. Only that owned
state may treat the exact object becoming absent as successful deletion. A
delete-pending object first observed before Setup's mark remains a
contradiction and is never adopted.

- The default parent location is the directory containing the running
  distribution Launcher; the managed root is a deterministic child directory.
  The user may select another parent before installation.
- The admitted root is one canonical absolute path on a local fixed drive. The
  root and every existing ancestor are non-reparse. UNC, mapped-network,
  removable, device, alternate-data-stream, relative, root-only, and escaping
  paths fail closed.
- Setup is per-user and never elevates itself. Unwritable destinations return a
  typed permission failure before source access or mutation.
- Only a genuinely absent root with no marker or staging residue may be
  materialized.
  Setup does not adopt, overwrite, merge, broadly clean, or repair other facts.

Under the existing state-path writer lease, the root adapter creates a uniquely
owned sibling staging directory on the destination volume:

```text
NvtFwCombiner/
├─ NvtFwCombiner.DistributionLauncher.exe
├─ NvtFwCombiner.Bootstrap.exe
├─ version-manager.seed.v1.json
└─ versions/<admitted-version>/<closed package payload>
```

The running distribution Launcher is captured through one stable PE/size/SHA
handle and copied byte-for-byte to the installed entry path. The embedded
Bootstrap is written from its exact descriptor-bound bytes. The existing
managed-version repository installs the admitted package. The existing seed
owner emits one canonical unbound seed with one admission,
`Active == LastKnownGood`, and no root binding, update source, pending/failed
activation, mutation, or retention review. Setup never writes per-user
`version-manager.v1.json` or `PendingActivation`.

Closed Setup-root capture applies three independent ceilings. Package content
allows at most 4,097 files, 4,096 directories, and 512 MiB plus the 4 KiB
admission document. Setup adds exactly three files and two directories to those
counts. Its byte overhead is exactly the captured distribution Launcher length,
the descriptor-bound Bootstrap length, and the actual canonical seed length.
The external transaction marker and sibling staging container are not hidden
inside that closed-root byte allowance.

Before one same-volume promotion into an absent final root, the adapter
reverifies the distribution Launcher copy, Bootstrap, seed, installed package
content, and expected top-level facts. Promotion retains the parent and the
delete-capable source handle. While that source still blocks replacement, the
adapter opens a read-only bridge by the exact final name relative to the held
parent and proves the same root identity. It then closes the source, reopens the
same relative name with the final immutable no-delete sharing contract, proves
identity again, releases the bridge, captures descendants, and verifies the
complete closed-root inventory. Substitution in this bounded transition fails
the identity or topology proof rather than gaining custody. It then creates
exactly one user shortcut pointing to the installed distribution Launcher.

The final held-tree revalidation immediately before `Process.Start` is
unchanged: standard Windows process creation cannot atomically combine that
observation with image creation, so the residual scheduler interval remains an
explicit OS boundary rather than a stronger atomicity claim.

### Transaction, retry, and Bootstrap handoff

The exact sibling
`<managedRoot>.managed-setup-transaction.v1.json` binds the measured
distribution Launcher identity, embedded descriptor and Bootstrap identity,
destination, immutable candidate, closed owned paths, and one closed phase:
`staging`, `root-promoted`, or `bootstrap-launch-recorded`.

Its only staging owner is the exact sibling container
`<managedRoot>.managed-setup-staging/<transactionId>`. The initial marker is
written directly to its final path with `CreateNew` and write-through so a
crash cannot leave an unobserved temporary marker. Staged payload facts are
verified immediately before the same-volume rename, and the exact root handle
remains held across it. The retained-parent bridge and double identity proof
transition it to final immutable custody before complete closed-root
verification. The marker keeps the promoted root
unavailable to normal launch until that post-promotion proof succeeds. The
complete root is revalidated once more in the writer-held transition that
records `bootstrap-launch-recorded`.

The marker is continuity evidence, not a signature or broad deletion
authority. The process that created it may advance only its own transaction
after rechecking exact equality and every owned path. A later Launcher process
never resumes or cleans it; any surviving marker or residue is
`RecoveryRequired` and is consumed only by `RECOVERY-105-01`.

Before process creation, Setup records `bootstrap-launch-recorded`, duplicates
the already verified final immutable-tree handles into one independently owned
Bootstrap launch lease, and releases the writer lease. It transfers that lease
to the existing stable process-start owner and waits for its terminal READY
result. Setup never falls back to ordinary path-based Bootstrap handoff,
releases final immutable transaction custody early, relaxes its sharing,
retries the self-contention, or falls back to a path-based start. Read-only
closed-tree topology revalidation remains permitted while the captured
identities are held. Exit code zero
proves that the existing seed importer bound durable state and the existing
launcher chain completed READY. Only then may the exact transaction marker be
removed through the same exclusively held handle that was schema- and
identity-validated, and only after the complete root is revalidated again while
that marker handle remains held. Completion never re-resolves the path for
deletion. Marker phases record promotion and one launch intent; they never
attest installation health or authorize a validation bypass.

A start failure, nonzero outcome, or caller cancellation after irreversible
root promotion leaves the complete promoted root intact and returns
`InstalledButLaunchFailed`; it is never reported as an ordinary `Cancelled`
attempt. A proven pre-process failure may revert only its own phase after
reacquiring the writer lease and proving state/root facts unchanged. A failure
before promotion retains its exact marker and staging tree as recovery evidence
for `RECOVERY-105-01`. Ordinary package installation reports failed exact-handle
cleanup as a typed cleanup-incomplete issue; Setup projects any such residue to
`RecoveryRequired`. Neither path substitutes raced recursive deletion.

### Non-circular release identity

Protected tooling first publishes and hashes the immutable Bootstrap, creates a
canonical payload-admission descriptor, and embeds both resources in the final
distribution Launcher. The descriptor does not contain the final Launcher's
own hash. After final publication, tooling measures the Launcher and
independently extracts/rechecks both resources before generating the external
installer manifest.

The separate closed evidence set is:

```text
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.exe
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.manifest.json
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.spdx.json
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.intoto.jsonl
NvtFwCombiner-Launcher-vX.Y.Z-win-x64.sha256
```

The external manifest is release evidence only. It is never embedded, required
adjacent runtime input, or transaction authority. The existing version ZIP and
its release assets remain Bootstrap-free and byte-contract compatible.

## Deferred to `RECOVERY-105-01`

- moved-root adoption or rebind;
- repair/replacement of Launcher, Bootstrap, seed, state, package, or shortcut;
- abandoned or unknown staging without exact transaction ownership;
- uninstall, reset, migration, and recovery history/UI; and
- installer-level Bootstrap protocol migration.

Until that capability exists, these states remain typed and fail closed. Setup
is not delivered to users until the v1.0.6 integrated Setup/recovery acceptance
gate passes.

## Consequences and gates

- This is R3 release/security/permission authority and R2 cross-layer
  architecture. It does not alter firmware bytes, profiles, ranges, CRC,
  naming, composition, or golden authority.
- Required evidence includes the entry classification matrix, proof of zero
  network/full-inventory calls on healthy startup, single-hash stable custody,
  cold/warm P50/P95/P99 timings, adversarial path/root/package tests, failure
  injection, process contention, deterministic builds, closed installer
  evidence, and clean-Windows Launcher -> Setup -> Bootstrap -> READY smoke.
- The exact release candidate requires independent R3 review and release-owner
  attestation. Repository tests cannot replace that authority.

### v1.1.0 bounded direct-package exception

v1.1.0 may be distributed as one Windows x64 manual-download Application ZIP
without Launcher, Setup, Bootstrap, Catalog, Registry, automatic update,
Version deployment, or reference payload. The ZIP uses release-manifest schema
1.3 with `distributionMode: manual-only` and is rejected by managed Version
verification. Users extract it and run `NvtFwCombiner.exe` directly.

This exception does not amend the managed-first authority above for Setup or
update, does not make the Desktop Application a second updater, and does not
authorize Catalog/Registry mutation. It is limited to v1.1.0 and the exact
three published assets: ZIP, SPDX SBOM, and provenance. Any future managed
distribution continues to require the Launcher/Bootstrap contract and its
independent gates.

## Rejected options

- A separate user-facing Setup executable.
- A self-installing Root Launcher that folds Setup/network/package UI into the
  immutable Bootstrap.
- A second updater, parser, extractor, state writer, launcher selector, or
  filesystem health walker.
- Bootstrap inside the version ZIP or update Catalog.
- Runtime trust in adjacent installer evidence or a prior healthy cache.
- Best-effort overwrite, automatic elevation/rebind, directory-name inference,
  or broad cleanup.
