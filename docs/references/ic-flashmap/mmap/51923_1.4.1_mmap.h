/*
 *  Memory Map : swapTPD() is formally UpdateInfo();
 */
#ifndef HAL_MMAP_H_
#define HAL_MMAP_H_

/*
 *   IP0 : 0x10000~0x11FFF
 */
///< Started by NT51923
#define CASCADE_CHIP_NUM               	(3)

#define SIF_I2C 						(0)
#define SIF_SPI 						(1)
#define SIF_INTERFACE   				(SIF_I2C)

//@CASCADE_EN : for cosim , noinfo block
#define INFO_BLOCK_SZ					(0x100)

#if (CASCADE_CHIP_NUM > 1)
#define CASCADE_INFO					(0x01) 	//bit0:same code, bit1: next header
#else
#define CASCADE_INFO					(0x00) 	//bit0:same code, bit1: next header
#endif
#define SPI_OPTION                 		(0x30)	// BLD SPIDMA: 4MHz

#if PC_SIMULATOR
#define MMap_S_2D_AdcRaw0 				(0x00082E20)    	//(0x20000)                   // for pcsim gui tool can parse correct name as 672A
#define MMap_S_2D_Diff0 				(0x00085BE0)      	//(0x24C60)                   // for pcsim gui tool can parse correct name as 672A
#endif

/*
**	NT51923 mmap
*/
// REMOTE DEBUG@ ILM_END~0x20000
#define MMAP_ILM_END_ADDR              (MMAP_S_2D_CR_OFFSET_F1)
#define MMAP_REMOTE_DEBUG_ILM_END_ADDR (MMAP_ILM_END_ADDR-USER_DEFINE_REMOTE_DEBUG_SAVE_SETTING)	//(0x00020000-256) keep 256 bytes for saving setting for keep setting after reset

//==============================================
// Data Access Check
//==============================================
// The ILM_RECORD_START_ADDR and ILM_RECORD_SIZE are different for each IC
// Please refer to doc\DataAccessCheck for more detail
#define ILM_RECORD_START_ADDR (0x19000)  // This value should be larger than text size, Ex: 0x19000 > text(80596=0x13AD4)
#define ILM_RECORD_SIZE       (16944)    // 0x19000~0x1D230 (should not be larger than unused ILM size)


#define MMAP_S_2D_CR_OFFSET_F1			     (0x0001D230)								/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_OFFSET_F2			     (0x0001DDA0)								/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_OFFSET_F3			     (0x0001E910)								/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_CR_OFFSET_F4			     (0x0001F480)								/// 2880 Bytes + Button 48 Bytes
//================================================================== MMAP_S_2D_CR_NF_Header
//
//       byte    0      1      2      3      4      5      6      7      8      9      10      11      12      13      14      15
//       shift-bit  <---reserved----> shift-bit <---reserved----> shift-bit <---reserved---->   shift-bit  <---reserved---->
//           F1                          F2                          F3                            F4
//
#define MMAP_ILM_START_ADDR 				(0x00000)
#define MMAP_S_2D_CR_NF_HEADER_ILM       	(0x18D80)									/// 16 Bytes
#define MMAP_NF_HEADER_SUBSIZE           	(0x4)										/// 4 Bytes

#define MMAP_S_2D_CR_NF1_ILM             	(MMAP_S_2D_CR_NF_HEADER_ILM+NF_HEADER_SZ)		/// 2880 Bytes + Button 48 Bytes, 0x1D230
#define MMAP_S_2D_CR_NF2_ILM            	(MMAP_S_2D_CR_NF1_ILM + NF_1FREQ_SZ)			/// 2880 Bytes + Button 48 Bytes, 0x1DDA0
#define MMAP_S_2D_CR_NF3_ILM             	(MMAP_S_2D_CR_NF2_ILM + NF_1FREQ_SZ)			/// 2880 Bytes + Button 48 Bytes, 0x1E910
#define MMAP_S_2D_CR_NF4_ILM             	(MMAP_S_2D_CR_NF3_ILM + NF_1FREQ_SZ)			/// 2880 Bytes + Button 48 Bytes, 0x1F480

#define MMAP_S_END_OF_ILM_DATA 				(MMAP_S_2D_CR_OFFSET_F1)
//==================================================================
/*
**	DLM
*/
#define MMAP_DLM_START_ADDR            			(0x00080000)
#define MMAP_FW_REGISTER               			(0x00080000)				// FWconfig = 2048 bytes
#define MMAP_FW_REGISTER_TP_REG 	   			(0x00080700)	 			// for EMI Tuning
#define MMAP_FW_REGISTER_DP_REG 	   			(0x00080740)	 			// for EMI Tuning
//==================================================================
// Initial DLM: move to the last of DLM
#define MMAP_ICM_CTRLRAM_TMP           			(0x00080800)				// ICM Ctrlram+MP	, Size 24KB
#define MMAP_ICS_DIFF_CTRLRAM_0        			(0x00086800)				// ICS Diff Ctrlram 0, Size 3KB
#define MMAP_ICS_DIFF_CTRLRAM_1        			(0x00087400)				// ICS Diff Ctrlram	1, Size 3KB
//==================================================================
// BLD Temp NF: move to the last of ILM
#define MMAP_S_2D_CR_NF_HEADER 					(0x00088000)    			/// 16 Bytes
#define MMAP_S_2D_CR_NF1 						(0x00088010)        		/// 4320 Bytes + Button 72 Bytes
#define MMAP_S_2D_CR_NF2 						(0x00089138)        		/// 4320 Bytes + Button 72 Bytes
#define MMAP_S_2D_CR_NF3 						(0x0008A260)        		/// 4320 Bytes + Button 72 Bytes
#define MMAP_S_2D_CR_NF4 						(0x0008B388)        		/// 4320 Bytes + Button 72 Bytes
//==================================================================
#define MMAP_S_2D_ADC_SCANRAW          			(0x00080800)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_RAWRSS               			(0x00081370)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_RAWIIR               			(0x00081EE0)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_BASELINE             			(0x00082A50)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_S_2D_DIFFPAST             			(0x000835C0)				/// 5280 Bytes + Button 88 Bytes
#define MMAP_S_2D_DIFF                 			(0x00084AB8)				/// 5280 Bytes + Button 88 Bytes

#define MMAP_TMP_NF_TABLE              			(0x00085FB0)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_TMP_OFFSET_TABLE          			(0x00086B20)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_AREA                      			(0x00087690)				/// Max Size = 1680 Bytes (120 zones) 60*28
#define MMAP_ZONE                      			(0x000881D0)				/// 5760 Bytes
#define MMAP_ALU_CMD_BASE              			(0x00089850)				/// ALU command section, Max Size = 40 Bytes
#define MMAP_RESERVE0_BUF              			(0x00089880)                /// 1076 Bytes

#define MMAP_SELF_TEST_DEBUG           			(0x00089CB4)				/// Debug info for self test: 68 bytes + 4(reserved)
#define MMAP_SELF_TEST_COLSCAN         			(0x00089CF8)				/// 22 (row)*3(col)*2(L/R)* 2 (short) = 1440 bytes for scan row  + Button 24 Bytes
#define MMAP_SELF_TEST_COLRAW          			(0x0008A2B0)				/// 22 (row)*3(col)*2(L/R)* 2 (short) = 960 bytes for judgement + Button 16 Bytes

#define MMAP_GMD_SCANRAW               			(0x0008A680)				/// 480 Bytes + Button 8 Bytes
#define MMAP_GMD_RAWIIR0               			(0x0008A868)				/// 480 Bytes + Button 8 Bytes
#define MMAP_GMD_RAWIIR1               			(0x0008AA50)				/// 480 Bytes + Button 8 Bytes
#define MMAP_GMD_BASELINE              			(0x0008AC38)				/// 480 Bytes + Button 8 Bytes
#define MMAP_GMD_BASELINE1             			(0x0008AE20)				/// 480 Bytes + Button 8 Bytes
#define MMAP_GMD_DIFF                  			(0x0008B008)				/// 480 Bytes + Button 8 Bytes
#define MMAP_CTRLRAM_OFFSET_MAP 				(0x0008B1F0)				/// 2880 Bytes
#define MMAP_RESERVE1_BUF              			(0x0008BD30)                /// 536 Bytes

#define MMAP_VNOISE_2D_OUTPUT          			(0x0008BF48)				/// 964 bytes
#define MMAP_VNOISE_1D_OUTPUT          			(0x0008C30C)				/// 960 bytes

#define MMAP_MP_ND_DATA                			(0x0008C74C)				/// 16 bytes
#define MMAP_CPINFO                    			(0x0008C75C)				/// 32 bytes for CP info read by FW
#define MMAP_DEBUG_SDLOC_000           			(0x0008C77C)				/// 16 bytes
#define MMAP_DEBUG_SDLOC_001           			(0x0008C78C)				/// 16 bytes
#define MMAP_DEBUG_SDLOC_010           			(0x0008C79C)				/// 16 bytes
#define MMAP_DEBUG_SDLOC_011           			(0x0008C7AC)				/// 16 bytes
#define MMAP_VNOISE_CTRLRAM            			(0x0008C800)				/// 280 bytes
#define MMAP_VNOISE_PU_TBL             			(0x0008C918)				/// 512 bytes
#define MMAP_VNOISE_VN_TBL             			(0x0008CB18)				/// 4036 bytes
#define MMAP_VNOISE_STAIR_TBL          			(0x0008DADC)				/// 450 bytes
#define MMAP_VNOISE_1DCOL_GAING_TBL    			(0x0008DC9E)				/// 450 bytes

#define MMAP_SPI_DMA_VEC               			(0x0008DE60)				/// 1024 bytes
#if (defined(PC_SIMULATOR) && ( PC_SIMULATOR) )
#define MMAP_HEADER_INFO               			P_TYPE(UINT16, 0x0008C000)	/// Header Info Setting, Size 256 Bytes *2
#else
#define MMAP_HEADER_INFO               			(0x0008E310)				/// Header Info Setting, Size 256 Bytes
#endif
#define MMAP_HEADER_ILM_CRC_OFFSET  			(0x18)
#define MMAP_HEADER_DLM_CRC_OFFSET  			(0x1C)
//==================================================================
// .data
// .bss
// .stack
//==================================================================
#define MMAP_EVENT_BUF                 			(0x00094000)				/// One page event buffer, Max Size = 256 Bytes
#define MMAP_HW_INFO_REGISTER 					(MMAP_EVENT_BUF + 0x78)    	/// General Info.
#define MMAP_DNS_DIAGNOSTIC_BUF        			(MMAP_EVENT_BUF + EN_PUB_EVENT_MAP_DNS_DIAGNOSTIC)

#define MMAP_COM_BUFFER            				(0x000941CC)				/// 8784 Bytes DEBUG_BUFFER
#define MMAP_EVENT_BUF_RESERVED        			(0x0009641C)				/// Event buffer reserved
#define MMAP_PARA_COM_BUFFER		   			(0x0009651C)				/// 188 bytes
#define MMAP_S_2D_RESETCOUNTER         			(0x000965D8)				/// 5280 Bytes + Button 88 Bytes
#define MMAP_S_1D_CR_OFFSET_F1         			(0x00097AD0)				/// Unuse 480 Bytes + Button 8 Bytes
#define MMAP_S_2D_BUCKET         	   			(0x00097CB8)				/// 2880 Bytes + Button 48 Bytes
#define MMAP_ENCODE_BUF  			   			(0x00098828)  				/// 2400 Bytes, 60(CHY) * 10(OVLAP) * 2(L/R) * 2
#define MMAP_MASKTEMP_BUF  			   			(0x00099188)  				/// 1200 Bytes
#define MMAP_FW_FLOW_DEBUG_INFO        			(0x00099638)				/// FW RESET Info, Max Size = 8 Bytes
#define MMAP_RECORED_MAX						(0x00099640)  				/// 4 Bytes
#define MMAP_RESERVE2_BUF  			   			(0x00099644)  				/// 3168 Bytes

#define MMAP_VN_PU_BL 				   			(0x0009A2A0)    			/// (60*2+1) PU_MAX*4(word)*4(F1~F4) = 1952 bytes
#define MMAP_VN_PU_RAW 				   			(0x0009AA40)				/// (60*2+1) PU_MAX*4(word) = 488 bytes
#define MMAP_RW_CROFFSET_KEY        			(0x0009AC28)    			/// 2 Bytes
#define MMAP_RW_CROFFSET_CHECKSUM   			(0x0009AC2A)	 			/// 2 Bytes
#define MMAP_FFM2CPU_HOST 						(0x0009AC2C)         		/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_FFM2CPU                   			(0x0009AC3C)				/// FFM2CPU Section, Max Size = 16 Bytes
#define MMAP_IC_RST_SOURCE_INFO       			(0x0009AC4C)				/// FW Debug Variable, Max Size = 8 Byte
#define MMAP_INIT_FAIL_CNT 	  					(0x0009AC54)    			/// 1 Bytes
#define MMAP_INIT_FAIL_CNT_BAR 	  				(0x0009AC55)    			/// 1 Bytes
#define MMAP_RUNTIME_FAIL_CNT 		  			(0x0009AC56)    			/// 1 Bytes
#define MMAP_RUNTIME_FAIL_CNT_BAR 		  		(0x0009AC57)    			/// 1 Bytes
#define MMAP_WDT_FAIL_TEMP 		  				(0x0009AC58)    			/// 2 Bytes
#define MMAP_ASIL_RESERVED 	   		  			(0x0009AC5A)    			/// 61 Bytes
#define MMAP_EMS_BACKUP 	   					(0x0009AC97)    			/// 1 Bytes
#define MMAP_POWER_ON_KEY				        (0x0009AC98)                /// 4 Bytes
#define MMAP_RESET_COUNTER            			(0x0009AC9C)				// 4 bytes
#define MMAP_HISTORY_EVENT0           			(0x0009ACA0)				// 64 bytes for History Event, Max Size = 64 Bytes
#define MMAP_HISTORY_EVENT1           			(0x0009ACE0)				// 64 bytes for History Event, Max Size = 64 Bytes

#define MMAP_SPI_DMA_BUF               			(0x0009AD20)				/// 128 Bytes, 16(protocol)+112(buffer) bytes
#define MMAP_IN_BAND_ND_SCANRAW        			(0x0009ADA0)				/// 2880 Bytes + Button 96 Bytes
#define MMAP_IN_BAND_ND_RAWDATA        			(0x0009B940)				/// 1920 Bytes + Button 64 Bytes
#define MMAP_IN_BAND_ND_RAWRSS         			(0x0009C100)				/// 1920 Bytes + Button 64 Bytes
#define MMAP_CR_ND_FIFO                			(0x0009C8C0)				/// 5760 Bytes + Button 192 Bytes
#define MMAP_SELF_TEST_OPEN_2D         			(MMAP_CR_ND_FIFO)			/// 2928 Bytes
#define MMAP_SELF_TEST_SHORT_2D        			(MMAP_SELF_TEST_OPEN_2D+2928)	/// 2928 Bytes
#define MMAP_NKL_DBE_OUTBPUT                    (MMAP_CR_ND_FIFO)
#define MMAP_END_ADDR				   			(0x0009E000)

#define MMAP_CTRLRAM                   			(0x0009E000)				// Ctrlram	, Size 14KB
#define MMAP_MP_DYNAMIC_CTRLRAM        			(0x000A1800)				// MP Ctrlram	, Size 10KB
//===========================================
// V Noise
//===========================================
#define MMAP_VN_2D_OUTPUT_ICM 					(MMAP_VNOISE_2D_OUTPUT)   	/// 128 bytes
#define MMAP_VN_2D_OUTPUT_ICS 					(MMAP_VNOISE_2D_OUTPUT)   	/// 128 bytes
/*
**	Flashmap
*/	
#define FLASHMAP_HEADER_INFO 					(0x00000)					/// Header Info Setting @Flash 0KB, Size 256 Bytes
#define FLASHMAP_AUTOBUILD_SVN 					(0x00024)    				/// Offset 36-Byte

#define FLASHMAP_FW_REGISTER           			(0x22000)
#define FLASHMAP_FW_REGISTER_TP_REG    			(0x22700)    				/// EMI Tool Register Setting
#define FLASHMAP_FW_REGISTER_DP_REG    			(0x22740)    				/// EMI Tool Register Setting
#define FLASHMAP_CTRLRAM               			(0x22800)
#define FLASHMAP_MP_CTRLRAM            			(0x26000)
#define FLASHMAP_DIFF_DLM 			   			(0x28800)
#define FLASHMAP_NF_TABLE              			(0x005DC)
#define FLASHMAP_VNCTRLRAM             			(0x2E800)					/// 160+384+2560
#define FLASHMAP_ENDFLAG               			(0x31FFC)					/// End flag @Flash, size 4Bytes

//------------------------------------
//------CNC Stair Table--------
//------------------------------------
#define DLM_TEMP_CNC_STAIR_TABLE 				P_TYPE(UINT16, MMAP_ZONE)

#define FWCONFIG_TOTAL_SIZE            (2048U)
#define CTRLRAM_SZ                     (14*1024)
#define MP_CTRLRAM_SZ                  (10*1024)
#define VN_CTRLRAM_SZ                  (5728)	//160 + 384 stair table + VN table 2560
#define NF_HEADER_SZ                   (16)
#define NF_1FREQ_SZ                    ((60*24 + 24)*3)//(12*122*3)
#define NF_REPLACE_RESERVED 		   (848)
#define MAX_2D_DOT_NUM                 (4392)	//4320(AA) + 72(Button)
#define MAX_SPI_DMA_VEC_SZ             (1200)	//5bytes * 240 vector
#define DIFF_DLM_SZ 				   (2*3*1024) // 3KB per Die
///< Ended by NT51923

#endif  /* HAL_MMAP_H_ END */
