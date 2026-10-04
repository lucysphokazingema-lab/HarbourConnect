@echo off
rem Double-click to install or update the HarbourConnect network server (asks for admin rights).
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','\"%~dp0Install-LanServer.ps1\"')"
