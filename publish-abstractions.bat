@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM  Chronos.Core.Abstractions – Publisher
REM  Reads GITHUB_TOKEN from .env file, bumps version, pushes to GitHub Packages
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
set "PROJ=src\Chronos.Core.Abstractions\Chronos.Core.Abstractions.csproj"

REM --- Read current version ---
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
echo [1/3] Building Chronos.Core.Abstractions...
dotnet build "%PROJ%" --configuration Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [2/3] Packing Chronos.Core.Abstractions...
dotnet build-server shutdown >nul 2>&1
set MSBUILDNOINPROCNODE=1
dotnet pack "%PROJ%" --no-build --configuration Release ^
    -p:PackageVersion="%NEW_VERSION%" --output "%NUPKG_DIR%" -nodeReuse:false
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [3/3] Pushing to GitHub Packages...
dotnet nuget push "%NUPKG_DIR%\Chronos.Core.Abstractions.%NEW_VERSION%.nupkg" ^
    --source "%FEED_URL%" ^
    --api-key "%GITHUB_TOKEN%" ^
    --skip-duplicate
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ============================================================
echo  Chronos.Core.Abstractions %NEW_VERSION% published successfully!
echo ============================================================

endlocal
exit /b 0
