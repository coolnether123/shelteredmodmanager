@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Verify-ShelteredApiPublicSurface.ps1" %*
