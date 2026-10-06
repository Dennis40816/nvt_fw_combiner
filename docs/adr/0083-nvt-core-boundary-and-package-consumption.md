# ADR 0083: NVT Core boundary and package consumption

- Status: Accepted. The owner decided the boundary and the package choice in board decisions 322 to 326. Rules
  marked "commander's rule" implement those decisions and apply until the owner changes them. The owner answered
  open points 1 to 4 in decision 328. Open point 5 is not decided.
- Date: 2026-10-06
- Owners: the owner decides. The NFC commander session implements and records dated updates here.
- Supersedes: None
- Superseded by: None
- Relation to board decisions: decision 322 replaces the tentative goal of decisions 280 and 281 that shared a
  core only from 2.0.0. Decision 281's rule stays: every tool installs, runs and releases on its own.

## Context

NFC, NFH and NFU contain the same mechanisms, for example startup tracing, bounded file reads, process
execution, the launcher, the message center and the shell. The owner decided to move these mechanisms into
the shared NVT Core repository by 2026-10-18 (decision 322). NFC adopts them in the next 1.2.x patch.

Each move changes who owns code, which way dependencies point and how NFC builds. NFC's adoption pull
requests need one boundary, one dependency rule and one package contract to follow. This ADR records them.

## Decision drivers

- NFC's output bytes and UI must not change. UI snapshots stay identical (decision 323).
- Every tool still installs, runs and releases on its own (decision 281).
- Firmware semantics keep their owners and their authority (AGENTS.md).
- Builds stay reproducible: exact versions, lock files and locked restore (decisions 323 and 324).

## Considered options

1. Git submodule with a project reference.
2. A copy of the Core source inside NFC.
3. Packages from nuget.org.
4. Versioned Core packages committed to NFC under `vendor/nuget`.

## Decision

The owner chose option 4 (decision 324).

### Boundary

Core owns generic mechanisms. NFC's module list is in decision 322: the launcher (descriptor, package
verification, activation, recovery and rollback), the message center, the shell with navigation and
workspace, report lists, shared locale and accessibility strings, the process runner, the path guard with
bounded reads and hashes, the clock and the startup trace. The NVT CORE session moves NFC's theme, focus
behaviors, JSON persistence and loading surface. The shell move covers only navigation history and the existing
page host interface for now (decision 328).

NFC keeps firmware writing, CRC, profiles and NFC's product identity (decision 322). NFU's atomic output never
replaces NFC's hardened firmware writer (decision 322). Commander's rule: NFC also keeps its output policy and
output naming, its package payload and trust policy, its release and signing authority, and its product
wording and pages. Core's package verification checks mechanics. NFC decides which packages it trusts.

### Dependency direction

- NFC depends on Core packages. Core never depends on NFC or on any other tool.
- The Core skeleton targets `Nvt.Core` at net8.0 with the base class library only, and `Nvt.Core.Avalonia`
  at net10.0 with Avalonia 12.0.5.
- Commander's rule, extending `docs/architecture/dependency-rules.md`. This ADR is the normative source for
  Core package references. That file points here.
  - Domain, Contracts, Profiles and the Application projects never reference a Core package. The Application
    projects include `VersionManagement.Application`. They may use the base class library, for example
    `TimeProvider`.
  - Platform, the Infrastructure projects and the non-Avalonia hosts, for example Launcher, may reference
    `Nvt.Core`.
  - Only the projects with Avalonia package references today may reference `Nvt.Core.Avalonia`:
    Presentation.Avalonia, Desktop and DistributionLauncher.
  - LauncherBootstrap references no Core package. An architecture test forbids any package reference there,
    and the installed Bootstrap stays unchanged (open point 5).
- NFC's Application ports stay. Their Infrastructure adapters delegate to Core. An adoption deletes NFC's
  duplicate implementation, not the port.
- Today the architecture tests check project references. Two tests also pin package references:
  DistributionLauncher has exactly four Avalonia packages, and LauncherBootstrap has none. A test that allows
  Core package references per project is a follow-up before the first adoption merges. An adoption that adds
  a Core package to DistributionLauncher also updates its pinned list.

### Package contract

- Core has its own semantic versions (decision 324).
- NFC commits each verified package under `vendor/nuget` with a `SOURCE.md` file (decision 324). Commander's
  rule: `SOURCE.md` names the Core source commit, the package version, the SHA-256 of each package and how it
  was built.
- NuGet source mapping restricts the Core packages to `vendor/nuget` (decision 324). Other packages keep
  their current sources.
- NFC pins each Core package to an exact version (`[x]`) and updates its lock files in the same pull request
  (decision 324). CI restores in locked mode (decision 323).
- Commander's rule: a rollback reverts the vendor commit together with its pins and lock files.
- Core keeps a proprietary license with an explicit grant for the named tools. NFC's
  `THIRD_PARTY_NOTICES.md` lists Core's terms and version (decision 324). That change is R3 with the release
  owner. This repository is public, so the Core license must also allow the package in this repository and
  in its source archives.

### Adoption rules

- Each module has two pull requests: the Core extraction and the NFC adoption (decision 322).
- The adoption pull request names its frozen NFC parent commit. It proves zero difference: identical UI
  snapshots, and identical behavior and outputs in the existing tests and any added comparison (decision 323).
  Nobody refreshes a baseline to make it pass.
- An adapter keeps NFC's types and messages where NFC callers depend on them.
- Adoption parts that touch firmware output paths, for example output names, are R3 with the firmware owner.
  They come in their own pull request (decision 326).
- The process runner adoption changes the rules of ADR 0081 and ADR 0006. It needs its own amendment of those
  ADRs first. This ADR does not change them.
- Commander's rule: before NFC releases with Core, the release proves that NFC still installs, runs and
  updates on its own.

## Rejected options

The commander's assessment, not stated in the decisions:

- Option 1: builds would need the Core checkout, and lock files would not pin the Core source.
- Option 2: two copies would drift, and review would cover both.
- Option 3: restore would depend on a public registry and on an account that publishes there.

## Open points

The owner answered points 1 to 4 on 2026-10-06 (see Dated updates). Point 5 is still open. The status ledger
lists them in section 4.

1. The authority floor and roles for `vendor/nuget`.
2. Whether strict manifest schema validation stays in NFC while Core receives validated facts.
3. Whether NFC keeps its message center template and wording while Core owns the mechanism.
4. Whether the shell move is limited to navigation history and the existing page host for now.
5. Whether a launcher adoption may replace an installed Bootstrap.

Answers are added below as dated updates. They do not rewrite the decisions above.

## Consequences

### Positive

- One implementation of each shared mechanism, reviewed once.
- Builds stay reproducible and work offline, because the packages live in the repository.
- NFC's users see no change, because every adoption proves zero difference.

### Negative

- Binary packages enlarge the repository history.
- Each module needs coordinated work in two repositories and two reviews.
- The license change needs a bridge release, because installed versions accept only MIT (decision 326). The
  owner chose the two-step bridge (decision 327).

### Follow-up

- An authority-policy entry for `vendor/nuget` before the first package commit.
- The package-reference allowlist test before the first adoption merges.
- The amendment of ADR 0081 and ADR 0006 before the process runner adoption.
- The first adoption pull request links this ADR.

## Dated updates

### 2026-10-06: open points 1 to 5 (decision 328)

The owner answered on 2026-10-06 09:0x, relayed by the Commander session. Each answer accepts the
recommendation.

1. `vendor/nuget` authority: the highest level. Each commit of Core package files and `SOURCE.md` is R3, and
   the owner approves it on GitHub. Changes to the authority policy and CODEOWNERS for this path are
   governance changes that the owner also approves.
2. Manifest schema: strict JSON schema validation stays in NFC. Core receives validated results and checks
   identity and limits again. Core has no path that accepts a manifest by default.
3. Message center: the display mechanism and its lifecycle move to Core. NFC keeps its XAML template and
   its wording.
4. Shell: this move covers only navigation history and the existing page host interface. Product pages,
   firmware guards and startup preload stay in NFC. A larger shell move needs its own brief.
5. Installed Bootstrap: not answered. Until the owner answers, the installed Bootstrap's bytes and protocol 1
   stay unchanged. No work that replaces or migrates it starts.
