# GitHub Governance Instructions

- Workflow, CODEOWNERS, ruleset, permission, secret, release and signing changes require human review.
- Pin third-party actions to reviewed full commit SHAs and retain the release tag in a comment.
- Default permissions are read-only; elevate only on the job that needs them.
- Never run untrusted code with `pull_request_target` or expose release/private firmware secrets to PRs.
- The stable workflow itself must be dispatched from the exact current protected-`main` SHA. Its product source is either that same reviewed `main` SHA or an explicitly owner-approved maintenance branch/version pair at its exact reviewed head. Stable publication runs only after the protected `release` environment creates or verifies an immutable annotated tag for that source SHA; local/manual tags grant no release authority. The only approved independent maintenance pair is currently `0.9.17` / `0.9.17`.
- Keep status-check names stable; add the required `policy / polytail` check when the policy runner is implemented.
