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

Bundle mode commits the effective primary output only inside the bundle directory.
The operator may independently edit the primary filename under the same compiled
override policy as loose output (ADR 0036). The accepted automatic preparation,
tokens and clock remain unchanged; one effective name and override provenance
flow through destination validation, execution, primary/bundle receipts and report.
Editing the folder or toggling Bundle must not reset the primary-name draft, and
editing the primary must not change the folder, source names or additional-artifact
names. This is delivery naming only, not a firmware-byte change.
The
Application destination request carries the exact accepted source identities,
stamps, and immutable bytes already used by execution. Repeated canonical
identity plus stamp is delivered once. Different sources with the same basename
are ordered by canonical binding/slot order and receive ` (2)`, ` (3)`, and so
on without changing the originals.

When bundle intent is enabled, optional additional artifacts, including the ADR
0037 A-only FlashCode, are members of that same transaction. Effective primary output,
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
Reports add resolved bundle/artifact provenance and hashes; the effective primary
name follows ADR 0036 while canonical automatic naming provenance remains retained.

## Independent additional-output names (2026-09-21)

The owner requires every generated BIN to be independently renameable in Output
Settings, including the optional A FlashCode. Primary, additional and folder
names retain separate draft and accepted values. Completing one edit validates
against the other fields' accepted values; an invalid edit restores only its own
accepted value. Unselecting additional delivery excludes its name from validation
and submission. Delivery toggles retain drafts; a new incompatible preparation
does not inherit the previous additional name.

Application derives an effective additional `FileName` from the selected exact
prepared plan, retaining its canonical `SuggestedFileName`, profile, kind and
source range. An override without a selected declared delivery is rejected.
Bundle preflight, artifact planning and commit consume the same effective name
through existing collision allocation; receipts/report retain the actual delivered
name and hashes. Loose Save dialogs start with the edited name and preserve the
distinction between automatic and explicit names. Source copies, firmware bytes,
CRC, output eligibility and report schema are unchanged. Independent design
admission: `/root/layout_ownership_review`; implementation evidence is maintained
in [the delivery checklist](../ui/v1.1.10-delivery.md).

## 1.1.9 name-edit validation amendment (2026-09-19)

Owner-approved recovery keeps separate accepted folder and primary-name values.
Keystrokes preserve the exact draft and show validation; explicit completion
accepts a valid edit or restores only that field's last accepted name. A
localized recovery notice never masks a current blocking destination issue.
The same draft/confirmation surface applies to loose and bundled primary names.

The existing `ICompositionOutputBundleDestinationValidator` gains a pure
`ValidateName(string)` operation, forwarded by `ICompositionOutputNaming`.
It returns the existing typed name issue or null, without accessing a parent
directory. `AtomicBundlePathRules` remains the single platform rule owner:
blank/reserved/invalid names and components longer than 255 UTF-16 units
(including extensions) are rejected without trimming or truncation. The
existing full-path limit remains unchanged; a valid component is not a valid
destination or authority to execute.

For a bundled edit, path-aware acceptance checks the candidate against the
other field's accepted value, not its uncommitted draft. Unrelated parent or
protected-input failures still block delivery but do not reject an otherwise
valid component. After completion/recovery, overall validation uses both
current drafts and the existing destination validator. Loose confirmation
checks the name before picker/execution; native picker final paths and actual
writers retain their own destination checks. No new filesystem, firmware or
profile authority belongs to Presentation. Generated staging/collision name
limits and general long-path support remain separate follow-ups.

Independent architecture admission: `name_validation_design`, 2026-09-19,
approved the existing-owner extension and these failure/ownership constraints.

## 1.1.9 actual Bundle destination preview amendment (2026-09-20)

`CompositionOutputBundleIntent.AdditionalDelivery` exposes the existing admitted
immutable plan through a public getter so the destination adapter can include
its exact suggested filename. This projection does not select eligibility,
recompile a delivery, or change the accepted plan.

`AtomicBundlePathRules` owns the shared filename allocation used by preview and
commit: reserve the primary name, then allocate additional deliveries and
accepted sources in their existing order with case-insensitive collisions and
numeric suffixes. Preview checks the actual allocated child components and
paths, and checks every child against every accepted source identity. It also
validates the actual suffix-resolved folder component. The existing 255 UTF-16
component and 259-character full-path limits remain unchanged. Preview does not
reserve or create a destination; commit retains its race-time checks.

This closes the actual collision-name preview gap identified in the prior
amendment. General long-path support and native-picker end-to-end evidence are
separate work. No source bytes, identity, A-only eligibility or firmware
semantics change. Independent R2 design admission: `name_validation_design`,
2026-09-20, recorded in `OUTPUT-119-PREVIEW-03`.

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
