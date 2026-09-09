@echo off
chcp 65001 > nul
title Electron Masaustu Uygulamasi Olusturucu

echo ====================================================
echo   Mesai Yonetim Sistemi - Masaustu (Electron) Surumu
echo   Derleme Islemi Basliyor...
echo ====================================================
echo.
echo Bu islem sirasinda arka planda Electron ve Node.js 
echo modulleri indirilebilir. Lutfen islem bitene kadar 
echo bu pencereyi KAPATMAYIN (yaklasik 2-3 dakika surebilir).
echo.

:: .NET surum uyumsuzlugunu onlemek icin Roll Forward ayarini yapiyoruz
set DOTNET_ROLL_FORWARD=Major

:: Web klasorune girip build aliyoruz
cd MesaiYonetimSistemi.Web
electronize build /target win

echo.
echo ====================================================
echo ISLEM TAMAMLANDI!
echo ====================================================
echo Masaustu .exe dosyasi su klasorde olusturuldu:
echo MesaiYonetimSistemi.Web\bin\Desktop\
echo.
echo O klasorun icindeki exe dosyasini tiklayarak 
echo tarayici olmadan direk kendi penceresiyle sistemi acabilirsiniz.
echo.
pause
