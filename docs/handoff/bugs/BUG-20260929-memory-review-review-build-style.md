# BUG-20260929-memory-review-review-build-style: Correction build initially fails analyzer and test declarations

Status: fixed
Severity: P3
Found: 2026-09-29, independent review and Codex gpt-6-astra correction,
at feature/1.1.15/memory-layout over 8010d7770.
Where: MemoryLayoutProjector.Pending.cs / MemoryCoverageGroupContractTests.cs / MemoryLayoutProjectorTests.SectionParents.cs
Observed: New test/source edits initially fail IDE0055/IDE0048, a missing XML comment, an unnecessary using and an omitted fixture argument.
Expected: The repository builds with analyzers and nullable checks enabled.
Evidence: review-p1-green.log, review-p1-build2.log, review-parent-red.log. Test artifacts are under
`evidence/1.1.15/test-results/`; see the correction checkpoint in WS-MEMLAYOUT.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Formatting, parentheses and test declarations were corrected without suppressions. These builds executed no tests; runtime retry counts are recorded separately. Included in the correction commit containing this record.

The later bilingual non-terminal card fixture initially omitted its Domain
namespace import (review-nonterminal-red.log). Adding the import allowed the
two intended production failures to run in review-nonterminal-red.trx.

The final staged diff check caught extra blank lines at EOF in eight new bug
records that the earlier unstaged tracked-file check had not covered. The
records were normalized and the complete staged check rerun before commit;
product and test source did not change.
