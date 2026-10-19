@echo off
setlocal
cd /d "%~dp0"
if exist "dist\AudioRouter.exe" goto launch
call Build.bat --no-pause
if errorlevel 1 goto failed
:launch
start "" "%~dp0dist\AudioRouter.exe"
exit /b 0
:failed
pause
exit /b 1
