# Candidate IC Workbook Contract 1.0

Status: Proposed — product, architecture, firmware-authoring, and security
acceptance required.

The executable normalized-projection schema is
[`candidate-ic-workbook-v1.schema.json`](candidate-ic-workbook-v1.schema.json).
This contract defines a strict `.xlsx` authoring surface for explicit candidate
evidence. It does not define another firmware profile language, execute a
workbook, install a profile, or promote support.

## Authority and pipeline

The workbook is an input adapter over the existing firmware-evidence fact
vocabulary. Its normalized projection is consumed by the typed candidate-intake
use case, which snapshots separately bound artifacts and produces a candidate
`firmware-evidence-manifest-v1` record. Only accepted, cited facts may later be
mechanically projected by the Profiles authoring boundary into existing
`firmware-family-v1`, `composition-profile-v2`, and `profile-bundle-v1`
documents. Those documents still require the existing schema, normalizer, map
resolver, compiler, parity, and owner-review gates.

```text
strict .xlsx named tables
  -> normalized candidate-ic-workbook-v1 projection
  -> immutable artifact bindings and hash/size snapshots
  -> candidate firmware-evidence-manifest-v1
  -> reviewed Profiles authoring projection
  -> existing V2 schema/normalizer/compiler
  -> candidate workspace diagnostics
```

No filename, neighboring IC, formula result, workbook layout, mmap token, BAT
line, successful parse, or sample BIN becomes a firmware fact automatically.
Missing or ambiguous facts remain `unresolved` and retain their declared
promotion impact.

## Closed workbook shape

The accepted file format is strict `.xlsx`. Macro-bearing `.xlsm`, legacy
binary `.xls`, encrypted/password-protected workbooks, external links, data
connections, formulas, array formulas, shared formulas, VBA, embedded packages,
OLE objects, scripts, hidden/very-hidden sheets, hidden rows/columns, merged
cells, and sheet protection fail closed. Formatting that does not affect cell
values is ignored.

The workbook contains exactly these worksheets and Excel tables. Every table is
present even when its fact table has no rows.

| Worksheet | Excel table | Rows | Purpose |
| --- | --- | ---: | --- |
| `Manifest` | `NfcManifest` | exactly 1 | candidate identity and literal-false authority |
| `Artifacts` | `NfcArtifacts` | 1–256 | declared external evidence roles |
| `RangeFacts` | `NfcRangeFacts` | 0–256 | address-space ranges using start + length |
| `ScalarFacts` | `NfcScalarFacts` | 0–256 | explicit boolean/integer/string facts |
| `ReferenceFacts` | `NfcReferenceFacts` | 0–256 | references to canonical ids |
| `StatementFacts` | `NfcStatementFacts` | 0–256 | unresolved or reviewed literal statements |
| `Citations` | `NfcCitations` | 1–1024 | ordered fact-to-artifact locations |

Extra worksheets, tables, columns, duplicate headers, case-colliding names, or
rows outside the declared table ranges fail validation. Table and column names
are ordinal and case-sensitive.

## Table columns

### `NfcManifest`

Columns, in order:

```text
schemaVersion workbookId workbookVersion candidateId manifestId manifestVersion
authority runtimeRegistration supportPromotion commandExecution
privatePayloadCopy profileInstallation firmwareInference
```

`schemaVersion` is `1.0`; `authority` is `candidate-only`; every capability cell
is the Boolean value `FALSE`. Identifiers are lowercase hyphenated ids and
versions are SemVer. A workbook cannot contain review decisions or a generated
timestamp. Reviews remain external human records, and deterministic replay uses
an injected clock.

### `NfcArtifacts`

Columns, in order:

```text
artifactId role sourceKind logicalName
```

Roles and their required evidence-manifest source kinds are:

| `role` | `sourceKind` |
| --- | --- |
| `supporting-workbook` | `workbook` |
| `memory-map` | `document` |
| `postbuild-command` | `source-code` |
| `sample-input` | `firmware` |
| `expected-output` | `firmware` |
| `provenance` | `document`, `issue-export`, or `owner-record` |

The workbook never stores a host path, repository path, executable path, URL,
command argv, firmware bytes, hash, or size. UI/CLI binds each `artifactId` to
one immutable external file. Infrastructure opens that exact regular file once,
calculates size/SHA-256 from the same validated handle, and adds the snapshot to
the candidate evidence manifest. The workbook itself is snapshotted by the host
outside its self-referential contents.

### Fact tables

Every fact table begins with these columns:

```text
factId familyId memberId modeId profileId factKind disposition promotionImpact rationale
```

`familyId` is required. `memberId`, `modeId`, `profileId`, and `rationale` are
optional; an empty optional cell means the normalized property is omitted.
`memberId` uses the existing `NT[0-9A-Z-]+` grammar. `factKind`, `disposition`,
and `promotionImpact` use the existing firmware-evidence-manifest enums.

The remaining columns are:

| Table | Additional columns | Normalized value |
| --- | --- | --- |
| `NfcRangeFacts` | `addressSpaceId startHex lengthHex` | `{ kind: range, addressSpaceId, range: { start, length } }` |
| `NfcScalarFacts` | `valueType value` | `{ kind: scalar, value }` |
| `NfcReferenceFacts` | `targetId` | `{ kind: reference, targetId }` |
| `NfcStatementFacts` | `text` | `{ kind: statement, text }` |

Range cells are text in canonical `0x` plus 1–16 uppercase hexadecimal digits.
The reader converts them to unsigned integers, requires `length > 0`, rejects
overflow, and reports the half-open range `[start, start + length)`. It never
accepts an inclusive end column. `valueType` is `boolean`, `integer`, or
`string`; the Excel cell type must match, and integers must be exact signed
53-bit values so Excel cannot silently round them.

Fact ids are unique across all four fact tables. A row's table chooses only the
value representation; it does not infer `factKind`, disposition, subject, or
promotion impact.

### `NfcCitations`

Columns, in order:

```text
factId ordinal artifactId location
```

Every fact has at least one citation. `ordinal` starts at 1 and is contiguous
within each fact. `artifactId` must name `NfcArtifacts`; `location` is explicit,
such as `DP Perspective!NfcMemoryMap[#Data]`, `postbuild.bat:L12-L14`, or an
owner-record section. Ordering is normalized by `factId`, then `ordinal`.

## Cell and normalization rules

- String cells reject leading/trailing whitespace and control characters.
- Blank required cells, error cells, rich-data cells, and locale-dependent
  numbers/dates fail validation.
- IDs, enums, hashes, and hexadecimal values are ordinal ASCII.
- Formula cells fail even when a cached value exists.
- Row order has no semantic authority. Projection arrays are sorted by stable id;
  citations use their explicit ordinal.
- Duplicate artifact ids, fact ids, citation ordinals, or normalized paths fail.
- Every citation and reference must resolve inside the normalized request.
- Normalized JSON is UTF-8 and validates against the companion schema before any
  candidate evidence or profile projection occurs.

JSON Schema validates shape. The future workbook reader/semantic validator must
also verify the closed Open XML package, exact worksheets/tables/headers, cell
types, uniqueness, cross-references, citation contiguity, hexadecimal conversion,
range arithmetic, and immutable artifact bindings.

## External postbuild boundary

`postbuild-command` artifacts are inert evidence only. The reader may preserve
an owner-selected line range for review, but never invokes a shell, expands an
environment variable, follows `call`, discovers sidecars, or converts argv into
runtime authority.

Executable processor configuration remains in existing V2 processor stages and
may reference only an installed allowlisted `processorId`, `toolBindingId`, and
`invocationProfileId`. The current comparison authority is the exact Legacy
Combiner 1.13 command. A future `combiner.exe` is a separately reviewed external
tool package; its algorithm is not implemented in this workbook reader or the
application. New command protocol, read/write ranges, integrity behavior, or
tool authority retains normal R2/R3 evidence and release review.

## Firmware invariants

This contract changes no composition kind, experience, IC, mode, address space,
range, operation, atomicity, processor write range, integrity rule, output name,
golden output, or support stage. It specifically cannot alter the full submitted
DP container base for NT51950/NT51951 AB candidates, add an AB `map.txt`,
authorize C# AB header CRC writes, replace exact Legacy Combiner 1.13 parity, or
weaken direct/approved fact-scoped evidence for NT51919/NT51932.

## Synthetic normalized example

The example is fictional, candidate-only, and not an IC support claim:

```json
{
  "schemaVersion": "1.0",
  "workbookId": "nt-example-intake",
  "workbookVersion": "0.1.0",
  "candidateId": "nt-example-candidate",
  "authority": "candidate-only",
  "manifest": {
    "manifestId": "nt-example-evidence",
    "manifestVersion": "0.1.0"
  },
  "artifacts": [
    {
      "artifactId": "memory-map-record",
      "role": "memory-map",
      "sourceKind": "document",
      "logicalName": "Synthetic memory map.txt"
    },
    {
      "artifactId": "owner-record",
      "role": "provenance",
      "sourceKind": "owner-record",
      "logicalName": "Synthetic owner record"
    }
  ],
  "rangeFacts": [
    {
      "factId": "synthetic-range",
      "familyId": "nt-example",
      "memberId": "NT00000",
      "modeId": "standard-merge",
      "profileId": "nt-example-standard-merge",
      "factKind": "range",
      "disposition": "unresolved",
      "promotionImpact": "blocks-execution",
      "rationale": "Synthetic shape only.",
      "addressSpaceId": "synthetic-input",
      "start": 0,
      "length": 1
    }
  ],
  "scalarFacts": [],
  "referenceFacts": [],
  "statementFacts": [],
  "citations": [
    {
      "factId": "synthetic-range",
      "ordinal": 1,
      "artifactId": "memory-map-record",
      "location": "Synthetic range row"
    }
  ],
  "capabilities": {
    "runtimeRegistration": false,
    "supportPromotion": false,
    "commandExecution": false,
    "privatePayloadCopy": false,
    "profileInstallation": false,
    "firmwareInference": false
  }
}
```

## Acceptance and release impact

Before a reader or UI is implemented, this Proposed contract requires product,
architecture, firmware-authoring, and workbook security acceptance. A reader
dependency requires a separate package/license/size review. This contract is
excluded from the portable package until the implementation/package decision is
reviewed. Any generated firmware declaration, processor selection, golden result,
or support promotion retains its normal owner gate.
