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
///< Started by NT51932
#define NT51919 								(0x01)
#define NT51929 								(0x02)
#define NT51932 								(0x03)
#define IC_SEL 									(NT51929)

#if(IC_SEL == NT51929 || IC_SEL == NT51919)
#define	NOVATEK_CHIP_ID 				        ((UINT8)0x29)
#define NOVATEK_CHIP_ID_2						((UINT8)0x19)
#else
#define	NOVATEK_CHIP_ID 				        ((UINT8)0x32)
#define NOVATEK_CHIP_ID_2						((UINT8)0xFF)
#endif

#define CASCADE_CHIP_NUM                        (2)
#define PANEL_BC_EN                             (0)	// panel_select

#if ( (CASCADE_CHIP_NUM == 3 && PANEL_BC_EN == 0) || (CASCADE_CHIP_NUM > 8 && PANEL_BC_EN == 1))
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

#if (PANEL_BC_EN == 0) //TDDI
	#if(CASCADE_CHIP_NUM == 2)
		#define DLM_BLD_LENGTH					(IP0_TOATAL_SZ + DIFF_CODE_SZ - 1) //74480 = 0x922F0(DiffDLM start) - 0x80000
	#elif(CASCADE_CHIP_NUM == 3)
		#define DLM_BLD_LENGTH					(IP0_TOATAL_SZ + (DIFF_CODE_SZ * 2) - 1)
	#else
		#define DLM_BLD_LENGTH					(IP0_TOATAL_SZ - 1)
	#endif

	#if(FLASH_BLD_PATH == DUAL_READ)
	#define BLD_RD_CMD							(0x3B)
	#define BLD_PATH 							(0x1B)
	#else //QUAD_READ
	#define BLD_RD_CMD							(0x6B)
	#define BLD_PATH 							(0x1D)
	#endif
#else //LTDI
	#define DLM_BLD_LENGTH						(IP0_TOATAL_SZ - 1)

	#if(FLASH_BLD_PATH == DUAL_READ)
	#define BLD_RD_CMD							(0x3B)
	#define BLD_PATH 							(0x1A)
	#else //QUAD_READ
	#define BLD_RD_CMD							(0x6B)
	#define BLD_PATH 							(0x1C)
	#endif
#endif
#define BLD_DIV_CNT								(0x35)	//flash 8M
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
**	NT51932 mmap
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
#define MMAP_FW_REGISTER               			((UINT32)0x00080000 + START_ADDR)				/// FWconfig = 2048-128 bytes = 1920
#define MMAP_EMI_REG_TUNING 	   				((UINT32)0x00080780 + START_ADDR)	 			/// 128 Bytes for EMI Rgister Tuning
//================= EVENT BUF ===========================
#define MMAP_EVENT_BUF              			((UINT32)0x00080800 + START_ADDR)	
#define MMAP_EVENT_BUF_RESERVED     			((UINT32)0x00080900 + START_ADDR)	
#define GENERAL_INFO_SIZE                       (0x0027)
#define EVENT_BUF_SIZE                          (256)
#define MMAP_HW_INFO_REGISTER 					(MMAP_EVENT_BUF + EVENT_MAP_FWINFO_COPY1) 
#define MMAP_CUSTOMIZED_FUNC_SWITCH				(MMAP_EVENT_BUF + EVENT_MAP_CUSTOMER_FUN_SWITCH)
//================= NF ==================================
#define MMAP_S_2D_CR_NF_HEADER					((UINT32)0x00080A00 + START_ADDR) 				/// 16 Bytes
#define MMAP_S_2D_CR_NF1            			((UINT32)0x00080A10 + START_ADDR)   			/// 1920+96 Bytes
#define MMAP_S_2D_CR_NF2            			((UINT32)0x000811F0 + START_ADDR)				/// 1920+96 Bytes
#define MMAP_S_2D_CR_NF3            			((UINT32)0x000819D0 + START_ADDR)    			/// 1920+96 Bytes
#define MMAP_S_2D_CR_NF4            			((UINT32)0x000821B0 + START_ADDR)    			/// 1920+96 Bytes
//================= DualBL reused NF2&3 =================
#define MMAP_S_2D_RAWIIR2            			((UINT32)0x000811F0 + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
//================= CtrlRAM =============================
#define	MMAP_CTRLRAM                			((UINT32)0x00082990 + START_ADDR)	 			/// Ctrlram	, Size 18.5KB
#define MMAP_VNOISE_CTRLRAM 					((UINT32)0x00087390 + START_ADDR)    			/// 280 bytes
#define MMAP_PU_TBL 							((UINT32)0x000874A8 + START_ADDR)    			/// 512 bytes
#define MMAP_VNOISE_VN_TBL 						((UINT32)0x000876A8 + START_ADDR)    			/// 4096 bytes
#define MMAP_VNOISE_STAIR_TBL 					((UINT32)0x000886A8 + START_ADDR)    			/// 900 bytes
#define MMAP_1D_COL_GAING_TBL 					((UINT32)0x00088A2C + START_ADDR)    			/// 452 bytes
#define	MMAP_VNOISE_MASK_TABLE					((UINT32)0x00088BF0 + START_ADDR)				/// 256 bytes
//================= DualBL reused VN Ctrlram ==============
#define MMAP_S_2D_REM_BR2            			((UINT32)0x00087390 + START_ADDR)				/// 3840 + 192 Bytes

#if PC_SIMULATOR
#define MMAP_HEADER_INFO 						P_TYPE(UINT16, 0x88CF0 + START_ADDR)    		/// Header Info Setting, Size 256 Bytes *2
#else
#define MMAP_HEADER_INFO 						((UINT32)0x00088CF0)    						/// Header , Size 512 Bytes
#endif
#define MMAP_EVENT_BUF_TEMP 					((UINT32)0x00088EF0 + START_ADDR)    			/// EventBuf Temp , Size 256 Bytes

//==================================================================
// .data + .bss 	~= 10K
// .stack 			~= 10K
//==================================================================

/*
**	IP2 : 0x8DF00~0xA8000 flash load to DLM
*/

#define	MMAP_DIFFDLM							((UINT32)0x0008DF00 + START_ADDR)				/// 2960 bytes (DIFFDLM Total 5KB)
#define	MMAP_DIFFDLM_NF							((UINT32)0x0008EA90 + START_ADDR)				/// 2160 bytes (DIFFDLM Total 5KB)
//================= DIFF DLM Reused =====================
#define MMAP_AREA                   			((UINT32)0x0008DF00 + START_ADDR)				/// Max Size = 1440 Bytes (60 zone)
#define MMAP_ZONE                   			((UINT32)0x0008E4A0 + START_ADDR)				/// Max Size = 6400 Bytes
//================= SELF TEST ===========================
#define MMAP_SELF_TEST_COLRAW	     			((UINT32)0x0008FDA0 + START_ADDR)				/// 484 Bytes
#define MMAP_SELF_TEST_INTERNAL_COLRAW	    	((UINT32)0x0008FF84 + START_ADDR)				/// 484 Bytes
#define MMAP_SELF_TEST_OPEN_2D               	((UINT32)0x00090168 + START_ADDR)				/// 3840 Bytes
#define MMAP_SELF_TEST_SHORT_2D               	((UINT32)0x00091068 + START_ADDR)				/// 3840 Bytes
#define MMAP_SELF_TEST_DEBUG 					((UINT32)0x00091F68 + START_ADDR)    			/// 8 Bytes
//================= HW IP ===============================
#define MMAP_ALU_CMD_BASE           			((UINT32)0x00091F70 + START_ADDR)				/// ALU command section, Max Size = 56 Bytes
#define MMAP_TMP_BUF0_FOR_ALU           		((UINT32)0x00091FA8 + START_ADDR)				/// 2 Bytes
#define MMAP_TMP_BUF1_FOR_ALU           		((UINT32)0x00091FAA + START_ADDR)				/// 2 Bytes
#define MMAP_FFM2CPU                			((UINT32)0x00091FAC + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_FFM2CPU_HOST                		((UINT32)0x00091FBC + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_CASC_DEBUG_BUF						((UINT32)0x00091FCC + START_ADDR)				/// For Engineer debug using, Size: 128 Bytes
#define MMAP_DNS_DIAGNOSTIC_INFO				((UINT32)0x0009204C + START_ADDR)				/// 8 Bytes
#define MMAP_ADC_OV_DETECT_OUTPUT				((UINT32)0x00092054 + START_ADDR)				/// 40 Bytes
#define MMAP_RESERVED1							((UINT32)0x0009207C + START_ADDR)				/// 4 Bytes
//================= HW OUTPUT ===========================
#define MMAP_S_2D_ADC_SCANRAW       			((UINT32)0x00092080 + START_ADDR)				/// 3840 + 192 Bytes
#define MMAP_IN_BAND_ND_SCANRAW     			((UINT32)0x00093040 + START_ADDR)				///	1008 + 16 Bytes
#define MMAP_GMD_SCANRAW            			((UINT32)0x00093440 + START_ADDR)				/// 480 + 4 Bytes
#define MMAP_VNOISE_2D_OUTPUT      				((UINT32)0x00093624 + START_ADDR)				/// 772 bytes
#define MMAP_SELF_TEST_COLSCAN            		((UINT32)0x00093928 + START_ADDR)				/// 484 Bytes
#define MMAP_SELF_TEST_INTERNAL_COLSCAN     	((UINT32)0x00093B0C + START_ADDR)				/// 484 Bytes
//================= 2D DATA =============================
#define MMAP_S_2D_RAWRSS						((UINT32)0x00093CF0 + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
#define MMAP_S_2D_RAWIIR            			((UINT32)0x00094CB0 + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
#define MMAP_S_2D_BASELINE          			((UINT32)0x00095C70 + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
#define MMAP_S_2D_DIFFPAST          			((UINT32)0x00096C30 + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
#define MMAP_S_2D_DIFF              			((UINT32)0x00097BF0 + START_ADDR)				/// (24(ICM)+16)*80(Y)*2(short) = 6400 + 192 Bytes
#define MMAP_S_2D_REM_BR            			((UINT32)0x000995B0 + START_ADDR)				/// 3840 + 192 Bytes
//================= CTRLRAM OFFSET ======================
#define MMAP_CTRLRAM_OFFSET_MAP					((UINT32)0x0009A570 + START_ADDR)				/// 3840 + 32 Bytes
#define MMAP_REF_DIFF							((UINT32)0x0009B490 + START_ADDR)				/// 3840 + 32 Bytes
//================= IN BAND ND ==========================
#define MMAP_IN_BAND_ND_RAWRSS     				((UINT32)0x0009C3B0 + START_ADDR)				/// 1008 + 16 Bytes
//================= 1D DATA =============================
#define MMAP_GMD_RAWIIR0            			((UINT32)0x0009C7B0 + START_ADDR)				/// 480 + 4 Bytes
#define MMAP_GMD_RAWIIR1            			((UINT32)0x0009C994 + START_ADDR)				/// 480 + 4 Bytes
#define MMAP_GMD_BASELINE0          			((UINT32)0x0009CB78 + START_ADDR)				/// 480 + 4 Bytes
#define	MMAP_GMD_BASELINE1          			((UINT32)0x0009CD5C + START_ADDR)				/// 480 + 4 Bytes
#define MMAP_GMD_DIFF               			((UINT32)0x0009CF40 + START_ADDR)				/// 480 + 4 Bytes
//================= HISTORY =============================
#define MMAP_IC_RST_SOURCE_INFO     			((UINT32)0x0009D124 + START_ADDR)				/// FW RESET Info, Max Size = 8 Bytes
#define MMAP_RESET_COUNTER          			((UINT32)0x0009D12C + START_ADDR)				/// 4 Bytes
#define MMAP_HISTORY_EVENT0         			((UINT32)0x0009D130 + START_ADDR)				/// 64 bytse for History Event, Max Size = 64 Bytes
#define MMAP_HISTORY_EVENT1         			((UINT32)0x0009D170 + START_ADDR)     			/// 64 bytse for History Event, Max Size = 64 Bytes
//================= ASIL ================================
#define MMAP_POWER_ON_KEY						((UINT32)0x0009D1B0 + START_ADDR)				/// 4 Bytes
#define MMAP_RW_CROFFSET_KEY					((UINT32)0x0009D1B4 + START_ADDR)				/// 2 Bytes
#define MMAP_RW_CROFFSET_CHECKSUM				((UINT32)0x0009D1B6 + START_ADDR)				/// 2 Bytes
#define MMAP_ASIL_INFO 							((UINT32)0x0009D1B8 + START_ADDR)    			/// 51 Bytes
#define MMAP_EMS_BACKUP 						((UINT32)0x0009D1EB + START_ADDR)    			/// 1 Bytes
//================= CR OFFSET ===========================
#define MMAP_S_2D_CR_OFFSET_F1      			((UINT32)0x0009D1EC + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F2      			((UINT32)0x0009E1AC + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F3      			((UINT32)0x0009F16C + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F4      			((UINT32)0x000A012C + START_ADDR)       		/// 3840 + 192 Bytes
//================= DualBL reused OFFSET2&3 =============
#define MMAP_S_2D_CR_OFFSET_F1_1      			((UINT32)0x0009D1EC + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F1_2      			((UINT32)0x0009E1AC + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F4_1      			((UINT32)0x0009F16C + START_ADDR)       		/// 3840 + 192 Bytes
#define MMAP_S_2D_CR_OFFSET_F4_2      			((UINT32)0x000A012C + START_ADDR)       		/// 3840 + 192 Bytes
//================= VNOISE ==============================
#define MMAP_VN_PU_BL 							((UINT32)0x000A10EC + START_ADDR)    			/// (96*2+1)PU_MAX*4(word)*4(F1~F4) = 3088 Bytes
#define MMAP_VN_PU_RAW 							((UINT32)0x000A1CFC + START_ADDR)    			/// (96*2+1)PU_MAX*4(word) = 772 Bytes
//================= DualBL reused VN ====================
#define MMAP_S_2D_BASELINE2 					((UINT32)0x000A10EC + START_ADDR)				/// 960(CH)*2(short)*2(tp_mux) = 3840 + 192 Bytes
//================= COM BUF =============================
#define MMAP_ALG_COMBUF_0                       ((UINT32)0x000A2000 + START_ADDR)               /// 6400  Bytes
#define MMAP_ALG_COMBUF_1                       ((UINT32)0x000A3900 + START_ADDR)               /// 6400  Bytes
#define MMAP_REPORT_COMBUF     					((UINT32)0x000A5200 + START_ADDR)				/// 11520 Bytes
//================= INFO ================================
#define MMAP_FW_FLOW_DEBUG_INFO     			((UINT32)0x000A7F00 + START_ADDR)				/// FW Debug Variable, Max Size = 8 Bytes
#define MMAP_MP_ND_DATA             			((UINT32)0x000A7F08 + START_ADDR)				/// 16 Bytes
#define MMAP_CPINFO                 			((UINT32)0x000A7F18 + START_ADDR)				/// 32 bytes for CP info read by FW
#define MMAP_ASIL_F2C_DP_STATUS					((UINT32)0x000A7F38 + START_ADDR)				/// 4 Bytes
#define MMAP_TUNING_RECORED_DATA				((UINT32)0x000A7F3C + START_ADDR)				/// 8 Bytes
//================= DEBUG ===============================
#define MMAP_DEBUG_BUFFER     					((UINT32)0x000A7F44 + START_ADDR)				/// 8 Bytes for Debug Print
#define MMAP_PARA_COM_BUFFER					((UINT32)0x000A7F4C + START_ADDR)				/// 168 Bytes
#define PARA_COM_BUFFER_SIZE                    ((UINT16)168)
#define PARA_COM_BUFFER_SIZE_PER_DIE            (PARA_COM_BUFFER_SIZE / (UINT16)3) 				/// MMAP_PARA_COM_BUFFER Size / 3
#define MMAP_HOST_CMD_BACKUP				    ((UINT32)0x000A7FF4 + START_ADDR)				/// 4 Bytes
#define MMAP_END_ADDR							((UINT32)0x000A7FF8 + START_ADDR)
#define MMAP_DATA_COLLECTION_KEY_BUF 			((UINT32)0x000A7FF8 + START_ADDR)    			/// 4 Bytes Key for handshake
/*
**	Flashmap
*/	
#define FLASHMAP_HEADER_INFO                    (0x07000)  /// Header Info Setting @Flash 0KB, Size 256+256 Bytes
#define FLASHMAP_AUTOBUILD_SVN 					(0x07024)  /// Offset 36-Bytes

#define FLASHMAP_FW_REGISTER 					(0x1F200)  /// FW settings @Flash, Size 1920 Bytes
#define FLASHMAP_EMI_REG_TUNING 				(0x1F980)  /// DP/TP EMI Register Tuning, Size 128 Bytes
#define FLASHMAP_EVENTBUF 						(0x1FA00)  /// EventBuf 256+256 Bytes
#define FLASHMAP_NF_TABLE                       (0x1FC00)  /// Normal Factor  @Flash, Size 8080 Bytes
#define FLASHMAP_CTRLRAM                        (0x21B90)  /// Control RAM @Flash, Size 18.5KB
#define FLASHMAP_VNCTRLRAM                      (0x26590)  /// VN Control RAM @Flash Size 6496 Bytes
#define FLASHMAP_HEADER_COPY                    (0x27EF0)  /// 256 Bytes
#define FLASHMAP_EVENT_BUF_TEMP 				(0x27FF0)  /// 256 Bytes


#define FLASHMAP_DIFF_DLM                       (0x2D100)   	/// Diff code
#define FLASHMAP_INITIAL_CODE 					(0x00000)   /// DP initial code
#define FLASHMAP_LOCAL_DIMMING 					(0x40000)   /// Local Dimming

#define FWCONFIG_SIZE                           (0x00780)   /// (UINT16)2048 - (UINT16)128, 1920 Bytes
#define EMI_REG_TUNING_SIZE                     (0x00080)   /// 128 Bytes
#define EVENT_BUF_RESERVED_SIZE					(0x00200)

#define IP0_TOATAL_SZ							(0x0DF00)
#define DIFF_CTRLRAM_SIZE 						(0x00B90)
#define DIFF_NF_SIZE 							(0x00870)
#define DIFF_CODE_SZ							(DIFF_CTRLRAM_SIZE + DIFF_NF_SIZE)

#define NF_CTRLRAM_SZ                           (0x01F90)   /// 8080 Bytes
#define NF_HEADER_SZ                            (0x00010)   /// 16 Bytes
#define MAX_2D_DOT_NUM                          (0x007E0)   /// 1920(AA) + 96(Button), 2016 Bytes
#define CTRLRAM_SZ                              (0x04A00)	/// 18.5K Bytes
#define VN_CTRLRAM_SZ                           (0x01960)   /// 280(VN Ctrlram)+512(PU Table)+4096(VN Table)+900(Stair)+452(1D Col)+256(Mask) = 6496
#define HEADER_SZ                               (0x00200)   /// 512 Bytes
#define DIFF_DLM_SZ                             (0x08C00)   /// 5KB*7
#define INITIAL_CODE_SZ                         (0x06000)   /// 24KB, 24576 Bytes
#define LOCAL_DIMMING_SZ                        (0x22000)   /// 136KB, 139264 Bytes

///< Ended by NT51932

#endif  /* INC_DEFINE_MMAP_H_ */
