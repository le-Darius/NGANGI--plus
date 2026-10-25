@echo off
cd /d "%~dp0"
dotnet run --project Ngangi.App -f net10.0-windows10.0.19041.0
pause
