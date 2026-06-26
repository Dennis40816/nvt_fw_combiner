# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future milestones are reserved here and are not pre-created against the wrong commit.

## Initial node

- `v0.1.0-dev.0` — init/bootstrap: specification, governance, .NET/Avalonia solution, installers, CI/release skeleton, two Python references, domain proof types, and no firmware-parity claim.

## Progression

```text
v0.1.0-dev.N    repository/bootstrap iterations
v0.1.0-alpha.N  bootstrap exit candidates
v0.2.0-dev.N    composition core
v0.3.0-dev.N    standard merge parity
v0.4.0-dev.N    worker/integrity
v0.5.0-dev.N    AB merge
v0.6.0-dev.N    Display/TP HW/TP FW Replace
v0.7.0-dev.N    General Merge/Replace and UX
v0.8.0-dev.N    packaging/security
v0.9.0-rc.N     UAT/release candidates
v1.0.0          stable
```

## Rules

- Create a tag only after its commit passes every gate available at that milestone.
- Never move or reuse a tag; corrections receive a new prerelease number.
- `VERSION`, changelog, assembly/worker versions, manifest, commit, and tag must agree.
- Development tags do not trigger stable publishing; only exact `vX.Y.Z` tags do.
- Stable release tags are signed once the signing policy and key custody are approved.
