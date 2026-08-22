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
