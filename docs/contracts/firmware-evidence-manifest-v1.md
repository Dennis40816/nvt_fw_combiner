# Firmware Evidence Manifest Contract 1.0

The executable schema is
[`firmware-evidence-manifest-v1.schema.json`](firmware-evidence-manifest-v1.schema.json).
It records immutable source artifacts, independently reviewable firmware facts, and
promotion-relevant blocker evidence without placing private firmware payloads in Git.

## Fact and promotion separation

`facts[].disposition` answers whether one assertion was observed, accepted, rejected, or remains
unresolved. `facts[].promotionImpact` records the consequence of uncertainty. It does not promote a
map or profile. Reviews accept or reject facts only.

An approved fact still cannot grant workflow execution. Family capability rows are technical facts;
only a matching composition profile owns promotion stage and blocker references. The compiler
derives eligibility from that profile plus resolved evidence.

## Source handling

Every cited workbook, source snapshot, firmware artifact, issue export, document, or owner record is
identified by logical name, exact byte size, and SHA-256. A repository path is optional and must not
point outside the repository. Private files remain in the owner's evidence store; the manifest keeps
only provenance, hashes, and precise locations such as workbook sheet/cell or source line.

Offline workbook intake may add `intakeProvenance` only to candidate output. The standard declared
input is [`ic-reference-intake-request-v1`](ic-reference-intake-request-v1.md). The intake tool
writes to a caller-selected empty staging directory, reads source files only, does not execute macros,
and cannot edit approved contracts, profiles, bundles, or evidence. Candidate output has no runtime
authority until reviewed and committed as an approved manifest in a trusted bundle.
