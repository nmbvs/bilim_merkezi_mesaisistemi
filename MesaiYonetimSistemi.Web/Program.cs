using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using ElectronNET.API;
using ElectronNET.API.Entities;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Application.Mappings;
using MesaiYonetimSistemi.Application.Services;
using MesaiYonetimSistemi.Application.Validators;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Interfaces;
using MesaiYonetimSistemi.Infrastructure.BackgroundServices;
using MesaiYonetimSistemi.Infrastructure.Data;
using MesaiYonetimSistemi.Infrastructure.Hubs;
using MesaiYonetimSistemi.Infrastructure.Repositories;
using MesaiYonetimSistemi.Infrastructure.Services;
using MesaiYonetimSistemi.Web;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: Program.cs
// KATMAN: Web (Sunum Katmanı)
//
// Uygulamanın giriş noktası (entry point). Bu dosya:
//  1. Tüm servisleri Dependency Injection (DI) container'a kaydeder
//  2. Middleware pipeline'ını yapılandırır (kimlik doğrulama, yönlendirme vb.)
//  3. Uygulama başladığında veritabanını oluşturur ve örnek verileri ekler (DbSeeder)
//  4. SignalR Hub'ı ve Background Service'i başlatır
//
// Geliştirici Notu:
//  Yeni bir servis eklendiğinde hem interface'ini (IServices.cs'e) hem de
//  bu dosyadaki DI kaydını eklemeyi unutmayın!
// ════════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// Electron.NET Config
builder.WebHost.UseElectron(args);
builder.Services.AddElectron();

// ── EPPlus Lisans Yapılandırması ─────────────────────────────────────────────
// EPPlus kütüphanesi Excel üretimi için kullanılır.
// NonCommercial: Ticari olmayan (eğitim/kişisel) kullanım lisansı.
// Ticari kullanımda lisans satın alınması gerekir: https://epplussoftware.com
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

// ════════════════════════════════════════════════════════════════════════════════
// 1. VERİTABANI BAĞLANTISI (SQLite)
// ════════════════════════════════════════════════════════════════════════════════
// Bağlantı dizisi appsettings.json'dan okunur.
// appsettings.json'da "DefaultConnection" tanımlı değilse
// varsayılan olarak "Data Source=MesaiYonetimSistemi.db" kullanılır.
// Bu sayede veritabanı dosyası projeyi başlattığınız klasörde oluşur.
//
// SQLite'ı başka bir veritabanıyla (örn. SQL Server) değiştirmek için:
//   options.UseSqlite(connectionString)
//       → options.UseSqlServer(connectionString) yapın ve
//     Microsoft.EntityFrameworkCore.SqlServer NuGet paketini ekleyin.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ════════════════════════════════════════════════════════════════════════════════
// 2. IDENTITY YAPILANDIRMASI (Kimlik Doğrulama & Yetkilendirme)
// ════════════════════════════════════════════════════════════════════════════════
// ASP.NET Core Identity: Kullanıcı yönetimi, şifre hash'leme, rol yönetimi
// ApplicationUser: Özelleştirilmiş kullanıcı sınıfı (Core/Entities/ApplicationUser.cs)
// ApplicationRole: Özelleştirilmiş rol sınıfı (aynı dosyada)
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Şifre gereksinimleri – kolaylık için gevşetilmiş
    // Prodüksiyonda daha katı kurallar önerilebilir
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;              // En az 6 karakter
    options.User.RequireUniqueEmail = true;           // Her e-posta bir kez kullanılabilir
})
.AddEntityFrameworkStores<ApplicationDbContext>()    // Kullanıcı verilerini EF Core ile sakla
.AddErrorDescriber<TurkishIdentityErrorDescriber>()  // Hata mesajlarını Türkçe yap (Web/TurkishErrorDescriber.cs)
.AddDefaultTokenProviders();                          // Şifre sıfırlama token'ları için

// Oturum (cookie) yapılandırması
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";            // Giriş yapmamışsa bu sayfaya yönlendir
    options.AccessDeniedPath = "/Account/AccessDenied"; // Yetkisi yoksa bu sayfaya yönlendir
    options.ExpireTimeSpan = TimeSpan.FromHours(8); // Oturum 8 saat açık kalır
    options.SlidingExpiration = true;                // Her istekte süre yenilenir
});

// ════════════════════════════════════════════════════════════════════════════════
// 3. AUTOMAPPER & FLUENTVALIDATION
// ════════════════════════════════════════════════════════════════════════════════
// AutoMapper: Entity ↔ DTO dönüşümlerini otomatik yapar
// MappingProfile'da hangi alanların nasıl eşleşeceği tanımlıdır
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// FluentValidation: Form doğrulama kurallarını C# sınıflarında tanımlar
// Validators.cs'teki tüm validator'lar otomatik olarak DI'ya kaydedilir
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();
builder.Services.AddFluentValidationAutoValidation();

// ════════════════════════════════════════════════════════════════════════════════
// 4. REPOSITORY'LER & UNIT OF WORK
// ════════════════════════════════════════════════════════════════════════════════
// Repository Pattern: Veritabanı erişimini soyutlar
// Scoped: Her HTTP isteği için yeni bir instance oluşturulur ve istek sonunda dispose edilir
// Bu sayede aynı istek içinde aynı DbContext kullanılır (transaction tutarlılığı)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOvertimeRepository, OvertimeRepository>();
builder.Services.AddScoped<IQueueRepository, QueueRepository>();

// ════════════════════════════════════════════════════════════════════════════════
// 5. İŞ MANTIĞI SERVİSLERİ & SIGNALR HUB SERVİSİ
// ════════════════════════════════════════════════════════════════════════════════
// Her interface ↔ implementasyon eşleşmesi burada kayıt edilir.
// Yeni servis eklerken buraya da satır eklemeyi unutmayın!

// SignalR Hub Servisi: Gerçek zamanlı bildirim gönderimi (NotificationHub üzerinden)
builder.Services.AddScoped<INotificationHubService, NotificationHubService>();

// Adil Sıra Kuyruğu: Round-robin mesai sırası yönetimi
builder.Services.AddScoped<IQueueService, QueueService>();

// Mesai Servisi: Mesai oluşturma, teklif akışı, çakışma kontrolü
builder.Services.AddScoped<IOvertimeService, OvertimeService>();

// Bildirim Servisi: Veritabanı + SignalR kombine bildirim gönderimi
builder.Services.AddScoped<INotificationService, NotificationService>();

// Dashboard Servisi: Yönetici ve personel panel verilerini toplar
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Dışa Aktarma Servisi: Excel (.xlsx) ve HTML rapor üretimi
builder.Services.AddScoped<IExportService, ExportService>();

// Duyuru Servisi: Sistem duyuruları yönetimi
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();

// Takas Servisi: Personeller arası mesai değişim talepleri
builder.Services.AddScoped<ISwapRequestService, SwapRequestService>();

// İzin Servisi: Personel izin yönetimi ve kontrolü
builder.Services.AddScoped<ILeaveService, LeaveService>();

// ════════════════════════════════════════════════════════════════════════════════
// 6. SIGNALR & ARKA PLAN SERVİSİ
// ════════════════════════════════════════════════════════════════════════════════
// SignalR: Sunucu → Tarayıcı gerçek zamanlı WebSocket bağlantısı
builder.Services.AddSignalR();

// OvertimeOfferTimeoutBackgroundService:
//   Her 60 saniyede bir çalışır ve süresi dolmuş (24+ saat) teklifleri kapatır.
//   Kapatılan teklifler için sonraki kuyruktaki kişiye yeni teklif gönderilir.
builder.Services.AddHostedService<OvertimeOfferTimeoutBackgroundService>();

// MVC Controller'lar ve View'lar
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ════════════════════════════════════════════════════════════════════════════════
// VERİTABANI BAŞLATMA (Seeding)
// ════════════════════════════════════════════════════════════════════════════════
// Uygulama her başladığında DbSeeder.SeedAsync çağrılır:
//  - Veritabanı yoksa oluşturulur (EnsureCreatedAsync)
//  - Roller yoksa oluşturulur (Yonetici, Personel)
//  - Admin kullanıcısı yoksa oluşturulur (admin@bilimmerkezi.gov.tr / Admin123!)
//  - Örnek 5 personel ve duyurular yoksa eklenir
//
// Bu işlem idempotent'tir: mevcut veri tekrar eklenmez, yalnızca eksik olanlar eklenir.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        // Veritabanını oluştur ve başlangıç verilerini yükle
        DbSeeder.SeedAsync(context, userManager, roleManager).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        // Seeding başarısız olursa loglayarak devam et; uygulama durmasın
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "Veritabanı oluşturulurken hata oluştu.");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// MİDDLEWARE PIPELINE (HTTP İstek İşleme Zinciri)
// Sıralama önemlidir! Her middleware bir sonrakini çağırır.
// ════════════════════════════════════════════════════════════════════════════════

// Production'da hata sayfası ve HSTS (HTTP güvenlik başlığı)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // Beklenmeyen hatalar için özel hata sayfası
    app.UseHsts();                          // Tarayıcıya "sadece HTTPS kullan" talimatı
}

app.UseHttpsRedirection(); // HTTP → HTTPS yönlendirmesi
app.UseStaticFiles();      // wwwroot klasöründeki CSS, JS, resim dosyalarına erişim

app.UseRouting();          // URL eşleştirme kurallarını etkinleştir

app.UseAuthentication();   // Oturum cookie'sini kontrol et (kim giriş yapmış?)
app.UseAuthorization();    // Rol/yetki kontrolü ([Authorize], [Authorize(Roles="Yonetici")])

// SignalR Hub rotası – site.js'te "/notificationHub" adresiyle bağlantı kurulur
app.MapHub<NotificationHub>("/notificationHub");

// MVC rotası: varsayılan olarak Dashboard/Index açılır
// Giriş yapmamışsa Account/Login'e yönlendirilir (cookie ayarları nedeniyle)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Uygulamayı başlat ve gelen istekleri dinle
if (HybridSupport.IsElectronActive)
{
    // Electron penceresini oluştur
    Task.Run(async () =>
    {
        var window = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
        {
            Title = "Mesai Yönetim Sistemi",
            Width = 1280,
            Height = 800,
            Icon = "logo.png",
            Show = false,
            BackgroundColor = "#0b192c"
        });

        window.OnReadyToShow += () => window.Show();

        window.SetMenuBarVisibility(false); // Opsiyonel: Üstteki varsayılan menüyü gizler
        window.OnClosed += () => {
            Electron.App.Quit();
        };
    });
}

app.Run();
