::@ECHO OFF
@SET/a DEBUG_EN=0
::@ECHO OFF 
 
goto :Sesrch

@ECHO ----------------------------------------------------
@ECHO --- NT51927 Search USER_SWITCH_APPLICATION_TYPE
@ECHO ----------------------------------------------------
:Sesrch
set file=..\inc\hal_mmap.h
set "LineOfCascadeChip="
for /f "delims=:" %%a in ('findstr /n "CASCADE_CHIP_NUM" "%file%"') do (
      if          not defined LineOfCascadeChip (set "LineOfCascadeChip=%%a"   
      )          
)  


if  not defined LineOfCascadeChip  Goto :ERR
if %DEBUG_EN% EQU 1 echo #01_%LineOfCascadeChip%

set LineNoApp=%LineOfCascadeChip%
set "LineAppContent="
set/a LineNoApp-=1
for /f "skip=%LineNoApp% delims=" %%a in (%file%)do set  "LineAppContent=%%a"&goto :LineOfAppReadEnd
:LineOfAppReadEnd

if  not defined LineAppContent  Goto :ERR
if %DEBUG_EN% EQU 1 echo #02_%LineAppContent%

for /f "tokens=3 delims= " %%i in ("%LineAppContent%") do (set AppType=%%i)
if %DEBUG_EN% EQU 1 echo #03_%AppType%

set Normal_Ctrlram_M=Normal_Ctrlram.bin
set Normal_Ctrlram_SL=Normal_Ctrlram_L.bin
set Normal_Ctrlram_SR=Normal_Ctrlram_R.bin
set MP_Ctrlram_M=MP_Ctrlram.bin
set MP_Ctrlram_SL=MP_Ctrlram_L.bin
set MP_Ctrlram_SR=MP_Ctrlram_R.bin
set VN_Ctrlram=VN_Ctrlram.bin
set NF_Ctrlram=NF_Ctrlram.bin
set Vector_Ctrlram=Vector_Ctrlram.bin

@ECHO ----------------------------------------------------
@ECHO --- NT51927: %AppType% CHIP(s)
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51927_fw.bin

if %AppType% ==(3)        Goto  :NT51927_3Cascade_Postbuild
if %AppType% ==(2)        Goto  :NT51927_2Cascade_Postbuild
if %AppType% ==(1)   	  Goto  :NT51927_Single_Postbuild
goto :ERR

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------
:NT51927_3Cascade_Postbuild
:: Master
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x16800 16 BIN\%NF_Ctrlram% 0xfd0 0x16810 4032 BIN\%Normal_Ctrlram_M% 0x0 0x177d0 12288 BIN\%MP_Ctrlram_M% 0x0 0x1a7d0 9216 BIN\%VN_Ctrlram% 0x0 0x1cbd0 5728
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x34000 2048
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400

:: SLV_R
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x1f000 36864
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x1f800 16 BIN\%NF_Ctrlram% 0x1f90 0x1f810 4032 BIN\%Normal_Ctrlram_SR% 0x0 0x207d0 12288 BIN\%MP_Ctrlram_SR% 0x0 0x237d0 9216 BIN\%VN_Ctrlram% 0x0 0x25bd0 5728

:: SLV_L
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x28000 36864
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x28800 4048 BIN\%Normal_Ctrlram_SL% 0x0 0x297d0 12288 BIN\%MP_Ctrlram_SL% 0x0 0x2c7d0 9216 BIN\%VN_Ctrlram% 0x0 0x2ebd0 5728


@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x0 0x32DC0 1120
@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x27230 400
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x30230 400

@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@copy output\nt51927_fw.bin   output\FlashMerge\TP_FW

exit /b
goto :EOF

:NT51927_2Cascade_Postbuild
:: Master
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x16800 4048 BIN\%Normal_Ctrlram_M% 0x0 0x177d0 12288 BIN\%MP_Ctrlram_M% 0x0 0x1a7d0 9216 BIN\%VN_Ctrlram% 0x0 0x1cbd0 5728
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x34000 2048
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400
:: SLV_R
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x1f000 36864
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x1f800 16 BIN\%NF_Ctrlram% 0xfd0 0x1f810 4032 BIN\%Normal_Ctrlram_SR% 0x0 0x207d0 12288 BIN\%MP_Ctrlram_SR% 0x0 0x237d0 9216 BIN\%VN_Ctrlram% 0x0 0x25bd0 5728

@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x0 0x32DC0 1120
@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x27230 400

@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@copy output\nt51927_fw.bin   output\FlashMerge\TP_FW

exit /b
goto :EOF

:NT51927_Single_Postbuild
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 BIN\%NF_Ctrlram% 0x0 0x16800 4048 BIN\%Normal_Ctrlram_M% 0x0 0x177d0 12288 BIN\%MP_Ctrlram_M% 0x0 0x1a7d0 9216 BIN\%VN_Ctrlram% 0x0 0x1cbd0 5728 
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x16000 0x34000 2048
@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400


@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x0 0x32DC0 1120
@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@output\Combiner.exe MERGE_MODE output\nt51927_fw.bin output\nt51927_fw.bin 0x0 0x0000 217088 output\nt51927_fw.bin 0x200 0x1E230 400
@output\Combiner.exe NT51927BASED_GEN_CRC_MODE CRC32 output\nt51927_fw.bin output\nt51927_fw.bin

@copy output\nt51927_fw.bin   output\FlashMerge\TP_FW

exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








