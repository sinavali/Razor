@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM  Chronos.Core.Abstractions – Local Publisher
REM  Builds and packs to .\nupkgs (no push)
REM ============================================================

set "VERSION_FILE=VERSION.txt"
set "NUPKG_DIR=nupkgs"
set "PROJ=src\Chronos.Core.Abstractions\Chronos.Core.Abstractions.csproj"

if not exist "%VERSION_FILE%" (
    echo ERROR: %VERSION_FILE% not found.
    exit /b 1
)
set /p CURRENT_VERSION=<"%VERSION_FILE%"
echo Current version: %CURRENT_VERSION%

REM --- Bump patch version ---
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set "MAJOR=%%a"
    set "MINOR=%%b"
    set "PATCH=%%c"
)
set /a "NEXT_PATCH=PATCH+1"
set "DEFAULT_VERSION=%MAJOR%.%MINOR%.%NEXT_PATCH%"

REM --- Ask for version ---
set /p NEW_VERSION="Enter new version (default: %DEFAULT_VERSION%): "
if "%NEW_VERSION%"=="" set "NEW_VERSION=%DEFAULT_VERSION%"
echo Using version %NEW_VERSION%

REM --- Update version file ---
echo %NEW_VERSION%>"%VERSION_FILE%"
echo Updated %VERSION_FILE% to %NEW_VERSION%

REM --- Clean and recreate output directory ---
if exist "%NUPKG_DIR%" rmdir /s /q "%NUPKG_DIR%"
mkdir "%NUPKG_DIR%"

echo.
echo [1/2] Building Abstractions...
dotnet build "%PROJ%" --configuration Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [2/2] Packing Abstractions...
dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack "%PROJ%" --no-build --configuration Release ^
    -p:PackageVersion="%NEW_VERSION%" --output "%NUPKG_DIR%" -nodeReuse:false
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ============================================================
echo  Abstractions %NEW_VERSION% packed locally to:
echo  %CD%\%NUPKG_DIR%\Chronos.Core.Abstractions.%NEW_VERSION%.nupkg
echo  No remote push performed.
echo ============================================================
endlocal
exit /b 0
