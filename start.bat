@echo off
title Project Infinity - Game & Server Launcher
echo ===================================================
echo               PROJECT INFINITY
echo ===================================================
echo.

:: 1. Check Node.js
where node >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] Node.js is not found on PATH!
    echo Please install Node.js 22 or higher from https://nodejs.org
    pause
    exit /b 1
)

:: 2. Check if Docker is requested or start Node server directly
echo [1/2] Starting authoritative backend server on port 8787...
start "Project Infinity Server" cmd /k "cd /d %~dp0server && node src/server.js"

echo Waiting for server to initialize...
timeout /t 2 /nobreak >nul

:: 3. Check Unity installation
echo [2/2] Checking Unity 6 Editor...
set UNITY_EXE=
if exist "C:\Program Files\Unity 6000.6.10f1\Editor\Unity.exe" (
    set "UNITY_EXE=C:\Program Files\Unity 6000.6.10f1\Editor\Unity.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe" (
    set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
)

if defined UNITY_EXE (
    echo Launching Unity with ProjectInfinity...
    start "" "%UNITY_EXE%" -projectPath "%~dp0ProjectInfinity"
) else (
    echo Unity Editor executable not found in default paths.
    echo Please open Unity Hub and launch ProjectInfinity from there.
)

echo.
echo ===================================================
echo Backend running on ws://127.0.0.1:8787
echo Healthcheck at http://127.0.0.1:8787/health
echo Press Play in Unity Editor to play the game!
echo ===================================================
pause
