# ADR 0050: Deliver optional output bundles through one atomic host boundary

- Status: Accepted by product owner directive on 2026-08-20; independent R2 review `PASS-WITH-HUMAN-GATE` on 2026-08-20
- Owners: Product owner, architecture owner
- Amends: ADR 0036 and ADR 0037 only when bundle intent is enabled
- Risk: R2 delivery and public host-contract change; no firmware-semantic change

Residual firmware-owner authority for A-only eligibility/ranges and release
evidence for production packages remain mandatory; this ADR does not satisfy
either gate.

## Context

Operators may need the canonical built BIN and every immutable source accepted
for that exact run in one reviewable folder. The current host commits one loose
primary output through a protected atomic file writer. Presentation owns the
file picker, while Application already owns the accepted session, typed naming
publication, input bindings, accepted bytes, and `FileStamp` identities.

Copying selected paths after Build would create a second identity owner, permit
time-of-check/time-of-use drift, and expose partial folders. Treating a bundle
as a profile delivery would also be wrong: it is host delivery intent and does
not change firmware bytes, compiled operations, profiles, or Golden evidence.

## Decision

Application accepts one optional typed bundle-delivery intent with the parent
destination and one validated plain folder name. It derives the proposed
Standard Merge name from the accepted canonical IC, typed DP/TP naming tokens,
and the same injected UTC run clock as primary output naming. Other routes use
an explicitly owned template or the canonical output basename. The default
folder name does not add a redundant `_bundle` suffix; destination collisions
still receive the next numeric suffix. Presentation never parses firmware facts
from paths, filenames, or labels.

Bundle mode commits the canonical output only inside the bundle directory. The
Application destination request carries the exact accepted source identities,
stamps, and immutable bytes already used by execution. Repeated canonical
identity plus stamp is delivered once. Different sources with the same basename
are ordered by canonical binding/slot order and receive ` (2)`, ` (3)`, and so
on without changing the originals.

When bundle intent is enabled, optional additional artifacts, including the ADR
0037 A-only FlashCode, are members of that same transaction. Canonical output,
additional artifacts, and accepted sources are staged together and promoted
once; bundle mode never requests an independent secondary destination. This
does not remove ADR 0037's loose A-only desktop delivery. The CLI separately
rejects loose A-only delivery and requires bundle intent. Neither path changes
A-only eligibility, half-open range, bytes, naming tokens, or canonical
firmware result identity.

Infrastructure validates Windows names, traversal, reserved names, protected
input aliases, and path length before visible mutation. It writes the output and
sources into one host-created sibling staging directory, then atomically moves
the complete directory to a newly allocated destination. Existing folder or
file collisions select the next numeric suffix. Cancellation or failure removes
only staging and leaves no visible partial bundle; inputs are never reopened,
moved, modified, or overwritten.

The GUI uses one in-app pre-delivery confirmation surface and a native picker
for the parent directory. CLI opt-in options create the same typed intent with
no prompt. Omitting bundle intent preserves existing loose-output behavior.
Reports add resolved bundle/artifact provenance and hashes without changing the
canonical firmware result identity.

## Consequences

- Delivery becomes one transaction boundary rather than a post-Build copy step.
- The destination adapter gains directory staging/promotion responsibility but
  no firmware or naming semantics.
- The accepted execution snapshot remains the only source-byte and `FileStamp`
  authority.
- Existing callers and output bytes remain unchanged when bundling is disabled.
- Bundle-directory atomicity is guaranteed only on one filesystem volume; the
  staging directory therefore remains a sibling of the final destination.

## Verification

- Test Standard Merge typed default naming and generic fallback without filename
  parsing, including unknown tokens and the injected UTC date.
- Test invalid/reserved/traversal/path-length names and inline UI diagnostics.
- Test deterministic source order, identical-source de-duplication, duplicate
  basename suffixes, destination races, cancellation, and injected copy failure.
- Test that an optional AB A-only artifact selected with bundle intent is
  included in the same promotion and never survives as a loose or partial
  output; separately retain the GUI loose-delivery and CLI bundle-only
  admission regressions owned by ADR 0037.
- Test that no visible folder survives failure and no selected input is reopened
  or mutated.
- Test UI cancellation/state retention, localization, keyboard/focus behavior,
  CLI parity, report provenance, and unchanged loose-output behavior.
- Run scoped Polytail, independent architecture review, and `verify.py --all`.
