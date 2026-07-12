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
outputs, including the Normal DP extraction warning contract. It is not yet packaged or selected by
Bootstrap; runtime migration remains subject to the firmware-owner review recorded in the support
matrix.
