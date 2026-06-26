# ============================================================
# IC Configuration
# ============================================================

# Design principles:
#
# 1. All offsets are BIN-space by default.
# 2. FLASH address = BIN offset + flash_reloc_offset.
# 3. TPA and TPB share IDENTICAL layout in BIN space.
#    - TP internal addresses / pointers are layout-based,
#      not instance-based (A/B).
#    - Only FLASH placement differs via relocation offsets.
# 4. DPA always has flash relocation (can be 0).
# 5. DPB relocation is optional (None = not supported).


class ICConfig:
    def __init__(
        self,
        name,

        # ----------------------------------------------------
        # TP layout (BIN space, shared by TPA / TPB)
        # ----------------------------------------------------
        tp_start_offset,
        tp_end_offset,

        tp_ilm_start_addr_offset,
        tp_dlm_start_addr_offset,
        tp_diff_dlm_start_addr_offset,

        # ----------------------------------------------------
        # DP layout (BIN space)
        # ----------------------------------------------------
        dp_b_header_offset,

        dp_a_ver_main_offset,
        dp_a_ver_sub_offset,

        # ----------------------------------------------------
        # FLASH mapping (relocation)
        # ----------------------------------------------------
        tpa_flash_reloc_offset,
        tpb_flash_reloc_offset,
        dpa_flash_reloc_offset,
        dpb_flash_reloc_offset,   # None = DPB mapping not supported
        
        # ----------------------------------------------------
        # CRC
        # ----------------------------------------------------
        tp_crc_addr_offset=None,
        tp_crc_range=None
    ):
        self.name = name

        # TP layout (BIN)
        self.tp_start_offset = tp_start_offset
        self.tp_end_offset = tp_end_offset

        self.tp_ilm_start_addr_offset = tp_ilm_start_addr_offset
        self.tp_dlm_start_addr_offset = tp_dlm_start_addr_offset
        self.tp_diff_dlm_start_addr_offset = tp_diff_dlm_start_addr_offset

        # DP layout (BIN)
        self.dp_b_header_offset = dp_b_header_offset

        self.dp_a_ver_main_offset = dp_a_ver_main_offset
        self.dp_a_ver_sub_offset = dp_a_ver_sub_offset

        # FLASH relocation
        self.tpa_flash_reloc_offset = tpa_flash_reloc_offset
        self.tpb_flash_reloc_offset = tpb_flash_reloc_offset
        self.dpa_flash_reloc_offset = dpa_flash_reloc_offset
        self.dpb_flash_reloc_offset = dpb_flash_reloc_offset
        
        # CRC
        self.tp_crc_addr_offset = tp_crc_addr_offset
        self.tp_crc_range = tp_crc_range

    # ========================================================
    # Address conversion utilities
    # ========================================================

    def to_flash_from_tpa(self, bin_offset: int) -> int:
        return bin_offset + self.tpa_flash_reloc_offset

    def to_flash_from_tpb(self, bin_offset: int) -> int:
        return bin_offset + self.tpb_flash_reloc_offset

    def to_flash_from_dpa(self, bin_offset: int) -> int:
        return bin_offset + self.dpa_flash_reloc_offset

    def to_flash_from_dpb(self, bin_offset: int) -> int:
        if self.dpb_flash_reloc_offset is None:
            raise RuntimeError("DPB flash relocation is not defined")
        return bin_offset + self.dpb_flash_reloc_offset

    # --------------------------------------------------------
    # Common derived ranges
    # --------------------------------------------------------

    def get_tpa_flash_range(self):
        start = self.to_flash_from_tpa(self.tp_start_offset)
        end = self.to_flash_from_tpa(self.tp_end_offset)
        return start, end

    def get_tpb_flash_range(self):
        size = self.tp_end_offset - self.tp_start_offset
        start = self.to_flash_from_tpb(self.tp_start_offset)
        end = start + size
        return start, end


# ============================================================
# IC Config Table
# ============================================================

IC_CONFIGS = {
    "51929": ICConfig(
        name="NT51929TT",

        # TP (BIN)
        tp_start_offset=0x7000,
        tp_end_offset=0x40000,

        tp_ilm_start_addr_offset=0x7164,
        tp_dlm_start_addr_offset=0x7168,
        tp_diff_dlm_start_addr_offset=0x716C,

        # DP (BIN)
        dp_b_header_offset=0x40000,

        dp_a_ver_main_offset=0x67,
        dp_a_ver_sub_offset=0x68,

        # FLASH relocation
        tpa_flash_reloc_offset=0x0,
        tpb_flash_reloc_offset=0x40000,
        dpa_flash_reloc_offset=0x0,
        dpb_flash_reloc_offset=None,
    ),

    "51932": ICConfig(
        name="NT51932TT",

        tp_start_offset=0x7000,
        tp_end_offset=0x40000,

        tp_ilm_start_addr_offset=0x7164,
        tp_dlm_start_addr_offset=0x7168,
        tp_diff_dlm_start_addr_offset=0x716C,

        dp_b_header_offset=0x40000,

        dp_a_ver_main_offset=0x67,
        dp_a_ver_sub_offset=0x68,

        tpa_flash_reloc_offset=0x0,
        tpb_flash_reloc_offset=0x40000,
        dpa_flash_reloc_offset=0x0,
        dpb_flash_reloc_offset=None,
    ),

    "51950": ICConfig(
        name="NT51950TT",

        tp_start_offset=0xA000,
        tp_end_offset=0x37000, # Not 0x38000, no including customer region

        tp_ilm_start_addr_offset=0xA100,
        tp_dlm_start_addr_offset=0xA110,
        tp_diff_dlm_start_addr_offset=0xA120,

        dp_b_header_offset=0x40000,

        dp_a_ver_main_offset=None,
        dp_a_ver_sub_offset=None,

        tpa_flash_reloc_offset=0x0,
        tpb_flash_reloc_offset=0x40000,
        dpa_flash_reloc_offset=0x0,
        dpb_flash_reloc_offset=None,
        
        tp_crc_addr_offset=0xA130,
        tp_crc_range=(0xA100, 0xA130)
    ),

    "51951": ICConfig(
        name="NT51951TT",

        tp_start_offset=0xA000,
        tp_end_offset=0x37000, # Not 0x38000, no including customer region

        tp_ilm_start_addr_offset=0xA100,
        tp_dlm_start_addr_offset=0xA110,
        tp_diff_dlm_start_addr_offset=0xA120,

        dp_b_header_offset=0x40000,

        dp_a_ver_main_offset=None,
        dp_a_ver_sub_offset=None,

        tpa_flash_reloc_offset=0x0,
        tpb_flash_reloc_offset=0x80000,
        dpa_flash_reloc_offset=0x0,
        dpb_flash_reloc_offset=None,
        
        tp_crc_addr_offset=0xA130,
        tp_crc_range=(0xA100, 0xA130)
    ),
}