# ADR 0016: Use closed typed firmware metadata values and decoding rules

- Status: Accepted
- Date: 2026-07-11
- Accepted: 2026-07-12 by the product and architecture owner
- Owners: Product owner + architecture owner + firmware reviewers
- Amends: ADR 0015
- Preserves: ADR 0012

## Context

`firmware-family-v1` declares metadata fields used by map resolution, validation, version display,
and output naming. Its initial field schema permits raw bytes, unsigned integers, and ASCII, while
the initial Domain scalar collapses every integer into signed `long` and has no raw-byte value.
The contract also leaves byte order, bit slicing, ASCII normalization, and masked assertions
under-specified.

This is already insufficient for the reviewed compatibility model: common FWConfig contains
signed `sbyte`, unsigned `byte`/`ushort`/`uint`, and fixed ranges. A permissive decoder could select
the wrong map through signed/unsigned coercion, replacement-character text, or noncanonical
assertion masks.

This ADR defines type and decoding semantics only. It does not declare a production structure,
offset, assertion, locator, alias, capability, or workflow. The existing half-open FWConfig
compatibility boundary at `0x07C` remains evidence to reproduce later, not a range inferred here.

## Decision drivers

- Deterministic map selection from one exact artifact/structure/field source.
- No implicit signedness, byte order, text normalization, or scalar coercion.
- Value identity that preserves raw bytes and leading zeroes.
- A closed declarative model that can replace IC-specific readers after parity evidence.
- No new runtime dependency, decoder plug-in hierarchy, or workflow-specific executor.

## Considered options

1. Keep one signed integer/text scalar and constrain individual family files by convention.
2. Decode every field as bytes and reinterpret values in Profiles, UI, or validators.
3. Add per-structure or per-IC decoder classes.
4. Use one closed field declaration and four non-coercing scalar kinds.

Option 4 is selected. The other options move ambiguity into conventions or duplicate firmware
semantics outside the canonical family model.

## Decision

### Field encodings and scalar identity

The closed field encodings are:

| Encoding | Width | Byte order | Bit slice | Domain value |
| --- | --- | --- | --- | --- |
| `bytes` | `>= 1` | forbidden | forbidden | raw bytes |
| `printable-ascii` | `>= 1` | forbidden | forbidden | text |
| `unsigned-integer` | `1..4` | required | optional | `ulong` |
| `signed-integer` | `1..4` | required | forbidden | `long` |

The initial four-byte integer maximum covers the reviewed `byte`, `sbyte`, `ushort`, and `uint`
compatibility evidence. Wider fields require a later contract decision about JSON number,
canonicalization, and hash interoperability. A one-byte integer still declares byte order so every
integer declaration has the same explicit shape.

`FirmwareMetadataValue` has four distinct kinds: SignedInteger, UnsignedInteger, Bytes, and Text.
Values of different kinds are never equal; signed `2`, unsigned `2`, bytes `02`, and text `"2"`
remain distinct. Raw bytes use structural equality and preserve leading zeroes.
The ambiguous `FromInteger` factory is removed rather than retained as a permanent adapter.

Predicate JSON remains compact and is interpreted only after resolving its exact field declaration:

- Signed/unsigned integer -> JSON integer within the effective bit width.
- Printable ASCII -> exactly `widthBytes` characters in `0x20..0x7E`.
- Bytes -> canonical lowercase, even-length hex string without `0x`, exactly matching field width.

Profiles validates this contextual conversion before constructing Domain predicate values. There is
no global unscoped scalar conversion.

### Integer decoding

Integer carriers are decoded using the declared byte order. Bit zero is the least-significant bit of
the normalized unsigned carrier, independent of storage byte order. An optional unsigned-integer bit
slice extracts:

```text
(carrier >> leastSignificantBit) & ((1UL << bitCount) - 1UL)
```

The checked declaration invariant is:

```text
leastSignificantBit >= 0
bitCount >= 1
checked(leastSignificantBit + bitCount) <= checked(widthBytes * 8)
```

Signed values prohibit bit slices and interpret the complete declared width as two's-complement.
Unsigned values produce `ulong`. No wrapping, truncation, floating-point rounding, numeric string
conversion, or signed/unsigned coercion is allowed. Predicate JSON accepts
any number the pinned Draft 2020-12 validator classifies as an integer; RFC 8785 canonicalization
normalizes its representation before bundle hashing and typed conversion.

No Boolean storage encoding is authorized without evidence. A future field must add an explicit
closed form such as one-byte canonical `boolean-byte` or required-one-bit `boolean-bit`; it cannot
weaken an integer encoding with implicit truthiness.

### Bytes and Printable ASCII

Raw bytes preserve exact order and length. Domain construction defensively snapshots supplied bytes,
and equality is content-based.

`printable-ascii` v1 is fixed-width and strict: every byte is printable `0x20..0x7E`, exactly
`widthBytes` are decoded, and comparison is ordinal. Leading and trailing spaces are preserved. NUL
termination, padding removal, trimming, case folding, replacement characters, and Unicode
normalization are forbidden. A later evidenced field may add a closed termination/padding policy;
the decoder never guesses one.

### Byte assertions

Assertions are structure-relative checked ranges. `expectedHex` contains at least one byte. Omitted
`maskHex` is the only canonical exact-match form and normalizes to all `ff`. An explicit mask has the
same length as expected, contains at least one set bit, and is not all `ff`. Matching is:

```text
(actual & mask) == expected
```

Masked-off expected bits must already be zero (`expected & ~mask == 0`) so semantically identical
assertions have one canonical representation. All-zero masks cannot satisfy an assertion requirement,
and explicit all-`ff` masks are rejected in favor of omission. Every assertion and metadata field must
fit the structure through checked `offset + length` arithmetic. All assertions pass before any field
is decoded; failure rejects the complete structure and yields no partial facts.

Read-only metadata fields may overlap, including multiple unsigned slices of one carrier. Assertions
may overlap fields and other assertions; overlapping assertions form a conjunction and every one must
pass. Metadata overlap never grants write authority.

### Ownership

- Domain owns immutable declarations, typed values, range/bit invariants, pure decode semantics, and
  a family-aggregate backstop for every normalized predicate value. The backstop rejects wrong kinds,
  signed/unsigned values outside the carrier or effective slice, byte values of the wrong width, and
  text of the wrong width or outside `0x20..0x7E`.
- Profiles owns trusted family cross-reference validation and contextual predicate scalar conversion.
- Application supplies immutable artifact payloads, orchestrates resolution, and maps structured
  pending/rejected outcomes; it does not reinterpret field bytes.
- Infrastructure loads trusted JSON/bundles only.
- Bootstrap, UI, and CLI project typed outcomes and never decode offsets or bytes.

The generic metadata path does not mutate firmware, invoke Python, run a processor, or create a
second composition executor.

## Consequences

### Positive

- Signed FWConfig fields, unsigned PID/version values, and raw identifiers remain exact.
- Invalid printable ASCII and assertion values fail closed before map selection.
- Predicate and bundle identity are deterministic and independent of UI or JSON parser coercion.
- Existing metadata readers can be retired after complete value and golden parity.

### Negative / trade-offs

- Field declarations and predicate values require stricter schema and semantic validation.
- Existing temporary `FromInteger` test construction must migrate explicitly to signed or unsigned.
- ASCII fields needing NUL/padding behavior remain unsupported until evidence adds a closed policy.
- Values wider than four bytes remain unsupported in v1.

### Risks and mitigations

- Offset/width overflow -> compose checked half-open ranges before slicing.
- Array reference equality -> snapshot bytes and compare content.
- Partial trust after assertion failure -> assertions gate the complete structure atomically.
- Legacy-reader drift -> keep old readers as compatibility evidence until all exposed values match.
- Premature family claims -> add no field offsets or production structures without owner evidence.

## Compatibility and migration

No `firmware-family-v1` instances have been committed or released, so the schema can be closed before
its executable compatibility population exists. If an external v1 consumer or family document is
identified, this assumption must be revisited and the required schema change versioned.

`FirmwareConfigLayout` and `FirmwareConfigMetadataReader` remain compatibility evidence. The generic
absolute-address inspection path must reproduce every reviewed `byte`, `sbyte`, `ushort`, and `uint`
value. ADR 0012's primary/Backup equality is golden evidence only; runtime facts always come from the
unique NVT Backup. Approximate prose
such as "through 0x78" never creates a field; every promoted declaration has exact offset, width,
structure length, provenance, and review.

`IsFirmwareVersionBarValid` is a derived validation outcome, not a Boolean storage field. The
compatibility reader remains authoritative for that result until a closed generic metadata relation
validator can express and reproduce the one's-complement relationship between FW and FW-bar. A
permanent FWConfig-specific branch is not an acceptable replacement.

## Verification

- Schema vectors for all four encodings and every forbidden property combination.
- Domain boundary tests for signed/unsigned extrema, endian order, unsigned bit slices, raw-byte
  structural equality/hashing, printable ASCII, checked field/assertion ranges, and masked assertions.
- Semantic tests for field-scoped predicate kinds, widths, bounds, and canonical byte hex.
- Overlapping field/slice and overlapping-assertion conjunction tests.
- Decoder tests proving assertion-first atomicity and no partial facts on any failure.
- Compatibility parity for all currently exposed FWConfig values, FW/FW-bar validity, and
  primary/Backup golden comparisons.
- Architecture tests preventing decoding in UI/CLI/Bootstrap and preventing a second executor.
- Architecture/contract review and product-owner approval before changing this ADR to Accepted.
