---
name: nfc-release-readiness
description: Prepare, audit, or troubleshoot an NFC prerelease/stable release, complete release notes, Windows portable package, GitHub Actions release workflow, SBOM, hashes, provenance, signing, or clean-machine smoke test.
---

# Release Readiness

The [release-package contract](../../../docs/ci/release-package.md#implemented-commands)
and [branch/version governance](../../../docs/governance/branch-version-and-release-governance.md)
own candidate eligibility, packaging, publication, notes and recovery. Load the
branch relevant to the requested release stage. This skill grants no additional
publication authority.

Keep one release identity across the reviewed source, merged tree, candidate
run, artifact digest, annotated tag and peeled commit, manifest, provenance and
published assets. Ancestry or a matching message is not tree equality. Normalize
only transport CRLF/LF when comparing tag messages; all logical identity fields
remain exact. Verify tag, VERSION, assemblies, worker, changelog, package names
and notes agree.

Before promotion, require the contract's exact-source CI and fresh execution
of every applicable owner-certified Golden case against the candidate source.
Compare complete outputs under their approved contract. Input-only evidence,
fixture hashes and a project name do not prove Golden execution. Inspect the
closed package allowlist, clean-machine smoke, hashes, SBOM, notices and
provenance using the canonical contract; record omitted human/private gates.

Release notes explain user outcomes, before/after behavior, affected workflows,
support and compatibility impact, upgrade/rollback guidance, evidence and
known limitations. Commit lists support them but cannot replace them. Keep
private firmware, evidence paths and credentials out of public artifacts.

On authorized publication, follow the protected workflow and its stage-specific
recovery. Stable tags and assets are immutable. After publication, independently
download and hash assets, inspect provenance and run the prescribed smoke test;
confirm the tag-derived source archives resolve too. Report exact identities,
commands, results, retry history and residual gates. A failed or omitted gate
is never a pass; use the contract's recovery rather than manual asset repair.
