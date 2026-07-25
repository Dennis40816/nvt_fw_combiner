# Coverage Baseline v1

`coverage-baseline-v1.json` is the checked-in source of truth for the real
coverage baseline used only by `scripts/verify.py`. It records executable line
and branch counts, not test counts.

## Collection

- .NET is collected from every solution test project by the test-only,
  centrally pinned `coverlet.collector` 6.0.4 package in Cobertura format.
- Python is collected from `tools/crc-worker` by the exactly pinned
  `pytest-cov` 6.3.0 / `coverage.py` 7.14.3 development dependencies in JSON
  format.
- Both report directories are recreated below ignored `artifacts/coverage/`.
  They are review evidence, not release inputs or source artifacts.

The first baseline uses the stable `v0.9.16` peeled commit
`462590e8b993b8e42d088bc07377571a4bb9f25d`. The collection implementation is
added on `0.10.1`, but that change does not alter counted product source.

## Policy

Every canonical Python or .NET verification run parses its real report and
rejects a decrease in that language's overall line or branch rate. Rates are
compared as integer fractions, so a changed number of executable lines cannot
hide a regression by rounding.

The baseline records every production assembly, so a missing report cannot be
hidden by the aggregate total. For `Domain` and `Application`, it additionally
records the nonblank production-line count. A module is substantially changed
at the earlier of 20 changed lines or 10% of its baseline nonblank lines. The
change count is calculated from zero-context Git hunks relative to the fixed
baseline: an added, removed, or substituted physical source line counts once;
untracked new C# files, including blank physical lines, are included. On a
substantial change the module must not regress from its own baseline and must
meet at least 85% line and 80% branch coverage.

This is deliberately not a premature repository-wide 85%/80% fail-under. The
existing baseline remains visible and non-decreasing while the maintainability
program raises coverage where it changes product code.

The non-UI production metric counts physical source files, so it cannot be
reduced by moving compiled C# into an excluded directory. Repository validation
rejects explicit or evaluated `Compile` items outside a production project's
owned source tree, and source-generating `Analyzer` items introduced directly,
through imported MSBuild, or by a package; either requires a reviewed
architecture change. Evaluated checks require restored project assets and fail
closed when those assets are absent.
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
