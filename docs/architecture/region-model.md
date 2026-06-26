# Canonical Region Model

This document refines the region model used by Merge, Replace, General mapping, integrity processing, and UI access rules.

## Purpose

A `MemoryRegion` gives semantic meaning to a byte range. It is not just a visual rectangle. Region declarations drive:

- persona access rules;
- replacement authorization;
- general mapping validation;
- copy order validation;
- processor read/write authority;
- preview rendering;
- mutation reports;
- golden evidence review.

## Required fields and purpose

| Field | Purpose |
| --- | --- |
| `regionId` | Stable id used by operations, access rules, saved rules, reports, and tests. Must not change casually. |
| `parentRegionId` | Optional hierarchy. Example: `tp-header` can be child of `tp`; `dp-header` can be child of `dp`. |
| `addressSpaceId` | Names the address space where the range lives, such as `output-image`, `work-buffer`, `reference-base`, or a logical bank view. |
| `range` | Half-open byte range `{ start, length }`. Internally interpreted as `[start, start + length)`. |
| `role` | One primary product role for coarse display and grouping, such as `dp`, `tp`, `header`, `ctrlram`, `bank`, `reserved`. |
| `classificationTags` | Multi-tag semantic classification. This is where DP/TP header split, protected status, integrity read/write, and replaceability are expressed. |
| `atomicity` | Authoring granularity: `whole`, `partitioned`, or `explicit-mapping`. Used by Display/TP HW/TP FW/General workflows. |
| `writePolicy` | Baseline write policy: `forbidden`, `whole-only`, `declared-parts`, or `general-explicit`. |
| `alignment` | Minimum allowed start/length alignment for replacement/mapping. |
| `processorDependencyIds` | Processors that must run before/after this region is considered valid, such as CRC/Header transforms. |
| `compatibilityTags` | Optional tags used to match saved rules, input artifacts, or IC variants. |
| `owner` | Optional human/system ownership hint for review, such as `display`, `tp-hw`, `tp-fw`, `system`. |
| `description` | Human explanation for UI, reports, and review. |

## Header modeling rule

Do not use one undifferentiated `header` region when DP and TP have different semantics. Declare DP and TP headers separately.

Recommended layout:

```text
dp
  dp-header
  dp-payload

tp
  tp-header
  tp-ctrlram
  tp-fw
  tp-payload
```

A header region can still use role `header`, but its tags must show ownership:

```json
{
  "regionId": "tp-header-crc",
  "parentRegionId": "tp",
  "role": "header",
  "classificationTags": ["tp", "tp-header", "integrity-write", "protected"],
  "range": { "start": 41264, "length": 4 },
  "atomicity": "whole",
  "writePolicy": "forbidden",
  "processorDependencyIds": ["nfc.nt51950.header-crc-v1"]
}
```

DP header example:

```json
{
  "regionId": "dp-header-version",
  "parentRegionId": "dp",
  "role": "header",
  "classificationTags": ["dp", "dp-header", "version-token", "protected"],
  "range": { "start": 103, "length": 2 },
  "atomicity": "whole",
  "writePolicy": "forbidden"
}
```

## Persona access examples

Display Replace:

- may expose `dp` whole or declared DP partitions;
- may show `dp-header` read-only;
- treats `tp` as whole-only when TP replacement is offered;
- must not expose `tp-header` as an independent editable region unless a profile explicitly allows it.

TP HW Replace:

- may expose `tp-ctrlram` and approved CtrlRAM groups;
- treats DP as whole-only or hidden;
- keeps `tp-header` read-only unless the processor/tool owns it.

TP FW Replace:

- may expose non-CtrlRAM TP regions;
- hides or blocks `tp-ctrlram` by default;
- keeps DP whole-only or hidden.

General Replace:

- may expose explicit ranges only where `writePolicy = general-explicit` and no protected header/integrity range is crossed;
- never bypasses processor dependencies or protected regions.

## Region hierarchy rule

Parent regions may overlap child regions only as containment. Sibling overlaps are rejected unless explicitly declared by the profile contract and previewed.

## Preview and report rule

Every mutation report should include both raw byte range and region classification. Example:

```text
operationId: run-header-crc
targetRange: [0xA130, 0xA134)
regions: tp > tp-header-crc
tags: tp, tp-header, integrity-write, protected
```
