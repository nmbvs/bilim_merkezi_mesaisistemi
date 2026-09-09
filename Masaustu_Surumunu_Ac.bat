@echo off
chcp 65001 > nul
title Mesai Yonetim Sistemi - Masaustu (Electron) Surumu

echo ====================================================
echo   Mesai Yonetim Sistemi Baslatiliyor...
echo ====================================================
echo.

set EXE_PATH="MesaiYonetimSistemi.Web\bin\Desktop\win-unpacked\MesaiYonetimSistemi.Web.exe"

if exist %EXE_PATH% (
    echo Uygulama aciliyor, lutfen bekleyin...
    start "" %EXE_PATH%
) else (
    echo DIKKAT: Masaustu (Electron) surumu henuz derlenmemis veya bulunamadi!
    echo.
    echo Kodlarda degisiklik yaptiysaniz veya projeyi ilk kez aciyorsaniz, 
    echo oncelikle "electron_yap.bat" dosyasina cift tiklayarak
    echo projeyi build (derleme) etmeniz gerekmektedir.
    echo.
    pause
)
