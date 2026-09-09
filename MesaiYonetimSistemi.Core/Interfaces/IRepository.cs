using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Core.Entities;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: IRepository.cs
// KATMAN: Core (Alan Modeli - Sözleşmeler / Contracts)
//
// Veritabanı erişim katmanının sözleşmelerini (interface) tanımlar.
// Bu interface'ler sayesinde:
//  - Uygulama katmanı somut veritabanı implementasyonuna bağımlı değildir
//  - Unit test yazarken mock repository'ler kolayca oluşturulabilir
//  - Veritabanı teknolojisi değişse bile (SQLite → PostgreSQL vb.) uygulama kodu değişmez
//
// Implementasyon: Infrastructure/Repositories/Repositories.cs
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Core.Interfaces
{
    /// <summary>
    /// Tüm entity türleri için temel CRUD (Create, Read, Update, Delete)
    /// operasyonlarını tanımlayan generic repository sözleşmesi.
    /// T: Herhangi bir entity sınıfı (Overtime, QueueItem, Notification vb.)
    /// </summary>
    public interface IRepository<T> where T : class
    {
        /// <summary>Integer birincil anahtar ile tek bir kayıt getirir. Bulunamazsa null döner.</summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>String birincil anahtar (GUID) ile tek bir kayıt getirir. ApplicationUser için kullanılır.</summary>
        Task<T?> GetByIdAsync(string id);

        /// <summary>Tablodaki tüm kayıtları liste olarak getirir. Büyük tablolar için dikkatli kullanın.</summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Belirtilen koşulu sağlayan tüm kayıtları getirir.
        /// Örnek: FindAsync(u => u.AktifMi) → Tüm aktif kullanıcılar
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Koşulu sağlayan tek bir kayıt getirir.
        /// Birden fazla kayıt bulunursa exception fırlatır.
        /// Hiç bulunamazsa null döner.
        /// </summary>
        Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Yeni bir kayıt ekler (henüz veritabanına yazmaz).
        /// SaveChangesAsync() çağrılana kadar yalnızca EF Core Change Tracker'da tutulur.
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>Birden fazla kaydı aynı anda ekler. Toplu import işlemlerinde kullanılır.</summary>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Mevcut bir kaydı günceller (henüz veritabanına yazmaz).
        /// SaveChangesAsync() çağrılana kadar yalnızca EF Core Change Tracker'da tutulur.
        /// </summary>
        void Update(T entity);

        /// <summary>Bir kaydı veritabanından siler (henüz yazmaz, SaveChangesAsync gerekir).</summary>
        void Remove(T entity);

        /// <summary>Birden fazla kaydı aynı anda siler. Toplu silme işlemlerinde kullanılır.</summary>
        void RemoveRange(IEnumerable<T> entities);

        /// <summary>
        /// Koşulu sağlayan kayıt sayısını döndürür.
        /// predicate null ise tüm tabloyu sayar.
        /// Örnek: CountAsync(u => u.AktifMi) → Aktif kullanıcı sayısı
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }

    /// <summary>
    /// Mesai (Overtime) entity'sine özgü gelişmiş sorgular için repository sözleşmesi.
    /// Temel IRepository&lt;Overtime&gt; metotlarına ek olarak Include işlemleri yapan sorgular içerir.
    /// Implementasyon: OvertimeRepository (Repositories.cs)
    /// </summary>
    public interface IOvertimeRepository : IRepository<Overtime>
    {
        /// <summary>
        /// Tüm mesaileri; atanan kullanıcı ve teklif geçmişiyle (tekliflerde personel bilgisi dahil)
        /// birlikte getirir. Overtime/Index sayfası için kullanılır.
        /// Sonuçlar tarihe göre azalan sırada gelir (en yeni önce).
        /// </summary>
        Task<IEnumerable<Overtime>> GetOvertimesWithOffersAsync();

        /// <summary>
        /// Belirli bir mesaiyi tüm detaylarıyla getirir:
        ///  - AtananUser (atanan personel bilgisi)
        ///  - Offers + Offers.Personnel (teklif geçmişi ve personel adları)
        ///  - SwapRequests + SwapRequests.IstekYapanUser (takas talepleri)
        /// Mesai detay/modal ekranları için kullanılır.
        /// </summary>
        Task<Overtime?> GetOvertimeDetailsAsync(int overtimeId);
    }

    /// <summary>
    /// Adil sıra kuyruğu (QueueItem) için özelleşmiş sorgu sözleşmesi.
    /// Sıralama ve yeniden numaralandırma işlemlerini içerir.
    /// Implementasyon: QueueRepository (Repositories.cs)
    /// </summary>
    public interface IQueueRepository : IRepository<QueueItem>
    {
        /// <summary>
        /// Aktif kuyruk kayıtlarını sıra numarasına göre (ascending) döndürür.
        /// İsteğe bağlı departman ve şube filtresi uygulanabilir.
        /// Yalnızca AktifMi=true olan QueueItem'lar ve Personnel'i aktif olanlar dahil edilir.
        /// </summary>
        Task<IEnumerable<QueueItem>> GetOrderedQueueAsync(string? department = null, string? sube = null);

        /// <summary>
        /// Belirli bir personelin kuyruk kaydını, personel bilgisiyle birlikte getirir.
        /// Sıra güncelleme ve istatistik artırma işlemlerinde kullanılır.
        /// </summary>
        Task<QueueItem?> GetQueueByPersonnelIdAsync(string personnelId);

        /// <summary>
        /// Tüm kuyruk kayıtlarını mevcut SiraPozisyonu sırasına göre 1'den başlayarak yeniden numaralandırır.
        /// Boşlukları kapatır (örn: 1,2,4,5 → 1,2,3,4).
        /// MoveUserToBackOfQueueAsync çağrısından sonra SaveChangesAsync ile birlikte kullanılır.
        /// NOT: Bu metot SaveChangesAsync'i çağırmaz; çağıran tarafın çağırması beklenir.
        /// </summary>
        Task ReorderQueueAsync();
    }

    public interface IUnitOfWork : IDisposable
    {
        /// <summary>Kullanıcı (ApplicationUser) tablosuna erişim. Personel sorgulama ve filtreleme için.</summary>
        IRepository<ApplicationUser> Users { get; }

        /// <summary>Mesai tablosuna erişim. Include'lu özel sorgular için IOvertimeRepository kullanın.</summary>
        IOvertimeRepository Overtimes { get; }

        /// <summary>Mesai teklifi tablosuna erişim.</summary>
        IRepository<OvertimeOffer> OvertimeOffers { get; }

        /// <summary>Adil sıra kuyruğu tablosuna erişim. Sıralama sorguları için IQueueRepository kullanın.</summary>
        IQueueRepository Queue { get; }

        /// <summary>Bildirim tablosuna erişim.</summary>
        IRepository<Notification> Notifications { get; }

        /// <summary>Duyuru tablosuna erişim.</summary>
        IRepository<Announcement> Announcements { get; }

        /// <summary>Mesai takas talepleri tablosuna erişim.</summary>
        IRepository<OvertimeSwapRequest> OvertimeSwapRequests { get; }

        /// <summary>Personel izinleri tablosuna erişim.</summary>
        IRepository<PersonnelLeave> PersonnelLeaves { get; }

        /// <summary>
        /// Tüm bekleyen Entity Framework Core değişikliklerini tek seferde veritabanına yazar.
        /// Etkilenen satır sayısını döndürür.
        /// DİKKAT: Her servis metodunda en az bir kez çağrılması gerekir; yoksa değişiklikler kaybolur.
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}
