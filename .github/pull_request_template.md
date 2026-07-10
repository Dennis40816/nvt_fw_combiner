## What changed

## Why

## Scope and architecture layer

## Firmware impact

- Affected IC/mode/profile:
- Ranges/offsets/patches/CRC/order changed: Yes / No
- Golden hashes changed: Yes / No

## Contracts and compatibility

- Schema/protocol/API impact:
- Breaking change: Yes / No
- ADR/profile version required: Yes / No

## Verification evidence

```text
# R1-R3 final gate
python scripts/verify.py --all

# Qualifying R0 documentation/governance-only gate
python scripts/verify.py --structure-only
```

- Commands actually run:
- Results:
- Checks not run and reason:

## Development execution

- Scope frozen before commit: Yes / No
- Narrow test run before broader verification:
- Final gate (`--all` or qualifying `--structure-only`):
- Retried commands and the material change before retry: None / details
- Generated, private, or temporary payloads excluded from the diff: Yes / No

## Polytail review

- Risk class (`R0`–`R3`):
- Verdict (`PASS`, `PASS-WITH-HUMAN-GATE`, `FAIL`):
- Architecture/test-quality findings:
- Residual evidence gaps:

## Security, dependency, and release impact

## Human review gates required

- [ ] Firmware semantics
- [ ] Schema/protocol compatibility
- [ ] Dependency/license
- [ ] CI/permissions/secrets
- [ ] Packaging/signing/release
- [ ] None
