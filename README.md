# Mesai Yönetim Sistemi (Masaüstü Sürüm)

> **Kocaeli Bilim Merkezi – Hafta Sonu & Nöbetçi Mesai Yönetim Sistemi**  
> ASP.NET Core 10 · **Neon PostgreSQL** · SignalR · Clean Architecture · **Electron.NET**

Bu proje en baştan itibaren web tabanlı yapılmış olup son aşamada tamamen **Neon PostgreSQL** veritabanına taşınmış ve Electron.NET teknolojisi ile Windows sistemlerinde çalışacak "**bağımsız masaüstü (.exe)**" hedefine yükseltilmiştir.

---

## 👩‍💻 KULLANICILAR İÇİN (Kurulum)

Eğer projeyi bir son kullanıcı olarak kullanacaksanız hiçbir şekilde kodlarla veya Visual Studio ile uğraşmanıza gerek yoktur. Sizin için hazırlanmış **her şey dahil kurulum dosyasını** kullanabilirsiniz.

1. Projenin ana klasöründeki veya size gönderilen **`bilim merkezi mesai Kurulum.exe`** dosyasına çift tıklayın.
2. Program kendini masaüstüne çıkarıp, kısa yolunu kuracaktır.
3. Çalıştırdığınız anda tüm sistem Neon PostgreSQL bulut altyapısı sayesinde senkronize bir şekilde çalışmaya başlayacaktır.

---

## 🛠️ GELİŞTİRİCİLER İÇİN (Developer Kılavuzu)

Eğer kaynak kodu düzenleyecek veya yeni özellikler ekleyecek bir yazılımcıysanız, bilmeniz gereken detaylar aşağıdadır:

### 1. Sistem Gereksinimleri
- **.NET 10 SDK** veya üzerindeki uygun sürümler
- **Node.js** (Electron.NET derlemesi için arka planda paketleri kullanır)
- **ElectronNET.CLI** (Küresel olarak `dotnet tool install --global ElectronNET.CLI` ile kurulmalı).

### 2. Alt Yapı ve Veritabanı
- **Veritabanı:** SQLite'tan tamamen Bulut **Neon PostgreSQL** sistemine geçilmiştir. 
- **Connection String:** `MesaiYonetimSistemi.Web/appsettings.json` içerisindedir. Sisteme müdahale ederken veya veritabanı ayarlarını güncellerken bu dosyayı kullanabilirsiniz.
- **ORM:** Entity Framework Core (PostgreSQL Provider)

### 3. Kullanıcı Bilgileri
Uygulamayı test etmek isterseniz geliştirme aşaması için tanımlı:
- E-posta: `admin@bilimmerkezi.gov.tr`
- Şifre: `Admin123!`

### 4. Projeyi Düzenleyip Yeniden ".exe" Almak
Varsayalım ki `.cshtml` dosyalarında bir arayüz düzenlemesi yaptınız veya yeni bir buton eklediniz. Bu değişikliğin masaüstü (.exe) versiyonuna yansıması için terminalden projeyi tekrar Electron ile paketlemeniz gerekir. 

Bunun için terminal veya powershell ekranını `MesaiYonetimSistemi.Web` klasörünün içinde açıp şu komutu çalıştırabilirsiniz:
```powershell
$env:DOTNET_ROLL_FORWARD="Major"; electronize build /target win
```
*(Eğer ".bat" dosyasıyla çalışmayı seviyorsanız ana dizindeki `electron_yap.bat` dosyasını da kullanabilirsiniz)*

Bu komut başarıyla tamamlandığında, `MesaiYonetimSistemi.Web\bin\Desktop\` yolunda yepyeni "**bilim merkezi mesai Setup X.X.X.exe**" dosyanız hazır olacaktır.

### 5. Masaüstü Simgesi (Logosu)
* Varsayılan logoyu değiştirmek isterseniz `MesaiYonetimSistemi.Web/logo.png` dosyasını kendi özel resminiz ile değiştirip derlediğinizde uygulama her yerde bu logoyla çıkar. `Program.cs` içerisindeki `BrowserWindowOptions.Icon` tanımlaması ve `electron.manifest.json` dosyası da bu logoya bağlanmıştır.
