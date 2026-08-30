# ADR 0063: Read-only managed Setup recovery diagnosis

Status: Accepted for `RECOVERY-105A`

Extends: ADR 0062 only with a reusable read-only diagnosis seam. It does not
authorize recovery mutation, retry, rebind, repair, migration, cleanup, source
access, or process launch.

## Context

ADR 0062 deliberately routes any surviving Setup marker or staging residue to
Recovery. The Setup materializer already owns the sole marker writer and had a
private strict parser. A later recovery experience needs to inspect that marker
without duplicating its schema interpretation or letting Infrastructure decide
whether recovery is safe.

Path-based reads are insufficient. The marker is deletion authority evidence,
so a recovery observer must not follow a reparse point, accept a replaced file,
read an unbounded document, or infer identities from nearby directory names.

## Decision

Application owns one `ManagedInstallationRecoveryExperience`. Its read-only
ports are intentionally incapable of starting a process or writing state:

- `IManagedSetupRecoveryStateReader` extends the canonical read-only state port
  with the exact immutable state-file identity that it reads;
- `IManagedInstallationRootProbe` supplies the existing complete root fact;
- `IManagedSetupRecoveryProbe` supplies the exact marker fact; and
- `IManagedProcessLifetimeProbe` observes the Bootstrap, Application, and
  Launcher lifetime roles.

The terminal outcomes are closed: `NoRecoveryNeeded`, `ActionAvailable`,
`Busy`, `HealthUnavailable`, and `ManualInterventionRequired`. Only
`ActionAvailable` carries an immutable typed Setup transaction. Application
does not parse JSON, inspect paths, acquire a writer lease, start Bootstrap,
select an update, or infer whether deletion is safe.

All three lifetime roles are observed first. Any `Active` role yields `Busy`
even when another role is unavailable. With no active role, any unavailable
role yields `HealthUnavailable`. The remaining decision table applies only
after all three roles are exactly `Exited`:

The recovery experience captures the state reader's absolute identity once at
construction and uses only that identity for every lifetime and marker
observation. `DiagnoseAsync` accepts no independent state path, so durable state
from path A cannot be combined with lifetime or marker evidence from path B.
An invalid adapter identity fails construction as a composition defect rather
than being projected as a user-remediable recovery outcome.

| Marker | Durable state | Root | Terminal |
| --- | --- | --- | --- |
| absent | missing | absent | `NoRecoveryNeeded` |
| absent | exact state bound to requested root | present | `NoRecoveryNeeded` |
| exact `staging` or `root-promoted` | missing | residue | `ActionAvailable` |
| exact `bootstrap-launch-recorded` | missing or exact bound state | residue | `ActionAvailable` |
| malformed, foreign, reparse, replaced, identity/path mismatch | any complete fact | any complete fact | `ManualInterventionRequired` |
| state invalid, unbound, wrong-bound or root invalid | any | any | `ManualInterventionRequired` |
| state/root/marker access, change, permission, or observation unavailable | any | any | `HealthUnavailable` |
| any other combination | any | any | `ManualInterventionRequired` |

The complete state/root/phase matrix is executable in Application tests; no
missing combination defaults to an actionable result.

Infrastructure owns one canonical `ManagedSetupTransactionCodec`. The existing
Setup materializer uses it for every marker write, phase read, and replacement.
The recovery probe uses the same codec for observation. A second parser or
marker schema owner is forbidden.

The production probe:

1. admits the caller-supplied canonical local managed-root and state-path
   identities;
2. derives only ADR 0062's exact sibling marker path;
3. acquires that file with `WindowsStablePathCustody.TryAcquireFile`, which
   opens every component without following reparse points;
4. reads at most 64 KiB through a duplicated held read-only handle;
5. accepts only the existing strict schema and the declared `staging`,
   `root-promoted`, or `bootstrap-launch-recorded` phase;
6. binds the marker to the exact root, state path, transaction id, and closed
   ordered owned-path set created by ADR 0062; and
7. revalidates held identities before returning `Exact`.

The `Exact` projection retains the transaction id, phase, root/state identities,
owned paths, distribution Launcher and Bootstrap hashes/sizes, and the complete
Registry/Catalog/package candidate identity. These values are evidence for a
later Application decision, not authority to mutate anything.

Absence is reported only when no-follow custody holds the exact parent chain
and two relative child opens return object-name/path-not-found while that same
parent custody remains held. That result is carried separately from generic
`Unavailable`; the probe never uses `File.Exists`, `File.GetAttributes`, or a
path-following read to turn an error into absence. A missing or unavailable
parent, unsupported platform, access failure, contention, replacement,
reparse point, malformed bytes, foreign identity, or unknown phase fails
closed as its typed fact. Sharing contention is incomplete transient health
and projects to `Unavailable`, hence `HealthUnavailable`; only a confirmed
held-identity or topology change projects to `Changed` and requires manual
intervention.

Lifetime observation is also zero-mutation. It uses the existing
`ManagedProcessLifetimeLease` naming and job-query owner, but the lease-file
observation opens only an existing exact no-follow child. Exact absence is
`Exited`, sharing contention is `Active`, and all other custody failures are
`Unavailable`. Observation never uses `OpenOrCreate`, creates a directory,
acquires a lease, creates a job, or starts a process. Application/Launcher tree
job queries remain exact and read-only.

## Non-goals and migration

`RECOVERY-105A` performs zero filesystem mutation and does not access Registry,
Catalog, packages, SharePoint, `G:`, or any updater. It starts Bootstrap exactly
zero times. It does not repair a shortcut, write state, scan for moved roots,
or clean staging residue.

The later recovery implementation may consume `KnownTransaction`, but it must
receive separate owner approval for every destructive transition and preserve
the Setup materializer as the sole marker writer until an explicit migration
seam and deletion milestone are accepted.

## Consequences and verification

- Dependency direction remains Infrastructure adapter -> Application port;
  Application has no filesystem or process-start dependency.
- Marker bytes have one strict schema/codec owner and deterministic canonical
  serialization.
- Reparse, replacement, oversize, malformed, identity mismatch, access,
  contention, cancellation, all marker phases, every state/root pair, and all
  three lifetime roles are typed and fail closed.
- Custody tests distinguish a held-parent exact-child absence from unavailable
  parent ancestry and prove that leaf-removal races and reparse leaves do not
  collapse into a generic path-based absence check.
- Architecture tests reject a second marker parser, Application filesystem or
  process-start access, lifetime lease creation during observation, probe
  mutation/process access, or materializer bypass of the codec.
- This is R2 architecture/security work. It changes no firmware bytes, ranges,
  CRC, naming, package selection, updater behavior, or release evidence.

## Rejected options

- Copying the private marker parser into a recovery adapter.
- Returning raw JSON or infrastructure document types to Application.
- Treating a schema-valid marker as exact without root/state/owned-path binding.
- Path-following `File.ReadAllBytes`, unbounded deserialization, broad directory
  scans, automatic cleanup, or an implicit Setup retry.
