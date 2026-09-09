using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MesaiYonetimSistemi.Core.Entities;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: ApplicationDbContext.cs
// KATMAN: Infrastructure (Altyapı - Veritabanı Erişimi)
//
// Entity Framework Core'un veritabanı bağlam (DbContext) sınıfıdır.
// Bu sınıf:
//  - Veritabanı tablolarını temsil eden DbSet'leri tanımlar
//  - Entity'ler arasındaki ilişkileri (Foreign Key, Cascade Delete vb.) yapılandırır
//  - ASP.NET Core Identity tablolarını (Users, Roles, UserRoles vb.) otomatik oluşturur
//
// Miras Alınan Sınıf: IdentityDbContext<ApplicationUser, ApplicationRole, string>
//  - ApplicationUser: Özelleştirilmiş kullanıcı sınıfı
//  - ApplicationRole: Özelleştirilmiş rol sınıfı
//  - string: Birincil anahtar tipi (GUID formatında string)
//
// Veritabanı: SQLite (geliştirme ortamı için) – Program.cs'te yapılandırılır.
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Infrastructure.Data
{
    /// <summary>
    /// Uygulamanın ana veritabanı bağlamı.
    /// Tüm Entity Framework Core sorguları ve kayıt işlemleri bu sınıf üzerinden yapılır.
    /// Identity tablolarının yanı sıra uygulamaya özel tablolar da burada tanımlıdır.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        /// <summary>
        /// DI container tarafından program başlangıcında oluşturulur.
        /// options: Veritabanı bağlantı dizisi ve sağlayıcı (SQLite) bilgisini taşır.
        /// </summary>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ── Uygulama Tabloları (DbSet'ler) ──────────────────────────────────
        // Her DbSet bir veritabanı tablosuna karşılık gelir.
        // null! (null-forgiving operator): EF Core bu alanları otomatik set edeceğinden
        // nullable uyarısını bastırmak için kullanılır.

        /// <summary>Mesai kayıtları tablosu (Overtimes).</summary>
        public DbSet<Overtime> Overtimes { get; set; } = null!;

        /// <summary>Mesai teklifleri tablosu (OvertimeOffers). Adil sıra akışının kayıtları.</summary>
        public DbSet<OvertimeOffer> OvertimeOffers { get; set; } = null!;

        /// <summary>Adil sıra kuyruğu tablosu (QueueItems). Her personelin sıra pozisyonu burada.</summary>
        public DbSet<QueueItem> QueueItems { get; set; } = null!;

        /// <summary>Bildirimler tablosu (Notifications). Okundu/okunmadı takibi burada yapılır.</summary>
        public DbSet<Notification> Notifications { get; set; } = null!;

        /// <summary>Duyurular tablosu (Announcements). Yönetici tarafından oluşturulan ilanlar.</summary>
        public DbSet<Announcement> Announcements { get; set; } = null!;

        /// <summary>Mesai takas talepleri tablosu (OvertimeSwapRequests).</summary>
        public DbSet<OvertimeSwapRequest> OvertimeSwapRequests { get; set; } = null!;

        /// <summary>Personel izinleri tablosu (PersonnelLeaves).</summary>
        public DbSet<PersonnelLeave> PersonnelLeaves { get; set; } = null!;

        /// <summary>
        /// Entity ilişkilerini ve kısıtlamalarını (constraint) yapılandırır.
        /// Bu metot uygulama ilk başladığında veritabanı oluşturulurken EF Core tarafından çağrılır.
        /// base.OnModelCreating çağrısı Identity tablolarının da yapılandırılması için zorunludur.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Identity tablolarını yapılandır (Users, Roles, UserRoles, UserClaims vb.)
            base.OnModelCreating(builder);

            // ── ApplicationUser ↔ QueueItem İlişkisi ────────────────────────
            // Her kullanıcının en fazla 1 QueueItem'ı olabilir (1-1 ilişki).
            // Kullanıcı silinirse QueueItem'ı da silinir (Cascade).
            builder.Entity<ApplicationUser>(b =>
            {
                b.HasOne(u => u.QueueItem)
                 .WithOne(q => q.Personnel)
                 .HasForeignKey<QueueItem>(q => q.PersonnelId)  // QueueItem.PersonnelId FK olarak kullanılır
                 .OnDelete(DeleteBehavior.Cascade);              // Kullanıcı silinirse kuyruk kaydı da silinir
            });

            // ── OvertimeOffer İlişkileri ─────────────────────────────────────
            builder.Entity<OvertimeOffer>(b =>
            {
                // OvertimeOffer → Overtime (çoka-bir): Bir mesainin birden fazla teklifi olabilir
                b.HasOne(o => o.Overtime)
                 .WithMany(ov => ov.Offers)
                 .HasForeignKey(o => o.OvertimeId)
                 .OnDelete(DeleteBehavior.Cascade);  // Mesai silinirse teklifleri de silinir

                // OvertimeOffer → ApplicationUser (çoka-bir): Bir kişiye birden fazla teklif gidebilir
                b.HasOne(o => o.Personnel)
                 .WithMany(u => u.Offers)
                 .HasForeignKey(o => o.PersonnelId)
                 .OnDelete(DeleteBehavior.Restrict);  // Kullanıcı silinmeden önce teklifleri silinmeli
            });

            // ── Overtime → AtananUser (Atanan Personel) İlişkisi ────────────
            // Bir mesaiye en fazla 1 personel atanabilir.
            // Personel silinirse mesainin AtananUserId alanı NULL'a çekilir (SetNull).
            builder.Entity<Overtime>(b =>
            {
                b.HasOne(o => o.AtananUser)
                 .WithMany()                           // Navigation property yok (tersi tarafta koleksiyon yok)
                 .HasForeignKey(o => o.AtananUserId)
                 .OnDelete(DeleteBehavior.SetNull);    // Kullanıcı silinirse AtananUserId = NULL olur
            });

            // ── OvertimeSwapRequest İlişkileri ───────────────────────────────
            builder.Entity<OvertimeSwapRequest>(b =>
            {
                // Takas talebi → Mesai (çoka-bir): Bir mesaiye birden fazla takas talebi gelebilir
                b.HasOne(s => s.Overtime)
                 .WithMany(o => o.SwapRequests)
                 .HasForeignKey(s => s.OvertimeId)
                 .OnDelete(DeleteBehavior.Cascade);    // Mesai silinirse takas talepleri de silinir

                // Takas talebi → Talep Eden Kullanıcı
                b.HasOne(s => s.IstekYapanUser)
                 .WithMany()
                 .HasForeignKey(s => s.IstekYapanUserId)
                 .OnDelete(DeleteBehavior.Restrict);   // Kullanıcı silinmeden önce takas kayıtları temizlenmeli

                // Takas talebi → Hedef Kullanıcı
                b.HasOne(s => s.HedefUser)
                 .WithMany()
                 .HasForeignKey(s => s.HedefUserId)
                 .OnDelete(DeleteBehavior.Restrict);   // Aynı kısıtlama hedef için de geçerli
            });

            // ── PersonnelLeave İlişkileri ───────────────────────────────
            builder.Entity<PersonnelLeave>(b =>
            {
                // İzin → Personel (çoka-bir)
                b.HasOne(l => l.Personnel)
                 .WithMany()
                 .HasForeignKey(l => l.PersonnelId)
                 .OnDelete(DeleteBehavior.Cascade);    // Personel silinirse izinleri de silinsin

                // İzin → Oluşturan Yönetici (çoka-bir)
                b.HasOne(l => l.OlusturanUser)
                 .WithMany()
                 .HasForeignKey(l => l.OlusturanUserId)
                 .OnDelete(DeleteBehavior.Restrict);   // Yönetici silindiğinde izin kayıtları kalsın
            });
        }
    }
}
