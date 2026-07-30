# Experience and Operation Rules

This document expands the product rules summarized in `SPEC.md` section 7.5. These rules constrain supported firmware composition; they do not constrain the standalone raw-BIN Hex Editor defined by ADR 0014.

## Replace and Merge Authoring

- **DP Replace**: DP may be whole or profile-declared partitions. LDC
  replacement belongs to DP Replace and may use a separate `ldc-replacement`
  slot. TP-specific replace personas are not exposed.
- **CtrlRAM Replace**: only named physical regions with owner `tp` and kind `ctrlram`, or approved
  groups composed only of those regions, are replaceable.
- **General Replace**: explicit mappings are available only in profile `explicit-range` access. Protected regions remain blocked. A TP-classified mapping must select an approved legacy Combiner CRC/header refresh after mutation or fail closed.
- **General Merge**: input cardinality is extensible and every mapping compiles to standard operations over a blank image.
- **Hex Editor**: follows ADR 0014. It is a raw in-memory BIN utility with no firmware support claim.

An explicitly declared selection group may contain individually optional
`zero-or-one` slots while requiring a minimum and maximum selected count across
the applicable members. This does not make unrelated multi-input workflows
optional. NT51928 DP Replace uses one Initial Code/LDC group with selected count
`1..2`; after a `0x40000` Reference resolves LDC as `NotApplicable`, Initial
Code is the only applicable member and is therefore required.

NT51928 Standard Merge and DP Replace each remain one public capability with
two declared map variants. For Standard Merge, LDC absence selects the shared
Initial-Code/TP-only `0x40000` candidate; supplied LDC selects the NT51928
`0x80000` candidate and must then pass structural validation. Failure blocks
and never falls back to absence. DP Replace resolves the variant from the
accepted Reference length.

Built-in Initial Code, DP, TP, LDC, TPA, and TPB slots are address-bearing
section sources. Their outer file length is not an exact gate when every
selected source/metadata/validation/processor read is covered; a compatible
same-IC FlashCode may supply the same views. TPA copies at the same coordinates.
TPB reads the TP-native source window and writes it at the resolved bank
placement delta. Only current compact CtrlRAM replacement payloads normally
map source byte `0` to a nonzero built-in firmware target.

Replace Reference and complete DP AB seeds are whole-container inputs, not
section projections. They must match one declared capacity variant. General
Merge/Replace may explicitly author From File Start as a user mapping preset;
that does not create another built-in firmware rule.

FlashCode classification requires a resolved complete-container variant with
required DP/Initial Code and TP views. LDC is variant-optional. NVT marker,
ASCII IC hint, CMI, PID, version, length, and non-uniform checks are composable
signals rather than one magic signature; inconclusive classification is
`Unknown` and remains separate from section admission.

## Operation Algebra

Only these composition primitives are allowed:

```text
copy-range
replace-range
fill-range
patch-scalar
transform-scalar
run-processor
```

Mutable-space initialization, metadata extraction, and validation are typed compiler/engine stages,
not additional byte-operation variants. Each operation declares an id, sequence, source/target
logical views when applicable, overlap policy, and reason. UI authoring interactions do not mutate
bytes directly.

## Integrity Authority

Do not model integrity as `needsCrc: bool`.

```text
IntegrityDisposition: none | verify-existing | recalculate-and-write
ProcessorAuthority: calculate | transform
```

Inventory data may be `unknown`, but a supported profile may not. A transform may mutate only a host-created staging copy and only declared write ranges. The host independently validates the resulting diff.

## Range and Mutation Invariants

- Internal ranges are half-open `[start, endExclusive)`.
- JSON uses `start` plus `length`; UI may additionally display an inclusive end.
- Arithmetic is checked; overflow and out-of-bounds fail before execution.
- Overlap rejects by default and must be explicitly declared per operation.
- Every mutation records operation id, target space/range, before/after digest, changed ranges, and reason.
- A count-dependent DiffDLM operation expands only active records. Source dummy
  records outside the complete active full-stride prefix never become write
  operations; a source that reaches only the last writable DLM byte but omits
  its preserved NF tail is truncated and rejected. Inactive target records
  remain cloned from the immutable reference. A later postbuild FWConfig
  Backup mutation is a separate processor-owned operation and report entry,
  not an exception that widens the DiffDLM mask.

## Preview and Build Readiness

- Application resolves action state from independent authoring, execution,
  input, runtime-dependency, evidence, and publication dimensions. Evidence and
  publication never become hidden byte-execution switches.
- Runtime readiness is bound to route identity, capability fingerprint,
  catalog resolution token, authoring revision, and the current processor
  environment generation. Any mismatch is stale and fails closed before
  processor execution.
- A required external processor that is missing, invalid, or replaced blocks
  the executable Preview/Build attempt without invoking mutation. The next
  explicit refresh can recover in the same process after the environment is
  corrected.
- Structural input safety remains blocking. A profile-declared
  `non-uniform-region` plausibility validation is different: one uniform
  Initial Code/DP/LDC source view emits a typed warning through the shared
  Application result, UI, CLI, Preview, and Build Report, but does not block
  Build or change output bytes.
- Check-time state never creates a Run Report. When the user explicitly
  attempts Preview with a runtime blocker, the headless workflow may return a
  blocked Preview report; Build remains unavailable until every execution gate
  is current.
- For General Replace whose accepted targets require POSTBUILD, a missing
  Parent stage or runtime dependency permits only a plan-only Diagnostic
  Preview. It reports accepted/compiled mappings, projected coverage, the
  required stage when compiled, and the blocker. It executes no mappings or
  processor, emits no output BIN, and makes no final Header/CRC/hash claim.
  Missing Parent authority and a missing runtime tool remain distinct issues.
