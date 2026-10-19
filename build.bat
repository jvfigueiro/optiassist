@echo off
title Building OptiAssist (WPF)...
echo ===============================================================
echo            OptiAssist - WPF (.NET) Build Script
echo ===============================================================
echo.

set MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
if not exist "%MSBUILD%" set MSBUILD=C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe

if not exist "%MSBUILD%" (
    echo [ERROR] .NET Framework 4.x MSBuild.exe was not found on Windows.
    echo Make sure .NET Framework 4.8 or later is installed.
    pause
    exit /b 1
)

echo Using compiler: %MSBUILD%
echo Compiling WPF project OptiAssist.csproj...
echo.

"%MSBUILD%" OptiAssist.csproj /p:Configuration=Release /v:minimal

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===============================================================
    echo      OptiAssist.exe compiled SUCCESSFULLY! - Ready for Use
    echo ===============================================================
    echo.
) else (
    echo.
    echo [ERROR] Compilation failed. Please check the error messages above.
    echo.
)

pause
