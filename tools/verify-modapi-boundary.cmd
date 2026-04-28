@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Verify-ModApiBoundary.ps1" %*
