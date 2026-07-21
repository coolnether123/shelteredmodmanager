@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-ScenarioAuthoringToggleContracts.ps1" -RepoRoot "%~dp0.."
