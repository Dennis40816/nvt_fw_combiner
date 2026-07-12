# Built-in Profiles

Built-in IC profiles are added one mode per reviewed change with evidence and golden regression.

`nt51920-standard-merge` is the first canonical V2 migration bundle. Its family/profile ranges,
operation order, output name, and owner-approved golden bytes are locked against the legacy
`nt51920-standard-merge-gen-flash` profile. Bootstrap now loads the packaged bundle through its
content-hash anchor and selects its V2 artifact for NT51920 Standard Merge; there is no legacy
compile fallback for that IC. Its `profileVersion` deliberately remains `0.5.0`: this migration does
not alter byte semantics. The bundle's `supported` promotion admits the closed V2 runtime contract
only; release support remains governed by the matrix and firmware-owner gate.

`nt51929-standard-merge` is the next canonical V2 candidate family. It declares the shared
NT51929/NT51932 physical map and the owner-confirmed NT51919 map-bound region-set alias. Its three
profiles are locked against existing legacy plan geometry and owner-approved 51929/51932 golden
outputs, including the Normal DP extraction warning contract. Bootstrap packages the bundle through
its content-hash anchor and selects its V2 artifacts for all three ICs without a legacy compile
fallback. Runtime migration remains subject to the firmware-owner review recorded in the support
matrix.

`nt51923-standard-merge` is a canonical V2 candidate family for NT51923 and NT51926. Its shared
physical map preserves TP `[0x00000, 0x3C000)`, the explicit forbidden gap, and DP
`[0x3E000, 0x40000)`. Both profiles have trusted-bundle, legacy-plan, Normal DP extraction, and
owner-approved golden-byte parity. Bootstrap packages the bundle through its content-hash anchor
and selects its V2 artifacts for both ICs without a legacy compile fallback. Release support still
requires firmware-owner migration review.

`nt51930-standard-merge` is a canonical V2 family for the standalone NT51930 FlashMap Standard
Merge profile. Its physical map preserves DP `[0x00000, 0x06000)`, the explicit forbidden gap, and
TP `[0x07000, 0x40000)`. Bootstrap packages the bundle through its content-hash anchor and selects
its V2 artifact without a legacy compile fallback. CtrlRAM postbuild remains outside this Standard
Merge bundle and still requires its independent firmware-owner evidence.

`nt51931-standard-merge` is a canonical V2 data candidate for the standalone NT51931 gen_flash
Standard Merge profile. Its physical map preserves TP `[0x00000, 0x3C000)`, the explicit forbidden
gap, and DP `[0x3E000, 0x40000)`. The V2 profile accepts the owner-approved `0x80000` DP container
while copying only the `0x40000` declared source span. It has direct legacy-plan and golden-byte
parity. Bootstrap packages the bundle through its content-hash anchor and selects its V2 artifact
without a legacy compile fallback; firmware-owner migration review remains required before release
support.

`nt51927-standard-merge` is a canonical V2 data candidate for standalone NT51927. It preserves TP
`[0x00000, 0x35000)`, the explicit gap, and DP `[0x3C000, 0x40000)`. Its profile accepts the
owner-approved `0x200000` DP outer container while copying only the `0x40000` declared source span.
The direct V2 bundle has legacy-plan and golden-byte parity; NT51917 remains a separate map-bound
alias and runtime-routing phase.

`nt51928-standard-merge` is the hash-anchored V2 Standard Merge route for standalone non-NB
NT51928. Its `0x80000` physical map models TP `[0x00000, 0x35000)`, DP
`[0x3C000, 0x40000)`, LDC `[0x40000, 0x62000)`, and both forbidden gaps. The third input is a typed
`auxiliary` LDC slot with exact map capacity, not a special execution path. Bootstrap packages this
bundle by its content-hash anchor and selects its V2 artifact without a legacy compile fallback.
Firmware-owner review is still required before support promotion; NT51928 NB remains unmodeled.
