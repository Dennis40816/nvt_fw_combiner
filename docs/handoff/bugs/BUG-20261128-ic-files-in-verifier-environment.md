# BUG-20261128-ic-files-in-verifier-environment: sandbox cannot admit the external verifier session

Status: fixed
Severity: P2
Found: 2026-11-28, primary Codex session, during the local IC file import at `feature/1.3.0/ic-files-in` based on `c3cc65875552ffa6b869b8f92e14f47722bedab0`.
Where: `python scripts/verify.py --structure-only`, before structural checks.
Observed: The canonical entry point exits 1 with `[WinError 5]` (access denied) while admitting the required external test-area session. The owner-authorized worktree temp directory supports pytest, but does not make the external verifier session writable. PyYAML is also absent; the pinned `PyYAML==6.0.3` install into ignored scratch fails with `[WinError 10013]` because outbound sockets are denied.
Expected: The canonical verifier runs with its existing external test-area custody checks and the declared pinned dependencies. The task-only pytest temp exception must not weaken those checks or turn partial checks into a full pass.
Evidence: The exact entry-point command returned 1 twice (the second capture used UTF-8 diagnostics); `scripts/sync_derived.py` independently reported `No module named 'yaml'`. Both new pytest modules run successfully with explicit worktree `--basetemp`; the isolated checker/formatter clone also passes with site packages disabled.
Owner: host/runtime environment; product fix is not authorized or required.
Resolution: Open for the host rerun in the real external test area with the existing pinned dependencies. The specifically requested report destination is also outside the sandbox write root: writing it raised `PermissionError: [Errno 13] Permission denied`. The complete owner report is retained as `.tmp/report-nfc-ic-files-in.md` for host transfer. No verifier guard, dependency pin or persistent environment setting changed.

Host rerun (2026-11-28 11:1x, PC-NFC, external test area, existing pinned dependencies): `python scripts/verify.py --structure-only` passed (structure=PASS, 28.0 s). The sandbox limit was environmental. No verifier guard changed.
