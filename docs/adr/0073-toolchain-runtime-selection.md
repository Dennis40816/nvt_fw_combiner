# ADR 0073: Transactional Toolchain runtime selection

- Status: Accepted for configuration publication and candidate verification; deployment remains separately admitted work
- Date: 2026-09-20
- Related: [ADR 0006](0006-external-combiner-tool-runner.md), [ADR 0072](0072-event-buffer-format-configuration.md)

## Context

The owner approved Settings > Config > Toolchain with the bundled VC++ runtime
as default and an optional verified user installation. A failed explicit user
selection must remain visible and block dependent Builds, not silently revert
to the bundled runtime. The approved page and safety decisions are recorded in
the [Toolchain handoff](../ui/v1.1.9-toolchain-runtime-handoff.md).

## Decision

Application owns one Toolchain configuration session with immutable generations.
It follows the existing Config draft/Save/Discard/Reset transaction pattern,
without putting executable configuration into firmware-specific Event Buffer
types. Infrastructure implements bounded stable reads and atomic writes through
the existing local-file port and a separate versioned configuration document.

A selection contains its source and, for User only, its original path and
accepted SHA-256. Save and Reload require an explicit complete Verified result
from the candidate-inspection port and exact identity equality. Empty issues,
PE header facts, filename, file version, or a prior successful inspection are
not proof of complete candidate verification or runtime readiness.

Missing configuration selects Bundled. Invalid, unreadable, deleted or changed
explicit configuration publishes a blocked state. The snapshot retains the
requested User identity separately from its nullable admitted selection, so the
UI can keep that choice selected and explain the failure. A failed Save retains
the previous publication; a failed Reload cannot silently use that publication.

The session serializes writes and reloads. Atomic persistence success is followed
by one publication with a new generation, even if cancellation arrives after
the storage commit. Observer failures cannot undo persistence or describe a
committed Save as failed. Detect and Inspect are read-only and do not select,
install, persist or run the candidate. Reset prepares a Bundled draft and uses
the same Save path.

## Integration requirements

Configuration publication is not execution admission. The external environment
must capture the same selection generation for readiness and processor use,
reject stale publications before new Build admission, and preserve an already
started operation's immutable execution lease. A notification callback may
schedule refresh but cannot be the safety mechanism that invalidates old leases.
Workflows without the dependency must remain independent.

Windows candidate verification follows the separately reviewed
[Toolchain runtime trust probe v1](../contracts/toolchain-runtime-probe-v1.md):
an internal trusted-host child verifies strict WinTrust signer evidence while
bounded PE inspection compares the selected DLL against the manifest-pinned
Combiner imports without loading candidate code. Isolated deployment and
processor consumption still require separate admission before integration.
This configuration decision does not relax the
manifest, processor, firmware, Golden or release contracts. A session tested
only against an inspector fake is not a completed Toolchain feature.

## Verification

Cover missing/invalid configuration, strict JSON transport, exact identity and
explicit Verified admission, failed persistence, changed/deleted reloads,
serialized publication, cancellation after commit, and read-only detection.
Integration additionally requires generation-race tests, actual selected DLL
loading evidence and the approved page rendered in normal, invalid, narrow,
Light and Dark states. Existing configuration and processor paths remain covered.
