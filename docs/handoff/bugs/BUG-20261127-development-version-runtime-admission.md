# BUG-20261127-development-version-runtime-admission: Development display version reaches stable admission

Status: fixed
Severity: P2
Found: 2026-11-27, Codex primary session, implementing owner task P-5 at
`feature/1.3.0/version-g0@820a66877d4a70fa249cf0beeaf5a9b16fcc4a4f`.
Where: Desktop `Program.CreatePresentationHostServices`, CLI `version-self-test`,
and `ManagedDistributionLauncherHostServices.Create`.
Observed: These callers pass informational version text to strict managed
version admission. Setting VERSION to the authorized `1.3.0-g.0` therefore
reaches `ManagedAppVersion.Parse` with a rejected suffix. The roadmap test and
several release tests also assume the repository VERSION is always stable.
Expected: Development display identity remains complete; managed identity uses
the build-owned VersionCore. Stable release paths retain strict admission, per
`docs/governance/development-tags.md#development-version-identity` and
`docs/governance/branch-version-and-release-governance.md#keep-one-stable-version-identity`.
Evidence: Informational-version caller search and the unchanged `ManagedAppVersion`
grammar at the pinned base; no startup/test execution claimed.
Owner: Codex primary session, `feature/1.3.0/version-g0`.
Resolution: The build emits VersionCore metadata. `ManagedAppVersion.FromAssembly`
reads it for the three callers. Regression tests cover the metadata path and
the strict rejection of `1.3.0-g.0`. The host ran the filtered .NET tests and
the affected `tests/scripts` files on `feature/1.3.0/version-g0`. All passed.
