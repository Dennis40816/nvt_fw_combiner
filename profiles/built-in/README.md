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
owner-approved golden-byte parity. Bootstrap routing remains intentionally deferred to its own
reviewed phase; release support still requires firmware-owner migration review.
