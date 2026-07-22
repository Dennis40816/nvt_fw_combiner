# Active workflow policy

`ci.yml` is active for pull requests and pushes to `main`. It exposes three stable checks:

- `policy / polytail`
- `python-worker / verify`
- `dotnet / build-test`

`release.yml` is manually dispatched for one explicit, reviewed full `main` SHA and the final merged PR that produced it. Its read-only candidate job uses pinned Python to prove the PR head tree, current-head approval, required checks, merge commit, workflow definition, checkout, and current `main` identity before it verifies, packages, smokes, and stages one closed immutable candidate. The protected `release` environment is the final tag confirmation. Only the approved promotion job receives `contents: write`; a first tag requires the candidate to remain current `main`, while same-run recovery of an exact tag permits later `main` advancement only when the source stays reachable. Promotion creates or exactly verifies the annotated stable tag, publishes the prepared assets and CHANGELOG-derived notes, then revalidates tag/Release metadata and the downloaded release. If promotion fails after tag creation, rerun only the failed promotion job in the same workflow run; zero/one/multi-asset partial states may add only missing matching assets, while tags and conflicting assets are never moved or overwritten. Development or local tags never publish a release.

`main-package.yml` is a manual preview only. Ordinary `main` pushes no longer spend Windows minutes packaging or create fallback prereleases. Draft pull requests run the policy check; the Python and .NET matrices start when the PR becomes review-ready, while all `main` pushes retain the complete CI matrix.

All external actions are pinned to full immutable commit SHAs. Workflow permissions are least-privilege per workflow, checkout credentials are not persisted, and pull-request jobs receive no release environment secrets. Reviewed policy templates are retained under `docs/ci/workflow-templates/` for change review; the complete executable source of truth remains `.github/workflows/`.
