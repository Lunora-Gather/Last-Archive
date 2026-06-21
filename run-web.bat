@echo off
set "PATH=C:\Program Files\dotnet;%PATH%"
cd /d "%~dp0"
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo.
echo Starting web server at http://localhost:8080
dotnet run -- --web
pause
