@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-NexusManagerContracts.ps1" %*
exit /b %ERRORLEVEL%
