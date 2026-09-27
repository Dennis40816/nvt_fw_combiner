---
name: nfc-firmware-profile-authoring
description: Add or change an IC profile, canonical memory region, experience access rule, merge/replace mode, mapping constraint, version extractor, patch, processor declaration, validation, or output naming. Never bypass the profile/compiler with a one-off script.
---

# Firmware Profile Authoring

Start from the affected profile, schema and authoritative memory-map/evidence
source; record provenance, owner and hashes. Read the current
[experience policy](../../../docs/architecture/experience-and-access-policy.md)
and relevant contract/ADR amendments before changing an access rule.

Profiles own canonical regions, protection, atomicity, access, mappings,
overlap, processors, integrity and naming. Declare each fact once and extend
its existing compiler owner. Keep composition kind, initializer, audience,
layout and input policy orthogonal; enforce deny-by-default access for the
active experience rather than reviving historical persona rules.

Name the address space and basis for every checked half-open range. Separate
physical inputs, binding instances and logical views. CtrlRAM membership is
physical `owner = tp`, `kind = ctrlram`, never inferred from UI or filenames.
Express ordered operations, explicit overlap and minimum processor read/write
ranges through the shared planner/executor. Unknown integrity cannot become
supported; a new firmware field or command requires independent authority.

Verify the affected schema/compiler/planner cases, including bounds,
arithmetic, atomicity, access, compatibility, missing references and malformed
values. Use skill `nfc-golden-regression` for changed expected bytes and support
promotion; synchronize affected support declarations, examples and evidence.
Apply skill `nfc-review` and root risk-based narrow/final gates. Report every
changed firmware fact and retain firmware-owner approval, independent Golden
evidence and exact write-range audit where required.
