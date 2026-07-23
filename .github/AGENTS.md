# GitHub Governance Instructions

- Workflow, CODEOWNERS, ruleset, permission, secret, release and signing changes require human review.
- Pin third-party actions to reviewed full commit SHAs and retain the release tag in a comment.
- Default permissions are read-only; elevate only on the job that needs them.
- Never run untrusted code with `pull_request_target` or expose release/private firmware secrets to PRs.
- A read-only stable candidate may start only from an exact reviewed protected-`main` SHA. Stable publication runs only after the protected `release` environment creates or verifies an immutable annotated tag for that same SHA; local/manual tags grant no release authority.
- Keep status-check names stable; add the required `policy / polytail` check when the policy runner is implemented.
