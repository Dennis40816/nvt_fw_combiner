# Built-in Profiles

Built-in IC profiles are added one mode per reviewed change with evidence and golden regression.

`nt51920-standard-merge` is the first canonical V2 migration bundle. Its family/profile ranges,
operation order, output name, and owner-approved golden bytes are locked against the legacy
`nt51920-standard-merge-gen-flash` profile. It is not selected by the UI or CLI yet, and the legacy
profile remains the production execution path until the V2 Bootstrap selection phase and firmware-owner
review are complete. Its `profileVersion` deliberately remains `0.5.0`: this migration does not alter
its byte semantics. The bundle's `supported` promotion admits the closed V2 runtime contract only;
release support remains governed by the matrix and owner gate.
