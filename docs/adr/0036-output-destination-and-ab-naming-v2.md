# ADR 0036: Resolve output identity safely and render AB FlashCode names

- Status: Accepted for `v0.9.15` on 2026-07-23
- Owners: Product owner, architecture owner, firmware owner
- Supersedes: The automatic-name and collision portions of ADR 0034
- Amends: ADR 0035
- Amended by: ADR 0050 for optional atomic bundle-directory delivery only
- Risk: R3 for AB profile admission; R2 for the all-mode output contract

## Context

An automatic AB name must come from the compiled IC and accepted execution
snapshots, while an explicit caller output name must become the single output
identity shown to the operator and in reports.  The prior no-clobber default
also prevents a deliberate rebuild from replacing an older result even when
the target is unrelated to every source artifact.

The product owner requires one cross-mode output policy: an output may replace
an existing unrelated file, but it must never replace a selected input.  This
is a destination-safety rule, not a firmware admission rule.  It applies to
automatic and explicit output names for every IC and mode.

## Decision

### AB automatic name

The closed AB automatic form is:

```text
{canonical-ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin
```

`canonical-ic` includes the `NT` prefix, for example `NT51929`; it is the
compiled selected IC, never a display alias.  `FlashCode` is a literal.  DP
tokens use the compiled A/B CMI views (Reg17 major and Reg18 high-nibble
minor); TP tokens use the accepted TP prefix described by ADR 0035.  Jira,
paths, filenames, UI labels, and whole-file hashes are not filename inputs.

The date is the one injected run-start UTC system date in invariant
`yyyyMMdd` form.  Unknown DP or TP metadata remains non-blocking and renders
`Dxxxx` or `Txxxx` with typed report diagnostics.

### Effective output identity

The effective output name/path is the explicit caller override when supplied;
otherwise it is the automatic/profile default.  Every primary report and CLI
output identity field uses this effective value.  A report must not present an
automatic candidate as though it were the committed/requested artifact after
an override.  It may retain the renderer kind, compiled template, token values,
and `isExplicitOverride` for audit without showing a competing output name.

### Safe replacement

Before promotion, the host compares the resolved output target identity with
every selected input identity.  If it is the same physical/canonical path as
any input, the run fails closed before staging or mutation.  It does not use a
filename string or whole-file hash as the protection test.

If the target is not an input, an existing target is atomically replaced for
automatic and explicit output names.  There is no counter suffix and no
separate overwrite switch needed to replace an unrelated older result.  Source
inputs remain immutable; all external processors still receive only
host-created staging copies.

## Consequences

- Existing callers receive predictable rebuild behavior without being able to
  overwrite their selected source firmware.
- AB naming uses the same output identity/report path as all other workflows;
  it does not create an AB-only publisher.
- Preview binds the effective output identity and compilation/input snapshots.
  A changed override, selected input, compiled plan, or UTC date requires a
  new Preview before Build.
- Profiles still declare their automatic template and whether an override is
  allowed; this ADR does not infer ranges, banks, topology, or processor
  authority.

## Verification

- Test automatic and explicit names for every output-capable mode.
- Test replacement of an existing unrelated target and rejection when the
  target aliases any DP/TP/reference/replacement input path.
- Test that report and CLI primary output fields always use the effective name.
- Test AB token source independence from UI text, filenames, Jira, and ignored
  TP tail bytes; test UTC date and Preview-to-Build identity.
- Test atomic failure leaves an existing unrelated target unchanged.
