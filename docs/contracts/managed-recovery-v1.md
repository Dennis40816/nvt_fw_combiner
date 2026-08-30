# Managed Setup recovery contract v1

This contract is the normative `RECOVERY-105B` extension to ADR 0063 and
`managed-setup-v1`. It authorizes only an explicit action over one exact Setup
transaction retained by the unchanged v1 marker.

## Roles and authority

| Owner | Responsibility |
| --- | --- |
| `ManagedInstallationRecoveryExperience` | Diagnose and produce immutable plans from read-only ports while invoking the one pure first-run state-pair policy |
| `ManagedSetupRecoveryExecutionCoordinator` | Require the explicit action, own the canonical writer lease, re-observe through the same pure policy, invoke exact execution, and publish typed outcomes |
| Recovery state/root/marker/lifetime ports | Return bounded typed facts without choosing policy |
| Exact execution port | Revalidate held identities and perform only the deletion sequence already selected by Application |
| Presentation | Render the typed action and result; never infer safety or delete by path |

There is one writer lease: the existing lease derived from the canonical
Application state path. Launcher state deliberately shares it. Application
passes the same live, non-serializable `VersionManagerWriteLeaseResult` into
the execution call while retaining its lexical `using` scope. The execution
adapter must require its internal production custody to report
`HoldsStatePath(exactStatePath)`. That check is false for a disposed result, a
different state path, or a result built from an arbitrary `IDisposable`; a
bool, serialized field, opaque plan value, or recreated token is not proof.
The adapter neither disposes that lease nor acquires or emulates another one.

## Plan and action

Only an `ActionAvailable` diagnosis carries a plan. A plan contains one of:

- `ConvergeReady`: preserve root and both state files, remove verified-empty
  residue, then remove the marker; or
- `RemoveIncompleteInstallation`: the user selecting this action is the one
  required confirmation. No background invocation and no second modal are
  allowed.

The plan token is opaque outside Application and Infrastructure. It binds the
complete transaction, exact normalized paths, state snapshots/absence,
candidate identity, verified Launcher identity for every complete installed
tree (or its explicit absence only for a proof-gated Missing/Missing
marker-derived restart prefix), closed inventory proof, deletion sequence,
and the three terminal lifetime observations. The opaque Infrastructure token
binds only these exact filesystem facts; the immutable internal Application
request is the sole boundary that binds that token to the selected action.
A plan cannot be converted to another action or silently refreshed. It is
immutable and retryable only while every bound fact still revalidates.

## Closed state-pair table

Let `A` be the exact candidate-bound state accepted by
`ManagedVersionSeedPolicy.IsCanonicalBoundFirstRunState`. Let `L` be the exact
Launcher identity verified from the same candidate admission and closed root.

| App | Launcher | Required phase | Action |
| --- | --- | --- | --- |
| Missing | Missing plus exact deterministic tree inventory/restart-prefix proof | staging, root-promoted, or bootstrap-launch-recorded | Remove incomplete installation |
| A | Missing | bootstrap-launch-recorded | Remove incomplete installation |
| A | Pending Requested(L), previous Active/LKG null, Active/LKG/Failed null | bootstrap-launch-recorded | Remove incomplete installation |
| A | Pending CandidateLaunchRecorded(L), previous Active/LKG null, Active/LKG/Failed null | bootstrap-launch-recorded | Remove incomplete installation |
| A | Failed=L, Active/LKG/Pending null | bootstrap-launch-recorded | Remove incomplete installation |
| A | Active=LKG=L, Pending/Failed null | bootstrap-launch-recorded | Converge READY |

No wildcard row exists. Every unlisted, malformed, unavailable, mismatched,
ordinary-update, split Active/LKG, different-root, different-candidate, or
foreign combination is non-actionable and performs zero mutation.
In particular, a partial staging tree without the exact deterministic
inventory/restart-prefix proof is `ManualInterventionRequired`, not an
actionable Missing/Missing row.
The Missing/Missing policy row accepts either a verified `L` or no `L`, but the
evidence adapter owns a stricter shape invariant: every complete installed
tree must supply its verified matching `L`; only exact early staging or a
marker-derived restart prefix after the fixed deletion sequence has begun may
omit it. This includes retained-manifest and post-manifest terminal-skeleton
prefixes where the Launcher is no longer independently verifiable. The opaque
inventory token binds the exact remaining shape. Every row containing `A` or
Launcher state always requires the verified matching `L`.

## Execution protocol

1. Reject a missing or different plan/action.
2. Application acquires the canonical writer lease using its existing bounded
   contention policy and retains it through the terminal result.
3. Re-observe Bootstrap, Application, and Launcher lifetime roles. Any Active
   role is `LifetimeActive`; any unavailable role is `HealthUnavailable`.
4. Reload Application state, Launcher state, root, marker, candidate/Launcher
   identity and inventory proof. Re-run the closed table and require an exact
   immutable-token match.
5. Acquire exact no-follow held custody for the marker and every target. Repeat
   identity, type, topology, schema, hash/size and token comparison.
6. Execute only the selected fixed sequence. After each individual delete,
   preserve the marker and an executable restart prefix.
7. Revalidate absence of every earlier target and presence/identity of every
   later target before advancing.
8. Delete the exact marker last and return Completed only after its held object
   is marked for deletion and all other authorized residue is absent. Exact
   absence is observed twice through a stable no-follow ancestor chain before
   the execution call returns.

This guarantee is bounded to the observation window in which Application
retains the canonical writer lease and Infrastructure performs the held delete
and final exact-absence checks. An unrelated external writer that creates new
residue after the call returns starts a new foreign state; the next health or
recovery observation must fail closed. The protocol does not claim permanent
absence without a journal, ACL, or exclusive parent-directory lease.

Infrastructure returns typed comparison/mutation facts. It never changes the
action, reconstructs policy, acquires a writer, starts a process, or falls back
to path-based recursive deletion.

## Deterministic rollback sequence

The global order is:

```text
launcher state
application state
ordinary transaction tree files (deepest path, then ordinal path)
nonterminal empty tree directories (deepest path, then ordinal path)
transaction authority manifest retained until ordinary files and nonterminal directories are absent
fixed terminal directory skeleton (deepest path first)
verified-empty staging container
marker
```

The tree is exactly one of the marker-owned transaction staging child or the
promoted root. An execution plan derives its exact inventory from the strict
marker-bound closed-root/release-manifest authority. Ordinary files are
ordered by deepest relative path and then ordinal relative path. Empty
nonterminal directories derived from those manifest paths follow in
deepest/ordinal order while the manifest remains present. The exact release
manifest is the final tree file: its hash is marker-bound, and it is retained
until every other expected file and nonterminal directory is absent. Once the
manifest is deleted, the only valid terminal skeleton is the candidate version
directory, its `versions` parent, and the transaction root; those names are
derivable from the unchanged marker without source access. They are deleted
deepest first. A partial staging tree that cannot provide that proof is not
deletable.

Restart accepts only prefixes of this order. For example, missing Launcher
state with Application state still present is reachable; missing Application
state while Launcher state remains is a hole. Missing root children are valid
only in the exact deterministic prefix proven by the retained inventory.
Arbitrary subsets are never normalized into progress.

## READY convergence sequence

`ConvergeReady` requires the READY row after the writer-lease reload and a
complete healthy closed root. It preserves both state files and the root,
removes only a verified-empty transaction staging child/container when
present, and deletes the exact marker last. It starts no process.

## Terminal outcomes

| Outcome | Meaning |
| --- | --- |
| `Completed` | Selected convergence or rollback completed and marker was removed last |
| `ConfirmationRequired` | Destructive rollback was requested without the explicit action |
| `Busy` | The canonical writer lease is contended |
| `LifetimeActive` | Bootstrap, Application, or Launcher lifetime is active |
| `HealthUnavailable` | A lifetime or general preflight health observation is unavailable |
| `StateUnavailable` | State, marker, root, or held filesystem evidence cannot be loaded completely during execution |
| `PermissionDenied` | Exact authorized object cannot be opened for the required access |
| `SourceChanged` | Candidate/inventory authority no longer matches the plan |
| `RecoveryRequired` | A safe restart prefix remains, or facts changed after planning |
| `ManualInterventionRequired` | Foreign, malformed, reparse, replacement, hole, or unprovable residue exists |
| `Cancelled` | Cancellation occurred before the first irreversible deletion |

After the first delete, interruption never reports ordinary Cancelled. Marker
retention remains the durable evidence for another explicit attempt.

## Compatibility and non-goals

The marker schema and three phases are unchanged. No separate recovery state
or journal is created. Existing healthy entry, Setup, Bootstrap, Launcher,
package, Registry/Catalog, firmware and report contracts are unchanged.

Move, rebind, repair, shortcut repair, uninstall, source replacement and 1.0.x
migration require later contracts and cannot reuse this deletion token.
