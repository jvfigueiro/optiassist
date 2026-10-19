@echo off
title OptiAssist - Redefinicao de Rede e Conectividade
echo ===================================================
echo      OptiAssist - Redefinicao de Rede e DNS
echo ===================================================
echo.

echo [1/5] Liberando concessao DHCP atual...
ipconfig /release >nul 2>&1

echo [2/5] Limpando cache do resolvedor DNS...
ipconfig /flushdns

echo [3/5] Redefinindo catalogo Winsock...
netsh winsock reset

echo [4/5] Redefinindo pilha TCP/IP (v4 e v6)...
netsh int ip reset >nul 2>&1

echo [5/5] Renovando concessao DHCP...
ipconfig /renew >nul 2>&1

echo.
echo ===================================================
echo   Rede redefinida com sucesso!
echo   (Se necessario, reinicie o computador).
echo ===================================================
echo.
pause
