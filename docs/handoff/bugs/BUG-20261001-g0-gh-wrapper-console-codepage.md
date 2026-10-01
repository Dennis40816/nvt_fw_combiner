# BUG-20261001-g0-gh-wrapper-console-codepage: the G0 gh wrapper decodes gh output with the console code page

Status: open (fix in an eighth G0 version, `1.2.1`; the owner installs it after its own independent review)
Severity: P2
Found: 2026-10-01, independent security review of the seventh G0 version (Claude Opus 5.5) for the G0 landing
pull request (board decision 220), by code reading and a fake-value test.
Where: `docs/handoff/1.1.13/g0-scripts/Invoke-NfcGh.ps1` lines 159-175 (the same code as the sixth version's lines
49-53): the wrapper starts `gh` without setting `StandardOutputEncoding`/`StandardErrorEncoding`.
`docs/handoff/1.1.13/g0-scripts/nfc-app-token-helper.ps1` line 60 (`$` instead of `\z`) and line 92 (the Git
response carries the token without a CR/LF check).
Observed: a `pwsh` started from Git Bash on this machine uses code page 950. `gh` writes UTF-8, so `體` arrives as
the bytes `e9 3f` (corrupted), and in `x體ghs_FAKE123` the byte `0x94` joins `g` into one CP950 character, so the
exact-string token replacement does not match and the fake token passes through unredacted. Agents read
Traditional Chinese pull request text corrupted and may write it back. The helper's line 60 finding fails safe;
line 92 is safe only while GitHub returns a well-formed token.
Expected: the wrapper decodes `gh` output as UTF-8 and forwards it unchanged, the token scrub works on the
decoded text, and a test covers non-ASCII output with a token after a CJK character; the helper input check uses
`\z` and the Git response refuses a token with CR or LF.
Also for the eighth version (P3, exact-head review of the G0 landing pull request, by code reading): consider
refusing the `gh alias` and `gh extension` subcommands in the wrapper. A `gh alias set --shell` alias or an
extension runs as a child of `gh` with `GH_TOKEN` and can print a transformed token that the exact-string scrub
(`Invoke-NfcGh.ps1` lines 165, 172-173) does not catch.
Evidence: the review record in `docs/handoff/1.1.13/WS-GOV.md` ("G0 seventh version acceptance (2026-10-01)") and
the landing pull request's exact-head review (board decision 222).
Owner: unassigned; `1.2.1` batch A (decisions 210 and 217). The installed scripts change only when the owner
installs a reviewed eighth version.
Resolution: pending.
