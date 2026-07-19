# General Replace migration

先在 `Reply.md` 核准：

- release scope 的 IC／IC count；
- protected ranges；
- explicit-mapping safety envelope；
- overlap／alignment 規則；
- 哪些 TP／CtrlRAM-touching mapping 必須執行 Postbuild。

核准後，每一種代表性 TP／CtrlRAM-touching case 再提供：

```text
mapping_inputs/            # 保留實際輸入原檔名
expected_output/           # 完整最終 FlashCode
Reply.md                   # target start + length、IC/count、命令與同一次 run 證明
```

DP-only mapping 若不觸發 processor，仍必須 byte-exact；General Replace 的自由度不代表
可以越過 profile 核准的 firmware safety envelope。
