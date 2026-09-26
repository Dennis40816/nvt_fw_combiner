# G0 owner checklist: agent GitHub identity and rulesets

Status: third version, 2026-09-26, revised after the second independent review
(REJECT; findings F-5 to F-8 and F-11 to F-13); for re-review, and not to be
executed before that re-review passes. It implements board decisions 49 and 56
(agents get their own GitHub identity, a GitHub App first and a machine account
as fallback, created by the owner), the G0 step of decision 50, decision 65
(key custody), decision 66 (trunk catch-up and an owner-only force-push
means), decision 67 (O-3, for RS-1g), decision 77 (a standing owner-only
bypass on `main`, the trunk and release branches, never a review or release
exemption) and decision 78 (pausing a ruleset when an admin bypass cannot be
set). Design: [governance ADR draft](ADR-DRAFT-governance-reset.md), items 7
to 11; log: [WS-GOV](WS-GOV.md). Nothing here is done until the owner does it.

`<owner>/<repo>` stands for this repository's full name, as listed in
[`agent-issue-tracker.md`](../../governance/agent-issue-tracker.md).

## What G0 changes, and who does what

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

| Who | Does | Never does |
| --- | --- | --- |
| Owner (you) | Every account, key and secret step: creates the app (or the machine account), completes the manifest conversion, installs the app, keeps its private key, backs up and restores any credential setting, sets up the token helper, changes every setting and ruleset, approves, merges, and runs the bypass or pause procedure | - |
| Agents | Prepare this checklist and non-secret files, draft the token helper for your review, set non-secret Git settings on request, run read-only checks and the verification of part D; obtain one-hour installation tokens only through the helper | Create accounts or apps; read the private key, your password manager, DPAPI-protected data, credential stores or credential settings you have not confirmed as non-secret; read or copy any long-lived token or your credentials; modify the helper; change settings or rulesets; approve pull requests or environments; use a bypass |

**This is a rule, not a technical boundary (decision 65).** Agents run under
your Windows user. Everything that user can open is technically within reach
of an agent process: the App private key in your store, an unlocked password
manager, DPAPI-protected files, Windows Credential Manager, Git credential
settings, the token helper and its configuration, your signed-in browser
session, and any login of yours in `gh` or Git Credential Manager. Decision 65
accepts a procedural constraint: agents do not read, copy or use them, except
through the helper's token interface. A technical boundary, such as a separate
Windows account or service that holds the key, was not adopted.

## Platform facts not verified locally

This checklist relies on GitHub and Windows behavior that was taken from
documentation and not verified on this repository or machine. G0 confirms
each one where the last column says before relying on it; if one does not
hold, stop and tell the commander.

| Fact | Used in | Confirmed by |
| --- | --- | --- |
| The manifest conversion `POST /app-manifests/{code}/conversions` needs no token, works once within one hour and returns `pem` with the other credentials | A2 | the conversion succeeding |
| An app name has at most 34 characters | A2 | the app form |
| A Windows Credential Manager secret is limited to 2,560 bytes | A4 | your store's own check |
| Installation tokens expire after one hour; `/installation/repositories` accepts only installation tokens | A6, D3a | D3a |
| A personal repository offers "Repository admin" in a ruleset bypass list; the API form is `RepositoryRole` with `actor_id` 5 and `bypass_mode` `always`, which also permits direct and force pushes | part C | the ruleset page and its saved JSON; D6 |
| `do_not_enforce_on_create` only exempts the required-status-checks rule when a branch is created; it skips no other rule, and the pull-request rule does not block creating a branch | RS-2, RS-3, D2 | the ruleset page's help text; D2 |
| The UI settings of part C have the API names given there (`dismiss_stale_reviews_on_push` and the others) | part C | the saved ruleset JSON |
| Rule insights list bypassed evaluations with actor and ref; the repository activity view lists pushes and force pushes with their actor | part C, D5, D6 | D5 and D6 |
| On a personal repository a collaborator gets fixed access; the Collaborators page may offer no role selector | part B | that page |
| `gh pr merge --match-head-commit <sha>` refuses to merge when the head moved | after G0 | its first use |
| GitHub's test merge `refs/pull/<n>/merge` has the base and the head as parents | after G0 | step 2 of the catch-up checks the parents |
| An approval may survive a new SHA with identical changes | top of this page, D4 | D4 |

## Before you start

- [ ] No release run is in progress and none is planned during G0.
- [ ] Open pull requests authored by your account are merged or closed (the
      commander lists them). After G0 you cannot approve a pull request your
      own account opened; agents reopen the unmerged ones as the app.
- [ ] Save the complete current `main` ruleset (22009240) so RS-1 can be
      restored: its JSON as an admin sees it (the ruleset page's export, or the
      API in your own session; the bypass list is visible only to an admin),
      and your screenshot of every section. An agent can save the non-admin
      JSON read-only as a cross-check.
- [ ] Credential settings, by you alone: in your own terminal, not recorded
      or logged, run `git config --show-origin --get-regexp "^credential\."`
      inside this repository (it covers the system, global and repository
      scopes) and save the raw output where only you keep it. Check it for
      secrets, such as a password or a helper command that embeds a token.
      Then give the agent only what you confirm as non-secret, per scope and
      in order: whether `credential.helper` and
      `credential.https://github.com.helper` exist, their values, and any
      `credential.*.username`. Anything secret-bearing stays with you and you
      restore it yourself (rollback).
- [ ] An agent saves the repository's `user.name` and `user.email` settings
      (`git config --local --get-regexp "^user\."`) and your confirmed
      summary to a local file outside every repository, for the rollback.
- [ ] You have 30 to 60 minutes; GitHub settings pages open in a browser
      signed in as you, with two-factor authentication.

## Part A: GitHub App (primary, decision 56)

### A1. The manifest

The manifest asks for the smallest permission set G0 needs. It contains no
secret and no `workflows` permission.

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
  "redirect_url": "https://github.com/<owner>/<repo>",
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
| `contents: write` | Push feature branches, read files, merge a pull request when you ask an agent to | No |
| `pull_requests: write` | Open, update and merge pull requests, post review records and replies | No |
| `checks: read`, `statuses: read`, `actions: read` | Read CI results, logs and artifacts | Only if agents never read CI |

Not requested: `workflows` (added by you, with your approval of the new
permission on the installation, just before the first authorized batch that
edits `.github/workflows/`, release batch R-1), administration, secrets,
variables, environments, deployments, issues (the bug ledger lives in the
repository, and GitHub issue writes need separate authorization), members,
pages and packages. The webhook is inactive and subscribes to no event.

### A2. Create the app: manifest flow with conversion

- [ ] Replace `<owner>` and `<repo>` (pick another name if GitHub reports it
      taken; at most 34 characters).
- [ ] Save the HTML below as a local file outside every repository and
      worktree, and paste the manifest (with your values) where marked:

      ```html
      <form action="https://github.com/settings/apps/new?state=nfc-g0" method="post">
        <input type="hidden" name="manifest" id="manifest">
        <input type="submit" value="Create the agent GitHub App">
      </form>
      <script>
        document.getElementById("manifest").value = JSON.stringify(
          /* paste the manifest object here */
        );
      </script>
      ```

- [ ] Open the file in your browser and press the button. GitHub shows the
      app form, filled in: check the name, the permissions above and that the
      webhook is inactive, then press **Create GitHub App**.
- [ ] GitHub redirects to the repository page with `?code=<code>&state=nfc-g0`
      in the address bar. The code works once, for one hour. Within that hour,
      in your own terminal (not an agent session, and not a terminal whose
      output is logged or recorded), complete the conversion:

      ```powershell
      $conversion = Invoke-RestMethod -Method Post -Headers @{ Accept = 'application/vnd.github+json' } -Uri 'https://api.github.com/app-manifests/<code>/conversions'
      ```

      Do not print `$conversion`: it holds the app's `id`, `slug`,
      `client_id`, `client_secret`, `webhook_secret` and `pem` (the private
      key).
- [ ] Put `$conversion.pem` into your password manager or store (A4). Note
      `id`, `slug` and `client_id` for A5; they are not secret. The client
      secret and the webhook secret are not used; do not keep them anywhere an
      agent can read (they can be regenerated in the app settings). Then run
      `Remove-Variable conversion`, clear the clipboard and close the
      terminal. The conversion response never enters an agent transcript,
      Git or a general log. The command line itself holds only the used code.
- [ ] If the hour passes before the conversion, delete the app (app settings,
      **Advanced**, **Delete GitHub App**) and start again. Generating a key
      in the app settings later is for key rotation, not a substitute for the
      conversion.

### A3. Install it on this repository only

- [ ] On the app page, choose **Install App**, then your account.
- [ ] Choose **Only select repositories**, select this repository, install.
- [ ] Note the installation ID from the address bar
      (`.../settings/installations/<id>`).

### A4. Key custody (decision 65)

- [ ] Keep the key in your own password manager or store. Windows Credential
      Manager limits a stored secret to 2,560 bytes: check the stored size of
      your key, since a key of about 1.7 KB fits as UTF-8 but not as UTF-16.
- [ ] Agents must not read the key, your password manager, DPAPI-protected
      data or credential stores. As stated above, this is a rule, not a
      technical boundary.
- [ ] If the key ever leaks, delete it in the app settings and generate a new
      one; uninstalling the app cuts access at once.

### A5. Non-secret values

- [ ] Keep these in the helper's local configuration (not in Git): App ID
      and Client ID, installation ID (A3), the bot login `<app-slug>[bot]` and
      its user ID (an agent can look it up through the public API).

### A6. Token helper (may run under the same Windows user, decision 65)

- [ ] Ask an agent to draft the helper. The draft contains no secret; you
      review it and install it in a folder outside every repository and
      worktree. Its contract:
  - read the key from your store (A4) into memory only; never print or log it;
  - sign a JSON Web Token (RS256; issuer = Client ID; issued 60 seconds in
    the past; expires within 10 minutes);
  - call `POST /app/installations/<installation-id>/access_tokens`, limited
    to this repository and to the A1 permissions, and return only the token,
    which expires after one hour;
  - for Git, answer the credential-helper `get` request with
    `username=x-access-token` and `password=<token>`, and ignore `store` and
    `erase`;
  - for `gh`, a wrapper sets `GH_TOKEN` from the helper for one `gh`
    invocation.
- [ ] Agents use only that token interface: they never open the key store,
      change the helper or its configuration, or run the key-reading part on
      its own (the rule of decision 65).
- [ ] Wire Git to the helper at the repository scope (all worktrees share it;
      an agent may run these on your request, since they contain no secret):

      ```text
      git config --local --replace-all credential.https://github.com.helper ""
      git config --local --add credential.https://github.com.helper "!<helper command> git"
      ```

      The empty first entry stops Git from using the helpers of the system
      and global scopes, and so your stored credentials, for this repository.
      Part C explains how you push as yourself despite it.

### A7. Agent commit identity

- [ ] With your approval, an agent sets, at the repository scope:
      `user.name` = `<app-slug>[bot]` and `user.email` =
      `<bot-user-id>+<app-slug>[bot]@users.noreply.github.com`. Agent commits
      keep their co-author lines.

### A8. Your own sessions

- [ ] Sign your account out of `gh` on this machine
      (`gh auth logout --hostname github.com --user <your-login>`) and remove
      your GitHub entry from Git Credential Manager (Windows Credential
      Manager, the `git:https://github.com` entries). This removes the easiest
      path to your credentials; it creates no boundary.
- [ ] Do owner actions in the browser: approvals, merges, release dispatch and
      approval, settings. A signed-in browser session is also reachable by
      processes of your Windows user; it too is covered by the rule only.
- [ ] When you need your own command-line access (the bypass procedure of
      part C), sign in for that task only, through the owner path there, and
      sign out afterwards.

## Part B: machine account (fallback, decision 56)

Use this only if the app does not work for you. GitHub's terms allow a
machine account that you control and use only for automation.

In this path agents work with a **long-lived token**: Git Credential Manager
and `gh` hold it, and every agent Git or `gh` call uses it. That contradicts
the "never read or copy any long-lived token" line of the table above in
practice, so this path rests on the rule alone. Compared with the app it adds
these risks:

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

- [ ] Create the account in a private browser window, with its own email
      address and two-factor authentication.
- [ ] Repository settings, **Collaborators**: add the machine account. A
      personal repository gives a collaborator the fixed collaborator access
      the page describes (reading and pushing, no administration) and may show
      no role selector; check on the page what it grants. Accept the
      invitation as that account.
- [ ] Token: prefer a fine-grained token limited to this repository if GitHub
      offers the repository; otherwise a classic token with only the `repo`
      scope and an expiry. Add the `workflow` scope only just before the
      first authorized batch that edits workflows (release batch R-1). You type
      the token yourself, in your own terminal, into the Git Credential
      Manager prompt or `gh auth login --with-token`; never into an agent
      session.
- [ ] For this repository set `credential.https://github.com.username` to the
      machine account's login, so Git Credential Manager picks that account,
      and set the commit identity to its noreply address. Do A8 as well.

## Part C: rulesets, bypass and pause

### C1. Ruleset changes, each for your approval

Settings, **Rules**, **Rulesets**. The UI takes branch-name patterns; the API
takes full ref patterns, so both are listed. RS-2 and RS-3 are new rulesets.
The API values are from the documentation (see the list of facts not
verified locally); the JSON GitHub saves is the record.

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
| RS-1i | `main` | Block force pushes; Restrict deletions | on; on | rules `non_fast_forward`, `deletion` |
| RS-1j | `main` | Require status checks to pass | unchanged: the saved checks with their sources, "Require branches to be up to date" and "Do not require status checks on creation" exactly as saved | unchanged: `required_status_checks` (each `context` with its `integration_id`), `strict_required_status_checks_policy`, `do_not_enforce_on_create` as saved |
| RS-1k | `main` | Bypass list (decision 77) | Repository admin, Always allow | `[{"actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always"}]` |
| RS-2 | new "trunk" | Target branches | Include by pattern: `*.*.x` | `"include": ["refs/heads/*.*.x"], "exclude": []` |
| RS-2 | "trunk" | Rules | RS-1b to RS-1i; required checks: the same three contexts with the same sources as RS-1j, "up to date" off, "Do not require status checks on creation" on | as RS-1b to RS-1i; `required_status_checks` copied from RS-1j, `strict_required_status_checks_policy: false`, `do_not_enforce_on_create: true` |
| RS-2 | "trunk" | Bypass list (decisions 66 and 77) | as RS-1k | as RS-1k |
| RS-3 | new "release branches" | Target branches | Include by pattern: `*.*.*`; Exclude by pattern: `*.*.x` | `"include": ["refs/heads/*.*.*"], "exclude": ["refs/heads/*.*.x"]` |
| RS-3 | "release branches" | Rules | RS-1b to RS-1h; Block force pushes on; **deletion allowed** (release closure deletes the branch after its tag); required checks as RS-2 | as RS-2 without `deletion` |
| RS-3 | "release branches" | Bypass list (decisions 66 and 77) | as RS-1k | as RS-1k |
| RS-4 | tag ruleset `refs/tags/v*` (existing) | Everything | confirm only: update and deletion restricted, bypass list **empty** | unchanged; the release policy requires an empty bypass list |

Notes:

- "Do not require status checks on creation" (`do_not_enforce_on_create`) only
  lets a new branch be created although its commit lacks the required checks.
  It skips no other rule and does not promise that any branch can be
  created. In RS-2 it lets the trunk itself be created, for example `1.2.x`
  from `main`; in RS-3 it lets a release branch be cut from the trunk.
- Confirm on each protected branch's effective rules (part D, D1) that there
  is no "Require linear history" rule (merge commits are required), no
  signed-commit or deployment rule, and no other ruleset adding rules.
- The bypass actor is the Repository admin role, which on this personal
  repository is only you; the app and the machine account never are. The
  page may not offer that role: confirm it when you edit RS-1. If it is not
  offered, leave every bypass list empty and use the pause procedure (C3,
  decision 78) instead of the bypass.
- Not part of G0: the branch-name allowlist and automatic branch deletion
  (checklist A-5, approved item by item under decision 24); if you approve the
  allowlist later, it must let the agent identity create `feature/*.*.*/**`
  and you create `recovery/**`.
- Unchanged: the protected `release` environment with you as the required
  reviewer, the Codex review app, and the read-only default workflow token.

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

Your push path. A6 routes every GitHub credential of this repository through
the App helper, so a push from your usual checkout would go out as the app,
which has no bypass. Use one of these, in your own terminal, with no token on
the command line:

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

1. The board records the branch, the old SHA, the intended new SHA, the
   reason and the time.
2. A recovery ref keeps the old SHA: you push
   `refs/heads/recovery/<branch>-<yyyymmdd>` pointing at it (outside every
   protected pattern), and delete it when the board closes the matter.
3. The commander stops related writes and releases: agents neither push nor
   merge into that branch, no release run starts, and open pull requests into
   it wait.

During:

4. You push through your path, with `--force-with-lease=<branch>:<old-sha>`
   (or `git push origin --delete <branch>` for a deletion).

After:

5. You confirm the actor and the result: the repository's activity view for
   that branch shows the push or force push by your account, and the
   ruleset's rule insights list the bypass. Then check that the agent path is
   unchanged: `git config --local --get-all
   credential.https://github.com.helper` still shows the empty entry and the
   App helper.
6. An agent reads the branch's effective rules
   (`GET /repos/<owner>/<repo>/rules/branches/<branch>`) and compares them
   with C1; you confirm the bypass lists on the page.
7. Approvals and evidence are renewed on the new head: open pull requests into
   the branch are reviewed and approved again, checks run again, and nothing
   approved before carries over.
8. Existing tags and release artifacts are never rewritten.
9. The board records the result, the recovery ref and the checks of steps 5
   and 6; related writes resume.

### C3. Pause procedure (decision 78, only without an admin bypass)

1. Steps 1 to 3 of C2, and a bounded window (for example 30 minutes) whose
   start and end the board records.
2. You set the ruleset's enforcement to **Disabled**, push through your path
   of C2, and set it back to **Active** at once. While it is disabled, no rule
   of that ruleset protects any branch it covers, for any actor.
3. Steps 5 to 9 of C2, with step 6 covering every branch the ruleset covers.

## Part D: verification

Destructive tests run only on disposable branches that match the protected
patterns, never on `main`, `1.1.x` or a real release branch. An agent records
each result with the message GitHub returns, so that each block can be traced
to its rule, in the WS-GOV log; you confirm.

- [ ] **D1 Formal refs by snapshot.** An agent reads the effective rules of
      `main`, `1.1.x` and, if present, a release branch
      (`GET /repos/<owner>/<repo>/rules/branches/<branch>`) and compares every
      rule with C1; you compare the bypass lists, visible only to an admin, in
      the UI.
- [ ] **D2 Disposable branches.** An agent creates `9.9.x` (trunk pattern) and
      `9.9.9` (release pattern) from the trunk head, as the agent identity.
      Then, each with the rule named in GitHub's message: a direct push to
      `9.9.x` is rejected (pull request required); a force push to `9.9.x` is
      rejected (force push blocked); deleting `9.9.x` is rejected (deletion
      restricted); a force push to `9.9.9` is rejected.
- [ ] **D3a App identity through real writes.** The agent pushes
      `feature/1.1.13/g0-check` (a one-line change under `docs/handoff/`)
      through the helper and opens a pull request into `9.9.x`: the pull
      request author and the pusher in its timeline are `<app-slug>[bot]`,
      and `gh api /installation/repositories` through the wrapper succeeds
      (only an installation token can call it) and lists only this
      repository. A successful public read alone proves no identity.
- [ ] **D3b Machine-account identity (fallback only).** The same push and pull
      request: the author and the pusher are the machine account; `gh api
      user` through its `gh` login returns that account; and you check on the
      account's settings that it can reach only this repository.
- [ ] **D4 Approval behavior.** You approve the head in the browser. The agent
      pushes a commit that changes the diff: the approval is dismissed. You
      approve again; the agent pushes a new SHA with the same tree (an empty
      commit): record whether GitHub keeps or dismisses the approval. Either
      result is acceptable in G0, because the exact-head rule belongs to G1-A.
      An unresolved review thread blocks the merge until it is resolved.
- [ ] **D5 Positive path without a bypass.** With your approval on the current
      head, all required checks green and every thread resolved, the agent
      identity, which has no bypass, merges the pull request into `9.9.x`
      (`gh pr merge <n> --merge --match-head-commit <head-sha>` through the
      wrapper). If you merge in the browser instead, leave the option to merge
      without the requirements unchecked, and confirm in the rule insights
      that the merge passed and was not a bypass.
- [ ] **D6 Owner bypass.** Only now: you force-push `9.9.x` and then delete
      it, each through your push path and the steps of C2 (a disposable
      branch, so step 3 concerns only it); both succeed and are recorded with
      the actor.
- [ ] **D7 Cleanup.** The agent deletes `9.9.9` (deletion allowed) and closes
      anything left open; you delete the recovery refs; the agent records all
      results and messages in the log.

## Rollback

- [ ] GitHub: set RS-2 and RS-3 to Disabled (or delete them), restore RS-1
      from your saved export and screenshots, and uninstall or suspend the
      app (or remove the machine account's access).
- [ ] Repository-scope Git settings, which are the only Git settings G0
      changed. An agent works only from the non-secret facts you confirmed:
  1. `git config --local --unset-all credential.https://github.com.helper`
     removes the empty entry and the App helper together.
  2. If that key had values at the repository scope before G0, add them back
     in their original order, each with `git config --local --add
     credential.https://github.com.helper "<value>"`, including an empty
     entry where one was. If it had none, leave it unset.
  3. `user.name` and `user.email`: set them back to the saved values, or
     unset them if they did not exist at the repository scope before.
  4. Machine-account path: the same for `credential.https://github.com.username`.
  5. Stop using the `gh` wrapper.
- [ ] Settings your backup marked secret-bearing: you restore them yourself.
- [ ] You compare the result with your raw backup, in your own terminal
      (`git config --show-origin --get-regexp "^credential\."`).
- [ ] Your own command-line access, if you want it back: you sign in again
      yourself (`gh auth login`, the Git Credential Manager prompt), in your
      own session. Nothing secret is backed up or handed to an agent.

Until G1-B the old record gate still runs, so this returns the repository to
today's state.

## After G0

- Agents push and open pull requests as the app; you approve and merge in
  the browser, one action at a time (decision 24). Integration into `1.1.x`
  and release branches happens only through pull requests.
- **Trunk catch-up until G1-B (decision 66).** RS-2 blocks decision 23's
  fast-forward. After a release, the trunk catches up with `main` through a
  pull request from `main` into `1.1.x`, merged with a merge commit:
  1. Record the pull request's base SHA (the trunk head) and head SHA (the
     `main` head): `gh pr view <n> --json baseRefOid,headRefOid`.
  2. Fetch GitHub's test merge `refs/pull/<n>/merge` and confirm that its two
     parents are exactly those SHAs.
  3. Its tree equals both parent trees: `git rev-parse <m>^{tree}`,
     `<m>^1^{tree}` and `<m>^2^{tree}` are one value.
  4. The history gate in force passes with the test merge as `HEAD`
     (`python scripts/verify.py --structure-only`); CI checks the pull
     request's head, not the merge.
  5. Just before merging, read both SHAs again; if either changed, return to
     step 2. Merge with `gh pr merge <n> --merge --match-head-commit
     <head-sha>`, or you press Merge after the agent confirms the SHAs.
  6. After the merge, confirm that the merge commit's parents are the recorded
     SHAs and that its tree equals both parent trees, and run the structure
     check on the new trunk head.

  If step 2, 3, 4 or 5 fails, nothing is merged: the commander stops and asks
  you. If step 6 fails, the merge has already happened: the commander records
  the merge commit, its parents and trees and the failing check in the board,
  stops further integration into the trunk and every release, and you decide
  what follows (for example a revert through a pull request, or a rebuild
  under C2).
- Releases: you approve the app-authored release pull request normally, so
  you dispatch `release.yml` with `owner_self_approval_exception` set to
  `false`. The exception and the Codex-only review rule stay in the code until
  their product-neutral replacement is built and tested (decision 49, release
  batch R-3).
- The record gate keeps running until G1-B (decision 50).
