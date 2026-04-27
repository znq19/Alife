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
if %errorlevel% equ 0 goto PYTHON_READY

echo [Warning] Python is not installed.
set /p "install=Install Python 3.12.10 now? (y/n): "
if /i "!install!" neq "y" (
    echo [Error] Python required.
    pause
    exit /b 1
)

echo [Info] Downloading Python...
powershell -Command "Invoke-WebRequest -Uri 'https://repo.huaweicloud.com/python/3.12.10/python-3.12.10-amd64.exe' -OutFile '%TEMP%\python_installer.exe'"

echo [Info] Installing Python...
start /wait "" "%TEMP%\python_installer.exe" /quiet PrependPath=1
del "%TEMP%\python_installer.exe"

echo [Info] Refreshing PATH...
set "PS_CMD=$p = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User'); [Environment]::ExpandEnvironmentVariables($p)"
for /f "delims=" %%i in ('powershell -Command "%PS_CMD%"') do set "PATH=%%i"

goto CHECK_PYTHON

:PYTHON_READY
echo [Success] Python detected.
python --version

echo.
echo [Info] Configuring Pip...
pip config set global.index-url https://mirrors.aliyun.com/pypi/simple/ >nul 2>&1

echo.
echo [Info] Launching Alife...
if exist "Outputs\Alife.exe" (
    "Outputs\Alife.exe"
) else (
    echo [Error] 'Outputs\Alife.exe' not found.
    pause
    exit /b 1
)

pause
