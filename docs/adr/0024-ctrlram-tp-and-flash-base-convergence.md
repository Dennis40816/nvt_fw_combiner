# ADR 0024: CtrlRAM TP-BIN and Full-Flash Base Convergence

- Status: Accepted
- Date: 2026-07-17
- Amended: 2026-07-18 for reviewed NT51926 1.4.1 cascade and 2.0.0 single/cascade runtime slices
- Amended: 2026-08-25 by ADR 0055 for supported CtrlRAM admission across declared TP/full maps
- Owners: Architecture owner + firmware owner
- Supersedes: ADR 0023
- Amends: ADR 0015 and ADR 0020
- Amended by: ADR 0055

## Context

The owner requires CtrlRAM Replace to accept both the TP work BIN and the full
Flash BIN. Both forms must execute the same replacement and Legacy Combiner
postbuild semantics. A full-Flash input must not cause the Combiner to receive
unrelated DP bytes, and processing must not change the container tail.

ADR 0023 temporarily restricted the non-routed NT51926 V2 candidate to exact
`0x3C000` TP input because processor operations previously staged the complete
output space. That restriction was safe but incomplete.

## Decision

Composition-profile schema 2.8 gives each Legacy Combiner stage an explicit
zero-based `targetViewId`. The shared
executor already stages an operation target range and imports the returned
bytes into that same range; the Domain contract now admits a zero-based prefix
while continuing to reject nonzero subranges.

Each routed NT51926 Common FW category therefore binds two canonical maps:

- `0x3C000` TP work: clone and process the complete image.
- `0x40000` full Flash: clone the complete container, process only
  `[0,0x3C000)`, and preserve `[0x3C000,0x40000)` byte-for-byte.

Both map shapes resolve the same TP regions, staged CtrlRAM sources, selected
Combiner 1.13 invocation profile, read authority, and write authority. The
FWConfig Backup Common FW version first selects the exact 1.4.1 or 2.0.0 map;
input length then selects its TP-work or full-Flash shape. An absent or
ambiguous version predicate and every undeclared length fail closed.

The exact command family, ordered blocks, arguments, source filenames/offsets,
and target ranges are loaded from `profiles/built-in/ctrlram-postbuild-v2/catalog.json`.
Infrastructure verifies its pinned SHA-256 before typed construction. This retires
the static C# command declarations without duplicating them in the V2 candidate;
the profile retains only the closed invocation profile id and processor authority.

## Consequences

- TP BIN and full Flash share one compiled workflow rather than two byte paths.
- Full-Flash DP/gap bytes never enter the Legacy Combiner staging image and are
  preserved by the engine-owned reference clone.
- NT51926 Common FW 1.4.1 cascade and Common FW 2.0.0 PID `0x1309` with chip
  count 1 or 3, all without a TP firmware-version edit, now use exact V2
  routes for Preview/Build. The
  profiles remain `executable-candidate`; this does not promote runtime support,
  other versions/counts, or version edits.
- Legacy Combiner EXE/runner, staging isolation, host diff enforcement, and
  owner review remain mandatory.
- Other IC/count branches and firmware-version edits remain separate
  evidence/migration work.

ADR 0055 later applies this same TP-prefix/full-tail contract to every declared
owner-approved CtrlRAM route and permits its structurally safe profile to be
promoted to `supported`. The exact route/fingerprint evidence and product
publication decisions remain separate release authorities.

## Verification

- Domain tests prove a processor can transform only a zero-based prefix and
  that the container tail remains unchanged.
- Profile/schema tests require the processor target view only in 2.8.
- NT51926 candidate tests select both exact capacities, pass only `0x3C000` to
  the processor, preserve the `0x4000` Flash tail, reject neighboring lengths,
  and execute against the hash-pinned Postbuild profile selected by id.
- The routed 1.4.1 TP-base case matches the archived Legacy Combiner 1.13 full
  output, and its full-Flash case matches the pre-retirement V1 control byte-for-byte.
- The final owner 2.0.0 single/cascade cases build their reference from the
  exact DP+TP inputs. The pre-retirement V1 controls and V2 outputs have identical full bytes and
  SHA-256. Each differs from the owner expected by exactly 16 bytes at
  `[0x1C,0x20)`, `[0xFC,0x100)`, `[0x32A8C,0x32A90)`, and
  `[0x32B6C,0x32B70)`: the owner-approved Header CRC/Header Copy CRC words;
  CtrlRAM payload difference is zero.
- Tests lock one processor session, two ordered commands, immutable inputs,
  the V1 full-Flash read authority, the V2 `[0,0x3C000)` read authority, exact
  write ranges, report identity, and full-Flash tail preservation. Runtime
  support promotion remains outside this amendment.
