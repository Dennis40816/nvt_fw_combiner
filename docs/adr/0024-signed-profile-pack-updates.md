# ADR 0024: Separate signed profile-pack updates from application releases

- Status: Proposed — owner signing/update acceptance required
- Date: 2026-07-16
- Owners: Product owner + architecture owner + security/release owners
- Target: v0.9.11, planned during v0.9.9
- Related: ADR 0015, ADR 0018, ADR 0022, ADR 0023,
  profile-bundle-v1, release-manifest-v1

## Context

The owner wants a new IC to require only configuration-like data and no C#/.NET
application rebuild when the IC is fully expressible by the existing approved
firmware-family, map, profile, operation, validation, and processor contracts.
The longer-term direction also includes automatic version detection and updates.

Today the portable package carries an immutable built-in bundle. Its release
manifest records the expected profile/schema/processor digests, and the existing
loader receives its expected bundle hash from a release/install authority. A
bundle cannot trust a digest stored inside itself. The application therefore
already has most of the deterministic validation/compiler boundary, but it does
not yet have an independently versioned external pack, trusted update index,
signing policy, atomic activation pointer, or rollback policy.

"No rebuild" means no new application binary for declarative IC facts that fit
the current language. The trusted profile compiler still normalizes, validates,
and compiles those documents into the one execution plan at runtime. It does
not mean executing raw configuration directly or bypassing compilation. A new
schema, operation, processor kind/id, integrity algorithm, executable, or other
firmware semantics requires an application release and its normal R2/R3 review.

This ADR changes no composition kind, experience, address space, range, bytes,
support state, runtime loader, package, feed, or signing implementation. It
records the proposed boundary before contracts and code are authorized.

## Decision drivers

- Add declarative IC facts without rebuilding an otherwise compatible engine.
- Preserve one schema/normalizer/compiler/executor and fail closed on new
  semantics.
- Treat signatures and externally supplied hashes, not HTTPS or self-declared
  metadata, as the content trust boundary.
- Keep candidate intake separate from stable support-pack publication.
- Install atomically, retain a known-good fallback, and work offline.
- Detect app and pack versions independently without automatic support
  promotion.
- Keep the Legacy Combiner 1.13.0 executable/runner exception and the exact
  `legacy-combiner-1.13.0` binding constrained; prevent packs from adding
  arbitrary tools or command authority. A future combiner replacement remains
  a separately reviewed external tool package.

## Considered options

1. **Fixed engine plus independently signed declarative profile pack
   (recommended).** Reuse the existing contracts, loader, compiler, and engine;
   add a release-owned pack authority and atomic activation boundary.
2. **Rebuild the application for every IC.** This preserves today's trust path
   but does not meet the configuration-only authoring goal and couples support
   data to engine distribution.
3. **Load arbitrary local folders or feed JSON directly.** This lets transport
   or writable files define trust and cannot provide provenance, rollback, or a
   closed snapshot.
4. **Ship scripts/plugins in an IC pack.** This creates a second executable
   extension system and bypasses the processor allowlist and package review.
5. **Combine application self-update and profile-pack activation in one
   updater.** This enlarges the first security/recovery slice and obscures which
   compatibility boundary failed.

Options 2 through 5 are rejected. Option 1 remains Proposed until the owner and
required security/release reviewers accept the decisions below.

## Proposed decision

### Two independently versioned lanes

```text
signed application release
  -> engine/app version detection
  -> official package acquisition or launch handoff

signed profile-pack release
  -> profile-pack version detection
  -> staged verification and compatibility check
  -> atomic active-pack switch
  -> retained previous known-good pack

active trusted pack
  -> existing bundle loader/normalizer/compiler
  -> existing one composition planner/executor
```

- The application version and `profilePackVersion` are independent semantic
  versions. A pack declares an engine compatibility range; compatibility is
  checked before activation and again at startup.
- v0.9.11 initially detects and prompts for application updates. It may open or
  acquire an official reviewed release, but in-process replacement of the
  running portable application is deferred.
- v0.9.11 initially defaults to detect-and-prompt plus explicit user-approved
  profile-pack installation. Future opt-in automatic installation may be
  considered only for signed stable-channel packs after operational evidence.
- Detection failure, an offline host, or an unavailable feed leaves the current
  verified active pack usable. It does not remove built-in support.

### Pack contents and semantics fence

The pack is a closed production profile bundle plus release-owned metadata. It
may contain only documents already admitted by the installed engine's approved
schema and processor allowlist. It may not contain:

- executables, DLLs, scripts, BAT files, macros, or tool binaries;
- arbitrary processor parameters, command lines, or executable paths;
- new operation/processor kinds or ids, schema versions, runtime dependencies,
  native libraries, or UI code; or
- candidate records represented as stable supported profiles.

An already approved external processor binding must be installed and verified
by application/release authority. A pack cannot install or replace Legacy
Combiner, a future `combiner.exe`, the Python CRC worker, or any other processor.
The current Legacy Combiner 1.13.0 executable, exact comparison command,
`legacy-combiner-1.13.0` binding, and constrained runner remain the explicit
evidence baseline. A future replacement combiner is delivered as a separately
signed and hashed external tool package with reviewed manifest/executable and
invocation assets, staging requirements, owner evidence, and clean-machine/
package gates. IC/mode-specific read/write ranges remain in reviewed V2 profile
data and the compiled plan; no tool package grants firmware mutation authority.
The postbuild algorithm remains outside this application. Packs may select only
an already installed allowlisted binding and cannot supply executable paths or
free-form argv.

### Trust and discovery

- A release/install authority verifies the signing identity and supplies the
  authenticated expected profile-bundle content hash to the existing loader. A
  digest or key declaration inside the downloaded pack cannot establish trust
  in that pack.
- HTTPS protects transport but is not the artifact trust decision. The staged
  archive/manifest signature, archive hash/size, closed unpacked inventory, and
  canonical bundle content hash are all verified before activation.
- A future versioned pack manifest/index contract must minimally bind the pack
  id/version, engine compatibility range, bundle content hash, archive hash and
  size, channel, signer/key id, release timestamp, and downgrade policy. This
  ADR does not add that schema or select a signing algorithm.
- Feed locations and trusted keys are release/install policy, not arbitrary
  per-run UI values. The first implementation adds no secrets or telemetry.
- Stable and candidate channels remain separate. Locally generated candidate
  intake output never overwrites or automatically joins the stable support pack.

### Staging, activation, and rollback

1. Fetch metadata and immutable bytes into a bounded staging location.
2. Verify the selected signing policy, signer, archive hash/size, release
   identity, channel, and anti-downgrade/replay policy.
3. Extract into a new immutable version directory with closed-inventory,
   traversal, link/reparse, duplicate/case-collision, entry-count, expansion,
   and size limits.
4. Supply the externally authenticated expected content hash to the existing
   profile-bundle loader; normalize and compile the complete pack side-effect
   free. Reject unknown schemas, operations, processors, aliases, or
   incompatible engines.
5. Atomically replace a small active-pack pointer only after the complete pack
   passes. Never mutate the current version directory in place.
6. Retain the prior known-good pack and the immutable built-in fallback. On an
   activation/startup failure, restore the last verified pointer and surface an
   explicit diagnostic rather than selecting files piecemeal.
7. Revalidate the active pack's signature/content binding on every startup.

Concurrent checks and installs serialize through a handle-owned lock and
revalidate source/destination identity immediately before publication. Power
loss or cancellation may leave an unreferenced staging/version directory for
safe cleanup, but never a partially active pack.

### Version and user experience

Settings remains the only top-level home for update status. It shows the
current application version, active profile-pack version/channel, last check,
signature/compatibility result, available versions, and rollback diagnostics.
It does not add a top-level tab or hide a failed trust/compatibility result.

Silent downgrade is forbidden. Retention count, downgrade override, clock
handling, key rotation/revocation, and emergency rollback are owner policies
that must be explicit before implementation. Update detection and pack
activation never change a candidate's promotion stage or replace firmware-owner
golden/parity approval.

## Consequences

### Positive

- Compatible IC facts can ship without rebuilding the application.
- The existing compiler and engine remain the only firmware execution path.
- Pack failure has an atomic rollback path and the portable package keeps a
  built-in offline fallback.
- Application and profile data can move at different cadences without treating
  a network source as trusted.

### Negative / trade-offs

- Signing, key custody/revocation, feed operation, rollback, and compatibility
  become release-owned product surfaces.
- A new IC that needs an unknown operation, schema, processor, or tool still
  requires an application release.
- Supporting both built-in and one active external pack adds measured storage,
  cleanup, and recovery responsibilities.
- Automatic installation remains deferred until signing and operational
  evidence are complete.

## Compatibility and migration

1. Keep the currently packaged built-in bundle as the immutable fallback.
2. Accept signing, feed, compatibility, retention, and UI policies below.
3. Review and version the pack release manifest/index contracts without
   changing shared profile schemas or the compiler.
4. Implement detection and signature/hash verification behind typed
   Application ports; Infrastructure owns network, staging, and install state.
5. Reuse the existing loader/compiler for a full side-effect-free compatibility
   probe, then add atomic pointer activation and rollback.
6. Add Settings status/prompt UI over the same Application use cases.
7. Enable stable-pack installation only after clean-machine, package,
   signing/legal, recovery, and security review. Keep application installation
   as an explicit external handoff in the initial release.
8. Consider opt-in automatic stable-pack installation only in a later reviewed
   slice with operational telemetry/privacy policy and rollback evidence.

## Verification plan

- Contract vectors for canonical pack/index bytes, detached-signature tampering,
  wrong/unknown/revoked signer, stale/replayed metadata, downgrade, clock skew,
  key rotation, and archive/content-hash disagreement.
- Compatibility tests for older/newer engines, unknown schema/operation/
  processor, missing approved processor binding, invalid support stage, and no
  candidate-to-stable promotion.
- Extraction/snapshot tests for traversal, alternate separators, links/reparse
  points, duplicate/case-colliding entries, archive bombs, entry/size limits,
  source swaps, and closed inventories.
- Install tests for concurrent checks, interrupted download/extract/activation,
  cancellation, power-loss checkpoints, atomic pointer publication, startup
  revalidation, rollback, cleanup, and immutable built-in fallback.
- Offline, unavailable/captive feed, invalid metadata, proxy/TLS, and clean-
  machine portable-package tests with no loss of the active verified pack.
- UI tests for explicit consent, signature/compatibility diagnostics,
  accessibility/localization, and no arbitrary feed/executable selection.
- Exact pack provenance, SBOM/package inventory, release hashes, Polytail,
  independent architecture/security/release review, and every applicable R3
  firmware-owner/golden gate.

## Release impact

- v0.9.9 gains architecture planning only. No runtime, dependency, network,
  signing, package, schema, support, or firmware behavior changes.
- v0.9.10 authors candidate configuration through ADR 0023 but does not publish
  or activate a stable pack automatically.
- v0.9.11 is the first proposed detection/activation boundary. This ADR alone
  does not authorize a feed, signature algorithm/library, contract/schema,
  trusted key, auto-install behavior, or release artifact.
- Source and portable-package size remain subject to the active milestone
  ratchets; update functionality does not waive them.

## Decisions and reviews required for acceptance

1. Select the signature algorithm/library, trust-store location, key custody,
   offline roots, rotation, revocation, and emergency-response policy.
2. Select the official feed host, stable/candidate channels, authentication,
   availability, retention, and privacy/telemetry policy.
3. Accept detect-and-prompt with explicit stable-pack installation as the
   v0.9.11 default; any automatic installation requires a later explicit opt-in
   decision.
4. Define engine compatibility, anti-downgrade/replay, clock, retention-count,
   and manual/emergency rollback rules.
5. Accept application update detection plus official acquisition/handoff for
   v0.9.11, with in-process application self-replacement deferred.
6. Require architecture, security, dependency/package, signing/legal, and
   release-owner review before implementation or activation.
