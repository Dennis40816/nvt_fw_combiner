# Distribution Launcher and managed Setup contract v1

This contract defines the single-entry and first-install protocol accepted by
ADR 0062. The distribution Launcher is an adapter over existing
VersionManagement use cases; it does not own package, state, launcher, update,
or firmware semantics.

Interrupted-transaction convergence and rollback are extended only by
[`managed-recovery-v1.md`](managed-recovery-v1.md). Setup itself never gains a
resume or cleanup branch.

## Roles

| Role | Runtime responsibility |
| --- | --- |
| Distribution Launcher | The only user-facing entry; classify installation, show Setup when genuinely absent, and hand off a healthy root |
| Root Bootstrap | Immutable internal supervisor; select and READY-check the committed version Launcher |
| Version Launcher | Version-scoped internal launcher from the admitted package |

The two internal roles are never presented as alternate user entry points.

## Inputs and authority

The distribution Launcher has exactly four Setup inputs:

1. its own stable measured executable identity and source lease;
2. one embedded canonical payload-admission descriptor and exact Bootstrap
   resource;
3. one user-selected local parent destination; and
4. the existing compiled production Registry replica locations.

Command-line payload paths, current directory, adjacent files, environment
payloads, development fallbacks, filenames, and the external installer
manifest are not runtime authority. An ordinary build without the embedded
release payload returns `PayloadUnavailable`.

## Entry ordering and outcomes

Every invocation validates its embedded descriptor and observes bounded durable
state plus the exact root before any Registry, Catalog, package, or network
access.

| Complete observation | Entry outcome |
| --- | --- |
| Exact healthy bound root and state | `LaunchInstalled` |
| State and root both genuinely absent | `ShowSetup` |
| Any first-install marker or residue from an earlier process | `RecoveryRequired` |
| Bootstrap handoff or Setup mutation contends for the state-path writer | `Busy` |
| Damaged, moved, partial, foreign, mismatched, or ambiguous facts | `RecoveryRequired` |
| Incomplete I/O or timeout | `HealthUnavailable` |

Only `ShowSetup` may continue to source admission. `Busy`,
timeout, or unavailable facts never fall through to Setup.

The distribution Launcher executable is not installation provenance. When the
exact durable state, managed root, transaction marker, and staging container
are all absent, the result is `ShowSetup` even if that Launcher remains at its
download location. Implementations must not add a provenance sidecar or scan
other directories to distinguish this case. The state-path writer lease must
repeat the exact absence check before materialization, and any newly observed
fact changes the result to a non-Setup outcome.

The bounded entry classification is read-only and does not acquire the writer
lease. Its result is only a routing observation. Bootstrap and Setup acquire the
existing writer authority before process or filesystem mutation and recheck
the exact facts they consume; contention at either mutation gate is `Busy`.

## Healthy-start critical path

The synchronous path may read only:

- bounded strict `version-manager.v1.json` and launcher sidecar state;
- exact root binding and non-reparse path facts;
- the Active admission and its bounded `RELEASE-MANIFEST.json`;
- the exact Root Bootstrap; and
- the exact version Launcher about to run.

Each executable is hashed once through a stable deny-write/delete handle held
through `Process.Start`. The path must not contact Registry, SharePoint, `G:`,
UNC, Catalog, package downloads, or release notes; enumerate all versions; hash
complete packages/ZIP/SBOM/provenance; or run firmware/profile/CRC self-test.

Root Bootstrap receives the exact descriptor-bound filename, positive length,
and lowercase SHA-256 through one bounded inherited context. Root Bootstrap
captures and clears the ambient value on every invocation and retains it only
for a complete inherited distribution-Launcher startup. Version Launcher
clears ambient identity before child creation and passes only that explicit
trusted value. Desktop consumes it only after managed lifetime capture returns
`Captured`; missing, malformed, legacy/direct, or manually injected context
leaves the session usable but provides no restart authority.

Acceptance budgets are 100 ms P95 local state/exact-root classification, a
250 ms hard local-health cutoff and progress threshold, 300 ms P95 Root
Bootstrap handoff, 750 ms P95 verified version-Launcher start, 250 ms maximum
writer-lease wait, and a 5.5-second end-to-end hard local-admission deadline.
The admission budget is at most five seconds of operation, including exact
Bootstrap hashing on cold or antivirus-scanned storage, plus a reserved 0.5
seconds for process-tree cleanup. P95 targets never weaken the hard cutoffs.
Cleanup uses the same absolute monotonic deadline; it
does not add a fresh wait. An ordinary hard timeout is `HealthUnavailable` only
after the process-start worker is terminal and the complete Bootstrap Job is
proved empty. Otherwise the typed result is `TerminationUnconfirmed`.

The distribution Launcher creates one outer Windows Job with a fresh
per-invocation identity under the deterministic state-path prefix and gives
Root Bootstrap a one-use parent-to-child `START` gate. The exact state-path
writer lease remains the serialization authority. Root Bootstrap must join that
Job and receive the exact START byte before it may create state, seed an
installation, or start another process. `START` authorizes execution;
child-to-parent `ADMITTED` separately proves the exact version Launcher
successfully started. Timeout and cancellation permanently abort START.

The 5.5-second deadline ends only after the version Launcher has started and
Root Bootstrap has returned `ADMITTED`. READY is a separate phase: each
existing candidate/LKG attempt retains its 20-second limit and the enclosing
completion receipt is bounded to 45 seconds, with no more than 44.5 seconds of
operation and at least 0.5 seconds reserved for cleanup, so one candidate
failure can still complete one LKG rollback. A READY result returns immediately;
the 45-second limit is not an added startup delay or health-inspection budget.
Exit-before-admission, timeout, cancellation, or non-success completion must
terminate and prove the outer Job empty. Only accepted READY or rollback
success may release the accepted child tree.

Filesystem observation is read-only and isolated behind the hard deadline. If
an OS metadata call does not return, the Launcher stops waiting at the
deadline; the abandoned observation has no process-start or mutation
authority and cannot delay the caller indefinitely.

Deep inventory and network update checks begin only after Desktop READY,
durable launch commit, and a rendered frame. Offline network results do not
damage local health.

The health gate is an admission check, not a startup task queue. Implementations
must not delay a healthy handoff to perform work that can run after the first
rendered frame.

## Setup candidate

After `ShowSetup`, the existing Registry/Catalog/package owner
returns one immutable candidate. It binds Registry replica id, revision and
digest; Catalog schema, latest version, digest and path; exact source root and
status; package version/path/size/SHA-256; release-manifest SHA-256; and entry
identity.

The newest compatible fully admitted candidate is accepted even when it equals
the distribution Launcher version. A changed exact snapshot during the
under-lease recheck is `SourceChanged`; the use case never silently switches to
another candidate.

Fresh Setup additionally requires release-manifest schema `1.2`, protocol
version `1`, and the exact supported version-scoped Launcher identity. Schema
`1.1` remains readable for historical installed inventory but is not an
installable Setup candidate.

## Destination states

The default parent is the running distribution Launcher's directory. The
managed root is a deterministic child. A replacement parent is admitted only
when Infrastructure proves a local fixed-drive canonical absolute path, all
existing ancestors are non-reparse, and the destination is writable without
elevation.

| Root and durable-state observation | Setup outcome |
| --- | --- |
| Absent root and missing state | `ReadyToInstall` |
| Any marker or residue | `RecoveryRequired` |
| Anything already installed | return to entry routing; Setup does not classify or launch it |
| Foreign/damaged/moved/extra/ambiguous root or different binding | `RecoveryRequired` |
| Incomplete permission or I/O fact | `StateUnavailable` |

## Fresh transaction

1. Reconfirm missing durable state and an absent residue-free root without
   source access.
2. Under stable no-follow ancestor custody, require the exact parent to exist
   and prove destination writability with one bounded `CreateNew`, write-through,
   delete-on-close probe. A rejected destination performs no payload, Registry,
   Catalog, package, or persistent mutation.
3. Inspect the running distribution Launcher payload and select one immutable
   fresh-install candidate for user review.
4. After confirmation, acquire the existing state-path writer lease; reload
   state/root and repeat destination admission before capturing payload or
   revalidating that exact candidate.
5. Capture the running distribution Launcher with stable PE/size/SHA custody,
   validate the descriptor plus Bootstrap resource, and reverify the reviewed
   Registry/Catalog/package token without selecting a replacement.
6. Create the exact sibling marker
   `<managedRoot>.managed-setup-transaction.v1.json` directly with `CreateNew`
   and write-through while retaining the same exclusive marker handle through
   every phase and terminal return. Create the exact staging container and root
   `<managedRoot>.managed-setup-staging/<transactionId>` relative to held parent
   handles with atomic create-and-open semantics; a collision fails closed.
   Every nested package component is then created through the same held
   relative directory owner with `OBJ_DONT_REPARSE`; no path-based nested-write
   fallback is allowed.
7. Copy the captured distribution Launcher bytes to
   `NvtFwCombiner.DistributionLauncher.exe`.
8. Write the exact descriptor-bound `NvtFwCombiner.Bootstrap.exe`.
9. Install the candidate through the existing managed-version repository.
10. Write the canonical unbound seed through the shared seed policy.
11. Verify staged payload facts and every known exact identity, then rename the
    exact held staging-root object on the same volume with
    `ReplaceIfExists == false`. While that delete-capable source handle still
    blocks replacement, open one read-only bridge by the exact final name
    relative to the retained parent and prove the same root file identity.
    Close the delete-capable source, reopen the same relative name without
    delete access or delete sharing, prove the identity again, then release the
    bridge. Capture descendants and revalidate the complete closed-root
    inventory under that final immutable sharing contract. Any missing name,
    substitution, identity drift, or topology drift fails closed. Advance the
    same marker handle from `staging` to `root-promoted` only after this
    transition. Ordinary absolute-path reacquisition is forbidden.
12. Create one user shortcut to the installed distribution Launcher.
13. Revalidate the complete promoted root again under the same writer lease,
    record `bootstrap-launch-recorded`, then consume the one launch opportunity
    by duplicating the already verified final immutable-tree handles into independent
    Bootstrap launch custody. Release the state writer, transfer that owned
    custody to the existing stable process-start seam, and wait for its terminal
    result. Setup never reacquires promoted-tree or Bootstrap launch custody by
    path, retries a self-contention, releases final immutable transaction
    custody early, relaxes its sharing contract, or
    falls back to a path-based start.
14. On exit code zero, reacquire the writer lease and prove exact bound healthy
    state. While the exact schema/identity-validated marker handle remains
    exclusively held, revalidate the complete closed root, mark only that same
    marker object for deletion, and report `Completed`.

The managed-version repository remains the single package-plan, extraction,
admission, and verification owner for both ordinary updates and Setup. Its
independent installed-package limits are 4,097 files, 4,096 directories, and
512 MiB plus the 4 KiB admission document. Closed Setup-root custody adds
exactly three files and two directories; its byte allowance adds exactly the
captured distribution Launcher length, descriptor-bound Bootstrap length, and
actual canonical seed length. Marker and sibling staging-container bytes are
outside, not silently charged to, the promoted root allowance.

Held-tree identity/topology is revalidated read-only by path while retained
identities remain held, synchronously immediately before `Process.Start`.
This is not a second custody acquisition. Standard Windows process creation still has no atomic
validate-and-create primitive; the scheduling interval after that observation
is the unchanged explicit OS residual.

The launch clone has independent handle ownership but retains the final
immutable share contract and exact root/file identities. That contract blocks
root deletion or replacement while permitting independent read-only installed
version and Launcher verification.
Every clone failure or cancellation closes all duplicated handles; successful
ownership transfers once to the process-start task, which closes it after the
start decision. The original promoted custody remains held through READY and
exact marker finalization or through the terminal recovery handoff.

After root promotion, the handoff retains one presentation-safe failure with
the exact boundary (`PostPromotion` while durably recording the launch
transaction, `BootstrapStart`, `LauncherAdmission`, or `ApplicationReady`),
one authoritative typed reason, and the observed process
exit code when one exists. One shared exit-protocol codec owns the numeric
Bootstrap mapping for both producer and consumer. Admission and completion
receipts are shape-validated at the Application boundary; contradictory or
unknown receipt shapes fail closed as `InvalidReceipt`. The Setup surface may
render only that typed stage, reason, and optional numeric code. A
post-promotion failure is terminal for the current Setup plan: the user must
close and reopen the Launcher so the existing Recovery owner can diagnose the
durable marker and root; same-plan retry is not offered. Application exposes
that continuation authority through `IsRecoveryOwned` on every valid
`RecoveryRequired` or `InstalledButLaunchFailed` result. Presentation consumes
that fact directly; it does not reconstruct promotion state from optional
diagnostics or exception text.

Setup never writes `version-manager.v1.json`, creates `PendingActivation`, or
records an update source. The existing Bootstrap seed importer performs the
first durable root binding.

## Retry and cleanup

The strict marker conforms to `managed-setup-transaction-v1.schema.json`. It
binds exact state path, distribution Launcher, payload descriptor, Bootstrap,
destination, candidate publication/source/package identity, closed owned paths,
and one phase.

`root-promoted` means only that same-volume promotion completed, and
`bootstrap-launch-recorded` means only that one Bootstrap launch intent was
durably recorded. Neither phase attests root health or permits validation to be
skipped. Product-owned mutation is serialized by the canonical writer lease;
external changes are detected again at each consumption and terminal gate.

- The creating process may advance `staging` and `root-promoted` only while it
  retains exact transaction custody and all facts revalidate.
- `bootstrap-launch-recorded` cannot start a second process.
- A proven pre-start failure may restore `root-promoted` only after state,
  root, and marker are rechecked under the writer lease.
- Cleanup is limited to exact canonical `ownedPaths`; any escape, reparse,
  ownership ambiguity, or identity mismatch stops cleanup.
- Ordinary package installation returns a typed cleanup-incomplete issue when
  exact held-handle cleanup cannot be proved. Setup maps that residue to
  `RecoveryRequired`; cleanup failure is never silently discarded.
- Before whole-root promotion, Setup may retry only an explicitly classified
  Windows sharing conflict for the same observed, plain, empty repository
  `.staging` child. The observation binds its file identity; every retry occurs
  through the same held parent custody and revalidates identity, type, and
  emptiness. There are at most 20 attempts and 19 cancellable 250 ms delays.
  Before Setup owns deletion, content, disappearance after observation,
  replacement, reparse/type change, access denial, delete-pending, or any
  unclassified failure stops immediately.
  The sole delete-pending exception is one causally owned by Setup after the
  same identity-matched empty handle was successfully marked for deletion. It
  may consume the same retry budget while waiting for that object to disappear,
  and only that state may treat absence as successful deletion; a delete-pending
  state observed before Setup marks it remains terminal.
- A failure before promotion retains its exact staging tree and matching marker
  as durable recovery evidence. Setup itself does not recursively delete by
  path. Read-only classification belongs to `RECOVERY-105A`; an explicitly
  invoked later mutation is authorized only by `managed-recovery-v1`.
- Successful completion validates and marks the same exclusively held marker
  object for deletion. It never closes the handle and then deletes whatever
  object happens to occupy the path.

A later ordinary entry or Setup process treats every surviving marker or
residue as `RecoveryRequired`; it never resumes or cleans it. Only the separate
`managed-recovery-v1` action may converge or roll back the exact admitted
transaction. An already-bound installation belongs to normal entry routing.
Repair, rebind, adoption, migration, and general uninstall remain outside both
Setup and recovery v1.

## Typed Setup outcomes

The Application owner exposes at least:

- `ReadyToInstall`
- `Installing`
- `Completed`
- `PayloadUnavailable`
- `PayloadInvalid`
- `SourceUnavailable`
- `SourceChanged`
- `InvalidDestination`
- `PermissionDenied`
- `Busy`
- `RecoveryRequired`
- `InstalledButLaunchFailed`
- `StateUnavailable`
- `Cancelled`

Presentation does not infer these outcomes from exception text.
`Cancelled` is valid only before irreversible root promotion. Cancellation
after promotion is `InstalledButLaunchFailed` (or `RecoveryRequired` when root
integrity cannot be proved), because an installed root and transaction evidence
already exist.

## Release separation

The embedded descriptor conforms to
`managed-setup-payload-admission-v1.schema.json`. The external installer
manifest conforms to `installer-release-manifest-v1.schema.json`; it is created
after the final distribution Launcher and is never runtime authority. Existing
version ZIP, release manifest, Catalog, SBOM, provenance, and checksum contracts
remain Bootstrap-free and unchanged.
