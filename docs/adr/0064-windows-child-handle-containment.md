# ADR 0064: Explicit Windows child-handle containment

Status: Accepted for `LAUNCH-106-HANDLE-CONTAINMENT-01`

Amends: ADR 0056 and ADR 0062 only for Windows process creation and inherited
handle transport. Their READY, START, ADMITTED, lifetime, Job, custody,
rollback, and state-transition decisions otherwise remain authoritative.

## Context

The managed Launcher chain already verifies each expected inherited handle in
the receiving child. The parent nevertheless created READY, START, ADMITTED,
and lifetime handles as ambient inheritable handles before ordinary
`Process.Start`. A concurrent, unrelated process start could therefore inherit
authority that was not intended for it. The observed lifetime-file cleanup
flake demonstrated this OS behavior; serializing the test does not contain the
production authority.

Child-side validation cannot retract a handle already inherited by another
process. The negative invariant must be enforced at the exact Windows child
creation boundary.

## Decision

One low-level Platform owner serializes every production process creation in a
process. Ordinary launches pass through the same gate. Managed launches use
`CreateProcessW` with `STARTUPINFOEX`,
`PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, and `EXTENDED_STARTUPINFO_PRESENT`.

Long-lived authority handles remain non-inheritable. Inside the serialized
create scope, the owner creates short-lived inheritable duplicates only for
the declared edge, builds the child's environment using those duplicate handle
values, creates the child with the explicit handle list, and closes every
parent-side duplicate and native allocation on success or failure. Unknown or
ambient handles are never inferred into the list.

The closed allowlists are:

| Parent to child edge | Inherited authority handles |
| --- | --- |
| Distribution Launcher or Setup to Root Bootstrap | START pipe, ADMITTED pipe, Bootstrap lifetime lease |
| Root Bootstrap to version Launcher | Launcher READY pipe, Launcher lifetime lease |
| Version Launcher to Desktop | Application READY pipe, Application lifetime lease |
| Legacy or direct Bootstrap start | Empty |

The named Job handle is opened by identity in the child and is not inherited.
Setup marker, root/tree custody, writer-lease, and all other ambient handles are
excluded. Standard streams are excluded unless a later contract names and
tests them as a new handle kind.

At process entry, each child immediately captures its expected pipe into a
typed owned handle and clears inheritance before it can start a descendant.
The existing exact path/role checks, Job admission, one-use handshakes, READY
ordering, timeouts, rollback, and `TerminationUnconfirmed` outcomes do not
change.

## Invariants

1. Every edge has one complete typed allowlist; omission or an unknown handle
   fails closed.
2. Original authority handles are never made inheritable.
3. Parallel launches cannot cross-inherit declared or undeclared handles.
4. Duplicate, attribute-list, environment, validation, or process-creation
   failure closes all temporary authority and cannot leave a late-start path.
   If a child exists but cannot be returned or resumed, the owner terminates it
   and confirms exit through the original process handle before releasing the
   temporary duplicates; failure to terminate or confirm exit is surfaced.
5. Final executable topology validation remains immediately before native
   process creation inside the same serialized start scope. This decision does
   not claim atomic image validation and loading or remove the residual OS
   scheduling boundary declared by ADR 0056.
6. No state phase, wire schema, protocol version, firmware behavior, profile,
   CRC/header rule, or recovery mutation authority changes.

## Consequences and evidence

The Platform assembly owns the sole Windows native child-create implementation;
Application continues to own launch and rollback policy, Version Management
Infrastructure binds typed handles to each edge, and executable entry projects
only capture and compose existing protocol state.

Focused tests must prove intended-handle delivery, rejection of an independent
inheritable sentinel by exact physical identity, parallel-launch isolation,
failure cleanup, Unicode argument/environment handling, and immediate
child-side de-inheritance. An architecture guard rejects direct production
`Process.Start` or a second managed `CreateProcess` owner outside Platform.

This implementation is R2 architecture/runtime-safety work. Using it to ship
Launcher/Setup remains an R3 release decision with exact-artifact smoke,
package, SBOM, provenance, signing, and release-owner evidence.

## Rejected alternatives

- Serializing tests: hides the production race without containing authority.
- Clearing inheritance only after `Process.Start`: the leak already occurred.
- A global lock around ordinary `Process.Start`: does not prove an explicit
  inherited-handle set and cannot contain external starts.
- Letting children ignore unknown handles: does not revoke leaked capability.
- A second protocol, broker, or Application-owned native launcher: duplicates
  the existing execution model and violates layer ownership.
