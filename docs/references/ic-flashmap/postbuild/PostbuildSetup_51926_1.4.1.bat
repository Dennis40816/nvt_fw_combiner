@ECHO OFF
@SET/a DEBUG_EN=0
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51926 Search USER_SWITCH_APPLICATION_TYPE
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

set Normal_Ctrlram=Normal_Ctrlram.bin
set DiffDLM=DiffDLM.bin
set MP_Ctrlram=MP_Ctrlram.bin
set VN_Ctrlram=VN_Ctrlram.bin
set NF_Ctrlram=NF_Ctrlram.bin

@ECHO ----------------------------------------------------
@ECHO --- NT51926: %AppType% CHIP(s)
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51926_fw.bin

if %AppType% ==(3)        Goto  :NT51926_Cascade_Postbuild
if %AppType% ==(2)        Goto  :NT51926_Cascade_Postbuild
if %AppType% ==(1)   	    Goto  :NT51926_Single_Postbuild
goto :ERR

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------

:NT51926_Cascade_Postbuild
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin BIN\%Normal_Ctrlram% 0x0 0x22800 11264 BIN\%DiffDLM% 0x0 0x27800 10240 BIN\%MP_Ctrlram% 0x0 0x25400 9216 BIN\%VN_Ctrlram% 0x0 0x315D0 5728 BIN\%NF_Ctrlram% 0x0 0x2C800 11728 output\nt51926_fw.bin 0x22000 0x3B000 2048 output\nt51926_fw.bin 0x0 0x32F50 256
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin output\nt51926_fw.bin 0x0 0x32F50 256
exit /b
goto :EOF

:NT51926_Single_Postbuild
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin BIN\%Normal_Ctrlram% 0x0 0x22800 11264 BIN\%MP_Ctrlram% 0x0 0x25400 9216 BIN\%VN_Ctrlram% 0x0 0x315D0 5728 BIN\%NF_Ctrlram% 0x0 0x2C800 11728 output\nt51926_fw.bin 0x22000 0x3B000 2048 output\nt51926_fw.bin 0x0 0x32F50 256
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin output\nt51926_fw.bin 0x0 0x32F50 256
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








