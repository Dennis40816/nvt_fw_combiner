# BUG-20261002-accepted-bytes-null-conditional: three `AcceptedBytes` properties return an empty memory instead of null

Status: open for two properties (latent, not reachable today); the third is fixed on the R02-02 branch
Severity: P3 today (no production path observes it). It becomes a P2 the moment General inspection stops capturing
bytes (`1.2.4`): an inspection without bytes would be rejected with an argument error, or accepted silently for a
zero-length file.
Found: 2026-10-02, the commander, while building and running the R02-02 change (bounded identity-only inspection)
outside the Codex sandbox: the identity-only request returned accepted bytes that were not null.
Where: the same expression in three places,
`AcceptedByteArray is null ? null : new ReadOnlyMemory<byte>(AcceptedByteArray)`, typed `ReadOnlyMemory<byte>?`:
- `SelectedFileContentInspection.AcceptedBytes`, `src/NvtFwCombiner.Application/Ports/ISelectedFileContentInspector.cs`;
- `AuthoringSlotState.AcceptedBytes`, `src/NvtFwCombiner.Application/Authoring/AuthoringSessionModels.cs`;
- `GeneralSelectedFileInspection.AcceptedBytes`, `src/NvtFwCombiner.Application/Authoring/GeneralSelectedFileInspection.cs`.
Observed: with a null array the property has a value, an empty memory. A test that expected null failed with
"Value of type 'Nullable<ReadOnlyMemory<byte>>' has a value"; a scratch compilation by the reviewer showed
`HasValue=True, Length=0` for the expression and null once the null branch is written `(ReadOnlyMemory<byte>?)null`.
Expected: null when no bytes were retained, as each property's documentation says.
Cause: the conditional has a natural type. `null` converts to `ReadOnlyMemory<byte>` through the implicit operator
from `byte[]`, so the whole expression is a `ReadOnlyMemory<byte>` and the null branch yields an empty memory; the
target type `ReadOnlyMemory<byte>?` is never consulted. `AuthoringInputSlotInspection.AcceptedBytes` already
carries the explicit cast, so the trap was met before and fixed in one place only.
Effect today (independent review of the R02-02 commit, Claude Opus 5.5, read-only):
- `SelectedFileContentInspection`: every producer passes bytes (the inspector's capture mode; the AB input
  inspection builds the record only with bytes), so no consumer sees a difference. Two `?? throw` guards and one
  `is null` guard on this property were dead code.
- `AuthoringSlotState`: no code under `src/` reads the public property; execution reads the internal array.
- `GeneralSelectedFileInspection`: the only consumer copies the value into a new revision. An inspection without
  bytes would become an empty non-null array and then fail a length check with an argument error, or pass silently
  for a zero-length file. Every General inspection carries bytes today.
Fix: write the null branch as `(ReadOnlyMemory<byte>?)null`.
- `SelectedFileContentInspection.AcceptedBytes`: fixed with R02-02 (branch `feature/1.2.4/bounded-identity-inspection`,
  where identity-only inspection needs it), with tests for both modes.
- `AuthoringSlotState.AcceptedBytes` and `GeneralSelectedFileInspection.AcceptedBytes`: open. They are fixed in
  `1.2.4` together with the change that lets General inspection run without captured bytes (R02-03), with a test
  for an inspection that has no bytes; fixing them alone changes no reachable behavior and has no test that can
  fail first.
Prevention: the Codex dispatch rules now name this expression as a build-time trap; a search for
`? null` followed by `new ReadOnlyMemory` under `src/` finds exactly these three places at `092641516`.
Owner: the `1.2.4` General input owner.
