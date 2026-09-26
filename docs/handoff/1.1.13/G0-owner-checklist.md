# G0 owner checklist: agent GitHub identity and rulesets

Status: draft for the owner, 2026-09-26, for the independent re-review. It
implements board decision 49 (agents get their own GitHub identity, created
by the owner) and the G0 step of board decision 50. Design:
[governance ADR draft](ADR-DRAFT-governance-reset.md), items 7 to 9; log:
[WS-GOV](WS-GOV.md). Nothing here is done until the owner does it.

`<owner>/<repo>` below stands for this repository's full name, as listed in
[`agent-issue-tracker.md`](../../governance/agent-issue-tracker.md).

## What G0 changes, and who does what

After G0, agents push branches and open pull requests under their own GitHub
identity, and you approve those pull requests as an ordinary reviewer.
GitHub then binds each approval to the exact commit and drops it when new
commits arrive. The rulesets make that approval, the checks and resolved
review threads mandatory on `main`, the trunk and release branches.

| Who | Does | Never does |
| --- | --- | --- |
| Owner (you) | Creates the GitHub App (or the fallback machine account), installs it, stores its private key, sets up the token helper, changes every setting and ruleset, approves and merges | - |
| Agents | Prepare this checklist and non-secret files, draft the token helper for your review, set non-secret Git settings on request, run read-only checks and the verification pull request; use one-hour installation tokens only through the helper | Create accounts or apps; read, type or store the private key, your credentials or any long-lived token; change settings or rulesets; approve pull requests or environments |

## Before you start

- [ ] No release run is in progress and none is planned during G0.
- [ ] Open pull requests authored by your account are merged or closed (the
      commander lists them). After G0 the rulesets require code-owner
      approval, and you cannot approve a pull request your own account opened;
      agents reopen the unmerged ones under the new identity.
- [ ] The current `main` ruleset (22009240) is saved, so RS-1 can be undone:
      a screenshot, or its API output that an agent fetches read-only.
- [ ] You have 30 to 60 minutes; GitHub settings pages open in a browser
      signed in as you, with two-factor authentication.

## Part A: GitHub App (primary)

### A1. Create the app from the manifest

The manifest asks for the smallest permission set the agents need. It
contains no secret.

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
    "actions": "read",
    "workflows": "write"
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
| `workflows: write` | Push changes to `.github/workflows/` (release batches from R-1 on) | Yes, until R-1; adding it later needs your approval on the installation |

Not requested: administration, secrets, variables, environments, deployments,
issues (the bug ledger lives in the repository, and GitHub issue writes need
separate authorization), members, pages and packages. The webhook is inactive
and subscribes to no event.

Steps:

- [ ] Replace `<owner>` and `<repo>` (and pick another name if GitHub reports
      it taken; at most 34 characters).
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
- [ ] GitHub redirects to the repository page with `?code=...&state=nfc-g0`
      in the address bar. Do not paste that address anywhere: the one-time
      code can be exchanged for the app's credentials for an hour. You do not
      need it, because A3 generates the private key in the app settings; the
      code then expires unused.

### A2. Install it on this repository only

- [ ] On the app page, choose **Install App**, then your account.
- [ ] Choose **Only select repositories**, select this repository, install.
- [ ] Note the installation ID from the address bar
      (`.../settings/installations/<id>`).

### A3. Generate and keep the private key

- [ ] App settings, **Private keys**, **Generate a private key**. The browser
      downloads a `.pem` file.
- [ ] Move the key into the store you choose, then delete the downloaded file:
  - a password manager with a command-line reader;
  - a file encrypted for your Windows account (DPAPI), in your user profile
    outside every repository and worktree;
  - Windows Credential Manager, if the tool you use stores the key as UTF-8:
    a stored secret is limited to 2,560 bytes, a GitHub App key is about
    1.7 KB of text, and tools that store text as UTF-16 double it.
- [ ] If the key ever leaks, delete it on the same page and generate a new
      one; uninstalling the app cuts access at once.

### A4. Non-secret values

- [ ] Keep these in the token helper's local configuration (not in Git):
      App ID and Client ID (app settings, "About"), installation ID (A2), the
      bot login `<app-slug>[bot]` and its user ID (agents can look this up
      through the public API).

### A5. Token helper: short-lived tokens without the key

Agents never read the key. A helper that you install and configure reads it
in memory and hands out only installation tokens, which expire after one hour.

- [ ] Ask an agent to draft the helper script. The draft contains no secret;
      you review it and install it in a folder outside every repository and
      worktree. Its contract:
  - read the key from your store (A3) into memory only; never print or log it;
  - sign a JSON Web Token (RS256; issuer = Client ID; issued 60 seconds in
    the past; expires within 10 minutes);
  - call `POST /app/installations/<installation-id>/access_tokens`, limited
    to this repository and to the A1 permissions, and return only the token;
  - for Git, answer the credential-helper `get` request with
    `username=x-access-token` and `password=<token>`, and ignore `store` and
    `erase`;
  - for `gh`, a wrapper sets `GH_TOKEN` from the helper for one `gh`
    invocation.
- [ ] Wire Git to the helper for this repository (all worktrees share this
      setting; an agent may run these on your request, since they contain no
      secret):

      ```text
      git config --replace-all credential.https://github.com.helper ""
      git config --add credential.https://github.com.helper "!<helper command> git"
      ```

      The empty first entry stops Git from falling back to your stored
      credentials for this repository.
- [ ] Agents call `gh` only through the wrapper. Most `gh pr`, `gh run` and
      `gh api` commands work with an installation token; commands that need a
      user account may not, which Part D checks.

### A6. Agent commit identity

- [ ] With your approval, an agent sets, for this repository:
      `user.name` = `<app-slug>[bot]` and `user.email` =
      `<bot-user-id>+<app-slug>[bot]@users.noreply.github.com`. Agent commits
      keep their co-author lines.

### A7. Keep your own credentials away from agent sessions (recommended)

Agents run under your Windows account, so anything stored for your GitHub
account on this machine is within their reach, and decision 49 would rest on
instructions alone.

- [ ] Sign your account out of `gh` on this machine
      (`gh auth logout --hostname github.com --user <your-login>`).
- [ ] Remove your GitHub entry from Git Credential Manager (Windows
      Credential Manager, the `git:https://github.com` entries), or keep it
      only in a separate Windows account you use for yourself.
- [ ] Do owner actions in the browser: approvals, merges, release dispatch
      and approval, settings.

## Part B: machine account (fallback)

Use this only if the app does not work for you. GitHub's terms allow a
machine account that you control and use only for automation.

- [ ] Create the account in a private browser window, with its own email
      address and two-factor authentication.
- [ ] Repository settings, **Collaborators**: invite it with the **Write**
      role (not Maintain or Admin); accept the invitation as that account.
- [ ] Token: GitHub may not let a fine-grained token reach a repository owned
      by another personal account; if the repository is not offered, create a
      classic token with only the `repo` and `workflow` scopes and an expiry
      (for example 90 days). You type it yourself, in your own terminal, into
      the Git Credential Manager prompt or `gh auth login --with-token`; never
      into an agent session.
- [ ] For this repository set `credential.https://github.com.username` to the
      machine account's login, so Git Credential Manager picks that account,
      and set the commit identity to its noreply address.
- [ ] Do A7 as well. Differences from the app: a long-lived token instead of
      one-hour tokens, and a second account with its own email and
      two-factor device to maintain.

## Part C: ruleset changes, each for your approval

Settings, **Rules**, **Rulesets**. UI labels first, API names in
parentheses. The required check names stay as today until G2.

| ID | Ruleset and target | Change | Value |
| --- | --- | --- | --- |
| RS-1a | `main` (existing ruleset 22009240), `refs/heads/main` | Require a pull request before merging (`pull_request`) | on |
| RS-1b | same | Required approvals (`required_approving_review_count`) | 1 |
| RS-1c | same | Dismiss stale pull request approvals when new commits are pushed (`dismiss_stale_reviews_on_push`) | on |
| RS-1d | same | Require review from Code Owners (`require_code_owner_review`) | on |
| RS-1e | same | Require approval of the most recent reviewable push (`require_last_push_approval`) | on |
| RS-1f | same | Require conversation resolution before merging (`required_review_thread_resolution`) | on |
| RS-1g | same | Allowed merge methods (`allowed_merge_methods`) | merge only |
| RS-1h | same | Block force pushes (`non_fast_forward`); restrict deletions (`deletion`) | on; on |
| RS-1i | same | Bypass list | empty |
| RS-1j | same | Required status checks (`required_status_checks`): `policy / polytail`, `python-worker / verify`, `dotnet / build-test`; branches up to date (`strict_required_status_checks_policy`) | unchanged; on |
| RS-2 | new ruleset "trunk", include `refs/heads/*.*.x` (`1.1.x` and later trunks) | RS-1a to RS-1i; the same three required checks, branches up to date off (the release pull request into `main` keeps it on) | as listed |
| RS-3 | new ruleset "release branches", include `refs/heads/*.*.*`, exclude `refs/heads/*.*.x` | RS-1a to RS-1g and RS-1i; the same three required checks, up to date off; block force pushes on; deletion **allowed** (release closure deletes the branch after its tag) | as listed |
| RS-4 | tag ruleset, `refs/tags/v*` (existing) | confirm only: update and deletion restricted, bypass list empty | no change |

Notes:

- RS-1d with today's CODEOWNERS (`*` owned by you) means you approve every
  pull request. G1-A replaces CODEOWNERS with the file derived from the
  authority map, which narrows that to the governed and R3 paths.
- The release policy compares the required checks of `main` with its fixed
  list, so RS-1j must stay unchanged until G2, and RS-4 must stay as it is.
- RS-2 stops every direct push to the trunk, a fast-forward included. Until
  G1-B, board decision 23 still asks for the trunk to catch up with `main`
  after a release: do it with a pull request from `main` into the trunk and a
  merge commit. While the trunk still equals the released head (no commits of
  its own and no release fix it lacks), that merge has the same tree as both
  parents, and the history audit lists no changed path for it. Otherwise wait
  for G1-B or ask the commander before merging.
- Not part of G0: the branch-name allowlist and automatic branch deletion
  (checklist A-5, approved item by item under decision 24); if you approve the
  allowlist later, it must let the agent identity create `feature/*.*.*/**`.
- Unchanged: the protected `release` environment with you as the required
  reviewer, the Codex review app, and the read-only default workflow token.

## Part D: verification, then report

An agent runs these with the new identity and records the results in the
WS-GOV log; you confirm.

- [ ] A branch `feature/1.1.13/g0-check` with a one-line change under
      `docs/handoff/` is pushed, and its pull request into `1.1.x` shows the
      app (or machine account) as author.
- [ ] Your approval appears; a further commit dismisses it.
- [ ] A direct push to `1.1.x` and a force push to `1.1.x` are rejected.
- [ ] An unresolved review thread blocks the merge.
- [ ] `git config --get-all credential.https://github.com.helper` shows the
      empty reset entry followed by the helper, and nothing else;
      `gh api repos/<owner>/<repo>` works through the wrapper; your own account
      is not reachable from an agent session (A7).
- [ ] The test pull request is closed without merging and its branch deleted.

## Rollback

Set the new rulesets to **Disabled**, revert RS-1 to its previous values,
and uninstall or suspend the app (or remove the machine account's access).
Until G1-B the old record gate still runs, so this returns the repository to
today's state.

## After G0

- Agents push and open pull requests under the new identity; you approve
  and merge in the browser, one action at a time (decision 24). Integration
  into `1.1.x` and release branches happens only through pull requests.
- Releases: you approve the agent-authored release pull request normally, so
  you dispatch `release.yml` with `owner_self_approval_exception` set to
  `false`. The exception and the Codex-only review rule stay in the code until
  their product-neutral replacement is built and tested (decision 49, release
  batch R-3).
- The record gate keeps running until G1-B (decision 50).
