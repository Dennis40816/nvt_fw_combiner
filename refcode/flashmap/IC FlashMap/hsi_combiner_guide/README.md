# README.md

- [README.md](#readmemd)
  - [Supported Commands](#supported-commands)
    - [Required Commands for Generating a Complete fw.bin in Andes PostBuild](#required-commands-for-generating-a-complete-fwbin-in-andes-postbuild)
    - [Other Commands](#other-commands)

## Supported Commands

### Required Commands for Generating a Complete fw.bin in Andes PostBuild

- `NORMAL_MODE` (`CRC_Enable`/`CRC32_Enable`/`CRC_Disable`)

   NORMAL_MODE was the earliest supported command. Therefore, instead of using NORMAL_MODE, one of CRC_Enable, CRC32_Enable, or CRC_Disable is used.

  - MD
    - Phase 2 applications for all IC series
    - Phase 3 applications for ICs before NT51927
  - AUTO
    - NT51900/NT51902
    - NT51920/NT51922
    - NT51923
    - NT51925
    - NT51926
- `NT51927BADED_GEN_CRC_MODE`
  - MD
    - Phase 3 applications for ICs after NT51927
  - AUTO
    - NT51927/NT51928
- `NT51931BASED_NORMAL_MODE`
  - AUTO
    - NT51931
- `NT51930BASED_NORMAL_MODE`
  - AUTO
    - NT51930
- `NT51932BASED_NORMAL_MODE`
  - AUTO
    - NT51932/NT51929
- [`NT51932BASED_MERGE_AB_MODE`](NT51932BASED_MERGE_AB_MODE.md)
  - AUTO
    - NT51932/NT51929
- [`NT51950BASED_NORMAL_MODE`](NT51950BASED_NORMAL_MODE.md)
  - AUTO
    - NT51950/NT51951
- [`NT51950BASED_MERGE_AB_MODE`](NT51950BASED_MERGE_AB_MODE.md)
  - AUTO
    - NT51950/NT51951
- [`NT51928BBASED_NORMAL_MODE`](NT51928BBASED_NORMAL_MODE.md)
  - AUTO
    - NT51928B
  
### Other Commands

- `MERGE_MODE`
  - Used to merge multiple inputs.bin files into a single output.bin
- `NT36672ABASED_MERGE_BIN_AND_GEN_CRC_MODE`
  - Provides the same functionality as NORMAL_MODE, but removes the steps of parsing map.txt and handling overlays
  - This command is used for CombinerGUI ([MD_TL2-4224](https://jira.novatek.com.tw/browse/MD_TL2-4224))