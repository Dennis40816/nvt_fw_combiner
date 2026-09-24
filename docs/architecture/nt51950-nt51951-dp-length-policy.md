# NT51950/NT51951 DP Length Policy

Source: `IC_FlashMap.xlsx`, sheet `51950 DP Perspective`; owner decision recorded
in the [1.1.10 delivery checklist](../ui/v1.1.10-delivery.md#dp-大小與-warning-設計)
on 2026-09-23. This document describes Standard Merge. AB Normal has its own
bank operations and needs separate admission and output evidence.

The canonical Standard maps retain the three confirmed DP container sizes
`0x40000`, `0x80000`, and `0x100000` for each member. When the captured DP length
matches one of these sizes, compilation selects its exact map first. Existing
owner Golden output bytes and map identities remain the comparison authority.
The six published exact route identities stay separate for map and evidence
traceability. For each Application request, one publication-bound Standard
route selection maps trusted capacities to those routes. Build, preview, and
metadata consume its selected result; classification enumerates candidates from
the same route set when the file type is still unknown. No consumer repeats the
capacity-to-route decision. A length-only request cannot select the layout
template, and any incomplete, ambiguous, or stale route set fails closed.

For another captured DP length, the profile explicitly selects that member's
`256k` map as a **layout template**. The template supplies canonical region
anchors; it does not limit the output to `0x40000`. The complete selected DP is
copied to an output of the same actual length. Only TP `[0xA000, 0x37000)` is
overlaid from the TP input. Customer information starting at `0x37000` and any
DP tail remain from the DP. Neither padding nor truncation is allowed. The
profile declares the source slot, template map, root region, expected outer
lengths, and typed `DP_NONSTANDARD_SIZE_WARNING`; the compiler and executor
remain shared with the exact routes.

An unexpected length is a nonblocking suspected-OSD advisory in accepted input
and Output Settings, not an automatic inference that OSD is present. A DP that
cannot cover a mandatory source read or output write remains an error with a
checked half-open range; for this Standard overlay, a length below `0x37000`
cannot contain the TP target. The selected TP must independently cover its
declared source range and pass its own IC Count and other input checks. Optional
DP metadata such as DPCMI naming retains its existing missing policy, including
`use-placeholder/xxxx`; absence does not become a new capacity blocker.

The Application report's optional `SourceEnvelope` records the actual output
length and the template map/capacity separately. Its `MapId` remains the
canonical map identity and must not be read as the actual output extent. The
report property is absent for exact-map runs. Nonstandard lengths do not inherit
Golden certification from the three exact capacities; they require their own
output evidence before a support claim.

General Merge has a separate authoring capacity choice. Its initial default for
these ICs is still the largest declared canonical map (`0x100000`) until the
author supplies an explicit General Merge mapping; this does not silently
select a Standard Merge map.

## Historical DP Replace decision

The dedicated DP Replace experience was retired for 1.1.10. Its earlier
exact-length base/replacement rule and 2026-08-02 owner decision remain
historical evidence, not a current Standard Merge or CtrlRAM admission path.
Canonical DP/LDC regions and customer-information facts remain available to
surviving declared experiences. See the
[experience and access policy](experience-and-access-policy.md) and
[1.1.10 retirement evidence](../ui/v1.1.10-delivery.md#dp-replace-汰除本地候選--2026-09-21).

## Required verification

- Execute the existing exact-map NT51950/NT51951 Golden cases against the
  candidate source without changing expected bytes, SHA, or difference bounds.
- Compare complete synthetic nonstandard outputs: length equals captured DP;
  every byte outside TP `[0xA000, 0x37000)` equals that DP; TP bytes equal the
  selected TP source. Include both members and lengths just beyond exact maps.
- Reject a DP shorter than a necessary source/output range. Verify captured
  nonstandard inputs show the warning before Build and exact inputs remain Ready.
- Verify automatic naming with missing optional DPCMI, report provenance, and
  the package/trust/policy route identities. Synthetic results do not replace
  firmware-owner Golden evidence or the release write-range audit.
