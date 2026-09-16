---
name: release-readiness
description: Prepare, audit, or troubleshoot an NFC prerelease/stable release, complete release notes, Windows portable package, GitHub Actions release workflow, SBOM, hashes, provenance, signing, or clean-machine smoke test.
---

# Release Readiness

Before release work, follow the [release-package contract](../../../docs/ci/release-package.md#implemented-commands)
and [branch/version governance](../../../docs/governance/branch-version-and-release-governance.md).
They own candidate eligibility, workflow authority, historical exceptions and
recovery; this skill is not a separate publication path or permission grant.

## Lock The Release Identity

1. Record the reviewed feature/version head, final merge commit, trees and
   intended tag. After tag creation, verify its annotated object and peeled SHA
   against that candidate. If the platform creates a merge commit, require the
   merged tree to equal the reviewed tree; messages or ancestry alone are insufficient.
2. Confirm version consistency across tag, `VERSION`, assemblies, worker,
   changelog, release manifest, package names, and release notes.
3. When comparing an annotated-tag message returned by GitHub, normalize only
   transport CRLF/LF line endings. Keep every logical line, tag field, source
   SHA/tree, candidate run, manifest digest, artifact digest, release body,
   asset name, and asset hash as exact identity checks.
4. Require the contract's exact-source CI admission. The stable candidate job
   performs the required fresh Golden verification, packaging and smoke before
   protected promotion. `main-package.yml` is a separate manual preview: it is
   neither an extra stable-release prerequisite nor a substitute for candidate
   verification. Build only the admitted exact source in the approved environment.
5. Never move or silently replace a stable tag or stable asset. A source or
   behavior correction requires a new version decision.

## Verify The Portable Package

1. Require all protected checks and applicable supported-matrix golden
   regressions. Keep support-neutral authoring separate from support promotion.
2. Verify package contents against `docs/ci/release-package.md`; fail on extra
   PDBs, source trees, `refcode`, tests, unmanifested firmware, private golden
   inputs, caches, credentials, or unapproved executables. Only owner-approved
   golden fixtures declared by manifest may ship under `reference/`.
3. Run the package on a clean Windows x64 environment with no preinstalled .NET
   or Python. Execute visible app startup, profile load, worker `123456789`
   self-check, synthetic preview/build, and report generation.
4. Generate and verify SHA-256, SBOM, third-party notices, and provenance. The
   provenance source commit and tag must equal the peeled release tag.
5. A portable ZIP containing the self-contained app is the default Windows
   distribution. Do not invent a literal one-file executable requirement unless
   the owner explicitly requests and accepts its startup/package tradeoffs.

## Write Complete Release Notes

Starting with `v0.9.12`, generated commit lists are supporting data, not complete
release notes. Every user-visible feature or behavior change must include:

```text
feature name and user outcome
Before -> After behavior
affected screen/workflow and applicable IC/mode/persona
support status: promoted, unchanged/support-neutral, or removed
compatibility, migration, saved-data, package, or automation impact
measured performance/size result when claimed
verification evidence and remaining human gates
known limitations and explicitly deferred follow-up
```

Also include a concise summary, fixes, upgrade/rollback guidance, download names,
hash/SBOM/provenance guidance, and known issues. Do not expose private firmware,
golden paths, secrets, audit-only jargon, or unsupported claims. A release note
must explain the shipped product state to a user; it must not merely restate PR
titles or implementation details.

## Publish And Independently Verify

1. Once exact-source CI passes, dispatch `release.yml` from protected `main`
   with the approved exact release-source SHA, final merged PR and required
   inputs. After candidate verification and protected `release` approval, the
   promotion job creates the annotated stable tag; do not create it manually
   beforehand. Workflow authority and release-source eligibility remain distinct.
2. Let that workflow publish the complete contract-declared candidate asset set.
   Confirm the Release is immutable, neither draft nor prerelease; do not replace
   workflow-owned publication with manual uploads or repair immutable assets.
3. Confirm GitHub's tag-derived source `.zip` and `.tar.gz` downloads resolve in
   addition to the uploaded Windows package. Source archives are GitHub-generated
   release downloads, not uploaded binary assets.
4. Download the published assets into a fresh directory, compare every GitHub
   digest, inspect provenance identity, and run `scripts/smoke-release.ps1`
   against the downloaded ZIP.
5. Produce a release evidence summary with exact URLs, SHAs, sizes, commands,
   retry history, and unresolved clean-machine, accessibility, signing, legal,
   firmware-owner, or private-golden gates. Never describe an omitted gate as
   passing.

If a run fails, diagnose the failure and follow the contract's stage-specific
recovery rules. If promotion fails after tag creation, eligible recovery reruns
only that failed job in the same workflow run, preserving candidate/run/artifact
identity. A new run cannot reuse that stable version; immutable Release
conflicts require a new-version decision, not in-place repair.
