// 51920 1.3.1
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

// 51923 1.4.1
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

// 51926 2.0.0
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

// 51927 & 51928 1.4.1
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

// 51929 & 51932 2.0.0
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

// 51930 2.0.0
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

// 51931 1.3.0
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

// 51950 & 51951 2.0.0
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