# ADR 0044: Bind General selected files to content-authoritative snapshots

- Status: Accepted
- Date: 2026-07-30
- Accepted: 2026-07-30 through approved issue #248
- Owners: Application architecture owner
- Risk: R2 reproducibility and Application contract
- Builds on: ADR 0001, ADR 0003, ADR 0005, ADR 0015, ADR 0019

## Context

The shared General mapping draft established one Start + Length seam for
General Merge, General Replace, CLI, and Saved Rule adapters. Its selected-file
identity still used existence, length, and last-write time, while General
planning separately queried `FileInfo.Length`. A same-size mutation could
therefore keep the same accepted identity, and Preview/Build could consume
bytes that were not the bytes inspected for authoring.

The authoring session may retain metadata and identity but must not retain
complete BIN payloads. File watching and silent automatic rebind are forbidden.
Build must therefore reopen the file and prove that it still matches the
operator-accepted content before those bytes enter an address space.

## Decision drivers

- Deterministic Build bytes and fail-closed stale-input behavior.
- One selected-file lifecycle and issue contract for desktop, CLI, and Saved
  Rule bindings.
- Explicit authoring revision and stale asynchronous result rejection.
- No full firmware payload ownership in authoring sessions.
- Compatibility that cannot preserve timestamp authority indefinitely.

## Considered options

1. Keep path + length + last-write time as identity.
2. Retain inspected file bytes in the authoring session and execute those bytes.
3. Accept immutable length + SHA-256, retain only metadata in authoring, and
   verify reopened bytes against the accepted stamp immediately before use.

## Decision

Option 3 is authoritative.

`FileStamp` contains exactly the accepted complete-file byte length and
lowercase SHA-256. Path, display name, existence checks, and last-write time
remain non-authoritative host or presentation hints and cannot participate in
stamp equality.

One Application inspection result binds:

- the resolved slot or mapping definition;
- the current `AuthoringRevision`;
- the accepted `FileStamp`; and
- non-authoritative path, display-name, and timestamp hints.

General Merge, General Replace, workbench/UI-facing entry points, CLI, and
Saved Rule execution cross this same Application result before validation,
compilation, Preview/Build, or report projection. Each unbound mapping file is
inspected once for that definition and revision. The accepted stamp is copied
into the immutable mapping source and then into the immutable run binding.
Execution entry points reject an unbound file row with
`authoring.general.selected-file-snapshot-required`; they never inspect or
rebind it implicitly. Ephemeral CLI and Saved Rule adapters may inspect once at
their invocation boundary and then pass the bound draft inward. The desktop
retains the bound draft from Preview for Build, and clears it only on an
explicit mapping edit or file selection/Reload.

An explicit Reload/Rebind action advances `AuthoringRevision`, clears
inspection/validation/Preview/Build publications, and places the slot in
`Checking`. A result publishes only when resolution, route, capability,
revision, definition, and selected-path hint still match. Acceptance records
the new content stamp without changing the revision again. The typed mapping
draft survives this transition.

Source Slice and From File Start are authoring presets over the same explicit
operation. From File Start fixes source start at zero. `Use full file length`
copies the currently accepted length into both concrete source and target
ranges. It creates no execution-time flag. Reload changes only the accepted
stamp; a previously materialized length remains unchanged until the operator
edits the row or invokes the helper again.

Preview and Build reopen run-bound artifacts. After the complete read, the
Application compares actual length and SHA-256 with the accepted stamp before
adding the bytes to any composition address space. A mismatch emits
`input.artifact.content-snapshot-mismatch` and fails closed.

`LegacyTimestampFileStampCompatibilityAdapter` is the only named one-way
timestamp compatibility seam. It projects through complete content inspection;
the timestamp is a hint and can never create a `FileStamp`. Its caller
inventory excludes General Merge, General Replace, CLI, and Saved Rule
bindings. Delete it when remaining non-General host-selection boundaries use
`FileContentSnapshotInspector` directly.

## Consequences

### Positive

- Same-size byte mutation always changes selected-file identity.
- Authoring, validation, Preview/Build, and reports share one accepted result.
- Run bindings are immutable and Build detects changes before composition uses
  the affected bytes.
- Explicit reload has a reviewable revision boundary and cannot silently
  rewrite editable mapping ranges.

### Negative / trade-offs

- First acceptance hashes the complete selected file, and each run reopens and
  hashes it again for verification.
- Existing timestamp-shaped tests and compatibility callers must migrate to
  content stamps.
- Direct General draft execution now requires accepted file stamps; host
  adapters must cross the shared inspection use case first.

### Risks and mitigations

- A file changes while it is being read -> the adapter hashes exactly the
  admitted stream length, probes at most one trailing byte, and rejects any
  shrink or growth; run verification repeats the complete check.
- A late inspection overwrites a newer selection -> the session matches the
  complete inspection lease before acceptance.
- Helpers become dynamic execution policy -> only concrete checked ranges are
  stored in the draft and run plan.

## Compatibility and migration

The timestamp-authoritative `FileStamp` constructor is removed. General
workbench, CLI, and Saved Rule run paths use
`FileContentSnapshotInspector` through
`GeneralSelectedFileInspectionService`. Other workflows may migrate
independently without adopting General mapping semantics.

No profile, schema, processor, output initializer, overlap policy, firmware
range, output naming, or expected output byte changes. Release impact is
limited to earlier deterministic rejection of stale selected General inputs.

## Verification

- Application tests cover length/hash identity, same-size mutation, immutable
  inspection result sharing, stale asynchronous inspection, explicit reload,
  draft preservation, full-length materialization, and pre-consumption run
  verification.
- Infrastructure tests cover complete file hashing, path confinement, and
  timestamp-hint non-authority through the compatibility adapter.
- Bootstrap General Merge, General Replace, and Saved Rule tests cover the
  shared adapter path.
- Architecture tests, structure verification, scoped Polytail, and the
  canonical verifier remain required for the frozen candidate.
