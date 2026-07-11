# Active workflow policy

`ci.yml` is active for pull requests and pushes to `main`. It exposes three stable checks:

- `policy / polytail`
- `python-worker / verify`
- `dotnet / build-test`

`release.yml` is manually dispatched only for an existing approved stable `vX.Y.Z` tag whose commit is reachable from `main`. Development tags such as `v0.1.0-dev.0` never publish a release. `main-package.yml` produces a downloadable package for each `main` push but does not create a stable release.

All external actions are pinned to full immutable commit SHAs. Workflow permissions are least-privilege per workflow, checkout credentials are not persisted, and pull-request jobs receive no release environment secrets. Reviewed source copies are retained under `docs/ci/workflow-templates/` for change review; the executable source of truth remains `.github/workflows/`.
