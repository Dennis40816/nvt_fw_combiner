# Confidential reference manifest

The public inventory is [confidential-references.json](../references/confidential-references.json),
validated by [the version 1 schema](confidential-references-v1.schema.json).
Each entry has a unique stable descriptive `id`, `sizeBytes`, lowercase `sha256`
and `kind`. Entries carry no original file names or paths. Identical content may
have multiple descriptive ids; SHA-256 identifies the private bytes.

The private repository `nvt-private-assets` resolves SHA-256 through
`nfc/references/SHA256SUMS`. `NVT_PRIVATE_ASSETS` identifies its checkout for
confidential Golden tests. Those tests verify the resolved bytes before use;
when the variable is unset they skip with `confidential golden not executed`.
Hash and presence assertions use this public inventory without private access.

The inventory records evidence identity and purpose. It does not redefine
profile facts, certify firmware outputs or grant release approval. Release
packages include public reference documentation and manifests, with confidential
source files and private provenance excluded. Historical source inventories
remain provenance records; they do not assert current public availability.
