# Coverage Baseline v1

`coverage-baseline-v1.json` is the checked-in source of truth for the real
coverage baseline used only by `scripts/verify.py`. It records executable line
and branch counts, not test counts.

## Collection

- .NET is collected from every solution test project by the test-only,
  centrally pinned `coverlet.collector` 6.0.4 package as paired Cobertura and
  Coverlet JSON reports. Cobertura supplies physical line evidence; JSON branch
  outcome identities are unioned across test assemblies so complementary hits
  remain distinguishable. Each pair is reconciled before union: every JSON
  branch must belong to a real Cobertura source/class and an owned physical
  production source line (C# or Avalonia source-mapped AXAML),
  every Cobertura-declared branch group must
  have the same JSON covered/total outcome measure, and every JSON report must
  match Cobertura's report-wide covered/total branch measure. The latter
  accounts for Coverlet source-mapped lambda branches that JSON identifies
  even when the corresponding Cobertura line is marked nonbranch. The
  physical-source fallback is required only for compiler-generated methods
  whose branch-only lines Coverlet omits from its Cobertura rendering.
- Python is collected from `tools/crc-worker` by the exactly pinned
  `pytest-cov` 6.3.0 / `coverage.py` 7.14.3 development dependencies in JSON
  format. Its report must enumerate every owned worker Python source file
  exactly once; the closed Hatch build configuration pins the wheel to that
  same single package root without alternate inclusion/mapping mechanisms, and
  alternate coverage configuration and denominator filters are forbidden.
- Both report directories are recreated below ignored `artifacts/coverage/`.
  They are review evidence, not release inputs or source artifacts.

The first baseline uses the stable `v0.9.16` peeled commit
`462590e8b993b8e42d088bc07377571a4bb9f25d`. The collection implementation is
integrated through `0.10.x`, but that change does not alter counted product
source.

## Policy

Every canonical Python or .NET verification run parses its real report and
rejects a decrease in that language's overall line or branch rate. Rates are
compared as integer fractions, so a changed number of executable lines cannot
hide a regression by rounding.

The baseline records every production assembly, so a missing report cannot be
hidden by the aggregate total. Unapproved production
`ExcludeFromCodeCoverage` attributes, repository `.runsettings` files, and
Coverlet include/exclude filter configuration are forbidden because they can
silently shrink the executable denominator. Any future exception is a reviewed
coverage-contract change with real before/after reports; it is not a local
source or project setting. The worker `pyproject.toml` also has one exact
coverage source/report configuration; `.coveragerc`, alternate coverage
sections (including coverage.py's standard `[run]`/`[report]` form),
pytest `addopts` coverage switches, `--cov-config`, noncanonical `--cov`
targets, and coverage RC/process environment overrides are rejected before
worker commands start. Structure validation pins the central
collector version and test-only reference, while the restored .NET gate confirms every
test project receives that exact central reference. The restored inventory is
evaluated after package-analyzer resolution in `Release`, accepts only the
selected SDK's explicit built-in analyzer allowlist and the exact analyzer
assets already supplied by pinned Avalonia, CommunityToolkit.Mvvm, and
Humanizer.Core packages, and treats C# extensions case-insensitively when
measuring changed modules. Every test project's restored asset graph must
resolve the baseline Coverlet version. The Python lane also confirms the active
`coverage.py` and
`pytest-cov` distribution versions before collecting evidence. Coverage report
directories are disposable only when their complete repository-relative path
contains no symbolic-link or junction hop and resolves inside the repository.

For `Domain` and `Application`, the baseline additionally records the nonblank
production-line count. A module is substantially changed at the earlier of 20
changed lines or 10% of its baseline nonblank lines. The change count is
calculated from zero-context Git hunks relative to the fixed baseline: an added,
removed, or substituted physical source line counts once; untracked new C#
files, including blank physical lines, are included. On a substantial change
the module must not regress from its own baseline and must meet at least 85%
line and 80% branch coverage.

This is deliberately not a premature repository-wide 85%/80% fail-under. The
existing baseline remains visible and non-decreasing while the maintainability
program raises coverage where it changes product code.

The non-UI production metric counts physical source files, so it cannot be
reduced by moving compiled C# into an excluded directory. Structure validation
rejects explicit `Compile` and `Analyzer` items. After the canonical .NET lane
restores, its single owner rejects evaluated `Compile` items outside a
production project's owned source tree and source-generating `Analyzer` items
introduced through imported MSBuild or a package; either requires a reviewed
architecture change. The evaluated check fails closed when restored project
assets are absent and never races the structure lane for restore ownership.
Every Python file below the CRC worker's canonical `src` root is likewise
counted, including legitimate `release`, `artifacts`, `bin`, or `obj`
subpackage names; only `.mypy_cache`, `.pytest_cache`, `.ruff_cache`, `.venv`,
`__pycache__`, and `venv` cache/environment directories are omitted.
Generated/cache directories remain forbidden tracked content, and the existing
.NET/Ruff format checks prevent layout-only compression from becoming a metric
escape hatch. Tests are excluded from the size metric, but deleting or weakening
them cannot lower the metric and must still preserve the coverage and existing
repository gates.

## Baseline changes

Changing the JSON is a policy decision, not generated output. A proposed change
must include the real reports, a reviewable explanation of the source or
collector change, focused parser/policy tests, and the canonical verification
gate. It must not be used to mask lost coverage.

The verifier does not rewrite this fixed floor or require every passing
improvement to promote it. Raising the recorded floor is a separate reviewed
policy decision with the same report provenance and no-regression evidence.
