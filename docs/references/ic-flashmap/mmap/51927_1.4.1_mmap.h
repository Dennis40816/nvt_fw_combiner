/*
 *  Memory Map : swapTPD() is formally UpdateInfo();
 */

#ifndef HAL_MMAP_H_
#define HAL_MMAP_H_

/*
 *   IP0 : 0x10000~0x11FFF
 */
///< Started by NT51927
#define     CASCADE_CHIP_NUM               (2)

#define     SIF_I2C                        (0)
#define     SIF_SPI                        (1)
#define     SIF_INTERFACE                  (SIF_I2C)

//@CASCADE_EN : for cosim , noinfo block
#define     INFO_BLOCK_SZ                  (0x100)

#if (CASCADE_CHIP_NUM > 1)
#define     CASCADE_INFO                   (0x01)		//bit0:same code, bit1: next header
#else
#define     CASCADE_INFO                   (0x00)		//bit0:same code, bit1: next header
#endif
#define     SPI_OPTION                     (0x70)		//926IC:4MHz
#define     T4T6_VAL_INFO                  (0x11)

#define     SPI_DMA_2X                     (0)
#define     SPI_DMA_4X                     (1)
#define     SPI_DMA_SPEED                  SPI_DMA_2X

#if PC_SIMULATOR
#define MMap_S_2D_AdcRaw0                  (0x00082E20)			//(0x20000)                   // for pcsim gui tool can parse correct name as 672A
#define MMap_S_2D_Diff0                    (0x00085BE0)			//(0x24C60)                   // for pcsim gui tool can parse correct name as 672A
#endif

/*
**	NT51927 mmap
*/
// REMOTE DEBUG@ ILM_END~0x20000
#if (USER_SWITCH_REMOTE_DEBUG_ILM == ENABLE)
#define MMAP_ILM_END_ADDR                  (0x15800)
#define MMAP_REMOTE_DEBUG_ILM_END_ADDR     (MMAP_ILM_END_ADDR - USER_DEFINE_REMOTE_DEBUG_SAVE_SETTING)	//(0x20000-256) keep 256 bytes for saving setting for keep setting after reset
#endif

//==============================================
// Data Access Check
//==============================================
// The ILM_RECORD_START_ADDR and ILM_RECORD_SIZE are different for each IC
// Please refer to doc\DataAccessCheck for more detail
#define ILM_RECORD_START_ADDR              (0x14000)// This value should be larger than text size, Ex: 0x14000 > text(80596=0x13AD4)
#define ILM_RECORD_SIZE                    (18432)	// 0x14000~0x187FF (should not be larger than unused ILM size)

//================================================================== MMAP_S_2D_CR_NF_Header
//
//       byte    0      1      2      3      4      5      6      7      8      9      10      11      12      13      14      15
//       shift-bit  <---reserved----> shift-bit <---reserved----> shift-bit <---reserved---->   shift-bit  <---reserved---->
//           F1                          F2                          F3                            F4
//
#define     MMAP_ILM_START_ADDR            (0x00000)
/*
**	IP1 : 0x80000~0x8C800 flash load to DLM
*/
#define     MMAP_DLM_START_ADDR            (0x80000)
#define     MMAP_FW_REGISTER               (0x80000)
#define     MMAP_FW_REGISTER_TP_REG        (0x80700)			/// for EMI Tuning
#define     MMAP_FW_REGISTER_DP_REG        (0x80740)			/// for EMI Tuning

#define     MMAP_S_2D_CR_NF_HEADER         (0x80800)			/// 16 Bytes
#define     MMAP_NF_HEADER_SUBSIZE         (0x04)
#define     MMAP_S_2D_CR_NF1               (0x80810)			/// 960+48 Bytes
#define     MMAP_S_2D_CR_NF2               (0x80C00)			/// 960+48 Bytes
#define     MMAP_S_2D_CR_NF3               (0x80FF0)			/// 960+48 Bytes
#define     MMAP_S_2D_CR_NF4               (0x813E0)			/// 960+48 Bytes
#define     MMAP_CTRLRAM                   (0x817D0)			/// Ctrlram	, Size 12KB
#define     MMAP_MP_DYNAMIC_CTRLRAM        (0x847D0)			/// Ctrlram	, Size 9KB
#define     MMAP_VNOISE_CTRLRAM            (0x86BD0)			/// 280 bytes
#if PC_SIMULATOR
#define     MMAP_HEADER_INFO               P_TYPE(UINT16, 0x88230)
#else
#define     MMAP_HEADER_INFO               (0x88230)
#endif

#if (CASCADE_CHIP_NUM == 1)
#define     MMAP_HEADER_ILM_CRC_OFFSET     (0x2C)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICM (0x3C)
#elif (CASCADE_CHIP_NUM == 2)
#define     MMAP_HEADER_ILM_CRC_OFFSET     (0x8C)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICM (0x2C)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICR (0x6C)
#elif (CASCADE_CHIP_NUM == 3)
#define     MMAP_HEADER_ILM_CRC_OFFSET     (0xBC)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICM (0x2C)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICR (0x6C)
#define     MMAP_HEADER_DLM_CRC_OFFSET_ICL (0x9C)
#endif

/* ========================
**	.data + .bss    ~= 12K
**	.stack      ~= 7K
==========================*/
#define     MMAP_SELF_TEST_COLSCAN_SIZE    (492)
#define     MMAP_SELF_TEST_COLRAW_SIZE     (492)


/*
**	IP2 : 0x8C800~0x9C000 flash load to DLM
*/
#define     MMAP_DIFFDLM_CTRLRAM           (0x8CF80)			/// 5 KB-1118 = 4002 bytes
#define     MMAP_ZONE                      (0x8CF80)
#define     MMAP_S_2D_ADC_SCANRAW          (0x8DC00)
#define     MMAP_IN_BAND_ND_SCANRAW        (0x8E3A0)
#define     MMAP_GMD_SCANRAW               (0x8E7A0)
#define     MMAP_VN_2D_OUTPUT              (0x8E990)
#define     MMAP_VN_1D_OUTPUT              (0x8EB14)
#define     MMAP_COM_BUFFER                (0x8EC98)
#define     MMAP_AREA                      (0x90394)
#define     MMAP_CTRLRAM_OFFSET_MAP        (0x90B14)			/// 1920 Bytes, 120(AFE)*2(L/R)*4(Mux)*2(short)
#define     MMAP_S_2D_CR_OFFSET_F1         (0x91294)
#define     MMAP_S_2D_CR_OFFSET_F2         (0x91A34)
#define     MMAP_S_2D_CR_OFFSET_F3         (0x921D4)
#define     MMAP_S_2D_CR_OFFSET_F4         (0x92974)
#define     MMAP_S_2D_RAWRSS               (0x93114)
#define     MMAP_S_2D_RAWIIR               (0x938B4)
#define     MMAP_S_2D_BASELINE             (0x94054)
#define     MMAP_S_2D_DIFFPAST             (0x947F4)
#define     MMAP_S_2D_DIFF                 (0x94F94)
#define     MMAP_S_2D_BUCKET               (0x95C34)
#define     MMAP_S_2D_RESETCOUNTER         (0x963D4)

#define     MMAP_GMD_RAWIIR0               (0x96A24)
#define     MMAP_GMD_RAWIIR1               (0x96C14)
#define     MMAP_GMD_BASELINE              (0x96E04)
#define     MMAP_GMD_BASELINE1             (0x96FF4)
#define     MMAP_GMD_DIFF                  (0x971E4)
//#define     MMAP_S_1D_CR_OFFSET0         (0x971E4)	/// Unuse 496 Bytes
#define     MMAP_SELF_TEST_OPEN_2D         (0x973D4)
#define     MMAP_SELF_TEST_SHORT_2D        (0x97B54)
#define     MMAP_SELF_TEST_COLRAW_BTN      (0x982D4)
#define     MMAP_SELF_TEST_DEBUG           (0x982DC)
#define     MMAP_ALU_CMD_BASE              (0x98320)
#define     MMAP_FFM2CPU_HOST              (0x98350)
#define     MMAP_FFM2CPU                   (0x98360)
#define     MMAP_RECORED_MAX               (0x98370)
#define     MMAP_RESERVE2_BUF              (0x98374)
#define     MMAP_SPI_DMA_BUF               (0x983B0)
#define     MMAP_IC_RST_SOURCE_INFO        (0x98430)
#define     MMAP_FW_FLOW_DEBUG_INFO        (0x98438)
#define     MMAP_RESET_COUNTER             (0x98440)

#define     MMAP_MP_ND_DATA                (0x98444)
#define     MMAP_CPINFO                    (0x98454)
#define     MMAP_CASCADE_FLOW_DEBUG_INFO   (0x98474)
#define     MMAP_VN_PU_BL                  (0x98494)
#define     MMAP_VN_PU_RAW                 (0x98AA4)

#define     MMAP_SELF_TEST_COLSCAN         (0x98C28)
#define     MMAP_SELF_TEST_COLRAW          (0x98E14)

#define     MMAP_EVENT_BUF                 (0x99000)			/// One page event buffer, Max Size = 256 Bytes
#define     MMAP_EVENT_BUF_RESERVED        (0x99100)			/// Event buffer reserved, Max Size = 256 Bytes
#define     MMAP_HISTORY_EVENT0            (0x99200)			/// 64 bytse for History Event, Max Size = 64 Bytes
#define     MMAP_HISTORY_EVENT1            (0x99240)			/// 64 bytse for History Event, Max Size = 64 Bytes

#define     MMAP_RW_CROFFSET_KEY           (0x99280)			/// 2 Bytes
#define     MMAP_RW_CROFFSET_CHECKSUM      (0x99282)			/// 2 Bytes
#define     MMAP_IN_BAND_ND_RAWRSS         (0x99284)			/// 1008 + 16 Bytes
#define     MMAP_DEBUG_RAM                 (0x99684)			/// 112 bytes
#define     MMAP_PARA_COM_BUFFER           (0x996F4)			/// 188 bytes
#define     MMAP_INIT_FAIL_CNT             (0x997B0)			/// 1 Bytes
#define     MMAP_INIT_FAIL_CNT_BAR         (0x997B1)			/// 1 Bytes
#define     MMAP_RUNTIME_FAIL_CNT          (0x997B2)			/// 1 Bytes
#define     MMAP_RUNTIME_FAIL_CNT_BAR      (0x997B3)			/// 1 Bytes
#define     MMAP_WDT_FAIL_TEMP             (0x997B4)			/// 2 Bytes
#define     MMAP_ASIL_RESERVED             (0x997B6)			/// 69 Bytes
#define     MMAP_EMS_BACKUP 	   		   (0x997FB)            /// 1 Bytes
#define     MMAP_POWER_ON_KEY              (0x997FC)			/// 4 Bytes
#define     MMAP_IP1_END_ADDR              (0x99800)			/// IP1 end



#define     MMAP_FW_REGISTER_SIZE          (0x800)							/// FW Config. Setting, Size 2KB
#define     MMAP_HW_INFO_REGISTER          (MMAP_EVENT_BUF + 0x78)			/// General Info.
#define     MMAP_DNS_DIAGNOSTIC_BUF        (MMAP_EVENT_BUF + EN_PUB_EVENT_MAP_DNS_DIAGNOSTIC)


//add for code collision
#define     DLM_TEMP_CNC_STAIR_TABLE       P_TYPE(UINT16, MMAP_ZONE)

#define     DLM_TOTAL_SIZE                 (0x99800-0x10)

/*
**	Flashmap
*/
#define     FLASHMAP_HEADER_INFO           (0x00000)						/// Header Info Setting @Flash 0KB, Size 256 Bytes
#define     FLASHMAP_AUTOBUILD_SVN         (0x00024)						/// Offset 36-Byte

#define     FLASHMAP_FW_REGISTER           (0x16000)						/// FW settings @Flash, Size 2KB
#define     FLASHMAP_FW_REGISTER_TP_REG    (0x16700)						/// EMI Tool Register Setting
#define     FLASHMAP_FW_REGISTER_DP_REG    (0x16740)						/// EMI Tool Register Setting
#define     FLASHMAP_NF_TABLE              (0x16800)						/// Normal Factor  @Flash, Size 4048 Bytes
#define     FLASHMAP_CTRLRAM               (0x177D0)						/// Control RAM @Flash, Size 10KB
#define     FLASHMAP_MP_CTRLRAM            (0x19FD0)						/// MP Control RAM for Short @Flash 108KB, Size 9KB
#define     FlashMap_VNCTRLRAM             (0x1C3D0)						/// 72+128+624
#define     FLASHMAP_SPI_VEC               (0x1DA30)
#define     FLASHMAP_DIFF_DLM              (0x22800)
#define     FLASHMAP_ENDFLAG               (0x3FFFC)						/// End flag @Flash, size 4Bytes
#define     FLASHMAP_MP_SHORT_CTRLRAM      (FLASHMAP_MP_CTRLRAM)						/// MP Control RAM for Short @Flash 108KB, Size 3KB
#define     FLASHMAP_MP_OPEN_CTRLRAM       (FLASHMAP_MP_CTRLRAM)						/// TBD
#define     FLASHMAP_MP_OPEN_GOLDEN_L      (FLASHMAP_MP_CTRLRAM)						/// TBD
#define     FLASHMAP_MP_OPEN_GOLDEN_H      (FLASHMAP_MP_CTRLRAM)						/// TBD

//------------------------------------
//------CNC Stair Table--------
//------------------------------------
#define     FWCONFIG_TOTAL_SIZE            (2048)
#define     CTRLRAM_INFO_SZ                (256)
#define     CTRLRAM_SZ                     (12 * 1024)				/// 10KB	///< @Caspar Chen
#define     DIFF_CTRLRAM_SZ                (5 * 1024)				/// 10KB	///< @Caspar Chen
#define     MP_CTRLRAM_SZ                  (9*1024)					///9KB	///< @Caspar Chen
#define     HEADER_SZ                      (400)
#define     VN_CTRLRAM_SZ                  (5728)					// 280 + 512 PU table + 2560 VN table + 384 stair table +  384 COL_GAING_TABLE	///< @Caspar Cgen Need Check
#define     NF_HEADER_SZ                   (16)
#define     MAX_2D_DOT_NUM                 1008			//(2928)            // 960(AA) + 48(Button)
#define     MAX_SPI_DMA_VEC_SZ             400			//(800)             // 5bytes * 80 vector
///< Ended by NT51927

#if PC_SIMULATOR && PC_SIM_HWIP_CS_LIB
#define MMAP_STACK_ADDRESS                 (0x8C800)
#define MMAP_STACK_PART_A_6KB              (MMAP_STACK_ADDRESS-18*1024)		//ethan@20230912, for PCSIM + C# lib, separate 3 parts of Stack for HW_IP using (each 6kB)
#define MMAP_STACK_PART_B_6KB              (MMAP_STACK_ADDRESS-12*1024)
#define MMAP_STACK_PART_C_6KB              (MMAP_STACK_ADDRESS-6*1024)

#define MMAP_STACK_PART_DC_ADDR            (MMAP_STACK_ADDRESS-22*1024)		//6kB //ethan@20230912, PCSIM + HW_IP C# lib, separate 7 parts of Stack for ALU CNC mode
#define MMAP_STACK_PART_DELTA_ADDR         (MMAP_STACK_ADDRESS-16*1024)		//6kB
#define MMAP_STACK_PART_CNT_ADDR           (MMAP_STACK_ADDRESS-10*1024)		//1kB
#define MMAP_STACK_PART_DBASE_ADDR         (MMAP_STACK_ADDRESS-9*1024)		//6kB
#define MMAP_STACK_PART_SUM_ADDR           (MMAP_STACK_ADDRESS-3*1024)		//1kB
#define MMAP_STACK_PART_MAX_ADDR           (MMAP_STACK_ADDRESS-2*1024)		//1kB
#define MMAP_STACK_PART_MIN_ADDR           (MMAP_STACK_ADDRESS-1*1024)		//1kB
#endif

#endif	/* HAL_MMAP_H_ END */
