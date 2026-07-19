@ECHO OFF
@SET/a DEBUG_EN=0
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51930 Search USER_SWITCH_APPLICATION_TYPE
@ECHO ----------------------------------------------------
:Sesrch
set file=..\inc\hal_mmap.h
set "LineOfCascade2Chip="
for /f "delims=:" %%a in ('findstr /n "CASCADE_CHIP_NUM" "%file%"') do (
      if          not defined LineOfCascade2Chip (set "LineOfCascade2Chip=%%a"   
      )          
)  


if  not defined LineOfCascade2Chip  Goto :ERR
if %DEBUG_EN% EQU 1 echo #01_%LineOfCascade2Chip%

set LineNoApp=%LineOfCascade2Chip%
set "LineAppContent="
set/a LineNoApp-=1
for /f "skip=%LineNoApp% delims=" %%a in (%file%)do set  "LineAppContent=%%a"&goto :LineOfAppReadEnd
:LineOfAppReadEnd

if  not defined LineAppContent  Goto :ERR
if %DEBUG_EN% EQU 1 echo #02_%LineAppContent%

for /f "tokens=3 delims= " %%i in ("%LineAppContent%") do (set AppType=%%i)
if %DEBUG_EN% EQU 1 echo #03_%AppType%



@ECHO ----------------------------------------------------
@ECHO --- NT51930 CASCADE_CHIP_NUM : %AppType% 
@ECHO ----------------------------------------------------
if %AppType% ==(1)   	  Goto  :NT51930_Single_Postbuild
if %AppType% ==(2)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(3)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(4)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(5)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(6)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(7)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(8)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(9)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(10)       Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(11)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(12)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(13)        Goto  :NT51930_Cascade_Postbuild
if %AppType% ==(14)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(15)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(16)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(17)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(18)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(19)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(20)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(21)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(22)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(23)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(24)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(25)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(26)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(27)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(28)        Goto  :NT51930_Cascade_extend_Postbuild
if %AppType% ==(29)        Goto  :NT51930_Cascade_extend_Postbuild
goto :ERR


:NT51930_Cascade_extend_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51930_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- CASCADE EXTEND MergeBin + DiffCode CRC
@ECHO ----------------------------------------------------
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\nt51930_fw.bin output\nt51930_fw.bin BIN\NF_Ctrlram.bin 0x0 0x1FC00 6736 BIN\Normal_Ctrlram.bin 0x0 0x21650 11264 BIN\MP_Ctrlram.bin 0x0 0x24250 13312 BIN\VN_Ctrlram.bin 0x0 0x27650 6494 output\nt51930_fw.bin 0x7000 0x28FB0 256 BIN\DiffDLM.bin 0x0 0x2F200 143360

exit /b
goto :EOF

:NT51930_Cascade_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51930_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- CASCADE MergeBin + DiffCode CRC
@ECHO ----------------------------------------------------
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\nt51930_fw.bin output\nt51930_fw.bin BIN\NF_Ctrlram.bin 0x0 0x1FC00 6736 BIN\Normal_Ctrlram.bin 0x0 0x21650 11264 BIN\MP_Ctrlram.bin 0x0 0x24250 13312 BIN\VN_Ctrlram.bin 0x0 0x27650 6494 output\nt51930_fw.bin 0x7000 0x28FB0 256 BIN\DiffDLM.bin 0x0 0x2F200 65024

exit /b
goto :EOF

:NT51930_Single_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51930_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- SINGLE MergeBin
@ECHO ----------------------------------------------------
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\nt51930_fw.bin output\nt51930_fw.bin BIN\NF_Ctrlram.bin 0x0 0x1FC00 6736 BIN\Normal_Ctrlram.bin 0x0 0x21650 11264 BIN\MP_Ctrlram.bin 0x0 0x24250 13312 BIN\VN_Ctrlram.bin 0x0 0x27650 6494 output\nt51930_fw.bin 0x7000 0x28FB0 256
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








