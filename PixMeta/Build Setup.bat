@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo ==============================================
echo  PixMeta Windows x64 - Build Setup.exe
echo ==============================================
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK is required ONLY to build PixMeta, not to install it.
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo Inno Setup 6 is required ONLY to make the installer.
  echo https://jrsoftware.org/isinfo.php
  pause
  exit /b 1
)
echo [1/2] Publishing self-contained PixMeta.exe...
dotnet publish "PixMeta.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "publish\win-x64"
if errorlevel 1 goto failed
if not exist "publish\win-x64\PixMeta.exe" goto failed
echo [2/2] Compiling installer...
"%ISCC%" "installer\PixMeta.iss"
if errorlevel 1 goto failed
if not exist "dist\PixMeta-Setup-v0.5.19-RC3.exe" goto failed
explorer "dist"
echo Done! Installable package: dist\PixMeta-Setup-v0.5.19-RC3.exe
pause
exit /b 0
:failed
echo Build failed. Check compiler errors above.
pause
exit /b 1
