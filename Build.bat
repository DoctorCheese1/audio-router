@echo off
setlocal
cd /d "%~dp0"
set "AUDIO_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%AUDIO_CSC%" set "AUDIO_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%AUDIO_CSC%" goto missing
if not exist "dist" mkdir "dist"
"%AUDIO_CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"dist\AudioRouter.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll AudioRouter.cs
if errorlevel 1 goto failed
copy /y README.md dist\README.md >nul
copy /y THIRD-PARTY.txt dist\THIRD-PARTY.txt >nul
echo Built successfully: dist\AudioRouter.exe
if /i "%~1"=="--no-pause" exit /b 0
pause
exit /b 0
:missing
echo Windows .NET Framework compiler was not found. Use the GitHub Releases build.
goto failed
:failed
echo Build failed. The error above explains why.
if /i "%~1"=="--no-pause" exit /b 1
pause
exit /b 1
