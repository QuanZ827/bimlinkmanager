@echo off
setlocal enabledelayedexpansion
title BimLinkManager - Build MSI Installer

echo.
echo ==========================================
echo   BimLinkManager - Build MSI Installer
echo   Supports Revit 2024-2027
echo ==========================================
echo.

:: Step 1: Project root (one level up from Installer folder)
set "PROJECT_DIR=%~dp0.."
pushd "%PROJECT_DIR%"
set "PROJECT_DIR=%CD%"
popd

echo Project: %PROJECT_DIR%
echo.

:: Step 2: Build net48 (Revit 2024)
echo [1/3] Building Release - net48 (Revit 2024)...
dotnet build "%PROJECT_DIR%" -f net48 --configuration Release
if errorlevel 1 (
    echo [ERROR] net48 build failed!
    pause
    exit /b 1
)
echo [OK] net48 build succeeded.

:: Step 3: Build net8.0-windows (Revit 2025/2026/2027)
echo [2/3] Building Release - net8.0-windows (Revit 2025-2027)...
dotnet build "%PROJECT_DIR%" -f net8.0-windows --configuration Release
if errorlevel 1 (
    echo [ERROR] net8.0-windows build failed!
    pause
    exit /b 1
)
echo [OK] net8.0-windows build succeeded.

:: Step 4: Compile MSI
echo [3/3] Creating MSI installer with WiX...
if not exist "%~dp0Output" mkdir "%~dp0Output"

:: CRITICAL: pushd to script dir so .wxs relative paths resolve correctly
pushd "%~dp0"
wix build "%~dp0BimLinkManager.wxs" -o "%~dp0Output\BimLinkManager_Setup_v1.0.5.msi" -arch x64
set "WIXRC=%ERRORLEVEL%"
popd

if not "%WIXRC%"=="0" (
    echo [ERROR] MSI compilation failed with code %WIXRC%
    pause
    exit /b 1
)

echo.
echo ==========================================
echo   SUCCESS! MSI: Installer\Output\BimLinkManager_Setup_v1.0.5.msi
echo ==========================================

endlocal
