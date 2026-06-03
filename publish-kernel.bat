@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM  Chronos.Core.Kernel – Publisher
REM  Reads GITHUB_TOKEN from .env file, reads version from VERSION.txt, pushes to GitHub Packages
REM ============================================================

REM --- Load GitHub token from .env file ---
set "GITHUB_TOKEN="
if exist ".env" (
    for /f "tokens=2 delims==" %%i in ('findstr /b "GITHUB_TOKEN=" .env') do set "GITHUB_TOKEN=%%i"
)
if "%GITHUB_TOKEN%"=="" (
    echo ERROR: GITHUB_TOKEN not found in .env file.
    echo Create a .env file with content: GITHUB_TOKEN=your_token
    exit /b 1
)

set "FEED_URL=https://nuget.pkg.github.com/ChronosPlatform/index.json"
set "VERSION_FILE=VERSION.txt"
set "NUPKG_DIR=nupkgs"
set "PROJ=src\Chronos.Core.Kernel\Chronos.Core.Kernel.csproj"

REM --- Read current version ---
if not exist "%VERSION_FILE%" (
    echo ERROR: %VERSION_FILE% not found.
    exit /b 1
)
set /p VERSION=<"%VERSION_FILE%"
echo Publishing Kernel version %VERSION%

REM --- Ensure user has updated Kernel.csproj ---
echo.
echo IMPORTANT: Make sure you have updated the Abstractions
echo package reference in src\Chronos.Core.Kernel\Chronos.Core.Kernel.csproj
echo to version %VERSION% before proceeding.
echo.
pause

REM --- Clean and recreate output directory ---
if exist "%NUPKG_DIR%" rmdir /s /q "%NUPKG_DIR%"
mkdir "%NUPKG_DIR%"

echo.
echo [1/3] Building Chronos.Core.Kernel...
dotnet build "%PROJ%" --configuration Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [2/3] Packing Chronos.Core.Kernel...
dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack "%PROJ%" --no-build --configuration Release ^
    -p:PackageVersion="%VERSION%" ^
    -p:UseLocalAbstractions=false ^
    --output "%NUPKG_DIR%" -nodeReuse:false
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [3/3] Pushing to GitHub Packages...
dotnet nuget push "%NUPKG_DIR%\Chronos.Core.Kernel.%VERSION%.nupkg" ^
    --source "%FEED_URL%" ^
    --api-key "%GITHUB_TOKEN%" ^
    --skip-duplicate
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ============================================================
echo  Chronos.Core.Kernel %VERSION% published successfully!
echo ============================================================

endlocal
exit /b 0
