/*
 * inc\define\mmap.h
 *
 */

#ifndef INC_DEFINE_MMAP_H_
#define INC_DEFINE_MMAP_H_

/*-----------------------------------------------------------------------------*/
/* Constant Definitions                                                        */
/*-----------------------------------------------------------------------------*/

/*
 *   IP0 : 0x10000~0x11FFF
 */
///< Started by NT51926
#define CASCADE_CHIP_NUM                (3)

#define BC_EN 							(0)	// panel_select
#if (CASCADE_CHIP_NUM >= 3)
#define PRIMARRY_LOC_MIDDLE 			(1)
#else
#define PRIMARRY_LOC_MIDDLE 			(0)
#endif

#define SIF_I2C 						(0)
#define SIF_SPI 						(1)
#define SIF_INTERFACE   				(SIF_I2C)

//@CASCADE_EN : for cosim , noinfo block
#define INFO_BLOCK_SZ					(0x100)

#if (CASCADE_CHIP_NUM > 1)
#define CASCADE_INFO					(0x09) 	//bit0:same code, bit1: next header, bit3: dual read
#else
#define CASCADE_INFO					(0x08) 	//bit0:same code, bit1: next header, bit3: dual read
#endif
#define FLASH_SPI_OPTION				(0x10)	//926IC:9MHz
#define T4T6_VAL_INFO					(0x33)
#define TX_SSC							(0x40)  // SYNC_TP_ENH_OE_BLD[1:0] = 01b => drving 78.6%

#define MMAP_DLM_START_ADDR            	((UINT32)0x00080000)

#if PC_SIMULATOR
#include <basetsd.h>

#define MMap_S_2D_AdcRaw0 				(UINT32)(0x00082E20)    	//(0x20000)                   // for pcsim gui tool can parse correct name as 672A
#define MMap_S_2D_Diff0 				((UINT32)0x00085BE0)      	//(0x24C60)                   // for pcsim gui tool can parse correct name as 672A

#define VIRTUAL_MEM_CHIP_DLM_SIZE		  (512*1024)
#define VIRTUAL_MEM_CHIP_ILM_SIZE		  (16*1024)

struct VirtualMem
{
	UINT8 u8DLMRegMap[CASCADE_CHIP_NUM][VIRTUAL_MEM_CHIP_DLM_SIZE];	//512K * IC#
	UINT8 u8ILMRegMap[CASCADE_CHIP_NUM][VIRTUAL_MEM_CHIP_ILM_SIZE];	//16K * IC#
};

struct VirtualMem *gpstVirtualMemRegMap;

UINT8 *gpu8VirtualMemDLMRegComAddr;
UINT8 *gpu8VirtualMemILMRegComAddr;

#define START_ADDR ((UINT32)gpu8VirtualMemDLMRegComAddr - MMAP_DLM_START_ADDR)
#else
#define START_ADDR ((UINT32)0)
#endif

/*
**	NT51926 mmap
*/

//==============================================
// Data Access Check
//==============================================
// The ILM_RECORD_START_ADDR and ILM_RECORD_SIZE are different for each IC
// Please refer to doc\DataAccessCheck for more detail
#define ILM_RECORD_START_ADDR (0x14000)  // This value should be larger than text size, Ex: 0x14000 > text(80596=0x13AD4)
#define ILM_RECORD_SIZE       (18432)    // 0x14000~0x187FF (should not be larger than unused ILM size)

//================================================================== MMAP_S_2D_CR_NF_Header
//
//       byte    0      1      2      3      4      5      6      7      8      9      10      11      12      13      14      15
//       shift-bit  <---reserved----> shift-bit <---reserved----> shift-bit <---reserved---->   shift-bit  <---reserved---->
//           F1                          F2                          F3                            F4
//
#define MMAP_ILM_START_ADDR 					(0x00000)
#if PC_SIMULATOR
#define MMAP_S_2D_CR_NF_HEADER_ILM 				((UINT32)gpu8VirtualMemILMRegComAddr)
#else
#define MMAP_S_2D_CR_NF_HEADER_ILM 				((UINT32)0x1E130)  /// 16 Bytes
#endif
#define MMAP_S_2D_CR_NF1_ILM             		(MMAP_S_2D_CR_NF_HEADER_ILM + NF_HEADER_SZ)						/// 1920 Bytes + Button 32 Bytes, 0x1E140
#define MMAP_S_2D_CR_NF2_ILM            		(MMAP_S_2D_CR_NF1_ILM + (S_2D_BUF_NUM_HW_ALL * (UINT32)2))		/// 1920 Bytes + Button 32 Bytes, 0x1E8E0
#define MMAP_S_2D_CR_NF3_ILM             		(MMAP_S_2D_CR_NF2_ILM + (S_2D_BUF_NUM_HW_ALL * (UINT32)2))		/// 1920 Bytes + Button 32 Bytes, 0x1F080
#define MMAP_S_2D_CR_NF4_ILM             		(MMAP_S_2D_CR_NF3_ILM + (S_2D_BUF_NUM_HW_ALL * (UINT32)2))		/// 1920 Bytes + Button 32 Bytes, 0x1F820

#define MMAP_S_END_OF_ILM_DATA 					((UINT32)0x0001FFC0)
//==================================================================
/*
**	DLM
*/
#define MMAP_FW_REGISTER               			((UINT32)0x00080000 + START_ADDR)				// FWconfig = 2048-128 bytes = 1920
#define MMAP_EMI_REG_TUNING 	   				((UINT32)0x00080780 + START_ADDR)	 			// 128 Bytes for EMI Rgister Tuning
//==================================================================
// Initial DLM: move to the last of DLM
#define MMAP_ICM_CTRLRAM_TMP            		((UINT32)0x00080800 + START_ADDR)				// ICM Ctrlram+MP	, Size 24KB
#define MMAP_ICS_DIFF_CTRLRAM_0            		((UINT32)0x00085800 + START_ADDR)				// ICS Diff Ctrlram 0, Size 5KB
#define MMAP_ICS_DIFF_CTRLRAM_1            		((UINT32)0x00086C00 + START_ADDR)				// ICS Diff Ctrlram	1, Size 5KB
#define MMAP_BOOT_REUSE_RESERVED            	((UINT32)0x00088000 + START_ADDR)				// Size 10KB
//==================================================================
#define MMAP_S_2D_ADC_SCANRAW 					((UINT32)0x00080800 + START_ADDR)      			/// 1920 Bytes + Button 32 Bytes
#define MMAP_S_2D_RAWRSS 						((UINT32)0x00080FA0 + START_ADDR)      			/// 1920 Bytes + Button 32 Bytes
#define MMAP_S_2D_RAWIIR 						((UINT32)0x00081740 + START_ADDR)      			/// 1920 Bytes + Button 32 Bytes
#define MMAP_S_2D_BASELINE 						((UINT32)0x00081EE0 + START_ADDR)    			/// 1920 Bytes + Button 32 Bytes
#define MMAP_S_2D_DIFFPAST 						((UINT32)0x00082680 + START_ADDR)    			/// 1920 Bytes + Button 32 Bytes
#define MMAP_S_2D_DIFF 							((UINT32)0x00082E20 + START_ADDR)        		/// 4480 Bytes + Button 32 Bytes

#define MMAP_TMP_NF_TABLE              			((UINT32)0x00083FC0 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
#define MMAP_TMP_OFFSET_TABLE          			((UINT32)0x00084760 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
#define MMAP_CTRLRAM_OFFSET_MAP                 ((UINT32)0x00084F00 + START_ADDR)               /// 1920 Bytes + Button 32 Bytes
#define MMAP_VNOISE_2D_OUTPUT 					((UINT32)0x000856A0 + START_ADDR)    			/// 484 bytes
#define MMAP_ADC_OV_DETECT_OUTPUT 				((UINT32)0x00085884 + START_ADDR)    			/// Max Size = 128 Bytes
#define MMAP_MP_ND_DATA 						((UINT32)0x00085904 + START_ADDR)    			/// 16 bytes
#define MMAP_RESERVE0_BUF                       ((UINT32)0x00085914 + START_ADDR)               /// 1260 Bytes

#define MMAP_SELF_TEST_INTERNAL_COLSCAN         ((UINT32)0x00085E00 + START_ADDR)               /// 488 bytes for scan row
#define MMAP_SELF_TEST_INTERNAL_COLRAW          ((UINT32)0x00085FE8 + START_ADDR)               /// 488 bytes for judgment
#define MMAP_SELF_TEST_DEBUG 					((UINT32)0x000861D0 + START_ADDR)				/// 8 Bytes
#define MMAP_S_2D_REM_BR						((UINT32)0x000861D8 + START_ADDR) 				/// 1920 Bytes + Button 32 Bytes
#define MMAP_TMP_BUF0_FOR_ALU					((UINT32)0x00086978 + START_ADDR)  				/// 2 Bytes
#define MMAP_TMP_BUF1_FOR_ALU					((UINT32)0x0008697A + START_ADDR)  				/// 2 Bytes
#define MMAP_RESERVE1_BUF                       ((UINT32)0x0008697C + START_ADDR)               /// 492 Bytes

#define MMAP_SELF_TEST_COLSCAN 					((UINT32)0x00086B68 + START_ADDR)    			/// 488 bytes for scan row
#define MMAP_SELF_TEST_COLRAW 					((UINT32)0x00086D50 + START_ADDR)     			/// 488 bytes for judgement
#define MMAP_RESERVE2_BUF                       ((UINT32)0x00086F38 + START_ADDR)               /// 1464 Bytes

#define MMAP_GMD_SCANRAW 						((UINT32)0x000874F0 + START_ADDR)        		/// 488 Bytes + Button 32 Bytes
#define MMAP_GMD_RAWIIR0 						((UINT32)0x000876D8 + START_ADDR)      			/// 488 Bytes
#define MMAP_GMD_RAWIIR1 						((UINT32)0x000878C0 + START_ADDR)      			/// 488 Bytes
#define MMAP_GMD_BASELINE 						((UINT32)0x00087AA8 + START_ADDR)     			/// 488 Bytes
#define MMAP_GMD_BASELINE1 						((UINT32)0x00087C90 + START_ADDR)    			/// 488 Bytes
#define MMAP_GMD_DIFF 							((UINT32)0x00087E78 + START_ADDR)        		/// 488 Bytes

#define MMAP_ALG_COMBUF_0                       ((UINT32)0x00088060 + START_ADDR)               /// 4480 Bytes + Button 32 Bytes
#define MMAP_ALG_COMBUF_1                       ((UINT32)0x00089200 + START_ADDR)               /// 4480 Bytes + Button 32 Bytes
#define MMAP_RESERVE3_BUF                       ((UINT32)0x0008A3A0 + START_ADDR)               /// 13632 Bytes
//==================================================================
// Reuse DLM: move to the ILM in the boot state
#define MMAP_S_2D_CR_NF_HEADER 					((UINT32)0x0008A800 + START_ADDR)    			/// 16 Bytes
#define MMAP_S_2D_CR_NF1 						((UINT32)0x0008A810 + START_ADDR)        		/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_NF2 						((UINT32)0x0008B380 + START_ADDR)        		/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_NF3 						((UINT32)0x0008BEF0 + START_ADDR)        		/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_NF4 						((UINT32)0x0008CA60 + START_ADDR)        		/// 2880 Bytes + Button 48 Bytes
//==================================================================
#define MMAP_AREA 								((UINT32)0x0008DB60 + START_ADDR)    			/// Max Size = 2880 Bytes (120 zone)
#define MMAP_ZONE 								((UINT32)0x0008E6A0 + START_ADDR)    			/// 4480 Bytes
#define MMAP_ALU_CMD_BASE 						((UINT32)0x0008F5A0 + START_ADDR)    			/// ALU command section, Max Size = 40 Bytes

#define MMAP_VNOISE_CTRLRAM 					((UINT32)0x0008F5D0 + START_ADDR)      			/// 280 bytes
#define MMAP_VNOISE_PU_TBL 						((UINT32)0x0008F6E8 + START_ADDR)       		/// 512 bytes
#define MMAP_VNOISE_VN_TBL 						((UINT32)0x0008F8E8 + START_ADDR)       		/// 4036 bytes
#define MMAP_VNOISE_STAIR_TBL 					((UINT32)0x000908AC + START_ADDR)    			/// 450 bytes
#define MMAP_UNUSE_ALIGN_BUF                    ((UINT32)0x00090A6E + START_ADDR)               /// UNUSE ALIGN 2Bytes

#if PC_SIMULATOR
#define MMAP_HEADER_INFO 						P_TYPE(UINT16, 0x8C000 + START_ADDR)    	/// Header Info Setting, Size 256 Bytes *2
#else
#define MMAP_HEADER_INFO 						((UINT32)0x00090A70)    			/// Header , Size 256 Bytes
#endif
//==================================================================
// .data
// .bss
// .stack
//==================================================================
#define EVENT_BUF_SIZE                          (256)
#define MMAP_EVENT_BUF 							((UINT32)0x00096A00 + START_ADDR)        		/// One page event buffer, Max Size = 256 Bytes
#define MMAP_EVENT_BUF_RESERVED 				((UINT32)0x00096B00 + START_ADDR)    			/// Event buffer reserved, Max Size = 256 Bytes
#define MMAP_EVENT_BUF_TEMP                     ((UINT32)0x00096C00 + START_ADDR)               /// 256 Bytes
#define GENERAL_INFO_SIZE                       (0x0027)
#define MMAP_HW_INFO_REGISTER 					(MMAP_EVENT_BUF + EVENT_MAP_FWINFO_COPY1)    	/// General Info.
#define MMAP_DNS_DIAGNOSTIC_BUF        			(MMAP_EVENT_BUF + EVENT_MAP_DNS_DIAGNOSTIC)
#define MMAP_CUSTOMIZED_FUNC_SWITCH				(MMAP_EVENT_BUF + EVENT_MAP_CUSTOMER_FUN_SWITCH)

#define PARA_COM_BUFFER_SIZE                    ((UINT16)180)
#define PARA_COM_BUFFER_SIZE_PER_DIE            (PARA_COM_BUFFER_SIZE / (UINT16)3)              /// MMAP_PARA_COM_BUFFER Size / 3
#define MMAP_PARA_COM_BUFFER                    ((UINT32)0x00096D00)				            /// 180 bytes

#define MMAP_FW_FLOW_DEBUG_INFO                 ((UINT32)0x00096DB4 + START_ADDR)    			/// FW RESET Info, Used Size = 8 Bytes
#define MMAP_HOST_CMD_BACKUP					((UINT32)0x00096DBC + START_ADDR)  				/// 8 Bytes
#define MMAP_DNS_DIAGNOSTIC_INFO                ((UINT32)0x00096DC4 + START_ADDR)               /// 8 Bytes
#define MMAP_ASIL_F2C_DP_STATUS                 ((UINT32)0x00096DCC + START_ADDR)          	 	/// 4 Bytes
#define MMAP_TUNING_RECORED_DATA                ((UINT32)0x00096DD0 + START_ADDR)  				/// 8 Bytes
#define MMAP_REF_DIFF                           ((UINT32)0x00096DD8 + START_ADDR)  				/// 1920 Bytes
#define MMAP_RESERVE4_BUF                       ((UINT32)0x00097558 + START_ADDR)  				/// 1604 Bytes

#define MMAP_REPORT_COMBUF                      ((UINT32)0x00097B9C + START_ADDR)   			/// 6276 Bytes
#define MMAP_VN_PU_BL                           ((UINT32)0x00099420 + START_ADDR)    			/// (60*2+1)PU_MAX*4(F1~F4)*4(word) = 1936 bytes
#define MMAP_VN_PU_RAW                          ((UINT32)0x00099BB0 + START_ADDR)    			/// (60*2+1)PU_MAX*4(word) = 484 bytes
#define MMAP_RW_CROFFSET_KEY                    ((UINT32)0x00099D94 + START_ADDR)    			/// 2 Bytes
#define MMAP_RW_CROFFSET_CHECKSUM               ((UINT32)0x00099D96 + START_ADDR)	 			/// 2 Bytes

#define MMAP_S_2D_CR_OFFSET                  	((UINT32)0x00099D98 + START_ADDR)				/// 7808 Bytes
//==================================================================
// MMAP_S_2D_CR_OFFSET_F1                  		((UINT32)0x00099D98 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
// MMAP_S_2D_CR_OFFSET_F2                  		((UINT32)0x0009A538 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
// MMAP_S_2D_CR_OFFSET_F3                  		((UINT32)0x0009ACD8 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
// MMAP_S_2D_CR_OFFSET_F4                  		((UINT32)0x0009B478 + START_ADDR)				/// 1920 Bytes + Button 32 Bytes
//==================================================================

#define MMAP_FFM2CPU_HOST                       ((UINT32)0x0009BC2C + START_ADDR)         		/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_FFM2CPU                            ((UINT32)0x0009BC3C + START_ADDR)         		/// FFM2CPU Section, Max Size = 16 Bytes

#define MMAP_IC_RST_SOURCE_INFO                 ((UINT32)0x0009BC4C + START_ADDR)    			/// FW Debug Variable, Max Size = 8 Bytes
#define MMAP_ASIL_INFO 						    ((UINT32)0x0009BC54 + START_ADDR)    			/// 71 Bytes
#define MMAP_EMS_BACKUP 						((UINT32)0x0009BC9B + START_ADDR)    			/// 1 Bytes
#define MMAP_POWER_ON_KEY                       ((UINT32)0x0009BC9C + START_ADDR)               /// 4 Bytes

#define MMAP_HISTORY_EVENT0 					((UINT32)0x0009BCA0 + START_ADDR)   			/// 64 bytse for History Event, Max Size = 64 Bytes
#define MMAP_HISTORY_EVENT1 					((UINT32)0x0009BCE0 + START_ADDR)   			/// 64 bytse for History Event, Max Size = 64 Bytes

#define MMAP_CASC_DEBUG_BUF 					((UINT32)0x0009BD20 + START_ADDR)   			/// 128 bytes, 16(protocol)+112(buffer) bytes
#define MMAP_IN_BAND_ND_SCANRAW 				((UINT32)0x0009BDA0 + START_ADDR)    			/// 992 Bytes
#define MMAP_IN_BAND_ND_RAWRSS                  ((UINT32)0x0009C180 + START_ADDR)    			/// 992 Bytes // unused for v2.0.0
#define MMAP_SELF_TEST_OPEN_2D                  ((UINT32)0x0009C560 + START_ADDR)    			/// 1952 Bytes
#define MMAP_SELF_TEST_SHORT_2D                 ((UINT32)0x0009CD00 + START_ADDR)    			/// 1952 Bytes
#define MMAP_RESERVE5_BUF                       ((UINT32)0x0009D4A0 + START_ADDR)               /// 7004 Bytes

#define MMAP_END_ADDR							((UINT32)0x0009EFFC + START_ADDR)
#define MMAP_DATA_COLLECTION_KEY_BUF 			((UINT32)0x0009EFFC + START_ADDR)    			/// 4 Bytes Key for handshake

#define MMAP_CTRLRAM                    		((UINT32)0x0009F000 + START_ADDR)				/// Ctrlram	, Size 11KB
#define MMAP_MP_DYNAMIC_CTRLRAM         		((UINT32)0x000A1C00 + START_ADDR)				/// MP Ctrlram	, Size 9KB
/*
**	Flashmap
*/	
#define FLASHMAP_HEADER_INFO                    (0x00000)					/// Header Info Setting @Flash 0KB, Size 256 Bytes
#define FLASHMAP_AUTOBUILD_SVN                  (0x00024)    				/// Offset 36-Byte

#define FLASHMAP_FW_REGISTER                    (0x22000)       			/// FW settings @Flash, Size 2.5KB
#define FLASHMAP_EMI_REG_TUNING                 (0x22780)    				/// DP/TP EMI Register Tuning
#define FLASHMAP_CTRLRAM                        (0x22800)
#define FLASHMAP_MP_CTRLRAM                     (0x25400)
#define FLASHMAP_DIFF_DLM                       (0x27800)
#define FLASHMAP_NF_TABLE                       (0x2C800)
#define FLASHMAP_VNCTRLRAM                      (0x315D0)       			/// 72+128+624
#define FLASHMAP_HEADER_CPY                     (0x32A70)
#define FLASHMAP_ENDFLAG                        (0x34FFC)                   /// End flag @Flash, size 4Bytes
#define FLASHMAP_FWCONFIG2                      (0x3B000)
#define FLASHMAP_INITIAL_CODE                   (0x3E000)    				/// DP initial code
#define FLASHMAP_LOCAL_DIMMING                  (0x40000)    				/// Local Dimming

#define FLASHMAP_MP_SHORT_CTRLRAM               (FLASHMAP_MP_CTRLRAM)    	/// MP Control RAM for Short @Flash 108KB, Size 3KB

#define FWCONFIG_SIZE                           (0x00780)   // (UINT16)2048 - (UINT16)128, 1920 Bytes
#define EMI_REG_TUNING_SIZE                     (0x00080)   // 128 Bytes
#define CTRLRAM_SZ                              (0x02c00)   // 11KB, 11264 Bytes
#define MP_CTRLRAM_SZ                           (0x02400)   // 9KB, 9216 Bytes
#define VN_CTRLRAM_SZ                           (0x0149E)   // 5278 Bytes
#define NF_CTRLRAM_SZ                           (0x02DD0)   // 11728 Bytes
#define NF_HEADER_SZ                            (0x00010)   // 16 Bytes
#define NF_REPLACE_RESERVED                     (0x02000)   // 8192 Bytes
#define MAX_2D_DOT_NUM                          (0x00B70)   // 2880(AA) + 48(Button), 2928 Bytes
#define MAX_SPI_DMA_VEC_SZ                      (0x00320)   // 5bytes * 80 vector, 800 Bytes
#define DIFF_DLM_SZ                             (0x02800)   // 5KB * 2(IC), 10240 Bytes
#define INITIAL_CODE_SZ                         (0x02000)   // 8KB, 8192 Bytes
#define LOCAL_DIMMING_SZ                        (0x22000)   // 136KB, 139264 Bytes
#define HEADER_SZ                               (0x00100)   // 256 Bytes
#define UNUSE_ALIG2BYTES						(0x02)
///< Ended by NT51926

#endif  /* INC_DEFINE_MMAP_H_ */
