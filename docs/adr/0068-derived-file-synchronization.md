# ADR 0068: One writer for source-derived repository files

- Status: Accepted
- Date: 2026-09-05
- Owners: Repository owner; release/tooling maintainers
- Supersedes: None
- Superseded by: None

## Context

Approved workflow changes left manually copied hashes, schema constants and
test fixtures inconsistent; a long test shard discovered the missing update.
The owner requested a common automation tool before formal verification.

## Decision

`scripts/sync_derived.py` is the only synchronization writer. A fixed typed
provider registry declares every input and output. Providers own their existing
projection rules and return complete byte plans; the runner owns safe paths,
snapshots, disjoint output ownership, diffs and atomic per-file replacement.
All providers must produce a converged plan before any write. Inputs are checked
again before writing, and all files are recaptured and replanned afterward.

Default execution checks all registered providers without writing. Local
`--write` requires explicit repeatable `--only` selection of providers whose
source changes are already authorized. CI cannot write. The canonical structure
gate runs the read-only check before expensive validation. Agents may perform
the authorized local synchronization themselves, then review the diff and check
all providers before formal verification; this is not another approval round.

The four providers cover the protected workflow contract and its raw-identity
projections, the exact CI documentation mirror, reviewed policy/index/Golden-allowlist
trust pins in their existing loader/package/smoke/test consumers, and authorized
`VERSION` projections into the two numeric SPEC/verification-report headers.
The version provider requires one complete stable-version header per target and
preserves every other byte, including status prose, dates and historical results.
Semantic validators,
not the runner, continue to decide firmware and release validity.

The reviewed-source provider has three fixed sources and four fixed consumer
files, declared separately. An explicitly owner-approved Golden redistribution
renewal permits only the allowlist's raw-SHA projection into the two existing
named package/smoke fields, with unique source-path/scalar bindings. It does
not derive approval from `VERSION` or write any source payload or authorization.

Golden expectations, historical evidence, approvals, source-authority commits,
SDK/dependency versions, `VERSION` itself and coverage ratchets are not
derived-file targets. Golden redistribution approval is never a version-header
projection. The historical development-tag index needs no entry per version
bump; actual release/tag identity still follows the release policy.
Existing package, Catalog, Registry and intake generators keep their artifact-
stage ownership; this command does not generate or publish releases.

## Alternatives and consequences

Manual replacement caused missed transitive references. Per-provider writers
would duplicate safety logic. An unrestricted hash scanner or a verifier that
rewrites its own expected values could silently accept unreviewed changes.
The fixed registry avoids those alternatives. Adding a provider requires its
source/target contract and tests; it is not a runtime plugin mechanism.

Writes are atomic per file, not a cross-file transaction. An interrupted command
fails, leaves a visible local diff and can be rerun; no recovery journal or new
evidence system is introduced. No automatic staging, commit, approval or release
action occurs. Existing R3 integration and release-owner gates remain required.

## Verification and migration

Use the sync runner and provider tests for clean/drift/write/idempotence,
projection completeness, path/reparse rejection, conflicting owners, source
races, CI refusal and protected-authority rejection. Remove the temporary
parity-specific sync CLI/writer; its existing owner retains only the pure plan.
See [Contributing](../../CONTRIBUTING.md#derived-file-preflight) for commands.
