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
///< Started by NT51930
#define NOVATEK_CHIP_ID                         ((UINT8)0x30)
#define CASCADE_CHIP_NUM                        (3)
#define PANEL_BC_EN                             (1)	// Do not change

#if (CASCADE_CHIP_NUM >= 3)
#define PRIMARRY_LOC_MIDDLE 					(1)
#else
#define PRIMARRY_LOC_MIDDLE 					(0)
#endif

#define SIF_I2C 								(0)
#define SIF_SPI 								(1)
#define SIF_INTERFACE   						(SIF_I2C)

#define DUAL_READ								(1)
#define QUAD_READ    							(2)
#define FLASH_BLD_PATH							(DUAL_READ)
#if(FLASH_BLD_PATH == DUAL_READ)
#define BLD_RD_CMD								(0x3B)
#define BLD_PATH 								(0x1A)
#else
#define BLD_RD_CMD								(0x6B)
#define BLD_PATH 								(0x1C)
#endif
#define BLD_DIV_CNT								(0x35)	//spidma 9M
#define T4T6_VAL 								(0x99)	//spidma 9M

//@CASCADE_EN : for cosim , noinfo block
#define INFO_BLOCK_SZ							(0x100)


#define MMAP_DLM_START_ADDR            			((UINT32)0x00080000)

#if PC_SIMULATOR
#include <basetsd.h>

#define VIRTUAL_MEM_CHIP_DLM_SIZE		  		(512*1024)
#define VIRTUAL_MEM_CHIP_ILM_SIZE		  		(16*1024)

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
**	NT51930 mmap
*/

//==============================================
// Data Access Check
//==============================================
// The ILM_RECORD_START_ADDR and ILM_RECORD_SIZE are different for each IC
// Please refer to doc\DataAccessCheck for more detail
#define ILM_RECORD_START_ADDR 					(0x14000)  // This value should be larger than text size, Ex: 0x14000 > text(80596=0x13AD4)
#define ILM_RECORD_SIZE       					(0x04000)    // 0x14000~0x187FF (should not be larger than unused ILM size)
#define MMAP_ILM_START_ADDR 					(0x00000)
#define MMAP_S_END_OF_ILM_DATA 					((UINT32)0x000187FF)
//==================================================================
/*
**	DLM
*/
//==================================================================

/*
**	IP1 : 0x80000~0x8DF00 flash load to DLM
*/

//================= FW REGISTER ===========================
#define MMAP_FW_REGISTER               			((UINT32)0x00080000 + START_ADDR)				// FWconfig = 2048-128 bytes = 1920
#define MMAP_EMI_REG_TUNING 	   				((UINT32)0x00080780 + START_ADDR)	 			// 128 Bytes for EMI Rgister Tuning
//================= EVENT BUF ===========================
#define MMAP_EVENT_BUF              			((UINT32)0x00080800 + START_ADDR)	
#define MMAP_EVENT_BUF_RESERVED     			((UINT32)0x00080900 + START_ADDR)	
#define GENERAL_INFO_SIZE                       (0x0027)
#define EVENT_BUF_SIZE                          (256)
#define MMAP_HW_INFO_REGISTER 					(MMAP_EVENT_BUF + EVENT_MAP_FWINFO_COPY1) 
#define MMAP_CUSTOMIZED_FUNC_SWITCH				(MMAP_EVENT_BUF + EVENT_MAP_CUSTOMER_FUN_SWITCH)
//================= NF ==================================
#define MMAP_S_2D_CR_NF_HEADER					((UINT32)0x00080A00 + START_ADDR) 				/// 16 Bytes
#define MMAP_S_2D_CR_NF1            			((UINT32)0x00080A10 + START_ADDR)   			/// 1600+80 Bytes
#define MMAP_S_2D_CR_NF2            			((UINT32)0x000810A0 + START_ADDR)				/// 1600+80 Bytes
#define MMAP_S_2D_CR_NF3            			((UINT32)0x00081730 + START_ADDR)    			/// 1600+80 Bytes
#define MMAP_S_2D_CR_NF4            			((UINT32)0x00081DC0 + START_ADDR)    			/// 1600+80 Bytes
//================= CtrlRAM =============================
#define	MMAP_CTRLRAM                			((UINT32)0x00082450 + START_ADDR)	 			/// Ctrlram	, Size 11KB
#define	MMAP_MP_DYNAMIC_CTRLRAM         		((UINT32)0x00085050 + START_ADDR)	 		    /// MP Ctrlram , Size 13KB
#define MMAP_VNOISE_CTRLRAM 					((UINT32)0x00088450 + START_ADDR)    			/// 280 bytes
#define MMAP_PU_TBL 							((UINT32)0x00088568 + START_ADDR)    			/// 512 bytes
#define MMAP_VNOISE_VN_TBL 						((UINT32)0x00088768 + START_ADDR)    			/// 4096 bytes
#define MMAP_VNOISE_STAIR_TBL 					((UINT32)0x00089768 + START_ADDR)    			/// 900 bytes
#define MMAP_1D_COL_GAING_TBL 					((UINT32)0x00089AEC + START_ADDR)    			/// 452 bytes
#define	MMAP_VNOISE_MASK_TABLE					((UINT32)0x00089CB0 + START_ADDR)				/// 256 bytes
#if PC_SIMULATOR
#define MMAP_HEADER_INFO 						P_TYPE(UINT16, 0x89DB0 + START_ADDR)    		/// Header Info Setting, Size 512 Bytes *2
#else
#define MMAP_HEADER_INFO 						((UINT32)0x00089DB0)    						/// Header , Size 512 Bytes
#endif

#define	MMAP_EVENT_BUF_TEMP						((UINT32)0x00089FB0 + START_ADDR)				/// 256 bytes
#define MMAP_PARA_COM_BUFFER 					((UINT32)0x0008A0B0 + START_ADDR)    			/// 168 bytes
#define PARA_COM_BUFFER_SIZE                    ((UINT16)168)
#define PARA_COM_BUFFER_SIZE_PER_DIE            (PARA_COM_BUFFER_SIZE / (UINT16)3) 				/// MMAP_PARA_COM_BUFFER Size / 3

//==================================================================
// .data + .bss 	~= 10K
// .stack 			~= 11K
//==================================================================

/*
**	IP2 : 0x90000~0xA7FFF flash load to DLM
*/

#define	MMAP_DIFFDLM							((UINT32)0x00090000 + START_ADDR)				/// 3 KB (DIFFDLM Total 5KB)
#define	MMAP_DIFFDLM_NF							((UINT32)0x00090C00 + START_ADDR)				/// 2 KB (DIFFDLM Total 5KB)
//================= DIFF DLM Reused =====================
#define MMAP_AREA                   			((UINT32)0x00090000 + START_ADDR)				/// Max Size = 2880 Bytes (80 zone)
#define MMAP_ZONE                   			((UINT32)0x00090B40 + START_ADDR)				/// Max Size = 5760 Bytes
//================= HW OUTPUT ===========================
#define MMAP_S_2D_ADC_SCANRAW       			((UINT32)0x000921C0 + START_ADDR)				/// 3200+160 Bytes
#define MMAP_IN_BAND_ND_SCANRAW     			((UINT32)0x00092EE0 + START_ADDR)				///	864 Bytes
#define MMAP_GMD_SCANRAW            			((UINT32)0x00093240 + START_ADDR)				/// 384 Bytes
#define MMAP_VNOISE_2D_OUTPUT      				((UINT32)0x000933CC + START_ADDR)				/// 644 bytes
#define MMAP_SELF_TEST_COLSCAN            		((UINT32)0x000938D4 + START_ADDR)				/// 768+24 Bytes
#define MMAP_SELF_TEST_COLSCAN_INTERNAL     	((UINT32)0x00093BEC + START_ADDR)				/// 768+24 Bytes
//================= SELF TEST ===========================
#define MMAP_SELF_TEST_COLRAW	     			((UINT32)0x00093F04 + START_ADDR)				/// 768+24 Bytes
#define MMAP_SELF_TEST_COLRAW_INTERNAL	    	((UINT32)0x0009421C + START_ADDR)				/// 768+24 Bytes
#define MMAP_SELF_TEST_OPEN_2D               	((UINT32)0x00094534 + START_ADDR)				/// 3200 Bytes
#define MMAP_SELF_TEST_SHORT_2D               	((UINT32)0x000951B4 + START_ADDR)				/// 3200 Bytes
#define MMAP_SELF_TEST_DEBUG 					((UINT32)0x00095E34 + START_ADDR)    			/// 68 Bytes
//================= 2D DATA =============================
#define MMAP_S_2D_RAWRSS						((UINT32)0x00095E78 + START_ADDR)				/// 3200 +160 bytes
#define MMAP_S_2D_RAWIIR            			((UINT32)0x00096B98 + START_ADDR)				/// 3200 +160 bytes
#define MMAP_S_2D_BASELINE          			((UINT32)0x000978B8 + START_ADDR)				/// 3200 +160 bytes
#define MMAP_S_2D_DIFFPAST          			((UINT32)0x000985D8 + START_ADDR)				/// 3200 +160 bytes
#define MMAP_S_2D_DIFF              			((UINT32)0x000992F8 + START_ADDR)				/// 5760 +192 bytes
#define MMAP_S_2D_REM_BR            			((UINT32)0x0009AA38 + START_ADDR)				/// 3200 +160 bytes
//================= IN BAND ND ==========================
#define MMAP_IN_BAND_ND_RAWRSS     				((UINT32)0x0009B758 + START_ADDR)				/// 864 Bytes
//================= 1D DATA =============================
#define MMAP_GMD_RAWIIR0            			((UINT32)0x0009BAB8 + START_ADDR)				/// 384+12 bytes
#define MMAP_GMD_RAWIIR1            			((UINT32)0x0009BC44 + START_ADDR)				/// 384+12 bytes
#define MMAP_GMD_BASELINE0          			((UINT32)0x0009BDD0 + START_ADDR)				/// 384+12 bytes
#define	MMAP_GMD_BASELINE1          			((UINT32)0x0009BF5C + START_ADDR)				/// 384+12 bytes
#define MMAP_GMD_DIFF               			((UINT32)0x0009C0E8 + START_ADDR)				/// 384+12 bytes
//================= HW IP ===============================
#define MMAP_ALU_CMD_BASE           			((UINT32)0x0009C274 + START_ADDR)				/// ALU command section, Max Size = 48 Bytes
#define MMAP_FFM2CPU_HOST                		((UINT32)0x0009C2A4 + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_FFM2CPU                			((UINT32)0x0009C2B4 + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
//================= VNOISE ==============================
#define MMAP_VN_PU_BL 							((UINT32)0x0009C2C4 + START_ADDR)    			/// (80*2+1)PU_MAX*4(word)*4(F1~F4) = 2576 bytes
#define MMAP_VN_PU_RAW 							((UINT32)0x0009CCD4 + START_ADDR)    			/// (80*2+1)PU_MAX*4(word) = 644 bytes
//================= CR OFFSET ===========================
#define MMAP_S_2D_CR_OFFSET_F1      			((UINT32)0x0009CF58 + START_ADDR)       		/// 3200+160 bytes
#define MMAP_S_2D_CR_OFFSET_F2      			((UINT32)0x0009DC78 + START_ADDR)       		/// 3200+160 bytes
#define MMAP_S_2D_CR_OFFSET_F3      			((UINT32)0x0009E998 + START_ADDR)       		/// 3200+160 bytes
#define MMAP_S_2D_CR_OFFSET_F4      			((UINT32)0x0009F6B8 + START_ADDR)       		/// 3200+160 bytes
#define MMAP_RW_CROFFSET_KEY					((UINT32)0x000A03D8 + START_ADDR)				/// 2 Bytes
#define MMAP_RW_CROFFSET_CHECKSUM				((UINT32)0x000A03DA + START_ADDR)				/// 2 Bytes
//================= HISTORY =============================
#define MMAP_IC_RST_SOURCE_INFO     			((UINT32)0x000A03DC + START_ADDR)				/// FW RESET Info, Max Size = 8 Bytes
#define MMAP_RESET_COUNTER          			((UINT32)0x000A03E4 + START_ADDR)				/// 4 Bytes
#define MMAP_RESERVED0          				((UINT32)0x000A03E8 + START_ADDR)				/// 24 Bytes
#define MMAP_HISTORY_EVENT0         			((UINT32)0x000A0400 + START_ADDR)				/// 64 bytse for History Event, Max Size = 64 Bytes
#define MMAP_HISTORY_EVENT1         			((UINT32)0x000A0440 + START_ADDR)     			/// 64 bytse for History Event, Max Size = 64 Bytes
//================= ASIL ================================
#define MMAP_INIT_FAIL_CNT 						((UINT32)0x000A0480 + START_ADDR)    			/// 1 Bytes
#define MMAP_INIT_FAIL_CNT_BAR 					((UINT32)0x000A0481 + START_ADDR)    			/// 1 Bytes
#define MMAP_RUNTIME_FAIL_CNT 					((UINT32)0x000A0482 + START_ADDR)    			/// 1 Bytes
#define MMAP_RUNTIME_FAIL_CNT_BAR 				((UINT32)0x000A0483 + START_ADDR)    			/// 1 Bytes
#define MMAP_WDT_FAIL_TEMP 						((UINT32)0x000A0484 + START_ADDR)    			/// 2 Bytes
#define MMAP_ASIL_RESERVED 						((UINT32)0x000A0486 + START_ADDR)    			/// 42 Bytes Reserved
#define MMAP_POWER_ON_KEY						((UINT32)0x000A04B0 + START_ADDR)				/// 4 Bytes
//================= INFO ================================
#define MMAP_CASC_DEBUG_BUF						((UINT32)0x000A04B4 + START_ADDR) 				/// For Engineer debug using, Size: 128 Bytes
#define MMAP_FW_FLOW_DEBUG_INFO     			((UINT32)0x000A0534 + START_ADDR)				/// FW Debug Variable, Max Size = 8 Bytes
#define MMAP_MP_ND_DATA             			((UINT32)0x000A053C + START_ADDR)				/// 16 Bytes
#define MMAP_CPINFO                 			((UINT32)0x000A054C + START_ADDR)				/// 32 bytes for CP info read by FW
#define MMAP_ASIL_F2C_DP_STATUS					((UINT32)0x000A056C + START_ADDR)				/// 4 Bytes
#define MMAP_TUNING_RECORED_DATA				((UINT32)0x000A0570 + START_ADDR)				/// 8 Bytes
#define MMAP_DNS_DIAGNOSTIC_INFO				((UINT32)0x000A0578 + START_ADDR)				/// 8 Bytes
#define MMAP_ADC_OV_DETECT_OUTPUT				((UINT32)0x000A0580 + START_ADDR)				/// 40 Bytes
#define MMAP_TMP_BUF0_FOR_ALU           		((UINT32)0x000A05A8 + START_ADDR)				/// 2 Bytes
#define MMAP_TMP_BUF1_FOR_ALU           		((UINT32)0x000A05AA + START_ADDR)				/// 2 Bytes
#define MMAP_DEBUG_BUFFER           			((UINT32)0x000A05AC + START_ADDR)				/// 12 Bytes
#define MMAP_HOST_CMD_BACKUP           			((UINT32)0x000A05B8 + START_ADDR)				/// 4 Bytes
#define MMAP_RESERVED1           				((UINT32)0x000A05BC + START_ADDR)				/// 68 Bytes
//================= COM BUF =============================
#define MMAP_REPORT_COMBUF     					((UINT32)0x000A0600 + START_ADDR)				/// 12800 bytes = 12.5KB
#define MMAP_ALG_COMBUF_0                       ((UINT32)0x000A3800 + START_ADDR)               /// 5760 +192 bytes
#define MMAP_ALG_COMBUF_1                       ((UINT32)0x000A4F40 + START_ADDR)               /// 5760 +192 bytes
#define MMAP_REF_DIFF							((UINT32)0x000A6680 + START_ADDR)				/// 3200 + 48 Bytes
//================= CTRLRAM OFFSET ======================
#define MMAP_CTRLRAM_OFFSET_MAP					((UINT32)0x000A7330 + START_ADDR)				/// 3200 + 48 Bytes
//================= END ===============================
#define MMAP_END_ADDR							((UINT32)0x000A7FFC + START_ADDR)
#define MMAP_DATA_COLLECTION_KEY_BUF 			((UINT32)0x000A7FFC + START_ADDR)    			/// 4 Bytes Key for handshake


/*
**	Flashmap
*/	
#define FLASHMAP_HEADER_INFO                    (0x07000)  /// Header Info Setting @Flash 0KB, Size 256+256 Bytes
#define FLASHMAP_AUTOBUILD_SVN                  (0x07024)  /// Offset 36-Bytes

#define FLASHMAP_FW_REGISTER                    (0x1F200)  /// FW settings @Flash, Size 1920 Bytes
#define FLASHMAP_EMI_REG_TUNING                 (0x1F980)  /// DP/TP EMI Register Tuning, Size 128 Bytes
#define FLASHMAP_EVENTBUF                       (0x1FA00)  /// EventBuf 256+256 Bytes
#define FLASHMAP_NF_TABLE                       (0x1FC00)  /// Normal Factor  @Flash, Size 6736 Bytes
#define FLASHMAP_CTRLRAM                        (0x21650)  /// Control RAM @Flash, Size 11KB
#define FLASHMAP_MP_CTRLRAM                     (0x24250)  /// Control RAM @Flash, Size 18.5KB
#define FLASHMAP_VNCTRLRAM                      (0x27650)  /// VN Control RAM @Flash Size 6496 Bytes
#define FLASHMAP_HEADER_COPY                    (0x28FB0)  /// 256 Bytes
//add #define FLASHMAP_EVENT_BUF_TEMP           (0x27FF0)  /// 256 Bytes


#define FLASHMAP_DIFF_DLM                       (0x2F200)   /// Diff code
#define FLASHMAP_INITIAL_CODE                   (0x00000)   /// DP initial code
#define FLASHMAP_LOCAL_DIMMING                  (0x40000)   /// Local Dimming

#define FWCONFIG_SIZE                           (0x00780)   /// (UINT16)2048 - (UINT16)128, 1920 Bytes
#define EMI_REG_TUNING_SIZE                     (0x00080)   /// 128 Bytes
#define EVENT_BUF_RESERVED_SIZE                 (0x00200)

#define NF_CTRLRAM_SZ                           (0x1A50)    /// 6736 Bytes
#define NF_HEADER_SZ                            (0x00010)   /// 16 Bytes
#define MAX_2D_DOT_NUM                          (0x00690)   /// 1600(AA) + 80(Button), 1680 Bytes
#define CTRLRAM_SZ                              (0x02C00)	/// 11K Bytes
#define MP_CTRLRAM_SZ                           (0x03400)	/// 13K Bytes
#define VN_CTRLRAM_SZ                           (0x01960)   /// 280(VN Ctrlram)+512(PU Table)+4096(VN Table)+900(Stair)+452(1D Col)+256(Mask) = 6496
#define HEADER_SZ                               (0x00200)   /// 512 Bytes
#if(CASCADE_CHIP_NUM < 14)
#define DIFF_DLM_SZ                             (0x0FE00)   /// 5KB * 12(IC), 65024 Bytes
#else
#define DIFF_DLM_SZ                             (0x23000)   /// 5KB * 28(IC), 143360 Bytes
#endif
#define INITIAL_CODE_SZ                         (0x06000)   /// 24KB, 24576 Bytes
#define LOCAL_DIMMING_SZ                        (0x22000)   /// 136KB, 139264 Bytes

///< Ended by NT51930

#endif  /* INC_DEFINE_MMAP_H_ */
