@ECHO OFF
@SET/a DEBUG_EN=0
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51931 Search USER_SWITCH_APPLICATION_TYPE
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
@ECHO --- NT51931 CASCADE_CHIP_NUM : %AppType% 
@ECHO ----------------------------------------------------
if %AppType% ==(0)   	  Goto  :NT51931_Single_Postbuild
if %AppType% ==(1)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(2)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(3)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(4)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(5)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(6)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(7)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(8)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(9)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(10)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(11)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(12)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(13)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(14)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(15)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(16)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(17)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(18)        Goto  :NT51931_Cascade_Postbuild
if %AppType% ==(19)        Goto  :NT51931_Cascade_Postbuild
goto :ERR


:NT51931_Cascade_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51931_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- MergeBin + DiffCode CRC
@ECHO ----------------------------------------------------
@output\Combiner.exe NT51931BASED_NORMAL_MODE CRC8 output\nt51931_fw.bin output\nt51931_fw.bin BIN\NF_Ctrlram.bin 0x0 0x16800 4048 BIN\Normal_Ctrlram.bin 0x0 0x177D0 10240 BIN\MP_Ctrlram.bin 0x0 0x19FD0 9216 BIN\VN_Ctrlram.bin 0x0 0x1C3D0 5728 output\nt51931_fw.bin 0x0 0x1DA30 256 output\nt51931_fw.bin 0x16000 0x3B000 2048  BIN\DiffDLM.bin 0x0 0x22800 51200

exit /b
goto :EOF

:NT51931_Single_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51931_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------
@output\Combiner.exe NT51931BASED_NORMAL_MODE CRC8 output\nt51931_fw.bin output\nt51931_fw.bin BIN\NF_Ctrlram.bin 0x0 0x16800 4048 BIN\Normal_Ctrlram.bin 0x0 0x177D0 10240 BIN\MP_Ctrlram.bin 0x0 0x19FD0 9216 BIN\VN_Ctrlram.bin 0x0 0x1C3D0 5728 output\nt51931_fw.bin 0x0 0x1DA30 256 output\nt51931_fw.bin 0x16000 0x3B000 2048 
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








