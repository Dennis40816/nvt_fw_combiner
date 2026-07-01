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
///< Started by NT51950
#define	NOVATEK_CHIP_ID 				        ((UINT8)0x50)
#define	NOVATEK_CHIP_ID_2 				        ((UINT8)0x51)
#define CASCADE_CHIP_NUM                        (2)

#define SIF_I2C 								(0)
#define SIF_SPI 								(1)
#define SIF_INTERFACE   						(SIF_I2C)

#define DUAL_READ								(1)
#define QUAD_READ    							(2)
#define FLASH_BLD_PATH							(DUAL_READ)

//#if(CASCADE_CHIP_NUM == 2)
//		#define DLM_BLD_LENGTH					(IP0_TOATAL_SZ + DIFF_CODE_SZ - 1) //74480 = 0x922F0(DiffDLM start) - 0x80000
//#else
//		#define DLM_BLD_LENGTH					(IP0_TOATAL_SZ - 1)
//#endif

#if(CASCADE_CHIP_NUM == 2)
	#define DLM_BLD_LENGTH						(IP0_TOATAL_SZ + DIFF_CODE_SZ) // 5KB Diff code
#else
	#define DLM_BLD_LENGTH						(IP0_TOATAL_SZ) // 0x91000(DiffDLM start) - 0x80000(DLM start)
#endif

#if(FLASH_BLD_PATH == DUAL_READ) // flash 2x 8M
#define BLD_RD_CMD								(0x3B)
#define BLD_PATH 								(0x1B)  //same code
#else //QUAD_READ
#define BLD_RD_CMD								(0x6B)
#define BLD_PATH 								(0x1D)
#endif


#define BLD_DIV_CNT								(0x15)	//flash 7.5M
#define T4T6_VAL 								(0xAA)	//spidma 7.5M
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
**	NT51950 mmap
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
#define MMAP_S_2D_CR_NF1            			((UINT32)0x00080A10 + START_ADDR)   			/// 2560+128 Bytes
#define MMAP_S_2D_CR_NF2            			((UINT32)0x00081490 + START_ADDR)				/// 2560+128 Bytes
#define MMAP_S_2D_CR_NF3            			((UINT32)0x00081F10 + START_ADDR)    			/// 2560+128 Bytes
#define MMAP_S_2D_CR_NF4            			((UINT32)0x00082990 + START_ADDR)    			/// 2560+128 Bytes
//================= DualBL reused NF2&3 =================
#define MMAP_S_2D_RAWIIR2            			((UINT32)0x00081490 + START_ADDR)    			/// 1280(CH)*2(short)*2(tp_mux) = 5120 + 256 Bytes
//================= CtrlRAM =============================
#define	MMAP_CTRLRAM                			((UINT32)0x00083410 + START_ADDR)	 			/// Ctrlram	, Size 23KB
#define MMAP_VNOISE_CTRLRAM 					((UINT32)0x00089010 + START_ADDR)    			/// 280 bytes
#define MMAP_PU_TBL 							((UINT32)0x00089128 + START_ADDR)    			/// 512 bytes
#define MMAP_VNOISE_VN_TBL 						((UINT32)0x00089328 + START_ADDR)    			/// 5492 bytes
#define MMAP_VNOISE_STAIR_TBL 					((UINT32)0x0008A89C + START_ADDR)    			/// 1208 bytes
#define MMAP_1D_COL_GAING_TBL 					((UINT32)0x0008AD54 + START_ADDR)    			/// 608 bytes
#define	MMAP_VNOISE_MASK_TABLE					((UINT32)0x0008AFB4 + START_ADDR)				/// 344 bytes
//================= DualBL reused VN Ctrlram ==============
#define MMAP_S_2D_REM_BR2            			((UINT32)0x00089010 + START_ADDR)				/// 5120 + 256 Bytes

#define MMAP_HEADER_INFO 						((UINT32)0x0008B10C + START_ADDR)    			/// Header , Size 512 Bytes

//==================================================================
// .data + .bss 	~= 12K
// .stack 			~= 11K
//==================================================================

/*
**	IP2 : 0x8DF00~0xA8000 flash load to DLM
*/

#define	MMAP_DIFFDLM							((UINT32)0x00091000 + START_ADDR)				/// 2320 bytes (DIFFDLM Total 5KB)
#define	MMAP_DIFFDLM_NF							((UINT32)0x00091910 + START_ADDR)				/// 2800 bytes (DIFFDLM Total 5KB)
//================= DIFF DLM Reused =====================
#define MMAP_AREA                   			((UINT32)0x00091000 + START_ADDR)				/// Max Size = 1920 Bytes (80 zone)
#define MMAP_ZONE                   			((UINT32)0x00091780 + START_ADDR)				/// Max Size = 6400 Bytes
//================= HW OUTPUT ===========================
#define MMAP_S_2D_ADC_SCANRAW       			((UINT32)0x00093080 + START_ADDR)				/// 5120+256 Bytes
#define MMAP_IN_BAND_ND_SCANRAW     			((UINT32)0x00094580 + START_ADDR)				///	1344+64 Bytes
#define MMAP_GMD_SCANRAW            			((UINT32)0x00094B00 + START_ADDR)				/// 640+16 Bytes
#define MMAP_SELF_TEST_COLSCAN            		((UINT32)0x00094D90 + START_ADDR)				/// 640+16 Bytes
#define MMAP_SELF_TEST_INTERNAL_COLSCAN	   		((UINT32)0x00095020 + START_ADDR)				/// 640+16 Bytes
#define MMAP_VNOISE_2D_OUTPUT   				((UINT32)0x000952B0 + START_ADDR)				/// 1028 bytes
#define MMAP_VNOISE_1D_OUTPUT      				((UINT32)0x000956B4 + START_ADDR)				/// 24 bytes
//================= SELF TEST ===========================
#define MMAP_SELF_TEST_COLRAW	     			((UINT32)0x000956CC + START_ADDR)				/// 640+16 Bytes
#define MMAP_SELF_TEST_INTERNAL_COLRAW 			((UINT32)0x0009595C + START_ADDR)				/// 640+16 Bytes
#define MMAP_SELF_TEST_OPEN_2D             		((UINT32)0x00095BEC + START_ADDR)				/// 5120 Bytes
#define MMAP_SELF_TEST_SHORT_2D            		((UINT32)0x00096FEC + START_ADDR)				/// 5120 Bytes
#define MMAP_SELF_TEST_DEBUG 					((UINT32)0x000983EC + START_ADDR)    			/// 8 Bytes
//================= 2D DATA =============================
#define MMAP_S_2D_RAWRSS						((UINT32)0x000983F4 + START_ADDR)				/// 1280(CH)*2(short)*2(tp_mux) = 5120 + 256 bytes
#define MMAP_S_2D_RAWIIR            			((UINT32)0x000998F4 + START_ADDR)				/// 1280(CH)*2(short)*2(tp_mux) = 5120 + 256 bytes
#define MMAP_S_2D_BASELINE          			((UINT32)0x0009ADF4 + START_ADDR)				/// 1280(CH)*2(short)*2(tp_mux) = 5120 + 256 bytes
#define MMAP_S_2D_DIFFPAST          			((UINT32)0x0009C2F4 + START_ADDR)				/// 1280(CH)*2(short)*2(tp_mux) = 5120 + 256 bytes
#define MMAP_S_2D_DIFF              			((UINT32)0x0009D7F4 + START_ADDR)				/// (32(ICM)+8)*80(Y)*2(short) = 6400 +256 bytes
#define MMAP_S_2D_REM_BR            			((UINT32)0x0009F1F4 + START_ADDR)				/// 5120 + 256 bytes
//================= CTRLRAM OFFSET ======================
#define MMAP_CTRLRAM_OFFSET_MAP					((UINT32)0x000A06F4 + START_ADDR)				/// 5120 + 256 Bytes
#define MMAP_REF_DIFF							((UINT32)0x000A1BF4 + START_ADDR)				/// 5120 + 256 Bytes
//================= IN BAND ND ==========================
#define MMAP_IN_BAND_ND_RAWRSS     				((UINT32)0x000A30F4 + START_ADDR)				/// 1344 + 64 bytes
//================= 1D DATA =============================
#define MMAP_GMD_RAWIIR0            			((UINT32)0x000A3674 + START_ADDR)				/// 640+16 bytes
#define MMAP_GMD_RAWIIR1            			((UINT32)0x000A3904 + START_ADDR)				/// 640+16 bytes
#define MMAP_GMD_BASELINE0          			((UINT32)0x000A3B94 + START_ADDR)				/// 640+16 bytes
#define	MMAP_GMD_BASELINE1          			((UINT32)0x000A3E24 + START_ADDR)				/// 640+16 bytes
#define MMAP_GMD_DIFF               			((UINT32)0x000A40B4 + START_ADDR)				/// 640+16 bytes
//================= HW IP ===============================
#define MMAP_ALU_CMD_BASE           			((UINT32)0x000A4344 + START_ADDR)				/// ALU command section, Max Size = 56 Bytes
#define MMAP_TMP_BUF0_FOR_ALU           		((UINT32)0x000A437C + START_ADDR)				/// 2 Bytes
#define MMAP_TMP_BUF1_FOR_ALU          			((UINT32)0x000A437E + START_ADDR)				/// 2 Bytes
#define MMAP_FFM2CPU                			((UINT32)0x000A4380 + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_FFM2CPU_HOST              			((UINT32)0x000A4390 + START_ADDR)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_CASC_DEBUG_BUF						((UINT32)0x000A43A0 + START_ADDR)				/// For Engineer debug using, Size: 128 byte
#define MMAP_DNS_DIAGNOSTIC_INFO				((UINT32)0x000A4420 + START_ADDR)				/// 8 Bytes
#define MMAP_ADC_OV_DETECT_OUTPUT				((UINT32)0x000A4428 + START_ADDR)				/// 40 Bytes
//================= HISTORY =============================
#define MMAP_IC_RST_SOURCE_INFO     			((UINT32)0x000A4450 + START_ADDR)				/// FW RESET Info, Max Size = 8 Bytes
#define MMAP_RESET_COUNTER          			((UINT32)0x000A4458 + START_ADDR)				/// 4 Bytes
#define MMAP_HISTORY_EVENT0         			((UINT32)0x000A445C + START_ADDR)				/// 64 bytse for History Event, Max Size = 64 Bytes
#define MMAP_HISTORY_EVENT1         			((UINT32)0x000A449C + START_ADDR)     			/// 64 bytse for History Event, Max Size = 64 Bytes
//================= ASIL ================================
#define MMAP_POWER_ON_KEY						((UINT32)0x000A44DC + START_ADDR)				/// 4 Bytes
#define MMAP_RW_CROFFSET_KEY					((UINT32)0x000A44E0 + START_ADDR)				/// 2 Bytes
#define MMAP_RW_CROFFSET_CHECKSUM				((UINT32)0x000A44E2 + START_ADDR)				/// 2 Bytes

#define MMAP_ASIL_INFO 							((UINT32)0x000A44E4 + START_ADDR)    			/// 147 Bytes
#define MMAP_EMS_BACKUP 						((UINT32)0x000A4577 + START_ADDR)    			/// 1 Byte
//================= CR OFFSET ===========================
#define MMAP_S_2D_CR_OFFSET_F1      			((UINT32)0x000A4578 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F2      			((UINT32)0x000A5A78 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F3      			((UINT32)0x000A6F78 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F4      			((UINT32)0x000A8478 + START_ADDR)       		/// 5120 + 256 Bytes
//================= DualBL reused OFFSET2&3 =============
#define MMAP_S_2D_CR_OFFSET_F1_1      			((UINT32)0x000A4578 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F1_2      			((UINT32)0x000A5A78 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F4_1      			((UINT32)0x000A6F78 + START_ADDR)       		/// 5120 + 256 Bytes
#define MMAP_S_2D_CR_OFFSET_F4_2      			((UINT32)0x000A8478 + START_ADDR)       		/// 5120 + 256 Bytes
//================= VNOISE ==============================
#define MMAP_VN_PU_BL 							((UINT32)0x000A9978 + START_ADDR)    			/// (128*2+1)PU_MAX*4(word)*4(F1~F4) = 4112 bytes
#define MMAP_VN_PU_RAW 							((UINT32)0x000AA988 + START_ADDR)    			/// (128*2+1)PU_MAX*4(word) = 1028 bytes
//================= DualBL reused VN ====================
#define MMAP_S_2D_BASELINE2 					((UINT32)0x000A9978 + START_ADDR)				/// 1280(CH)*2(short)*2(tp_mux) = 5120 bytes
//================= COM BUF =============================
#define MMAP_REPORT_COMBUF                      ((UINT32)0x000AAD8C + START_ADDR)               /// 6400  Bytes
#define MMAP_ALG_COMBUF_1     					((UINT32)0x000AC68C + START_ADDR)				/// 6400  Bytes (2IC will be used by MMAP_REPORT_COMBUF)
#define MMAP_ALG_COMBUF_0                       ((UINT32)0x000ADF8C + START_ADDR)               /// 6400  Bytes
#define MMAP_RESERVED_0		                    ((UINT32)0x000AF88C + START_ADDR)               /// 1400  Bytes
//================= INFO ================================
#define	MMAP_EVENT_BUF_TEMP 					((UINT32)0x000AFE04 + START_ADDR)   		    /// EventBuf Temp , Size 256 Bytes
#define MMAP_FW_FLOW_DEBUG_INFO     			((UINT32)0x000AFF04 + START_ADDR)				/// FW Debug Variable, Max Size = 8 Bytes
#define MMAP_MP_ND_DATA             			((UINT32)0x000AFF0C + START_ADDR)				/// 16 Bytes
#define MMAP_CPINFO                 			((UINT32)0x000AFF1C + START_ADDR)				/// 32 bytes for CP info read by FW
#define MMAP_ASIL_F2C_DP_STATUS					((UINT32)0x000AFF3C + START_ADDR)				/// 4 Bytes
#define MMAP_TUNING_RECORED_DATA				((UINT32)0x000A7F3C + START_ADDR)				/// 8 Bytes
//================= DEBUG ===============================
#define MMAP_HOST_CMD_BACKUP				    ((UINT32)0x000AFF48 + START_ADDR)				/// 4 Bytes
#define MMAP_DEBUG_BUFFER     					((UINT32)0x000AFF4C + START_ADDR)				/// 4 Bytes for Debug Print

#define MMAP_PARA_COM_BUFFER					((UINT32)0x000AFF50 + START_ADDR)				/// 168 Bytes
#define PARA_COM_BUFFER_SIZE                    ((UINT16)168)
#define PARA_COM_BUFFER_SIZE_PER_DIE            (PARA_COM_BUFFER_SIZE / (UINT16)3) 				/// MMAP_PARA_COM_BUFFER Size / 3
#define MMAP_END_ADDR							((UINT32)0x000AFFF8 + START_ADDR)				/// IP1 end
#define MMAP_DATA_COLLECTION_KEY_BUF 			((UINT32)0x000AFFF8 + START_ADDR)    			/// 4 Bytes Key for handshake
/*
**	Flashmap (Don't use Tab!)
*/	
#define FLASHMAP_HEADER_INFO                    (0x0A000)  /// Header Info Setting @Flash 0KB, Size 256+256 Bytes
#define FLASHMAP_AUTOBUILD_SVN                  (0x0A024)  /// Offset 36-Bytes

#define FLASHMAP_FW_REGISTER                    (0x22200)  /// FW settings @Flash, Size 1920 Bytes
#define FLASHMAP_EMI_REG_TUNING                 (0x22980)  /// DP/TP EMI Register Tuning, Size 128 Bytes
#define FLASHMAP_EVENTBUF                       (0x22A00)  /// EventBuf 256+256 Bytes
#define FLASHMAP_NF_TABLE                       (0x22C00)  /// Normal Factor  @Flash, Size 8080 Bytes
#define FLASHMAP_CTRLRAM                        (0x25610)  /// Control RAM @Flash, Size 18.5KB
#define FLASHMAP_VNCTRLRAM                      (0x2B210)  /// VN Control RAM @Flash Size 8444 Bytes
#define FLASHMAP_HEADER_COPY                    (0x2D30C)  /// 512 Bytes

#define FLASHMAP_DIFF_DLM                       (0x33200)  /// Diff code
#define FLASHMAP_ENDFLAG                        (0x36FFC)  /// End flag @Flash, size 4Bytes
#define FLASHMAP_INITIAL_CODE                   (0x00000)  /// DP initial code

#define FWCONFIG_SIZE                           (0x00780)  /// (UINT16)2048 - (UINT16)128, 1920 Bytes
#define EMI_REG_TUNING_SIZE                     (0x00080)  /// 128 Bytes
#define EVENT_BUF_RESERVED_SIZE                 (0x00200)

#define IP0_TOATAL_SZ							(0x11000)
#define DIFF_CTRLRAM_SIZE 						(0x00910)
#define DIFF_NF_SIZE 							(0x00AF0)
#define DIFF_CODE_SZ							(DIFF_CTRLRAM_SIZE + DIFF_NF_SIZE)

#define NF_CTRLRAM_SZ                           (0x02A10)  /// 10768 Bytes
#define NF_HEADER_SZ                            (0x00010)  /// 16 Bytes
#define MAX_2D_DOT_NUM                          (0x00A80)  /// 2560(AA) + 128(Button), 2688 Bytes
#define CTRLRAM_SZ                              (0x05C00)  /// 23K Bytes
#define VN_CTRLRAM_SZ                           (0x020FC)  /// 280(VN Ctrlram)+512(PU Table)+5492(VN Table)+1208(Stair)+608(1D Col)+344(Mask) = 8444 bytes
#define HEADER_SZ                               (0x00200)  /// 512 Bytes
#define DIFF_DLM_SZ                             (0x01400)  /// 5KB
#define INITIAL_CODE_SZ                         (0x0A000)  /// 40KB, 40960 Bytes


///< Ended by NT51950

#endif  /* INC_DEFINE_MMAP_H_ */
