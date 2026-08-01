# Profile declaration instructions

- Profiles are canonical IC facts, not UI configuration or golden-derived
  guesses.
- Declare named half-open address ranges, address spaces, topology/IC Count
  applicability, regions, mappings, access, atomicity, metadata, processors,
  integrity, validation, and output naming once.
- Perfect-like families own one complete modeled firmware definition and forbid
  member-specific semantic overrides. Partial family relationships are named
  and part/fact-scoped; sharing one part never implies inheritance of another.
- Every byte-semantic change is R3 and requires schema/profile tests,
  independent byte/golden evidence, and firmware-owner approval.
- Do not add real firmware payloads.
- First test: `NvtFwCombiner.ProfileContract.Tests`.
