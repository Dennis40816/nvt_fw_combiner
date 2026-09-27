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
| `NfcG0.Common.ps1`, `tests/` | no GitHub identity | Shared functions and offline tests. |

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

## 3. Install the daily-use interfaces

The owner reviews and installs the helper and wrapper outside the repository. For repository Git credential configuration, preserve a private copy of the old settings, then set the helper and `useHttpPath` for `https://github.com`. Replace `HELPER_COMMAND` with the reviewed helper's full invocation, including `-Owner`, `-Repo`, `-ClientId`, `-InstallationId`, and `-DpapiPath`. Do not put a token or PEM in the command.

```text
git config --local --replace-all credential.https://github.com.helper ""
git config --local --add credential.https://github.com.helper "!HELPER_COMMAND"
git config --local credential.https://github.com.useHttpPath true
```

`HELPER_COMMAND` has the form `pwsh -NoProfile -File "FULL_PATH_TO_HELPER" -Mode git -Owner OWNER -Repo REPO -ClientId CLIENT_ID -InstallationId INSTALLATION_ID -DpapiPath "OWNER_CHOSEN_PATH"`. Git appends `get`, `store`, or `erase`. The helper only answers `get` for an exact HTTPS GitHub request whose path is `OWNER/REPO` or `OWNER/REPO.git`. A missing, blank, or different repo path exits without reading the DPAPI store or returning credentials. `store` and `erase` do not save anything. Git receives the credential response through its helper protocol; that protocol contains the token, so the agent must not invoke or capture the helper directly. Tokens are not cached on disk.

For `gh`, the agent invokes the owner-installed `Invoke-NfcGh.ps1` with its configured App identifiers and the desired `-GhArguments`. The wrapper starts the helper as a separate `pwsh -NoProfile -File` subprocess, privately captures its token and exit status, and supplies `GH_TOKEN` only to a single `gh` subprocess. It does not set the parent process environment. It replaces the exact token if it appears in captured `gh` stdout or stderr before forwarding that output, and its own error messages do not include the token. A child process started by `gh` may inherit `GH_TOKEN`; do not enable debug or recording output that exposes credentials. For example, after owner setup the agent may use the wrapper to run `gh api /installation/repositories` for checklist D3a.

The owner sets the bot commit identity and performs the remaining checklist steps A7–A8 and D1–D7. These scripts do not auto-approve pull requests, create releases, invoke bypass, change account login, or perform the checklist's remote write validation.

## Recovery and revocation

- For a completed ruleset transaction, the owner runs `Set-NfcRulesets.ps1 -Restore -Owner OWNER -Repo REPO -BackupDirectory 'EXISTING_BACKUP_DIRECTORY'`, reviews the proposed changes, and approves each item. `-WhatIf -Restore` only displays the plan. Restore uses `index.json` to act only on this transaction's recorded changes: it restores the saved main ruleset body and disables only the RS-2/RS-3 IDs this run created. It does not modify RS-4 or unrelated rulesets. It compares each live ruleset with its saved post-write snapshot before approval and again immediately before writing, then verifies the restore readback. A pending or unknown write must be reconciled by the owner before restore; after restoring, verify GitHub's effective rules. A prewrite failure can be rerun after the cause is resolved; a write with unknown outcome requires owner reconciliation first.
- For App or key revocation, the owner revokes or deletes the private key in GitHub App settings, suspends or uninstalls the App, or deletes the App. On suspected key exposure, revoke it promptly; uninstalling the App cuts off that installation's access. The owner then removes the DPAPI file and Bitwarden secure note under owner control. Check both copies and GitHub's state after a partial storage failure.
- To undo daily-use setup, the owner restores the repository's previous credential helper, username, `user.name`, and `user.email` from the private backup and stops using the `gh` wrapper. If that backup contains secrets, only the owner handles it. The owner signs in separately for owner CLI work.

PowerShell/.NET strings and conversion objects cannot be guaranteed erased from memory. The scripts clear mutable byte arrays and drop references where possible, but do not protect against memory capture or external recording. The owner must verify the live ruleset JSON, bypass availability, App installation scope, and remaining checklist evidence.
