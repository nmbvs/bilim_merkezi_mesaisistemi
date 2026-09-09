@echo off
chcp 65001 > nul
title Mesai Yönetim Sistemi

echo.
echo ====================================================
echo   Mesai Yönetim Sistemi Başlatılıyor...
echo ====================================================
echo.
echo Uygulama çalıştırılıyor, lütfen bekleyin...
echo Tarayıcı otomatik olarak açılacaktır (http://localhost:5051).
echo.

:: Tarayıcıyı 3 saniye sonra otomatik açmak için arka planda zamanlayıcı başlatıyoruz
start "" cmd /c "timeout /t 3 > nul && start http://localhost:5051"

:: ASP.NET Core projesini başlatıyoruz
dotnet run --project MesaiYonetimSistemi.Web

pause
