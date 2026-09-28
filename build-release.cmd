@echo off
rem Double-click to build a release. Answers come from release-settings.ini.
rem The window stays open so the result (or what to fix) can be read.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1" %*
echo.
pause
