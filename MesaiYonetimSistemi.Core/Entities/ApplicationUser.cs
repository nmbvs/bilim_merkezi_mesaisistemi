using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: ApplicationUser.cs
// KATMAN: Core (Alan Modeli)
//
// Sistemdeki kullanıcı (personel ve yönetici) modelini tanımlar.
// ASP.NET Core Identity'nin IdentityUser sınıfından türetilmiştir.
// Bu sayede şifre hash'leme, rol yönetimi, oturum açma gibi
// standart kimlik doğrulama özellikleri otomatik olarak gelir.
//
// Önemli: IdentityUser zaten şu alanları içerir:
//  - Id (GUID string), UserName, Email, PasswordHash,
//    PhoneNumber, EmailConfirmed, LockoutEnd vb.
//
// Aşağıdaki alanlar kuruma özel ek bilgilerdir.
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Core.Entities
{
    /// <summary>
    /// Sisteme giriş yapabilen her kullanıcıyı (personel veya yönetici) temsil eder.
    /// ASP.NET Core Identity ile entegre çalışır; kimlik doğrulama ve yetkilendirme
    /// bu sınıf üzerinden yönetilir.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        // ── Kişisel Bilgiler ─────────────────────────────────────────────────

        /// <summary>Personelin tam adı (örn: "Ahmet Yılmaz"). Görüntüleme ve bildirimler için kullanılır.</summary>
        public string AdSoyad { get; set; } = string.Empty;

        /// <summary>
        /// Personelin kurumsal sicil numarası (örn: "SCL-101").
        /// Sistemde eşsiz (unique) olması beklenir; raporlarda kimlik tanımlayıcı olarak kullanılır.
        /// </summary>
        public string SicilNo { get; set; } = string.Empty;

        /// <summary>
        /// TC Kimlik Numarası – 11 haneli.
        /// Validasyon için Validators.cs'te kontrol edilir.
        /// </summary>
        public string TcKimlikNo { get; set; } = string.Empty;

        // ── Kurumsal Bilgiler ────────────────────────────────────────────────

        /// <summary>
        /// Personelin çalıştığı departman (örn: "Bilim Merkezi", "İdari İşler").
        /// Mesai teklifleri bu bilgiye göre filtrelenerek gönderilir:
        /// Mesainin Departman alanı dolu ise sadece o departmandaki personele teklif gider.
        /// </summary>
        public string Departman { get; set; } = string.Empty;

        /// <summary>Personelin unvanı/görevi (örn: "Bilim Merkezi Eğitmeni", "Güvenlik").</summary>
        public string Gorev { get; set; } = string.Empty;

        /// <summary>
        /// Personelin çalıştığı bina/şube (örn: "Ana Bina", "B Blok").
        /// Departman filtresine ek olarak şube bazlı filtreleme de desteklenir.
        /// </summary>
        public string Sube { get; set; } = string.Empty;

        /// <summary>
        /// Personelin kurumda işe başladığı tarih.
        /// Adil sıra sıfırlanırken kıdem sırasına göre sıralama yapılır
        /// (en kıdemlisi = en uzun çalışan = 1. sıra).
        /// </summary>
        public DateTime IseGirisTarihi { get; set; } = DateTime.Now;

        // ── Sistem Durumu ────────────────────────────────────────────────────

        /// <summary>
        /// Personelin sistemdeki aktiflik durumu.
        /// false yapılırsa:
        ///  - Sisteme giriş yapamaz
        ///  - Adil sıra kuyruğundan çıkarılır (teklif almaz)
        ///  - Personel listesinde "Pasif" olarak görünür
        /// </summary>
        public bool AktifMi { get; set; } = true;

        /// <summary>
        /// Personele verilen ilk şifre (düz metin olarak saklanır – sadece referans için).
        /// Gerçek şifre IdentityUser.PasswordHash'te hash'li saklanır.
        /// NOT: Güvenlik gereği bu alan üretim ortamında boş bırakılabilir.
        /// </summary>
        public string IlkSifre { get; set; } = string.Empty;

        /// <summary>Hesabın sisteme eklendiği tarih ve saat.</summary>
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        // ── Navigasyon Özellikleri (EF Core İlişkileri) ─────────────────────
        // Bu özellikler Entity Framework Core tarafından lazy/eager loading için kullanılır.
        // Doğrudan veritabanında sütun oluşturmaz; JOIN işlemleri için yardımcı olur.

        /// <summary>
        /// Bu kullanıcının adil sıra kuyruğundaki kaydı.
        /// Her aktif personelin bir QueueItem'ı olması beklenir.
        /// Yönetici kullanıcısının QueueItem'ı olmayabilir.
        /// </summary>
        public virtual QueueItem? QueueItem { get; set; }

        /// <summary>Bu kullanıcıya gönderilmiş tüm mesai tekliflerinin koleksiyonu.</summary>
        public virtual ICollection<OvertimeOffer> Offers { get; set; } = new List<OvertimeOffer>();

        /// <summary>Bu kullanıcıya ait tüm sistem bildirimlerinin koleksiyonu.</summary>
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    /// <summary>
    /// Sistemdeki rol tanımlarını genişletilmiş şekilde temsil eder.
    /// ASP.NET Core Identity'nin IdentityRole sınıfından türetilmiştir.
    /// Mevcut Roller:
    ///  - "Yonetici": Tüm ekranlara erişim, mesai oluşturma, sıra yönetimi
    ///  - "Personel": Yalnızca kendi tekliflerini/mesailerini görme, takas talebi oluşturma
    /// Not: Rol adlarında Türkçe karakter kullanılmamıştır (örn: "Yönetici" değil "Yonetici")
    /// çünkü ASP.NET Core Identity route ve claim karşılaştırmalarında ASCII kullanır.
    /// </summary>
    public class ApplicationRole : IdentityRole
    {
        /// <summary>Rolün ne anlama geldiğini açıklayan ek bilgi metni.</summary>
        public string Aciklama { get; set; } = string.Empty;

        /// <summary>Parametresiz yapıcı – Entity Framework Core tarafından gereklidir.</summary>
        public ApplicationRole() : base() { }

        /// <summary>
        /// Rol adı ve açıklamasıyla birlikte oluşturur.
        /// DbSeeder'da roller oluşturulurken bu yapıcı kullanılır.
        /// </summary>
        /// <param name="roleName">Rolün adı (örn: "Yonetici")</param>
        /// <param name="aciklama">Rolün açıklaması (opsiyonel)</param>
        public ApplicationRole(string roleName, string aciklama = "") : base(roleName)
        {
            Aciklama = aciklama;
        }
    }
}
