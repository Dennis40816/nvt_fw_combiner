---
name: release-readiness
description: Prepare, audit, or troubleshoot an NFC prerelease/stable release, Windows portable package, GitHub Actions release workflow, SBOM, hashes, provenance, signing, or clean-machine smoke test.
---

# Release Readiness

1. Confirm version consistency across tag, `VERSION`, assemblies, worker, changelog, and release manifest.
2. Require all protected CI checks and supported-matrix golden regressions.
3. Build only from a reachable protected `main` commit in an approved release environment.
4. Verify package contents against `docs/ci/release-package.md`; fail on extra PDBs, source, `refcode`, tests, unmanifested firmware, private golden inputs, caches, or credentials. Only owner-approved golden fixture BINs declared by manifest may ship under `reference/`.
5. Run the package on a clean Windows x64 environment with no preinstalled .NET or Python.
6. Execute app startup, profile load, worker `123456789` self-check, synthetic preview/build, and report generation.
7. Generate SHA-256, SBOM, third-party notices, and provenance/attestation; sign according to the approved policy.
8. Upload immutable versioned artifacts. Never silently replace a stable release asset.
9. Produce a release evidence summary and unresolved-risk decision.
