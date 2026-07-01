# Combiner.exe Parameter Reference

Combiner version: 1.13.0.0

## Common Rules

- `block_source_address`, `block_destination_address`: **hexadecimal** (parsed with `strtol(..., 16)`)
- `block_length`: **decimal** bytes (parsed with `strtol(..., 10)`)
- Maximum number of block groups: **20**
- CRC method string:
  - New modes: `CRC8` / `CRC32`
  - Legacy Normal Mode: `CRC_Enable` / `CRC32_Enable` / `CRC_Disable`

---

## argv[1] Summary

| argv[1] | Mode | map.txt required |
|---------|------|:---:|
| `CRC_Enable` | Normal Mode (CRC8) | Yes |
| `CRC32_Enable` | Normal Mode (CRC32) | Yes |
| `CRC_Disable` | Normal Mode (no CRC) | Yes |
| `MERGE_MODE` | Merge only, no CRC | No |
| `NT36672ABASED_MERGE_BIN_AND_GEN_CRC_MODE` | NT36672A merge + CRC | No |
| `NT51927BASED_GEN_CRC_MODE` | NT51927 CRC only (no merge) | No |
| `NT51931BASED_NORMAL_MODE` | NT51931 merge + CRC | Yes |
| `NT51930BASED_NORMAL_MODE` | NT51930 merge + CRC | Yes |
| `NT51932BASED_NORMAL_MODE` | NT51932 merge + CRC | Yes |
| `NT51932BASED_MERGE_AB_MODE` | NT51932 A/B code merge | No |
| `NT51950BASED_NORMAL_MODE` | NT51950 merge + CRC | Yes |
| `NT51950BASED_MERGE_AB_MODE` | NT51950 A/B code merge | No |
| `NT51928BBASED_NORMAL_MODE` | NT51928B merge + CRC | Yes |

---

## Mode Details

### 1. Normal Mode

```
Combiner.exe <CRC_Enable|CRC32_Enable|CRC_Disable> <fw_bin>
             <block1_bin> <block1_src_addr> <block1_dst_addr> <block1_len>
             [block2_bin block2_src_addr block2_dst_addr block2_len ...]
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | CRC mode | `CRC_Enable`, `CRC32_Enable`, `CRC_Disable` |
| [2] | Main FW bin (input and output) | file path |
| [3+N×4] | blockN bin | file path |
| [4+N×4] | blockN source offset | hex |
| [5+N×4] | blockN destination offset | hex |
| [6+N×4] | blockN length | decimal |

- argc must be **odd** and **>= 7**
- Requires `map.txt` in the same directory as `fw_bin`, or in its `output/` subdirectory

---

### 2. MERGE_MODE

```
Combiner.exe MERGE_MODE <output_bin>
             <binA> <binA_src_addr> <binA_dst_addr> <binA_len>
             [binB binB_src_addr binB_dst_addr binB_len ...]
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | `MERGE_MODE` |
| [2] | Output bin | file path |
| [3+N×4] | Input binN | file path |
| [4+N×4] | binN source offset | hex |
| [5+N×4] | binN destination offset | hex |
| [6+N×4] | binN length | decimal |

- No CRC calculation
- Does not require `map.txt`

---

### 3. NT36672ABASED_MERGE_BIN_AND_GEN_CRC_MODE

```
Combiner.exe NT36672ABASED_MERGE_BIN_AND_GEN_CRC_MODE <CRC_method> <output_bin> <fw_bin>
             <block1_bin> <block1_src_addr> <block1_dst_addr> <block1_len>
             [block2_bin block2_src_addr block2_dst_addr block2_len ...]
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | `NT36672ABASED_MERGE_BIN_AND_GEN_CRC_MODE` |
| [2] | CRC method | `CRC8`, `CRC32` |
| [3] | Output bin | file path |
| [4] | Main FW bin (input) | file path |
| [5+N×4] | blockN bin | file path |
| [6+N×4] | blockN source offset | hex |
| [7+N×4] | blockN destination offset | hex |
| [8+N×4] | blockN length | decimal |

- argc must be **odd** and **>= 9**
- Does not require `map.txt`

---

### 4. NT51927BASED_GEN_CRC_MODE

```
Combiner.exe NT51927BASED_GEN_CRC_MODE <CRC_method> <input_bin> <output_bin>
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | `NT51927BASED_GEN_CRC_MODE` |
| [2] | CRC method | `CRC8`, `CRC32` |
| [3] | Input bin | file path |
| [4] | Output bin | file path |

- argc must be exactly **5**
- Calculates CRC only; does not merge bins

---

### 5. NT51931BASED_NORMAL_MODE / NT51930BASED_NORMAL_MODE / NT51932BASED_NORMAL_MODE / NT51950BASED_NORMAL_MODE / NT51928BBASED_NORMAL_MODE

These five modes share the same parameter structure:

```
Combiner.exe <MODE> <CRC_method> <output_bin> <fw_bin>
             <block1_bin> <block1_src_addr> <block1_dst_addr> <block1_len>
             [block2_bin block2_src_addr block2_dst_addr block2_len ...]
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | see table below |
| [2] | CRC method | `CRC8`, `CRC32` (anything else disables CRC) |
| [3] | Output bin | file path |
| [4] | Main FW bin (input) | file path |
| [5+N×4] | blockN bin | file path |
| [6+N×4] | blockN source offset | hex |
| [7+N×4] | blockN destination offset | hex |
| [8+N×4] | blockN length | decimal |

| argv[1] | Target IC |
|---------|-----------|
| `NT51931BASED_NORMAL_MODE` | NT51931 |
| `NT51930BASED_NORMAL_MODE` | NT51930 |
| `NT51932BASED_NORMAL_MODE` | NT51932 |
| `NT51950BASED_NORMAL_MODE` | NT51950 |
| `NT51928BBASED_NORMAL_MODE` | NT51928B |

- argc must be **odd** and **>= 9**
- Requires `map.txt` in the same directory as `fw_bin`, or in its `output/` subdirectory

---

### 6. NT51932BASED_MERGE_AB_MODE

```
Combiner.exe NT51932BASED_MERGE_AB_MODE <a_code_bin> <b_code_bin> <output_bin> <b_code_offset>
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | `NT51932BASED_MERGE_AB_MODE` |
| [2] | A code bin | file path |
| [3] | B code bin | file path |
| [4] | Output bin | file path |
| [5] | B code offset in output | numeric (`0x` prefix supported) |

- argc must be exactly **6**

---

### 7. NT51950BASED_MERGE_AB_MODE

```
Combiner.exe NT51950BASED_MERGE_AB_MODE <CRC_method> <a_code_bin> <b_code_bin> <output_bin> <b_code_offset>
```

| argv | Description | Value |
|------|-------------|-------|
| [1] | Mode | `NT51950BASED_MERGE_AB_MODE` |
| [2] | CRC method | `CRC8`, `CRC32` |
| [3] | A code bin | file path |
| [4] | B code bin | file path |
| [5] | Output bin | file path |
| [6] | B code offset in output | numeric (`0x` prefix supported) |

- argc must be exactly **7**
