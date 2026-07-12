# Built-in Profiles

Built-in IC profiles are added one mode per reviewed change with evidence and golden regression.

`nt51920-standard-merge` is the first canonical V2 migration bundle. Its family/profile ranges,
operation order, output name, and owner-approved golden bytes are locked against the legacy
`nt51920-standard-merge-gen-flash` profile. Bootstrap now loads the packaged bundle through its
content-hash anchor and selects its V2 artifact for NT51920 Standard Merge; there is no legacy
compile fallback for that IC. Its `profileVersion` deliberately remains `0.5.0`: this migration does
not alter byte semantics. The bundle's `supported` promotion admits the closed V2 runtime contract
only; release support remains governed by the matrix and firmware-owner gate.
