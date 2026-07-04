/*
 *  Memory Map : swapTPD() is formally UpdateInfo();
 */

/*
 *   IP0 : 0x10000~0x11FFF
 */
///< Started by NT51920
#define 	CASCADE_2CHIP 	(1)

#define 	SIF_I2C 		(0)
#define 	SIF_SPI 		(1)
#define 	SIF_INTERFACE 	(SIF_SPI)

//@CASCADE_EN : for cosim , noinfo block
#define 	INFO_BLOCK_SZ	(0x20)

#if (CASCADE_2CHIP)
#define 	CASCADE_INFO	(0x01) //bit0:same code, bit1: next header
#else
#define 	CASCADE_INFO	(0x00) //bit0:same code, bit1: next header
#endif
#define 	SPI_OPTION		(0x30)

#if PC_SIMULATOR
#define MMap_S_2D_AdcRaw0 (0x32E40)    //(0x20000)                   // for pcsim gui tool can parse correct name as 672A
#define MMap_S_2D_Diff0 (0x34DC0)      //(0x24C60)                   // for pcsim gui tool can parse correct name as 672A
#endif

/*
**	NT51920 mmap
*/
// REMOTE DEBUG@ ILM_END~0x20000
#if (USER_SWITCH_REMOTE_DEBUG_ILM == ENABLE)
#define 	MMAP_ILM_END_ADDR 				(MMAP_S_2D_CR_NF_HEADER_ILM)
#define 	MMAP_REMOTE_DEBUG_ILM_END_ADDR 	(MMAP_ILM_END_ADDR - USER_DEFINE_REMOTE_DEBUG_SAVE_SETTING)    //(0x20000-256) keep 256 bytes for saving setting for keep setting after reset
#endif

//================================================================== MMAP_S_2D_CR_NF_Header
//
//       byte    0      1      2      3      4      5      6      7      8      9      10      11      12      13      14      15
//       shift-bit  <---reserved----> shift-bit <---reserved----> shift-bit <---reserved---->   shift-bit  <---reserved---->
//           F1                          F2                          F3                            F4
//
#define 	MMAP_ILM_START_ADDR 			(0x00000)
#define 	MMAP_S_2D_CR_NF_HEADER_ILM 		(0x1D800)    /// 16 Bytes
#define 	MMAP_S_2D_CR_NF1_ILM 			(0x1D810)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF2_ILM 			(0x1DFF0)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF3_ILM 			(0x1E7D0)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF4_ILM 			(0x1EFB0)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_END_OF_ILM_DATA 			(0x1F730)

/*
**	IP1 : flash load to DLM
*/
#define 	MMAP_DLM_START_ADDR 			(0x20000)
#define 	MMAP_FW_REGISTER 				(0x20000)
#define 	MMAP_FW_REGISTER_TP_REG 		(0x20700)	 /// for EMI Tuning
#define 	MMAP_FW_REGISTER_DP_REG 		(0x20740)	 /// for EMI Tuning
#define 	MMAP_CTRLRAM 					(0x20780)    /// Ctrlram	, Size 9.5KB
#define 	MMAP_MP_DYNAMIC_CTRLRAM 		(0x22F80)    /// MP Ctrlram	, Size 5.75KB
#define 	MMAP_HEADER_INFO 				(0x24680)    /// Header , Size 256 Bytes
#define 	MMAP_HEADER_ILM_CRC_OFFSET  	(0x18)
#define 	MMAP_HEADER_DLM_CRC_OFFSET  	(0x1C)
#define 	MMAP_CTRLRAM_IC_S 				(0x24780)    /// Ctrlram for IC-S	, Size 9.5KB
#define 	MMAP_MP_DYNAMIC_CTRLRAM_IC_S 	(0x26F80)    /// MP Ctrlram for IC-S	, Size 5.75KB
#define 	MMAP_HEADER_INFO_S 				(0x28680)    /// Header Slave , Size 256 Bytes

// Initial DLM: move to RO_ILM
#define 	MMAP_S_2D_CR_NF_HEADER 			(0x28780)    /// 16 Bytes
#define 	MMAP_NF_HEADER_SUBSIZE 			(0x4)        /// 4 Bytes
#define 	MMAP_S_2D_CR_NF1 				(0x28790)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF2 				(0x28F70)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF3 				(0x29750)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_CR_NF4 				(0x29F30)    /// 1920 Bytes + Button 96 Bytes

#define 	MMAP_AREA 						(0x24780)    /// Max Size = 3000 Bytes (120 zone)
#define 	MMAP_ZONE 						(0x25338)    /// 3840 Bytes

#define 	MMAP_S_2D_RAWRSS 				(0x26238)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_RAWIIR 				(0x271F8)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_BASELINE 				(0x281B8)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_DIFFPAST 				(0x29178)    /// 3840 Bytes + Button 192 Bytes

#define 	MMAP_VNOISE_2D_OUTPUT 			(0x2A138)    /// 772 bytes
#define 	MMAP_VNOISE_1D_OUTPUT 			(0x2A43C)    /// 768 bytes

#define 	MMap_VNOISE_CTRLRAM 			(0x2A710)    /// 280 bytes
#define 	MMAP_PU_TBL 					(0x2A828)    /// 512 bytes
#define 	MMap_VNOISE_VN_TBL 				(0x2AA28)    /// 2560 bytes
#define 	MMap_VNOISE_STAIR_TBL 			(0x2B428)    /// 384 bytes
#define 	MMAP_1D_COL_GAING_TBL 			(0x2B5A8)    /// 384 bytes

#define 	MMAP_SPI_DMA_VEC 				(0x2B728)    /// 400 bytes

#if PC_SIMULATOR
#define 	MMAP_HEADER_INFO 				P_TYPE(UINT16, 0x2C000)     /// Header Info Setting, Size 256 Bytes *2
#endif

#define 	MMAP_EVENT_BUF 					(0x30500)    /// One page event buffer, Max Size = 256 Bytes
#define 	MMAP_EVENT_BUF_RESERVED 		(0x30600)    /// Event buffer reserved, Max Size = 256 Bytes

#define 	MMAP_GMD_SCANRAW 				(0x30700)    /// 640 Bytes + Button 32 Bytes
#define 	MMAP_IN_BAND_ND_SCANRAW 		(0x309A0)    /// 1312 Bytes
#define 	MMAP_S_2D_ADC_SCANRAW 			(0x30EC0)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_DIFF 					(0x31E80)    /// 3840 Bytes + Button 192 Bytes

///---------------- 1st PowerOn Init ----------------
#define 	MMAP_S_2D_CR_OFFSET_F1 			(0x32E40)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_CR_OFFSET_F2 			(0x33E00)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_CR_OFFSET_F3 			(0x34DC0)    /// 3840 Bytes + Button 192 Bytes
#define 	MMAP_S_2D_CR_OFFSET_F4 			(0x35D80)    /// 3840 Bytes + Button 192 Bytes
///---------------------------------------------------

#define 	MMAP_S_2D_RESETCOUNTER 			(0x36D40)    /// 1920 Bytes + Button 96 Bytes
#define 	MMAP_S_2D_BUCKET 				(0x37520)    /// 1920 Bytes + Button 96 Bytes

#define 	MMAP_SELF_TEST_OPEN_2D 			(0x37D00)    /// 1920 Bytes
#define 	MMAP_SELF_TEST_SHORT_2D 		(0x38480)    /// 1920 Bytes
#define 	MMAP_GMD_DIFF 					(0x38C00)    /// 672 Bytes
#define 	MMAP_RESERVED2 					(0x38EA0)    /// 0x38F20-0x38EA0 = 128 Bytes

#define 	MMAP_COM_BUFFER 				(0x38F20)    /// 0x3A4FC-0x38F20 = 4044 Bytes

#define 	MMAP_GMD_RAWIIR0 				(0x39EEC)    /// 672 Bytes
#define 	MMAP_GMD_RAWIIR1 				(0x3A18C)    /// 672 Bytes
#define 	MMAP_GMD_BASELINE 				(0x3A42C)    /// 672 Bytes
#define 	MMAP_GMD_BASELINE1 				(0x3A6CC)    /// 672 Bytes

#define 	MMAP_IN_BAND_ND_RAWRSS			(0x3A96C)    /// 1312 Bytes
#define 	MMAP_VN_PU_BL 					(0x3AE8C)    /// (48*2+1)PU_MAX*4(word)*4(freq) = 1552 bytes
#define 	MMAP_VN_PU_RAW 					(0x3B49C)    /// (48*2+1)PU_MAX*4(word) = 388 bytes

#define 	MMAP_ALU_CMD_BASE 				(0x3B620)    /// ALU command section, Max Size = 40 Bytes
#define 	MMAP_FFM2CPU 					(0x3B648)    /// FFM2CPU Section, Max Size = 16 Bytes
#define 	MMAP_FW_FLOW_DEBUG_INFO 		(0x3B658)    /// FW Debug Variable, Max Size = 8 Bytes

///---------------- 1st PowerOn Init ----------------
#define 	MMAP_IC_RST_SOURCE_INFO 		(0x3B660)    /// FW RESET Info, Max Size = 8 Bytes
#define 	MMAP_CASCADE_REBOOT_CNT 		(0x3B668)    /// 2 Bytes Reboot Cnt
#define 	MMAP_OTP_CRC_FAIL_CNT 			(0x3B66A)    /// 2 Bytes
#define 	MMAP_LVD_RECORD_BUF 			(0x3B66C)    /// 2 Bytes
#define 	MMAP_WDT_FAIL_CNT 				(0x3B66E)    /// 2 Bytes
#define 	MMAP_ICS_ERROR_RECORD_BUF 		(0x3B670)    /// 2 Bytes
#define 	MMAP_ASIL_RESERVED				(0x3B672)    /// 66 Bytes
#define 	MMAP_POWER_ON_KEY				(0x3B6B4)    /// 4 Bytes
#define 	MMAP_RESET_COUNTER 				(0x3B6B8)    /// 4 bytes
#define 	MMAP_HISTORY_EVENT0 			(0x3B6BC)    /// 64 bytse for History Event, Max Size = 64 Bytes
#define 	MMAP_HISTORY_EVENT1 			(0x3B6FC)    /// 64 bytse for History Event, Max Size = 64 Bytes
///---------------------------------------------------

#define 	MMAP_MP_ND_DATA 				(0x3B73C)    /// 16 bytes
#define 	MMAP_CPINFO 					(0x3B74C)    /// 32 bytes for CP info read by FW
#define 	MMAP_SPI_DMA_BUF 				(0x3B76C)    /// 128 bytes, 16(protocol)+112(buffer) bytes

#define 	MMAP_SELF_TEST_COLSCAN 			(0x3B7EC)    /// 656 bytes for scan row
#define 	MMAP_SELF_TEST_COLRAW 			(0x3BA7C)    /// 656 bytes for judgement
#define 	MMAP_SELF_TEST_DEBUG 			(0x3BD0C)    /// Debug info for self test: 65 bytes

#define 	MMAP_CASCADE_FLOW_DEBUG_INFO 	(0x3BD50)    /// 32 Bytes
#define 	MMAP_RESERVED 					(0x3BD70)    /// 0x3C000-0x3BD70 = 656 Bytes
#define 	MMAP_END_ADDR					(0x3C000)    ///

#define 	MMAP_HW_INFO_REGISTER 			(MMAP_EVENT_BUF + 0x78)		/// General Info.
//===========================================
// V Noise
//===========================================
#define 	MMap_VN_2D_OUTPUT_ICM 			(MMAP_VNOISE_2D_OUTPUT)   	/// 128 bytes
#define 	MMap_VN_2D_OUTPUT_ICS 			(MMap_VN_2D_OUTPUT_ICM)     /// 128 bytes
#define 	MMap_VN_1D_OUTPUT_ICM 			(MMAP_VNOISE_1D_OUTPUT)		/// 8 bytes
#define 	MMap_VN_1D_OUTPUT_ICS 			(MMap_VN_1D_OUTPUT_ICM)     /// 8 bytes
/*
**	Flashmap
*/
#define 	FLASHMAP_HEADER_INFO 			(0x00000)	 /// Header Info Setting @Flash 0KB, Size 256 Bytes
#define 	FLASHMAP_AUTOBUILD_SVN 			(0x00024)    /// Offset 36-Byte

#define 	FLASHMAP_FW_REGISTER 			(0x22000)    /// FW settings @Flash, Size 2.5KB
#define 	FLASHMAP_FW_REGISTER_TP_REG 	(0x22700)    /// EMI Tool Register Setting
#define 	FLASHMAP_FW_REGISTER_DP_REG 	(0x22740)    /// EMI Tool Register Setting
#define 	FLASHMAP_CTRLRAM 				(0x22780)    /// Control RAM @Flash, Size 8KB
#define 	FLASHMAP_MP_CTRLRAM 			(0x24F80)    /// MP Control RAM for Short @Flash 108KB, Size 5.75KB
#define 	FLASHMAP_HEADER 				(0x26680)    /// Header, Size 256 Bytes
#define 	FLASHMAP_DIFF_DLM 				(0x26780)  
#define 	FLASHMAP_CTRLRAM_IC_S 			(0x26780)    /// Control RAM @Flash, Size 8KB
#define 	FLASHMAP_MP_CTRLRAM_IC_S 		(0x28F80)    /// MP Control RAM for Short @Flash 108KB, Size 5.75KB
#define 	FLASHMAP_HEADER_S 				(0x2A680)    /// Header, Size 256 Bytes
#define 	FLASHMAP_NF_TABLE 				(0x2A780)    /// Normal Factor  @Flash, Size 6416 Bytes
#define 	FlashMap_VNCTRLRAM 				(0x2C710)    /// 72+128+624

#define 	FLASHMAP_ENDFLAG 				(0x2FFFC)    /// End flag @Flash, size 4Bytes

#define 	FLASHMAP_MP_SHORT_CTRLRAM 		(FLASHMAP_MP_CTRLRAM)    /// MP Control RAM for Short @Flash 108KB, Size 3KB

//------------------------------------
//------CNC Stair Table--------
//------------------------------------
#define 	DLM_TEMP_CNC_STAIR_TABLE P_TYPE(UINT16, MMAP_ZONE)

#define 	FWCONFIG_TOTAL_SIZE 			(1920)
#define 	CTRLRAM_SZ 						(10 * 1024)
#define 	MP_CTRLRAM_SZ 					(5888)	     /// (6 * 1024)-256(Header)
#define 	HEADER_SZ 						(256)
#define 	PAD_SZ 							(216)
#define 	VN_CTRLRAM_SZ 					(4120)       /// 280 + 512 PU table + 2560 VN table + 384 stair table +  384 COL_GAING_TABLE
#define 	NF_HEADER_SZ 					(16)
#define 	MAX_2D_DOT_NUM 					(2016)       /// 1920(AA) + 96(Button)
#define 	MAX_SPI_DMA_VEC_SZ 				(600)        /// VECOTR MAX: 5bytes * 107=535
#define 	USED_SPI_DMA_VEC_SZ 			(127)        /// 5bytes * 23 vector
#define 	DIFF_DLM_SZ 					(10*1024)   
#define 	MMAP_DNS_DIAGNOSTIC_BUF         (MMAP_EVENT_BUF + EN_PUB_EVENT_MAP_DNS_DIAGNOSTIC)
///< Ended by NT51920
