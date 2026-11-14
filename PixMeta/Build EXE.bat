@echo off
setlocal
cd /d "%~dp0"
echo Publishing PixMeta for Windows x64...
dotnet publish PixMeta.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
if errorlevel 1 (
  echo.
  echo Publish failed.
  pause
  exit /b 1
)
echo.
echo Done: %CD%\publish\win-x64\PixMeta.exe
explorer "%CD%\publish\win-x64"
pause
