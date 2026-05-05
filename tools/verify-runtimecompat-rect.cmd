@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Verify-RuntimeCompatRect.ps1" %*
