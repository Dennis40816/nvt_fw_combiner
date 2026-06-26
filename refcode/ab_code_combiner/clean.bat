@echo off
setlocal
rem -------------------------------------------------
rem  clean.bat
rem  Purpose:
rem    Clear all content under DP_AB, TPA, and TPB.
rem  Safety:
rem    Ask for confirmation before deleting files.
rem -------------------------------------------------

chcp 65001 >nul

set "BASE_DIR=%~dp0"
set "SUBFOLDERS=DP_AB TPA TPB"

echo This will delete all files and subfolders under:
for %%F in (%SUBFOLDERS%) do (
    echo   "%BASE_DIR%%%F"
)
echo.
choice /m "Continue"
if errorlevel 2 exit /b 1

for %%F in (%SUBFOLDERS%) do (
    if exist "%BASE_DIR%%%F\" (
        del /f /q "%BASE_DIR%%%F\*.*" >nul 2>&1
        for /d %%D in ("%BASE_DIR%%%F\*") do (
            rd /s /q "%%D"
        )
    )
)

echo Done.
exit /b 0
