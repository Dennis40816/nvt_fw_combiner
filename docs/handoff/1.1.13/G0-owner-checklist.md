# G0 owner checklist: agent GitHub identity and rulesets

Status: revised 2026-09-26 after the independent review of the first version
(REJECT, findings F-1 to F-10), for re-review; not to be executed before that
re-review passes. It implements board decisions 49 and 56 (agents get their
own GitHub identity, a GitHub App first and a machine account as fallback,
created by the owner), the G0 step of decision 50, decision 65 (key custody),
decision 66 (trunk catch-up and an owner-only force-push means) and, for
RS-1g, decision 67 (O-3). Design: [governance ADR draft](ADR-DRAFT-governance-reset.md),
items 7 to 11; log: [WS-GOV](WS-GOV.md). Nothing here is done until the owner
does it.

`<owner>/<repo>` stands for this repository's full name, as listed in
[`agent-issue-tracker.md`](../../governance/agent-issue-tracker.md).

## What G0 changes, and who does what

After G0, agents push branches and open pull requests as the GitHub App, and
you review and approve them as an ordinary reviewer. The rulesets make an
approval, the required checks and resolved review threads mandatory on
`main`, the trunk and release branches.

GitHub's ruleset settings dismiss an approval when new reviewable commits
arrive and require the most recent push to be approved; they do not promise
that every new commit SHA loses its approval (a new SHA whose changes are
identical may keep it). The exact rule "the approval was given on the current
head SHA" is enforced by the authority check of G1-A and by the release policy
in R-3, not by G0.

| Who | Does | Never does |
| --- | --- | --- |
| Owner (you) | Every account, key and secret step: creates the app (or the machine account), completes the manifest conversion, installs the app, keeps its private key, sets up the token helper, changes every setting and ruleset, approves, merges, and uses the force-push bypass | - |
| Agents | Prepare this checklist and non-secret files, draft the token helper for your review, set non-secret Git settings on request, run read-only checks and the verification of Part D; obtain one-hour installation tokens only through the helper | Create accounts or apps; read the private key, your password manager, DPAPI-protected data or credential stores; read or copy any long-lived token or your credentials; modify the helper; change settings or rulesets; approve pull requests or environments; use a bypass |

**This is a rule, not a technical boundary (decision 65).** Agents run under
your Windows user. Everything that user can open is technically within reach
of an agent process: the App private key in your store, an unlocked password
manager, DPAPI-protected files, Windows Credential Manager, the token helper
and its configuration, your signed-in browser session, and any login of yours
in `gh` or Git Credential Manager. Decision 65 accepts a procedural constraint:
agents do not read, copy or use them, except through the helper's token
interface. A technical boundary, such as a separate Windows account or
service that holds the key, was not adopted.

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
- [ ] An agent saves the current non-secret Git settings of this repository
      and the global scope (`git config --show-origin --get-regexp
      "^(credential|user)\."`) to a local file outside every repository, for
      the rollback. Those settings contain helper names and user names, no
      secret; if a line ever looks like a secret, the agent stops and tells you.
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
| `pull_requests: write` | Open and update pull requests, post review records and replies | No |
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
- [ ] Wire Git to the helper for this repository (all worktrees share this
      setting; an agent may run these on your request, since they contain no
      secret):

      ```text
      git config --replace-all credential.https://github.com.helper ""
      git config --add credential.https://github.com.helper "!<helper command> git"
      ```

      The empty first entry stops Git from falling back to your stored
      credentials for this repository.

### A7. Agent commit identity

- [ ] With your approval, an agent sets, for this repository:
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
- [ ] When you need your own command-line access (for example the bypass in
      Part C), sign in for that task and sign out afterwards.

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
- [ ] Repository settings, **Collaborators**: invite it with the **Write**
      role (not Maintain or Admin); accept the invitation as that account.
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

## Part C: ruleset changes, each for your approval

Settings, **Rules**, **Rulesets**. The UI takes branch-name patterns; the API
takes full ref patterns, so both are listed. RS-2 and RS-3 are new rulesets.

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
| RS-1k | `main` | Bypass list | see the force-push means below | see below |
| RS-2 | new "trunk" | Target branches | Include by pattern: `*.*.x` | `"include": ["refs/heads/*.*.x"], "exclude": []` |
| RS-2 | "trunk" | Rules | RS-1b to RS-1i; required checks: the same three contexts with the same sources as RS-1j, "up to date" off, "Do not require status checks on creation" on (so a branch can be created, for example a release branch cut from the trunk) | as RS-1b to RS-1i; `required_status_checks` copied from RS-1j, `strict_required_status_checks_policy: false`, `do_not_enforce_on_create: true` |
| RS-2 | "trunk" | Bypass list | Repository admin, Always allow | `[{"actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always"}]` |
| RS-3 | new "release branches" | Target branches | Include by pattern: `*.*.*`; Exclude by pattern: `*.*.x` | `"include": ["refs/heads/*.*.*"], "exclude": ["refs/heads/*.*.x"]` |
| RS-3 | "release branches" | Rules | RS-1b to RS-1h; Block force pushes on; **deletion allowed** (release closure deletes the branch after its tag); required checks as RS-2, including "Do not require status checks on creation" on, so the release branch can be cut | as RS-2 without `deletion` |
| RS-3 | "release branches" | Bypass list | Repository admin, Always allow | as RS-2 |
| RS-4 | tag ruleset `refs/tags/v*` (existing) | Everything | confirm only: update and deletion restricted, bypass list **empty** | unchanged; the release policy requires an empty bypass list |

Also confirm on each protected branch's effective rules (Part D, D1) that
there is no "Require linear history" rule (merge commits are required), no
signed-commit or deployment rule, and no other ruleset adding rules. The
`actor_id` 5 is the admin role in the API; check it against the ruleset JSON
GitHub saves, where the UI shows the role by name.

**Force-push means (decision 66).** The means is owner-only: the Repository
admin role, which on this personal repository is only you, is the bypass
actor of RS-2 and RS-3; the app and the machine account never are.

- When: a protected branch must be force-pushed or deleted, for example when
  the trunk has to be rebuilt as wave 2 was on 2026-09-26.
- How: you, signed in as yourself for this task only (A8), run
  `git push --force-with-lease=<branch>:<expected-old-sha> origin <new-sha>:refs/heads/<branch>`.
  The push succeeds only through the bypass. Agents never run it.
- Record: the board gets the date, branch, old and new SHA, reason and
  decision reference; GitHub's rule insights (Settings, Rules, Insights)
  should list the bypassed evaluation with the actor and ref, and the board
  entry is the record if they do not.
- `main` (RS-1k): open question for the owner (see the log). Recommended: no
  bypass actor on `main`, which holds released code; for a true emergency you
  can still, as admin, set RS-1 to Disabled for the push and back to Active.
- If this repository does not offer the Repository admin role as a bypass
  actor, stop and let the commander ask you (see the log for the options).

Not part of G0: the branch-name allowlist and automatic branch deletion
(checklist A-5, approved item by item under decision 24); if you approve the
allowlist later, it must let the agent identity create `feature/*.*.*/**`.
Unchanged: the protected `release` environment with you as the required
reviewer, the Codex review app, and the read-only default workflow token.

## Part D: verification

Destructive tests run only on disposable branches that match the protected
patterns, never on `main`, `1.1.x` or a real release branch. An agent records
each result with the message GitHub returns, so that each block can be traced
to its rule, in the WS-GOV log; you confirm.

- [ ] **D1 Formal refs by snapshot.** An agent reads the effective rules of
      `main`, `1.1.x` and, if present, a release branch
      (`GET /repos/<owner>/<repo>/rules/branches/<branch>`) and compares every
      rule with Part C; you compare the bypass lists, visible only to an
      admin, in the UI.
- [ ] **D2 Disposable branches.** An agent creates `9.9.x` (trunk pattern) and
      `9.9.9` (release pattern) from the trunk head, as the app. Then, each
      with the rule named in GitHub's message: a direct push to `9.9.x` is
      rejected (pull request required); a force push to `9.9.x` is rejected
      (force push blocked); deleting `9.9.x` is rejected (deletion
      restricted); a force push to `9.9.9` is rejected.
- [ ] **D3 App identity through real writes.** The agent pushes
      `feature/1.1.13/g0-check` (a one-line change under `docs/handoff/`)
      through the helper and opens a pull request into `9.9.x`: the pull
      request author and the pusher in its timeline are `<app-slug>[bot]`,
      and `gh api /installation/repositories` through the wrapper succeeds
      (only an installation token can call it) and lists only this
      repository. A successful public read alone proves no identity.
- [ ] **D4 Approval behavior.** You approve the head in the browser. The agent
      pushes a commit that changes the diff: the approval is dismissed. You
      approve again; the agent pushes a new SHA with the same tree (an empty
      commit): record whether GitHub keeps or dismisses the approval. Either
      result is acceptable in G0, because the exact-head rule belongs to G1-A.
      An unresolved review thread blocks the merge until it is resolved.
- [ ] **D5 Positive path.** With your approval on the current head, all
      required checks green and every thread resolved, the pull request
      merges into `9.9.x` (you press Merge, or tell the agent to).
- [ ] **D6 Owner bypass.** You force-push `9.9.x` with the bypass, as in
      Part C, and then delete it the same way; both succeed and are recorded.
- [ ] **D7 Cleanup.** The agent deletes `9.9.9` (deletion allowed) and closes
      anything left open; the agent records all results and messages in the
      log.

## Rollback

- [ ] GitHub: set RS-2 and RS-3 to Disabled (or delete them), restore RS-1
      from the saved JSON and screenshots, and uninstall or suspend the app
      (or remove the machine account's access).
- [ ] Git settings: an agent restores the saved non-secret settings: removes
      the helper entries (`git config --unset-all
      credential.https://github.com.helper`), restores `user.name`,
      `user.email` and any `credential...username` from the backup, and stops
      using the `gh` wrapper.
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
  pull request from `main` into `1.1.x`, merged with a merge commit, only when
  both checks pass on GitHub's test merge of that pull request
  (`refs/pull/<n>/merge`), which has the same parents and tree as the merge
  GitHub will create:
  1. its tree equals both parent trees (`git rev-parse <m>^{tree}`,
     `<m>^1^{tree}` and `<m>^2^{tree}` are one value);
  2. the history gate in force passes with that commit as `HEAD`
     (`python scripts/verify.py --structure-only`; CI checks the pull
     request's head, not the merge).
  After the merge the commander repeats check 1 on the real merge commit and
  the structure check on the new trunk head. If either check fails, the
  commander stops and asks you; the pull request is not merged.
- Releases: you approve the app-authored release pull request normally, so
  you dispatch `release.yml` with `owner_self_approval_exception` set to
  `false`. The exception and the Codex-only review rule stay in the code until
  their product-neutral replacement is built and tested (decision 49, release
  batch R-3).
- The record gate keeps running until G1-B (decision 50).
