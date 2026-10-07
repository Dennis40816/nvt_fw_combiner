# Active workflow policy

`ci.yml` is active for pull requests and for pushes to `main`, the minor-line trunks (`*.*.x`) and the release
branches (`*.*.*`). Branch protection
retains three stable required checks:

- `policy / polytail`
- `python-worker / verify`
- `dotnet / build-test`

The stable .NET verdict is an always-run finalizer over one full Release-build
producer and the three `bootstrap`, `ui`, and `core` Windows test shards. The
closed project map, expected counters, commands, manifest validation, coverage
aggregation, complete discovery-reconciled GoldenRegression execution, and the
fixture gate remain owned only by
`scripts/verify.py`; the workflow contains no project list or test filter.
Missing or failed producers and missing, extra, duplicate, wrong-SHA,
wrong-SDK, or hash-mismatched evidence fail the finalizer.

`authority.yml` produces the pull request check `governance / authority`
([ADR 0080](../../docs/adr/0080-governance-reset.md), G1-A): one job, run on
every pull request event type that can change the head or the description
(`edited` included), with read-only `contents` and `pull-requests` permissions
and no secret. `scripts/authority_check.py` classifies the change against
`docs/governance/authority-policy.json` and verifies the description's
authority block and the review records on the head; its job summary records the
evaluated head and base, the Git blob IDs of the authority files at both, and
the revision and blob of the checker that ran. The context binds a branch only
after the owner adds it to that branch's ruleset (trunk and release branches in
G1-A, `main` in G2); until then the three checks above stay the required set.
Procedures: `docs/governance/development-execution-workflow.md`, "Authority
check".

`release.yml` is dispatched only from the exact current protected `main` SHA,
which is also the product source. Its candidate validates the merged release
pull request, exact-source CI, fresh Golden execution, the stable version floor,
closed package, smoke, notes, and update-source handoff. The candidate and
pre-tag boundaries both require a version newer than every stable tag; an
existing tag is accepted only for same-run promotion recovery. Every release
from 2.0.0 requires a successful terminal parity chain. The read-only
`release / eligibility` job fails a required chain that failed, was cancelled,
or was skipped, instead of letting promotion silently skip.

The default-off `dry_run` dispatch executes candidate admission and packaging on
protected `main`, marks its manifest `nonPromotable`, and gives its artifact a
`dry-run-candidate` prefix. It has no release-floor eligibility and never runs
promotion or published smoke. Only the approved promotion job receives
`contents: write` and the protected `release` environment. It revalidates
live authority immediately before tag and Release mutations, creates or
verifies the immutable annotated tag, publishes the prepared assets and notes,
and verifies downloaded assets. A separate read-only job smokes the published
package without a GitHub token in its execution step. A failed post-tag
promotion can be rerun only within the same run while protected `main` remains
at the candidate SHA; conflicting immutable Releases require a new version.

`release-rehearsal.yml` is manually dispatched on the selected branch. It uses
the same pinned toolchain setup and stable package path as the release candidate,
then runs release Golden verification, package smoke, notes rendering and the
update-source handoff. Its 3-day artifacts and synthetic handoff timestamp are
build rehearsal evidence only; it has no admission, release environment, tag or
publication authority. Ordinary `main` pushes do not package. Draft
pull requests run policy checks; review-ready PRs and `main` pushes run the
required CI matrix.

All external actions are pinned to full immutable commit SHAs. Workflow permissions are least-privilege per workflow, checkout credentials are not persisted, and pull-request jobs receive no release environment secrets. Reviewed policy templates are retained under `docs/ci/workflow-templates/` for change review; the complete executable source of truth remains `.github/workflows/`.

The CI template is an exact-byte generated mirror, maintained with
`python scripts/sync_derived.py --write --only ci-template-mirror` after approved
CI edits. Do not edit it separately. The default read-only sync check detects
drift before expensive verification. The release template remains a reviewed
security-policy summary, not a generated copy of the executable release workflow.
