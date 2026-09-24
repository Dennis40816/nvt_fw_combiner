@echo off
setlocal
pushd "%~dp0.." || exit /b 1
python scripts\open_golden_example.py %*
set "NFC_EXIT=%ERRORLEVEL%"
popd
exit /b %NFC_EXIT%
