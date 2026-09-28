# GitHub Governance Instructions

- Workflow, CODEOWNERS, ruleset, permission, secret, release and signing changes require human review before integration or application. Authorized local preparation and read-only inspection may proceed; retain approval for the affected external action.
- Pin third-party actions to reviewed full commit SHAs and retain the release tag in a comment.
- Default permissions are read-only; elevate only on the job that needs them.
- Never run untrusted code with `pull_request_target` or expose release/private firmware secrets to PRs.
- The stable workflow starts automatically after the successful `ci` workflow on protected `main`; `workflow_dispatch` is a manual fallback. Both paths must use the exact current protected-`main` SHA as workflow and product source. Stable publication requires approval in the protected `release` environment, which creates or verifies an immutable annotated tag for that source SHA; local/manual tags grant no release authority. The `dry_run` path creates only non-promotable artifacts.
- A release-source checkout, script, or packaged executable must never run in the `contents: write` job. That job uses only protected-main release tooling and GitHub-observed source identity; packaged smoke runs afterward in a separate read-only job without a GitHub token in the execution step.
- Keep the existing required status-check names stable: `policy / polytail`, `python-worker / verify`, and `dotnet / build-test`.
