/*!
 *    \file        ap_fwconfig.c
 *    \brief        Configurations for FW.
 *
 *    \note
 *    This is note line.
 *
 *    \copyright    Novatek Microelectronics Corp. 2015. All rights reserved.
 */

/*-----------------------------------------------------------------------------*/
/* Including Files                                                             */
/*-----------------------------------------------------------------------------*/
#include "ap\ap_fwconfig.h"
#include "ap\ap_custom.h"

/*-----------------------------------------------------------------------------*/
/* Local Constant Definitions                                                  */
/*-----------------------------------------------------------------------------*/
//=======================FW Version============================================//
#define 	USER_FW_VERSION 							(0x01)
#define 	USER_FW_SUB_VERSION 						(0x00)

//=======================General Para==========================================//
#define		USER_REPORT_TOUCH_NUM						(10)
#define 	USER_IRQ_TYPE 								(ATTN_EDGE_RISING)

#define 	USER_I2C_DEVICE_ADDRESS 					(0x01)

//=======================Switch on/off=========================================//
#define 	USER_SWITCH_FREQ_HOPPING 					(ENABLE)
#define 	USER_SWITCH_DOZE_MODE 						(DISABLE)
#define     USER_SWITCH_RAW_IIR                        	(ENABLE)
#define     USER_SWITCH_COMMON_NOISE_CANCEL            	(ENABLE)
#define 	USER_SWITCH_PALM_DETECT 					(ENABLE)
#define 	USER_SWITCH_WATER_DETECT 					(DISABLE)

#define 	USER_SWITCH_P2P_CALIBRATION 				(DISABLE)
#define	 	USER_SWITCH_BOUNDARY 						(ENABLE)
#define 	USER_SWITCH_GLOVE 							(ENABLE)

#define 	USER_SWITCH_RC_CHECK 						(ENABLE)
#define 	USER_SWITCH_GREEN_MODE_RC_CHECK 			(DISABLE)

#define 	USER_SWITCH_ENFORCE_TWO_FINGER_SEPARATION 	(ENABLE)

#define     USER_SWITCH_PF_VECTOR_COMPENSATION          (ENABLE)
#define     USER_SWITCH_PF_BOUNDARY_COMPENSATION       	(ENABLE)

#define     USER_SWITCH_PF_POINT_IIR                   	(ENABLE)
#define     USER_SWITCH_PF_JITTER                      	(ENABLE)
#define     USER_SWITCH_PF_JITTER_COMPENSATION          (ENABLE)
#define     USER_SWITCH_PF_DYNAMIC_FIR                 	(ENABLE)
#define     USER_SWITCH_PF_REPEATABILITY               	(ENABLE)

#define     USER_SWITCH_RLE_REVERSE_X                  	(DISABLE)
#define     USER_SWITCH_RLE_REVERSE_Y                  	(DISABLE)
#define     USER_SWITCH_RLE_XY_SWAP                    	(DISABLE)
#define     USER_SWITCH_RLE_PANEL_SCALE                	(ENABLE)

#define     USER_SWITCH_STUCK_SIGNAL_DETECT				(ENABLE)

//=======================2D Threshold==========================================//
#define     USER_S_2D_TP_TH                            	(100)	//based on big area floating touch & sensitivity
#define     USER_HF_ENTER_TP_TH_OFFSET         			(80)
#define     USER_HF_ENTER_TP_TH                			((UINT16)USER_S_2D_TP_TH + (UINT16)USER_HF_ENTER_TP_TH_OFFSET)	//based on 4mm floating touch (Click)
#define 	USER_HOPPING_TP_TH_HOPPING 					((UINT16)USER_S_2D_TP_TH + (UINT16)20)

//=======================Raw data IIR==========================================//
#define     USER_S_2D_IIR_WEIGHT                       	(IIR_FW_DISABLE)		//IIR_Weight_7_8__1_8, IIR_Weight_5_8__3_8, IIR_Weight_3_8__5_8,
																				//IIR_Weight_1_8__7_8, IIR_Weight_3_4__1_4, IIR_Weight_1_4__3_4,
																				//IIR_Weight_1_2__1_2, IIR_FW_DISABLE(FW Function)
#define     USER_S_2D_IIR_WEIGHT_HOPPING               	(IIR_Weight_5_8__3_8)	//IIR_Weight_7_8__1_8, IIR_Weight_5_8__3_8, IIR_Weight

//=======================Baseline IP===========================================//
/////////////////////////////////////////////////////////////////////////////////////////
//	CurrWeight/WeightSum = n/128 is equal to BL_UPDATE_TH (120/n) and BL_OFFSET (1).   //
//	EX.  																			   //
//	CurrWeight/WeightSum = 1/128 is equal to BL_UPDATE_TH (120) and BL_OFFSET (1).	   //
//	CurrWeight/WeightSum = 2/128 is equal to BL_UPDATE_TH (60) and BL_OFFSET (1).	   //
//	CurrWeight/WeightSum = 3/128 is equal to BL_UPDATE_TH (40) and BL_OFFSET (1).	   //
//	CurrWeight/WeightSum = 4/128 is equal to BL_UPDATE_TH (30) and BL_OFFSET (1).	   //
/////////////////////////////////////////////////////////////////////////////////////////
#define     USER_BL_IIR_WEIGHT_SUM                     	(7)	//power of 2  => 128
#define     USER_BL_IIR_CURR_WEIGHT                    	(1)
#define     USER_BL_IIR_CURR_WEIGHT_GLOVE              	(3)
#define     USER_BL_IIR_CURR_WEIGHT_2D_BL_UPDATE       	(2)
#define     USER_FW_BL_FAST_TRACKING_CNT_TH            	(10)
#define     USER_BL_IIR_CURR_WEIGHT_FAST               	(((UINT16)1 << (UINT8)USER_BL_IIR_WEIGHT_SUM)/((UINT16)8))

#define     USER_S_2D_BLRN                             	(20)
#define     USER_S_2D_NTC                              	(10)

#define     USER_RC_CHECK_LEVEL_CNT                    	(0)

//=======================Frequency hopping=====================================//
#define 	USER_HOPPING_CARRIER_NOISE_TH 				(500)

//=======================COMMON Noise Canceling================================//
#define     USER_COMMON_NOISE_HISTOGRAM_TH        	    (52)
#define     USER_COMMON_NOISE_HISTOGRAM_BIN_SCALE      	(13)	//scale scope
#define     USER_COMMON_NOISE_AVG_VALID_POINT_TH       	(0)

//=======================Palm Detect===========================================//
#define     USER_PALM_NORMSUM_THD_LEVEL1               	(10)
#define     USER_PALM_NORMSUM_THD_LEVEL1_IN_PALM       	(10)
#define     USER_PALM_KEEP_CNT                         	(15)
#define     USER_PALM_MODE                             	(PALM_REJECT_ALL_TILL_NO_FINGER)
#define     USER_PALM_JUDGE_TYPE                        (PALM_NORDIFF_MODE)
#define     USER_PALM_TARGET_PHI               			(33) 	// Target of elliptic major
//=======================Elliptic==============================================//
#define     USER_SWITCH_ELLIPTIC_GRAPH_CALCULATE       	(DISABLE)
//=======================Zone==================================================//
//normal
#define     USER_ZONE_AREA_TH_0                        	(15)
#define     USER_ZONE_AREA_TH_1                        	(35)
#define     USER_ZONE_AREA_TH_2                        	(50)
#define     USER_ZONE_AREA_TH_3                        	(70)
#define     USER_ZONE_AREA_TH_4                        	(80)
#define     USER_ZONE_AREA_TH_5                        	(0)
#define     USER_ZONE_AREA_TH_6                        	(0)
#define     USER_ZONE_AREA_TH_7                        	(0)

#define     USER_ZONE_MERGE_TH_0                       	(110)
#define     USER_ZONE_MERGE_TH_1                       	(105)
#define     USER_ZONE_MERGE_TH_2                       	(95)
#define     USER_ZONE_MERGE_TH_3                       	(80)
#define     USER_ZONE_MERGE_TH_4                       	(70)
#define     USER_ZONE_MERGE_TH_5                       	(60)
#define     USER_ZONE_MERGE_TH_6                       	(60)
#define     USER_ZONE_MERGE_TH_7                       	(60)

//=======================Zone Other============================================//
//palm
#define     USER_ZONE_AREA_TH_PALM_0                   	(15)
#define     USER_ZONE_AREA_TH_PALM_1                   	(20)
#define     USER_ZONE_AREA_TH_PALM_2                   	(30)
#define     USER_ZONE_AREA_TH_PALM_3                   	(40)
#define     USER_ZONE_AREA_TH_PALM_4                   	(0)
#define     USER_ZONE_AREA_TH_PALM_5                   	(0)
#define     USER_ZONE_AREA_TH_PALM_6                   	(0)
#define     USER_ZONE_AREA_TH_PALM_7                   	(0)

#define     USER_ZONE_MERGE_TH_PALM_0                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_1                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_2                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_3                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_4                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_5                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_6                  	(10)
#define     USER_ZONE_MERGE_TH_PALM_7                  	(10)

//hopping
#define     USER_ZONE_AREA_TH_HOPPING_0                	(25)
#define     USER_ZONE_AREA_TH_HOPPING_1                	(70)
#define     USER_ZONE_AREA_TH_HOPPING_2                	(0)
#define     USER_ZONE_AREA_TH_HOPPING_3                	(0)
#define     USER_ZONE_AREA_TH_HOPPING_4                	(0)
#define     USER_ZONE_AREA_TH_HOPPING_5                	(0)
#define     USER_ZONE_AREA_TH_HOPPING_6                	(0)
#define     USER_ZONE_AREA_TH_HOPPING_7                	(0)

#define     USER_ZONE_MERGE_TH_HOPPING_0               	(55)
#define     USER_ZONE_MERGE_TH_HOPPING_1               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_2               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_3               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_4               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_5               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_6               	(70)
#define     USER_ZONE_MERGE_TH_HOPPING_7               	(70)

//=======================Glove=================================================//
#define     USER_GLOVE_ENTER_DEBOUNCE_0                 (3)
#define     USER_GLOVE_ENTER_DEBOUNCE_1                 (3)
#define     USER_GLOVE_ENTER_DEBOUNCE_2                 (3)
#define     USER_GLOVE_ENTER_DEBOUNCE_3                 (3)
#define     USER_GLOVE_ENTER_DEBOUNCE_4                 (3)
#define     USER_GLOVE_ENTER_DEBOUNCE_5                 (5)
#define     USER_GLOVE_ENTER_DEBOUNCE_6                 (5)
#define     USER_GLOVE_ENTER_DEBOUNCE_7                 (5)
#define     USER_GLOVE_ENTER_DEBOUNCE_8                 (5)
#define     USER_GLOVE_ENTER_DEBOUNCE_9                 (5)

#define     USER_GLOVE_FIND_PEAK_TH                     (40)
#define     USER_GLOVE_ENTER_TP_TH                      (50)

#define     USER_GLOVE_AREA_MIN_TH                      (2)

//=======================WakeupGesture=========================================//
#define     USER_WKG_SWITCH_GESTURE_ITEM               	(0xFFF8)	//bit15~bit0: C,W,V,DbClick,Z,M,O,e,S,slide up,slide down,slide left,slide right,reserved,reserved,reserved,
#define     USER_WKG_SWITCH_GESTURE_ITEM_EXTEND        	(0)

#define     USER_WKG_TP_TH                             	(USER_S_2D_TP_TH)
#define     USER_WKG_ENTER_TP_TH                       	(USER_WKG_TP_TH)
#define     USER_WKG_TRACK_ENTER_DEBOUNCE              	(USER_TRACK_ENTER_DEBOUNCE_0)	//Setting of every ID.

#define     USER_WKG_AREA_TH                           	(50)
#define     USER_WKG_FRAME_LIMIT                       	(200)

#define     USER_WKG_VECTOR_CHECK_LENGTH               	(40)
#define     USER_WKG_VECTOR_LENGTH_X                   	(15)
#define     USER_WKG_VECTOR_LENGTH_Y                   	(15)

#define     USER_WKG_SLIDE_HORIZONTAL_X_LENGTH         	(360)
#define     USER_WKG_SLIDE_HORIZONTAL_Y_LENGTH         	(300)
#define     USER_WKG_SLIDE_VERTICAL_X_LENGTH           	(500)
#define     USER_WKG_SLIDE_VERTICAL_Y_LENGTH           	(760)
#define     USER_WKG_SLIDE_XMIN_STARTRANGE             	(0)
#define     USER_WKG_SLIDE_XMAX_STARTRANGE             	(USER_LCM_RESO_X-1)
#define     USER_WKG_SLIDE_YMIN_STARTRANGE             	(0)
#define     USER_WKG_SLIDE_YMAX_STARTRANGE             	(USER_LCM_RESO_Y-1)

#define     USER_WKG_ALPHABET_TOTAL_LENGTH_TH          	(500)
#define     USER_WKG_ALPHABET_XMIN_STARTRANGE          	(0)
#define     USER_WKG_ALPHABET_XMAX_STARTRANGE          	(USER_LCM_RESO_X-1)
#define     USER_WKG_ALPHABET_YMIN_STARTRANGE          	(0)
#define     USER_WKG_ALPHABET_YMAX_STARTRANGE          	(USER_LCM_RESO_Y-1)
#define     USER_WKG_ALPHABET_X_RATIO_C_FIRST2END      	(50)
#define     USER_WKG_ALPHABET_Y_RATIO_C_FIRST2END      	(20)
#define     USER_WKG_ALPHABET_X_RATIO_C_Y_VERSE        	(2)
#define     USER_WKG_ALPHABET_X_RATIO_e_FIRST2END      	(30)
#define     USER_WKG_ALPHABET_Y_RATIO_e_FIRST2END      	(30)
#define     USER_WKG_ALPHABET_Y_RATIO_V_FIRST2END      	(50)
#define     USER_WKG_ALPHABET_Y_RATIO_V_X_VERSE        	(2)

#define     USER_WKG_DBCLICK_DISTANCE_RANGE            	(100)
#define     USER_WKG_DBCLICK_FINGERON_FRAME_LIMIT      	(70)
#define     USER_WKG_DBCLICK_FINGEROFF_FRAME_LIMIT     	(30)
#define     USER_WKG_DBCLICK_XMIN_STARTRANGE           	(0)
#define     USER_WKG_DBCLICK_XMAX_STARTRANGE           	(USER_LCM_RESO_X-1)
#define     USER_WKG_DBCLICK_YMIN_STARTRANGE           	(0)
#define     USER_WKG_DBCLICK_YMAX_STARTRANGE           	(USER_LCM_RESO_Y-1)

//=======================P2P Calibration=======================================//
#define 	USER_P2P_RESOLUTION_X 						(USER_TP_RESO_X)
#define 	USER_P2P_EDGE_RESOLUTION_X 					((UINT16)26)    //~4.5mm x 5.807 pixel/mm
#define 	USER_P2P_CENTER_POINT_X 					((UINT16)145)    //~(330.624 -2*4.5mm) / 2mm
#define 	USER_P2P_EDGE_POINT_X 						((UINT16)1)
#define 	USER_P2P_TOTAL_POINT_X 						(USER_P2P_CENTER_POINT_X + (USER_P2P_EDGE_POINT_X * (UINT16)2))

#define 	USER_P2P_RESOLUTION_Y 						(USER_TP_RESO_Y)
#define 	USER_P2P_EDGE_RESOLUTION_Y 					((UINT16)26)    //~4.5mm x 5.807 pixel/mm
#define 	USER_P2P_CENTER_POINT_Y 					((UINT16)25)    //~(206.64 - 2*4.5mm) / 2mm
#define 	USER_P2P_EDGE_POINT_Y 						((UINT16)1)
#define 	USER_P2P_TOTAL_POINT_Y                      (USER_P2P_CENTER_POINT_Y + (USER_P2P_EDGE_POINT_Y * (UINT16)2))

#define USER_P2P_CALI_TABLE_X                                                                                                                                                                                                                                                                                                                                                                                  \
{   (UINT16)0,(UINT16)37, (UINT16)60, (UINT16)86, (UINT16)113, (UINT16)142, (UINT16)170, (UINT16)197, (UINT16)225, (UINT16)252, (UINT16)281, (UINT16)308, (UINT16)335, (UINT16)363, (UINT16)392, (UINT16)418, (UINT16)446, (UINT16)472, (UINT16)499, (UINT16)525, (UINT16)550, (UINT16)577, (UINT16)604, (UINT16)629, (UINT16)655, (UINT16)682, (UINT16)709, (UINT16)734, (UINT16)760, (UINT16)788, (UINT16)813, (UINT16)839,\
    (UINT16)865, (UINT16)892, (UINT16)918, (UINT16)944, (UINT16)971, (UINT16)997, (UINT16)1023, (UINT16)1049, (UINT16)1076, (UINT16)1102, (UINT16)1128, (UINT16)1154, (UINT16)1181, (UINT16)1206, (UINT16)1233, (UINT16)1259, (UINT16)1293, (UINT16)1313, (UINT16)1339, (UINT16)1365, (UINT16)1391, (UINT16)1418, (UINT16)1443, (UINT16)1471, (UINT16)1497, (UINT16)1523, (UINT16)1549, (UINT16)1576,\
    (UINT16)1601, (UINT16)1627, (UINT16)1654, (UINT16)1681, (UINT16)1706, (UINT16)1732, (UINT16)1760, (UINT16)1786, (UINT16)1811, (UINT16)1838, (UINT16)1864, (UINT16)1890, (UINT16)1916, (UINT16)1943, (UINT16)1970, (UINT16)1995, (UINT16)2021, (UINT16)2049, (UINT16)2075, (UINT16)2101, (UINT16)2126, (UINT16)2153, (UINT16)2179, (UINT16)2206, (UINT16)2232, (UINT16)2259, (UINT16)2285, (UINT16)2310,\
    (UINT16)2338, (UINT16)2364, (UINT16)2388, (UINT16)2415, (UINT16)2443, (UINT16)2468, (UINT16)2495, (UINT16)2521, (UINT16)2547, (UINT16)2572, (UINT16)2594, (UINT16)2626, (UINT16)2653, (UINT16)2680, (UINT16)2705, (UINT16)2731, (UINT16)2758, (UINT16)2783, (UINT16)2810, (UINT16)2837, (UINT16)2863, (UINT16)2888, (UINT16)2915, (UINT16)2942, (UINT16)2968, (UINT16)2994, (UINT16)3021, (UINT16)3047,\
    (UINT16)3072, (UINT16)3098, (UINT16)3126, (UINT16)3151, (UINT16)3177, (UINT16)3204, (UINT16)3231, (UINT16)3256, (UINT16)3282, (UINT16)3309, (UINT16)3335, (UINT16)3360, (UINT16)3387, (UINT16)3414, (UINT16)3440, (UINT16)3467, (UINT16)3495, (UINT16)3522, (UINT16)3550, (UINT16)3578, (UINT16)3606, (UINT16)3634, (UINT16)3661, (UINT16)3689, (UINT16)3717, (UINT16)3744, (UINT16)3772, (UINT16)3799,\
    (UINT16)3825, (UINT16)3849, (UINT16)3887, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
}
#define USER_P2P_CALI_TABLE_Y                                                                                                                                                                                                                                                                                                                                                                                  \
{                                                                                                                                                                                                                                                                                                                                                                                                          \
    (UINT16)0, (UINT16)39, (UINT16)64, (UINT16)91, (UINT16)118, (UINT16)147, (UINT16)177, (UINT16)205, (UINT16)231, (UINT16)260, (UINT16)289, (UINT16)317, (UINT16)344, (UINT16)372, (UINT16)402, (UINT16)430, (UINT16)457, (UINT16)485, (UINT16)514, (UINT16)543, (UINT16)570, (UINT16)598, (UINT16)627, (UINT16)656, (UINT16)682, (UINT16)704, (UINT16)739, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
    (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0, (UINT16)0,\
}

//=======================Tracking==============================================//
#define     USER_TRACK_TAPPING_HIGHSPEED_CNT_TH        	(2)
#define     USER_TRACK_BREAK_DEBOUNCE                  	(1)

#define     USER_TRACK_TAPPING_RATIO_TH                	(32)
#define     USER_TRACK_TAPPING_SMALL_DIS_TH            	((((UINT32)USER_INTERPOLATION_STEP_X + (UINT32)USER_INTERPOLATION_STEP_Y) * (UINT32)3) / (UINT32)2)
#define     USER_TRACK_TAPPING_MIDDLE_DIS_TH           	(((UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH * (UINT32)7) / (UINT32)6)
#define     USER_TRACK_TAPPING_MAX_ALLOW_DIS_TH        	((UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH * (UINT32)5)
#define     USER_TRACK_TAPPING_HIGH_SPEED_DIS_TH       	((UINT32)USER_TRACK_TAPPING_MIDDLE_DIS_TH * (UINT32)2)

#define     USER_TRACK_TAPPING_HI_TH                   	(100)
#define     USER_TRACK_TAPPING_LOW_TH                  	(50)

#define     USER_TRACK_ENTER_DEBOUNCE_0                	(1)
#define     USER_TRACK_ENTER_DEBOUNCE_1                	(1)
#define     USER_TRACK_ENTER_DEBOUNCE_2                	(3)
#define     USER_TRACK_ENTER_DEBOUNCE_3                	(3)
#define     USER_TRACK_ENTER_DEBOUNCE_4                	(3)
#define     USER_TRACK_ENTER_DEBOUNCE_5                	(5)
#define     USER_TRACK_ENTER_DEBOUNCE_6                	(5)
#define     USER_TRACK_ENTER_DEBOUNCE_7                	(5)
#define     USER_TRACK_ENTER_DEBOUNCE_8                	(5)
#define     USER_TRACK_ENTER_DEBOUNCE_9                	(5)

#define     USER_TRACK_SWITCH_UNCERTAIN                	(ENABLE)		//for very high speed drawing with tapping--> please turn off "Raw IIR" if you set this config as ENABLE

#define     USER_TRACK_TAPPING_COSINE                  	(75)			//ex. (con30)^2 = 0.75 --> setting should be 0.75*100 = 75
#define     USER_TRACK_UNCERTAIN_RANGE                 	(20)			//the range for "UNCERTAIN_DRAW"

#define     USER_TRACK_SMALL_DIS_TH_RATIO              	(50)			//if previous condition is uncertain, u32SmallDistanceTh = u32SmallDistanceTh* USER_TRACK_SMALL_DIS_TH_RATIO/100 ~ u32SmallDistanceTh

//=======================Tracking Other========================================//
#define     USER_TRACK_BREAK_DEBOUNCE_HOPPING          	(2)
#define     USER_TRACK_TAPPING_HOPPING_SMALL_DIS_TH    	(350)

//=======================VectorCompensation====================================//
#define     USER_VC_FACTOR_X                           	(20)
#define     USER_VC_FACTOR_Y                           	(20)
#define     USER_VC_BASE_X                             	(10)
#define     USER_VC_BASE_Y                             	(10)
#define     USER_VC_TH                                 	(20)		//for turn off VC --> (10000)

//=======================VectorCompensation Other==============================//
#define     USER_VC_FACTOR_X_Hopping                   	(20)
#define     USER_VC_FACTOR_Y_Hopping                   	(20)

//=======================Point Filter==========================================//
#define     USER_PF_BOUNDARY_RANGE_TH                  	(64)
#define     USER_PF_BOUNDARY_DIS_TH                    	(50)

#define     USER_PF_REPEAT_BREAK_CNT_TH                	(1000)
#define     USER_PF_REPEAT_DIS_TH                      	(15)
#define     USER_PF_REPEAT_WEIGHTING                   	(8)

#define     USER_PF_JITTER_HUGE_TH						((((UINT16)USER_INTERPOLATION_STEP_X + (UINT16)USER_INTERPOLATION_STEP_Y)*(UINT16)3)/(UINT16)2)		//0~255		//recommand setting: USER_PF_JITTER_HUGE_TH > USER_PF_JITTER_BIG_TH > USER_PF_JITTER_SMALL_TH
#define     USER_PF_JITTER_BIG_TH                       ((((UINT16)USER_INTERPOLATION_STEP_X + (UINT16)USER_INTERPOLATION_STEP_Y)/(UINT16)2)/(UINT16)4)		//0~255		//recommand setting: USER_PF_JITTER_HUGE_TH > USER_PF_JITTER_BIG_TH > USER_PF_JITTER_SMALL_TH
#define     USER_PF_JITTER_SMALL_TH                     (0)			                    //0~255		//recommand setting: USER_PF_JITTER_HUGE_TH > USER_PF_JITTER_BIG_TH > USER_PF_JITTER_SMALL_TH
#define 	USER_PF_JITTER_COMPENSATE_DIS				(((UINT16)USER_INTERPOLATION_STEP_X + (UINT16)USER_INTERPOLATION_STEP_Y)/(UINT16)2)		//1~65535	//The min setting value is 1 for preventing devided by 0
#define 	USER_PF_JITTER_COMPENSATE_DIS_MIN			(10)		//1~65535	//The min setting value is 1 for preventing devided by 0
#define     USER_PF_JITTER_PROTECT_CNT					(10)		//0~255		//if frame cnt < USER_PF_JITTER_PROTECT_CNT, USER_PF_JITTER_HUGE_TH is Jitter Threshold
#define 	USER_PF_JITTER_EDGE_PROTECT_RANGE			(10)		//0~255		//Jitter threshold @ Edge Condition

#define     USER_PF_FIR_LEVEL_MIN                       (15)		//10~160	//if setting 15, mean FIR using 1.5 level of input coord.
#define     USER_PF_FIR_LEVEL_MAX                       (160)		//10~160	//if setting 15, mean FIR using 1.5 level of input coord.
#define     USER_PF_FIR_DISTANCE_MIN                    (40)		//0~65535	//if setting 40, mean 4.0 pixel
#define     USER_PF_FIR_DISATNCE_MAX                    (180)		//0~65535	//if setting 40, mean 4.0 pixel

#define		USER_PF_IIR_STR_MIN 						(20)		//1~100		//if setting 25, the IIR formula is Output = 0.25*Input + 0.75*PreOutput
#define 	USER_PF_IIR_STR_MAX 						(75)		//1~100		//if setting 25, the IIR formula is Output = 0.25*Input + 0.75*PreOutput
#define 	USER_PF_IIR_DIS_MIN 						(40)		//0~65535	//if setting 40, mean 4.0 pixel
#define 	USER_PF_IIR_DIS_MAX 						(300)		//0~65535	//if setting 40, mean 4.0 pixel

//=======================Point Filter Other ===================================//
#define     USER_PF_FIR_LEVEL_MIN_HOPPING               (20)
#define     USER_PF_FIR_LEVEL_MAX_HOPPING               (160)
#define     USER_PF_FIR_DISTANCE_MIN_HOPPING            (60)
#define     USER_PF_FIR_DISATNCE_MAX_HOPPING            (180)

#define		USER_PF_IIR_STR_MIN_HOPPING 				(20)
#define 	USER_PF_IIR_STR_MAX_HOPPING 				(50)
#define 	USER_PF_IIR_DIS_MIN_HOPPING 				(60)
#define 	USER_PF_IIR_DIS_MAX_HOPPING 				(300)

//=======================Green mode ===========================================//
#define 	USER_GMD_S1D_IIR_WEIGHT 					(15)    // Cur : Pre = USER_GMD_S1D_IIR_WEIGHT : (16 - USER_GMD_S1D_IIR_WEIGHT)
#define 	USER_GMD_ENTER_DOZE_FRAME_TH 				(120)   // enter doze when 1 round self test done
#define 	USER_GMD_DOZE_TP_TH 						(200)
#define 	USER_GMD_FDM_TP_TH 							(200)
#define 	USER_GMD_FDM_REK_TP_TH 						(100)
#define 	USER_GMD_FDM_REK_CNT_TH 					(10)

#define 	USER_GMD_DOZE_S1D_BL_UPDATE_FRAME_TH 		(50)
#define 	USER_GMD_DOZE_S2D_BL_UPDATE_FRAME_TH 		(100)
#define 	USER_GMD_DOZE_S1D_GLOVE_BL_UPDATE_FRAME_TH 	(100)    // Used when Glove mode is enabled
#define 	USER_GMD_DOZE_S2D_GLOVE_BL_UPDATE_FRAME_TH 	(2)      // Used when Glove mode is enabled
#define 	USER_GMD_FDM_S1D_BL_UPDATE_FRAME_TH 		(100)
#define 	USER_GMD_FDM_S2D_BL_UPDATE_FRAME_TH 		(300)

//=======================Boundary =============================================//
#define     USER_BD_GAUSSIAN_R_X0						(210)  //	(?/1000)
#define     USER_BD_GAUSSIAN_R_X1						(210)  //	(?/1000)
#define     USER_BD_GAUSSIAN_R_Y0						(210)  //	(?/1000)
#define     USER_BD_GAUSSIAN_R_Y1						(210)  //	(?/1000)

//========================== OSC Trim =========================================//
#define 	USER_OSC_TRIM_REK_F1_ABNORMAL_DIFF_TH 		(40)
#define 	USER_OSC_TRIM_REK_F2_ABNORMAL_DIFF_TH 		(35)
#define 	USER_OSC_TRIM_REK_F3_ABNORMAL_DIFF_TH 		(35)
#define 	USER_OSC_TRIM_REK_F4_ABNORMAL_DIFF_TH 		(30)
#define 	USER_OSC_TRIM_REK_AREA_TH					(75)   // (?/100)

//=======================Self Test parameters =================================//
#define 	USER_SELFTEST_JUDGE_EN 						(JUDGE_ENABLE)  // Judge self test result or not
#define 	USER_SELFTEST_OPEN_JUDGE_LOW 				(1000)          // OPEN P2P lower bound
#define 	USER_SELFTEST_OPEN_JUDGE_HIGH 				(13500)         // OPEN P2P upper bound
#define 	USER_SELFTEST_SHORT_JUDGE_LOW 				(8000)          // SHORT P2P lower bound
#define 	USER_SELFTEST_SHORT_JUDGE_HIGH 				(14100)         // SHORT P2P upper bound

//======================= TwoFingerSeparation ==================================//
#define     USER_TFS_EGA_COORD_DOWNSCALE                (1)		// coordinate value downscale factor. 1: >>1, 2: >>2
#define     USER_TFS_EGA_DIFF_DOWNSCALE                 (3)		// diff value downscale factor. 1: >>1, 2: >>2
#define     USER_TFS_EGA_MAJOR_SCALE                    (0)		// down scale factor. If == 0, no scale, else major = ( major * USER_TFS_EGA_MAJOR_SCALE ) >> 8
#define     USER_TFS_EGA_MINOR_SCALE                    (0)		// down scale factor. If == 0, no scale, else minor = ( minor * USER_TFS_EGA_MINOR_SCALE ) >> 8

//======================= EMS Solution =========================================//
// Stop Baseline tracking
#define     USER_SWITCH_ABN_STOP_BL_TRACK               (ENABLE)
#define     USER_SWITCH_DEBOUNCE_TH_STOP_BL_TRACK       (120)

// Stop Point tracking
#define     USER_SWITCH_ABN_STOP_POINT_TRACK 		    (ENABLE)
#define     USER_SWITCH_ABN_DIFF_TH_STOP_POINT_TRACK    (((UINT16)TARGET_NF_VALUE * (UINT16)12) / (UINT16)10) //(480) // Don't lower it as it may pose a risk of frame freezing
#define     USER_SWITCH_DEBOUNCE_TH_STOP_POINT_TRACK    (2)

// EMS flag
#define     USER_SWITCH_EMS_PATTERN_JUDGE               (ENABLE)
#define     USER_SWITCH_DEBOUNCE_TH_EMS_PATTERN         (120)
#define     USER_SWITCH_TRACK_ENTER_DEBOUNCE_EMS        (10)
#define     USER_SWITCH_ABN_DIFF_TH_EMS_PATTERN         (((UINT16)TARGET_NF_VALUE * (UINT16)12) / (UINT16)10) //(480)
#define     USER_SWITCH_ABN_DIFF_POS_NEG_CNT_RATIO      (5) // (?/100), shall <= 30 to avoid overflow (5% for pos & neg respectively, and 10% for neg only)

// Raw Check
#define USER_EMS_REK_RAWDATA_CHECK                      (ENABLE)

//======================= Customized Function ==================================//
#define     USER_CUSTOMIZED_FRAME_CNT_REPORT            (ENABLE)		//Bit0 do not disable

#define     USER_CUSTOMIZED_FUNCTION_SWITCH             ((UINT8)USER_CUSTOMIZED_FRAME_CNT_REPORT << (UINT8)0)

/*-----------------------------------------------------------------------------*/
/* Local Types Declarations                                                    */
/*-----------------------------------------------------------------------------*/

/*-----------------------------------------------------------------------------*/
/* Local Function Prototype                                                    */
/*-----------------------------------------------------------------------------*/
#if ! PC_SIMULATOR
extern char bootld_sram_end;
#endif
/*-----------------------------------------------------------------------------*/
/* Local Global Variables                                                      */
/*-----------------------------------------------------------------------------*/
UINT32 gau32EndFlag ANDES_ATTR_SECT_ENDFLAG_ = 0x54564E00;    //"NVT"

UINT8 gau8InfoDummyOffset[0x7000] ANDES_ATTR_SECT_DUMMY_;
UINT8 gau8FWConfigReserved[FWCONFIG_SIZE - (INT32)(sizeof(ST_PUB_FW_CONFIG))] ANDES_ATTR_SECT_RESERVED_FWCONFIG_ = { 0 };
UINT8 gau8EMITuningReserved[EMI_REG_TUNING_SIZE] ANDES_ATTR_SECT_EMI_TUNING_;
UINT8 gau8EventBuf_Reserved[EVENT_BUF_RESERVED_SIZE] ANDES_ATTR_SECT_EVENTBUF_ = { 0 };
UINT8 gau8NF_Table_Reserved[NF_HEADER_SZ + 4 * MAX_2D_DOT_NUM] ANDES_ATTR_SECT_NFTABLE_ = { 0 };
UINT8 gau8CtrlRAM_Reserved[CTRLRAM_SZ] ANDES_ATTR_SECT_CTRLRAM_;
UINT8 gau8VnCtrlRAM_Reserved[VN_CTRLRAM_SZ] ANDES_ATTR_SECT_VNCTRLRAM_;
UINT8 gu8HeaderInfo[HEADER_SZ] ANDES_ATTR_SECT_HEADERCOPY_;
UINT8 gau8EventBuf_Temp[EVENT_BUF_SIZE] ANDES_ATTR_SECT_EVENTBUF_TEMP_;

ST_PUB_FW_CONFIG gstFwSettings ANDES_ATTR_SECT_FWCONFIG_ = {
    //===========================General Para============================//
    {
        (UINT8)USER_FW_VERSION,                                // 0x000 UINT8 u8FWVersion
        (UINT8)(~(UINT8)USER_FW_VERSION),                      // 0x001 UINT8 u8FWVersionBar
        (UINT8)USER_PANEL_VIEW_TOTAL_X,                        // 0x002 UINT8 u8AlgNumberX
        (UINT8)USER_FW_ALGO_Y,                                 // 0x003 UINT8 u8AlgNumberY
        (UINT16)USER_LCM_RESO_X,                               // 0x004 UINT16 u16LCMResolutionX
        (UINT16)USER_LCM_RESO_Y,                               // 0x006 UINT16 u16LCMResolutionY
        (UINT8)USER_COMMON_FW_FORMAT_VERSION,                  // 0x008 UINT8 u8CommonFwFormatVersion
        (UINT8)USER_REPORT_TOUCH_NUM,                          // 0x009 UINT8 u8ReportFingerNum
        (UINT8)0,                                              // 0x00A UINT8 u8ButtonNum
        (UINT8)USER_IRQ_TYPE,                                  // 0x00B UINT8 u8IRQ_Type
        (UINT8)USER_EVENT_BUFFER_FORMAT_CURRENT_VERSION,       // 0x00C UINT8 u8EventBufferFormatVersion
        (UINT8)USER_CUSTOMIZED_FUNCTION_SWITCH,                // 0x00D UINT8 u8CustomizedFunction
        (UINT8)0,                                              // 0x00E UINT8 u8CustomerTuningVersion
        (UINT8)USER_PANEL_VIEW_TOTAL_X,                        // 0x00F UINT8 u8HWNumberX
        (UINT8)USER_FW_ALGO_Y,                             	   // 0x010 UINT8 u8HWNumberY
        (UINT8)USER_FW_SUB_VERSION,                            // 0x011 UINT8 u8FWSubVersion
        (UINT16)USER_TP_RESO_X,                                // 0x012 UINT16 u16TPResolutionX
        (UINT16)USER_TP_RESO_Y,                                // 0x014 UINT16 u16TPResolutionY
        (UINT8)1,                                              // 0x016 UINT8 u8ReportByComBuf
        (UINT8)CASCADE_CHIP_NUM,                               // 0x017 UINT8 u8Chip_Num
        (UINT8)PANEL_BC_EN,                                    // 0x018 UINT8 u8BC_EN
        (UINT8)MAX_BUFFER_NUM,                                 // 0x019 UINT8 u8MaxBufferNum
        (UINT8)USER_COMMON_FW_MAJOR_VERSION,                   // 0x01A UINT8 u8CommonFwMajorVersion
        (UINT8)USER_COMMON_FW_MINOR_VERSION,                   // 0x01B UINT8 u8CommonFwMinorVersion
        (UINT8)USER_COMMON_FW_ADDITIONAL_VERSION,              // 0x01C UINT8 u8CommonFwAdditionalVersion
        (UINT8)0,                       					   // 0x01D UINT8 u8AutoBuildSvnVer1
        (UINT8)0,                       					   // 0x01E UINT8 u8AutoBuildSvnVer2
        (UINT8)0,                       					   // 0x01F UINT8 u8AutoBuildSvnVer3
        (UINT8)0,                       					   // 0x020 UINT8 u8AutoBuildSvnVer4
        (UINT8)1,                                              // 0x021 UINT8 u8CascadeEn
        (UINT16)USER_NOVATEK_PROJECT_ID,                       // 0x022 UINT16 u16NovaTekProjectID
        (UINT8)USER_S_1D_X_NUM * (UINT8)CASCADE_CHIP_NUM,      // 0x024 UINT8 u8X1DNum
        (UINT8)USER_S_1D_Y_NUM,                                // 0x025 UINT8 u8Y1DNum
        (UINT8)NT519XX_MAX_AFE_NUM_ALL,               		   // 0x026 UINT8 u8AFE_Num
        //===========================EventBuf Addr=============================//
#if !PC_SIMULATOR
        (UINT8)((MMAP_EVENT_BUF >>(UINT8)16) & (UINT32)0xFF),  // 0x027 UINT8 u8EventBufHbyte
        (UINT8)((MMAP_EVENT_BUF >>(UINT8)8) & (UINT32)0xFF),   // 0x028 UINT8 u8EventBufMbyte
#else
		(UINT8)0,
		(UINT8)0,
#endif
        //=====================================================================//
        //                        HW Information                               //
        //=====================================================================//
        (UINT8)0,                					  		   // 0x029 UINT8 Reserved
        (UINT8)USER_SWITCH_FREE_RUN_SCAN,                      // 0x02A UINT8 u8FreeRunMode
        (UINT8)0,                        			  	       // 0x02B UINT8 Reserved
        (UINT8)SYSTEM_DEFINE_MODE,                             // 0x02C UINT8 u8SyncType

        (UINT8)USER_SENSE_TERM_NUM,                            // 0x02D UINT8 u8SenseTermNum
        (UINT8)USER_TP_TERM_NUM_NORMAL, 					   // 0x02E UINT8 u8TPTermNumNormal
        (UINT8)USER_TP_TERM_NUM_SELF,                          // 0x02F UINT8 u8TPTermNumSelf
                                                               
        (UINT8)0,                                              // 0x030 UINT8 u8Reserved5
        (UINT8)USER_I2C_DEVICE_ADDRESS,                        // 0x031 UINT8 u8I2CDevAddr
        (UINT8)USER_INTERPOLATION_STEP_X,                      // 0x032 UINT8 u8InterpolationX
        (UINT8)USER_INTERPOLATION_STEP_Y,                      // 0x033 UINT8 u8InterpolationY
                                                               
        (UINT16)USER_S_2D_SENSOR_DOTS,                         // 0x034 UINT16 u16S2DSensorDots
        (UINT16)0,                                             // 0x036 UINT16 u16Reserved0

        (UINT8)MAX_ZONE_NUM,                              	   // 0x038 UINT8 u8MaxZoneNum
        (INT8)USER_INTERP_START_OFFSET_X,                      // 0x039 INT8  s8InterpStartOffsetX
        (INT8)USER_INTERP_START_OFFSET_Y,                      // 0x03A INT8  s8InterpStartOffsetY
        (UINT8)MAX_TOUCH_NUM,                             	   // 0x03B UINT8 u8MaxFingerNum

        (UINT32)USER_GIP_BEFORE_TABLE0_L,                      // 0x03C UINT32 u32GIPBeforeTable0_L
        (UINT32)USER_GIP_BEFORE_TABLE1_L,                      // 0x040 UINT32 u32GIPBeforeTable1_L
        (UINT32)USER_GIP_BEFORE_TABLE2_L,                      // 0x044 UINR32 u32GIPBeforeTable2_L
        (UINT32)USER_GIP_BEFORE_TABLE3_L,                      // 0x048 UINT32 u32GIPBeforeTable3_L

        (UINT32)USER_GIP_BEFORE_TABLE0_R,                      // 0x04C UINT32 u32GIPBeforeTable0_R
        (UINT32)USER_GIP_BEFORE_TABLE1_R,                      // 0x050 UINT32 u32GIPBeforeTable1_R
        (UINT32)USER_GIP_BEFORE_TABLE2_R,                      // 0x054 UINR32 u32GIPBeforeTable2_R
        (UINT32)USER_GIP_BEFORE_TABLE3_R,                      // 0x058 UINT32 u32GIPBeforeTable3_R

        (UINT32)USER_GIP_AFTER_TABLE0_L,                       // 0x05C UINT32 u32GIPAfterTable0_L
        (UINT32)USER_GIP_AFTER_TABLE1_L,                       // 0x060 UINT32 u32GIPAfterTable1_L
        (UINT32)USER_GIP_AFTER_TABLE2_L,                       // 0x064 UINR32 u32GIPAfterTable2_L
        (UINT32)USER_GIP_AFTER_TABLE3_L,                       // 0x068 UINT32 u32GIPAfterTable3_L

        (UINT32)USER_GIP_AFTER_TABLE0_R,                       // 0x06C UINT32 u32GIPAfterTable0_R
        (UINT32)USER_GIP_AFTER_TABLE1_R,                       // 0x070 UINT32 u32GIPAfterTable1_R
        (UINT32)USER_GIP_AFTER_TABLE2_R,                       // 0x074 UINR32 u32GIPAfterTable2_R
        (UINT32)USER_GIP_AFTER_TABLE3_R,                       // 0x078 UINT32 u32GIPAfterTable3_R
        #if (! PC_SIMULATOR)
        ((UINT32)(&bootld_sram_end) + (UINT32)0x10),    	   // 0x07C~0x07F UINT32 u32ILM_End_Addr
        #else
        (UINT32)0,
        #endif
    },

    //===========================Switch on/off===========================//
    {
        (UINT8)USER_SWITCH_FREQ_HOPPING,                       // 0x080 UINT8 u8FreqHoppingSwitchEN
        (UINT8)USER_SWITCH_DOZE_MODE,                          // 0x081 UINT8 u8DozeModeSwitchEN
        (UINT8)USER_SWITCH_RAWDATA_NORMALIZATION,              // 0x082 UINT8 u8RawdataNormalizationSwitchEN
        (UINT8)USER_SWITCH_RAW_IIR,                            // 0x083 UINT8 u8RawIIRSwitchEN
        (UINT8)USER_SWITCH_COMMON_NOISE_CANCEL,                // 0x084 UINT8 u8CommonNoiseCancelSwitchEN
        (UINT8)USER_SWITCH_PALM_DETECT,                        // 0x085 UINT8 u8PalmDetectSwitchEN
        (UINT8)USER_SWITCH_WATER_DETECT,                       // 0x086 UINT8 u8WaterDetectSwitchEN
        (UINT8)USER_SWITCH_GLOVE,                              // 0x087 UINT8 u8GloveSwitchEN
        (UINT8)USER_SWITCH_P2P_CALIBRATION,                    // 0x088 UINT8 u8P2P_CalibrationSwitchEN
        (UINT8)USER_SWITCH_BOUNDARY,                           // 0x089 UINT8 u8BoundarySwitchEN
        (UINT8)USER_SWITCH_RC_CHECK,                           // 0x08A UINT8 u8RCCheckSwitchEN
        (UINT8)USER_SWITCH_GREEN_MODE_RC_CHECK,                // 0x08B UINT8 u8GreenModeRCCheckSwitchEN
        (UINT8)USER_SWITCH_ENFORCE_TWO_FINGER_SEPARATION,      // 0x08C UINT8 u8TwoFingerSeparationSwitchEN
        (UINT8)USER_SWITCH_PF_VECTOR_COMPENSATION,             // 0x08D UINT8 u8VectorCompensationEn
        (UINT8)USER_SWITCH_PF_BOUNDARY_COMPENSATION,           // 0x08E UINT8 u8BoundaryCompensationEn
        (UINT8)USER_SWITCH_PF_REPEATABILITY,                   // 0x08F UINT8 u8PointRepeatabilityEn
        (UINT8)USER_SWITCH_PF_JITTER,                          // 0x090 UINT8 u8PointJitterEn
        (UINT8)USER_SWITCH_PF_JITTER_COMPENSATION,             // 0x091 UINT8 u8PointJitterCompensationEn
        (UINT8)USER_SWITCH_PF_DYNAMIC_FIR,                     // 0x092 UINT8 u8PointDynamicFIREn
        (UINT8)USER_SWITCH_PF_POINT_IIR,                       // 0x093 UINT8 u8PointIIREn
        (UINT8)USER_SWITCH_RLE_REVERSE_X,                      // 0x094 UINT8 u8XReverse_En
        (UINT8)USER_SWITCH_RLE_REVERSE_Y,                      // 0x095 UINT8 u8YReverse_En
        (UINT8)USER_SWITCH_RLE_XY_SWAP,                        // 0x096 UINT8 u8XYSwitch_En
        (UINT8)USER_SWITCH_RLE_PANEL_SCALE,                    // 0x097 UINT8 u8PanelScale_En
        (UINT8)USER_SWITCH_STUCK_SIGNAL_DETECT,                // 0x098 UINT8 u8StuckSignalDetect_En
        (UINT8)USER_SWITCH_ELLIPTIC_GRAPH_CALCULATE,           // 0x099 UINT8 u8Elliptic_En
        (UINT8)USER_SWITCH_STOP_FW_DEBUG,                      // 0x09A UINT8 u8StopFWDebugEn
        (UINT8)USER_SWITCH_CR_NF_EN,                           // 0x09B UINT8 u8CRNF_En
        (UINT8)USER_SWITCH_DUAL_BASELINE,                      // 0x09C UINT8 u8DualBase_En
        (UINT8)0,                                              // 0x09D UINT8 u8FuncReseved_10
        (UINT8)0,                                              // 0x09E UINT8 u8FuncReseved_9
        (UINT8)0,                                              // 0x09F UINT8 u8FuncReseved_8
        (UINT8)0,                                              // 0x0A0 UINT8 u8FuncReseved_7
        (UINT8)0,                                              // 0x0A1 UINT8 u8FuncReseved_6
        (UINT8)0,                                              // 0x0A2 UINT8 u8FuncReseved_5
        (UINT8)0,                                              // 0x0A3 UINT8 u8FuncReseved_4
        (UINT8)0,                                              // 0x0A4 UINT8 u8FuncReseved_3
        (UINT8)0,                                              // 0x0A5 UINT8 u8FuncReseved_2
        (UINT8)0,                                              // 0x0A6 UINT8 u8FuncReseved_1
        (UINT8)0,                                              // 0x0A7 UINT8 u8FuncReseved_13
    },

    //===========================2D Threshold============================//
    {
        (UINT16)USER_S_2D_TP_TH,                               // 0x0A8 UINT16 u16S2DTPTh
        (UINT16)USER_HF_ENTER_TP_TH,                           // 0x0AA UINT16 u16EnterTpTh
        (UINT16)0,                                             // 0x0AC UINT16 u16Reserved
        (UINT16)0,                       					   // 0x0AE UINT16 u16Reserved0
        (UINT16)0,                    						   // 0x0B0 UINT16 u16Reserved1
        (UINT16)USER_HOPPING_TP_TH_HOPPING,                    // 0x0B2 UINT16 u16HoppingTPTh
        (UINT8)0,                                              // 0x0B4 UINT8 u8Reserved0
        (UINT8)0,                                              // 0x0B5 UINT8 u8Reserved1
        (UINT16)0,                                             // 0x0B6 UINT16 u16Reserved2
    },

    //===========================Raw data IIR============================//
    {
        (UINT8)USER_S_2D_IIR_WEIGHT,                           // 0x0B8 UINT8 u8NormalRawIIRWeight_Base
        (UINT8)USER_S_2D_IIR_WEIGHT_HOPPING,                   // 0x0B9 UINT8 u8NormalRawIIRWeight_Hopping
        (UINT8)USER_S_2D_IIR_WEIGHT,                           // 0x0BA UINT8 u8NormalRawIIRWeight_Tmp
        (UINT8)0                                               // 0x0BB UINT8 u8Reserved1
    },

    //===========================Baseline IP=============================//
    {
        (UINT8)USER_BL_IIR_WEIGHT_SUM,                         // 0x0BC UINT8  u8BLIIRTotalWeightSum
        (UINT8)USER_BL_IIR_CURR_WEIGHT,                        // 0x0BD UINT8  u8BLIIRCurrentWeight
        (UINT8)USER_BL_IIR_CURR_WEIGHT_GLOVE,                  // 0x0BE UINT8  u8BLIIRCurrentWeightGlove
        (UINT8)USER_BL_IIR_CURR_WEIGHT_2D_BL_UPDATE,           // 0x0BF UINT8  u8BLIIRCurrentWeight2DBLUPD
        (UINT16)USER_BL_IIR_CURR_WEIGHT_FAST,                  // 0x0C0 UINT16 u16BLIIRCurrentWeightFast
        (UINT8)USER_FW_BL_FAST_TRACKING_CNT_TH,                // 0x0C2 UINT8  u8BLFastTrackCnt
        (UINT8)USER_S_2D_NTC,                                  // 0x0C3 UINT8  u8S2DBLNTC
        (UINT16)USER_S_2D_BLRN,                                // 0x0C4 UINT16 u16S2DBLRN
        (UINT16)0,                        					   // 0x0C6 UINT16 u16Reserved0
        (UINT16)USER_RC_CHECK_LEVEL_CNT,                       // 0x0C8 UINT16 u16S2DBaseReKNum
        (UINT16)0,                   						   // 0x0CA UINT16 u16Reserved1
        (UINT32)0,                                             // 0x0CC UINT32 u32Reserved0
        (UINT32)0,                                             // 0x0D0 UINT32 u32Reserved1
    },

    //=========================Frequency hopping=========================//
    {
        (UINT16)USER_HOPPING_CARRIER_NOISE_TH,                 // 0x0D4 UINT16 u16CarrierNoiseTh
        (UINT16)0,                                             // 0x0D6 UINT16 u16Reserved0
        (UINT16)0,                                             // 0x0D8 UINT16 u16Reserved1
        (UINT16)0,                                             // 0x0DA UINT16 u16Reserved2
        (UINT16)0,                                             // 0x0DC UINT16 u16Reserved3
        {
            (INT16)0,                                          // 0x0DE INT16 as16FreqTuningBuf[0] -> FreqScan
            (INT16)0,                                          // 0x0E0 INT16 as16FreqTuningBuf[1]
            (INT16)0,                                          // 0x0E2 INT16 as16FreqTuningBuf[2]
            (INT16)0,                                          // 0x0E4 INT16 as16FreqTuningBuf[3]
            (INT16)0,                                          // 0x0E6 INT16 as16FreqTuningBuf[4]
            (INT16)0,                                          // 0x0E8 INT16 as16FreqTuningBuf[5]
            (INT16)0,                                          // 0x0EA INT16 as16FreqTuningBuf[6]
            (INT16)0,                                          // 0x0EC INT16 as16FreqTuningBuf[7]
            (INT16)0,                                          // 0x0EE INT16 as16FreqTuningBuf[8]
            (INT16)0,                                          // 0x0F0 INT16 as16FreqTuningBuf[9]
            (INT16)0,                                          // 0x0F2 INT16 as16FreqTuningBuf[10]
            (INT16)0,                                          // 0x0F4 INT16 as16FreqTuningBuf[11]
            (INT16)0,                                          // 0x0F6 INT16 as16FreqTuningBuf[12]
        },
    },

    //=====================COMMON Noise Canceling========================//
    {
        (UINT8)USER_COMMON_NOISE_AVG_VALID_POINT_TH,           // 0x0F8 UINT8 u8AvgValidPointTh
        (UINT8)USER_COMMON_NOISE_HISTOGRAM_BIN_SCALE,          // 0x0F9 UINT8 u8HistogramBinScale
        (UINT8)USER_COMMON_NOISE_HISTOGRAM_BIN_SCALE,          // 0x0FA UINT8 u8HistogramBinScale_Base
        (UINT8)0,                                              // 0x0FB UINT8 u8Reserved0
        (UINT16)USER_COMMON_NOISE_HISTOGRAM_TH,                // 0x0FC UINT16 u16HistogramTH_Base
        (UINT16)0,                                             // 0x0FE UINT16 u16Reserved0
    },

    //===========================Palm Detect=============================//
    {
        (UINT8)USER_PALM_MODE,                                 // 0x100 UINT8 u8PalmMode
        (UINT8)USER_PALM_KEEP_CNT,                             // 0x101 UINT8 u8PalmKeepCNT
        (UINT8)USER_PALM_NORMSUM_THD_LEVEL1,                   // 0x102 UINT8 u8PalmNormSumTHDLevel1
        (UINT8)USER_PALM_NORMSUM_THD_LEVEL1_IN_PALM,           // 0x103 UINT8 u8PalmNormSumTHDLevel1InPalm
        (UINT8)USER_PALM_JUDGE_TYPE,                           // 0x104 UINT8 u8PalmJudgeType
        (UINT8)USER_PALM_TARGET_PHI,                           // 0x105 UINT8 u8PalmTargetPhi
        (UINT16)0,                                             // 0x106 UINT16 u16reserved0
        (UINT32)0,                                             // 0x108 UINT32 u32reserved1
        (UINT32)0,                                             // 0x10C UINT32 u32reserved2
    },

    //=======================Zone========================================//
    {
        { (UINT8)USER_ZONE_AREA_TH_0,  (UINT8)USER_ZONE_AREA_TH_1,  (UINT8)USER_ZONE_AREA_TH_2,  (UINT8)USER_ZONE_AREA_TH_3,  (UINT8)USER_ZONE_AREA_TH_4,  (UINT8)USER_ZONE_AREA_TH_5,  (UINT8)USER_ZONE_AREA_TH_6,  (UINT8)USER_ZONE_AREA_TH_7 },	// 0x110 UINT8 au8AreaThTable_Current[8]
        { (UINT8)USER_ZONE_MERGE_TH_0, (UINT8)USER_ZONE_MERGE_TH_1, (UINT8)USER_ZONE_MERGE_TH_2, (UINT8)USER_ZONE_MERGE_TH_3, (UINT8)USER_ZONE_MERGE_TH_4, (UINT8)USER_ZONE_MERGE_TH_5, (UINT8)USER_ZONE_MERGE_TH_6, (UINT8)USER_ZONE_MERGE_TH_7 }, // 0x118 UINT8 au8MergeThTable_Current[8]
        (UINT8)ZONE_MERGE_TABLE_NUM,																																																				// 0x120 UINT8 u8MergeTableNum
        (UINT8)0,																																																									// 0x121 UINT8 u8Reserved0
        (UINT8)0,																																																									// 0x122 UINT8 u8Reserved1
        (UINT8)0,																																																									// 0x123 UINT8 u8Reserved2
        (UINT32)0,																																																									// 0x124 UINT32 u32Reserved_Dummy_Align
    },

    //=======================Zone Other==================================//
    {
        { (UINT8)USER_ZONE_AREA_TH_0,          (UINT8)USER_ZONE_AREA_TH_1,          (UINT8)USER_ZONE_AREA_TH_2,          (UINT8)USER_ZONE_AREA_TH_3,          (UINT8)USER_ZONE_AREA_TH_4,          (UINT8)USER_ZONE_AREA_TH_5,          (UINT8)USER_ZONE_AREA_TH_6,          (UINT8)USER_ZONE_AREA_TH_7 },			// 0x128 UINT8 au8AreaThTable[8]
        { (UINT8)USER_ZONE_MERGE_TH_0,         (UINT8)USER_ZONE_MERGE_TH_1,         (UINT8)USER_ZONE_MERGE_TH_2,         (UINT8)USER_ZONE_MERGE_TH_3,         (UINT8)USER_ZONE_MERGE_TH_4,         (UINT8)USER_ZONE_MERGE_TH_5,         (UINT8)USER_ZONE_MERGE_TH_6,         (UINT8)USER_ZONE_MERGE_TH_7 },			// 0x130 UINT8 au8MergeThTable[8]

        { (UINT8)USER_ZONE_AREA_TH_PALM_0,     (UINT8)USER_ZONE_AREA_TH_PALM_1,     (UINT8)USER_ZONE_AREA_TH_PALM_2,     (UINT8)USER_ZONE_AREA_TH_PALM_3,     (UINT8)USER_ZONE_AREA_TH_PALM_4,     (UINT8)USER_ZONE_AREA_TH_PALM_5,     (UINT8)USER_ZONE_AREA_TH_PALM_6,     (UINT8)USER_ZONE_AREA_TH_PALM_7 },		// 0x138 UINT8 au8AreaThTable_Palm[8]
        { (UINT8)USER_ZONE_MERGE_TH_PALM_0,    (UINT8)USER_ZONE_MERGE_TH_PALM_1,    (UINT8)USER_ZONE_MERGE_TH_PALM_2,    (UINT8)USER_ZONE_MERGE_TH_PALM_3,    (UINT8)USER_ZONE_MERGE_TH_PALM_4,    (UINT8)USER_ZONE_MERGE_TH_PALM_5,    (UINT8)USER_ZONE_MERGE_TH_PALM_6,    (UINT8)USER_ZONE_MERGE_TH_PALM_7 },	// 0x140 UINT8 au8MergeThTable_Palm[8]

        { (UINT8)USER_ZONE_AREA_TH_HOPPING_0,  (UINT8)USER_ZONE_AREA_TH_HOPPING_1,  (UINT8)USER_ZONE_AREA_TH_HOPPING_2,  (UINT8)USER_ZONE_AREA_TH_HOPPING_3,  (UINT8)USER_ZONE_AREA_TH_HOPPING_4,  (UINT8)USER_ZONE_AREA_TH_HOPPING_5,  (UINT8)USER_ZONE_AREA_TH_HOPPING_6,  (UINT8)USER_ZONE_AREA_TH_HOPPING_7 },	// 0x148 UINT8 au8AreaThTable_Hopping[8]
        { (UINT8)USER_ZONE_MERGE_TH_HOPPING_0, (UINT8)USER_ZONE_MERGE_TH_HOPPING_1, (UINT8)USER_ZONE_MERGE_TH_HOPPING_2, (UINT8)USER_ZONE_MERGE_TH_HOPPING_3, (UINT8)USER_ZONE_MERGE_TH_HOPPING_4, (UINT8)USER_ZONE_MERGE_TH_HOPPING_5, (UINT8)USER_ZONE_MERGE_TH_HOPPING_6, (UINT8)USER_ZONE_MERGE_TH_HOPPING_7 },	// 0x150 UINT8 au8MergeThTable_Hopping[8]

        (UINT32)0,																																																																									// 0x158 UINT32 u32Reserved_Dummy_Align
    },

    //====================Glove==========================================//
    {
        {
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_0,                // 0x15C UINT8 au8GloveEnterDebounceTable[0]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_1,                // 0x15D UINT8 au8GloveEnterDebounceTable[1]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_2,                // 0x15E UINT8 au8GloveEnterDebounceTable[2]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_3,                // 0x15F UINT8 au8GloveEnterDebounceTable[3]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_4,                // 0x160 UINT8 au8GloveEnterDebounceTable[4]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_5,                // 0x161 UINT8 au8GloveEnterDebounceTable[5]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_6,                // 0x162 UINT8 au8GloveEnterDebounceTable[6]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_7,                // 0x163 UINT8 au8GloveEnterDebounceTable[7]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_8,                // 0x164 UINT8 au8GloveEnterDebounceTable[8]
            (UINT8)USER_GLOVE_ENTER_DEBOUNCE_9,                // 0x165 UINT8 au8GloveEnterDebounceTable[9]
        },
        (UINT16)USER_GLOVE_FIND_PEAK_TH,                       // 0x166 UINT16 u16GloveFindPeakTh
        (UINT16)USER_GLOVE_ENTER_TP_TH,                        // 0x168 UINT16 u16GloveEnterTpTh
        (UINT8)USER_GLOVE_AREA_MIN_TH,                         // 0x16A UINT8 u8GloveAreaMinTh
        (UINT8)0,                                              // 0x16B UINT8 u8Reserved0
        (UINT16)0,                                             // 0x16C UINT16 u16Reserved1
        (UINT16)0,                                             // 0x16E UINT16 u16Reserved2
    },

    //====================WakeupGesture==================================//
    {
        (UINT16)USER_WKG_SWITCH_GESTURE_ITEM,                  // 0x170 UINT16 u16GestureItemEn
        (UINT16)USER_WKG_SWITCH_GESTURE_ITEM_EXTEND,           // 0x172 UINT16 u16GestureItemEnExtend
        (UINT16)USER_WKG_TP_TH,                                // 0x174 UINT16 u16WKGTPTh
        (UINT8)USER_WKG_AREA_TH,                               // 0x176 UINT8 u8AreaTh
        (UINT8)USER_WKG_TRACK_ENTER_DEBOUNCE,                  // 0x177 UINT8 u8EnterDebounce
        (UINT16)USER_WKG_FRAME_LIMIT,                          // 0x178 UINT16 u16FrameLimit
        (UINT16)USER_WKG_VECTOR_CHECK_LENGTH,                  // 0x17A UINT16 u16VectorCheckLength
        (UINT16)USER_WKG_VECTOR_LENGTH_X,                      // 0x17C UINT16 u16VectorLengthX
        (UINT16)USER_WKG_VECTOR_LENGTH_Y,                      // 0x17E UINT16 u16VectorLengthY
        (UINT16)USER_WKG_SLIDE_HORIZONTAL_X_LENGTH,            // 0x180 UINT16 u16SlideLengthHorizontalX
        (UINT16)USER_WKG_SLIDE_HORIZONTAL_Y_LENGTH,            // 0x182 UINT16 u16SlideLengthHorizontalY
        (UINT16)USER_WKG_SLIDE_VERTICAL_X_LENGTH,              // 0x184 UINT16 u16SlideLengthVerticalX
        (UINT16)USER_WKG_SLIDE_VERTICAL_Y_LENGTH,              // 0x186 UINT16 u16SlideLengthVerticalY
        (UINT16)USER_WKG_SLIDE_XMIN_STARTRANGE,                // 0x188 UINT16 u16SlideStartRangeXMin
        (UINT16)USER_WKG_SLIDE_XMAX_STARTRANGE,                // 0x18A UINT16 u16SlideStartRangeXMax
        (UINT16)USER_WKG_SLIDE_YMIN_STARTRANGE,                // 0x18C UINT16 u16SlideStartRangeYMin
        (UINT16)USER_WKG_SLIDE_YMAX_STARTRANGE,                // 0x18E UINT16 u16SlideStartRangeYMax
        (UINT16)USER_WKG_ALPHABET_TOTAL_LENGTH_TH,             // 0x190 UINT16 u16AlphabetTotalLengthTh
        (UINT16)USER_WKG_ALPHABET_XMIN_STARTRANGE,             // 0x192 UINT16 u16AlphabetStartRangeXMin
        (UINT16)USER_WKG_ALPHABET_XMAX_STARTRANGE,             // 0x194 UINT16 u16AlphabetStartRangeXMax
        (UINT16)USER_WKG_ALPHABET_YMIN_STARTRANGE,             // 0x196 UINT16 u16AlphabetStartRangeYMin
        (UINT16)USER_WKG_ALPHABET_YMAX_STARTRANGE,             // 0x198 UINT16 u16AlphabetStartRangeYMax
        (UINT8)USER_WKG_ALPHABET_X_RATIO_C_FIRST2END,          // 0x19A UINT8 u8AlphabetCFirst2EndXRatio
        (UINT8)USER_WKG_ALPHABET_Y_RATIO_C_FIRST2END,          // 0x19B UINT8 u8AlphabetCFirst2EndYRatio
        (UINT8)USER_WKG_ALPHABET_X_RATIO_C_Y_VERSE,            // 0x19C UINT8 u8AlphabetCYVerseXRatio
        (UINT8)USER_WKG_ALPHABET_X_RATIO_e_FIRST2END,          // 0x19D UINT8 u8AlphabeteFirst2EndXRatio
        (UINT8)USER_WKG_ALPHABET_Y_RATIO_e_FIRST2END,          // 0x19E UINT8 u8AlphabeteFirst2EndYRatio
        (UINT8)USER_WKG_ALPHABET_Y_RATIO_V_FIRST2END,          // 0x19F UINT8 u8AlphabetVFirst2EndYRatio
        (UINT8)USER_WKG_ALPHABET_Y_RATIO_V_X_VERSE,            // 0x1A0 UINT8 u8AlphabetVXVerseYRatio
        (UINT8)0,                                              // 0x1A1 UINT8 u8Reserved1
        (UINT16)USER_WKG_DBCLICK_DISTANCE_RANGE,               // 0x1A2 UINT16 u16DBClickDisRange
        (UINT16)USER_WKG_DBCLICK_FINGERON_FRAME_LIMIT,         // 0x1A4 UINT16 u16DBClickFrameLimitFingerOn
        (UINT16)USER_WKG_DBCLICK_FINGEROFF_FRAME_LIMIT,        // 0x1A6 UINT16 u16DBClickFrameLimitFingerOff
        (UINT16)USER_WKG_DBCLICK_XMIN_STARTRANGE,              // 0x1A8 UINT16 u16DBClickStartRangeXMin
        (UINT16)USER_WKG_DBCLICK_XMAX_STARTRANGE,              // 0x1AA UINT16 u16DBClickStartRangeXMax
        (UINT16)USER_WKG_DBCLICK_YMIN_STARTRANGE,              // 0x1AC UINT16 u16DBClickStartRangeYMin
        (UINT16)USER_WKG_DBCLICK_YMAX_STARTRANGE,              // 0x1AE UINT16 u16DBClickStartRangeYMax
        (UINT16)USER_WKG_ENTER_TP_TH,                          // 0x1B0 UINT16 u16WKGEnterTPTh
        (UINT16)0,                                             // 0x1B2 UINT16 u16Reserved_Dummy_Align
    },

    //=======================P2P Calibration=============================//
    {
        (UINT16)USER_P2P_RESOLUTION_X,                         // 0x1B4 UINT16 u16P2PResolutionX
        (UINT16)USER_P2P_EDGE_RESOLUTION_X,                    // 0x1B6 UINT16 u16P2PEdgeResolutionX
        (UINT16)USER_P2P_CENTER_POINT_X,                       // 0x1B8 UINT16 u16P2PCenterPointX
        (UINT16)USER_P2P_EDGE_POINT_X,                         // 0x1BA UINT16 u16P2PEdgePointX
        (UINT16)USER_P2P_TOTAL_POINT_X,                        // 0x1BC UINT16 u16P2PPointX
        USER_P2P_CALI_TABLE_X,                                 // 0x1BE UINT16 au16P2PCaliTableX[300]

        (UINT16)USER_P2P_RESOLUTION_Y,                         // 0x416 UINT16 u16P2PResolutionY
        (UINT16)USER_P2P_EDGE_RESOLUTION_Y,                    // 0x418 UINT16 u16P2PEdgeResolutionY
        (UINT16)USER_P2P_CENTER_POINT_Y,                       // 0x41A UINT16 u16P2PCenterPointY
        (UINT16)USER_P2P_EDGE_POINT_Y,                         // 0x41C UINT16 u16P2PEdgePointY
        (UINT16)USER_P2P_TOTAL_POINT_Y,                        // 0x41E UINT16 u16P2PPointY
        USER_P2P_CALI_TABLE_Y,                                 // 0x420 UINT16 au16P2PCaliTableY[100]
        (UINT32)0,                                             // 0x4E8 UINT32 u32Reserved_Dummy_Align
    },

    //=======================Tracking====================================//
    {
        (UINT8)USER_TRACK_TAPPING_HIGHSPEED_CNT_TH,                                                     	// 0x4EC UINT8 u8HighSpeedCntTh
        (UINT8)USER_TRACK_BREAK_DEBOUNCE,                                                               	// 0x4ED UINT8 u8BreakDebounceTh
        (UINT16)USER_TRACK_TAPPING_RATIO_TH,                                                            	// 0x4EE UINT16 u16RatioTh
        ((UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH * (UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH),            	// 0x4F0 UINT32 u32SmallDistanceTh
        ((UINT32)USER_TRACK_TAPPING_MIDDLE_DIS_TH * (UINT32)USER_TRACK_TAPPING_MIDDLE_DIS_TH),          	// 0x4F4 UINT32 u32MiddleDistanceTh
        ((UINT32)USER_TRACK_TAPPING_MAX_ALLOW_DIS_TH * (UINT32)USER_TRACK_TAPPING_MAX_ALLOW_DIS_TH),    	// 0x4F8 UINT32 u32MaxAllowDistanceTh
        ((UINT32)USER_TRACK_TAPPING_HIGH_SPEED_DIS_TH * (UINT32)USER_TRACK_TAPPING_HIGH_SPEED_DIS_TH),  	// 0x4FC UINT32 u32HighSpeedDistanceTh

        ((UINT32)USER_TRACK_TAPPING_HI_TH * (UINT32)USER_TRACK_TAPPING_HI_TH),                          	// 0x500 UINT32 u32HighSpeedTh
        ((UINT32)USER_TRACK_TAPPING_LOW_TH * (UINT32)USER_TRACK_TAPPING_LOW_TH),                        	// 0x504 UINT32 u32LowSpeedTh
        {
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_0,                                                         	// 0x508 UINT8 au8EnterDebounceTable[0]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_1,                                                         	// 0x509 UINT8 au8EnterDebounceTable[1]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_2,                                                         	// 0x50A UINT8 au8EnterDebounceTable[2]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_3,                                                         	// 0x50B UINT8 au8EnterDebounceTable[3]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_4,                                                         	// 0x50C UINT8 au8EnterDebounceTable[4]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_5,                                                         	// 0x50D UINT8 au8EnterDebounceTable[5]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_6,                                                         	// 0x50E UINT8 au8EnterDebounceTable[6]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_7,                                                         	// 0x50F UINT8 au8EnterDebounceTable[7]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_8,                                                         	// 0x510 UINT8 au8EnterDebounceTable[8]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_9,                                                         	// 0x511 UINT8 au8EnterDebounceTable[9]
        },
        (UINT8)USER_TRACK_SWITCH_UNCERTAIN,                                                             	// 0x512 UINT8 u8UncertainEn
        (UINT8)USER_TRACK_TAPPING_COSINE,                                                               	// 0x513 UINT8 u8TappingCosine
        (UINT8)USER_TRACK_UNCERTAIN_RANGE,                                                              	// 0x514 UINT8 u8UncertainRange
        (UINT8)USER_TRACK_SMALL_DIS_TH_RATIO,                                                           	// 0x515 UINT8 u8SmallDistanceThRatio
        (UINT16)0,                                                                                      	// 0x516 UINT16 u16Reserved0
        (UINT32)0,                                                                                      	// 0x518 UINT32 u32Reserved_Dummy_Align
    },

    //=======================Tracking Other==============================//
    {
        (UINT8)USER_TRACK_BREAK_DEBOUNCE,                                                                    // 0x51C UINT8 u8BreakDebounceTh
        {                                                                                                    
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_0,                                                              // 0x51D UINT8 au8EnterDebounceTable[0]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_1,                                                              // 0x51E UINT8 au8EnterDebounceTable[1]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_2,                                                              // 0x51F UINT8 au8EnterDebounceTable[2]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_3,                                                              // 0x520 UINT8 au8EnterDebounceTable[3]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_4,                                                              // 0x521 UINT8 au8EnterDebounceTable[4]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_5,                                                              // 0x522 UINT8 au8EnterDebounceTable[5]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_6,                                                              // 0x523 UINT8 au8EnterDebounceTable[6]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_7,                                                              // 0x524 UINT8 au8EnterDebounceTable[7]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_8,                                                              // 0x525 UINT8 au8EnterDebounceTable[8]
            (UINT8)USER_TRACK_ENTER_DEBOUNCE_9,                                                              // 0x526 UINT8 au8EnterDebounceTable[9]
        },
        (UINT8)0,                                                                                            // 0x527 UINT8 u8Reserved0
        ((UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH * (UINT32)USER_TRACK_TAPPING_SMALL_DIS_TH),                 // 0x528 UINT32 u32SmallDistanceTh

        (UINT8)USER_TRACK_BREAK_DEBOUNCE_HOPPING,                                                            // 0x52C UINT8 u8BreakDebounceTh_Hopping
        (UINT8)0,                                                                                            // 0x52D UINT8 u8Reserved1
        (UINT8)0,                                                                                            // 0x52E UINT8 u8Reserved2
        (UINT8)0,                                                                                            // 0x52F UINT8 u8Reserved3
        ((UINT32)USER_TRACK_TAPPING_HOPPING_SMALL_DIS_TH * (UINT32)USER_TRACK_TAPPING_HOPPING_SMALL_DIS_TH), // 0x530 UINT32 u32SmallDistanceTh_Hopping
        (UINT32)0,                                                                                           // 0x534 UINT32 u32Reserved_Dummy_Align
    },

    //=======================VectorCompensation==========================//
    {
        (UINT8)USER_VC_FACTOR_X,                               // 0x538 UINT8 u8CompensateFactorX
        (UINT8)USER_VC_FACTOR_Y,                               // 0x539 UINT8 u8CompensateFactorY
        (UINT8)USER_VC_BASE_X,                                 // 0x53A UINT8 u8CompensateBaseX
        (UINT8)USER_VC_BASE_Y,                                 // 0x53B UINT8 u8CompensateBaseY
        (UINT16)USER_VC_TH,                                    // 0x53C UINT16 u16VectorCompensateTh
        (UINT16)0,                                             // 0x53E UINT16 u16Reserved0
        (UINT32)0,                                             // 0x540 UINT32 u32Reserved_Dummy_Align
    },

    //===================== VectorCompensation Other=====================//
    {
        (UINT8)USER_VC_FACTOR_X,                               // 0x544 UINT8 u8CompensateFactorX
        (UINT8)USER_VC_FACTOR_Y,                               // 0x545 UINT8 u8CompensateFactorY
        (UINT8)USER_VC_FACTOR_X_Hopping,                       // 0x546 UINT8 u8CompensateFactorX_Hopping
        (UINT8)USER_VC_FACTOR_Y_Hopping,                       // 0x547 UINT8 u8CompensateFactorY_Hopping
    },

    //=======================Point Filter================================//
    {
        (UINT16)USER_PF_BOUNDARY_RANGE_TH,                                    // 0x548 UINT16 u16BoundaryCompensationRangeTh
        (UINT16)USER_PF_BOUNDARY_DIS_TH,                                      // 0x54A UINT16 u16BoundaryCompensationDisTh
        (UINT16)USER_PF_REPEAT_BREAK_CNT_TH,                                  // 0x54C UINT16 u16RepeatBreakCntTh
        (UINT16)USER_PF_REPEAT_DIS_TH,                                        // 0x54E UINT16 u16RepeatDisTh
        (UINT16)USER_PF_REPEAT_WEIGHTING,                                     // 0x550 UINT16 u16RepeatWeighting

        ((UINT16)USER_PF_JITTER_HUGE_TH * (UINT16)USER_PF_JITTER_HUGE_TH),    // 0x552 UINT16 u16JitterHugeTh
        ((UINT16)USER_PF_JITTER_BIG_TH * (UINT16)USER_PF_JITTER_BIG_TH),      // 0x554 UINT16 u16JitterBigTh
        ((UINT16)USER_PF_JITTER_SMALL_TH * (UINT16)USER_PF_JITTER_SMALL_TH),  // 0x556 UINT16 u16JitterSmallTh
        (UINT16)USER_PF_JITTER_COMPENSATE_DIS,                                // 0x558 UINT16 u16JitterCompensateDistance
        (UINT16)USER_PF_JITTER_COMPENSATE_DIS_MIN,                            // 0x55A UINT16 u16JitterCompensateDistanceMin
        (UINT8)USER_PF_JITTER_PROTECT_CNT,                                    // 0x55C UINT8 u8JitterProtectCnt
        (UINT8)USER_PF_JITTER_EDGE_PROTECT_RANGE,                             // 0x55D UINT8 u8JitterEdgeProtectRange

        (UINT8)USER_PF_FIR_LEVEL_MIN,                                         // 0x55E UINT8 u8PfirMinLevel
        (UINT8)USER_PF_FIR_LEVEL_MAX,                                         // 0x55F UINT8 u8PfirMaxLevel
        (UINT16)USER_PF_FIR_DISTANCE_MIN,                                     // 0x560 UINT16 u16PfirMinDistance
        (UINT16)USER_PF_FIR_DISATNCE_MAX,                                     // 0x562 UINT16 u16PfirMaxDistance

        (UINT8)USER_PF_IIR_STR_MIN,                                           // 0x564 UINT8 u8PiirMinStrength
        (UINT8)USER_PF_IIR_STR_MAX,                                           // 0x565 UINT8 u8PiirMaxStrength
        (UINT16)USER_PF_IIR_DIS_MIN,                                          // 0x566 UINT16 u16PiirMinDistance
        (UINT16)USER_PF_IIR_DIS_MAX,                                          // 0x568 UINT16 u16PiirMaxDistance
        (UINT16)0,                                                            // 0x56A UINT16 u16Reserved_Dummy_Align
    },

    //========================== Point Filter Other =====================//
    {
        (UINT8)USER_PF_FIR_LEVEL_MIN,                          // 0x56C UINT8 u8PfirMinLevel
        (UINT8)USER_PF_FIR_LEVEL_MAX,                          // 0x56D UINT8 u8PfirMaxLevel
        (UINT16)USER_PF_FIR_DISTANCE_MIN,                      // 0x56E UINT16 u16PfirMinDistance
        (UINT16)USER_PF_FIR_DISATNCE_MAX,                      // 0x570 UINT16 u16PfirMaxDistance

        (UINT8)USER_PF_IIR_STR_MIN,                            // 0x572 UINT8 u8PiirMinStrength
        (UINT8)USER_PF_IIR_STR_MAX,                            // 0x573 UINT8 u8PiirMaxStrength
        (UINT16)USER_PF_IIR_DIS_MIN,                           // 0x574 UINT16 u16PiirMinDistance
        (UINT16)USER_PF_IIR_DIS_MAX,                           // 0x576 UINT16 u16PiirMaxDistance

        (UINT8)USER_PF_FIR_LEVEL_MIN_HOPPING,                  // 0x578 UINT8 u8PfirMinLevel_Hopping
        (UINT8)USER_PF_FIR_LEVEL_MAX_HOPPING,                  // 0x579 UINT8 u8PfirMaxLevel_Hopping
        (UINT16)USER_PF_FIR_DISTANCE_MIN_HOPPING,              // 0x57A UINT16 u16PfirMinDistance_Hopping
        (UINT16)USER_PF_FIR_DISATNCE_MAX_HOPPING,              // 0x57C UINT16 u16PfirMaxDistance_Hopping

        (UINT8)USER_PF_IIR_STR_MIN_HOPPING,                    // 0x57E UINT8 u8PiirMinStrength_Hopping
        (UINT8)USER_PF_IIR_STR_MAX_HOPPING,                    // 0x57F UINT8 u8PiirMaxStrength_Hopping
        (UINT16)USER_PF_IIR_DIS_MIN_HOPPING,                   // 0x580 UINT16 u16PiirMinDistance_Hopping
        (UINT16)USER_PF_IIR_DIS_MAX_HOPPING,                   // 0x582 UINT16 u16PiirMaxDistance_Hopping
    },

    //======================= Green mode ================================//
    {
        (UINT16)USER_GMD_ENTER_DOZE_FRAME_TH,                  // 0x584 UINT16 u16EnterDozeFrameTh
        (UINT8)USER_GMD_FDM_PD_INTERVAL,                       // 0x586 UINT8 u8FDMPDInterval
        (UINT8)USER_GMD_WKG_PD_INTERVAL,                       // 0x587 UINT8 u8WKGPDInterval
        (UINT16)USER_GMD_FDM_TP_TH,                            // 0x588 UINT16 u16FDMTPTh
        (UINT16)USER_GMD_DOZE_TP_TH,                           // 0x58A UINT16 u16DozeTPTh
        (UINT16)USER_GMD_FDM_REK_TP_TH,                        // 0x58C UINT16 u16DozeFDMBLRekDiffTh
        (UINT16)USER_GMD_FDM_REK_CNT_TH,                       // 0x58E UINT16 u16DozeFDMBLRekFrameCnt
        (UINT16)USER_GMD_DOZE_S1D_BL_UPDATE_FRAME_TH,          // 0x590 UINT16 u16Doze1DBLUpdateFrameTh
        (UINT16)USER_GMD_DOZE_S2D_BL_UPDATE_FRAME_TH,          // 0x592 UINT16 u16Doze2DBLUpdateFrameTh
        (UINT16)USER_GMD_FDM_S1D_BL_UPDATE_FRAME_TH,           // 0x594 UINT16 u16FDM1DBLUpdateFrameTh
        (UINT16)USER_GMD_FDM_S2D_BL_UPDATE_FRAME_TH,           // 0x596 UINT16 u16FDM2DBLUpdateFrameTh
        (UINT16)USER_GMD_DOZE_S1D_GLOVE_BL_UPDATE_FRAME_TH,    // 0x598 UINT16 u16Doze1DBLUpdateFrameTh_Glove
        (UINT16)USER_GMD_DOZE_S2D_GLOVE_BL_UPDATE_FRAME_TH,    // 0x59A UINT16 u16Doze2DBLUpdateFrameTh_Glove
        (UINT8)USER_GMD_S1D_IIR_WEIGHT,                        // 0x59C UINT8 u8S1DIIRWeight
        (UINT8)0,                                              // 0x59D UINT8 u8Reserved0
        (UINT16)0,                                             // 0x59E UINT16 u16Reserved0
        (UINT32)0,                                             // 0x5A0 UINT32 u32Reserved0
        (UINT32)0,                                             // 0x5A4 UINT32 u32Reserved1
    },

    //======================= Boundary ==================================//
    {
        (INT16)USER_BD_GAUSSIAN_R_X0,                          // 0x5A8 INT16 s16GaussianR_X0
        (INT16)USER_BD_GAUSSIAN_R_X1,                          // 0x5AA INT16 s16GaussianR_X1
        (INT16)USER_BD_GAUSSIAN_R_Y0,                          // 0x5AC INT16 s16GaussianR_Y0
        (INT16)USER_BD_GAUSSIAN_R_Y1,                          // 0x5AE INT16 s16GaussianR_Y1

        (UINT32)0,                                             // 0x5B0 UINT32 u32Reserved_Dummy_Align
    },


    //=======================FFM2CPU Customized CMD =====================//
    {

        // CMD number & R/W option
        (UINT8)0,                                              // 0x5B4 UINT8 u8FFM2CPU_CMD_Num
        (UINT8)0,                                              // 0x5B5 UINT8 u8FFM2CPU_CMD_RW
        (UINT16)0,                                             // 0x5B6 UINT16 u16Reserved

        // 1
        (UINT16)0,                                             // 0x5B8 UINT16 u16FFM2CPU_CMD1_Addr
        (UINT8)0,                                              // 0x5BA UINT8 u8FFM2CPU_CMD1_WriteData
        (UINT8)0,                                              // 0x5BB UINT8 u8FFM2CPU_CMD1_ReadData

        // 2
        (UINT16)0,                                             // 0x5BC UINT16 u16FFM2CPU_CMD2_Addr
        (UINT8)0,                                              // 0x5BE UINT8 u8FFM2CPU_CMD2_WriteData
        (UINT8)0,                                              // 0x5BF UINT8 u8FFM2CPU_CMD2_ReadData

        // 3
        (UINT16)0,                                             // 0x5C0 u16FFM2CPU_CMD3_Addr
        (UINT8)0,                                              // 0x5C2 UINT8 u8FFM2CPU_CMD3_WriteData
        (UINT8)0,                                              // 0x5C3 UINT8 u8FFM2CPU_CMD3_ReadData

        // 4
        (UINT16)0,                                             // 0x5C4 u16FFM2CPU_CMD4_Addr
        (UINT8)0,                                              // 0x5C6 UINT8 u8FFM2CPU_CMD4_WriteData
        (UINT8)0,                                              // 0x5C7 UINT8 u8FFM2CPU_CMD4_ReadData

        // 5
        (UINT16)0,                                             // 0x5C8 UINT16 u16FFM2CPU_CMD5_Addr
        (UINT8)0,                                              // 0x5CA UINT8 u8FFM2CPU_CMD5_WriteData
        (UINT8)0,                                              // 0x5CB UINT8 u8FFM2CPU_CMD5_ReadData

        // 6
        (UINT16)0,                                             // 0x5CC UINT16 u16FFM2CPU_CMD6_Addr
        (UINT8)0,                                              // 0x5CE UINT8 u8FFM2CPU_CMD6_WriteData
        (UINT8)0,                                              // 0x5CF UINT8 u8FFM2CPU_CMD6_ReadData                               // 0x5CF
    },

    //========================== OSC Trim ===============================//
    {
        {
            (UINT16)USER_OSC_TRIM_REK_F1_ABNORMAL_DIFF_TH,     // 0x5D0 u16OSCTrimReKTH[0]
            (UINT16)USER_OSC_TRIM_REK_F2_ABNORMAL_DIFF_TH,     // 0x5D2 u16OSCTrimReKTH[1]
            (UINT16)USER_OSC_TRIM_REK_F3_ABNORMAL_DIFF_TH,     // 0x5D4 u16OSCTrimReKTH[2]
            (UINT16)USER_OSC_TRIM_REK_F4_ABNORMAL_DIFF_TH,     // 0x5D6 u16OSCTrimReKTH[3]
        },
        (UINT16)0,                                             // 0x5D8 u16Reserved0
        (UINT8)USER_OSC_TRIM_REK_AREA_TH,                      // 0x5DA u8OSCTrimReKAreaTH
        (UINT8)0,                                              // 0x5DB u8Reserved0
        (UINT8)0,                                              // 0x5DC u32Reserved_Dummy_Align
    },

    //======================= TwoFingerSeparation =======================//
    {
        (UINT8)USER_TFS_EGA_COORD_DOWNSCALE,                   // 0x5E0 UINT8 u8CoordScale
        (UINT8)USER_TFS_EGA_DIFF_DOWNSCALE,                    // 0x5E1 UINT8 u8DiffScale
        (UINT8)0,											   // 0x5E2 UINT8 u8Reserved0
        (UINT8)0,											   // 0x5E3 UINT8 u8Reserved1
        (UINT8)0,											   // 0x5E4 UINT8 u8Reserved2
        (UINT8)0,											   // 0x5E5 UINT8 u8Reserved3
        (UINT8)USER_TFS_EGA_MAJOR_SCALE,                       // 0x5E6 UINT8 u8MajorScale
        (UINT8)USER_TFS_EGA_MINOR_SCALE                        // 0x5E7 UINT8 u8MinorScale
    },
    //======================= Customized FW Config==========================//
    {
        {
            (UINT32)0,                                         // 0x5E8 UINT32 u32Reserved[0]
            (UINT32)0,                                         // 0x5EC UINT32 u32Reserved[1]
            (UINT32)0,                                         // 0x5F0 UINT32 u32Reserved[2]
            (UINT32)0,                                         // 0x5F4 UINT32 u32Reserved[3]
            (UINT32)0,                                         // 0x5F8 UINT32 u32Reserved[4]
            (UINT32)0,                                         // 0x5FC UINT32 u32Reserved[5]
            (UINT32)0,                                         // 0x600 UINT32 u32Reserved[6]
            (UINT32)0,                                         // 0x604 UINT32 u32Reserved[7]
        },
    },

    //========================= EMS Solution ============================//
    {
        (UINT8)USER_EMS_REK_RAWDATA_CHECK,                     // 0x608 UINT8 u8RekRawdataCheckSwitchEN

        (UINT8)USER_SWITCH_ABN_STOP_BL_TRACK,                  // 0x609 UINT8 u8AbnStopBLTrackSwitchEN
        (UINT16)USER_SWITCH_DEBOUNCE_TH_STOP_BL_TRACK,         // 0x60A UINT16 u16StopBLTrackLeaveDebounce

        (UINT8)USER_SWITCH_ABN_STOP_POINT_TRACK,               // 0x60C UINT8 u8AbnStopPointTrackSwitchEN
        (UINT8)USER_SWITCH_DEBOUNCE_TH_STOP_POINT_TRACK,       // 0x60D UINT8 u8StopPointTrackLeaveDebounce
        (UINT16)USER_SWITCH_ABN_DIFF_TH_STOP_POINT_TRACK,      // 0x60E UINT16 u16StopPointTrackDiffTh

        (UINT8)USER_SWITCH_EMS_PATTERN_JUDGE,                  // 0x610 UINT8 u8EMSJudgeSwitchEN
        (UINT8)USER_SWITCH_TRACK_ENTER_DEBOUNCE_EMS,           // 0x611 UINT8 u8EMSPointEnterDebounce
        (UINT16)USER_SWITCH_DEBOUNCE_TH_EMS_PATTERN,           // 0x612 UINT16 u16EMSJudgeLeaveDebounce
        (UINT16)USER_SWITCH_ABN_DIFF_TH_EMS_PATTERN,           // 0x614 UINT16 u16EMSJudgeMaxDiffTh
        (UINT8)USER_SWITCH_ABN_DIFF_POS_NEG_CNT_RATIO,         // 0x616 UINT8 u8EMSJudgePosNegCntRatio

        (UINT8)0,                                              // 0x617 UINT8 u8Reserved0
    },

    //========================== Reserved0===============================//
    {
        {
            (UINT32)0,                                         // 0x618	UINT32 u32Reserved[0]
            (UINT32)0,                                         // 0x61C	UINT32 u32Reserved[1]
            (UINT32)0,                                         // 0x620	UINT32 u32Reserved[2]
            (UINT32)0,                                         // 0x624	UINT32 u32Reserved[3]
            (UINT32)0,                                         // 0x628	UINT32 u32Reserved[4]
            (UINT32)0,                                         // 0x62C	UINT32 u32Reserved[5]
            (UINT32)0,                                         // 0x630	UINT32 u32Reserved[6]
            (UINT32)0,                                         // 0x634	UINT32 u32Reserved[7]
            (UINT32)0,                                         // 0x638	UINT32 u32Reserved[8]
            (UINT32)0,                                         // 0x63C	UINT32 u32Reserved[9]
            (UINT32)0,                                         // 0x640	UINT32 u32Reserved[10]
            (UINT32)0,                                         // 0x644	UINT32 u32Reserved[11]
            (UINT32)0,                                         // 0x648	UINT32 u32Reserved[12]
            (UINT32)0,                                         // 0x64C	UINT32 u32Reserved[13]
            (UINT32)0,                                         // 0x650	UINT32 u32Reserved[14]
            (UINT32)0,                                         // 0x654	UINT32 u32Reserved[15]
            (UINT32)0,                                         // 0x658	UINT32 u32Reserved[16]
            (UINT32)0,                                         // 0x65C	UINT32 u32Reserved[17]
            (UINT32)0,                                         // 0x660	UINT32 u32Reserved[18]
            (UINT32)0,                                         // 0x664	UINT32 u32Reserved[19]
            (UINT32)0,                                         // 0x668	UINT32 u32Reserved[20]
            (UINT32)0,                                         // 0x66C	UINT32 u32Reserved[21]
            (UINT32)0,                                         // 0x670	UINT32 u32Reserved[22]
        }
    },
    // Reserved: 92 bytes

    //========================== Self Test parameters ===================//
    {
        (UINT16)USER_SELFTEST_OPEN_JUDGE_LOW,                  // 0x674 UINT16 u16SelfTestOpenThreshold_Low
        (UINT16)USER_SELFTEST_OPEN_JUDGE_HIGH,                 // 0x676 UINT16 u16SelfTestOpenThreshold_High
        (UINT16)USER_SELFTEST_SHORT_JUDGE_LOW,                 // 0x678 UINT16 u16SelfTestShortThreshold_Low
        (UINT16)USER_SELFTEST_SHORT_JUDGE_HIGH,                // 0x67A UINT16 u16SelfTestShortThreshold_High
        (UINT8)USER_SWITCH_NORMALRUN_SELF_TEST,                // 0x67C UINT8 u8NormalRunSelfTestSwitchEN
        (UINT8)USER_SELFTEST_JUDGE_EN,                         // 0x67D UINT8 u8SelfTestJudgeEn
    },
};

/*-----------------------------------------------------------------------------*/
/* Debug Variables & Functions Prototype                                       */
/*-----------------------------------------------------------------------------*/

/*-----------------------------------------------------------------------------*/
/* Interface Functions                                                         */
/*-----------------------------------------------------------------------------*/

/*-----------------------------------------------------------------------------*/
/* Module Functions                                                            */
/*-----------------------------------------------------------------------------*/

/*-----------------------------------------------------------------------------*/
/* Local Functions                                                             */
/*-----------------------------------------------------------------------------*/

/*-----------------------------------------------------------------------------*/
/* Debug Functions                                                             */
/*-----------------------------------------------------------------------------*/
