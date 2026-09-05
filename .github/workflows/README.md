# Active workflow policy

`ci.yml` is active for pull requests and pushes to `main`. Branch protection
retains three stable required checks:

- `policy / polytail`
- `python-worker / verify`
- `dotnet / build-test`

The stable .NET verdict is an always-run finalizer over one full Release-build
producer and the three `bootstrap`, `ui`, and `core` Windows test shards. The
closed project map, expected counters, commands, manifest validation, coverage
aggregation, Golden 17/17 gate, and fixture gate remain owned only by
`scripts/verify.py`; the workflow contains no project list or test filter.
Missing or failed producers and missing, extra, duplicate, wrong-SHA,
wrong-SDK, or hash-mismatched evidence fail the finalizer.

`release.yml` is always dispatched from the exact current protected `main`
workflow definition for one explicit reviewed release-branch head and its final
merged PR. The product source is normally `main`; the approved
`0.9.17` / `0.9.17`, `0.9.18` / `0.9.18`, and `0.9.19` / `0.9.19`
maintenance pairs may publish
independently without merging their product commits into `main`. Its read-only candidate job uses pinned Python
to prove the PR head tree, PR base, current-head approval, required checks,
merge commit, workflow authority, checkout, and selected branch identity before
it verifies, packages, smokes, and stages one closed immutable candidate. The
protected `release` environment is the final tag confirmation. Only the
approved promotion job receives `contents: write`; a first tag requires the
candidate to remain the selected release-branch head and the workflow authority
to remain current protected `main`. Same-run recovery of an exact tag permits
later source-branch advancement only while the source stays reachable.
Promotion creates or exactly verifies the annotated stable tag, publishes the
prepared assets and CHANGELOG-derived notes, then revalidates tag/Release
metadata and the downloaded release. The write-token job checks out only
protected-main authority and never executes candidate code. A subsequent
`contents: read` job downloads the package and runs protected-main smoke tooling
in a token-free execution step. If promotion fails after tag creation, rerun
only the failed promotion job in the same workflow run; zero/one/multi-asset
partial states may add only missing matching assets, while tags and conflicting
assets are never moved or overwritten. Development or local tags never publish
a release.

`main-package.yml` is a manual preview only. Ordinary `main` pushes no longer spend Windows minutes packaging or create fallback prereleases. Draft pull requests run the policy check; the Python and .NET matrices start when the PR becomes review-ready, while all `main` pushes retain the complete CI matrix.

All external actions are pinned to full immutable commit SHAs. Workflow permissions are least-privilege per workflow, checkout credentials are not persisted, and pull-request jobs receive no release environment secrets. Reviewed policy templates are retained under `docs/ci/workflow-templates/` for change review; the complete executable source of truth remains `.github/workflows/`.

The CI template is an exact-byte generated mirror, maintained with
`python scripts/sync_derived.py --write --only ci-template-mirror` after approved
CI edits. Do not edit it separately. The default read-only sync check detects
drift before expensive verification. The release template remains a reviewed
security-policy summary, not a generated copy of the executable release workflow.
