/*
 *  Memory Map : swapTPD() is formally UpdateInfo();
 */

/*
 *   IP0 : 0x10000~0x11FFF
 */
///< Started by NT51931
#define 	CASCADE_CHIP_NUM 					(4)
#define 	SIF_I2C 							(0)
#define 	SIF_SPI 							(1)
#define 	SIF_INTERFACE 						(SIF_I2C)

//@CASCADE_EN : for cosim , noinfo block
#define 	INFO_BLOCK_SZ						(0x100)

#if (CASCADE_CHIP_NUM>1)
#define 	CASCADE_INFO						(0x00) //bit0:same code, bit1: next header
#else
#define 	CASCADE_INFO						(0x00) //bit0:same code, bit1: next header
#endif
#define 	SPI_OPTION							(0x00)

#if PC_SIMULATOR
#define 	MMap_S_2D_AdcRaw0           		(0x90FA0)//(0x80000)                   // for pcsim gui tool can parse correct name as 672A
#define 	MMap_S_2D_Diff0             		(0x98068)//(0x84C60)                   // for pcsim gui tool can parse correct name as 672A
#endif

/*
**	NT51931 mmap
*/
#define 	MMAP_ILM_START_ADDR 				(0x00000)
/*
**	IP1 : 0x80000~0x8C800 flash load to DLM
*/
#define		MMAP_DLM_START_ADDR 				(0x80000)
#define 	MMAP_FW_REGISTER            		(0x80000)
#define 	MMAP_FW_REGISTER_TP_REG            	(0x80700)
#define 	MMAP_FW_REGISTER_DP_REG            	(0x80740)
#define  	MMAP_S_2D_CR_NF_HEADER				(0x80800) 		/// 16 Bytes
#define 	MMAP_NF_HEADER_SUBSIZE      		(0x04)
#define 	MMAP_S_2D_CR_NF1            		(0x80810)   	/// 960+48 Bytes
#define 	MMAP_S_2D_CR_NF2            		(0x80C00)		/// 960+48 Bytes
#define 	MMAP_S_2D_CR_NF3            		(0x80FF0)    	/// 960+48 Bytes
#define 	MMAP_S_2D_CR_NF4            		(0x813E0)    	/// 960+48 Bytes
#define		MMAP_CTRLRAM                		(0x817D0)	 	/// Ctrlram	, Size 10KB
#define		MMAP_MP_DYNAMIC_CTRLRAM         	(0x83FD0)	 	/// MP Ctrlram	, Size 9KB
#define 	MMAP_VNOISE_CTRLRAM 				(0x863D0)    	/// 280 bytes
#define 	MMAP_PU_TBL 						(0x864E8)    	/// 512 bytes
#define 	MMAP_VNOISE_VN_TBL 					(0x866E8)    	/// 4036 bytes
#define 	MMAP_VNOISE_STAIR_TBL 				(0x876AC)    	/// 450 bytes
#define 	MMAP_1D_COL_GAING_TBL 				(0x8786E)    	/// 450 bytes
#define		MMAP_SPI_DMA_VEC					(0x87A30)		/// 400 bytes => no use
#if PC_SIMULATOR
#define 	MMAP_HEADER_INFO 					P_TYPE(UINT16, 0x8C000)    	/// Header Info Setting, Size 256 Bytes *2
#else
#define	 	MMAP_HEADER_INFO 					(0x87A30)    	/// Header , Size 256 Bytes
#endif
#define		MMAP_SPI_DMA_DEBUG_INFO				(0x87B30)		/// 4 bytes
#define		MMAP_SPI_DMA_COM_BUFFER				(0x87B34)		/// 140 bytes
#define	 	MMAP_DATA_BSS_START 				(0x87BC0)		///	for reference
/* ========================
**	.data + .bss 	~= 12K
**	.stack 		~= 7K
==========================*/

/*
**	IP2 : 0x8C800~0x9C000 flash load to DLM
*/
#define		MMAP_DIFFDLM						(0x8C800)		/// 3.5 KB (DIFFDLM Total 5KB)
#define		MMAP_DIFFDLM_NF						(0x8D600)		/// 1.5 KB (DIFFDLM Total 5KB)
//================= DIFF DLM Reused =====================
#define 	MMAP_AREA                   		(0x8C800)		/// Max Size = 1920 Bytes (80 zone)
#define 	MMAP_ZONE                   		(0x8CF80)		/// Max Size = 3200 Bytes
//================= HW OUTPUT ===========================
#define 	MMAP_S_2D_ADC_SCANRAW       		(0x8DC00)		/// 1920+96 Bytes
#define 	MMAP_IN_BAND_ND_SCANRAW     		(0x8E3E0)		///	688+32 Bytes
#define 	MMAP_GMD_SCANRAW            		(0x8E6B0)		/// 320+8 Bytes
#define 	MMAP_VN_2D_OUTPUT      				(0x8E7F8)		/// 388 bytes
#define 	MMAP_VN_1D_OUTPUT      				(0x8E97C)		/// 388 bytes
#define 	MMAP_SELF_TEST_COLSCAN            	(0x8EB00)		/// 320+8 Bytes
//================= SELF TEST ===========================
#define 	MMAP_SELF_TEST_COLRAW	     		(0x8EC48)		/// 320+8 Bytes
#define 	MMAP_SELF_TEST_OPEN               	(0x8ED90)		/// 1920
#define 	MMAP_SELF_TEST_SHORT               	(0x8F510)		/// 1920
#define 	MMAP_SELF_TEST_DEBUG 				(0x8FC90)    	/// 68

//================= CR OFFSET ===========================
#define 	MMAP_S_2D_CR_OFFSET_F1      		(0x8FCD4)       /// 1920+96 bytes
#define 	MMAP_S_2D_CR_OFFSET_F2      		(0x904B4)       /// 1920+96 bytes
#define 	MMAP_S_2D_CR_OFFSET_F3      		(0x90C94)       /// 1920+96 bytes
#define 	MMAP_S_2D_CR_OFFSET_F4      		(0x91474)       /// 1920+96 bytes

//================= 2D DATA =============================
#define 	MMAP_S_2D_RAWRSS					(0x91C54)		/// 960(CH)*2(short) = 1920 +96 bytes
#define 	MMAP_S_2D_RAWIIR            		(0x92434)		/// 960(CH)*2(short) = 1920 +96 bytes
#define 	MMAP_S_2D_BASELINE          		(0x92C14)		/// 960(CH)*2(short) = 1920 +96 bytes
#define 	MMAP_S_2D_DIFFPAST          		(0x933F4)		/// (24(ICM)+16)*40(Y)*2(short) = 3200 +128 bytes
#define 	MMAP_S_2D_DIFF              		(0x940F4)		/// (24(ICM)+16)*40(Y)*2(short) = 3200 +128 bytes
#define 	MMAP_S_2D_BUCKET            		(0x94DF4)		/// 1600+64 bytes (REMAINDER)
#define 	MMAP_S_2D_REMAINDER            		(0x94DF4)		/// 1600+64 bytes
#define 	MMAP_S_2D_RESETCOUNTER      		(0x95474)		/// 1600+64 bytes
#define 	MMAP_IN_BAND_ND_RAWRSS     			(0x95AF4)		/// 688+32 bytes
//================= 1D DATA =============================
#define 	MMAP_GMD_RAWIIR0            		(0x95DC4)		/// 320+8 bytes
#define 	MMAP_GMD_RAWIIR1            		(0x95F0C)		/// 320+8 bytes
#define 	MMAP_GMD_BASELINE0          		(0x96054)		/// 320+8 bytes
#define		MMAP_GMD_BASELINE1          		(0x9619C)		/// 320+8 bytes
#define 	MMAP_GMD_DIFF               		(0x962E4)		/// 320+8 bytes
#define 	MMAP_S_1D_CR_OFFSET0				(0x9642C)		/// 320+8 bytes
#define 	MMAP_S_1D_CR_OFFSET1               	(0x96574)		/// 320+8 bytes

#define 	MMAP_ALU_CMD_BASE           		(0x966BC)		/// ALU command section, Max Size = 48 Bytes
#define 	MMAP_FFM2CPU                		(0x966EC)		/// FFM2CPU Section, Max Size = 96 Bytes
#define 	MMAP_SPI_DMA_BUF					(0x9674C)		/// For Engineer debug using, Size:0x99680 + 128 byte
#define 	MMAP_IC_RST_SOURCE_INFO     		(0x967CC)		/// FW RESET Info, Max Size = 8 Bytes
#define 	MMAP_FW_FLOW_DEBUG_INFO     		(0x967D4)		/// FW Debug Variable, Max Size = 8 Bytes
#define 	MMAP_RESET_COUNTER          		(0x967DC)
//================= HISTORY =============================
#define 	MMAP_HISTORY_EVENT0         		(0x967E0)		/// 64 bytse for History Event, Max Size = 64 Bytes
#define 	MMAP_HISTORY_EVENT1         		(0x96820)     	/// 64 bytse for History Event, Max Size = 64 Bytes
#define 	MMAP_MP_ND_DATA             		(0x96860)		/// 16 bytes
#define 	MMAP_CPINFO                 		(0x96870)		/// 32 bytes for CP info read by FW
#define 	MMAP_CASCADE_FLOW_DEBUG_INFO 		(0x96890)    	/// 32 Bytes
//================= VNOISE =============================
#define 	MMAP_VN_PU_BL 						(0x968B0)    	/// (48*2+1)PU_MAX*4(word)*4(F1~F4) = 1552 bytes
#define 	MMAP_VN_PU_RAW 						(0x96EC0)    	/// (48*2+1)PU_MAX*4(word) = 388 bytes
//================= ASIL ================================
#define 	MMAP_CASCADE_REBOOT_CNT 			(0x97044)    	/// 2 Bytes Reboot Cnt
#define 	MMAP_OTP_CRC_FAIL_CNT 				(0x97046)    	/// 2 Bytes
#define 	MMAP_LVD_RECORD_BUF 				(0x97048)    	/// 2 Bytes
#define 	MMAP_WDT_FAIL_CNT 					(0x9704A)    	/// 2 Bytes
#define 	MMAP_ICS_ERROR_RECORD_BUF 			(0x9704C)    	/// 2 Bytes
#define 	MMAP_RESERVED 						(0x9704E)    	/// 178 bytes Reserved
//================= EVENT BUF ===========================
#define 	MMAP_EVENT_BUF              		(0x97100)		/// One page event buffer, Max Size = 256 Bytes
#define 	MMAP_EVENT_BUF_RESERVED     		(0x97200)		/// Event buffer reserved, Max Size = 256 Bytes
//================= COM BUF =============================
#define 	MMAP_COM_BUFFER     				(0x97300)		/// 19456 bytes = 19KB
#define 	MMAP_FLOW_DEBUG_RAM     			(0x9BF00)		/// Debug Print
#define 	MMAP_IP1_END_ADDR     				(0x9C000)		/// IP1 end

#define 	MMAP_FW_REGISTER_SIZE       		(0x800)						/// FW Config. Setting, Size 2KB
#define 	MMAP_HW_INFO_REGISTER       		(MMAP_EVENT_BUF + 0x78)		/// General Info.
#define 	MMAP_DNS_DIAGNOSTIC_BUF        		(MMAP_EVENT_BUF + EN_PUB_EVENT_MAP_DNS_DIAGNOSTIC)

//================= CNC STAIR TABLE =====================
#define 	DLM_TEMP_CNC_STAIR_TABLE 			P_TYPE(UINT16, MMAP_ZONE)
//#define		MMAP_SELF_TEST_COLRAW				(MMAP_DEBUG_RAM)

/*
**	Flashmap
*/
#define 	FLASHMAP_HEADER_INFO        		(0x00000)					/// Header Info Setting @Flash 0KB, Size 256 Bytes
#define 	FLASHMAP_AUTOBUILD_SVN      		(0x00024)					/// Offset 36-Byte

#define 	FLASHMAP_FW_REGISTER        		(0x16000)					/// FW settings @Flash, Size 2KB
#define 	FLASHMAP_FW_REGISTER_TP_REG 		(0x16700)    				/// EMI Tool Register Setting
#define 	FLASHMAP_FW_REGISTER_DP_REG 		(0x16740)    				/// EMI Tool Register Setting
#define 	FLASHMAP_NF_TABLE           		(0x16800)					/// Normal Factor  @Flash, Size 4048 Bytes
#define 	FLASHMAP_CTRLRAM            		(0x177D0)					/// Control RAM @Flash, Size 10KB
#define 	FLASHMAP_MP_CTRLRAM   				(0x19FD0)					/// MP Control RAM @Flash Size 9KB
#define 	FlashMap_VNCTRLRAM          		(0x1C3D0)					/// VN Control RAM @Flash Size 5728 Bytes
#define 	FLASHMAP_SPI_VEC        			(0x1DA30)					/// no used
#define 	FLASHMAP_HEADER        				(0x1DA30)					/// Header @Flash Size 256 Bytes

#define 	FLASHMAP_DIFF_DLM        			(0x22800)
#define 	FLASHMAP_ENDFLAG            		(0x2FFFC)					/// End flag @Flash, size 4Bytes
#define 	FLASHMAP_MP_SHORT_CTRLRAM   		(FLASHMAP_MP_CTRLRAM)		/// MP Control RAM for Short @Flash Size 3KB

#define		FWCONFIG_TOTAL_SIZE					(2048)						/// 2KB
#define 	NF_HEADER_SZ						(16)
#define 	MAX_2D_DOT_NUM						(1008)//(960+48)  					/// 80(AFE)*6(MUX)*2(L/R)+48(Key) = 1008 bytes
#define 	CTRLRAM_SZ							(10*1024)					/// 10KB
#define 	MP_CTRLRAM_SZ						(9*1024)					/// 9KB
#define 	VN_CTRLRAM_SZ						(5728)						/// 280(VN Ctrlram)+512(PU Table)+4036(VN Table)+450(Stair)+450(1D Col) = 5728 bytes
#define 	MAX_SPI_DMA_VEC_SZ					(400)						/// no used
#define 	HEADER_INFO_SIZE 					(256)						/// 256 bytes
#define 	DIFF_DLM_SZ 						(19*5*1024)					/// 95KB

///< Ended by NT51931
