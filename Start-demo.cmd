@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -File "%~dp0Start-demo.ps1"
if errorlevel 1 pause
