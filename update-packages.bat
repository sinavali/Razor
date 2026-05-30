@echo off
setlocal enabledelayedexpansion

REM ==============================================================
REM  Chronos Packages Update Script – absolute final
REM  (uses temp PS script to update .csproj, zero hangs)
REM ==============================================================

REM --- Disable QuickEdit (for new console windows) ---
reg add HKCU\Console /v QuickEdit /t REG_DWORD /d 0 /f >nul 2>&1

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
dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack %ABSTR_PROJ% --no-build --configuration Release ^
    -p:PackageVersion=%NEW_VERSION% --output "%NUPKG_DIR%" ^
    -nodeReuse:false
if %errorlevel% neq 0 goto :ERROR "Pack failed for Chronos.Core.Abstractions."

echo [3/5] Pushing Chronos.Core.Abstractions to Gitea...
dotnet nuget push "%NUPKG_DIR%\Chronos.Core.Abstractions.%NEW_VERSION%.nupkg" ^
    --source "%GITEA_FEED%" ^
    --api-key %TOKEN% ^
    --skip-duplicate
if %errorlevel% neq 0 goto :ERROR "Push failed for Chronos.Core.Abstractions."

REM ==============================================================
REM  2. Update Kernel reference (safe XML edit via temp PS)
REM ==============================================================
echo.
echo [4/5] Updating Chronos.Core.Kernel to use Chronos.Core.Abstractions %NEW_VERSION%...
pushd src\Chronos.Core.Kernel

set CSPROJ=Chronos.Core.Kernel.csproj
if not exist "%CSPROJ%" goto :ERROR "Missing %CSPROJ%"

REM Create a temporary PowerShell script to update the version
set PS_SCRIPT=%TEMP%\update_ref_%RANDOM%.ps1
(
echo $xml = [xml](Get-Content '%CSPROJ%')
echo $node = $xml.Project.ItemGroup.PackageReference ^| Where-Object { $_.Include -eq 'Chronos.Core.Abstractions' }
echo if ($node) {
echo     $node.Version = '%NEW_VERSION%'
echo     $xml.Save('%CSPROJ%')
echo     Write-Host 'Updated version in csproj to %NEW_VERSION%'
echo } else {
echo     Write-Error 'PackageReference not found'
echo     exit 1
echo }
) > "%PS_SCRIPT%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
if %errorlevel% neq 0 (
    del "%PS_SCRIPT%" 2>nul
    popd
    goto :ERROR "Failed to update package reference in Kernel project file."
)
del "%PS_SCRIPT%" 2>nul

REM Now restore (credentials are set)
set NUGET_USERNAME=sinavali
set NUGET_PASSWORD=%TOKEN%
dotnet restore --source "%GITEA_FEED%"
if %errorlevel% neq 0 (
    popd
    goto :ERROR "Restore failed for Chronos.Core.Kernel."
)
popd

REM ==============================================================
REM  3. Build & Push Chronos.Core.Kernel
REM ==============================================================
echo.
echo [5/5] Building and packing Chronos.Core.Kernel...
dotnet build %KERNEL_PROJ% --configuration Release
if %errorlevel% neq 0 goto :ERROR "Build failed for Chronos.Core.Kernel."

dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack %KERNEL_PROJ% --no-build --configuration Release ^
    -p:PackageVersion=%NEW_VERSION% --output "%NUPKG_DIR%" ^
    -nodeReuse:false
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