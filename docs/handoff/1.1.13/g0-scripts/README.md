# NFC G0 owner scripts

The reviewed source of these scripts is kept in the repository for review and traceability; the owner runs a verified copy installed outside every repository, worktree and AppData tree, and credentials and key files always stay outside the repository. They implement the G0 owner checklist (A1–A6, C1, and rollback) under owner decisions 65, 80, and 82. Review them independently before live use. The owner performs App creation, conversion, key storage, App installation, ruleset changes, and credential configuration. After the owner installs the token helper and `gh` wrapper, an agent may use those installed interfaces for authorized Git and `gh` operations as the App. The agent must not read a private key, DPAPI file, Bitwarden vault, credential store, or token.

## Requirements and operators

Run on **Windows with PowerShell 7.4 or later** (`pwsh`). Before opening the App form, the setup script checks required Windows/.NET APIs, a new DPAPI destination in an existing directory, the Bitwarden CLI, an unlocked `BW_SESSION`, and detectable recording policy. The owner must confirm that the process has no manual transcript, screen recorder, or other recording method the script cannot detect. A detected script block, module, or transcription policy stops the secret handling flow; there is no confirmation override. `BW_SESSION` is sensitive and belongs under owner control. The script does not request the Bitwarden master password.

| Step and file | Operator → GitHub identity | Purpose |
| --- | --- | --- |
| `New-NfcGitHubApp.ps1` | owner → owner in the browser; conversion uses GitHub's manifest code | Create the App and save its private key in both owner stores. |
| App installation in GitHub UI | owner → owner | Select only the target repository and record the installation ID. |
| `Set-NfcRulesets.ps1`, `rulesets/*.json` | owner → owner through authenticated `gh` | Back up, inspect, approve, and apply rulesets; restore only this transaction's changes if needed. |
| `nfc-app-token-helper.ps1` | agent → App, after owner installation | Read the DPAPI copy, request a repository-scoped installation token, and answer a matching Git credential `get` request. |
| `Invoke-NfcGh.ps1` | agent → App, after owner installation | Run one `gh` subprocess with an installation token in that subprocess's environment. |
| `open-pr.ps1` | agent → App, after owner installation | Open a pull request through the wrapper, verify its author, and close it if the author does not match or cannot be read. |
| `NfcG0.Common.ps1`, `tests/` | no GitHub identity | Shared functions and offline tests with fake secrets. Run them with `pwsh -NoProfile -File tests/Invoke-NfcG0Tests.ps1`, which starts the suite in its own process without GitHub, Git credential or Bitwarden variables and with the test-area temp folder; the suite stops if such a variable is present. The runner supports only Pester 3.4.0, whose failure count includes setup, cleanup and block failures; any other requested version exits with 2. It exits with 0 only when Pester 3.4.0 ran at least one test and none failed, 1 when a test or block failed, and 2 when the run itself failed (another, missing or unloadable Pester version, no valid result, or no executed test). |

The same-Windows-user separation in decision 65 is **a rule, not a technical security boundary**. A process under that user may technically access the DPAPI file, an unlocked vault, the owner's browser session, Git settings, or the helper. These scripts cannot enforce isolation between such processes.

## 1. Create and install the App

First complete the checklist's “Before you start” items, including a private backup of the existing main ruleset and Git configuration. Use an owner-controlled, unrecorded terminal. Choose an existing private directory and a **new** DPAPI filename outside any repository or worktree. Unlock Bitwarden yourself and make `BW_SESSION` available only to this terminal. Install the Bitwarden CLI.

```powershell
pwsh -NoProfile -File .\New-NfcGitHubApp.ps1 -Owner OWNER -Repo REPO -AppName nfc-agent-OWNER -KeyStore Both -DpapiPath 'OWNER_CHOSEN_PATH\app-key.dpapi'
```

`Both` is the default and required setting for a live run (decision 82). The DPAPI file is for the helper's everyday use; the Bitwarden secure note is the backup. Although the parameter still accepts `Dpapi` and `Bitwarden`, a live run with either value stops before the form opens. The helper reads only DPAPI. The owner reviews the local form and personally clicks **Create the agent GitHub App**. The script listens only on `127.0.0.1` at a random port. It serves the form at a random path, accepts a strictly checked callback at `/callback` with the expected random state once, then stops the listener before conversion. HTTP input has a total byte limit, a header count limit, and a deadline; an invalid connection is rejected while the listener continues waiting. The form path and loopback listener do not authenticate the owner.

For a local preview, `-DryRun` writes `nfc-manifest.dry-run.json` and `nfc-manifest.dry-run.html` to the current directory using a placeholder port. It **does not start a listener**, open the browser, convert the manifest, or store a key. Keep these files out of the repository.

Before sending conversion, the script records that the request is about to be attempted. If the response is lost, times out, or cannot be validated, the outcome is unknown: the owner must check GitHub for an App and key, revoke or clean up anything created, and only then decide whether to create a new App. Do not retry conversion directly. After a confirmed conversion, the script saves DPAPI first and Bitwarden second, then prints the non-secret App ID, Client ID, slug, and installation URL. If conversion succeeds but DPAPI storage is not confirmed, the owner must revoke or delete the App key before restarting. If DPAPI succeeds and Bitwarden fails or its result is unknown, the script stops and identifies the DPAPI path; the owner must inspect the vault. If the Bitwarden copy is absent, the owner may use a separately reviewed local recovery step to copy the DPAPI key into Bitwarden, or remove the DPAPI file and revoke/delete the App key before restarting. A Bitwarden timeout does not prove that no item was created. Do not print, log, or place the PEM in a worktree.

The owner then clicks **Install App**, selects **Only select repositories**, chooses only `OWNER/REPO`, and records the installation ID. The owner keeps the App ID, Client ID, slug, installation ID, and chosen DPAPI path in owner-controlled local configuration. App creation does not change Git configuration.

## 2. Review and apply rulesets

The owner first checks the GitHub UI to decide whether this personal repository offers `Repository admin` bypass, then passes `-AdminBypassAvailable Yes` or `No`. The script does not make that policy decision. `No` produces empty branch bypass arrays; use the checklist C3 pause procedure when a protected-branch force push is necessary. Authenticate `gh` as `OWNER` and use a new backup directory for every apply attempt.

```powershell
pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -BackupDirectory 'NEW_OWNER_BACKUP_DIRECTORY' -AdminBypassAvailable Yes -WhatIf
pwsh -NoProfile -File .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -BackupDirectory 'ANOTHER_NEW_BACKUP_DIRECTORY' -AdminBypassAvailable Yes
```

Before any remote write, the script reads all ruleset pages up to its safety limit, exports repository settings and each listed ruleset, rechecks the list, validates the existing main checks and templates, rejects name collisions for RS-2/RS-3, and confirms the active RS-4 tag ruleset. It copies the three saved check contexts and integration IDs into RS-2 and RS-3; the main required checks and branch-creation policy retain their existing values. RS-4 is **read only**: its active state, tag target, complete include/exclude patterns, update/deletion rules, and empty bypass must match. A mismatch stops the run; the script never repairs RS-4 automatically.

Proposed fields and rules are displayed for individual owner approval. In a live apply, every `YES` approval, including the unchanged RS-4 confirmation, occurs before the first remote write; a refusal stops without sending a write. The script rechecks the complete writable state of every relevant ruleset and the ID list after approval and before each write. It verifies the readback against the requested body before marking a change applied. A mismatch remains pending and stops the transaction for owner reconciliation. `-WhatIf` still reads GitHub and writes a local backup, but sends no remote write and asks for no item approvals. The apply records each pending, completed, or uncertain write in `index.json`, including IDs actually created and verified post-write snapshots. If a request or snapshot fails, the owner must inspect the transaction record and live GitHub state before proceeding or restoring; a failed client call does not prove that GitHub made no change. The script does not automatically roll back a partial apply. **No one may modify repository rulesets concurrently during the owner's apply or restore maintenance window.** The read, write, and readback sequence is not atomic.

## R41: update existing approval fields (2026-10-01, pending owner trial)

The original G0 creation procedure above is retained for its historical setup;
do not rerun it to modify existing RS-2/RS-3. R41 changes the three branch
templates to zero general approvals, keeping code-owner review, stale dismissal
and last-push approval true. RS-4 is unchanged. The new inventory is appended
in [checklist C1a](../G0-owner-checklist.md#c1a-r41-approval-scope-trial-apply-and-rollback-2026-10-01);
older hash/acceptance tables remain evidence for their recorded versions.

Only the owner operates this mode, using a confirmed list of existing ruleset
IDs from the live UI/API and a new private backup directory per attempt.
In an owner PowerShell 7.4+ session, replace the example IDs with confirmed IDs:

```powershell
$confirmedIds = @(CONFIRMED_RULESET_ID_1, CONFIRMED_RULESET_ID_2)
& .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -UpdateApprovals -RulesetIds $confirmedIds -BackupDirectory 'NEW_PREVIEW_BACKUP' -WhatIf
& .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -UpdateApprovals -RulesetIds $confirmedIds -BackupDirectory 'NEW_APPLY_BACKUP'
& .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -Restore -BackupDirectory 'NEW_APPLY_BACKUP' -WhatIf
& .\Set-NfcRulesets.ps1 -Owner OWNER -Repo REPO -Restore -BackupDirectory 'NEW_APPLY_BACKUP'
```

`-UpdateApprovals` requires positive unique IDs and active branch rulesets
with one PR rule and one required-check rule. It does not require three checks
or copy main's checks. It backs up all live bodies, shows each confirmed ID's
complete before/after body and four fields for `YES` approval before any write,
then patches only `required_approving_review_count`, `require_code_owner_review`,
`dismiss_stale_reviews_on_push`, `require_last_push_approval`. It preserves
each target's live name, scope, checks and integration IDs, check options,
bypass, thread/merge options and other rules; no POST or tag write occurs.
Do not pass `-AdminBypassAvailable` or combine update with restore. A changed
list/body during confirmation or between writes stops the transaction.
Failed/mismatched readback stays pending; unknown outcomes require owner
reconciliation before restore. This is a maintenance window, not an atomic API.

For an approval update, `-Restore` writes each updated ID's exact backed-up
before body, including its original approval values; it never disables an
existing ruleset. It checks the current body against the recorded after body
before confirmation and again before writing, then checks the restore readback.
Initial G0 transactions still restore main and disable only IDs they created.

Trial on an isolated target with the candidate policy/CODEOWNERS/checker/W1
on its base before touching formal targets. Prove R1/R2 COMMENT-record plus
green-CI auto-merges without an owner APPROVED review, and R3 blocks until
owner approval on the exact last push names its roles. Record edits/dismissals
must trigger the same required authority context without body edits; missing,
rejecting, incomplete or stale records and missing/failed/cancelled checks
block. If last-push true still requires an approval for R0–R2, stop and obtain
a new owner decision before changing it. Preserve candidate SHA, trial IDs,
before/after JSON, review/run IDs, check SHA/source, merge state/commit and
no-bypass evidence. Only switch a formal target whose base has the amended
authority files; leave unswitched targets protected. Release environment stays.

Enable `--auto --merge --match-head-commit <head>` only after live record/check
verification and any required R3 approval snapshot. The guard checks the head
at the call. Cancel auto-merge before subsequent pushes or authority/record
edits and repeat verification. Trial failure leaves formal rules unchanged.
After a formal-cutover problem, cancel auto-merge, restore the saved live body
and read back effective rules, then use an R3 revert PR for repository changes.
Keep required CI, RS-4 and the release environment; never disable a trunk
ruleset as a substitute for restore.

## 3. Install the daily-use interfaces

The owner reviews and installs the helper and wrapper outside the repository. For repository Git credential configuration, preserve a private copy of the old settings, then set the helper and `useHttpPath` for `https://github.com`. Replace `HELPER_COMMAND` with the reviewed helper's full invocation, including `-Owner`, `-Repo`, `-ClientId`, `-InstallationId`, and `-DpapiPath`. Do not put a token or PEM in the command.

```text
git config --local --replace-all credential.https://github.com.helper ""
git config --local --add credential.https://github.com.helper "!HELPER_COMMAND"
git config --local credential.https://github.com.useHttpPath true
```

`HELPER_COMMAND` has the form `pwsh -NoProfile -File "FULL_PATH_TO_HELPER" -Mode git -Owner OWNER -Repo REPO -ClientId CLIENT_ID -InstallationId INSTALLATION_ID -DpapiPath "OWNER_CHOSEN_PATH" [-IncludeWorkflowsWrite] [-IncludeIssuesWrite]`. Git appends `get`, `store`, or `erase`. The helper only answers `get` for an exact HTTPS GitHub request whose path is `OWNER/REPO` or `OWNER/REPO.git`. A missing, blank, or different repo path exits without reading the DPAPI store or returning credentials. `store` and `erase` do not save anything. Git receives the credential response through its helper protocol; that protocol contains the token, so the agent must not invoke or capture the helper directly. Tokens are not cached on disk.

By default, every token requests the fixed A1 permission set for the one repository. `-IncludeWorkflowsWrite` adds `workflows: write` to that one token and changes nothing else; without it the helper never requests `workflows`. `-IncludeIssuesWrite` adds `issues: write` to that one token only, is never implied, and is independent of `-IncludeWorkflowsWrite`; without it the helper never requests `issues`. It is used by the NFH project only; NFC calls do not pass it and NFC's bot permissions stay unchanged. The owner must grant Issues read/write in that App's settings and accept it on the installation before using the switch; it only requests a permission the App already has. New Apps retain the least-privilege A1 manifest set. The helper refuses a reply that is not limited to `OWNER/REPO`, that goes beyond the requested set or carries a permission at a different level, that lacks `workflows: write` or `issues: write` when it was requested, or whose token is not printable ASCII without spaces, so a reply cannot add lines to the Git credential response. It checks `OWNER` and `REPO` case-sensitively and to the end of the value before it reads the DPAPI file. As in the reviewed version, a reply that omits one of the other requested permissions is accepted; the call that needs the missing permission then fails at GitHub, and the owner compares the App's permissions with checklist A1. Use `-IncludeWorkflowsWrite` only after the owner has granted `workflows` to the App and accepted it on the installation, and only for an owner-authorized batch that writes `.github/workflows/`; for Git, the owner adds it to the configured helper command for that batch window and removes it afterwards. The Git setting is shared by every worktree of the repository, so every Git token issued in that window carries `workflows: write`; removing the switch affects only tokens issued afterwards and revokes none already issued.

For `gh`, the agent invokes the owner-installed wrapper as its own process: `pwsh -NoProfile [-NonInteractive] [-NoLogo] -File "FULL_PATH_TO_WRAPPER" -Owner OWNER -Repo REPO -ClientId CLIENT_ID -InstallationId INSTALLATION_ID -DpapiPath "OWNER_CHOSEN_PATH" [-IncludeWorkflowsWrite] [-IncludeIssuesWrite] [--] GH_ARGUMENTS`. The wrapper has no PowerShell parameter block. It reads its own process command line: it requires `-NoProfile`, accepts only `-NonInteractive` and `-NoLogo` besides it, and the first execution mode must be `-File` with the fully qualified path of the wrapper itself, so neither a profile nor the working directory takes part. It also requires that the process invoked the wrapper directly: no other script, profile or command may be on its call stack. `-Command`, another script (including one with the same name that changes the working directory), a relative `-File` path, a missing `-NoProfile`, any other start-up option, dot-sourcing or a call with `&` stops it with exit code 64 before the helper runs; it never searches later arguments for another `-File`. It then takes its own options up to `--` or up to the first argument that does not begin with `-`, and passes every remaining argument to `gh` unchanged, so PowerShell no longer binds `gh` options such as `--repo`, `-R` or `-v` or splits arguments such as `--head=owner:branch`. The `gh` arguments must begin with the `gh` command (see below), so `--` before them is optional. It passes `-IncludeWorkflowsWrite` and `-IncludeIssuesWrite` to the helper only when each switch is among its own options.

Before it requests a token, the wrapper also refuses common repository options early: it stops with exit code 64 and a message naming `OWNER/REPO` when a `--repo`/`-R` value or `GH_REPO` names anything other than exactly `OWNER/REPO`, when a group of short options contains `-R`, or when its own options are missing, repeated, unknown or malformed. It also refuses, with exit code 64, `gh` arguments that do not start with the `gh` command (global options such as `--help=false` before it), and `gh alias` and `gh extension` (and `ext`, `extensions`) as that command, because alias shell commands and extensions run as children of `gh` with the token. This is an early refusal of common options, not a limit on what `gh` does. It does not inspect `gh api repos/OTHER/...` paths, GraphQL queries, `gh repo` positional repository arguments or the repository `gh` derives from the working directory; `GH_HOST` or any other API host or URL selection; or an alias or extension that already exists and is invoked by its own name, other child processes, and whichever `pwsh` or `gh` executable `PATH` selects. The installation token authorizes this one repository only, but that does not prove that each request targets it, and a request to another host may use a different login. The wrapper therefore does not technically guarantee the App identity or the destination host of a call. Use it only with a trusted `pwsh` and `gh` installation, only against GitHub.com (no `GH_HOST` or enterprise host), with no untrusted `gh` alias or extension, and only for authorized commands. The wrapper starts the helper as a separate `pwsh -NoProfile -File` subprocess, privately captures its token and exit status, and supplies `GH_TOKEN` only to a single `gh` subprocess. It does not set the parent process environment. It reads `gh` stdout and stderr as raw bytes, replaces each occurrence of the exact token bytes, and writes the bytes unchanged otherwise, so text in any encoding (such as Traditional Chinese) and binary output pass through whatever the console code page is. This redaction only catches the token printed verbatim by accident. A command can still print it transformed: `--jq` can read environment variables (for example `env.GH_TOKEN` piped through a filter), and an alias, extension or browser setting (`GH_BROWSER`, `gh config set browser`) runs a child process with the token. The wrapper is therefore no defense against a deliberate command; use it only for authorized commands. Its own error messages do not include the token. A child process started by `gh` may inherit `GH_TOKEN`; do not enable debug or recording output that exposes credentials. For example, after owner setup the agent may use the wrapper to run `gh api /installation/repositories` for checklist D3a.

The owner sets the bot commit identity and performs the remaining checklist steps A7–A8 and D1–D7. These scripts do not auto-approve pull requests, create releases, invoke bypass, change account login, or perform the checklist's remote write validation.

## Open a pull request

The owner installs a verified copy of `open-pr.ps1` next to `Invoke-NfcGh.ps1`.
Use the same ClientId, InstallationId and DpapiPath values as for `Invoke-NfcGh.ps1`.
Replace the placeholders, then run this line from the installed scripts directory:

```powershell
pwsh -NoProfile -File .\open-pr.ps1 -Repo OWNER/REPO -Base 1.2.x -Head feature/1.2.5/TOPIC -Title 'fix: describe the change' -BodyFile .\pr-body.md -ClientId '<client-id>' -InstallationId '<installation-id>' -DpapiPath '<dpapi-path>'
```

Add `-Draft` for a draft pull request. The script always verifies the exact NFC
App login `app/nfc-agent-dennis40816`. It rejects `GH_HOST` values other than
`github.com` (ignoring case) before calling the wrapper. It starts the wrapper
with `GH_HOST=github.com` and without `GH_ENTERPRISE_TOKEN` and
`GITHUB_ENTERPRISE_TOKEN`. The script uses the wrapper's default six permissions. It exits
with 64 for a usage error, 0 after verifying the author
(the URL is the last output line), or 1 for a failure. An author mismatch or
unreadable author triggers a close with the reason as a comment. If closing
fails, close the pull request manually before retrying.

## Recovery and revocation

- For a completed ruleset transaction, the owner runs `Set-NfcRulesets.ps1 -Restore -Owner OWNER -Repo REPO -BackupDirectory 'EXISTING_BACKUP_DIRECTORY'`, reviews the proposed changes, and approves each item. `-WhatIf -Restore` only displays the plan. Restore uses `index.json` to act only on this transaction's recorded changes: it restores the saved main ruleset body and disables only the RS-2/RS-3 IDs this run created. It does not modify RS-4 or unrelated rulesets. It compares each live ruleset with its saved post-write snapshot before approval and again immediately before writing, then verifies the restore readback. A pending or unknown write must be reconciled by the owner before restore; after restoring, verify GitHub's effective rules. A prewrite failure can be rerun after the cause is resolved; a write with unknown outcome requires owner reconciliation first.
- For App or key revocation, the owner revokes or deletes the private key in GitHub App settings, suspends or uninstalls the App, or deletes the App. On suspected key exposure, revoke it promptly; uninstalling the App cuts off that installation's access. The owner then removes the DPAPI file and Bitwarden secure note under owner control. Check both copies and GitHub's state after a partial storage failure.
- To undo daily-use setup, the owner restores the repository's previous credential helper, username, `user.name`, and `user.email` from the private backup and stops using the `gh` wrapper. If that backup contains secrets, only the owner handles it. The owner signs in separately for owner CLI work.

PowerShell/.NET strings and conversion objects cannot be guaranteed erased from memory. The scripts clear mutable byte arrays and drop references where possible, but do not protect against memory capture or external recording. The owner must verify the live ruleset JSON, bypass availability, App installation scope, and remaining checklist evidence.
