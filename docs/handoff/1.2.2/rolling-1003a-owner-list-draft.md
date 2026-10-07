# Predecessor coverage and difference list

Status: diagnostic rehearsal, not a report of record

Mode: rolling; candidate: 1.2.2; baseline: v1.2.1; result: blocked.
certification: none; terminal: false

Candidate commit: 1a5dd0d614033e49ac2e9659ff3d8df62ed94ce0
Result deterministic SHA-256: bf16499ee03807ffc7ca3f6d4f7ef57adfb3eba0b425e4f47e553ffd5b8e30c1

此清單未重新比較 bytes，也不核准 gap 或差異。宣告引用及裁定沿用結果；範圍是觀察值，不擴大核准邊界。
正式證據與本版 owner 核准仍須另行確認；過去版本的 gap 核准不沿用。

## Coverage

acceptedGaps: 0; coveredRoutes: 37; debtSetInUniverse: 26; pendingAcceptedGaps: 11; scenarios: 39; universe: 74

| Route | Scenario | Verdict | 情境／未比較原因 | Baseline | Candidate |
| --- | --- | --- | --- | --- | --- |
| route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | NT51917:standard-merge:selector-free:nt51927-standard-merge-256k:nt51917-standard-merge-gen-flash-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | NT51917:ctrlram-replace:1-ic:nt51927-ctrlram-fw141-single-full-flash:nt51917-fw141-single-nt51927-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | NT51917:ctrlram-replace:1-ic:nt51927-ctrlram-fw141-single-tp-work-212k:nt51917-fw141-single-nt51927-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51917-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51917-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51917-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51917-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k | NT51919:standard-merge:selector-free:nt51919-standard-merge-256k:nt51919-standard-merge-gen-flash-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51919-15-ctrlram-replace-4-1-ic-21-nt51919-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | NT51919:ctrlram-replace:1-ic:nt51929-ctrlram-fw200-single-full-flash:nt51919-fw200-single-nt51929-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51919-15-ctrlram-replace-6-2-8-ic-21-nt51919-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51919-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k | NT51919:ab-merge:selector-free:nt51919-ab-merge-512k:nt51919-ab-t05-d06-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k | NT51923:standard-merge:selector-free:nt51923-standard-merge-256k:nt51923-gen-flash | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash | NT51923:ctrlram-replace:1-ic:nt51923-ctrlram-fw141-single-full-flash:nt51923-fw141-single-auto-prj-662-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51923-15-ctrlram-replace-4-1-ic-41-nt51923-ctrlram-fw141-single-tp-work-240k | NT51923:ctrlram-replace:1-ic:nt51923-ctrlram-fw141-single-tp-work-240k:nt51923-fw141-single-auto-prj-662-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash | NT51923:ctrlram-replace:2-plus-ic:nt51923-ctrlram-fw141-cascade3-full-flash:nt51923-fw141-cascade3-auto-prj-734-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-43-nt51923-ctrlram-fw141-cascade3-tp-work-240k | NT51923:ctrlram-replace:2-plus-ic:nt51923-ctrlram-fw141-cascade3-tp-work-240k:nt51923-fw141-cascade3-auto-prj-734-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k | NT51926:standard-merge:selector-free:nt51926-standard-merge-256k:nt51926-gen-flash | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k | NT51926:ctrlram-replace:1-ic:nt51926-ctrlram-fw141-tp-work-240k:nt51926-fw141-single-auto-prj-747-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k | NT51926:ctrlram-replace:1-ic:nt51926-ctrlram-fw200-tp-work-240k:nt51926-fw200-single-auto-prj-597-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k | NT51926:ctrlram-replace:1-ic:nt51926-ctrlram-fw141-full-flash-256k:nt51926-fw141-single-auto-prj-747-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k | NT51926:ctrlram-replace:1-ic:nt51926-ctrlram-fw200-full-flash-256k:nt51926-fw200-single-auto-prj-597-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k | NT51926:ctrlram-replace:2-plus-ic:nt51926-ctrlram-fw141-tp-work-240k:nt51926-fw141-cascade2-auto-prj-597-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k | NT51926:ctrlram-replace:2-plus-ic:nt51926-ctrlram-fw200-tp-work-240k:nt51926-fw200-cascade3-auto-prj-597-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k | NT51926:ctrlram-replace:2-plus-ic:nt51926-ctrlram-fw141-full-flash-256k:nt51926-fw141-cascade2-auto-prj-597-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k | NT51926:ctrlram-replace:2-plus-ic:nt51926-ctrlram-fw200-full-flash-256k:nt51926-fw200-cascade3-auto-prj-597-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | NT51927:standard-merge:selector-free:nt51927-standard-merge-256k:nt51927-gen-flash | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | NT51927:ctrlram-replace:1-ic:nt51927-ctrlram-fw141-single-full-flash:nt51927-fw141-single-auto-prj-529-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51927-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | NT51927:ctrlram-replace:1-ic:nt51927-ctrlram-fw141-single-tp-work-212k:nt51927-fw141-single-auto-prj-529-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51927-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51927-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51927-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51927-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-1-ic-39-nt51928-ctrlram-fw141-single-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-1-ic-41-nt51928-ctrlram-fw141-single-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-2-ic-40-nt51928-ctrlram-fw132-twochip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-2-ic-42-nt51928-ctrlram-fw132-twochip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-3-ic-42-nt51928-ctrlram-fw140-threechip-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51928-15-ctrlram-replace-4-3-ic-44-nt51928-ctrlram-fw140-threechip-tp-work-212k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k | NT51929:standard-merge:selector-free:nt51929-standard-merge-256k:nt51929-gen-flash | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | NT51929:ctrlram-replace:1-ic:nt51929-ctrlram-fw200-single-full-flash:nt51929-fw200-single-auto-prj-594-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51929-15-ctrlram-replace-6-2-8-ic-21-nt51929-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51929-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k | NT51929:ab-merge:selector-free:nt51929-ab-merge-512k:nt51929-ab-t05-d06 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k | NT51932:standard-merge:selector-free:nt51932-standard-merge-256k:nt51932-gen-flash | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51932-15-ctrlram-replace-4-1-ic-21-nt51932-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51932-15-ctrlram-replace-4-1-ic-38-nt51932-ctrlram-fw1x-single-full-flash | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51932-15-ctrlram-replace-6-2-8-ic-21-nt51932-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash | NT51932:ctrlram-replace:2-8-ic:nt51932-ctrlram-fw200-cascade-full-flash:nt51932-fw200-cascade3-auto-prj-525-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k | NT51932:ab-merge:selector-free:nt51932-ab-merge-512k:nt51932-ab-t05-d06-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k | NT51950:standard-merge:selector-free:nt51950-standard-merge-256k:51950-dp-256k | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: synthetic-oracle | — | — |
| route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: synthetic-oracle | — | — |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-21-nt51950-ab-merge-512k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash | NT51950:ctrlram-replace:1-ic:nt51950-ctrlram-fw200-single-full-flash:nt51950-fw200-single-auto-prj-676-20260717 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-22-nt51950-ab-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work | NT51950:ctrlram-replace:2-ic:nt51950-ctrlram-fw1x-cascade-tp-work:nt51950-cascade2-geometry-nt51951-auto-prj-599-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash | NT51950:ctrlram-replace:2-ic:nt51950-ctrlram-fw1x-cascade-full-flash:nt51950-cascade2-geometry-nt51951-auto-prj-599-alias | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps | NT51950:ab-merge:1-ic:nt51950-ab-merge-maps:nt51950-ab-boe-d82t80 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps | NT51950:ab-merge:1-ic:nt51950-ab-merge-maps:nt51950-ab-hiway-d82t80 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps | NT51950:ab-merge:1-ic:nt51950-ab-merge-maps:nt51950-ab-osd-d03t02-20260924 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: synthetic-oracle | — | — |
| route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k | NT51951:standard-merge:selector-free:nt51951-standard-merge-512k:51951-dp-512k | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: synthetic-oracle | — | — |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-22-nt51951-ab-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash | NT51951:ctrlram-replace:1-ic:nt51951-ctrlram-fw200-single-full-flash:nt51951-fw200-single-auto-prj-695-20260718 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-22-nt51951-ab-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；待本版 owner 核准；evidenceKind: contract-only | — | — |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash | NT51951:ctrlram-replace:2-ic:nt51951-ctrlram-fw1x-cascade-full-flash:nt51951-fw200-cascade2-auto-prj-599-20260731 | equal（完整輸出相同） | inputRevision 1 | output | output |
| route-7-nt51951-8-ab-merge-13-selector-free-22-nt51951-ab-merge-1024k | — | not-covered（未比較） | 無 canonical 認證案例；歷史 debt set；evidenceKind: contract-only | — | — |

候選版 published routes 補充目錄：只按 routeId 列出，不推定 renamed route；不作為該次執行的 authority。

| Route | Scenario | 未比較原因 |
| --- | --- | --- |
| — | — | 補充目錄沒有其他 route |

## Differences and rejected inputs

所有範圍採具名 address space 的半開區間；precursor 為前置 Standard Merge 輸出。

結果未列出 byte 或 acceptance 差異。

## Failures and coverage changes

- route-7-nt51919-15-ctrlram-replace-4-1-ic-21-nt51919-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51919-15-ctrlram-replace-6-2-8-ic-21-nt51919-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51929-15-ctrlram-replace-6-2-8-ic-21-nt51929-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51932-15-ctrlram-replace-4-1-ic-21-nt51932-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51932-15-ctrlram-replace-6-2-8-ic-21-nt51932-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51950-15-ctrlram-replace-4-1-ic-21-nt51950-ab-merge-512k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51950-15-ctrlram-replace-4-2-ic-22-nt51950-ab-merge-1024k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51951-15-ctrlram-replace-4-1-ic-22-nt51951-ab-merge-1024k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval
- route-7-nt51951-15-ctrlram-replace-4-2-ic-22-nt51951-ab-merge-1024k: PREDECESSOR_COVERAGE_UNDISPOSED — pending gap approval

## Informational differences

這些欄位沿用結果的 informational 分類，不是 byte／acceptance 差異。

結果未列出 informational 差異。

Result file SHA-256: a2337cf2f0efc743485a5ba94425236fd7db3b4683da4c477485e6c499351059
Supplemental catalogue SHA-256: a3ad08440076fb6b8b840ba64a6fbe0ccadb4345530ba1db09ab0b4f0fa671d4
