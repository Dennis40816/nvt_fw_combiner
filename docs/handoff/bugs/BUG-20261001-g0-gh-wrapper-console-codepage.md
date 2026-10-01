# BUG-20261001-g0-gh-wrapper-console-codepage: the G0 gh wrapper decodes gh output with the console code page

Status: fixed (eighth G0 version reviewed on #504, merged into `1.2.x` as `6f2e2cfa2`, installed and verified on
2026-10-01; see WS-GOV "G0 eighth version installation")
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
Resolution: pending. The proposed fix copies `gh` output as raw bytes and redacts the token bytes (no
decoding, so neither the code page nor a byte order mark can hide the token), refuses `gh` arguments that do
not start with the command and the `gh alias`/`gh extension` commands, checks `-Owner`/`-Repo`
case-sensitively with `\z`, and accepts only a printable ASCII token; five new offline cases cover them
(69/69 through the runner). The first proposal (`8f327e7b8`, UTF-8 decoding) was revised after its
independent review (accept-with-changes, six P3). It closes when the owner installs the reviewed version and
the commander's A6b step 4 check is recorded.
