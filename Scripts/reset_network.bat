@echo off
title OptiAssist - Network & Connectivity Reset
echo ===================================================
echo      OptiAssist - Network & DNS Reset
echo ===================================================
echo.

echo [1/5] Releasing current DHCP lease...
ipconfig /release >nul 2>&1

echo [2/5] Flushing DNS resolver cache...
ipconfig /flushdns

echo [3/5] Resetting Winsock catalog...
netsh winsock reset

echo [4/5] Resetting TCP/IP stack (v4 and v6)...
netsh int ip reset >nul 2>&1

echo [5/5] Renewing DHCP lease...
ipconfig /renew >nul 2>&1

echo.
echo ===================================================
echo   Network reset completed successfully!
echo   (If necessary, restart your computer).
echo ===================================================
echo.
pause
