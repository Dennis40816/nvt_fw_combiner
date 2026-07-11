#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define DataLength 500 
#define MaxBlockSize 20

typedef enum CrcMethod_t
{
	None, Crc8, Crc32,
}CrcMethod_t;
const char* CRC8_STRING = "CRC8";
const char* CRC32_STRING = "CRC32";


//---------------- Initial Parameters ---------------------
char mystring[DataLength];
FILE* Map_ptr;
int BlockSourceAddress[MaxBlockSize];
int BlockDesAddress[MaxBlockSize];
int BlockLength[MaxBlockSize];
unsigned char* pBlockBuffer[MaxBlockSize];
FILE* pBlockBin[MaxBlockSize]; //FILE** BlockBin =  malloc(sizeof(FILE*) * ((argc-2)/2)); //
unsigned char* pTotalBuffer;
int TotalSize = 0;
int Fw_without_overlay_flag = 1;
int ILM_LimitSize = 0;
int OverlaySize = 0;
CrcMethod_t CrcMethod;
int HeaderSize = -1;

unsigned int CRC8Alg(unsigned int addr, int size, unsigned char* buf)
{
	unsigned char lfsr_c[32];
	unsigned char lfsr_q[32];
	unsigned char data_in[8];
	memset(lfsr_c, 0x1, 32);
	memset(lfsr_q, 0x1, 32);
	int i, j;

	for (i = addr; i <= addr + size; i++)
	{
		unsigned char nowdata = buf[i];
		for (j = 0; j < 8; j++)
		{
			data_in[j] = (nowdata & 0x01);
			nowdata = nowdata >> 1;
		}
		//-------------------------------------------------------
		lfsr_c[0] = lfsr_q[24] ^ lfsr_q[30] ^ data_in[0] ^ data_in[6];
		lfsr_c[1] = lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[6] ^ data_in[7];
		lfsr_c[2] = lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[2] ^ data_in[6] ^ data_in[7];
		lfsr_c[3] = lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[3] ^ data_in[7];
		lfsr_c[4] = lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[30] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[4] ^ data_in[6];
		lfsr_c[5] = lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4] ^ data_in[5] ^ data_in[6] ^ data_in[7];
		lfsr_c[6] = lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5] ^ data_in[6] ^ data_in[7];
		lfsr_c[7] = lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[5] ^ data_in[7];
		lfsr_c[8] = lfsr_q[0] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[28] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4];
		lfsr_c[9] = lfsr_q[1] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[29] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5];
		lfsr_c[10] = lfsr_q[2] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[5];
		lfsr_c[11] = lfsr_q[3] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[28] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4];
		lfsr_c[12] = lfsr_q[4] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[0] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5] ^ data_in[6];
		lfsr_c[13] = lfsr_q[5] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[3] ^ data_in[5] ^ data_in[6] ^ data_in[7];
		lfsr_c[14] = lfsr_q[6] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[2] ^ data_in[3] ^ data_in[4] ^ data_in[6] ^ data_in[7];
		lfsr_c[15] = lfsr_q[7] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[3] ^ data_in[4] ^ data_in[5] ^ data_in[7];
		lfsr_c[16] = lfsr_q[8] ^ lfsr_q[24] ^ lfsr_q[28] ^ lfsr_q[29] ^ data_in[0] ^ data_in[4] ^ data_in[5];
		lfsr_c[17] = lfsr_q[9] ^ lfsr_q[25] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[1] ^ data_in[5] ^ data_in[6];
		lfsr_c[18] = lfsr_q[10] ^ lfsr_q[26] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[2] ^ data_in[6] ^ data_in[7];
		lfsr_c[19] = lfsr_q[11] ^ lfsr_q[27] ^ lfsr_q[31] ^ data_in[3] ^ data_in[7];
		lfsr_c[20] = lfsr_q[12] ^ lfsr_q[28] ^ data_in[4];
		lfsr_c[21] = lfsr_q[13] ^ lfsr_q[29] ^ data_in[5];
		lfsr_c[22] = lfsr_q[14] ^ lfsr_q[24] ^ data_in[0];
		lfsr_c[23] = lfsr_q[15] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[30] ^ data_in[0] ^ data_in[1] ^ data_in[6];
		lfsr_c[24] = lfsr_q[16] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[7];
		lfsr_c[25] = lfsr_q[17] ^ lfsr_q[26] ^ lfsr_q[27] ^ data_in[2] ^ data_in[3];
		lfsr_c[26] = lfsr_q[18] ^ lfsr_q[24] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[30] ^ data_in[0] ^ data_in[3] ^ data_in[4] ^ data_in[6];
		lfsr_c[27] = lfsr_q[19] ^ lfsr_q[25] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[1] ^ data_in[4] ^ data_in[5] ^ data_in[7];
		lfsr_c[28] = lfsr_q[20] ^ lfsr_q[26] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[2] ^ data_in[5] ^ data_in[6];
		lfsr_c[29] = lfsr_q[21] ^ lfsr_q[27] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[3] ^ data_in[6] ^ data_in[7];
		lfsr_c[30] = lfsr_q[22] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[4] ^ data_in[7];
		lfsr_c[31] = lfsr_q[23] ^ lfsr_q[29] ^ data_in[5];
		//----------------------------------------------------------------------------
		memcpy(lfsr_q, lfsr_c, 32);
	}
	unsigned int CRC32 = 0;
	for (i = 0; i < 32; i++)
	{
		CRC32 |= lfsr_c[31 - i];
		if ((31 - i) > 0)
			CRC32 = CRC32 << 1;
	}
	return CRC32;
}
unsigned int CRC32Alg(unsigned int addr, int size, unsigned char* buf)
{
	unsigned char lfsr_c[32];
	unsigned char lfsr_q[32];
	unsigned char data_in[32];

	memset(lfsr_c, 0x1, 32);
	memset(lfsr_q, 0x1, 32);
	unsigned int i, j;
	unsigned int looptime = (size + 3) / 4;
	unsigned char remainder = size % 4;
	unsigned int addroffset = addr;
	unsigned int nowdata;

	for (i = 0; i < looptime; i++)
	{
		nowdata = 0;
		if (i == looptime - 1)//last word
		{
			switch (remainder)
			{
			case 1:
			{
				nowdata = buf[addroffset];
				break;
			}
			case 2:
			{
				nowdata = buf[addroffset] + (buf[addroffset + 1] << 8);
				break;
			}
			case 3:
			{
				nowdata = buf[addroffset] + (buf[addroffset + 1] << 8) + (buf[addroffset + 2] << 16);
				break;
			}
			default://0				
				nowdata = buf[addroffset] + (buf[addroffset + 1] << 8) + (buf[addroffset + 2] << 16) + (buf[addroffset + 3] << 24);
				break;
			}
		}
		else
		{
			nowdata = buf[addroffset] + (buf[addroffset + 1] << 8) + (buf[addroffset + 2] << 16) + (buf[addroffset + 3] << 24);
		}
		//printf("----------------------------------------------------------------\n");
		//printf("%4x   ,  %4x , %d\n", addroffset, nowdata, remainder);
		//printf("----------------------------------------------------------------\n");
		//printf("%4x \n", nowdata);
		for (j = 0; j < 32; j++)
		{
			data_in[j] = ((nowdata >> j) & 0x01);
			//nowdata = nowdata >> 1;
		}
		//-------------------------------------------------------
		lfsr_c[0] = lfsr_q[0] ^ lfsr_q[6] ^ lfsr_q[9] ^ lfsr_q[10] ^ lfsr_q[12] ^ lfsr_q[16] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[6] ^ data_in[9] ^ data_in[10] ^ data_in[12] ^ data_in[16] ^ data_in[24] ^ data_in[25] ^ data_in[26] ^ data_in[28] ^ data_in[29] ^ data_in[30] ^ data_in[31];
		lfsr_c[1] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[9] ^ lfsr_q[11] ^ lfsr_q[12] ^ lfsr_q[13] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[24] ^ lfsr_q[27] ^ lfsr_q[28] ^ data_in[0] ^ data_in[1] ^ data_in[6] ^ data_in[7] ^ data_in[9] ^ data_in[11] ^ data_in[12] ^ data_in[13] ^ data_in[16] ^ data_in[17] ^ data_in[24] ^ data_in[27] ^ data_in[28];
		lfsr_c[2] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[9] ^ lfsr_q[13] ^ lfsr_q[14] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[2] ^ data_in[6] ^ data_in[7] ^ data_in[8] ^ data_in[9] ^ data_in[13] ^ data_in[14] ^ data_in[16] ^ data_in[17] ^ data_in[18] ^ data_in[24] ^ data_in[26] ^ data_in[30] ^ data_in[31];
		lfsr_c[3] = lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[9] ^ lfsr_q[10] ^ lfsr_q[14] ^ lfsr_q[15] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[3] ^ data_in[7] ^ data_in[8] ^ data_in[9] ^ data_in[10] ^ data_in[14] ^ data_in[15] ^ data_in[17] ^ data_in[18] ^ data_in[19] ^ data_in[25] ^ data_in[27] ^ data_in[31];
		lfsr_c[4] = lfsr_q[0] ^ lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[6] ^ lfsr_q[8] ^ lfsr_q[11] ^ lfsr_q[12] ^ lfsr_q[15] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[4] ^ data_in[6] ^ data_in[8] ^ data_in[11] ^ data_in[12] ^ data_in[15] ^ data_in[18] ^ data_in[19] ^ data_in[20] ^ data_in[24] ^ data_in[25] ^ data_in[29] ^ data_in[30] ^ data_in[31];
		lfsr_c[5] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[10] ^ lfsr_q[13] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[24] ^ lfsr_q[28] ^ lfsr_q[29] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4] ^ data_in[5] ^ data_in[6] ^ data_in[7] ^ data_in[10] ^ data_in[13] ^ data_in[19] ^ data_in[20] ^ data_in[21] ^ data_in[24] ^ data_in[28] ^ data_in[29];
		lfsr_c[6] = lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[11] ^ lfsr_q[14] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[25] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5] ^ data_in[6] ^ data_in[7] ^ data_in[8] ^ data_in[11] ^ data_in[14] ^ data_in[20] ^ data_in[21] ^ data_in[22] ^ data_in[25] ^ data_in[29] ^ data_in[30];
		lfsr_c[7] = lfsr_q[0] ^ lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[5] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[10] ^ lfsr_q[15] ^ lfsr_q[16] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[28] ^ lfsr_q[29] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[5] ^ data_in[7] ^ data_in[8] ^ data_in[10] ^ data_in[15] ^ data_in[16] ^ data_in[21] ^ data_in[22] ^ data_in[23] ^ data_in[24] ^ data_in[25] ^ data_in[28] ^ data_in[29];
		lfsr_c[8] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[8] ^ lfsr_q[10] ^ lfsr_q[11] ^ lfsr_q[12] ^ lfsr_q[17] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4] ^ data_in[8] ^ data_in[10] ^ data_in[11] ^ data_in[12] ^ data_in[17] ^ data_in[22] ^ data_in[23] ^ data_in[28] ^ data_in[31];
		lfsr_c[9] = lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[9] ^ lfsr_q[11] ^ lfsr_q[12] ^ lfsr_q[13] ^ lfsr_q[18] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[29] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5] ^ data_in[9] ^ data_in[11] ^ data_in[12] ^ data_in[13] ^ data_in[18] ^ data_in[23] ^ data_in[24] ^ data_in[29];
		lfsr_c[10] = lfsr_q[0] ^ lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[5] ^ lfsr_q[9] ^ lfsr_q[13] ^ lfsr_q[14] ^ lfsr_q[16] ^ lfsr_q[19] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[0] ^ data_in[2] ^ data_in[3] ^ data_in[5] ^ data_in[9] ^ data_in[13] ^ data_in[14] ^ data_in[16] ^ data_in[19] ^ data_in[26] ^ data_in[28] ^ data_in[29] ^ data_in[31];
		lfsr_c[11] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[9] ^ lfsr_q[12] ^ lfsr_q[14] ^ lfsr_q[15] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[20] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[3] ^ data_in[4] ^ data_in[9] ^ data_in[12] ^ data_in[14] ^ data_in[15] ^ data_in[16] ^ data_in[17] ^ data_in[20] ^ data_in[24] ^ data_in[25] ^ data_in[26] ^ data_in[27] ^ data_in[28] ^ data_in[31];
		lfsr_c[12] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[9] ^ lfsr_q[12] ^ lfsr_q[13] ^ lfsr_q[15] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[21] ^ lfsr_q[24] ^ lfsr_q[27] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[2] ^ data_in[4] ^ data_in[5] ^ data_in[6] ^ data_in[9] ^ data_in[12] ^ data_in[13] ^ data_in[15] ^ data_in[17] ^ data_in[18] ^ data_in[21] ^ data_in[24] ^ data_in[27] ^ data_in[30] ^ data_in[31];
		lfsr_c[13] = lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[10] ^ lfsr_q[13] ^ lfsr_q[14] ^ lfsr_q[16] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[22] ^ lfsr_q[25] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[1] ^ data_in[2] ^ data_in[3] ^ data_in[5] ^ data_in[6] ^ data_in[7] ^ data_in[10] ^ data_in[13] ^ data_in[14] ^ data_in[16] ^ data_in[18] ^ data_in[19] ^ data_in[22] ^ data_in[25] ^ data_in[28] ^ data_in[31];
		lfsr_c[14] = lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[11] ^ lfsr_q[14] ^ lfsr_q[15] ^ lfsr_q[17] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[23] ^ lfsr_q[26] ^ lfsr_q[29] ^ data_in[2] ^ data_in[3] ^ data_in[4] ^ data_in[6] ^ data_in[7] ^ data_in[8] ^ data_in[11] ^ data_in[14] ^ data_in[15] ^ data_in[17] ^ data_in[19] ^ data_in[20] ^ data_in[23] ^ data_in[26] ^ data_in[29];
		lfsr_c[15] = lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[9] ^ lfsr_q[12] ^ lfsr_q[15] ^ lfsr_q[16] ^ lfsr_q[18] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[24] ^ lfsr_q[27] ^ lfsr_q[30] ^ data_in[3] ^ data_in[4] ^ data_in[5] ^ data_in[7] ^ data_in[8] ^ data_in[9] ^ data_in[12] ^ data_in[15] ^ data_in[16] ^ data_in[18] ^ data_in[20] ^ data_in[21] ^ data_in[24] ^ data_in[27] ^ data_in[30];
		lfsr_c[16] = lfsr_q[0] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[8] ^ lfsr_q[12] ^ lfsr_q[13] ^ lfsr_q[17] ^ lfsr_q[19] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[0] ^ data_in[4] ^ data_in[5] ^ data_in[8] ^ data_in[12] ^ data_in[13] ^ data_in[17] ^ data_in[19] ^ data_in[21] ^ data_in[22] ^ data_in[24] ^ data_in[26] ^ data_in[29] ^ data_in[30];
		lfsr_c[17] = lfsr_q[1] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[9] ^ lfsr_q[13] ^ lfsr_q[14] ^ lfsr_q[18] ^ lfsr_q[20] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[1] ^ data_in[5] ^ data_in[6] ^ data_in[9] ^ data_in[13] ^ data_in[14] ^ data_in[18] ^ data_in[20] ^ data_in[22] ^ data_in[23] ^ data_in[25] ^ data_in[27] ^ data_in[30] ^ data_in[31];
		lfsr_c[18] = lfsr_q[2] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[10] ^ lfsr_q[14] ^ lfsr_q[15] ^ lfsr_q[19] ^ lfsr_q[21] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[2] ^ data_in[6] ^ data_in[7] ^ data_in[10] ^ data_in[14] ^ data_in[15] ^ data_in[19] ^ data_in[21] ^ data_in[23] ^ data_in[24] ^ data_in[26] ^ data_in[28] ^ data_in[31];
		lfsr_c[19] = lfsr_q[3] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[11] ^ lfsr_q[15] ^ lfsr_q[16] ^ lfsr_q[20] ^ lfsr_q[22] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[29] ^ data_in[3] ^ data_in[7] ^ data_in[8] ^ data_in[11] ^ data_in[15] ^ data_in[16] ^ data_in[20] ^ data_in[22] ^ data_in[24] ^ data_in[25] ^ data_in[27] ^ data_in[29];
		lfsr_c[20] = lfsr_q[4] ^ lfsr_q[8] ^ lfsr_q[9] ^ lfsr_q[12] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[21] ^ lfsr_q[23] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[30] ^ data_in[4] ^ data_in[8] ^ data_in[9] ^ data_in[12] ^ data_in[16] ^ data_in[17] ^ data_in[21] ^ data_in[23] ^ data_in[25] ^ data_in[26] ^ data_in[28] ^ data_in[30];
		lfsr_c[21] = lfsr_q[5] ^ lfsr_q[9] ^ lfsr_q[10] ^ lfsr_q[13] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[22] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[5] ^ data_in[9] ^ data_in[10] ^ data_in[13] ^ data_in[17] ^ data_in[18] ^ data_in[22] ^ data_in[24] ^ data_in[26] ^ data_in[27] ^ data_in[29] ^ data_in[31];
		lfsr_c[22] = lfsr_q[0] ^ lfsr_q[9] ^ lfsr_q[11] ^ lfsr_q[12] ^ lfsr_q[14] ^ lfsr_q[16] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[0] ^ data_in[9] ^ data_in[11] ^ data_in[12] ^ data_in[14] ^ data_in[16] ^ data_in[18] ^ data_in[19] ^ data_in[23] ^ data_in[24] ^ data_in[26] ^ data_in[27] ^ data_in[29] ^ data_in[31];
		lfsr_c[23] = lfsr_q[0] ^ lfsr_q[1] ^ lfsr_q[6] ^ lfsr_q[9] ^ lfsr_q[13] ^ lfsr_q[15] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[0] ^ data_in[1] ^ data_in[6] ^ data_in[9] ^ data_in[13] ^ data_in[15] ^ data_in[16] ^ data_in[17] ^ data_in[19] ^ data_in[20] ^ data_in[26] ^ data_in[27] ^ data_in[29] ^ data_in[31];
		lfsr_c[24] = lfsr_q[1] ^ lfsr_q[2] ^ lfsr_q[7] ^ lfsr_q[10] ^ lfsr_q[14] ^ lfsr_q[16] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[30] ^ data_in[1] ^ data_in[2] ^ data_in[7] ^ data_in[10] ^ data_in[14] ^ data_in[16] ^ data_in[17] ^ data_in[18] ^ data_in[20] ^ data_in[21] ^ data_in[27] ^ data_in[28] ^ data_in[30];
		lfsr_c[25] = lfsr_q[2] ^ lfsr_q[3] ^ lfsr_q[8] ^ lfsr_q[11] ^ lfsr_q[15] ^ lfsr_q[17] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[2] ^ data_in[3] ^ data_in[8] ^ data_in[11] ^ data_in[15] ^ data_in[17] ^ data_in[18] ^ data_in[19] ^ data_in[21] ^ data_in[22] ^ data_in[28] ^ data_in[29] ^ data_in[31];
		lfsr_c[26] = lfsr_q[0] ^ lfsr_q[3] ^ lfsr_q[4] ^ lfsr_q[6] ^ lfsr_q[10] ^ lfsr_q[18] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[28] ^ lfsr_q[31] ^ data_in[0] ^ data_in[3] ^ data_in[4] ^ data_in[6] ^ data_in[10] ^ data_in[18] ^ data_in[19] ^ data_in[20] ^ data_in[22] ^ data_in[23] ^ data_in[24] ^ data_in[25] ^ data_in[26] ^ data_in[28] ^ data_in[31];
		lfsr_c[27] = lfsr_q[1] ^ lfsr_q[4] ^ lfsr_q[5] ^ lfsr_q[7] ^ lfsr_q[11] ^ lfsr_q[19] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[29] ^ data_in[1] ^ data_in[4] ^ data_in[5] ^ data_in[7] ^ data_in[11] ^ data_in[19] ^ data_in[20] ^ data_in[21] ^ data_in[23] ^ data_in[24] ^ data_in[25] ^ data_in[26] ^ data_in[27] ^ data_in[29];
		lfsr_c[28] = lfsr_q[2] ^ lfsr_q[5] ^ lfsr_q[6] ^ lfsr_q[8] ^ lfsr_q[12] ^ lfsr_q[20] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[30] ^ data_in[2] ^ data_in[5] ^ data_in[6] ^ data_in[8] ^ data_in[12] ^ data_in[20] ^ data_in[21] ^ data_in[22] ^ data_in[24] ^ data_in[25] ^ data_in[26] ^ data_in[27] ^ data_in[28] ^ data_in[30];
		lfsr_c[29] = lfsr_q[3] ^ lfsr_q[6] ^ lfsr_q[7] ^ lfsr_q[9] ^ lfsr_q[13] ^ lfsr_q[21] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[25] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[31] ^ data_in[3] ^ data_in[6] ^ data_in[7] ^ data_in[9] ^ data_in[13] ^ data_in[21] ^ data_in[22] ^ data_in[23] ^ data_in[25] ^ data_in[26] ^ data_in[27] ^ data_in[28] ^ data_in[29] ^ data_in[31];
		lfsr_c[30] = lfsr_q[4] ^ lfsr_q[7] ^ lfsr_q[8] ^ lfsr_q[10] ^ lfsr_q[14] ^ lfsr_q[22] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[26] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ data_in[4] ^ data_in[7] ^ data_in[8] ^ data_in[10] ^ data_in[14] ^ data_in[22] ^ data_in[23] ^ data_in[24] ^ data_in[26] ^ data_in[27] ^ data_in[28] ^ data_in[29] ^ data_in[30];
		lfsr_c[31] = lfsr_q[5] ^ lfsr_q[8] ^ lfsr_q[9] ^ lfsr_q[11] ^ lfsr_q[15] ^ lfsr_q[23] ^ lfsr_q[24] ^ lfsr_q[25] ^ lfsr_q[27] ^ lfsr_q[28] ^ lfsr_q[29] ^ lfsr_q[30] ^ lfsr_q[31] ^ data_in[5] ^ data_in[8] ^ data_in[9] ^ data_in[11] ^ data_in[15] ^ data_in[23] ^ data_in[24] ^ data_in[25] ^ data_in[27] ^ data_in[28] ^ data_in[29] ^ data_in[30] ^ data_in[31];
		//----------------------------------------------------------------------------
		memcpy(lfsr_q, lfsr_c, 32);
		addroffset += 4;
	}
	unsigned int CRC32 = 0;
	for (i = 0; i < 32; i++)
	{
		CRC32 |= lfsr_c[31 - i];
		if ((31 - i) > 0)
			CRC32 = CRC32 << 1;
	}
	return CRC32;
}
unsigned int CalCrc(unsigned int addr, int size, unsigned char* pBuf)
{
	if (CrcMethod == Crc8)
	{
		return CRC8Alg(addr, size, pBuf);
	}
	else if (CrcMethod == Crc32)
	{
		if (size == 0)//0 , no need to calculate
			return 0;
		else
			return CRC32Alg(addr, size + 1, pBuf);
	}
	else
	{
		return 0;
	}
}
void CalculateOverlayCRC(unsigned char* pBuf, unsigned int dlmStartAddrIdx)
{
	int DataStartAddr = *(int*)(pBuf + dlmStartAddrIdx);
	int i, Size;
	unsigned int StartAddress;
	unsigned int crc32 = 0;
	printf("--------------------------------------\n");
	printf("OverlayAddr  Size   CRC\n");
	int OverlayCRCIsZero = *(int*)(pBuf + DataStartAddr + 12);
	if (OverlayCRCIsZero == 0)
	{
		for (i = 0; i < OverlaySize; i++)
		{
			Size = *(int*)(pBuf + DataStartAddr + 4 + i * 16) - 1;
			StartAddress = *(int*)(pBuf + DataStartAddr + 8 + i * 16);
			crc32 = CalCrc(StartAddress, Size, pBuf);

			*(unsigned int*)(pBuf + DataStartAddr + 12 + i * 16) = crc32;
			*(unsigned int*)(pBuf + DataStartAddr + 4 + i * 16) = Size;
			//printf("Overlay%d CRC is %x\n",i,crc32) ; 
			printf("%4x        %4x    %4x\n", StartAddress, Size, crc32);
		}
	}
}
void Cal_ILM0_DLM0_CRC(unsigned char* pBuf, unsigned int offset)
{
	unsigned int StartAddress;
	int Size;
	unsigned int crc32;
	printf("--------------------------------------\n");
	//char* pBuf = _pBuf;// +offset;
	//ILM0  
	StartAddress = *(int*)(pBuf + 0 + offset);
	Size = *(int*)(pBuf + 8 + offset);
	crc32 = CalCrc(StartAddress, Size, pBuf);
	*(unsigned int*)(pBuf + 24 + offset) = crc32;
	printf("ILM0 CRC :%4x\n", crc32);
	//DLM0
	StartAddress = *(int*)(pBuf + 12 + offset);
	Size = *(int*)(pBuf + 20 + offset);
	crc32 = CalCrc(StartAddress, Size, pBuf);
	*(unsigned int*)(pBuf + 28 + offset) = crc32;
	printf("DLM0 CRC :%4x\n", crc32);
}
void CalculateDLMCRC(unsigned char* pBuf, unsigned int offset, unsigned int HeadSize)
{

	int Size;
	unsigned int StartAddress;
	int offset_L = 0x30 + offset;
	int offset_H = 0x38 + offset;
	//int DLMLoopIdx = 0;
	unsigned int HeadAddr = offset + HeadSize - 16;
	unsigned int crc32 = 0;
	//char* pBuf = _pBuf + offset;
	printf("--------------------------------------\n");
	printf("DLM_BinAddr  Size   CRC\n");
	while (1)
	{
		//long long test = *(long long*)(pBuf+offset_L);
		if ((*(long long*)(pBuf + offset_L) == 0 && *(long long*)(pBuf + offset_H) == 0) || offset_L == HeadAddr)
		{
			//  DLMLoopLoop--;		
			break;
		}
		Size = *(int*)(pBuf + offset_L + 4);
		StartAddress = *(int*)(pBuf + offset_H);
		unsigned int crc32 = CalCrc(StartAddress, Size, pBuf);
		*(unsigned int*)(pBuf + offset_L + 12) = crc32;
		printf("%4x        %4x    %4x\n", StartAddress, Size, crc32);
		offset_L += 16;
		offset_H += 16;
	}
	printf("--------------------------------------\n");
	printf("HeaderBinAddr  Size   CRC\n");
	//Header CRC	
	StartAddress = *(int*)(pBuf + offset + HeadSize - 8);
	Size = *(int*)(pBuf + offset + HeadSize - 12);
	if (Size == 0)
	{
		printf("size of header is ZERO  , bypass the Header CRC \n");
	}
	else
	{
		Size -= 4;
		crc32 = CalCrc(StartAddress, Size, pBuf);
		*(unsigned int*)(pBuf + offset + HeadSize - 4) = crc32;
		printf("%5x           %4x   %8x\n", StartAddress, Size, crc32);
	}

}
void strchrn(char* dest, const char* src, char b, char e)
{
	char* m = strchr(src, b) + 1;
	char* n = strchr(src, e);
	int len = strlen(m) - strlen(n);

	strncpy(dest, m, len);
	dest[len] = '\0';
}


int OpenMapTxt(const char* fwFilePath)
{
	int idx = strlen(fwFilePath);

	while (idx > 0)
	{
		if (fwFilePath[idx] == '/' || fwFilePath[idx] == '\\')
			break;
		--idx;
	}
	char* fwDir = (char*)malloc(sizeof(char) * (idx + 2));
	memcpy(fwDir, fwFilePath, idx + 1);
	if (idx == 0)
		fwDir[0] = '\0';
	else
		fwDir[idx + 1] = '\0';

	int fwDirLen = strlen(fwDir);
	char* mapFilePath = malloc(sizeof(char) * (idx + 2 + 20));

	strncpy(mapFilePath, fwDir, fwDirLen);
	memcpy(&mapFilePath[fwDirLen], "map.txt\0", 8);

	Map_ptr = fopen(mapFilePath, "r");
	if (Map_ptr == NULL)
	{
		printf("Open map.txt at \"%s\" failed.\n", mapFilePath);
		memcpy(&mapFilePath[fwDirLen], "output\\map.txt\0", 15);
		Map_ptr = fopen(mapFilePath, "r");
		if (Map_ptr == NULL)
		{
			printf("Open map.txt at \"%s\" failed.\n", mapFilePath);
			return 1;
		}
	}
	printf("Success to open map.txt at \"%s\".\n", mapFilePath);
	printf("--------------------------------------\n");

	return 0;
}
int GetIlmLimitSize(FILE* Map_ptr)
{
	const char* KEYWORD = "?TEXT_SIZE:";
	while (fgets(mystring, DataLength, Map_ptr))
	{
		if (strstr(mystring, KEYWORD) != NULL)
		{
			char* tmpStr = strstr(mystring, KEYWORD);
			tmpStr = strstr(tmpStr, "0x");
			if (tmpStr == NULL)
			{
				printf("Can't find the first hex value when decode IlmLimitSize, please check the format of map.txt: \n%s\n", mystring);
				return -1;
			}
			tmpStr = strstr(tmpStr + 1, "0x");
			if (tmpStr == NULL)
			{
				printf("Can't find the second hex value when decode IlmLimitSize, please check the format of map.txt: \n%s\n", mystring);
				return -1;
			}

			char ILMsize_tmp[8];
			strncpy(ILMsize_tmp, tmpStr, 8);
			strchrn(ILMsize_tmp, ILMsize_tmp, 'x', ')');
			ILM_LimitSize = strtol(ILMsize_tmp, NULL, 16);
			printf("ILM_LimitSize = 0x%x\n", ILM_LimitSize);
			break;

			return 0;
		}
	}
	return 0;
}
int DecodeOneLineOverlayInfo(const char* mystring, char** expectedString)
{
	*expectedString = strstr(mystring, "0x");
	if (*expectedString == NULL)
	{
		printf("Can't find the first hex value when decode overlay info, please check the format of map.txt: \n%s\n", mystring);
		return -1;
	}
	*expectedString = strstr(*expectedString + 1, "0x");
	if (*expectedString == NULL)
	{
		printf("Can't find the second hex value when decode overlay info, please check the format of map.txt: \n%s\n", mystring);
		return -1;
	}
	*expectedString = strstr(*expectedString + 1, "0x");
	if (*expectedString == NULL)
	{
		printf("Can't find the third hex value when decode overlay info, please check the format of map.txt: \n%s\n", mystring);
		return -1;
	}

	return 0;
}
int CheckFwOverley(FILE* Map_ptr)
{
	char* expectedString = NULL;
	rewind(Map_ptr);
	while (fgets(mystring, DataLength, Map_ptr))
	{
		if (strstr(mystring, "_ovly_table =") != NULL)
		{
			char size_tmp[16];
			int ramBaseAddr = 0;
			int Section_size = 0;
			int OverLimitFlag = 0;

			do
			{
				// ILM BaseAddr                         
				fgets(mystring, DataLength, Map_ptr);
				if (strstr(mystring, "_novlys = ") != NULL)
					break;
				if (DecodeOneLineOverlayInfo(mystring, &expectedString) != 0)
				{
					return -1;
				}
				strncpy(size_tmp, expectedString, 8);
				strchrn(size_tmp, size_tmp, 'x', ' ');
				ramBaseAddr = strtol(size_tmp, NULL, 16);
				printf("RAM_BaseAddr  = %x\n", ramBaseAddr);

				// Section size
				fgets(mystring, DataLength, Map_ptr);
				if (DecodeOneLineOverlayInfo(mystring, &expectedString) != 0)
				{
					return -1;
				}
				strncpy(size_tmp, expectedString, 8);
				strchrn(size_tmp, size_tmp, 'x', ' ');
				Section_size = strtol(size_tmp, NULL, 16);
				printf("section size  = %x\n", Section_size);

				// Judge
				// MD_TL2-4408: 有部份 IC 會做 DLM overlay，不包含在 ILM overlay 檢查範圍內，需跳過
				if (ramBaseAddr >= ILM_LimitSize)
				{
					printf("RAM_BaseAddr(0x%x) >= ILM_LimitSize(0x%x) ==> skip checking this overlay section.\n", ramBaseAddr, ILM_LimitSize);
				}
				else
				{
					printf("RAM_BaseAddr + Section_size = %x\n", ramBaseAddr + Section_size);
					if ((ramBaseAddr + Section_size) >= ILM_LimitSize)
					{
						OverLimitFlag = 1;
					}
				}
				fgets(mystring, DataLength, Map_ptr);
				fgets(mystring, DataLength, Map_ptr);
				OverlaySize++;
			} while (1);

			if (OverLimitFlag)
			{
				printf("FW size overflow\n");
				return -1;
			}
			printf("FW pass overlay check.\n");
			Fw_without_overlay_flag = 0;
			break;
		}
	}
	if (Fw_without_overlay_flag)
	{
		printf("This FW has no overlay.\n");
	}
	return 0;
}
int ReadOtherBinsToGlobalBuffer(int argc, char* argv[], int* Max_Block_Address)
{
	int i = 0;
	*Max_Block_Address = 0;
	int RealBlockSize = 0;
	printf("--------------------------------------\n");
	printf("Start to read other bins.\n");
	printf("Source\t\tDest\t\tLength\t\tFileName\n");
	for (i = 0; i < argc / 4; i++)
	{
		pBlockBin[i] = fopen(argv[i * 4], "rb");
		if (pBlockBin[i] == NULL)
		{
			printf("file open failure: %s\n", argv[i * 4]);
			return 1;
		}
		fseek(pBlockBin[i], 0, SEEK_END);
		RealBlockSize = ftell(pBlockBin[i]);
		rewind(pBlockBin[i]);


		/*BlockSourceAddress[i] = 0;
		BlockDesAddress[i] = 0x14800;
		BlockLength[i] = 5000;*/

		BlockSourceAddress[i] = strtol(argv[i * 4 + 1], NULL, 16);
		BlockDesAddress[i] = strtol(argv[i * 4 + 2], NULL, 16);
		BlockLength[i] = strtol(argv[i * 4 + 3], NULL, 10);

		BlockLength[i] = (RealBlockSize < BlockLength[i]) ? RealBlockSize : BlockLength[i];
		*Max_Block_Address = (BlockLength[i] + BlockDesAddress[i] > *Max_Block_Address) ? BlockLength[i] + BlockDesAddress[i] : *Max_Block_Address;

		pBlockBuffer[i] = malloc(BlockLength[i] * sizeof(char));
		fseek(pBlockBin[i], BlockSourceAddress[i], SEEK_SET);
		fread(pBlockBuffer[i], BlockLength[i], 1, pBlockBin[i]);
		fclose(pBlockBin[i]);
		// printf( "BlockSourceAddress: %x\n", BlockSourceAddress[i] );
		// printf( "BlockDesAddress: %x\n", BlockDesAddress[i] ); 
		// printf( "BlockLength: %d\n", BlockLength[i] ); 
		// printf( "pBlockBuffer: %x\n", pBlockBuffer[i] );      

		printf("0x%X\t\t0x%X\t\t%d\t\t%s\n", BlockSourceAddress[i], BlockDesAddress[i], BlockLength[i], argv[i * 4]);
	}
	return 0;
}
int MergeOtherBinAndFwBin(FILE* fs_FWCode, int argc, char* argv[])
{
	int i;
	int Max_Block_Address = 0;
	if (ReadOtherBinsToGlobalBuffer(argc, argv, &Max_Block_Address) != 0)
	{
		printf("Read other bins to GlobalBuffer fail.\n");
		return -1;
	}

	fseek(fs_FWCode, 0, SEEK_END);
	int FWCode_Size = ftell(fs_FWCode);
	rewind(fs_FWCode);
	TotalSize = 0;
	TotalSize = (FWCode_Size > Max_Block_Address) ? FWCode_Size : Max_Block_Address;

	pTotalBuffer = malloc(TotalSize * sizeof(char));
	fread(pTotalBuffer, FWCode_Size, 1, fs_FWCode);
	fclose(fs_FWCode);
	for (i = 0; i < argc / 4; i++)
	{
		memcpy(pTotalBuffer + BlockDesAddress[i], pBlockBuffer[i], BlockLength[i]);
	}

	return 0;
}
void DecodeOverlayInfoAndWriteToFwBin(unsigned int ovInfoIdx)
{
	// write OverlaySize
	*(pTotalBuffer + ovInfoIdx) = (*(pTotalBuffer + ovInfoIdx) & 0xF0) | OverlaySize;
	char OverlayInfo = *(pTotalBuffer + ovInfoIdx);
	// extensionflag 
	if ((OverlayInfo >> 4 & 0x1) == 1)//HostDL/Process              
	{
		rewind(Map_ptr);
		int i = 1;
		char addr_tmp[10];
		int DLM_DataStartAddr = *(int*)(pTotalBuffer + 12);
		int OverlayDLMaddr = 0;
		while (fgets(mystring, DataLength, Map_ptr))
		{
			// MD_TL2-4345: 此關鍵字於舊 IC、N25 的 map.txt 都找尋不到，所以無法知道如何修改抓取 mystring 的邏輯
			// 而這段看起來也只是解析出 map.txt 的資訊並顯示在 console，應該不影響產出的結果，故先不處理
			if (strstr(mystring, "OverlayDLMaddr") != NULL)
			{
				fgets(mystring, DataLength, Map_ptr);
				strncpy(addr_tmp, &mystring[18], 8);
				OverlayDLMaddr = strtol(addr_tmp, NULL, 16);
				*(int*)(pTotalBuffer + DLM_DataStartAddr + i * 16) = OverlayDLMaddr;
				fgets(mystring, DataLength, Map_ptr);
				fgets(mystring, DataLength, Map_ptr);
				printf("OverlayDLMaddr%d : %x\n", i, OverlayDLMaddr);
				i++;
			}
		}
	}
}
int DecodeOneHeaderSize(unsigned char* pBuf)
{
	printf("--------------------------------------\n");
	int maxHeaderSize = *(int*)(pBuf + 0);	// 拿 ILM_start_addr 當作 header 的最大範圍
	for (int offset = 0x30; offset < maxHeaderSize; offset += 0x10)
	{
		int len = *(int*)(pBuf + offset + 4);
		int binAddr = *(int*)(pBuf + offset + 8);

		if (binAddr == 0 && len != 0)
		{
			HeaderSize = len + 1;
			printf("One HeaderSize: %d-byte.\n", HeaderSize);
			return 0;
		}
	}

	printf("Can't decode HeaderSize. No section with binAddr == 0x00000000 and len != 0x00000000.");
	return -1;
}

/* Normal mode:
 * argv[0]: exe
 * argv[1]: CRC_Enable/CRC32_Enable/CRC_Disable
 * argv[2]: FW_bin
 * argv[3]: block1_bin
 * argv[4]: block1_source_address
 * argv[5]: block1_destination_address
 * argv[6]: block1_length
 * argv[7]: block2_bin
 * argv[8]: block2_source_address
 * argv[9]: block2_destination_address
 * argv[10]: block2_length
 */
int NormalMode(int argc, char* argv[])
{
	//------------------ Open Map.txt -------------------------
	char* fwFilePath = argv[2];
	printf("FwFilePath: %s\n", fwFilePath);
	if (OpenMapTxt(fwFilePath) != 0)
		return -1;

	if (GetIlmLimitSize(Map_ptr) != 0)
		return -1;
	if (CheckFwOverley(Map_ptr) != 0)
		return -1;

	//-----------------------------------------------------------------------------------------
	int argc_is_even_flag = argc & 1; // make sure  length of argc is even.        
	if (argc < 7 || !argc_is_even_flag)
	{
		printf("Parameter format is error. FW Merge is FAIL\n");
		return -1;
	}
	printf("Start to Merge...\n");

	FILE* fs_FWCode;

	fs_FWCode = fopen(argv[2], "rb");
	if (fs_FWCode == NULL)
	{
		printf("Open FW file '%s' failure\n", argv[2]);
		return 1;
	}

	if (MergeOtherBinAndFwBin(fs_FWCode, argc - 3, (argv + 3)) != 0)
		return -1;

	if (strcmp(argv[1], "CRC_Enable") == 0)
		CrcMethod = Crc8;
	else if (strcmp(argv[1], "CRC32_Enable") == 0)
		CrcMethod = Crc32;
	else
		CrcMethod = None;

	if (CrcMethod == Crc8 || CrcMethod == Crc32)
	{
		DecodeOverlayInfoAndWriteToFwBin(0x28);
		CalculateOverlayCRC(pTotalBuffer, 12);

		if (DecodeOneHeaderSize(pTotalBuffer) != 0)
			return -1;

		char IsCascadeIC = (*(pTotalBuffer + 0x20) >> 1) & 0x1;
		if (IsCascadeIC == 1)
		{
			Cal_ILM0_DLM0_CRC(pTotalBuffer, 0);
			CalculateDLMCRC(pTotalBuffer, 0, HeaderSize);

			Cal_ILM0_DLM0_CRC(pTotalBuffer, HeaderSize);
			CalculateDLMCRC(pTotalBuffer, HeaderSize, HeaderSize);
		}
		else
		{
			Cal_ILM0_DLM0_CRC(pTotalBuffer, 0);
			CalculateDLMCRC(pTotalBuffer, 0, HeaderSize);
		}
	}
	else
	{
		printf("CRC Disable...\n");
	}
	fclose(Map_ptr);
	FILE* savedata = fopen(argv[2], "wb");
	fwrite(pTotalBuffer, TotalSize, 1, savedata);
	free(pTotalBuffer);
	fclose(savedata);
	printf("FW Merge is OK\n");
	return 0;
}

int main(int argc, char* argv[])
{
	printf("--------------------------------------\n");
	printf("Combiner version:1.6.0.1\n");

	if (strcmp(argv[1], "CRC_Enable") == 0 ||
		strcmp(argv[1], "CRC32_Enable") == 0 ||
		strcmp(argv[1], "CRC_Disable") == 0)
	{
		return NormalMode(argc, argv);
	}
	else
	{
		printf("invalid argument: %s\n", argv[1]);
		return -1;
	}
}





