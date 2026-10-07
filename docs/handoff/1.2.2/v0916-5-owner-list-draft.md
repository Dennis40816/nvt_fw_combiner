# Predecessor coverage and difference list

Status: diagnostic rehearsal, not a report of record

Mode: v0916-1x; candidate: 1.2.2; baseline: v0.9.16; result: invalid.
certification: none; terminal: false

Candidate commit: 1a5dd0d614033e49ac2e9659ff3d8df62ed94ce0
Result deterministic SHA-256: ff7f09eba9f9ce3177e5dd5cac2860a6ac3dbc44a8dbf4403d0d87e6b4eb48bd

此清單未重新比較 bytes，也不核准 gap 或差異。宣告引用及裁定沿用結果；範圍是觀察值，不擴大核准邊界。
正式證據與本版 owner 核准仍須另行確認；過去版本的 gap 核准不沿用。

Milestone: 1.2.0-release-approval

歷史模式每個 plan route 是一個比較單位；proofKind 說明其情境。

## Coverage

consistent: 35; inconsistent: 0; invalid: 2; notCovered: 27

| Route | Scenario | Verdict | 情境／未比較原因 | Baseline | Candidate |
| --- | --- | --- | --- | --- | --- |
| route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | consistent（符合該項證明） | tp-prefix-transitive | — | output |
| route-7-nt51917-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | route-7-nt51917-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51917-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | route-7-nt51917-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51917-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | route-7-nt51917-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51917-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | route-7-nt51917-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k | route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51919-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | route-7-nt51919-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k | route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k | route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash | route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51923-15-ctrlram-replace-4-1-ic-41-nt51923-ctrlram-fw141-single-tp-work-240k | route-7-nt51923-15-ctrlram-replace-4-1-ic-41-nt51923-ctrlram-fw141-single-tp-work-240k | consistent（符合該項證明） | tp-prefix-transitive | — | output |
| route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash | route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-43-nt51923-ctrlram-fw141-cascade3-tp-work-240k | route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-43-nt51923-ctrlram-fw141-cascade3-tp-work-240k | consistent（符合該項證明） | tp-prefix-transitive | — | output |
| route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k | route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k | route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k | route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k | route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k | route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k | route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k | route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k | route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k | route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51927-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | route-7-nt51927-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k | consistent（符合該項證明） | tp-prefix-transitive | — | output |
| route-7-nt51927-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | route-7-nt51927-15-ctrlram-replace-4-2-ic-40-nt51927-ctrlram-fw132-twochip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51927-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | route-7-nt51927-15-ctrlram-replace-4-2-ic-42-nt51927-ctrlram-fw132-twochip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51927-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | route-7-nt51927-15-ctrlram-replace-4-3-ic-42-nt51927-ctrlram-fw140-threechip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51927-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | route-7-nt51927-15-ctrlram-replace-4-3-ic-44-nt51927-ctrlram-fw140-threechip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k | route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-1-ic-39-nt51928-ctrlram-fw141-single-full-flash | route-7-nt51928-15-ctrlram-replace-4-1-ic-39-nt51928-ctrlram-fw141-single-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-1-ic-41-nt51928-ctrlram-fw141-single-tp-work-212k | route-7-nt51928-15-ctrlram-replace-4-1-ic-41-nt51928-ctrlram-fw141-single-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-2-ic-40-nt51928-ctrlram-fw132-twochip-full-flash | route-7-nt51928-15-ctrlram-replace-4-2-ic-40-nt51928-ctrlram-fw132-twochip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-2-ic-42-nt51928-ctrlram-fw132-twochip-tp-work-212k | route-7-nt51928-15-ctrlram-replace-4-2-ic-42-nt51928-ctrlram-fw132-twochip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-3-ic-42-nt51928-ctrlram-fw140-threechip-full-flash | route-7-nt51928-15-ctrlram-replace-4-3-ic-42-nt51928-ctrlram-fw140-threechip-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51928-15-ctrlram-replace-4-3-ic-44-nt51928-ctrlram-fw140-threechip-tp-work-212k | route-7-nt51928-15-ctrlram-replace-4-3-ic-44-nt51928-ctrlram-fw140-threechip-tp-work-212k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k | route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51929-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | route-7-nt51929-15-ctrlram-replace-6-2-8-ic-39-nt51929-ctrlram-fw1x-cascade-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k | route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k | route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51932-15-ctrlram-replace-4-1-ic-38-nt51932-ctrlram-fw1x-single-full-flash | route-7-nt51932-15-ctrlram-replace-4-1-ic-38-nt51932-ctrlram-fw1x-single-full-flash | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash | route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k | route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k | route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k | route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-512k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k | route-7-nt51950-14-standard-merge-13-selector-free-28-nt51950-standard-merge-1024k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work | route-7-nt51950-15-ctrlram-replace-4-1-ic-36-nt51950-ctrlram-fw200-single-tp-work | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash | route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work | route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work | consistent（符合該項證明） | exact-output-with-approved-semantic-correction | output | output |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash | route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash | invalid（執行或證據無效） | canonical-binding-not-applicable-to-v0916 | invalid at preview: profile.v2.compile.map-selection-invalid | output |
| route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k | route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k | invalid（執行或證據無效） | exact-output | invalid at preview | output |
| route-7-nt51950-8-ab-merge-9-2-plus-ic-22-nt51950-ab-merge-1024k | route-7-nt51950-8-ab-merge-9-2-plus-ic-22-nt51950-ab-merge-1024k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k | route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-256k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k | route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k | route-7-nt51951-14-standard-merge-13-selector-free-28-nt51951-standard-merge-1024k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work | route-7-nt51951-15-ctrlram-replace-4-1-ic-36-nt51951-ctrlram-fw200-single-tp-work | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash | route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash | consistent（符合該項證明） | exact-output | output | output |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work | route-7-nt51951-15-ctrlram-replace-4-2-ic-36-nt51951-ctrlram-fw1x-cascade-tp-work | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash | route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash | consistent（符合該項證明） | exact-output-with-approved-semantic-correction | output | output |
| route-7-nt51951-8-ab-merge-13-selector-free-22-nt51951-ab-merge-1024k | route-7-nt51951-8-ab-merge-13-selector-free-22-nt51951-ab-merge-1024k | not-covered（未比較） | 無 canonical 認證案例；plan 列為 not-covered | — | — |

候選版 published routes 補充目錄：只按 routeId 列出，不推定 renamed route；不作為該次執行的 authority。

| Route | Scenario | 未比較原因 |
| --- | --- | --- |
| route-7-nt51919-15-ctrlram-replace-4-1-ic-21-nt51919-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51919-15-ctrlram-replace-6-2-8-ic-21-nt51919-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51929-15-ctrlram-replace-4-1-ic-21-nt51929-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51929-15-ctrlram-replace-6-2-8-ic-21-nt51929-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51932-15-ctrlram-replace-4-1-ic-21-nt51932-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51932-15-ctrlram-replace-6-2-8-ic-21-nt51932-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51950-15-ctrlram-replace-4-1-ic-21-nt51950-ab-merge-512k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51950-15-ctrlram-replace-4-2-ic-22-nt51950-ab-merge-1024k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-maps | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51950-8-ab-merge-9-2-plus-ic-23-nt51950-ab-cascade-maps | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51951-15-ctrlram-replace-4-1-ic-22-nt51951-ab-merge-1024k | — | outside-mode（不在本模式 plan route 集合） |
| route-7-nt51951-15-ctrlram-replace-4-2-ic-22-nt51951-ab-merge-1024k | — | outside-mode（不在本模式 plan route 集合） |

## Differences and rejected inputs

所有範圍採具名 address space 的半開區間；precursor 為前置 Standard Merge 輸出。

### route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work

已引用 amendment/approvedSemanticCorrections（裁定仍為 consistent）；consistent（符合該項證明）。

- baseline: output
  - output: size 225280; SHA-256 cfae15911aac4ef641eed9dc95acdb08bb69e988284b8753ad0528f3ed44e47e
- candidate: output
  - output: size 225280; SHA-256 a239645dd6e2527af34934e20ef7399ecb9d748aca32ce9ed4b829b94b8fc643
- output-image 不同 bytes：2816；範圍數：5；range-list SHA-256：abbdc6e5bde94f57bf9ee5b469b267396aa56965f636834b063dd95c13b76012。
  - output-image [41244, 41248)
  - output-image [41264, 41268)
  - output-image [185384, 185388)
  - output-image [185404, 185408)
  - output-image [211728, 214528)
- 結果綁定文件的完整宣告邊界（不代表本次已重現；仍看結果裁定）：
  - 不同 bytes：2816
    - output-image [41244, 41248)
    - output-image [41264, 41268)
    - output-image [185384, 185388)
    - output-image [185404, 185408)
    - output-image [211728, 214528)
  - ownerDecision: owner-decision:2026-09-26:nt51950-cascade-tp-work-diff-nf-preservation-is-correct
  - boardDecision: 1.1.12 board decision 12
  - scope: exactly-these-values
  - baselineOutput: size 225280; SHA-256 cfae15911aac4ef641eed9dc95acdb08bb69e988284b8753ad0528f3ed44e47e
  - candidateOutput: size 225280; SHA-256 a239645dd6e2527af34934e20ef7399ecb9d748aca32ce9ed4b829b94b8fc643

### route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash

已引用 amendment/baselineNotApplicable（裁定仍為 invalid）；invalid（執行或證據無效）。

- baseline: invalid at preview: profile.v2.compile.map-selection-invalid
  - precursor: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
- candidate: output
  - output: size 524288; SHA-256 1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd
  - precursor: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
- 結果綁定文件的完整宣告邊界（不代表本次已重現；仍看結果裁定）：
  - ownerDecision: owner-decision:2026-09-26:nt51950-cascade-full-flash-binding-not-applicable-to-v0916
  - boardDecision: 1.1.12 board decision 62
  - scope: this-canonical-binding-only
  - baseline rejection: preview; issueCodes: ['profile.v2.compile.map-selection-invalid']
  - expectedBaseline precursorOutput: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
  - expectedCandidate precursorOutput: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
  - expectedCandidate output: size 524288; SHA-256 1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd
  - canonical binding: {'evidenceCaseId': 'nt51950-cascade2-geometry-nt51951-auto-prj-599-alias', 'inputCaseId': 'nt51951-fw200-cascade2-auto-prj-599-20260731', 'precursorMapVariant': 'nt51950-standard-merge-512k'}
- 無效／不一致原因碼：PREDECESSOR_REPORT_INVALID

### route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k

無可接受差異裁定；invalid（執行或證據無效）。

- baseline: invalid at preview
- candidate: output
  - output: size 524288; SHA-256 d18db8dc02ab4ff52cb17b4b3b3b90f99047c9d1acd2a5c23627197cf32f8650
- 無效／不一致原因碼：PREDECESSOR_REPORT_INVALID

### route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash

已引用 plan/approvedSemanticCorrections（裁定仍為 consistent）；consistent（符合該項證明）。

- baseline: output
  - output: size 524288; SHA-256 7d657a3d0abc2cc6779e759c17567b40740de95235bf6d1e71c147d815edcca2
  - precursor: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
- candidate: output
  - output: size 524288; SHA-256 1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd
  - precursor: size 524288; SHA-256 ff9ad012454cab3d9d76d7270dfde7b402d23c2d0376ba6776cd1a242144aa32
- output-image 不同 bytes：2816；範圍數：5；range-list SHA-256：abbdc6e5bde94f57bf9ee5b469b267396aa56965f636834b063dd95c13b76012。
  - output-image [41244, 41248)
  - output-image [41264, 41268)
  - output-image [185384, 185388)
  - output-image [185404, 185408)
  - output-image [211728, 214528)
- 結果綁定文件的完整宣告邊界（不代表本次已重現；仍看結果裁定）：
  - 不同 bytes：2816
    - output-image [41244, 41248)
    - output-image [41264, 41268)
    - output-image [185384, 185388)
    - output-image [185404, 185408)
    - output-image [211728, 214528)
  - ownerDecision: owner-decision:2026-08-28:nt51951-diff-nf-preservation-is-correct
  - baselineOutput: size 524288; SHA-256 7d657a3d0abc2cc6779e759c17567b40740de95235bf6d1e71c147d815edcca2
  - candidateOutput: size 524288; SHA-256 1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd

## Failures and coverage changes

- route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash: PREDECESSOR_REPORT_INVALID — PARITY_REPORT_RANGE_INVALID
- route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k: PREDECESSOR_REPORT_INVALID — later work-space read outside every processor allowed write range

## Informational differences

這些欄位沿用結果的 informational 分類，不是 byte／acceptance 差異。

- route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k: capability-fingerprint; baseline=2017499fd34783c0db185b3128c0e3b021da5b011d54efcb5ae1f3299d2a4be8; candidate=c6abe2cdfa6a68533434352564bf6dce9efe52c2e7192eed792c532c269893d6
- route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k: map-id; baseline=None; candidate=nt51927-standard-merge-256k
- route-7-nt51917-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k: operation-projection-sha256; baseline=4ebb970d82a8cd9617ca7e39093b7837fa20d1c5cfa736f81410acca89b53b6b; candidate=7c160b7c084c3594f1cabc794c35edd1719497457ae97b1c93a495e42f19c86e
- route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: capability-fingerprint; baseline=98e2bbc0e57be9c3508b50a399cee244b1c9d90349563cf98b3a83fd471b0331; candidate=0b417aafabc34386549d3f243c79a6684cb4bfd9f4c6a87e15fd98bd8b9f115d
- route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: issue-codes; baseline=["DP_SIZE_WARNING","input.address-space.truncated"]; candidate=["input.address-space.truncated"]
- route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: map-id; baseline=None; candidate=nt51927-ctrlram-fw141-single-full-flash
- route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: operation-projection-sha256; baseline=b937a042b7d618548eda0fa8d258a2639e2b9a0e777c8097d64aef708cab8764; candidate=479e4e36797be8164c94d43f927d0447aa1f93791500bfc66badbc86c225316b
- route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k: capability-fingerprint; baseline=e9df8c17b750b64b9eb91eb095c97e62f663e7957f86cd3d01cddceafc589b03; candidate=eed9dd921b7caf06560838aee1f44f81429136efbbb58b93f4ae34eca553c1b0
- route-7-nt51919-14-standard-merge-13-selector-free-27-nt51919-standard-merge-256k: map-id; baseline=None; candidate=nt51919-standard-merge-256k
- route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: capability-fingerprint; baseline=d38c981439e034bd1e2008d631030ac508f266af0647537a90ccabee9da8756f; candidate=6152a4862dca264b570b69386f5535d097dc41f395120bfe34acd755ebc9786e
- route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: issue-codes; baseline=["DP_SIZE_WARNING","input.address-space.truncated"]; candidate=["input.address-space.truncated"]
- route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: map-id; baseline=None; candidate=nt51929-ctrlram-fw200-single-full-flash
- route-7-nt51919-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: operation-projection-sha256; baseline=863799bfda34ce68d9133d0711d6d9316960ba0fbc45f00d35cf88eaa5b0feb8; candidate=b4181d3fc367f26a4d70e00c641f6f478f9f564e9292c716a43f21926116ecb9
- route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k: capability-fingerprint; baseline=f3f7a8cd954928162c864851f1369781ea5a680a793f6b10e607b325b06e01fa; candidate=491497607c926c4f5e8263c04e896f1ded2902d18bc67e7b68a2c667939ad0de
- route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k: map-id; baseline=None; candidate=nt51919-ab-merge-512k
- route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k: operation-projection-sha256; baseline=a9125873e07e0f8af9a03f0c4418a484d78231d94c815fe8e593c8ef58cfae58; candidate=95f91e96a59bcd4ba346ce34ec4096a7e84b645d5ea5619a5b8bfa75c643c666
- route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k: capability-fingerprint; baseline=558f07b6a63936805dde4847b9f539acdc069a274c5b0f180661b817d84da508; candidate=8463ee50bda78dfa24a6ddb4c8d46efa67654f595023cd3f658e7f029c8a1437
- route-7-nt51923-14-standard-merge-13-selector-free-27-nt51923-standard-merge-256k: map-id; baseline=None; candidate=nt51923-standard-merge-256k
- route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash: capability-fingerprint; baseline=06cd3a7b22fd50e09906d972a00e1cc8b1d5393ef456f47731296294fc32fc3d; candidate=90a923b0d931f7c431eba78bbe01bbbc4f7997672ff90391835dee411fdd2722
- route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash: map-id; baseline=None; candidate=nt51923-ctrlram-fw141-single-full-flash
- route-7-nt51923-15-ctrlram-replace-4-1-ic-39-nt51923-ctrlram-fw141-single-full-flash: operation-projection-sha256; baseline=16be7cfd9a1aa9db5269ac8588889b69a5c8579cd769eb08925756a3e060ee5d; candidate=56406501820e21f6b2f5b7607761d04ca59430be46abd7d617ba9eb7c57f127a
- route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash: capability-fingerprint; baseline=911c2a3b066ec3a86b4102e7012c0c4f361d685a2b5082d8e6006404ede25111; candidate=8236e882d335cc5ddc7a5eed7d8b1a56d87ba5136963951949077bbc054bb0db
- route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash: map-id; baseline=None; candidate=nt51923-ctrlram-fw141-cascade3-full-flash
- route-7-nt51923-15-ctrlram-replace-9-2-plus-ic-41-nt51923-ctrlram-fw141-cascade3-full-flash: operation-projection-sha256; baseline=8c5943dc88f17c538e1b657c1be917772890f8ce8da14489b661e7a4f0f161b2; candidate=55457e8e985440b8555f8375dc8b5451781413cd5b7030ad5316d5f9387c2b49
- route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k: capability-fingerprint; baseline=44efd697017e0a4c81b387e7fed3f6deebc61efa20641fec6172aa35e1e1a3d4; candidate=c634303247bd730a0c9f6096202ae97593ed84d1af148e9eaae127d778fc8e44
- route-7-nt51926-14-standard-merge-13-selector-free-27-nt51926-standard-merge-256k: map-id; baseline=None; candidate=nt51926-standard-merge-256k
- route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k: capability-fingerprint; baseline=6f0a3b9eb927e3d993dcb875a0bc9c0570523702fe0b4475a77064798c336e6c; candidate=97246f5021276704e48ef183324741b55e3c809f39ac9b61266fc3174db80f40
- route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k: map-id; baseline=None; candidate=nt51926-ctrlram-fw141-tp-work-240k
- route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k: capability-fingerprint; baseline=0a01582d0488c44a2b73edb0ba1f98068bca504588c5d840a3f9518051a71321; candidate=9e40e19b1a68ba1896b93bac70c097ea1724dd04935a4882812779f477650e21
- route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k: map-id; baseline=None; candidate=nt51926-ctrlram-fw200-tp-work-240k
- route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k: capability-fingerprint; baseline=132fc2fa3dbac941c4e608d8cf081a90cce27820711ded756b978cf609a698d2; candidate=7f7cb72897b5f0f7e36d2802217d0bf59330ed9f61eb6969e05f541e72438198
- route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k: map-id; baseline=None; candidate=nt51926-ctrlram-fw141-full-flash-256k
- route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k: capability-fingerprint; baseline=33e731f61b088331e178cf46a7474d5544525f74a0cb48d412bf4d4fa31b1412; candidate=0228847c9246c5c2dc251bb508c18883d1075ddabcb9cb4a9138b6d3a5106543
- route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k: map-id; baseline=None; candidate=nt51926-ctrlram-fw200-full-flash-256k
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k: capability-fingerprint; baseline=58036aadb80202f5488687c42df523d5a6b9832b353d9c92d8c94cd623dfb3cc; candidate=f50fb11ae37da5190a4b32fd566edd729f1a56d65cf168b501ece8d7529815c5
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k: map-id; baseline=None; candidate=nt51926-ctrlram-fw141-tp-work-240k
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k: capability-fingerprint; baseline=3c70e5504cbc0d2eaae1541d2c9afcc51e7c927a1416b3f1ef92879abed42784; candidate=3fe3944dbb9e0d1211f9ba85a555b49d2e67f6d1323c15cdb32e2c516c5d14ab
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k: map-id; baseline=None; candidate=nt51926-ctrlram-fw200-tp-work-240k
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k: capability-fingerprint; baseline=0041ea6366adfd864fd3d5415bf6b5ee66c796adcab6685731efa87efd8bf8a2; candidate=48660dede1e09eb13d45d13f0be47097bc18ac779deb3fbb5b27b484f0d127dc
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k: map-id; baseline=None; candidate=nt51926-ctrlram-fw141-full-flash-256k
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k: capability-fingerprint; baseline=1ea9ced1f12352c666ee855645649dd841674ac0b113a15baaa0f2a7c3520e7c; candidate=c37027ea9234db42926b16dd5e10ff51985abb58516915d994fcfeec73921efb
- route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k: map-id; baseline=None; candidate=nt51926-ctrlram-fw200-full-flash-256k
- route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k: capability-fingerprint; baseline=8cdc3ae28aa2fc2155b8a8e848575b1826994214d7011db0b4115d0d5270d569; candidate=a141bac198e9fcd58f40827942b2377187b7022daadd4d1db1bfaf71adbd69d2
- route-7-nt51927-14-standard-merge-13-selector-free-27-nt51927-standard-merge-256k: map-id; baseline=None; candidate=nt51927-standard-merge-256k
- route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: capability-fingerprint; baseline=c1907cb800037b88d6f18be099595ab56008c90d0b8e62e8b40d6590f6e93939; candidate=5744d87fdf0304845f081f6d779c25fe66873d77df2eec66c1cdaee2323112c5
- route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: issue-codes; baseline=["DP_SIZE_WARNING","input.address-space.truncated"]; candidate=["input.address-space.truncated"]
- route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: map-id; baseline=None; candidate=nt51927-ctrlram-fw141-single-full-flash
- route-7-nt51927-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash: operation-projection-sha256; baseline=226fca7850c67a1eb0b9d292630c6cd8898258f95b973eedd20a725694e153b9; candidate=12c5c6406110742df4998faaae55ee05ee2d5e66e0f42b023a97edbbf85718db
- route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k: capability-fingerprint; baseline=3b4b7b12f781fae73d97d74f47fbe9d798e39d6dea252e167285ebb2015860be; candidate=3d7670f4641f1a63bb4a2dbac0444ecf6dd09e4a32501cb5cb77464752fda734
- route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k: map-id; baseline=None; candidate=nt51929-standard-merge-256k
- route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: capability-fingerprint; baseline=d323ec15bd533ca1b168e1d2c614e2520007b387b23841dddb6bfce27245206e; candidate=6a27d1ce039ca10f4d63a0d623a25742eac69964a969d02a7b2faa93ee121411
- route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: issue-codes; baseline=["DP_SIZE_WARNING","input.address-space.truncated"]; candidate=["input.address-space.truncated"]
- route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: map-id; baseline=None; candidate=nt51929-ctrlram-fw200-single-full-flash
- route-7-nt51929-15-ctrlram-replace-4-1-ic-39-nt51929-ctrlram-fw200-single-full-flash: operation-projection-sha256; baseline=1d4d3bf4c828dea0cfa7c4233abe6603237b7193bf737790dc83e05b6d7704b9; candidate=d1496c99b09483a9ef838f8142b5848574e0c8c525752bfa28d33f8f17485af9
- route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k: capability-fingerprint; baseline=b9784dc0c9de7c7d18ba5480290429bbcaee811714eef89d9cb8e833138a8e4c; candidate=0aa7fa08c269960f48d14ee2437e1e52820ddcf46197cdacb1156d9ece236975
- route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k: map-id; baseline=None; candidate=nt51929-ab-merge-512k
- route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k: operation-projection-sha256; baseline=a9125873e07e0f8af9a03f0c4418a484d78231d94c815fe8e593c8ef58cfae58; candidate=95f91e96a59bcd4ba346ce34ec4096a7e84b645d5ea5619a5b8bfa75c643c666
- route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k: capability-fingerprint; baseline=0244444eea271300afbe531ee1b2694fcc00c4ae83c4ed023d42cba4e8798ca6; candidate=a4d1fe24c6eccbd0096205592dbd78956c640ba45c4ad34ae4cbb58b9dad6e7d
- route-7-nt51932-14-standard-merge-13-selector-free-27-nt51932-standard-merge-256k: map-id; baseline=None; candidate=nt51932-standard-merge-256k
- route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash: capability-fingerprint; baseline=621bce8ad86ffe3e01d766cec43017afa09efc24b7086edab41d37dc49516719; candidate=cf7cf3b955be5c48b72dce196e3d9dda4b956061ee1495d3f495e34ee0ce5491
- route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash: map-id; baseline=None; candidate=nt51932-ctrlram-fw200-cascade-full-flash
- route-7-nt51932-15-ctrlram-replace-6-2-8-ic-40-nt51932-ctrlram-fw200-cascade-full-flash: operation-projection-sha256; baseline=f95b39f61cd72fe84836ffb7d2ac6dd5a36a5f7abeed36604736c7a4414f5d19; candidate=13e5168664e5602c80f9c2d5e1f1bc0a046f4f88f3eda816c026368f3147b9f3
- route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k: capability-fingerprint; baseline=43a9905e024c0087882b7efbe62af3fe59985047d90ad9e8bf5ce20c04563a39; candidate=2489883635c5c7eb92294c0474a09286e73eecff0f88dd02c016244547d46629
- route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k: map-id; baseline=None; candidate=nt51932-ab-merge-512k
- route-7-nt51932-8-ab-merge-13-selector-free-21-nt51932-ab-merge-512k: operation-projection-sha256; baseline=a9125873e07e0f8af9a03f0c4418a484d78231d94c815fe8e593c8ef58cfae58; candidate=95f91e96a59bcd4ba346ce34ec4096a7e84b645d5ea5619a5b8bfa75c643c666
- route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k: capability-fingerprint; baseline=7b068f21609708c2c00572e742fb76de37bdc6c51b38beeef63b7d668e5d1e4e; candidate=21adc7a96cf26d1125ccfe91e4cd6b6aacffe0d63a96636a6663e6d4cbf6895d
- route-7-nt51950-14-standard-merge-13-selector-free-27-nt51950-standard-merge-256k: map-id; baseline=None; candidate=nt51950-standard-merge-256k
- route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash: capability-fingerprint; baseline=ce8ba9efc29546cee166dca56216c8fcf71f5d38f74d8f52da8fe1315e471795; candidate=2414a7934383934453ffddf854258e295980b81e19d94d2ba7c1feb012bc01bd
- route-7-nt51950-15-ctrlram-replace-4-1-ic-39-nt51950-ctrlram-fw200-single-full-flash: map-id; baseline=None; candidate=nt51950-ctrlram-fw200-single-full-flash
- route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work: capability-fingerprint; baseline=032458580988af8571d65125bf3858f70401e901a81ccba6633d9ec636a92980; candidate=1707356bdeed629428b119ad077ec1b2f7b494053e534ebaf4e459c43d5c8f61
- route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work: map-id; baseline=None; candidate=nt51950-ctrlram-fw1x-cascade-tp-work
- route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work: operation-projection-sha256; baseline=92d75831259e632c6d7d8e82e2f90ae71b8cabe0de852314caf9febaa52e65ba; candidate=c1cd8cc70840bb81065f1e95ac954c2faf4230b85646624f9691ace4c24ff912
- route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash: capability-fingerprint; baseline=None; candidate=de232d70378c1d9dd16cc81678c916dcbfb981a2176fdd818a219d3855ddce1b
- route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash: issue-codes; baseline=["profile.v2.compile.map-selection-invalid"]; candidate=["input.address-space.truncated"]
- route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash: map-id; baseline=None; candidate=nt51950-ctrlram-fw1x-cascade-full-flash
- route-7-nt51950-15-ctrlram-replace-4-2-ic-39-nt51950-ctrlram-fw1x-cascade-full-flash: operation-projection-sha256; baseline=8cbf2dee0817255f596e6a91b466a8d82729e68dfb4de31c7bfd1361324e2a8e; candidate=c1cd8cc70840bb81065f1e95ac954c2faf4230b85646624f9691ace4c24ff912
- route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k: capability-fingerprint; baseline=d06d407a795f36e29dd055d2bc5fe99046aa2c6059150e99b77a0906d2f8302d; candidate=05856930cfdb546c305f16dfd55da565ed05c42f979bae3679838bbc9bb7db34
- route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k: map-id; baseline=None; candidate=nt51950-ab-merge-512k
- route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k: operation-projection-sha256; baseline=8623384539f24dbb2e0b4bc1b829dcf058da0a8a511509478392971f4591b5e2; candidate=73869ea5f2ec67a6d9ec1068c721a53917328724184807405505347ea7d68371
- route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k: capability-fingerprint; baseline=35ed6c70c1b03e5fcc2ce272497d746b524e935ec356af1ce1bda285e520517a; candidate=f7e6cdb8fbbfb5e5978f1952ffce56bae2af7bfef222a1c9119b19b82ba91463
- route-7-nt51951-14-standard-merge-13-selector-free-27-nt51951-standard-merge-512k: map-id; baseline=None; candidate=nt51951-standard-merge-512k
- route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash: capability-fingerprint; baseline=d632adb4c389091a2107e0325a7319c063664acdbb721d281e378b94d3c170c8; candidate=6d44ea3f0133827d40f3ba4ae55aee4a9e673c8ea4ba7b143164437e5eeaef36
- route-7-nt51951-15-ctrlram-replace-4-1-ic-39-nt51951-ctrlram-fw200-single-full-flash: map-id; baseline=None; candidate=nt51951-ctrlram-fw200-single-full-flash
- route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash: capability-fingerprint; baseline=4c48de741908c6e86f897e953fbe0d8c446633163a4403a4d96ed3dd517dff54; candidate=50682dbd6a10a8c612feb5c753b1c6010b9400c858972a94e557af4c069a8219
- route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash: map-id; baseline=None; candidate=nt51951-ctrlram-fw1x-cascade-full-flash
- route-7-nt51951-15-ctrlram-replace-4-2-ic-39-nt51951-ctrlram-fw1x-cascade-full-flash: operation-projection-sha256; baseline=d0d2d2577df05c20a2fb38b8dc507d65489a46102af40a1098414d6e299df7e8; candidate=9c2415fac9e8d87c316c83183df2d52ec636ca96cb7e51c76d6623470b3f5488

Result file SHA-256: ca50a9aeb8a96af04cdbfe2b5b0fa0056bf525f4b141c6b8eb5b79617c6914a5
Supplemental catalogue SHA-256: a3ad08440076fb6b8b840ba64a6fbe0ccadb4345530ba1db09ab0b4f0fa671d4
Bound amendment SHA-256: eeb29d35d5769bb414c4a488d5a26c92ab9398b430b99fd40af902fb14b92742
Bound plan SHA-256: e8d6dcaec9e5fd3335f81012960be65d459d09668b81f156e32be9347658efe0
