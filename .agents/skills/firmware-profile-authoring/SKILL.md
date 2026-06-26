---
name: firmware-profile-authoring
description: Add or change an IC profile, canonical memory region, experience access rule, merge/replace mode, mapping constraint, version extractor, patch, processor declaration, validation, or output naming. Never bypass the profile/compiler with a one-off script.
---

# Firmware Profile Authoring

1. Record authoritative memory-map/evidence source, owner, ref and hashes.
2. Define canonical regions once, including classification tags such as `dp`, `tp`, `tp-ctrlram`, `protected`.
3. Select orthogonal composition kind, initializer, experience audience, layout policy and input policy.
4. Add deny-by-default region access rules. Enforce Display TP whole-only; TP HW CtrlRAM-only plus DP whole; TP FW non-CtrlRAM plus DP whole.
5. Express every range as start+length/half-open and name its address space/basis.
6. Separate physical inputs, binding instances and logical views.
7. Add ordered operations, explicit overlap and processor/integrity declarations.
8. Validate bounds, arithmetic, atomicity, access, overlap, compatibility, missing references and malformed values.
9. Update support matrix, examples, evidence manifest and golden regression.
10. Run schema/profile/planner/golden tests and `$polytail`; report every changed firmware fact.
