@ECHO OFF
@SET/a DEBUG_EN=0
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51920 Search USER_SWITCH_APPLICATION_TYPE
@ECHO ----------------------------------------------------
:Sesrch
set file=..\inc\hal_mmap.h
set "LineOfCascade2Chip="
for /f "delims=:" %%a in ('findstr /n "CASCADE_2CHIP" "%file%"') do (
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
@ECHO --- NT519210 CASCADE_2CHIP : %AppType% 
@ECHO ----------------------------------------------------
if %AppType% ==(1)        Goto  :NT51920_Cascade_Postbuild
if %AppType% ==(0)   	  Goto  :NT51920_Single_Postbuild
goto :ERR


:NT51920_Cascade_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51920_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------
@output\Combiner.exe CRC_Enable output\nt51920_fw.bin BIN\Normal_Ctrlram.bin 0x0 0x22780 10240 BIN\Normal_Ctrlram_S.bin 0x0 0x26780 10240 BIN\MP_Ctrlram.bin 0x0 0x24F80 5888 BIN\MP_Ctrlram_S.bin 0x0 0x28F80 5888 BIN\VN_Ctrlram.bin 0x0 0x2C710 4120 BIN\NF_Ctrlram.bin 0x0 0x2A780 8080 BIN\Vector_Ctrlram.bin 0x0 0x2D728 600 output\nt51920_fw.bin 0x22000 0x2F000 1920 output\nt51920_fw.bin 0x0 0x26680 256
@output\Combiner.exe CRC_Enable output\nt51920_fw.bin output\nt51920_fw.bin 0x0 0x26680 256
exit /b
goto :EOF

:NT51920_Single_Postbuild
@ECHO ----------------------------------------------------
@ECHO --- InsertSID
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51920_fw.bin

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------
@output\Combiner.exe CRC_Enable output\nt51920_fw.bin BIN\Normal_Ctrlram.bin 0x0 0x22780 10240 BIN\MP_Ctrlram.bin 0x0 0x24F80 5888 BIN\VN_Ctrlram.bin 0x0 0x2C710 4120 BIN\NF_Ctrlram.bin 0x0 0x2A780 8080 output\nt51920_fw.bin 0x22000 0x2F000 1920 output\nt51920_fw.bin 0x0 0x26680 256
@output\Combiner.exe CRC_Enable output\nt51920_fw.bin output\nt51920_fw.bin 0x0 0x26680 256
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








