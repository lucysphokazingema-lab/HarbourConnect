@echo off
rem Double-click to deploy or update HarbourConnect on Azure (free tier).
powershell -NoProfile -ExecutionPolicy Bypass -NoExit -File "%~dp0Deploy-Azure.ps1"
