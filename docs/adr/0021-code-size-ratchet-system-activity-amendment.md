# ADR 0021 normative appendix — current-session System Activity

The owner approved the two-level System Activity history on 2026-08-22: one
existing Application-owned System Information service retains a bounded,
privacy-filtered current-session activity list; the default view shows
important events, explicit Debug disclosure adds user operations, and the
Message Center matches the accepted 1,536 by 864 wide reference in Light and
Dark. The former diagnostic-transition list is replaced, not mirrored, and no
new persistence or report-history owner is introduced.

Relative to the exact descending checkpoint immediately before this feature,
full production changes from 109,213 to 109,849 (+636) and runtime changes from
74,937 to 75,135 (+198). Domain plus Profiles remains 20,632; Application
changes from 33,225 to 33,423 (+198); Bootstrap plus CLI plus Desktop host
remains 3,503; and Infrastructure plus Contracts plus CRC worker remains
17,577. The remaining +438 is Presentation XAML/localization. The Application
delta includes fail-closed validation of the activity disclosure/category/
severity vocabulary so exported diagnostics cannot contain undefined enum
values.

The executable allowances therefore become exactly 6,952 full production,
5,078 runtime, and 2,732 Application above the frozen pre-v0.10.6 base
ratchets; every other slice allowance remains unchanged. This named amendment
is non-transferable and becomes an exact descending ceiling immediately. It
changes no firmware/profile/range/output byte, CRC/header, processor, support,
Golden, update-package, installation, activation, deletion, or report-history
authority. It also does not close or fund the owner-requested repository-wide
single-implementation, layering, unused-module, and code-size audit.

## 2026-08-22 CtrlRAM input-admission crash correction

The owner approved the bounded correction after the real NT51950 `0x37000`
TP-work base exposed two invariant exceptions during CtrlRAM authoring. The
correction reuses the selected CtrlRAM route's profile maps to reject non-exact
reference capacities as a typed input-length issue before canonical identity
resolution, and returns the canonical empty report-metadata plan when the
reviewed Standard Merge profile declares no report-classification authority.
No second capacity table, map resolver, or Presentation exception policy is
introduced.

Relative to the preceding exact checkpoint, full production changes from
109,849 to 109,883 (+34) and runtime changes from 75,135 to 75,169 (+34).
Domain plus Profiles remains 20,632; Application remains 33,423; Bootstrap plus
CLI plus Desktop host remains 3,503; and Infrastructure plus Contracts plus CRC
worker changes from 17,577 to 17,611 (+34). Presentation is unchanged.

The executable allowances therefore become exactly 6,986 full production,
5,112 runtime, and 2,255 Infrastructure/Contracts/worker above the frozen
pre-v0.10.6 base ratchets; every other slice allowance remains unchanged. This
named R2 correction is non-transferable and becomes an exact descending ceiling
immediately. It changes no profile, range, output byte, operation order,
CRC/header, processor, naming, support, or Golden authority, and it does not
close or fund the separate repository-wide single-implementation, layering,
unused-module, and code-size audit.

## 2026-08-22 exact CtrlRAM report-metadata counterpart correction

The owner then approved removal of the remaining CtrlRAM report-metadata map
heuristic. The correction adds one optional, hash-closed trust-index counterpart,
validates the complete CtrlRAM registration set before publication, materializes
the exact declared Standard-profile map through the existing compiler, and binds
the actual map in the Application capability contract. It deletes the former
capacity/TP-length search, candidate ranking, deduplication, and fallback. It
introduces no second map catalog, per-IC C# table, runtime discovery, or UI path.

After readability-preserving simplification, full production changes from
109,883 to 109,929 (+46) and runtime changes from 75,169 to 75,215 (+46).
Domain plus Profiles remains 20,632; Application changes from 33,423 to 33,429
(+6); Bootstrap plus CLI plus Desktop host remains 3,503; and Infrastructure
plus Contracts plus CRC worker changes from 17,611 to 17,651 (+40).

The executable allowances therefore become exactly 7,032 full production,
5,158 runtime, 2,738 Application, and 2,295
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets;
every other slice allowance remains unchanged. This named R2 correction is
non-transferable and becomes an exact descending ceiling immediately. It
changes no profile geometry, range, output byte, operation order, CRC/header,
processor authority, naming, support, UI, or Golden authority, and it does not
close or fund the separate repository-wide single-implementation, layering,
unused-module, and code-size audit.
