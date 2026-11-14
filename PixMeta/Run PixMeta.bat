@echo off
cd /d "%~dp0"
if exist "publish\win-x64\PixMeta.exe" (
  start "" "publish\win-x64\PixMeta.exe" %*
  exit /b
)
dotnet run --no-restore -- %*
if errorlevel 1 (
  echo.
  echo PixMeta could not start. Running restore once...
  dotnet restore && dotnet run --no-restore -- %*
)
if errorlevel 1 pause
