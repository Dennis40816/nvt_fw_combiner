## How to use?

- 點擊 clean.bat 清除資料夾內 bin，之後將 bin 檔放入，確保每個資料夾中只有一個 bin 檔
- 把 DP Code 放到 DP_AB 資料夾
- 把 TP A Code 放到 TPA 資料夾 
- 把 TP B Code 放到 TPB 資料夾 (不用自己改 Header)
- Output overall size 會跟 DP_AB bin size 一致，不再固定為 0x80000
- 如果輸出檔名已存在，會自動加上 _1、_2...，避免覆蓋舊檔

## CMD

- help: python main.py -h
- combine: python main.py --ic <IC>，例如 python main.py --ic 51929
- debug default: python main.py --ic <IC>，預設顯示 range/CRC/patch 細節
- summary only: python main.py --ic <IC> --debug 0
- debug: python main.py --ic <IC> --debug，等同 --debug 1
- trace debug: python main.py --ic <IC> --debug 2，額外顯示 CRC range hex dump
- list supported IC: python main.py --list-ic

## How to add new IC?

- 參考 ic_config.py 添加相關 offset 參數

## Notes

- clean.bat 會先要求確認，確認後才清除 DP_AB、TPA、TPB 內容

## Verification Status

- 51929: PASS
- 51932: Same as 51929 => PASS
- 51950: Not yet
- 51951: Not yet
