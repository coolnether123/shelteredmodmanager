@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scan-StaleVersionReferences.ps1" %*
