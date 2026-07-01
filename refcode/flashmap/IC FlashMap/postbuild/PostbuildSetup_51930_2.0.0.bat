@ECHO OFF
@SET/a DEBUG_EN=1
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51930 Search USER_SWITCH_APPLICATION_TYPE
@ECHO ----------------------------------------------------
:Sesrch
set file=..\inc\define\mmap.h

@ECHO ----------------------------------------------------
@ECHO --- NT51930 Parsing FLASH MAP
@ECHO ----------------------------------------------------
set fmap_cascadenum=CASCADE_CHIP_NUM
set fmap_ctrlram=FLASHMAP_CTRLRAM
set fmap_diffctrlram=FLASHMAP_DIFF_DLM
set fmap_nfctrlram=FLASHMAP_NF_TABLE
set fmap_vnctrlram=FLASHMAP_VNCTRLRAM
set fmap_headercpy=FLASHMAP_HEADER_COPY

set fmap_ctrlram_sz=CTRLRAM_SZ
set fmap_diffctrlram_sz=DIFF_DLM_SZ
set fmap_vnctrlram_sz=VN_CTRLRAM_SZ
set fmap_nfctrlram_sz=NF_CTRLRAM_SZ
set fmap_headercpy_sz=HEADER_SZ


for /f "usebackq delims=" %%x in ("%file%") do (

	for /f "tokens=2,3 delims= " %%i in ("%%x") do (
		rem echo %%i
		rem echo %%j

		if %fmap_cascadenum% == %%i (
			set cascadenums=%%j
		)

		if %fmap_ctrlram% == %%i (
			set ctrlrams=%%j
		)
		
		if %fmap_diffctrlram% == %%i (
			set diffctrlrams=%%j
		)

		if %fmap_nfctrlram% == %%i (
			set nfctrlrams=%%j
		)

		if %fmap_vnctrlram% == %%i (
			set vnctrlrams=%%j
		)

		if %fmap_ctrlram_sz% == %%i (
			set ctrlram_szs=%%j
		)

		if %fmap_diffctrlram_sz% == %%i (
			set diffctrlram_szs=%%j
		)

		if %fmap_vnctrlram_sz% == %%i (
			set vnctrlram_szs=%%j
		)

		if %fmap_nfctrlram_sz% == %%i (
			set nfctrlram_szs=%%j
		)

		if %fmap_headercpy% == %%i (
			set headercpy=%%j
		)

		if %fmap_headercpy_sz% == %%i (
			set headercpy_szs=%%j
		)
	)
)

if  not defined cascadenums  Goto :ERR
if  not defined ctrlrams  Goto :ERR
if  not defined diffctrlrams  Goto :ERR
if  not defined nfctrlrams  Goto :ERR
if  not defined vnctrlrams  Goto :ERR
if  not defined ctrlram_szs  Goto :ERR
if  not defined diffctrlram_szs  Goto :ERR
if  not defined vnctrlram_szs  Goto :ERR
if  not defined nfctrlram_szs  Goto :ERR
if  not defined headercpy  Goto :ERR
if  not defined headercpy_szs  Goto :ERR


set cascadenum=%cascadenums:~1,1%
set ctrlramAddr=%ctrlrams:~1,7%
set diffctrlramAddr=%diffctrlrams:~1,7%
set nfctrlramAddr=%nfctrlrams:~1,7%
set vnctrlramAddr=%vnctrlrams:~1,7%
set headercpyAddr=%headercpy:~1,7%

set /a ctrlram_sz=%ctrlram_szs:~1,7%
set /a diffctrlram_sz=%diffctrlram_szs:~1,7%
set /a vnctrlram_sz=%vnctrlram_szs:~1,7%
set /a nfctrlram_sz=%nfctrlram_szs:~1,7%
set /a headercpy_sz=%headercpy_szs:~1,7%

set Normal_Ctrlram=Normal_Ctrlram.bin
set DiffDLM=DiffDLM.bin
set VN_Ctrlram=VN_Ctrlram.bin
set NF_Ctrlram=NF_Ctrlram.bin

set CtrlramCMD=BIN\%Normal_Ctrlram% 0x0 %ctrlramAddr% %ctrlram_sz%
set DiffCtrlramCMD=BIN\%DiffDLM% 0x0 %diffctrlramAddr% %diffctrlram_sz%
set VNCtrlramCMD=BIN\%VN_Ctrlram% 0x0 %vnctrlramAddr% %vnctrlram_sz%
set NFCtrlramCMD=BIN\%NF_Ctrlram% 0x0 %nfctrlramAddr% %nfctrlram_sz%
set HeaderCMD=output\NT51930_fw.bin 0x7000 %headercpyAddr% %headercpy_sz%

if %DEBUG_EN% EQU 1 (
	echo cascadenum:%cascadenum%
	echo ctrlramAddr:%ctrlramAddr%
	echo diffctrlramAddr:%diffctrlramAddr%
	echo nfctrlramAddr:%nfctrlramAddr%
	echo vnctrlramAddr:%vnctrlramAddr%
	echo headercpyAddr:%headercpyAddr%

	echo ctrlram_sz:%ctrlram_sz%
	echo diffctrlram_sz:%diffctrlram_sz%
	echo vnctrlram_sz:%vnctrlram_sz%
	echo nfctrlram_sz:%nfctrlram_sz%
	echo headercpy_sz:%headercpy_sz%

	echo %CtrlramCMD%
	echo %DiffCtrlramCMD%
	echo %VNCtrlramCMD%
	echo %NFCtrlramCMD%
	echo %HeaderCMD%
)


@ECHO ----------------------------------------------------
@ECHO --- NT51930: %cascadenum% CHIP(s)
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\NT51930_fw.bin
if %cascadenum% == 1   	    Goto  :NT51930_Single_Postbuild
if %cascadenum% == 2        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 3        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 4        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 5        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 6        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 7        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 8        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 9        Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 10       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 11       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 12       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 13       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 14       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 15       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 16       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 17       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 18       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 19       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 20       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 21       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 22       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 23       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 24       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 25       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 26       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 27       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 28       Goto  :NT51930_Cascade_Postbuild
if %cascadenum% == 29       Goto  :NT51930_Cascade_Postbuild

goto :ERR

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------

:NT51930_Cascade_Postbuild
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\NT51930_fw.bin output\NT51930_fw.bin %CtrlramCMD% %DiffCtrlramCMD% %VNCtrlramCMD% %NFCtrlramCMD% %HeaderCMD%
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\NT51930_fw.bin output\NT51930_fw.bin %HeaderCMD%
exit /b
goto :EOF

:NT51930_Single_Postbuild
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\NT51930_fw.bin output\NT51930_fw.bin %CtrlramCMD% %VNCtrlramCMD% %NFCtrlramCMD% %HeaderCMD%
@output\Combiner.exe NT51930BASED_NORMAL_MODE CRC8 output\NT51930_fw.bin output\NT51930_fw.bin %HeaderCMD%
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , please check PostBuildSetup.bat
@ECHO ----------------------------------------------------
if %DEBUG_EN% EQU 1 (
	echo cascadenum:%cascadenum%
	echo ctrlramAddr:%ctrlramAddr%
	echo diffctrlramAddr:%diffctrlramAddr%
	echo nfctrlramAddr:%nfctrlramAddr%
	echo vnctrlramAddr:%vnctrlramAddr%
	echo headercpyAddr:%headercpyAddr%

	echo ctrlram_sz:%ctrlram_sz%
	echo diffctrlram_sz:%diffctrlram_sz%
	echo vnctrlram_sz:%vnctrlram_sz%
	echo nfctrlram_sz:%nfctrlram_sz%
	echo headercpy_sz:%headercpy_sz%

	echo %CtrlramCMD%
	echo %DiffCtrlramCMD%
	echo %VNCtrlramCMD%
	echo %NFCtrlramCMD%
	echo %HeaderCMD%
)

pause
goto :EOF








