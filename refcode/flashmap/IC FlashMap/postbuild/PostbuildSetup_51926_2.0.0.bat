@ECHO OFF
@SET/a DEBUG_EN=0
@ECHO OFF 
 
goto :Sesrch



@ECHO ----------------------------------------------------
@ECHO --- NT51926 Search USER_SWITCH_APPLICATION_TYPE
@ECHO ----------------------------------------------------
:Sesrch
set file=..\inc\define\mmap.h

@ECHO ----------------------------------------------------
@ECHO --- NT51926 Parsing FLASH MAP
@ECHO ----------------------------------------------------
set fmap_cascadenum=CASCADE_CHIP_NUM
set fmap_ctrlram=FLASHMAP_CTRLRAM
set fmap_mpctrlram=FLASHMAP_MP_CTRLRAM
set fmap_diffctrlram=FLASHMAP_DIFF_DLM
set fmap_nfctrlram=FLASHMAP_NF_TABLE
set fmap_vnctrlram=FLASHMAP_VNCTRLRAM
set fmap_headercpy=FLASHMAP_HEADER_CPY
set fmap_fwconfig=FLASHMAP_FW_REGISTER
set fmap_fwconfig2=FLASHMAP_FWCONFIG2

set fmap_ctrlram_sz=CTRLRAM_SZ
set fmap_mpctrlram_sz=MP_CTRLRAM_SZ
set fmap_diffctrlram_sz=DIFF_DLM_SZ
set fmap_vnctrlram_sz=VN_CTRLRAM_SZ
set fmap_nfctrlram_sz=NF_CTRLRAM_SZ
set fmap_headercpy_sz=HEADER_SZ
set fmap_fwconfig_sz=FWCONFIG_SIZE


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

		if %fmap_mpctrlram% == %%i (
			set mpctrlrams=%%j
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

		if %fmap_mpctrlram_sz% == %%i (
			set mpctrlram_szs=%%j
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

		if %fmap_fwconfig% == %%i (
			set fwconfig=%%j
		)

		if %fmap_fwconfig_sz% == %%i (
			set fwconfig_szs=%%j
		)

		if %fmap_fwconfig2% == %%i (
			set fwconfig2=%%j
		)
	)
)

if  not defined cascadenums  Goto :ERR
if  not defined ctrlrams  Goto :ERR
if  not defined mpctrlrams  Goto :ERR
if  not defined diffctrlrams  Goto :ERR
if  not defined nfctrlrams  Goto :ERR
if  not defined vnctrlrams  Goto :ERR
if  not defined ctrlram_szs  Goto :ERR
if  not defined mpctrlram_szs  Goto :ERR
if  not defined diffctrlram_szs  Goto :ERR
if  not defined vnctrlram_szs  Goto :ERR
if  not defined nfctrlram_szs  Goto :ERR
if  not defined headercpy  Goto :ERR
if  not defined headercpy_szs  Goto :ERR
if  not defined fwconfig  Goto :ERR
if  not defined fwconfig_szs  Goto :ERR
if  not defined fwconfig2  Goto :ERR


set cascadenum=%cascadenums:~1,1%
set ctrlramAddr=%ctrlrams:~1,7%
set mpctrlramAddr=%mpctrlrams:~1,7%
set diffctrlramAddr=%diffctrlrams:~1,7%
set nfctrlramAddr=%nfctrlrams:~1,7%
set vnctrlramAddr=%vnctrlrams:~1,7%
set headercpyAddr=%headercpy:~1,7%
set fwconfigAddr=%fwconfig:~1,7%
set fwconfig2Addr=%fwconfig2:~1,7%

set /a ctrlram_sz=%ctrlram_szs:~1,7%
set /a mpctrlram_sz=%mpctrlram_szs:~1,7%
set /a diffctrlram_sz=%diffctrlram_szs:~1,7%
set /a vnctrlram_sz=%vnctrlram_szs:~1,7%
set /a nfctrlram_sz=%nfctrlram_szs:~1,7%
set /a headercpy_sz=%headercpy_szs:~1,7%
set /a fwconfig_sz=%fwconfig_szs:~1,7%

set Normal_Ctrlram=Normal_Ctrlram.bin
set DiffDLM=DiffDLM.bin
set MP_Ctrlram=MP_Ctrlram.bin
set VN_Ctrlram=VN_Ctrlram.bin
set NF_Ctrlram=NF_Ctrlram.bin

set CtrlramCMD=BIN\%Normal_Ctrlram% 0x0 %ctrlramAddr% %ctrlram_sz%
set DiffCtrlramCMD=BIN\%DiffDLM% 0x0 %diffctrlramAddr% %diffctrlram_sz%
set MPCtrlramCMD=BIN\%MP_Ctrlram% 0x0 %mpctrlramAddr% %mpctrlram_sz%
set VNCtrlramCMD=BIN\%VN_Ctrlram% 0x0 %vnctrlramAddr% %vnctrlram_sz%
set NFCtrlramCMD=BIN\%NF_Ctrlram% 0x0 %nfctrlramAddr% %nfctrlram_sz%
set FWConfigCMD=output\nt51926_fw.bin %fwconfigAddr% %fwconfig2Addr% %fwconfig_sz%
set HeaderCMD=output\nt51926_fw.bin 0x0 %headercpyAddr% %headercpy_sz%

if %DEBUG_EN% EQU 1 (
	echo cascadenum:%cascadenum%
	echo ctrlramAddr:%ctrlramAddr%
	echo mpctrlramAddr:%mpctrlramAddr%
	echo diffctrlramAddr:%diffctrlramAddr%
	echo nfctrlramAddr:%nfctrlramAddr%
	echo vnctrlramAddr:%vnctrlramAddr%
	echo headercpyAddr:%headercpyAddr%
	echo fwconfigAddr:%fwconfigAddr%

	echo ctrlram_sz:%ctrlram_sz%
	echo mpctrlram_sz:%mpctrlram_sz%
	echo diffctrlram_sz:%diffctrlram_sz%
	echo vnctrlram_sz:%vnctrlram_sz%
	echo nfctrlram_sz:%nfctrlram_sz%
	echo headercpy_sz:%headercpy_sz%
	echo fwconfig_sz:%fwconfig_sz%

	echo %CtrlramCMD%
	echo %DiffCtrlramCMD%
	echo %MPCtrlramCMD%
	echo %VNCtrlramCMD%
	echo %NFCtrlramCMD%
	echo %FWConfigCMD%
	echo %HeaderCMD%
)


@ECHO ----------------------------------------------------
@ECHO --- NT51926: %cascadenum% CHIP(s)
@ECHO ----------------------------------------------------
@python output\InsertSID.py output\nt51926_fw.bin

if %cascadenum% == 3        Goto  :NT51926_Cascade_Postbuild
if %cascadenum% == 2        Goto  :NT51926_Cascade_Postbuild
if %cascadenum% == 1   	    Goto  :NT51926_Single_Postbuild
goto :ERR

@ECHO ----------------------------------------------------
@ECHO --- MergeBin
@ECHO ----------------------------------------------------

:NT51926_Cascade_Postbuild
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin %CtrlramCMD% %DiffCtrlramCMD% %MPCtrlramCMD% %VNCtrlramCMD% %NFCtrlramCMD% %FWConfigCMD% %HeaderCMD%
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin %HeaderCMD%
exit /b
goto :EOF

:NT51926_Single_Postbuild
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin %CtrlramCMD% %MPCtrlramCMD% %VNCtrlramCMD% %NFCtrlramCMD% %FWConfigCMD% %HeaderCMD%
@output\Combiner.exe CRC_Enable output\nt51926_fw.bin %HeaderCMD%
exit /b
goto :EOF

:ERR
@ECHO ----------------------------------------------------
@ECHO --- Error , pealse check PostBuildSetup.bat
@ECHO ----------------------------------------------------
pause
goto :EOF








