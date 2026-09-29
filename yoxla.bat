@echo off
chcp 65001 >nul
title AvtoPark - Sistem Yoxlamasi
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0yoxla.ps1"
