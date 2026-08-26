# ADR 0056: Update the launcher through an immutable bootstrap trust anchor

- Status: Accepted by product owner and admitted by independent R2 architecture
  review on 2026-08-26; first release remains an R3 human gate
- Date: 2026-08-26
- Owners: Product owner, architecture owner, release owner
- Risk: R2 process/state/contract architecture; R3 first `v1.0.0` package and
  release evidence
- Builds on: ADR 0051 managed application activation

## Context

ADR 0051 deliberately keeps one launcher outside installed application
versions. That launcher cannot safely replace its own running executable. The
product owner requires launcher self-update in `v1.0.0`, while preserving the
existing exact state-path writer identity, rollback bounds, offline startup,
and closed package verification.

Replacing a running root executable, selecting an arbitrary launcher found on
disk, or allowing an application package to rewrite the trust anchor would
expose mixed installations and unbounded recovery. Coupling launcher activation
to an uncommitted application activation would also combine two durable state
machines and make power-loss recovery ambiguous.

## Decision

The managed root contains one immutable `NvtFwCombiner.Bootstrap.exe` trust
anchor. It is distributed only as part of the initial managed installation and
is never a Version-page update target. A bootstrap change requires a new
distribution/migration decision; ordinary release packages cannot replace it.

Every admitted application version contains one coupled launcher under
`versions/<appVersion>/launcher/`. There is no launcher-only package. The
release manifest declares an independent launcher identity: owning application
version, the exact owning application admission identity and release-manifest
SHA-256, launcher version, protocol version, exact relative executable path,
byte length, and lowercase SHA-256. The installed repository verifies the
admitted release-manifest hash, strict manifest schema, safe non-reparse path,
length, and digest before Application receives that identity. Matching SemVer
without matching admission is never sufficient.

Bootstrap follows only the already committed application `ActiveVersion`. It
never selects the launcher coupled to `PendingActivation.CandidateVersion`.
Application activation therefore remains wholly owned by the selected launcher;
a newly committed application release makes its coupled launcher eligible on
the next Bootstrap start.

Launcher activation begins only when application `PendingActivation` and
`PendingMutation` are both null. When application activation is pending,
Bootstrap starts only its already admitted active launcher without opening a
launcher transaction. An application mutation journal blocks all launcher
process work. This ordering prevents the two durable journals from being
advanced together.

Launcher activation uses strict mutable per-user state derived injectively as
`<exact-version-manager-state-path>.launcher-bootstrap.v1.json`. This is not a
fixed sibling name, a managed-root seed, or an application preference. It records the
normalized managed-root binding, exact active and last-known-good launcher
identities, an optional exact pending transaction, and the most recent failed
identity. Missing launcher state may start only the fully verified launcher
coupled to the already committed active application; that candidate is not
trusted as active or last-known-good until it completes readiness.

There is one writer identity. Bootstrap and desktop acquire the existing
OS-backed lease derived only from the normalized exact
`version-manager.v1.json` path before reading or writing either durable state.
The launcher-state store exposes no independent lease. A different managed root
sharing that state path fails before launcher inventory, process start, or
state mutation.

The transaction phases are `requested`, `candidateLaunchRecorded`, and
`rollbackLaunchRecorded`:

1. Under the shared lease, Bootstrap verifies the target, records `requested`,
   then records `candidateLaunchRecorded` before process start.
2. Bootstrap releases the lease and starts the exact verified executable with
   the exact `--managed-root` and `--state-path` values plus a one-use inherited
   ready channel.
3. Launcher runs the existing application activation coordinator. Only after
   the application reports READY, the application activation commit completes,
   and Launcher reloads the exact durable application state may Launcher report
   its own exact launcher identity plus committed application admission,
   release-manifest identity, and protocol READY message.
4. Bootstrap reacquires the same lease, reloads both durable states, rechecks
   root, pending identity, and committed active application, and only then
   commits launcher active/last-known-good.

Both the application release and launcher declare version-management protocol
1. Bootstrap admits no pre-fence payload: every application that it can start
must understand launcher-state delete protection and every launcher must
propagate the exact custom state path. `v1.0.0` is the first end-user managed
seed, so no older public managed payload is grandfathered into this boundary.

While launcher `pending` is non-null, every application-state mutation is
logically fenced even though Bootstrap releases the OS lease while a process is
running. Read-only startup and inventory may proceed. The candidate launcher is
eligible only when no application activation exists, so it needs no app-state
write while its own transaction is pending. After its app reports READY it
reloads the unchanged app state and sends its exact admission in outer READY;
changed app state or any attempted mutation aborts launcher commit.

`requested` may attempt its exact candidate. Restart from
`candidateLaunchRecorded` treats candidate outcome as uncertain and advances
only to the exact previous last-known-good launcher. Restart from
`rollbackLaunchRecorded` may retry only that same fallback. Start failure,
timeout, invalid readiness, tamper, protocol mismatch, or post-ready reload
failure cannot advance active launcher state. No directory scan or newest-
version fallback is permitted. A running executable is never overwritten or
deleted.

Within one invocation, a rejected or timed-out application or launcher permits
automatic rollback only after its process adapter confirms exit. A `Kill` or
wait failure that leaves exit unconfirmed returns a distinct fail-closed
outcome, preserves the current recoverable launch-recorded phase, and starts no
fallback. A later invocation continues to apply the existing power-loss
recovery rule from that durable phase.

Application deletion policy protects every exact owner admission named by an
active or pending launcher identity. Invalid or unavailable launcher state
blocks deletion. A target named only by launcher last-known-good requires the
existing explicit rollback-loss confirmation; Application first retires that
exact fallback durably under the same writer authority, then prepares the app
delete. It never deletes first or silently substitutes a scanned launcher.
Interrupted application mutation recovery applies the same exact-admission
protection, and Bootstrap refuses to begin launcher work while an application
install/delete mutation is pending.

This amendment preserves ADR 0051's ability to delete a non-active admitted
version while making rollback-executable loss explicit and ordered. An active
or pending launcher owner remains blocked until a later successful launcher
activation makes it irrelevant; confirmation alone cannot delete it.

## Rejected options

- **Overwrite the root launcher.** Windows executable locks and power loss can
  leave a mixed or absent launcher.
- **Let each launcher update itself.** The updater would be replacing its own
  execution authority and could not independently supervise rollback.
- **Store launcher fields in version-manager state v1.** Older strict readers
  reject unknown fields. The separate state avoids changing that wire shape;
  safety across executable versions comes from the mandatory protocol-1 fence,
  not from the sidecar's existence.
- **Give launcher state its own lock.** Two locks would permit split-brain
  application/launcher decisions.
- **Select the pending application's launcher.** Launcher and application
  activation journals would become one ambiguous transaction.
- **Retry or scan after an uncertain start.** That can start more than one
  candidate and loses the exact rollback bound.

## Consequences

- Bootstrap stays intentionally small and stable; package and SBOM policies
  distinguish the immutable trust anchor from version-scoped launchers.
- Application packages grow by one trimmed self-contained launcher payload.
  The existing exact ZIP and executable ceilings remain release gates unless
  the release owner explicitly changes them with measured evidence.
- Launcher update failure does not roll back an already ready application; the
  exact prior launcher can continue to launch that application when protocol
  compatible.
- Launcher protocol compatibility is explicit. `v1.0.0` admits protocol 1 only.
- Firmware bytes, profiles, ranges, processors, naming, and Golden authority do
  not change.

## Verification

- Pure Application tests cover first seed, ordinary launch, candidate success,
  exact rollback, all three power phases, start failure, timeout, invalid
  readiness, app-state post-commit reload failure, state/root mismatch, and
  delete protection.
- Infrastructure tests cover strict bounded state, duplicate/unknown JSON,
  tampered length/hash, reparse/path rejection, protocol mismatch, process
  arguments through managed Desktop, inherited readiness, injective custom
  state-path mapping, and two roots sharing one state path with zero
  unauthorized mutation.
- Transaction tests cover requested restart, each durable-save failure,
  changed app state after READY, pending app activation/mutation, first-launch
  failure without LKG, fallback tamper, cross-version protocol rejection, and
  exact-admission delete protection.
- Package policy and smoke prove one immutable Bootstrap, one coupled launcher,
  closed manifests, hashes, SBOM/provenance, startup, update, rollback, offline
  start, and deletion block on clean Windows x64.
- Narrow Application, Infrastructure, Bootstrap, architecture, and script tests;
  scoped Polytail; independent R2 review; release-owner R3 approval; and one
  frozen `python scripts/verify.py --all` run before handoff.
