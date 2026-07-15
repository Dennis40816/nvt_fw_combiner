# Canonical Region Model

This document defines physical regions owned by `firmware-family-v1` and the separate workflow
access rules owned by `composition-profile-v2`.

## Physical ownership

A `FirmwareRegion` is one evidence-backed half-open range in a named family address space. It owns
physical facts only:

| Field | Purpose |
| --- | --- |
| `regionId` | Stable id referenced by maps, profile views, saved rules, reports, and tests. |
| `parentRegionId` | Optional physical containment hierarchy. |
| `owner` | Closed owner: `system`, `dp`, `tp`, `ldc`, `register`, `customer`, `shared`, `reserved`, or `unknown`. |
| `kind` | Closed physical kind such as `code`, `header`, `command`, `firmware-config`, `ctrlram`, `customer-information`, `checksum`, `reserved`, or `unmapped`. |
| `range` | Half-open `{ start, length }` in the region set's address space. |
| `writeConstraint` | Non-relaxable bound: `forbidden`, `whole-region`, `declared-subregions`, or `explicit-range`. |
| `alignment` | Minimum checked start/length alignment. |

An image-map shape selects one exact capacity and declares
`coveragePolicy = complete-with-explicit-gaps`. Referenced regions must stay in bounds, preserve
proper parent containment, reject sibling overlap, and cover the complete capacity. Root regions
partition the full image range. Whenever a region has children, its direct children also partition
the complete parent range. Every otherwise unclassified interval is therefore an explicit
`reserved` or `unmapped` region rather than an implicit gap.

Physical owner/kind is the canonical classification. There is no parallel tag catalog. During v1
compatibility migration, an adapter may project owner/kind to legacy `classificationTags`; that
projection is not accepted as firmware truth and is deleted after parity.

## Header and metadata rule

DP and TP headers remain distinct physical regions. FirmwareConfig, CMD, CMD-BK, PID, version, and
other structures are modeled by family metadata locators/fields rather than semantic region tags or
profile offsets.

Example synthetic regions:

```json
{
  "regionId": "tp-control-ram-a",
  "parentRegionId": "tp-image",
  "owner": "tp",
  "kind": "ctrlram",
  "range": { "start": 4096, "length": 8192 },
  "writeConstraint": "whole-region",
  "alignment": 4
}
```

```json
{
  "regionId": "customer-information",
  "owner": "customer",
  "kind": "customer-information",
  "range": { "start": 12288, "length": 4096 },
  "writeConstraint": "forbidden",
  "alignment": 1
}
```

The values are synthetic and grant no production range authority.

## Workflow access

Profiles reference region ids and can only narrow physical `writeConstraint`:

```text
regionId
access: hidden | read-only | whole | parts | explicit-range
allowedSubregionIds[]
reason
```

- DP Replace may expose DP whole regions or declared DP subregions.
- CtrlRAM Replace may expose only regions with `owner = tp` and `kind = ctrlram`, or approved groups
  composed entirely of those regions.
- General Replace may expose explicit ranges only inside the intersection of physical constraints
  and profile access. Protected/checksum/header behavior remains processor and validation policy.

Access rules, processor requirements, operation order, UI visibility, and promotion are profile
facts. They never appear in a family region declaration.

## Reporting

Compilation resolves every logical view to one checked address-space range and records the physical
region chain. Mutation reports show operation id, resolved range, region ids, owner/kind, overlap
policy, and reason. Processor changes are additionally checked against compiled allowed write views.
