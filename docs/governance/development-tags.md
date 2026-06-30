# Development Tags and Milestone Nodes

Tags are immutable annotated SemVer tags describing code that exists. Future milestones are reserved here and are not pre-created against the wrong commit.

## Initial node

- `v0.1.0-dev.0` — init/bootstrap and contract definition node. Includes specification, governance, .NET/Avalonia solution skeleton, installers, CI/release skeleton, two Python references, domain proof primitives, external combiner runner contracts, and no firmware-parity claim.

## Branch and merge policy

- `0.1.0` is the dev0 contract branch.
- `0.1.1` is the active UI planning branch.
- `main` is the stable branch.
- Progress to `main` must happen through reviewed merge/PR, not direct unreviewed development pushes.
- Agent/Codex work should stay on the active milestone branch until review gates pass.

## Milestone scope

Current execution priority: normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows are pulled forward. AB merge remains in the roadmap but is deferred until the owner reactivates it. NT51950/NT51951 normal Merge waits for owner memory maps; Replace CRC/header waits for owner-supplied `combiner.exe` invocation and golden evidence.

| Milestone | Scope | Implementation boundary |
| --- | --- | --- |
| `0.1.0-dev.N` | Dev0 contract definition and verification | Small proof primitives only: ranges, diff, write policy, manifest validation, policy scripts. No broad engine implementation. |
| `0.1.0-alpha.N` | Dev0 exit candidates | Contract freeze review, CI green, dev1 backlog ready. |
| `0.1.1-dev.N` | UI design and demo planning | UI documents, low-fidelity demo shell planning, terminal/log/report UX definition. No firmware semantics in UI. |
| `0.2.0-dev.N` | Dev1 non-UI composition core | Profile compiler, composition plan, operation executor, preview/report core, staging workspace, fake external processor runner. |
| `0.3.0-dev.N` | Standard merge parity | First standard IC group, golden tests, naming/version extraction. |
| `0.4.0-dev.N` | Integrity/tool processing | Legacy combiner runner hardening, CRC/Header golden cases, packaging integration. |
| `0.5.0-dev.N` | Normal Replace priority | DP Replace and CtrlRAM Replace workflows, IC num `single`/`cascade` selection, reserved `numeric` mode, and post-replace combiner readiness. |
| `0.6.0-dev.N` | AB merge | Bank layout, relocation, compare rules, AB golden parity; deferred until the owner reactivates it. |
| `0.7.0-dev.N` | General Merge/Replace and saved rules | Dynamic mappings, saved rule promotion, preset catalog. |
| `0.8.0-dev.N` | Packaging/security | Release packaging, tool manifests, smoke tests. |
| `0.9.0-rc.N` | UAT/release candidates | UX polish, internal sign-off. |
| `v1.0.0` | stable | Signed-off support matrix. |

## Progression

```text
v0.1.0-dev.N    dev0 contract and verification
v0.1.0-alpha.N  dev0 exit candidates
v0.1.1-dev.N    UI design/demo planning and terminal/log UX
v0.2.0-dev.N    dev1 composition core
v0.3.0-dev.N    standard merge parity
v0.4.0-dev.N    worker/tool integrity
v0.5.0-dev.N    normal Replace priority for DP/CtrlRAM
v0.6.0-dev.N    AB merge
v0.7.0-dev.N    General Merge/Replace saved rules
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
