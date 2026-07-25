# Profile declaration instructions

- Profiles are canonical IC facts, not UI configuration or golden-derived
  guesses.
- Declare named half-open address ranges, address spaces, topology/IC Count
  applicability, regions, mappings, access, atomicity, metadata, processors,
  integrity, validation, and output naming once.
- Family relationships are fact-scoped; sharing one fact never implies
  inheritance of all facts.
- Every byte-semantic change is R3 and requires schema/profile tests,
  independent byte/golden evidence, and firmware-owner approval.
- Do not add real firmware payloads.
- First test: `NvtFwCombiner.ProfileContract.Tests`.
