using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Enums;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: DbSeeder.cs
// KATMAN: Infrastructure (Altyapı - Veritabanı Başlatma)
//
// Uygulama ilk kez çalıştırıldığında (veya veritabanı boşsa) çalışan
// otomatik veri yükleme (seed) sınıfıdır.
//
// Program.cs'te uygulama başladığında çağrılır: DbSeeder.SeedAsync(...)
//
// Bu sınıf şunları yapar:
//  1. Veritabanını oluşturur (yoksa)
//  2. Rolleri ekler: "Yonetici" ve "Personel"
//  3. İlk yönetici hesabını oluşturur
//  4. Örnek duyuruları ekler
//  5. 5 adet örnek personel ve adil sıra kayıtlarını ekler
//
// ÖNEMLİ: Bu metot idempotent'tir!
//  Yani defalarca çalıştırılsa bile aynı veriyi tekrar eklemez,
//  yalnızca eksik olanları tamamlar.
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Infrastructure.Data
{
    /// <summary>
    /// Veritabanı başlangıç verilerini (seed data) yükleyen statik yardımcı sınıf.
    /// Prodüksiyon ortamına geçişte bu dosyayı güncelleyerek
    /// kurumunuza özel başlangıç verilerini girebilirsiniz.
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>
        /// Veritabanını başlatır ve gerekli başlangıç verilerini yükler.
        /// Program.cs'teki using bloğu içinde uygulama başlangıcında çağrılır.
        /// </summary>
        /// <param name="context">EF Core veritabanı bağlamı</param>
        /// <param name="userManager">Identity kullanıcı yöneticisi</param>
        /// <param name="roleManager">Identity rol yöneticisi</param>
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            // Veritabanı dosyası yoksa oluştur
            await context.Database.EnsureCreatedAsync();

            // Otomatik Şema Güncellemesi: Yeni eklenen esnek mesai kolonları veritabanında yoksa tabloya ekle
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""Overtimes"" ADD COLUMN IF NOT EXISTS ""SaatBelirsizMi"" boolean NOT NULL DEFAULT FALSE;
                    ALTER TABLE ""Overtimes"" ADD COLUMN IF NOT EXISTS ""BildirilenBaslangicSaati"" interval NULL;
                    ALTER TABLE ""Overtimes"" ADD COLUMN IF NOT EXISTS ""BildirilenBitisSaati"" interval NULL;
                    ALTER TABLE ""Overtimes"" ADD COLUMN IF NOT EXISTS ""SaatBildirimDurumu"" integer NOT NULL DEFAULT 0;
                    ALTER TABLE ""Overtimes"" ADD COLUMN IF NOT EXISTS ""PersonelSaatNotu"" text NULL;
                ");
            }
            catch
            {
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Overtimes"" ADD COLUMN ""SaatBelirsizMi"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Overtimes"" ADD COLUMN ""BildirilenBaslangicSaati"" TEXT NULL;"); } catch { }
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Overtimes"" ADD COLUMN ""BildirilenBitisSaati"" TEXT NULL;"); } catch { }
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Overtimes"" ADD COLUMN ""SaatBildirimDurumu"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
                try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Overtimes"" ADD COLUMN ""PersonelSaatNotu"" TEXT NULL;"); } catch { }
            }

            // ── ADIM 1: ROL OLUŞTURMA ────────────────────────────────────────
            // Yonetici: Tüm ekranlara erişim, mesai oluşturma, sıra yönetimi, rapor
            // Personel: Kendi tekliflerini görme, kabul/red, takas talebi oluşturma
            if (!await roleManager.RoleExistsAsync("Yonetici"))
            {
                await roleManager.CreateAsync(new ApplicationRole("Yonetici", "Sistem Yöneticisi yetkileri"));
            }
            if (!await roleManager.RoleExistsAsync("Personel"))
            {
                await roleManager.CreateAsync(new ApplicationRole("Personel", "Saha ve Ofis Personeli yetkileri"));
            }

            // ── ADIM 2: YÖNETİCİ HESABI OLUŞTURMA ───────────────────────────
            // İlk çalıştırmada otomatik oluşturulan yönetici hesabı.
            // GİRİŞ BİLGİLERİ:
            //   E-posta : admin@bilimmerkezi.gov.tr
            //   Şifre   : Admin123!
            //
            // Farklı bir yönetici e-postası kullanmak için bu satırı değiştirin:
            var adminEmail = "admin@bilimmerkezi.gov.tr";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                // Önceki sürümlerde farklı e-postalar kullanılmış olabilir; kontrol et
                adminUser = await userManager.FindByEmailAsync("admin@belediye.gov.tr")
                         ?? await userManager.FindByEmailAsync("admin@mesai.com");
            }

            if (adminUser == null)
            {
                // Admin hesabı yok → oluştur
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    AdSoyad = "Bilim Merkezi Yöneticisi",
                    SicilNo = "ADM-001",
                    TcKimlikNo = "10000000000",
                    PhoneNumber = "05000000000",
                    Departman = "Yönetim",
                    Gorev = "Sistem Yöneticisi",
                    Sube = "Ana Hizmet Binası",
                    IlkSifre = "Admin123!",         // Düz metin referans (hash ayrıca tutulur)
                    IseGirisTarihi = DateTime.Now,
                    AktifMi = true,
                    EmailConfirmed = true            // E-posta doğrulama adımını atla
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Yonetici");
                }
            }
        }
    }
}
