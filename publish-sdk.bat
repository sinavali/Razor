@echo off
setlocal enabledelayedexpansion

:: Load .env file if it exists
if exist ".env" (
    echo Loading credentials from .env file...
    for /f "usebackq delims=" %%a in (".env") do (
        set "%%a"
    )
) else (
    echo .env file not found. Please create one with:
    echo GITHUB_PACKAGE_TOKEN=your_pat_here
    pause
    exit /b 1
)

:: Configuration
set PROJECT_PATH=src\Razor.Core.Sdk\Razor.Core.Sdk.csproj
set CONFIG=Release
set PACKAGE_OUTPUT=.\nupkg
set PACKAGE_SOURCE=https://nuget.pkg.github.com/%GITHUB_USERNAME%/index.json

:: Check if token is set
if "%GITHUB_PACKAGE_TOKEN%"=="" (
    echo ERROR: GITHUB_PACKAGE_TOKEN not found in .env file.
    pause
    exit /b 1
)

:: Read version from VERSION.txt
if not exist "VERSION.txt" (
    echo ERROR: VERSION.txt not found.
    pause
    exit /b 1
)
set /P VERSION=<"VERSION.txt"
for /f "tokens=*" %%a in ("%VERSION%") do set VERSION=%%a
echo Version: %VERSION%

echo.
echo === Cleaning old packages ===
if exist %PACKAGE_OUTPUT% rmdir /s /q %PACKAGE_OUTPUT%
mkdir %PACKAGE_OUTPUT%

echo.
echo === Restoring dependencies ===
dotnet restore %PROJECT_PATH% || exit /b 1

echo.
echo === Building %CONFIG% ===
dotnet build %PROJECT_PATH% --configuration %CONFIG% --no-restore || exit /b 1

echo.
echo === Creating NuGet package (Version %VERSION%) ===
:: Version is already read from Directory.Build.props
dotnet pack %PROJECT_PATH% --configuration %CONFIG% --no-build --output %PACKAGE_OUTPUT% || exit /b 1

echo.
echo === Pushing to GitHub Packages ===
for %%f in (%PACKAGE_OUTPUT%\*.nupkg) do (
    echo Pushing %%f...
    dotnet nuget push "%%f" --source "%PACKAGE_SOURCE%" --api-key %GITHUB_PACKAGE_TOKEN%
    if errorlevel 1 (
        echo Failed to push %%f
        exit /b 1
    )
)

echo.
echo === Done! Package published successfully. ===
pause