@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM  Chronos.Core.Kernel – Local Publisher
REM  Uses local Abstractions package from .\nupkgs
REM ============================================================

set "VERSION_FILE=VERSION.txt"
set "NUPKG_DIR=nupkgs"
set "PROJ=src\Chronos.Core.Kernel\Chronos.Core.Kernel.csproj"

if not exist "%VERSION_FILE%" ( echo ERROR: %VERSION_FILE% missing & exit /b 1 )
set /p VERSION=<"%VERSION_FILE%"
echo Publishing Kernel version %VERSION%

REM --- Ensure local nupkgs folder exists (Abstractions must be built first) ---
if not exist "%NUPKG_DIR%" (
    echo ERROR: %NUPKG_DIR% folder not found. Run publish-abstractions.bat first.
    exit /b 1
)

echo.
echo [0/3] Updating Abstractions reference to version %VERSION%...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$proj='%PROJ%'; $ver='%VERSION%'; $xml=[xml](Get-Content $proj); $xml.Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq 'Chronos.Core.Abstractions' -and -not $_.Condition } | ForEach-Object { $_.Version = $ver }; $xml.Save($proj)"
if %errorlevel% neq 0 exit /b %errorlevel%

REM ============================================================
REM Create a temporary NuGet.Config that points ONLY to local nupkgs
REM ============================================================
set "TEMP_NUGET_CONFIG=%TEMP%\nuget_%RANDOM%.config"
set "LOCAL_FEED_PATH=%~dp0%NUPKG_DIR%"
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<configuration^>
echo   ^<packageSources^>
echo     ^<add key="LocalPackages" value="%LOCAL_FEED_PATH%" /^>
echo   ^</packageSources^>
echo ^</configuration^>
) > "%TEMP_NUGET_CONFIG%"

echo.
echo [1/3] Restoring from local feed: %LOCAL_FEED_PATH%
dotnet restore "%PROJ%" --configfile "%TEMP_NUGET_CONFIG%" --no-cache --disable-parallel
set RESTORE_EXIT=%errorlevel%
del "%TEMP_NUGET_CONFIG%" 2>nul

if %RESTORE_EXIT% neq 0 (
    echo ERROR: Restore failed. Ensure Abstractions package version %VERSION% exists in %NUPKG_DIR%
    exit /b 1
)

echo.
echo [2/3] Building Kernel...
dotnet build "%PROJ%" --configuration Release --no-restore
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [3/3] Packing Kernel...
dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack "%PROJ%" --no-build --configuration Release -p:PackageVersion="%VERSION%" -p:UseLocalAbstractions=false --output "%NUPKG_DIR%" -nodeReuse:false
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ============================================================
echo  Kernel %VERSION% packed locally to:
echo  %CD%\%NUPKG_DIR%\Chronos.Core.Kernel.%VERSION%.nupkg
echo  No remote push performed.
echo ============================================================
endlocal
exit /b 0
