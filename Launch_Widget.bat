@echo off
title Dexplay — Desktop Music Island
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Launch_Widget.ps1"
exit
