# BUG-20260929-open-golden-example-alias-crash: the example opener fails on fact-scoped aliases

Status: fixed (2026-10-02): an alias is refused with a clear message
Severity: P3 (developer tool; blocks decision 183 if reused as is)
Found: 2026-09-29, Codex connector review of pull request #484; reproduced by the commander at `2eee1ad31`
Where: `scripts/open_golden_example.py:40` (`artifact`) and `:82` (`case["artifacts"]`)
Observed: `python scripts/open_golden_example.py nt51951-ab-boe-d82t80-workflow-alias --dry-run` ends with
`error: 'artifacts'`. The twelve fact-scoped alias manifests in the canonical selection have no `artifacts`
list; they point to a source case instead.
Expected: the opener either rejects an alias with a clear message or resolves it to its declared source case
while keeping the alias's non-independent evidence label. It never treats an alias as loadable firmware.
Evidence: the command above at `2eee1ad31`; the release canonical selection declares 25 Direct Golden cases,
three input-only cases and twelve aliases (`testdata/golden/release-canonical-v1.json`).

Owner: `scripts/open_golden_example.py` and the decision 183 example drop-down (`1.2.7`).
Resolution: the crash is fixed (see Fix); the decision 183 proposal still decides whether the drop-down lists
aliases at all.
Fix (2026-10-02, Codex `gpt-6.1-sol`; built, run and reviewed by the commander): `launch_args` refuses a manifest that
has no `artifacts` list and declares an `alias` before any artifact is read: `error: <alias id>: alias of <source id>;
open source case '<source id>' instead`, exit code 2, no traceback. It never resolves an alias to loadable firmware.
`--list` still shows aliases with their declared disposition; whether a chooser offers them stays with the decision
183 proposal. Tests: `tests/scripts/test_open_golden_example.py` (the twelve committed aliases, a direct case, an
input-only case, a synthetic alias whose source case is missing, the listing).
