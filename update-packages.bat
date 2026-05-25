@echo off
setlocal enabledelayedexpansion

REM ==============================================================
REM  Chronos Packages Update Script
REM  Prerequisite: Run this command ONCE to store credentials for
REM  the feed URL:
REM    dotnet nuget update source Gitea --username git --password YOUR_TOKEN --store-password-in-clear-text
REM ==============================================================

REM --- Configuration ---
set TOKEN=a535a3fa75145f3dc64ba0439560fa71bd90021c
set GITEA_FEED=http://localhost:300/api/packages/Chronos/nuget/index.json
set VERSION_FILE=VERSION.txt
set NUPKG_DIR=nupkgs

set ABSTR_PROJ=src\Chronos.Core.Abstractions\Chronos.Core.Abstractions.csproj
set KERNEL_PROJ=src\Chronos.Core.Kernel\Chronos.Core.Kernel.csproj

REM --- Read current base version ---
if not exist "%VERSION_FILE%" goto :ERROR "ERROR: %VERSION_FILE% not found."
for /f "usebackq tokens=* delims=" %%a in ("%VERSION_FILE%") do set CURRENT_VERSION=%%a
set CURRENT_VERSION=%CURRENT_VERSION: =%
echo Current base version: %CURRENT_VERSION%

REM --- Compute next patch version ---
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set PATCH=%%c
)
set /a NEXT_PATCH=%PATCH%+1
set DEFAULT_VERSION=%MAJOR%.%MINOR%.%NEXT_PATCH%

REM --- Ask for new version ---
set /p NEW_VERSION="Enter new version (default: %DEFAULT_VERSION%): "
if "%NEW_VERSION%"=="" set NEW_VERSION=%DEFAULT_VERSION%
echo Using version %NEW_VERSION%

REM --- Update VERSION.txt ---
echo %NEW_VERSION%> %VERSION_FILE%
echo Updated %VERSION_FILE% to %NEW_VERSION%

REM --- Clean old packages ---
if exist "%NUPKG_DIR%" rmdir /s /q "%NUPKG_DIR%"
mkdir "%NUPKG_DIR%"

REM ==============================================================
REM  1. Build & Push Chronos.Core.Abstractions
REM ==============================================================
echo.
echo [1/5] Building Chronos.Core.Abstractions...
dotnet build %ABSTR_PROJ% --configuration Release
if %errorlevel% neq 0 goto :ERROR "Build failed for Chronos.Core.Abstractions."

echo [2/5] Packing Chronos.Core.Abstractions...
dotnet pack %ABSTR_PROJ% --no-build --configuration Release ^
    -p:PackageVersion=%NEW_VERSION% ^
    --output "%NUPKG_DIR%"
if %errorlevel% neq 0 goto :ERROR "Pack failed for Chronos.Core.Abstractions."

echo [3/5] Pushing Chronos.Core.Abstractions to Gitea...
dotnet nuget push "%NUPKG_DIR%\Chronos.Core.Abstractions.%NEW_VERSION%.nupkg" ^
    --source "%GITEA_FEED%" ^
    --api-key %TOKEN% ^
    --skip-duplicate
if %errorlevel% neq 0 goto :ERROR "Push failed for Chronos.Core.Abstractions."

REM ==============================================================
REM  2. Clear local cache & update Kernel reference
REM ==============================================================
echo.
echo [4/5] Clearing local NuGet cache...
dotnet nuget locals http-cache --clear
if %errorlevel% neq 0 echo Warning: Could not clear HTTP cache, but continuing...

echo Updating Chronos.Core.Kernel to use Chronos.Core.Abstractions %NEW_VERSION%...
pushd src\Chronos.Core.Kernel
dotnet add package Chronos.Core.Abstractions --version %NEW_VERSION% --source "%GITEA_FEED%"
if %errorlevel% neq 0 (
    popd
    goto :ERROR "Failed to update package reference in Chronos.Core.Kernel."
)
popd

REM ==============================================================
REM  3. Build & Push Chronos.Core.Kernel
REM ==============================================================
echo.
echo [5/5] Building and packing Chronos.Core.Kernel...
dotnet build %KERNEL_PROJ% --configuration Release
if %errorlevel% neq 0 goto :ERROR "Build failed for Chronos.Core.Kernel."

dotnet pack %KERNEL_PROJ% --no-build --configuration Release ^
    -p:PackageVersion=%NEW_VERSION% ^
    --output "%NUPKG_DIR%"
if %errorlevel% neq 0 goto :ERROR "Pack failed for Chronos.Core.Kernel."

echo Pushing Chronos.Core.Kernel to Gitea...
dotnet nuget push "%NUPKG_DIR%\Chronos.Core.Kernel.%NEW_VERSION%.nupkg" ^
    --source "%GITEA_FEED%" ^
    --api-key %TOKEN% ^
    --skip-duplicate
if %errorlevel% neq 0 goto :ERROR "Push failed for Chronos.Core.Kernel."

echo ============================================================
echo  Chronos.Core.Abstractions %NEW_VERSION% and
echo  Chronos.Core.Kernel %NEW_VERSION% published successfully!
echo ============================================================
pause
exit /b 0

REM ##############################################################
REM  Error handler
REM ##############################################################
:ERROR
echo.
echo ============================================================
echo  ERROR: %~1
echo ============================================================
pause
exit /b 1