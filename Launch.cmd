@echo off
setlocal enabledelayedexpansion
title Alife Launcher

echo ============================================================
echo                Alife System Launcher
echo ============================================================
echo.

:CHECK_PYTHON
echo [System] Checking Python environment...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [Warning] Python is not installed or not in your PATH.
    set /p "install=Would you like to install Python 3.12.10 now? (y/n): "
    if /i "!install!" neq "y" (
        echo [Error] Python is required to run Alife. Exiting...
        pause
        exit /b 1
    )

    echo [Info] Downloading Python 3.12.10 installer...
    powershell -Command "Invoke-WebRequest -Uri 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe' -OutFile '%TEMP%\python_installer.exe'"
    
    echo [Info] Installing Python (Please grant administrator permission if prompted)...
    start /wait "" "%TEMP%\python_installer.exe" /quiet PrependPath=1
    del "%TEMP%\python_installer.exe"
    
    echo [Info] Refreshing environment variables for this session...
    for /f "delims=" %%i in ('powershell -Command "$p = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User'); [Environment]::ExpandEnvironmentVariables($p)"') do set "PATH=%%i"
    
    goto CHECK_PYTHON
)

echo [Success] Python detected:
python --version

echo.
echo [Info] Configuring Pip mirror (Alibaba Cloud)...
pip config set global.index-url https://mirrors.aliyun.com/pypi/simple/ >nul 2>&1

echo.
echo [Info] launching Alife application...
echo ------------------------------------------------------------

if exist "Outputs\Alife.exe" (
    "Outputs\Alife.exe"
) else (
    echo [Error] 'Outputs\Alife.exe' not found! 
    echo Please build the project in Visual Studio or via 'dotnet build' first.
    pause
    exit /b 1
)

echo.
echo ------------------------------------------------------------
echo [System] Alife process has terminated.
pause
