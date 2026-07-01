# NT51932BASED_MERGE_AB_MODE

## Summary
This command performs an A/B code merge for NT51932-based firmware binaries. It reads two binary files, updates the **ILM start addr in bin**, the **DLM start addr in bin**, and the **DLM_DIFF start addr in bin**, and then produces a single merged output file.

## Parameters
- argv[0]: executable name (provided by the OS; do not supply)
- argv[1]: command name (fixed: NT51932BASED_MERGE_AB_MODE)
- argv[2]: a_code_bin — path to A binary (input)
- argv[3]: b_code_bin — path to B binary (input)
- argv[4]: output_bin — path for merged output (output)
- argv[5]: b_code_offset — offset (in bytes) where B is placed in the output

## Usage

Basic syntax:
```
./Combiner.exe NT51932BASED_MERGE_AB_MODE <a_code_bin> <b_code_bin> <output_bin> <b_code_offset>
```

Example:
```
# Place B at output offset 0x40000
./Combiner.exe NT51932BASED_MERGE_AB_MODE "A.bin" "B.bin" "merged.bin" 0x40000
```

## Flow Chart
```mermaid
%%{
    init: {
        "flowchart": {
            "wrappingWidth": "600"
        }
    }
}%%
flowchart TB

    Start([Start])
    P1[Read a_code.bin to 0x00000]
    P2[Read b_code.bin to **b_code_offset**]
    P3[Modify values at
       #40;**b_code_offset** + 0x7164#41; &amp;
       #40;**b_code_offset** + 0x7168#41; &amp;
       #40;**b_code_offset** + 0x716C#41;
    ]
    End([End])

    Start --> P1
    P1 --> P2
    P2 --> P3
    P3 --> End

```