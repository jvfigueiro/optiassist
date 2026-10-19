@echo off
title Compilando OptiAssist (WPF)...
echo ===============================================================
echo            OptiAssist - Script de Compilacao WPF (.NET)
echo ===============================================================
echo.

set MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
if not exist "%MSBUILD%" set MSBUILD=C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe

if not exist "%MSBUILD%" (
    echo [ERRO] O MSBuild.exe do .NET Framework 4.x nao foi encontrado no Windows.
    echo Certifique-se de que o .NET Framework 4.8 esteja instalado.
    pause
    exit /b 1
)

echo Usando compilador: %MSBUILD%
echo Compilando projeto WPF OptiAssist.csproj...
echo.

"%MSBUILD%" OptiAssist.csproj /p:Configuration=Release /v:minimal

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===============================================================
    echo      OptiAssist.exe compilado com SUCESSO! - Pronto para Uso
    echo ===============================================================
    echo.
) else (
    echo.
    echo [ERRO] Houve falhas durante a compilacao. Verifique as mensagens acima.
    echo.
)

pause
