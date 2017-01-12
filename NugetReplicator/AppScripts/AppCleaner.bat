@echo off
echo clean app data ...
rmdir NugetRepo /s /q >nul 2>nul
rmdir logs /s /q >nul 2>nul
pause