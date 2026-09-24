# Event Buffer Format configuration v1

This bounded persistence contract implements [ADR 0072](../adr/0072-event-buffer-format-configuration.md).
It does not define firmware effects, support, or Build readiness.

## Document

The host supplies one fixed local path (filename convention:
`event-buffer-format.v1.json`). Neither the document nor a per-operation caller
chooses another path. The [canonical schema](event-buffer-format-configuration-v1.schema.json)
defines exact camelCase properties. UTF-8 without BOM, one JSON root, unique
property names, no comments/trailing commas, maximum 64 KiB and depth 8 apply.
Maximums are 64 entries, 256 values per entry, 128 characters per scope/identity,
and 256 alias characters. Recognition values use `0x` and exactly two hexadecimal
digits; Application save emits uppercase digits. Unknown identities and duplicate/conflicting
byte memberships are rejected by Application admission, not a second codec registry.
Aliases affect display only; an empty set matches nothing.
Raw and escaped strings must contain valid Unicode scalar sequences. Malformed
UTF-8 and unpaired UTF-16 surrogates are format errors, never silent replacement
characters; schema/programmer failures are not relabeled as bad user input.

## Persistence and session

The adapter reuses bounded stable reads and atomic writes from `ILocalFileStore`.
The SHA-256 describes the complete bytes actually read or successfully written,
not a normalized rendering. Session generation is a separate monotonically
increasing publication number, not that hash.

Save defensively snapshots and admits the draft before its first await. Save and
reload serialize. Save publishes only after successful persistence. Rejected
drafts and failed writes leave Current and LastSaved unchanged. Cancellation is
not reported as failure after a successful atomic commit.

As amended by the owner on 2026-09-20, an absent custom file publishes admitted
canonical built-in defaults as Ready with UsesBuiltInDefaults=true, without
writing a file. This also applies after deletion/restart. The built-in digest
is SHA-256 of a versioned, domain-separated, length-prefixed canonical encoding
of scope and sorted entries (identity, display name, sorted recognition bytes),
not a persisted-file hash. Custom snapshots retain their raw-file SHA-256.
Malformed, wrong-scope, semantically invalid or unreadable custom files remain
Invalid; they never fall back. Built-in publication does not update LastSaved.
Settings explicitly labels the active built-in source and uses defaults for both
editor and baseline. Defaults/Discard remain draft operations; edits require
Save. Previously captured immutable snapshots remain unchanged, and pre-Build
admission still checks the current effective rules.

This unit does not wire startup initialization, watchers, pre-Build refresh,
runtime selection, UI, or CLI. Those integrations must consume this result and
their own accepted readiness contracts; NotLoaded is not permission to use
Common. An already-running operation retains its accepted captured state.
