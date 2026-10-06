# G0 owner checklist: agent GitHub identity and rulesets

R41 amendment, 2026-10-01: the new ruleset script/templates/README/tests below
have a separate candidate hash table in [C1a](#c1a-r41-approval-scope-trial-apply-and-rollback-2026-10-01).
It supersedes earlier hashes only for those changed files after review and
installation; earlier installed-version statements remain dated history.

Amendment, 2026-10-01 (status; the 1.1.13 text below is kept as recorded): the seventh version
(`4ac380da8`) is the installed version. The owner installed it before an acceptance was recorded; the
installed copy is byte-identical to `4ac380da8` (the commander's SHA-256 check of the five scripts and
the README, 2026-10-01; the reviewer could not verify the installed copy). A fresh independent security review on 2026-10-01 (Claude Opus 5.5) accepted it with
changes, no P0 or P1; the record is in [WS-GOV](WS-GOV.md#g0-seventh-version-acceptance-2026-10-01).
The **pending review** markers below therefore describe the history, not the current state. The
trunk carried this version from the G0 landing pull request; the eighth version
([A6b](#a6b-eighth-helper-and-wrapper-version-installed-2026-10-01), installed 2026-10-01) changes four of its
files, whose new hashes are listed there; every other file still matches the inventory table below.

**Pending independent review: seventh version, 2026-09-27.** This version
proposes a change to the token helper, the `gh` wrapper, their README and the
tests: a token carries `workflows: write` only on explicit request (A6a), and
the wrapper passes `gh` options such as `--repo` to `gh` unchanged (A6;
`BUG-20260927-g0-gh-wrapper-repo-option-conflict`). The first independent
review of the proposal (G0HR, `codex/gpt-6-astra`) returned
ACCEPT-WITH-CHANGES, and so did its re-reviews (G0HR2, G0HR3). G0HR3 closed
F-1 (launch identity); this revision addresses the rest of F-5 by limiting the
test runner to Pester 3.4.0, and still needs the review's acceptance. The inventory rows marked
**pending review** describe that proposal. Do not install it, and do not
change the App's permissions for it, until an independent review accepts it
and the commander records the acceptance in [WS-GOV](WS-GOV.md). Until then
the installed reviewed version stays in use; its hashes are listed under the
inventory.

Status of the reviewed baseline: sixth version, 2026-09-27, retaining the owner-run setup
scripts of decisions 80 and 82 after they passed their fourth independent
security review (G0SR4, ACCEPT): the App, its key, the rulesets and their
rollback are now done with the reviewed scripts in
[`g0-scripts/`](g0-scripts/README.md). The local Git compatibility correction
and its review are recorded below; MSIX operator and migration requirements
were added after live setup. This is a reusable checklist, not a completed
acceptance record; current execution evidence is in WS-GOV. It implements board decisions 49 and 56
(agents get their own GitHub identity, a GitHub App first and a machine
account as fallback, created by the owner), the G0 step of decision 50,
decision 65 (key custody, and agents obtaining installation tokens through the
helper), decision 66 (trunk catch-up and an owner-only force-push means),
decision 67 (O-3, for RS-1g), decision 77 (a standing owner-only bypass on
`main`, the trunk and release branches, never a review or release exemption),
decision 78 (pausing a ruleset when an admin bypass cannot be set), decision
80 (the owner runs reviewed scripts for the App, its key, the rulesets and the
token helper) and decision 82 (the key is stored twice, a DPAPI file for the
helper and a Bitwarden backup; only the owner runs the setup scripts, and the
installed helper and `gh` wrapper are the decision 65 interface through which
agents act as the App). Design: [governance ADR draft](ADR-DRAFT-governance-reset.md),
items 7 to 11; log: [WS-GOV](WS-GOV.md). Nothing here is done until its
operator does it.

`<owner>/<repo>` stands for this repository's full name, as listed in
[`agent-issue-tracker.md`](../../governance/agent-issue-tracker.md).

## Owner notes from the security review (G0SR4)

The fourth security review accepted the scripts for your use with these
notes. Read them before you start.

1. **Decision 65 is a rule, not a technical boundary.** The agents run under
   your Windows user, so nothing isolates the DPAPI key file, an unlocked
   Bitwarden vault, your browser session, your Git settings or the helper
   from an agent process. Agents act as the App only through the helper (which
   Git calls) and the `gh` wrapper that you installed. They never read the
   key, the DPAPI file, the vault, a credential store or a token, and never
   call the helper on their own (its token mode prints a token).
2. **Recording the scripts cannot detect.** The scripts stop, with no
   override, when PowerShell script-block, module or transcription logging is
   switched on by policy or configuration. They cannot detect a manual
   transcript (`Start-Transcript`), a screen recorder, other external
   recording or memory capture: before each run that handles a secret, you
   confirm that none is active. PowerShell cannot guarantee that strings are
   erased from memory; close the terminal when such a run ends.
3. **What only the GitHub UI shows you.** The scripts neither decide nor check
   whether this repository offers "Repository admin" as a bypass actor (you
   look, then pass `-AdminBypassAvailable Yes` or `No`, C1), that the App is
   installed on this repository only (A3), or the resulting rulesets, the
   effective protection of each branch and the rest of the evidence (part D).
   You confirm these yourself.
4. **Operating limits.** Windows with PowerShell 7.4 or later. The key is
   stored in both DPAPI and Bitwarden (decision 82), and your private backups
   ("Before you start") come first. A ruleset apply or restore is not atomic
   and never rolls back by itself, and nobody changes the repository's
   rulesets while one runs (C1). When a conversion, a key store or a remote
   write ends with an unknown outcome, check GitHub and your stores and clean
   up first; only then decide whether to run again.

## The reviewed scripts

[`g0-scripts/`](g0-scripts/README.md) preserves the reviewed files and the
independently reviewed Git compatibility correction below;
[`.gitattributes`](.gitattributes) turns off line-ending conversion for it, so
a checkout keeps these bytes. The current inventory below includes the
2026-09-27 local R1 correction to `NfcG0.Common.ps1` and its tests: legal
repeated Git advisory arrays no longer fail scalar duplicate validation.
Independent scoped review passed; the complete G0 suite passed 42/42. All
other script files retain their G0SR4 bytes. The README's custody sentence was
corrected in batch 2c (text only); this checklist adds the MSIX location requirements. Run only
your verified copy outside every repository and worktree. The original
inventory remains in source commit `c61f10e3f1bfc03b287b256dd0526588cf2f4dc9`.

The seventh version proposes new bytes for four files and one new file, marked
**pending review** in the table: `nfc-app-token-helper.ps1`
(`-IncludeWorkflowsWrite`; the fixed permission set now comes from one
function, and the reply check also requires `workflows: write` when it was
requested), `Invoke-NfcGh.ps1` (accepts only a `pwsh -NoProfile -File`
launch of itself by its fully qualified path, called directly by the
process; passes the `gh` arguments unchanged; refuses common options naming
another repository before requesting a token), `README.md`,
`tests/NfcG0.Tests.ps1` and the new `tests/Invoke-NfcG0Tests.ps1`, which runs
the suite in its own process without GitHub, Git credential or Bitwarden
variables (the suite stops if such a variable is present, and no test saves a
real value). The runner accepts only Pester 3.4.0, whose failure count
includes setup, cleanup and block failures, and exits with 0 only when it ran
at least one test and none failed (1: a test or block failed; 2: the run
itself failed, including any other requested Pester version).
`NfcG0.Common.ps1`, the setup scripts and the ruleset templates are
unchanged. Through that runner, the complete G0 suite passed 64/64 with fake
secrets on the proposal. The 42 earlier cases also pass against the installed
version, where the new cases fail; against the G0HR2-reviewed wrapper and
runner, the 4 cases added for G0HR2 fail (among them the same-name script that
moves the working directory, which that wrapper let through); and against the
G0HR3-reviewed runner, only the runner case fails, because that runner
accepted a fake Pester 5.7.1 result with a failed cleanup block and exited
with 0. The proposal is a local patch, not an installed or reviewed version.

| File | Used in | Operator → GitHub identity | SHA-256 |
| --- | --- | --- | --- |
| `New-NfcGitHubApp.ps1` | A1, A2: manifest, local callback, conversion, key saved to DPAPI and Bitwarden | owner → owner | `6d9cc3b509b59d6e71bea60bc22494a6e1b003a4c1302cdbdb9dd703ab22f0d2` |
| `Set-NfcRulesets.ps1` | C1 and rollback: backup, item-by-item approval, transaction record, restore | owner → owner | `cff4633a4cdc0f857d322723e464198683a87f991e35004b2e96a4a5e4aca815` |
| `rulesets/RS-1a-to-1k.json` | C1: the values proposed for `main` | (data) | `14fdd532b024dd55545c10c35c9a32a751f1c964775ac7e026f932770f4467ac` |
| `rulesets/RS-2.json` | C1: the new trunk ruleset | (data) | `360aff370f725ea5e913bfdd1a5453ffe5d3e2a5d8f128725a0f877a563f142e` |
| `rulesets/RS-3.json` | C1: the new release-branch ruleset | (data) | `59bca0cad48739cfdc8e635851611908ca20af780862f6b225d7ec01e47d4762` |
| `rulesets/RS-4.json` | C1: what the tag ruleset must already be | (data) | `2896cc49e313cc14a93305e6eccdce3fe527f4255a6f8616946f11cb90968f14` |
| `nfc-app-token-helper.ps1` | A6, A6a: an installation token for Git; `workflows: write` only with `-IncludeWorkflowsWrite` (**pending review**) | agent → App, after you install it | `6a4c3767bce7d1e1a05c2d38ab94552501398026acb69217ed22c7c5b3074944` |
| `Invoke-NfcGh.ps1` | A6, A6a: one `gh` call with an installation token; `gh` arguments passed unchanged (**pending review**) | agent → App, after you install it | `29c3ecb9d7fe8356a0af4ae0edfcb7e3a69522149b729dbbeb56855f2002b0d0` |
| `NfcG0.Common.ps1` | shared functions; independently reviewed Git advisory-array correction (2026-09-27) | (loaded by the others) | `d58661852446634454e8f09b1b6acba27af0325cf58892537618f73eed0e63a4` |
| `tests/NfcG0.Tests.ps1` | offline fake-secret tests; 64/64 on the proposal through the runner (**pending review**); the installed version passed 42/42; original G0SR4 run was 38/38 | none | `3a2fdad3f8591f73dd6fc35b9bd41d3d612e0b90a1f1e78498ff1a7ac7320014` |
| `tests/Invoke-NfcG0Tests.ps1` | runs the offline tests in an isolated process with Pester 3.4.0 only; fails unless it executed a test (**pending review**, new) | none | `5cd25e943a95cfec9a210f558a282a6a54d05d951ea3166bdf8f4720f219f02b` |
| `README.md` | the scripts' own instructions (**pending review**) | (text) | `8d1b2934c6bd770d1a0b13f395772522fa037fa6b748db7ac8e5a42ce27522dc` |

The installed reviewed version (sixth version) of the four changed files; it
has no `tests/Invoke-NfcG0Tests.ps1`. A copy for a new setup made before the
review accepts the proposal comes from source commit `9c7ad0caa` and is
compared with these hashes; the other files keep the hashes above.

| File | Installed reviewed SHA-256 |
| --- | --- |
| `nfc-app-token-helper.ps1` | `17fc16ba844bfcd25eba416dc742eecc2a175f1e286cff6d22912f5fc8660b82` |
| `Invoke-NfcGh.ps1` | `b26088a795b659f0d34f88e9bec09908a9e45f18c683cae306233dfa6b227afb` |
| `tests/NfcG0.Tests.ps1` | `030b2433480408d4930cc40a7b3f4a5e8404a810fc3baf0075b9adb6b7c03a83` |
| `README.md` | `72e19e83281b02cb081fce9fe682010c2f84ffba1f251a4ba65f490e05464e1d` |

## What G0 changes

After G0, agents push branches and open pull requests as the GitHub App, and
you review and approve them as an ordinary reviewer. The rulesets make an
approval, the required checks and resolved review threads mandatory on
`main`, the trunk and release branches, for everyone except you when you use
the bypass under the procedure of part C.

GitHub's ruleset settings dismiss an approval when new reviewable commits
arrive and require the most recent push to be approved; they do not promise
that every new commit SHA loses its approval (a new SHA whose changes are
identical may keep it). The exact rule "the approval was given on the current
head SHA" is enforced by the authority check of G1-A and by the release policy
in R-3, not by G0.

## Operators and identities

Every step starts with **operator → GitHub identity**: who does it (you, the
owner, or an agent) and which GitHub identity the step authenticates as: the
**App**, the **machine account** (fallback only), **owner** (your account,
in the browser, through `gh` signed in as you, or through the owner push path
of C2), or **none** (local work, or a public read that needs no login). The
setup scripts (`New-NfcGitHubApp.ps1` and `Set-NfcRulesets.ps1`) are run only
by you (decision 82), and every step that runs one is labeled **owner →
owner**, its offline preview included.

- **You operate** every step that creates an account or an app, runs a setup
  script or the manifest conversion, handles a key, a token, a PAT or any
  other secret (`BW_SESSION` included), changes a ruleset or another
  repository setting, installs or configures the helper and the wrapper,
  changes a local credential or identity setting, or uses the owner bypass or
  the C3 pause.
- **An agent may operate** the ordinary Git and pull-request steps that
  authenticate as the App through the helper and the wrapper you installed,
  because decisions 65 and 82 let agents obtain one-hour installation tokens
  that way: pushes, pull requests, merges and the cleanup of the
  verification. With the machine-account fallback the same steps
  authenticate as the machine account. An agent also prepares non-secret
  material, reads public or non-secret results, and writes the board and the
  log.

Agents never create accounts or apps; never run the setup scripts; never call
the helper directly, pass a token on a command line, or switch on tracing or
debug output of Git or `gh`; never read the private key, the DPAPI key file,
your Bitwarden vault or other password manager, credential stores, or
credential settings you have not confirmed as non-secret; never read or copy
any long-lived token or your credentials; never modify the helper, the
wrapper or their configuration; never change settings or rulesets; never
approve pull requests or environments; and never use a bypass.

**This is a rule, not a technical boundary (decision 65).** Agents run under
your Windows user. Everything that user can open is technically within reach
of an agent process: the App private key in its DPAPI file, an unlocked
Bitwarden vault or other password manager, Windows Credential Manager, Git
credential settings, the helper, the wrapper and their configuration, your
signed-in browser session, and any login of yours in `gh` or Git Credential
Manager. Decision 65 accepts a procedural constraint: agents do not read, copy
or use them, except through the helper's and the wrapper's token interface. A
technical boundary, such as a separate Windows account or service that holds
the key, was not adopted.

## Platform facts not verified locally

This checklist relies on GitHub and Windows behavior that was taken from
documentation and not verified on this repository or machine. G0 confirms
each one where the last column says before relying on it; if one does not
hold, stop and tell the commander.

| Fact | Used in | Confirmed by |
| --- | --- | --- |
| The manifest flow redirects to the manifest's `redirect_url` (here `http://127.0.0.1:<port>/callback`) with `code` and the `state` given in the form's address | A2 | the script accepting the callback |
| The manifest conversion `POST /app-manifests/{code}/conversions` needs no token, works once within one hour and returns `pem` with the other credentials | A2 | the script's summary after the conversion |
| An app name has at most 34 characters | A2 | the script (it refuses a longer name) and the app form |
| `bw create item` with an encoded secure-note item creates it and returns its ID | A2, A4 | the script's confirmation and your look at the vault (A4) |
| Installation tokens expire after one hour; `POST /app/installations/{id}/access_tokens` accepts `repositories` and `permissions` and returns a token limited to them; `/installation/repositories` accepts only installation tokens | A6, D3a | D3a (the helper also refuses a reply that is not limited that way) |
| `gh` uses `GH_TOKEN` from its environment before any stored login | A6, D3a | D3a |
| A permission added to an existing App reaches its installation only after the installation's owner accepts the request; until then GitHub refuses a token request that asks for it | A6a | A6a step 5 (the wrapper call with `-IncludeWorkflowsWrite` succeeds only after your acceptance in step 4) |
| An App token needs `workflows: write` to push, or to merge through the API, a change under `.github/workflows/`; without it GitHub refuses the write and names the `workflows` permission | A6a | the first authorized workflow write (A6a step 7) |
| A personal repository offers "Repository admin" in a ruleset bypass list; the API form is `RepositoryRole` with `actor_id` 5 and `bypass_mode` `always`, which also permits direct and force pushes | C1, C2 | your check in C1 step 1 and the saved ruleset JSON; D6 |
| `do_not_enforce_on_create` only exempts the required-status-checks rule when a branch is created; it skips no other rule, and the pull-request rule does not block creating a branch | RS-2, RS-3, D2 | the ruleset page's help text; D2 |
| The UI settings of C1 have the API names given there (`dismiss_stale_reviews_on_push` and the others) | C1 | the readbacks the script saves (`after-<id>.json`) |
| Rule insights list bypassed evaluations with actor and ref; the repository activity view lists pushes and force pushes with their actor | C2, C3, D5, D6 | D5 and D6 |
| On a personal repository a collaborator gets fixed access; the Collaborators page may offer no role selector | part B | that page |
| `gh pr merge --match-head-commit <sha>` refuses to merge when the head moved | after G0 | its first use |
| GitHub's test merge `refs/pull/<n>/merge` has the base and the head as parents | after G0 | step 3 of the catch-up checks the parents |
| An approval may survive a new SHA with identical changes | top of this page, D4 | D4 |

## Before you start

- [ ] **owner → none.** Run setup and installation in an unpackaged
      PowerShell process, for example a normal Windows Terminal session
      running `pwsh.exe`, not a shell launched by an agent app. An MSIX app
      such as Codex desktop and its child processes can virtualize writes
      under `%LOCALAPPDATA%` / `%APPDATA%` into
      `%LOCALAPPDATA%\Packages\<PackageFamily>\LocalCache\...`. Other tools
      may not see those files, and app removal/reset may delete them. Use a
      private root outside AppData, such as `%USERPROFILE%\.nfc\G0`, for
      scripts, DPAPI and rollback backups. A2, A6 and C1 use this root below;
      D0 verifies it from outside the package.
- [ ] **owner → none.** The current scripts do **not** implement a packaged
      process/AppData path guard. The preceding location check and D0 remain
      manual requirements; do not infer that script success proves a durable
      location. The bounded follow-up and its acceptance conditions are in
      [WS-GOV](WS-GOV.md#g0-local-execution-checkpoint--2026-09-27).
- [ ] **owner → none.** No release run is in progress and none is planned
      during G0.
- [ ] **owner → owner.** Open pull requests authored by your account are merged
      or closed (the commander lists them). After G0 you cannot approve a pull
      request your own account opened; agents reopen the unmerged ones as the
      app.
- [ ] **owner → owner.** Save the complete current `main` ruleset (22009240) so
      RS-1 can be restored: its JSON as an admin sees it (the ruleset page's
      export, or the API in your own session; the bypass list is visible only
      to an admin), and your screenshot of every section. `Set-NfcRulesets.ps1`
      also saves every ruleset in its backup directory (C1); this copy is
      yours, independent of the script. **agent → none:** an agent may save the
      public JSON read-only as a cross-check.
- [ ] **owner → none.** Credential settings: in your own terminal, not recorded
      or logged, run `git config --show-origin --get-regexp "^credential\."`
      inside this repository (it covers the system, global and repository
      scopes) and save the raw output where only you keep it. Check it for
      secrets, such as a password or a helper command that embeds a token.
      Then give the agent only what you confirm as non-secret, per scope and
      in order: whether `credential.helper`,
      `credential.https://github.com.helper` and any `useHttpPath` setting
      exist, their values, and any `credential.*.username`. Anything
      secret-bearing stays with you.
- [ ] **agent → none.** An agent saves the repository's `user.name` and
      `user.email` settings (`git config --local --get-regexp "^user\."`) and
      your confirmed summary to a local file outside every repository, for the
      rollback.
- [ ] **owner → none.** Tools, installed and checked by you: Windows with
      PowerShell 7.4 or later (`pwsh`), the GitHub CLI (`gh`) and the
      Bitwarden CLI (`bw`) with your vault.
- [ ] **owner → none.** Choose, outside every repository and worktree: a
      private folder outside AppData for your copy of the scripts (for example
      `%USERPROFILE%\.nfc\G0\scripts`; A6); an existing private directory
      such as `%USERPROFILE%\.nfc\G0\keys` for the DPAPI key file,
      with a file name that does not exist yet; and a place for the ruleset
      backup directories (every preview and every apply needs a new one).
- [ ] **owner → none.** Copy [`g0-scripts/`](g0-scripts/README.md) into that
      folder and compare every file with the table above
      (`Get-FileHash -Algorithm SHA256 <file>`; case does not matter). Stop on
      any difference.
- [ ] You have 30 to 60 minutes; GitHub settings pages open in a browser
      signed in as you, with two-factor authentication.

## Part A: GitHub App (primary, decision 56)

### A1. The manifest

`New-NfcGitHubApp.ps1` builds the manifest itself (`New-NfcManifest` in
`NfcG0.Common.ps1`). It asks for the smallest permission set G0 needs and
contains no secret and no `workflows` permission. `<port>` is the local port
the script picks when it runs:

```json
{
  "name": "nfc-agent-<owner>",
  "url": "https://github.com/<owner>/<repo>",
  "description": "Agent identity for <repo>: pushes feature branches and opens pull requests. No administration, secrets or environment access.",
  "public": false,
  "hook_attributes": {
    "url": "https://example.com/nfc-agent-webhook-unused",
    "active": false
  },
  "redirect_url": "http://127.0.0.1:<port>/callback",
  "request_oauth_on_install": false,
  "default_permissions": {
    "metadata": "read",
    "contents": "write",
    "pull_requests": "write",
    "checks": "read",
    "statuses": "read",
    "actions": "read"
  },
  "default_events": []
}
```

| Permission | Why | Can be dropped? |
| --- | --- | --- |
| `metadata: read` | Required for every app | No |
| `contents: write` | Push feature branches, read files, merge a pull request | No |
| `pull_requests: write` | Open, update and merge pull requests, post review records and replies | No |
| `checks: read`, `statuses: read`, `actions: read` | Read CI results, logs and artifacts | Only if agents never read CI |

Not requested: `workflows` (added by you in A6a, with your acceptance of the
new permission on the installation, just before the first authorized batch
whose pushes or merges change `.github/workflows/`: G1-A's workflow wiring,
G2 or release batch R-1, whichever comes first), administration, secrets,
variables, environments, deployments, issues (the bug ledger lives in the
repository, and GitHub issue writes need separate authorization), members,
pages and packages. The webhook is inactive and subscribes to no event. The
installed helper requests exactly this permission set for every token and
refuses a token with any other, so granting `workflows` on the installation is
not enough: the owner-reviewed helper change of A6a (pending review) must be
installed and verified first. With it, a token still carries this set alone
unless the helper is started with `-IncludeWorkflowsWrite`, which adds
`workflows: write` and nothing else.

Amendment, 2026-10-01 ([board decision 216](../1.2.x.md)): that change is the opt-in
`-IncludeWorkflowsWrite` switch of helper revision `4ac380da8`, which this trunk carries
since the G0 landing pull request. The owner enabled it on 2026-10-01 and keeps it in the Git
helper command until `1.2.1` is released, instead of adding it per batch as the scripts'
README describes; at that release the owner decides whether it stays. This note lives here
and on the board, not in the scripts' README, so that the README keeps the reviewed hash.
Risk, as restated after the independent review of 2026-10-01: while it is on, every Git token
issued for this repository can create or change `.github/workflows/` on any branch the rulesets
let it push. Such a
workflow runs on push, without a pull request or review, with `GITHUB_TOKEN` at the
repository's default workflow permissions and with every secret that no environment protection
rule guards. The `release` environment requires the owner's approval and protected branches;
the `firmware-parity` environment named in `release.yml` does not exist yet, so its secrets
`NFC_FIRMWARE_OWNER_*` would be unguarded if they were stored as repository secrets. The owner
chose the switch after a narrower explanation (pull-request CI only); the fuller risk was
reported to the owner on 2026-10-01 for re-confirmation, together with three settings the App
cannot read: read-only default workflow permissions, GitHub Actions not allowed to create or
approve pull requests, and which repository secrets exist.
Agents never run `git credential fill`, `git credential approve`, a credential manager
command, or `git -c credential.helper=store` (or any caching helper) for this repository: each
would expose or persist the one-hour token.

Amendment, 2026-10-01 ([board decision 221](../1.2.x.md)): decision 216 is withdrawn. After the
fuller risk above was reported, the owner removed `-IncludeWorkflowsWrite` from the Git helper
command; the per-batch rule of the scripts' README applies again.

Amendment, 2026-10-01 ([board decision 222](../1.2.x.md)): while the App installation holds
`workflows: write`, removing the switch is only a procedural limit, because an agent could pass
`-IncludeWorkflowsWrite` to the wrapper or run the helper itself. Between authorized batches the
App's **Workflows** permission is therefore **No access** (A6a step 8 is no longer optional), and
each workflow-writing batch starts with A6a steps 3 to 6. The owner set it to No access on
2026-10-01 (confirmed in chat).

- [ ] **owner → owner.** Optional preview, which contacts no GitHub: in your
      script folder run
      `pwsh -NoProfile -File .\New-NfcGitHubApp.ps1 -Owner <owner> -Repo <repo> -AppName nfc-agent-<owner> -DryRun`.
      It writes `nfc-manifest.dry-run.json` and `nfc-manifest.dry-run.html`
      to the current folder with a placeholder port; it opens no listener or
      browser, converts nothing and stores no key. Compare the manifest with
      the one above, then delete both files. The name has at most 34
      characters; choose another if GitHub reports it taken.

### A2. Create the app and store its key (decisions 80 and 82)

- [ ] **owner → none.** Prepare your own terminal: not an agent session, and
      with no transcript, screen recorder or other recording (G0SR4 note 2).
      Unlock Bitwarden yourself (`bw unlock`) and make the `BW_SESSION` it
      gives you available in this terminal only; `BW_SESSION` is a secret.
- [ ] **owner → owner.** From your script folder, run:

      ```powershell
      pwsh -NoProfile -File .\New-NfcGitHubApp.ps1 -Owner <owner> -Repo <repo> -AppName nfc-agent-<owner> -KeyStore Both -DpapiPath "$env:USERPROFILE/.nfc/G0/keys/app-key.dpapi"
      ```

      This existing private directory is outside AppData so the key does not
      become dependent on an MSIX package's virtualized storage.

      In order, the script:
      1. checks, before anything opens: Windows, PowerShell 7.4 or later and
         the .NET functions it needs; no detectable recording policy (one
         found stops it, with no override); `-KeyStore Both` (decision 82: a
         live run with `Dpapi` or `Bitwarden` alone stops); a DPAPI file that
         does not exist yet, in an existing directory; `bw` present and
         unlocked. It warns that manual transcripts and screen recording
         cannot be detected and asks `Continue? Type YES`: type `YES` only if
         nothing records this terminal or your screen;
      2. listens on `127.0.0.1` at a random port and opens a local page at a
         random path in your browser. Press **Create the agent GitHub App**.
         GitHub shows the app form, filled in: check the name, the
         permissions of A1 and that the webhook is inactive, then press
         **Create GitHub App**. The local page and the listener do not prove
         that it is you (any process of your Windows user could reach them),
         so do this at once;
      3. accepts GitHub's redirect to `http://127.0.0.1:<port>/callback` once,
         only with the expected random state, stops listening and completes
         the conversion. It waits for the callback at most `-TimeoutMinutes`
         (default 10);
      4. saves the key first to the DPAPI file (encrypted for your Windows
         user, with file access limited to your account) and then to
         Bitwarden, as a secure note named `NFC GitHub App <owner>/<repo>`;
         and prints only non-secret values: the App ID, Client ID, slug, App
         URL and installation URL. The key is never shown or logged; the
         client secret and the webhook secret are not kept (they can be
         regenerated in the app settings).
- [ ] **owner → none.** Note the App ID, Client ID and slug for A5. Lock
      Bitwarden (`bw lock`) and close the terminal (strings may remain in its
      memory).
- [ ] **owner → owner.** If the script stops, act on its message; never run it
      again blindly. A new run needs a DPAPI file name that does not exist.

      | Message | State | What you do |
      | --- | --- | --- |
      | "App setup did not complete" (shown for every stop before the conversion, including a missing requirement, an answer other than `YES` and a wait for the callback that ran out) | No conversion was sent; if you had pressed **Create GitHub App**, an app may exist without its key | Check the requirements listed in item 1 of the run above, and your answer. If you had pressed **Create GitHub App**, check your GitHub Apps (Settings, Developer settings) and delete such an app (**Advanced**, **Delete GitHub App**) before running again |
      | "App conversion outcome is unknown" | The conversion was sent; an app and a key may exist | Check GitHub for the app and its keys, revoke or delete any key and delete the app, then decide whether to create a new one. Never retry the conversion |
      | "App conversion succeeded, but no protected key copy was confirmed" | The app exists; no stored copy of the key is confirmed | Revoke or delete the app's key (or delete the app) before starting again |
      | "App setup stopped after DPAPI save" | The DPAPI file exists (its path is in the message); Bitwarden may or may not hold the note (a timeout does not prove that none was created) | Inspect the vault. If the note is there with the key, both copies exist: continue with A3. If not, copy the DPAPI key into Bitwarden with a separately reviewed local step, or remove the DPAPI file and revoke or delete the app's key before starting again |
      | Push works inside the agent app, but an ordinary terminal cannot find the helper | MSIX virtualization may have placed the files in package-local storage while Git names the logical AppData path | Resolve the physical location; relocate the local encrypted files and backups outside AppData under owner custody, update helper/key/wrapper paths, and complete D0. For a different computer, use A9's Bitwarden restore procedure instead of copying the virtualized folder |
- [ ] **owner → none.** The manifest never asks for `workflows`. When a
      workflow-writing batch needs it, you add it to the existing App and
      accept it on the installation in A6a; you do not create a new App or
      run this script again for it.

### A3. Install it on this repository only

- [ ] **owner → owner.** Open the installation URL the script printed (or the
      app page, **Install App**), choose your account, choose **Only select
      repositories**, select this repository only and install. Note the
      installation ID from the address bar (`.../settings/installations/<id>`)
      and check on that page that only this repository is listed (G0SR4
      note 3).

### A4. Key custody (decisions 65 and 82)

- [ ] **owner → none.** The key exists twice: the DPAPI file, which the helper
      reads for everyday use and which only your Windows user can decrypt,
      and the Bitwarden secure note, the backup. The helper never reads
      Bitwarden. Confirm yourself that both exist, and keep no other copy
      (clipboard, file or message).
- [ ] Agents must not read the key, the DPAPI file, your Bitwarden vault or
      other password manager, or credential stores. As stated above, this is
      a rule, not a technical boundary.
- [ ] **owner → owner**, then **owner → none.** If the key may have leaked:
      delete it in the app settings, and uninstall or suspend the app if
      needed (uninstalling cuts access at once); then remove the DPAPI file
      and the Bitwarden note. The setup script stores only the key of its own
      conversion: storing a key generated later in the app settings needs its
      own reviewed step.

### A5. Non-secret values

- [ ] **owner → none.** Keep these in your own local configuration, not in
      Git: the App ID, Client ID, slug, installation ID (A3), the DPAPI file's
      path, and the bot login `<app-slug>[bot]` with its user ID (**agent →
      none:** an agent looks the user ID up through the public API). The
      helper command (A6) needs the Client ID, the installation ID and the
      DPAPI path.

### A6. Token helper and `gh` wrapper (decisions 65 and 82)

- [ ] **owner → none.** Install: `nfc-app-token-helper.ps1`, `Invoke-NfcGh.ps1`
      and `NfcG0.Common.ps1` stay together in your script folder, outside
      every repository, worktree and AppData tree, for example
      `%USERPROFILE%\.nfc\G0\scripts`, with the hashes of the table. This
      keeps other tools' Git authentication independent of package storage.
      The wrapper
      starts the helper from its own folder, and both load
      `NfcG0.Common.ps1` from there. Agents never change these files or
      their configuration. What they do:
  - The helper, in Git mode, answers only `get`, and only for an HTTPS
    request to `github.com` whose path is exactly `<owner>/<repo>` or
    `<owner>/<repo>.git`; any other request gets no credential, and the key is
    not read. `store` and `erase` save nothing.
  - It reads the DPAPI copy into memory only, signs a JSON Web Token (RS256;
    issuer = Client ID; issued 60 seconds in the past; valid for 9 minutes),
    and requests an installation token limited to this repository and to the
    A1 permissions. It refuses a reply that names another or a second
    repository, that goes beyond the requested set or that carries a
    permission at a different level; a reply that omits a requested
    permission is accepted (A6a failure table). The pending-review version
    adds `workflows: write` to one token only when its command line carries
    `-IncludeWorkflowsWrite` (A6a), and then also refuses a reply without
    `workflows: write`. The token
    expires after one hour and is not cached on disk. Git receives
    `username=x-access-token` and the token through the credential-helper
    protocol. The helper stops when it detects a recording policy.
  - The wrapper runs the helper in token mode as a separate process, gives
    `GH_TOKEN` to one `gh` process only (not to its caller's environment), and
    replaces the exact token in `gh`'s output before passing the output on. A
    process that `gh` itself starts may inherit `GH_TOKEN`.
- [ ] **owner → none.** Wire Git to the helper at the repository scope (all
      worktrees share it; an agent prepares the exact lines for your shell
      from the A5 values):

      ```text
      git config --local --replace-all credential.https://github.com.helper ""
      git config --local --add credential.https://github.com.helper "!<helper command>"
      git config --local credential.https://github.com.useHttpPath true
      ```

      `<helper command>` is `pwsh -NoProfile -File "<script folder>/nfc-app-token-helper.ps1" -Mode git -Owner <owner> -Repo <repo> -ClientId <client-id> -InstallationId <installation-id> -DpapiPath "<DPAPI file>"`
      (the form of the scripts' README, section 3), with no token or key in
      it. Expand the script folder to an absolute path under
      `%USERPROFILE%\.nfc\G0\scripts` and the DPAPI path under
      `%USERPROFILE%\.nfc\G0\keys` before saving the helper command; do not
      leave a PowerShell environment expression in Git's shell command.
      Git runs it as a shell command and appends `get`, `store` or
      `erase`. The empty first entry stops Git from using the helpers of the
      system and global scopes, and so your stored credentials, for this
      repository; `useHttpPath` makes Git pass the repository path, without
      which the helper returns nothing. C2 explains how you push as yourself
      despite it.
- [ ] **owner → none.** Check the result:
      `git config --local --get-all credential.https://github.com.helper`
      shows the empty entry and then the helper command, and
      `git config --local --get credential.https://github.com.useHttpPath`
      shows `true`.
- [ ] **agent → App.** From here on, Git pushes and fetches call the helper by
      themselves; for `gh`, an agent runs the wrapper:

      ```text
      pwsh -NoProfile -File "$env:USERPROFILE/.nfc/G0/scripts/Invoke-NfcGh.ps1" -Owner <owner> -Repo <repo> -ClientId <client-id> -InstallationId <installation-id> -DpapiPath "$env:USERPROFILE/.nfc/G0/keys/app-key.dpapi" <gh arguments>
      ```

      "Through the wrapper" below means this form. The first use is D3a.

      **Installed reviewed (sixth) version.** PowerShell binds the wrapper's
      own parameters before the rest reaches `gh`, so a `gh` option that it
      reads as one of them or as a common parameter never reaches `gh`:
      `-R`/`--repo`, `--owner`, `-c`, `-d`, `-i`, `-o` and `-r` (in either
      case) are refused as duplicates, `-e`, `-p` and `-w` as ambiguous, and
      `-v`, `--verbose` and `--debug` are dropped silently. It also splits an
      argument that begins with `-` and contains `:` (for example
      `--head=<owner>:<branch>` reaches `gh` as two arguments), and it does
      not accept `--`. Use long options that match none of them, as this
      checklist does (`--json`, `--jq`, `--base`, `--head`, `--title`,
      `--body`, `--merge`, `--match-head-commit`), with their values as
      separate arguments; `gh` finds the repository from the working
      directory. (Observed locally with the wrapper's parameter block on
      PowerShell 7.6, without GitHub.)

      **Pending-review (seventh) version.** The wrapper has no PowerShell
      parameter block; it reads its own process command line. It requires
      `-NoProfile`, accepts only `-NonInteractive` and `-NoLogo` besides it,
      and the first execution mode must be `-File` with the fully qualified
      path of the wrapper itself (as in the form above, once your shell has
      expanded `$env:USERPROFILE`), so neither a profile nor the working
      directory takes part. It also requires that the process invoked the
      wrapper directly, with no other script, profile or command on its call
      stack. `-Command`, another script (including one with the same name that
      changes the working directory), a relative `-File` path, a missing
      `-NoProfile`, any other start-up option (for example `-ExecutionPolicy`),
      dot-sourcing or a call with `&` stops it with exit code 64 before the
      helper runs; it never searches later arguments for another `-File`. It
      then reads its own
      options up to `--`, or up to the first argument that does not begin
      with `-`, and passes every later argument to `gh` exactly as given. The
      form above therefore keeps working, and so do the calls already made
      through the wrapper (`pr create` without `--repo`, `api graphql -f ...`,
      `pr merge <n> --merge --match-head-commit <sha>`); the offline tests run
      each of them. Every `gh` option listed for the installed version, and
      arguments such as `--head=<owner>:<branch>`, now reach `gh` unchanged.
      Put `--` between the wrapper options and `<gh arguments>` when these
      begin with an option (since the eighth version, A6b, the `gh` arguments
      must begin with the `gh` command and `--` is optional). Wrapper options come first: an option placed
      after the `gh` arguments goes to `gh`. It also stops, with exit code 64,
      on a missing, repeated, unknown or malformed wrapper option.

      Before requesting a token, the wrapper refuses common repository
      options early: it stops, with exit code 64 and a message naming
      `<owner>/<repo>`, when `--repo`/`-R` or `GH_REPO` names anything other
      than exactly `<owner>/<repo>`, or when a group of short options
      contains `-R`. Omit `--repo` or pass exactly `--repo <owner>/<repo>`,
      and pass a value that begins with `-R` as `--option=<value>`. This
      early refusal is not a limit on what `gh` does. It does not inspect
      `gh api repos/<other>/...` paths, GraphQL queries, `gh repo` positional
      repository arguments or the repository `gh` derives from the working
      directory; `GH_HOST` or any other API host or URL selection; or `gh`
      aliases, extensions, child processes and whichever `pwsh` or `gh`
      executable `PATH` selects. The installation token authorizes this one
      repository only, but that does not prove that each request targets it,
      and a request to another host may use a different login: the wrapper
      does not technically guarantee the App identity or the destination
      host of a call. **owner → none:** keep the wrapper's operating
      conditions: a trusted `pwsh` and `gh` installation, GitHub.com only (no
      `GH_HOST` or enterprise host in the agents' environment), no untrusted
      `gh` alias or extension, and only commands the board authorizes.

### A6a. New helper and wrapper version, and the `workflows` permission (pending review)

This part uses the pending-review (seventh) version of the helper and the
wrapper. None of it starts before an independent review accepts that version
and the commander records the acceptance in WS-GOV. Steps 1 and 2 install and
verify the new version as soon as it is accepted; steps 3 to 8 follow only
just before the first authorized batch whose pushes or merges change
`.github/workflows/` (A1). The App permission is the outer limit and the
helper switch the inner one: a token carries `workflows: write` only when the
App has the permission, the installation has accepted it and the helper was
started with `-IncludeWorkflowsWrite`.

1. **owner → none.** In your own terminal, keep a private copy of the four
   installed files that change (helper, wrapper, README, tests) outside every
   repository and AppData tree, for example in
   `%USERPROFILE%\.nfc\G0\scripts-sixth`. Copy the four accepted changed
   files and the new `tests/Invoke-NfcG0Tests.ps1` from the reviewed source
   commit into `%USERPROFILE%\.nfc\G0\scripts` and compare every file in
   that folder with the inventory (`Get-FileHash -Algorithm SHA256 <file>`);
   stop on any difference. `NfcG0.Common.ps1` and the Git helper entry of A6
   stay as they are.
2. **agent → App**, checked by **owner → none.** Verify the new version,
   still without `workflows`:
   - through the wrapper, `api /installation/repositories --jq '.repositories[].full_name'`
     succeeds and lists only `<owner>/<repo>`;
   - through the wrapper, `pr list --repo <owner>/<repo> --limit 1` succeeds
     (the form the installed version refused), and
     `pr list --repo <owner>/<another-repo> --limit 1` stops with exit code 64
     and a message naming `<owner>/<repo>`, before any token is requested;
   - Git, from an ordinary terminal as in D0:
     `git push --dry-run origin HEAD:refs/heads/<disposable-ref>` authenticates
     through the helper and creates nothing.
3. **owner → owner.** Just before the first workflow-writing batch, add the
   permission to the App in the browser: Settings, Developer settings, GitHub
   Apps, `<app-slug>`, **Edit**; under **Permissions & events**, **Repository
   permissions**, set **Workflows** to **Read and write**, change nothing
   else, and save. Check that the other repository permissions still equal
   A1.
4. **owner → owner.** Accept the change on the installation: Settings,
   Applications, **Installed GitHub Apps**, `<app-slug>`, **Configure** (the
   installation page `.../settings/installations/<installation-id>`). GitHub
   shows that the app requests updated permissions: open the request, check
   that it adds only **Workflows** (read and write), and accept it. Check
   that **Only select repositories** still lists this repository alone. These
   labels come from GitHub's documentation; if the page differs, follow its
   wording, and stop if the request lists any other permission.
5. **agent → App.** Verify the switch: through the wrapper with
   `-IncludeWorkflowsWrite` among its options,
   `api /installation/repositories --jq '.repositories[].full_name'` succeeds
   and lists only `<owner>/<repo>`. The helper returns a token only when the
   reply stays within the requested set, carries no permission at a different
   level and includes `workflows: write`, so this success shows the
   `workflows` grant without showing the token. (A reply that omits another
   requested permission is accepted, as in the installed version; see the
   table below.) Without the switch the same call still succeeds with the
   ordinary set.
6. **owner → none.** Git: for the window of the authorized batch only, replace
   the App helper entry with one that carries the switch (the empty first
   entry stays; `<helper command>` is the A6 command):

   ```text
   git config --local --replace-all credential.https://github.com.helper ""
   git config --local --add credential.https://github.com.helper "!<helper command> -IncludeWorkflowsWrite"
   ```

   Check with `git config --local --get-all credential.https://github.com.helper`.
   The `--local` setting is shared by every worktree of this repository, so
   in this window every Git call from any worktree, not only the batch's
   pushes, receives a token with `workflows: write`; the commander keeps
   other Git writes out of the window or accepts that. When the batch's
   workflow pushes are finished, put the A6 entry back the same way without
   the switch and check again. Removing the switch affects only tokens issued
   afterwards: it revokes none already issued, and each keeps
   `workflows: write` until it expires (at most one hour). If a token must
   stop working before then, suspend the installation (A4). The commander
   records the times the switch was added and removed in WS-GOV. Agents
   never change this entry.
7. **agent → App.** Only in the authorized batch, a `gh` call that writes a
   change under `.github/workflows/`, such as merging a pull request that
   contains one, carries `-IncludeWorkflowsWrite` among the wrapper options;
   no other call does. The first such push and merge, with GitHub's result,
   are recorded in WS-GOV.
8. **owner → owner**, optional. After the batch you may set **Workflows** back
   to **No access** on the App and check on the installation page that it is
   gone; before the next workflow-writing batch, repeat steps 3 to 6.
   Since [board decision 222](../1.2.x.md) (2026-10-01) this step is required
   after every batch, not optional.

| Message or symptom | State | What you do |
| --- | --- | --- |
| A hash differs in step 1 | Wrong or changed copy; nothing was run | Stop. Copy the files again from the reviewed source commit |
| "NFC gh wrapper failed" or "NFC token helper failed" in step 2 | The new version or its configuration does not work; nothing was written | Put the private copy of step 1 back, compare it with the installed reviewed hashes, and tell the commander |
| "NFC gh wrapper usage error: ... limited to `<owner>/<repo>`" (exit code 64) | A `--repo`/`-R` value or `GH_REPO` named another repository; no token was requested | Omit `--repo` or pass exactly `--repo <owner>/<repo>`; unset `GH_REPO` or set it to exactly `<owner>/<repo>` |
| "NFC gh wrapper usage error: Start the wrapper as its own process" | The wrapper was dot-sourced, called with `&`, started through `-Command` or from another script, started without `-NoProfile` or with another PowerShell option before `-File` (only `-NonInteractive` and `-NoLogo` are allowed), started through a relative `-File` path, or started through a path it cannot match to itself (a link is not supported); no token was requested | Run it as `pwsh -NoProfile -File "<script folder>/Invoke-NfcGh.ps1" ...` with its real, fully qualified path |
| "NFC gh wrapper usage error: Unknown wrapper option ...", "... is required", "... needs a value" or "... more than once" | A wrapper option is missing, mistyped or repeated, or a `gh` option came before the `gh` arguments without `--` | Correct the wrapper options; put `--` before `gh` arguments that begin with an option (eighth version, A6b: start the `gh` arguments with the `gh` command instead) |
| `gh` reports an unknown flag `-IncludeWorkflowsWrite` | The installed wrapper is the old version, which passes the switch to `gh` | Complete step 1 first |
| Git asks for credentials or reports failed authentication after step 6 | The installed helper is the old version, which takes the switch as an unknown Git action and answers nothing | Put the A6 entry back without the switch, then complete step 1 |
| Step 5 fails with "NFC gh wrapper failed" | The installation has not accepted `workflows`, so GitHub refuses the token request; or the old helper is installed | Check steps 3 and 4 and the hashes; nothing was written |
| A push or merge is refused and GitHub's message names the `workflows` permission | The token did not carry `workflows: write`: the Git entry lacks the switch (step 6), the wrapper call lacks it (step 7), or the App or the installation lacks the permission | Inside the authorized batch, correct that step; outside one, the refusal is intended |
| A call that needs one of the other A1 permissions (for example reading CI results with `actions: read`) fails with a GitHub permission error, although the token was issued | The App or the installation lacks that permission. The helper accepts a reply that omits a requested permission, never one that adds a permission or changes its level (the installed version behaves the same) | Compare the App's repository permissions and the installation's accepted permissions with A1, restore the missing one yourself, and accept it on the installation |
| A `gh` call reaches another repository or host despite the early refusal (for example `gh api repos/<other>/...` or `GH_HOST` set) | The early refusal covers only `--repo`, `-R` and `GH_REPO`; the token itself authorizes only `<owner>/<repo>` | Stop the batch and tell the commander; keep the operating conditions of A6 (GitHub.com only, no untrusted alias or extension, authorized commands) |

Rollback: put the private copy of step 1 back, remove
`tests/Invoke-NfcG0Tests.ps1`, and compare the files with the installed
reviewed hashes; put the A6 Git entry back without the switch;
optionally set **Workflows** to **No access** on the App. The installed
sixth version then works as before, including its `gh` option limits (A6).
Since board decision 222 (2026-10-01), setting **Workflows** to **No access**
is required, not optional.

### A6b. Eighth helper and wrapper version (installed 2026-10-01)

Amendment, 2026-10-01: the eighth version fixes the findings of the seventh
version's review, tracked in `BUG-20261001-g0-gh-wrapper-console-codepage`. It
changes four files of the seventh version; `NfcG0.Common.ps1`,
`tests/Invoke-NfcG0Tests.ps1`, the setup scripts and the ruleset templates keep
the hashes of the inventory table above. The independent exact-head review of
#504 accepted it (head `36754c99f`, no P0 or P1), and it was installed on
2026-10-01 in a new folder beside the seventh version; steps 1-4 and their
results are recorded in
[WS-GOV](WS-GOV.md#g0-eighth-version-installation-2026-10-01). The seventh
version's folder stays for rollback.

- `Invoke-NfcGh.ps1`: copies `gh` stdout and stderr as raw bytes, replaces the
  exact token bytes and writes everything else unchanged, so Traditional Chinese
  text, a byte order mark, invalid UTF-8 or binary output pass through and none
  of them can hide the token from the redaction; refuses `gh` arguments that do
  not start with the `gh` command, and `gh alias` and `gh extension` (also
  `ext` and `extensions`) as that command, with exit code 64 before it requests
  a token; checks `-Owner` and `-Repo` case-sensitively.
- `nfc-app-token-helper.ps1`: checks `-Owner` and `-Repo` case-sensitively and
  to the end of the value before it reads the DPAPI file; accepts only a token
  of printable ASCII without spaces.
- `README.md`: states these behaviors, and that the redaction only catches a
  token printed verbatim (`--jq` reading `env.GH_TOKEN` or a browser setting can
  still print it transformed).
- `tests/NfcG0.Tests.ps1`: five new cases; through the runner the suite passed
  69/69 with fake secrets on source commit `8057d32d5` (2026-10-01).

| File | SHA-256 (eighth version, installed 2026-10-01) |
| --- | --- |
| `nfc-app-token-helper.ps1` | `73e9d63271889dfac8710e13cb7336dd1a3575117bcd0b7e8c8689054f5f32f0` |
| `Invoke-NfcGh.ps1` | `be793f98913792cbff4bb5fba39b5b91bf1a421d6a6a0e10a53693d8ab064a82` |
| `README.md` | `3be6415a66c41c7d6dfbb16a8292aad1d70fe08642cbe60e5598c82d4e240193` |
| `tests/NfcG0.Tests.ps1` | `297403ed071abf536fa4240202a7d509d9ac9652cad901fd34d237439d61112f` |

1. **owner → owner.** After the review is recorded, copy the complete script
   folder from the reviewed source commit into a new folder outside every
   repository, worktree and AppData tree; keep the seventh version's folder for
   rollback. Compare the four files with the hashes above and every other file
   with the inventory table above.
2. **owner → owner.** In the new folder run
   `pwsh -NoProfile -File tests/Invoke-NfcG0Tests.ps1`; it must exit with 0
   and report 69 passed.
3. **owner → owner.** Point the repository's Git helper entry at the new
   folder's helper with the same options and without `-IncludeWorkflowsWrite`
   (board decisions 221 and 222), and tell the commander the new wrapper path.
4. **agent → App.** The commander compares the installed copy with the hashes,
   runs one read-only wrapper call whose output contains Traditional Chinese,
   such as `gh api repos/<owner>/<repo>/contents/docs/handoff/1.1.14/1.2.x-allocation.md -H "Accept: application/vnd.github.raw"`,
   and confirms the text arrives intact, and confirms that `gh alias list`
   stops with exit code 64. The results are recorded in WS-GOV.

| Message or symptom | State | What you do |
| --- | --- | --- |
| "NFC gh wrapper usage error: gh alias is refused" (or `extension`, `ext`, `extensions`), exit code 64 | Intended: these commands would run child processes with the token; no token was requested | Nothing; an owner who needs an alias or extension uses their own `gh` login, not the wrapper |
| "NFC gh wrapper usage error: gh arguments must start with the gh command", exit code 64 | A global option such as `--help=false` or `--version` came before the `gh` command; no token was requested | Put the `gh` command first, options after it |
| Non-ASCII text from `gh` looks corrupted in the agent's terminal | The bytes are forwarded unchanged; the terminal decodes them with its own code page | Nothing in the wrapper; the agent reads the output as UTF-8 |

Rollback: point the Git helper entry back at the seventh version's folder and
tell the commander; nothing else changes.

### A6c. Optional `issues: write` switch (ninth version; decision 319)

The ninth version adds the optional switch `-IncludeIssuesWrite` to the token
helper and the `gh` wrapper (board decision 319). Without it a token requests
exactly the A1 set as before; with it the one token also requests
`issues: write`, and the helper refuses a reply that lacks it. Only the NFH
project passes the switch, and only after its App has Issues read and write;
NFC's own Git helper entry and wrapper calls do not change, and the manifest
for a new App (`NfcG0.Common.ps1`) keeps the A1 set without `issues`. It
changes four files of the eighth version; every other file keeps its hash.

| File | SHA-256 (ninth version, LF repository bytes) |
| --- | --- |
| `nfc-app-token-helper.ps1` | `1437ea92bd5018b0d2029aa321936ac77df916f33d7623c246e1c386405ce304` |
| `Invoke-NfcGh.ps1` | `ecf8ff29fdfd737cbf25e80a4d29b173f56577099492da4a0130b6723ba14adc` |
| `README.md` | `f9f58bc2d1942d31767ebe410410650a4b48e38495892d798e4376fde42d8b36` |
| `tests/NfcG0.Tests.ps1` | `4479d0dfbe509fec30bf5dba60cb5b23d5ff71122bd035acd6b5dc29fc8ef1e2` |

The installed folder is named after the reviewed head, as
`%USERPROFILE%\.nfc\G0\scripts-<first 12 characters of the head>` (the eighth
version is `scripts-36754c99fc81`). Use the head of the pull request that the
owner approved, not the merge commit; a different folder name is only a name,
and the hashes above decide whether the copy is right.

1. **owner → owner** (or the commander on the owner's instruction). After the
   pull request is merged, export the folder from the approved head with
   `git archive`, which writes the repository bytes; a copy from a worktree has
   CRLF line endings in the `.ps1` files and other hashes:

   ```powershell
   $head = '<approved head, 40 characters>'
   $dst = "$env:USERPROFILE\.nfc\G0\scripts-$($head.Substring(0, 12))"
   $tar = "$env:TEMP\g0-$($head.Substring(0, 12)).tar"
   git -C <repository> archive --format=tar -o $tar $head docs/handoff/1.1.13/g0-scripts
   New-Item -ItemType Directory -Path $dst | Out-Null
   tar -xf $tar -C $dst --strip-components=4
   ```

   Compare the four files with the hashes above (`Get-FileHash -Algorithm SHA256 <file>`).
   Compare `Set-NfcRulesets.ps1`, `rulesets/RS-1a-to-1k.json`, `rulesets/RS-2.json`
   and `rulesets/RS-3.json` with the R41 table of
   [C1a](#c1a-r41-approval-scope-trial-apply-and-rollback-2026-10-01) (R41 changed
   them after the eighth version was installed), and every other file with the
   inventory table above. Keep the eighth version's folder for rollback.
2. **owner → owner.** In the new folder run
   `pwsh -NoProfile -File tests/Invoke-NfcG0Tests.ps1`; it must exit with 0
   and report no failed test.
3. **owner → owner.** Point each repository's Git helper entry at the new
   folder's helper with the same options as before, and tell each project's
   commander the new wrapper path. For NFC the options stay exactly as they are
   (no `-IncludeWorkflowsWrite`, no `-IncludeIssuesWrite`). For NFH the Git
   entry needs no new switch either: Git pushes need no `issues` permission;
   NFH passes `-IncludeIssuesWrite` only among the wrapper options of the `gh`
   calls that label, comment on or close an issue.
4. **agent → App.** Each commander compares the installed copy with the
   hashes and runs one read-only wrapper call without the switch. The NFH
   commander then labels or comments on a test issue with the switch; a 403
   there means the NFH App or its installation has not granted Issues write.

| Message or symptom | State | What you do |
| --- | --- | --- |
| "NFC gh wrapper failed" with `-IncludeIssuesWrite` only | Usually GitHub refused `issues` because the App or its installation lacks Issues write, or the helper refused a reply without `issues: write`; then no token was issued. The same message also appears when `gh` failed after it started, so whether the issue changed is unknown | Look at the issue on GitHub before retrying. If it did not change, grant Issues read and write in that App's settings and accept it on the installation, or stop using the switch |
| `gh` reports an unknown flag `-IncludeIssuesWrite` | The switch came after the `gh` command, or the old wrapper is installed | Put the switch among the wrapper options before the `gh` command; check the installed hashes |

Rollback: point the Git helper entry and the wrapper path back at the eighth
version's folder and tell the commander; nothing else changes.

### A6d. `open-pr.ps1` (tenth version; owner proposal 2026-10-06)

The tenth version adds `open-pr.ps1`. It opens a pull request through the
installed `Invoke-NfcGh.ps1` and closes it unless its author is exactly
`app/nfc-agent-dennis40816`, the login that `gh pr view --json author` reports
for the App. The owner asked for it on 2026-10-06: 「或許開 PR 要有標準
script? 確保不用錯帳號」. The helper, the wrapper and every other file keep the
ninth version's hashes. It changes or adds three files:

| File | SHA-256 (tenth version, LF repository bytes) |
| --- | --- |
| `open-pr.ps1` | `2a0c2f3f4ebeb07af4c6d0b7fec72cf110e4541bf1364a27dabf06b977fd15e0` |
| `README.md` | `d9b64d69413888fa287dbd029c568337234a3505d8ad18a4dd57d475ce77e23f` |
| `tests/NfcG0.Tests.ps1` | `6cb8128fbb9dde6e8ac3b144763b54917e458b938ec8e151e8b2c801526ccc7f` |

1. **owner → owner** (or the commander on the owner's instruction). After the
   pull request is merged, export the folder from the approved head exactly as
   in [A6c](#a6c-optional-issues-write-switch-ninth-version-decision-319)
   step 1. Compare the three files with the hashes above, and every other file
   with the ninth version's hashes and the inventory tables above. Keep the
   ninth version's folder for rollback.
2. **owner → owner.** In the new folder run
   `pwsh -NoProfile -File tests/Invoke-NfcG0Tests.ps1`; it must exit with 0
   and report no failed test.
3. **owner → owner.** Point each repository's Git helper entry at the new
   folder's helper with the same options as before, and tell each project's
   commander the new folder.
4. **agent → App.** The next pull request of each commander is opened with
   the installed `open-pr.ps1`. It must print the pull request URL as its last
   line and exit with 0.

Rollback: point the Git helper entry and the wrapper path back at the ninth
version's folder and tell the commander; nothing else changes.

### A7. Agent commit identity

- [ ] **owner → none.** Set, at the repository scope, `user.name` =
      `<app-slug>[bot]` and `user.email` =
      `<bot-user-id>+<app-slug>[bot]@users.noreply.github.com` (an agent
      prepares the values). Agent commits keep their co-author lines.

### A8. Your own sessions

- [ ] **owner → owner.** Sign your account out of `gh` on this machine
      (`gh auth logout --hostname github.com --user <your-login>`) and remove
      your GitHub entries from Git Credential Manager (in Windows Credential
      Manager, those whose name starts with `git:https://github.com`). This
      removes the easiest path to your credentials; it creates no boundary.
- [ ] **owner → owner.** Do owner actions in the browser: approvals, merges
      you make yourself, release dispatch and approval, settings. A signed-in
      browser session is also reachable by processes of your Windows user; it
      too is covered by the rule only.
- [ ] **owner → owner.** When you need your own command-line access (the
      ruleset script of C1 and its restore, C2 and C3), sign in for that task
      only, in your own terminal: `gh auth login` for `gh` (answer no if it
      offers to set up Git with your credentials), the owner push path of C2
      for Git. Sign out afterwards.

### A9. Changing computers: operational reference (2026-09-27)

Owner request: retain this procedure for a future Windows replacement. This
note maps that scenario to the existing steps; it does not change operators,
permissions, approvals or the reviewed script inventory.

**MSIX location note (owner follow-up, 2026-09-27).** A packaged host such as
Codex can virtualize writes under `%LOCALAPPDATA%` into its package's
`LocalCache/Local` directory. Its child PowerShell can see the logical path
while an ordinary terminal, another coding tool or an IDE cannot. Removing
or resetting that package may also remove the virtualized files. Therefore
the owner selected `%USERPROFILE%\.nfc\G0` as the durable local root for this
setup, outside AppData, the app package, repositories and worktrees.

For an existing virtualized setup, preserve the actual physical source and
private backups, move the encrypted file opaquely without printing or
decrypting it, retain its restricted ACL, and update every installed helper,
wrapper invocation and local non-secret record to the durable root. Verify
the scripts' approved hashes, key-file existence and access restrictions,
then test from an ordinary process outside the package as well as inside it.
Do not regard a second packaged child process as the outside-process check.
An encrypted-file move under the same Windows user does not constitute a
new-machine DPAPI restore; the restoration gap below still applies.
Keep the old virtualized copy until the owner decides its disposition after
confirming the durable location works and the Bitwarden backup is valid.
It contains a private-key file; an agent must not delete it on its own.

Before changing computers, confirm the actual files already live outside
`Packages\...\LocalCache`, not merely at an AppData path visible to a
packaged process. Restore the private key from the owner's Bitwarden backup
through the separately reviewed local restoration step; do not migrate by
copying the virtualized folder. Ordinary CLI login/session caches are not
part of the G0 file migration.

| State | Location | On a replacement computer |
| --- | --- | --- |
| GitHub App, repository installation and rulesets | GitHub | Inspect and reuse the existing resources if still valid; changing computers alone does not require creating them again or reapplying C1. |
| App key backup | Owner's Bitwarden vault (A4) | Owner confirms the existing secure note and key are available; agents do not inspect them. |
| DPAPI key file | Original Windows user's protection context | Copying this file alone is not a restore procedure. The scripts use `DataProtectionScope.CurrentUser`; a new Windows installation needs a key copy protected for its own user. |
| PowerShell, Git, `gh`, `bw`, reviewed helper and wrapper | Local machine | Install the required tools, obtain the approved script version, verify its inventory, and keep the three daily-use scripts together outside repositories. |
| Git credential wiring and bot commit identity | Local repository configuration, shared by its worktrees | Repeat A5–A7 with the new machine's paths and existing App/installation identifiers, after the owner backs up the new checkout's existing settings. |
| Bitwarden unlock session and App installation tokens | Short-lived process state | Obtain fresh sessions as needed; do not migrate or record their values. |

There is **no reviewed existing-key restore command in this script set**.
`New-NfcGitHubApp.ps1` creates an App and stores the key returned by that
conversion; it is not a Bitwarden-to-DPAPI restore utility. A2 already requires
a separately reviewed local step for copying an existing DPAPI key into
Bitwarden, and its equivalent for restoring the backup to a new user's DPAPI
store remains to be prepared and reviewed before migration. This is a future
restoration gap, not evidence that restoration has been tested.

After owner installation/configuration on the new computer, verify repo-only
access through the wrapper as in D3a and verify the bot identity. Inspect D1's
effective rules; use D2–D7's disposable verification scope for any repeated
write tests. Normal branches are not a migration test surface. Apply A8's
owner-session cleanup and A4's key-custody rules on each machine. Retiring an
old machine or rotating a shared App key is a separate owner action; key
revocation also affects other machines using that key.

Keep machine-specific paths, App identifiers and non-secret execution status
in the owner's local A5 record, outside Git. Never place passwords, private
keys, `BW_SESSION`, callback codes or installation tokens in this checklist,
shell history, screenshots or a handoff. Preserve the existing rollback
evidence until the owner has verified the replacement setup.

## Part B: machine account (fallback, decision 56)

Use this only if the app does not work for you. GitHub's terms allow a
machine account that you control and use only for automation. The App
scripts (`New-NfcGitHubApp.ps1`, the helper and the wrapper) are not used on
this path; C1 still uses `Set-NfcRulesets.ps1`.

In this path agents work with a **long-lived token**: Git Credential Manager
and `gh` hold it, and every agent Git or `gh` call uses it. That contradicts
the "never read or copy any long-lived token" rule in practice, so this path
rests on the rule alone. Compared with the app it adds these risks:

1. The token is valid until it expires or is revoked (for example 90 days),
   not for one hour.
2. A classic token's `repo` scope reaches every repository the account can
   access; the account must stay limited to this repository.
3. Requests cannot be narrowed to one repository or one permission set.
4. The token can be copied and used outside the helper path, and GitHub sees
   no difference.
5. Rotation is manual, and a second account with its own email and
   two-factor device must be kept.

Steps:

- [ ] **owner → machine account.** Create the account in a private browser
      window, with its own email address and two-factor authentication.
- [ ] **owner → owner, then owner → machine account.** Repository settings,
      **Collaborators**: add the machine account. A personal repository gives
      a collaborator the fixed collaborator access the page describes (reading
      and pushing, no administration) and may show no role selector; check on
      the page what it grants. Accept the invitation as that account.
- [ ] **owner → machine account.** Token: prefer a fine-grained token limited
      to this repository if GitHub offers the repository; otherwise a classic
      token with only the `repo` scope and an expiry. Add the `workflow` scope
      only just before the first authorized batch that edits workflows
      (release batch R-1). You type the token yourself, in your own terminal,
      into the Git Credential Manager prompt or `gh auth login --with-token`;
      never into an agent session.
- [ ] **owner → none.** For this repository set
      `credential.https://github.com.username` to the machine account's login,
      so Git Credential Manager picks that account, and set the commit
      identity to its noreply address. Do A8 as well.

## Part C: rulesets, bypass and pause

### C1. Ruleset changes with `Set-NfcRulesets.ps1`, each for your approval

Every step is **owner → owner**: only you run the script (decision 82), and it
works through `gh` signed in as you. The table lists every change. The
script's approval prompts carry its IDs (for example `RS-1c approvals` or
`RS-2 do_not_enforce_on_create`), and its proposed values come from the
templates in [`g0-scripts/rulesets/`](g0-scripts/rulesets/); the UI column is
for comparing on the settings page. The API values are from the
documentation (see the list of facts not verified locally); the JSON GitHub
returns, which the script saves, is the record.

1. **owner → owner.** Bypass availability: under Settings, **Rules**,
   **Rulesets**, open the `main` ruleset and check whether its bypass list
   offers "Repository admin"; change nothing there. If it is offered, use
   `-AdminBypassAvailable Yes`, and C2 is your bypass procedure. If not, use
   `No`: every branch bypass list ends up empty (the current list of `main`
   included) and the pause procedure C3 replaces the bypass (decision 78).
2. **owner → owner.** In your own unrecorded terminal, with `GH_TOKEN` and
   `GITHUB_TOKEN` unset, sign `gh` in as yourself (A8). The script stops
   unless `gh api user` returns `<owner>`.
3. **owner → owner.** Preview, from your script folder, with a new backup
   directory:

   ```powershell
   pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Owner <owner> -Repo <repo> -BackupDirectory "$env:USERPROFILE/.nfc/G0/backups/g0-preview-$(Get-Date -Format yyyyMMdd-HHmmss)" -AdminBypassAvailable Yes -WhatIf
   ```

   It reads GitHub and fills the new directory (`repository.json`,
   `rulesets-list.json`, `ruleset-<id>.json` for every ruleset, and
   `index.json`); checks that the ruleset list is complete and did not change
   while it read, that `main` (22009240) targets `main` or the default branch
   and has exactly one required-checks rule with three contexts, each with its
   source, that RS-4 matches exactly, and that no ruleset named "trunk" or
   "release branches" exists; and prints every item of the table with its
   current and proposed value. It writes nothing to GitHub and asks for no
   approval. Compare every item with the table, and stop on any difference.
4. **owner → owner.** Apply, with another new backup directory:

   ```powershell
   pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Owner <owner> -Repo <repo> -BackupDirectory "$env:USERPROFILE/.nfc/G0/backups/g0-apply-$(Get-Date -Format yyyyMMdd-HHmmss)" -AdminBypassAvailable Yes
   ```

   It repeats the backup and the checks, then asks for each item in turn
   (`Type YES`), starting with the confirmation of the unchanged RS-4. Every
   approval comes before the first write; any other answer stops it with
   nothing written. It then checks that no ruleset changed since the backup,
   updates `main` (22009240) and creates RS-2 and RS-3, checking the live
   rulesets again before each write and comparing each readback with the
   requested body. `index.json` records every write: `pending` before it is
   sent, then `applied` with the ruleset ID and its saved readback
   (`after-<id>.json`).
5. **owner → owner.** If the script stops before its first write, nothing
   changed on GitHub: fix the cause and run again with a new backup
   directory. If it stops after a write started, it does not roll back: an
   entry still `pending` has an unknown outcome, and a failed request does not
   prove that GitHub changed nothing. Compare the live rulesets with
   `index.json` and the saved files, tell the commander the state (these files
   hold no secret), and then restore what was applied (Rollback) or decide
   with the commander how to complete it. Do not re-run the apply as it is:
   it stops when RS-2 or RS-3 already exists.
6. **owner → none.** Keep every backup directory private, outside every
   repository and outside AppData; the rollback needs the apply directory's
   `index.json` even if the agent app is removed or reset.

From the preview until the apply has ended, and during a restore, nobody
changes this repository's rulesets: you edit none in the browser, and the
commander runs no other ruleset work (agents cannot change rulesets anyway).
The script stops on a change it sees, but its read, check and write sequence
is not atomic (G0SR4 note 4). D1 then verifies the effective rules.

| ID | Ruleset | Setting | UI input | API value |
| --- | --- | --- | --- | --- |
| RS-0 | RS-1, RS-2, RS-3 | Enforcement status; target | **Active**; branch ruleset | `"enforcement": "active"`, `"target": "branch"` |
| RS-1a | `main` (existing 22009240) | Target branches | keep: the default branch or `main` | keep: `"include": ["refs/heads/main"]` (or `~DEFAULT_BRANCH`) as saved |
| RS-1b | `main` | Require a pull request before merging | on | rule `pull_request` |
| RS-1c | `main` | Required approvals | 1 | `required_approving_review_count: 1` |
| RS-1d | `main` | Dismiss stale pull request approvals when new commits are pushed | on | `dismiss_stale_reviews_on_push: true` |
| RS-1e | `main` | Require review from Code Owners | on | `require_code_owner_review: true` |
| RS-1f | `main` | Require approval of the most recent reviewable push | on | `require_last_push_approval: true` |
| RS-1g | `main` | Require conversation resolution before merging (O-3, decision 67) | on | `required_review_thread_resolution: true` |
| RS-1h | `main` | Allowed merge methods | Merge only | `allowed_merge_methods: ["merge"]` |
| RS-1i | `main` | Block force pushes; Restrict deletions | on; on | rules `non_fast_forward`, `deletion` (added only if missing) |
| RS-1j | `main` | Require status checks to pass | unchanged: the saved checks with their sources, "Require branches to be up to date" and "Do not require status checks on creation" exactly as saved | unchanged: `required_status_checks` (each `context` with its `integration_id`), `strict_required_status_checks_policy`, `do_not_enforce_on_create` as saved |
| RS-1k | `main` | Bypass list (decision 77) | Repository admin, Always allow (`-AdminBypassAvailable Yes`); empty with `No` | `[{"actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always"}]`, or `[]` with `No` |
| RS-2 | new "trunk" | Target branches | Include by pattern: `*.*.x` | `"include": ["refs/heads/*.*.x"], "exclude": []` |
| RS-2 | "trunk" | Rules | RS-1b to RS-1i; required checks: the same three contexts with the same sources as RS-1j, "up to date" off, "Do not require status checks on creation" on | as RS-1b to RS-1i; `required_status_checks` copied from RS-1j, `strict_required_status_checks_policy: false`, `do_not_enforce_on_create: true` |
| RS-2 | "trunk" | Bypass list (decisions 66 and 77) | as RS-1k | as RS-1k |
| RS-3 | new "release branches" | Target branches | Include by pattern: `*.*.*`; Exclude by pattern: `*.*.x` | `"include": ["refs/heads/*.*.*"], "exclude": ["refs/heads/*.*.x"]` |
| RS-3 | "release branches" | Rules | RS-1b to RS-1h; Block force pushes on; **deletion allowed** (release closure deletes the branch after its tag); required checks as RS-2 | as RS-2 without `deletion` |
| RS-3 | "release branches" | Bypass list (decisions 66 and 77) | as RS-1k | as RS-1k |
| RS-4 | tag ruleset `refs/tags/v*` (existing) | Everything | confirm only: Active, the pattern `refs/tags/v*` with no exclusion, update and deletion restricted and no other rule, bypass list **empty** | unchanged; the release policy requires an empty bypass list. The script stops if RS-4 differs in any of these and never repairs it |

Notes:

- The script keeps every other rule `main` already has and its target; it
  replaces only the pull-request rule and the bypass list, and adds
  `non_fast_forward` and `deletion` if missing.
- "Do not require status checks on creation" (`do_not_enforce_on_create`) only
  lets a new branch be created although its commit lacks the required checks.
  It skips no other rule and does not promise that any branch can be
  created. In RS-2 it lets the trunk itself be created, for example `1.2.x`
  from `main`; in RS-3 it lets a release branch be cut from the trunk.
- Confirm on each protected branch's effective rules (D1) that there is no
  "Require linear history" rule (merge commits are required), no
  signed-commit or deployment rule, and no other ruleset adding rules.
- The bypass actor is the Repository admin role, which on this personal
  repository is only you; the app and the machine account never are. Step 1
  checks whether the page offers that role before the script runs; if it does
  not, every bypass list stays empty and the pause procedure (C3, decision 78)
  replaces the bypass.
- Not part of G0: the branch-name allowlist and automatic branch deletion
  (checklist A-5, approved item by item under decision 24); if you approve the
  allowlist later, it must let the agent identity create `feature/*.*.*/**`
  and you create `recovery/**`.
- Unchanged: the protected `release` environment with you as the required
  reviewer, the Codex review app, and the read-only default workflow token.

### C1a. R41 approval scope trial, apply and rollback (2026-10-01)

Current status (2026-10-01, after the cutover): reviewed and merged into
`1.2.x` (#509, merge `b83328a64`); W1 is live.
Cutover amendment, 2026-10-01 (board decision 246): the owner changed only the
`trunk` ruleset (ID 24060410, `refs/heads/*.*.x`, including the retired
`1.1.x`) in the GitHub settings page, Required approvals 1 -> 0; the commander
backed up all four live ruleset bodies first and read back only that field
changed among the rule fields (the `updated_at` timestamp also changed); the
other three rulesets read back identical to their backups (test area
`evidence/1.2.1/R41-ruleset/`). No isolated trial ruleset was used: the trial
runs on `1.2.x` with one R0 and one R3 pull request. `main` and release-branch
rulesets keep one general approval; the tag ruleset (update and deletion
protection, no approvals) is unchanged. Rollback: set Required approvals back
to 1. Step 2's script mode stays available for future updates.
Step 5 applied (board decision 247): with zero approvals, last-push approval
true blocked the R0 trial pull request, so the owner unchecked it on `trunk`
only; the R0 (#510) and R3 (#511) trials then passed. W1's remaining
limitation (a failing pull-request-event run from before the record still
counts) is handled by a description edit until `1.2.11` (decision 248).

The status recorded before the cutover, kept as written:

Status: local implementation of decisions 225 W1 and 233; pending independent
exact-head review, required CI, owner approval and live trial/cutover. This
amendment supersedes C1's one-general-approval values only on targets whose
R41 cutover is complete. Earlier inventory tables and G0 acceptance remain
historical; neither certifies R41. The new G0 hashes below identify this local
candidate; commander binds them to the reviewed commit before installation.

1. **commander / owner:** review the R41 candidate and base-checker self-change
   evidence under the existing rules. The owner's last-push approval names
   `governance-owner` and `release-owner` separately. Open one Workflows batch
   under A6a and decision 222; after the final workflow push remove the helper
   switch and return the App permission to No access, without waiting for merge.
2. **owner:** inspect all effective rulesets and confirm the existing IDs to
   update. Use an isolated trial target whose base contains candidate policy,
   CODEOWNERS, checker and W1 workflow. Preserve live repository/ruleset backups;
   no concurrent ruleset changes or bypass during this maintenance window.
   Use the new `-UpdateApprovals -RulesetIds <confirmed IDs>` mode in the
   [README](g0-scripts/README.md#r41-update-existing-approval-fields-2026-10-01-pending-owner-trial),
   with separate new preview/apply backup directories. Do not rerun initial
   G0 creation or pass `-AdminBypassAvailable` to an update.
3. **owner:** confirm the complete ID/body/scope and all four approval fields
   before any write. Only `required_approving_review_count` becomes 0;
   `require_code_owner_review`, `dismiss_stale_reviews_on_push` and
   `require_last_push_approval` stay true. Live check names/integration IDs,
   check options, include/exclude scope, bypass and other rules stay identical.
   Read back each body and effective target rules; RS-4 and `release`
   environment approval remain protected. Do not add a required-check context.
4. **commander / owner:** retain candidate SHA, confirmed trial ruleset IDs,
   before/after JSON, review IDs, run IDs, check SHA/source, merge state and
   merge commit, and proof no bypass was used. Trial acceptance requires:
   - R1 and R2 independently COMMENT-reviewed exact heads with green required
     CI auto-merge without any owner APPROVED review. Missing, reject,
     incomplete and old-head records block.
   - R3 with identical record/CI blocks without code-owner approval; owner
     exact-head last-push approval naming every role permits merge. A
     diff-changing push dismisses approval; a new SHA needs a new record.
   - W1 COMMENT submission starts a run without a PR-body edit; editing to
     reject/incomplete or dismissing the record reruns and fails. Owner
     APPROVED review alone cannot replace the independent COMMENT record.
   - Both events' results satisfy the same `governance / authority` required
     context. Missing/failed/cancelled runs block. Read-only permissions,
     no secrets, head checkout, `persist-credentials: false`, no conditional
     skip, no path filter and no `pull_request_target` remain.
5. **stop condition:** zero general approvals plus last-push true may still
   require an approval for non-R3 on GitHub. If the trial shows that, do not
   relax formal rules or silently set last-push false; obtain the owner's
   explicit decision accepting the changed M2 guarantee, then review and retrial.
6. **owner:** after trial passes, patch only confirmed formal IDs whose target
   bases already contain the amended authority files. Record a cutover and
   effective-rules readback per target. Unswitched `main`, release or older
   trunk targets keep their live protections. Templates alone prove no cutover.
7. **commander:** verify live records/checks and base authority; R3 additionally
   needs role evidence and an approval snapshot. Enable auto-merge with
   `--auto --merge --match-head-commit <head>`; it checks the head at the call,
   not future queued heads. Before a push or authority/record/evidence edit,
   cancel with `gh pr merge <n> --disable-auto`, repeat verification and renew
   required review/approval/snapshot before re-enabling.
8. **rollback:** trial failure leaves formal rules unchanged. After a cutover
   failure cancel auto-merge; owner runs `-Restore` from that update's backup,
   previews/approves the exact before bodies and verifies readback/effective
   rules. Pending/unknown outcomes or later live changes require reconciliation
   first. Restore never disables an existing updated ruleset. Follow with an
   R3 repository revert PR when needed; retain required CI, tags and release
   environment protection.

R41 changed G0 files, SHA-256 of the candidate bytes (new table; pending review):

| Changed G0 file | SHA-256 (LF repository source) |
| --- | --- |
| `Set-NfcRulesets.ps1` | `fc5062a6c41a25188184a4d61c4f2535b74f5efea657b56b4a86950ae46026e4` |
| `rulesets/RS-1a-to-1k.json` | `2c7afbe6b73be54154e618c0f8c950e60c47dcaf000a86a249215b2ed77b9dae` |
| `rulesets/RS-2.json` | `dfde6fc9eb8ac9f012d6e2727a0a35bfcf9d5f19ef124bd6e0c37a3db6ac705d` |
| `rulesets/RS-3.json` | `347977f5b34852dfe8337280c652cd8f82a9a4bb84be821af61c817ae99af4ac` |
| `README.md` | `60ef1440484295c61321bc756f3c43914d8a6501b00645c2ebb84d887b7fde44` |
| `tests/NfcG0.Tests.ps1` | `a4b3a60ef4c4f47cf2e3a7d8909db1d6526218c2c8adb0ae6d0c1098ed767c73` |

These hashes use the LF repository source bytes, as the earlier source
inventories did. If a Windows checkout converts `.ps1` to CRLF under
`.gitattributes`, compare its LF-normalized content and retain the raw
installed-copy hash separately; no other content normalization is allowed.
RS-4 and the token/key/helper files are unchanged by R41.

### C2. Bypass procedure (decision 77)

A bypass skips **every** rule of its ruleset for you: the pull request,
approvals, required checks, and the force-push and deletion protection. It
is an owner action outside the normal flow, used only to force-push or delete
a protected branch (for example rebuilding the trunk, as wave 2 needed on
2026-09-26). It is never a review or release exemption. It does not bypass the
tag ruleset (whose bypass list stays empty), the protected `release`
environment or the release workflow's checks: releasing still needs an
approved, fully checked pull request for the exact head, so a bypassed change
is released only after it passes those again.

**Your push path (owner → owner).** A6 routes every GitHub credential of this
repository through the App helper, so a push from your usual checkout would go
out as the app, which has no bypass. Use one of these, in your own terminal,
with no token on the command line:

- a one-command override:

  ```text
  git -c credential.https://github.com.helper= -c credential.https://github.com.helper=manager -c credential.https://github.com.username=<your-login> push --force-with-lease=<branch>:<old-sha> origin <new-sha>:refs/heads/<branch>
  ```

  The empty entry clears the App helper for this command only; Git Credential
  Manager (`manager`, or the helper name your Git installation uses) then asks
  you to sign in, in the browser. Afterwards remove the credential it stored
  for your account (A8);
- or a separate clone of your own, outside every worktree and without the
  repository-level App helper, used only for owner pushes.

Before:

1. **agent → none.** The board records the branch, the old SHA, the intended
   new SHA, the reason and the time.
2. **owner → owner.** A recovery ref keeps the old SHA: you push
   `refs/heads/recovery/<branch>-<yyyymmdd>` pointing at it (outside every
   protected pattern) through your push path, and delete it when the board
   closes the matter.
3. **agent → none.** The commander stops related writes and releases: agents
   neither push nor merge into that branch, no release run starts, and open
   pull requests into it wait. (The bypass leaves every other branch's rules
   in force, so the freeze covers that branch.)

During:

4. **owner → owner.** You push through your path, with
   `--force-with-lease=<branch>:<old-sha>` (or `git push origin --delete
   <branch>` for a deletion).

After:

5. **owner → owner.** You confirm the actor and the result: the repository's
   activity view for that branch shows the push or force push by your
   account, and the ruleset's rule insights list the bypass. Then sign out of
   the credential the push stored (A8).
6. The agent path is unchanged, checked in two steps:
   - 6a. **agent → none.** Local settings: with the app,
     `git config --local --get-all credential.https://github.com.helper`
     still shows the empty entry and the App helper, and
     `credential.https://github.com.useHttpPath` is still `true`; with the
     machine account,
     `git config --local --get credential.https://github.com.username` still
     shows its login.
   - 6b. **agent → App** (app path): the `gh` wrapper still reaches
     `/installation/repositories`; or **agent → machine account** (fallback):
     `gh api user --jq .login` still returns its login.
7. **agent → none**, then **owner → owner.** An agent reads the branch's
   effective rules (`GET /repos/<owner>/<repo>/rules/branches/<branch>`) and
   compares them with C1; you confirm the bypass lists on the page.
8. Approvals and evidence are renewed on the new head, and nothing approved
   before carries over. The app keeps only `actions: read`, so it cannot
   re-run workflows:
   - **owner → owner.** You review and approve again any open pull request
     into the branch.
   - Checks that start by themselves (a push to the pull request's branch
     triggers CI) need no operator; **agent → App** reads their results.
   - **owner → owner.** A check that needs a manual re-run is re-run by you in
     the browser; **agent → App** only reads its results.
   - **agent → none.** Local verification an agent runs (for example the
     structure check) is recorded with the commit it ran on.
9. Existing tags and release artifacts are never rewritten.
10. **agent → none.** The board records the result, the recovery ref and the
    checks of steps 5 to 7; related writes resume.

### C3. Pause procedure (decision 78, only without an admin bypass)

Disabling a ruleset removes its protection from **every branch it covers**,
for every actor: RS-1 covers `main`, RS-2 every trunk (`*.*.x`, today
`1.1.x`), RS-3 every release branch.

Before:

1. **agent → none.** The board records the ruleset, the branch to change, the
   old SHA, the intended new SHA, the reason, and a bounded window (for
   example 30 minutes).
2. **owner → owner.** Recovery ref, as C2 step 2.
3. **agent → none.** The commander freezes writes and related releases on
   **every branch the ruleset covers**, not only the target: no agent pushes
   or merges into any of them, no release run starts, and open pull requests
   into them wait. The freeze starts before the ruleset is disabled.
4. **owner → owner.** Export the ruleset's JSON immediately before disabling
   it; that export is what you restore and compare.

During:

5. **owner → owner.** Set the ruleset's enforcement to **Disabled** and note
   the time; push through your push path of C2; note the push result.
6. **owner → owner.** Whatever the push did (succeeded, failed or was
   interrupted), set the ruleset back to **Active** at once and note the
   time.

After:

7. **owner → owner**, then **agent → none.** Confirm that the ruleset is
   Active and that its rules and bypass list equal the export of step 4 (you
   compare on the page or with a new export; the agent compares the effective
   rules of every covered branch with C1).
8. **owner → owner.** If step 6 or 7 fails, or the window passes before the
   ruleset is Active again: stop. The freeze stays, nothing else happens, and
   you restore the ruleset from the export (re-enable, or re-create it by
   importing the export) until step 7 passes. A re-created ruleset has a new
   ID, so the script's restore (Rollback) no longer covers it.
9. **owner → owner.** Confirm the actor of the push in the repository's
   activity view. A push made while the ruleset was disabled is not a bypass,
   so no bypass evaluation is expected.
10. C2 steps 6a (**agent → none**) and 6b (**agent → App**, or **agent →
    machine account** on the fallback).
11. Renewed approvals and evidence on the new head, split as C2 step 8
    (**owner → owner** for approvals and manual re-runs, **agent → App** for
    reading results, **agent → none** for local verification); tags and
    release artifacts are never rewritten.
12. **agent → none.** Only after step 7 passes, the commander lifts the freeze.
    The board records the disable and restore times, the export compared, the
    push result and actor, and the verification.

## Part D: verification

Destructive tests run only on disposable branches that match the protected
patterns, never on `main`, `1.1.x` or a real release branch. An agent records
each result with the message GitHub returns, so that each block can be traced
to its rule, in the WS-GOV log; you confirm.

- [ ] **D0 Outside-package verification. owner → none**, then **owner → App**
      through the configured helper. From a newly opened ordinary Windows
      Terminal/PowerShell process outside the agent package, confirm that
      the helper path in `.git/config`, its `-DpapiPath`, and the installed
      wrapper path all exist and are accessible. Repeat an actual disposable
      branch push through that helper and clean up the probe. An
      `Everything up-to-date` result or a public read does not prove
      authentication. Passing inside the packaged app or one of its child
      shells does not pass D0. Record the external operator and outcome.

      Execution update, 2026-09-27, reported by Claude commander from Claude
      Code's Git Bash outside Codex: the configured helper and DPAPI files
      were visible; `git push --dry-run` to the disposable external-check
      ref completed authentication with exit 0; `ls-remote` confirmed no
      branch was created. **External visibility and authentication passed
      at dry-run level only.** Wrapper-path visibility was not reported,
      and an actual external disposable push remains outstanding; D0 as a
      whole stays unchecked. Existing D2 protection tests are separate.
- [ ] **D1 Formal refs by snapshot. agent → none**, then **owner → owner.** An
      agent reads the effective rules of `main`, `1.1.x` and, if present, a
      release branch (`GET /repos/<owner>/<repo>/rules/branches/<branch>`) and
      compares every rule with C1; you compare the bypass lists, visible only
      to an admin, in the UI.
- [ ] **D2 Disposable branches. agent → App** (or machine account). An agent
      creates `9.9.x` (trunk pattern) and `9.9.9` (release pattern) from the
      trunk head. Then, each with the rule named in GitHub's message: a direct
      push to `9.9.x` is rejected (pull request required); a force push to
      `9.9.x` is rejected (force push blocked); deleting `9.9.x` is rejected
      (deletion restricted); a force push to `9.9.9` is rejected.
- [ ] **D3a App identity through real writes. agent → App.** The agent pushes
      `feature/1.1.13/g0-check` (a one-line change under `docs/handoff/`)
      through the helper and opens a pull request into `9.9.x` through the
      wrapper: the pull request author and the pusher in its timeline are
      `<app-slug>[bot]`, and `gh api /installation/repositories` through the
      wrapper succeeds (only an installation token can call it) and lists only
      this repository. A successful public read alone proves no identity.
- [ ] **D3b Machine-account identity (fallback only). agent → machine
      account**, then **owner → machine account.** The same push and pull
      request: the author and the pusher are the machine account, and `gh api
      user --jq .login` returns it; you sign in as the machine account and
      check on its settings that it can reach only this repository.
- [ ] **D4 Approval behavior. owner → owner** for approvals, **agent → App**
      for pushes. You approve the head in the browser. The agent pushes a
      commit that changes the diff: the approval is dismissed. You approve
      again; the agent pushes a new SHA with the same tree (an empty commit):
      record whether GitHub keeps or dismisses the approval. Either result is
      acceptable in G0, because the exact-head rule belongs to G1-A. An
      unresolved review thread blocks the merge until it is resolved.
- [ ] **D5 Positive path without a bypass. agent → App.** With your approval on
      the current head, all required checks green and every thread resolved,
      the agent identity, which has no bypass, merges the pull request into
      `9.9.x` (`gh pr merge <n> --merge --match-head-commit <head-sha>` through
      the wrapper).
- [ ] **D6 Owner bypass or pause, only now. owner → owner.** Take the path that
      G0 set up:
  - **D6-C2 (admin bypass).** You force-push `9.9.x` and then delete it, each
    through C2 (a disposable branch, so C2 step 3 concerns only it); the
    rule insights list both bypasses, and the activity view shows you as the
    actor.
  - **D6-C3 (pause).** Disabling RS-2 also unprotects `1.1.x`, so C3 applies in
    full: the freeze covers every `*.*.x` branch, `1.1.x` included, before
    RS-2 is disabled. You force-push `9.9.x` and delete it inside one window,
    restore RS-2 whatever happens, and complete C3 steps 7 to 12. The record
    holds the disable and restore times, the export compared and the actual
    push actor; no bypass evaluation is expected.
- [ ] **D7 Cleanup. agent → App**, and **owner → owner** for recovery refs. The
      agent deletes `9.9.9` (deletion allowed) and closes anything left open;
      you delete the recovery refs; the agent records all results and
      messages in the log.

## Rollback

- [ ] **owner → owner.** Rulesets, with `Set-NfcRulesets.ps1` and the apply's
      backup directory, `gh` signed in as you (A8) and no concurrent ruleset
      changes:

      ```powershell
      pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Restore -Owner <owner> -Repo <repo> -BackupDirectory '<backup-place>\g0-apply' -WhatIf
      pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Restore -Owner <owner> -Repo <repo> -BackupDirectory '<backup-place>\g0-apply'
      ```

      The first line only shows the plan. The restore acts only on what
      `index.json` records as applied: it puts back the body of `main` saved
      before the apply (its bypass list included) and sets the RS-2 and RS-3
      that this apply created to Disabled, without deleting them; it leaves
      RS-4 and every other ruleset alone. It refuses to start while any entry
      is pending (reconcile it first, C1 step 5), and it stops if a ruleset no
      longer equals its saved readback, for example after any later edit or a
      C3 re-creation: then you restore by hand from your own export and
      screenshots ("Before you start"). Each item needs your `YES`; it checks
      the live ruleset again before each write, verifies the readback, and
      records `restore_pending` and then `restored` in `index.json`. Like the
      apply, it is not atomic and does not roll back by itself. Afterwards
      compare the effective rules with your pre-G0 export, and delete the
      disabled RS-2 and RS-3 in the browser if you no longer want them.
- [ ] **owner → owner**, then **owner → none.** The app: uninstall or suspend
      it (uninstalling cuts access at once) or delete it, and delete its key
      in the app settings; then remove the DPAPI file and the Bitwarden note
      yourself, and check both, especially after a partial storage failure
      (A2). On the machine-account path, remove the account's access instead.
- [ ] **owner → none.** Repository-scope Git settings, which are the only Git
      settings G0 changed; an agent prepares the commands from the non-secret
      facts you confirmed:
  1. `git config --local --unset-all credential.https://github.com.helper`
     removes the empty entry and the App helper together.
  2. If that key had values at the repository scope before G0, add them back
     in their original order, each with `git config --local --add
     credential.https://github.com.helper "<value>"`, including an empty
     entry where one was. If it had none, leave it unset.
  3. `git config --local --unset credential.https://github.com.useHttpPath`,
     or set it back to its value from before G0 if it had one at the
     repository scope.
  4. `user.name` and `user.email`: set them back to the saved values, or
     unset them if they did not exist at the repository scope before.
  5. Machine-account path: the same for `credential.https://github.com.username`.
  6. Stop using the `gh` wrapper; you may remove the helper and the wrapper
     from your script folder once nothing uses them.
- [ ] **owner → none.** Settings your backup marked secret-bearing: you restore
      them yourself, and compare the result with your raw backup in your own
      terminal (`git config --show-origin --get-regexp "^credential\."`).
- [ ] **owner → owner.** Your own command-line access, if you want it back: you
      sign in again yourself (`gh auth login`, the Git Credential Manager
      prompt), in your own session. Nothing secret is backed up or handed to
      an agent.

Until G1-B the old record gate still runs, so this returns the repository to
today's state.

## After G0

- **agent → App** for pushes and pull requests, **owner → owner** for
  approvals and for merges you make yourself. Integration into `1.1.x` and
  release branches happens only through pull requests, one merge at a time
  (decision 24).
- **Historical trunk catch-up (decision 66; ended with G1-B).** The procedure
  below applied before G1-B. Current catch-up follows
  [branch governance](../../governance/branch-version-and-release-governance.md#establish-branch-authority).
  RS-2 blocks decision 23's
  fast-forward. After a release, the trunk catches up with `main` through a
  pull request from `main` into `1.1.x`, merged with a merge commit:
  1. **agent → none.** The commander freezes every other write and merge into
     `main` and `1.1.x`, and every release, from step 2 until step 7 is
     complete.
  2. **agent → App.** Record the pull request's base SHA (the trunk head) and
     head SHA (the `main` head) through the wrapper: `gh pr view <n> --json
     baseRefOid,headRefOid`.
  3. **agent → App.** Fetch GitHub's test merge `refs/pull/<n>/merge` and
     confirm that its two parents are exactly those SHAs.
  4. **agent → none.** Its tree equals both parent trees: `git rev-parse
     <m>^{tree}`, `<m>^1^{tree}` and `<m>^2^{tree}` are one value; and the
     history gate in force passes with the test merge as `HEAD`
     (`python scripts/verify.py --structure-only`), since CI checks the pull
     request's head, not the merge.
  5. **owner → owner.** You approve the pull request's head.
  6. **agent → App.** Read both SHAs again; if either changed, return to
     step 3. Then merge only with `gh pr merge <n> --merge
     --match-head-commit <head-sha>` through the wrapper, on your go-ahead for
     this merge; no other merge path is used.
  7. **agent → none.** Confirm that the merge commit's parents are the
     recorded SHAs and that its tree equals both parent trees, and run the
     structure check on the new trunk head.

  If step 3, 4, 5 or 6 fails, nothing is merged: the commander stops and
  asks you, and the freeze ends only on your decision. If step 7 fails, the
  merge has already happened: the commander records the merge commit, its
  parents and trees and the failing check in the board, keeps the freeze on
  integration into the trunk and on every release, and you decide what
  follows (for example a revert through a pull request, or a rebuild under C2
  or C3).
- **owner → owner.** Releases: you approve the app-authored release pull
  request normally, so you dispatch `release.yml` with
  `owner_self_approval_exception` set to `false`. The exception and the
  Codex-only review rule stay in the code until their product-neutral
  replacement is built and tested (decision 49, release batch R-3).
- The record gate keeps running until G1-B (decision 50).
