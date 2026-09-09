using System;
using System.Collections.Generic;
using MesaiYonetimSistemi.Core.Enums;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: DomainEntities.cs
// KATMAN: Core (Alan Modeli - Domain Layer)
//
// Bu dosya sistemin temel veri modellerini (entity) içerir.
// Buradaki sınıflar veritabanı tablolarına karşılık gelir ve
// uygulama mantığının merkezinde yer alır.
//
// Mimari Not: Core katmanı hiçbir dış kütüphane veya servise bağımlı değildir.
// Yalnızca Microsoft.AspNetCore.Identity referansı ApplicationUser için kullanılır.
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Core.Entities
{
    // ─────────────────────────────────────────────────────────────────────────
    // MESAİ KAYDI
    // Veritabanı Tablosu: Overtimes
    // Hafta sonu veya nöbetçi mesai ilanını temsil eder.
    // Bir mesai oluşturulduğunda, sistem adil sıra algoritmasıyla
    // otomatik olarak sıradaki personele teklif gönderir.
    // ─────────────────────────────────────────────────────────────────────────
    public class Overtime
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Mesainin yapılacağı tarih (gün bilgisi).</summary>
        public DateTime Tarih { get; set; }

        /// <summary>Mesai başlangıç saati (örn: 08:00).</summary>
        public TimeSpan BaslangicSaati { get; set; }

        /// <summary>Mesai bitiş saati (örn: 17:00).</summary>
        public TimeSpan BitisSaati { get; set; }

        /// <summary>Mesai hakkında ek açıklama/not bilgisi.</summary>
        public string Aciklama { get; set; } = string.Empty;

        /// <summary>Kaç kişinin bu mesaiye alınabileceği (şu an 1 kişilik sistem).</summary>
        public int Kontenjan { get; set; } = 1;

        /// <summary>Mesainin ait olduğu departman (örn: "Bilim Merkezi").</summary>
        public string Departman { get; set; } = string.Empty;

        /// <summary>Mesainin ait olduğu şube/bina (örn: "Ana Bina").</summary>
        public string Sube { get; set; } = string.Empty;

        /// <summary>Mesai resmi tatil gününe denk geliyor mu? Ekstra ücret hesabı için kullanılır.</summary>
        public bool ResmiTatilMi { get; set; } = false;

        /// <summary>Mesai saatlerinin net/belirli olup olmadığı (true ise personel mesai sonrası saat bildirir).</summary>
        public bool SaatBelirsizMi { get; set; } = false;

        /// <summary>Saati belirsiz mesailer için personelin bildirdiği başlangıç saati.</summary>
        public TimeSpan? BildirilenBaslangicSaati { get; set; }

        /// <summary>Saati belirsiz mesailer için personelin bildirdiği bitiş saati.</summary>
        public TimeSpan? BildirilenBitisSaati { get; set; }

        /// <summary>Personelin saat bildirim ve yönetici onay durumu.</summary>
        public SaatBildirimStatus SaatBildirimDurumu { get; set; } = SaatBildirimStatus.Yok;

        /// <summary>Personelin saat bildirimi yaparken eklediği açıklama/not.</summary>
        public string? PersonelSaatNotu { get; set; }

        /// <summary>
        /// Mesainin güncel durumu:
        /// Planlandi = Henüz yapılmadı/atama bekleniyor
        /// Tamamlandi = Mesai gerçekleşti
        /// IptalEdildi = Yönetici tarafından iptal edildi
        /// </summary>
        public OvertimeStatus Durum { get; set; } = OvertimeStatus.Planlandi;

        /// <summary>Mesainin sisteme girildiği tarih ve saat (otomatik set edilir).</summary>
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        /// <summary>Mesaiyi oluşturan yöneticinin kullanıcı ID'si.</summary>
        public string OlusturanUserId { get; set; } = string.Empty;

        // ── Atanan Personel Bilgisi ──────────────────────────────────────────
        // Bir personel teklifi kabul ettiğinde bu alan doldurulur.
        // Henüz kimse kabul etmemişse null olur.

        /// <summary>Mesaiye atanan personelin kullanıcı ID'si. Teklif kabul edilene kadar null.</summary>
        public string? AtananUserId { get; set; }

        /// <summary>Atanan personelin navigasyon referansı (EF Core lazy/eager loading için).</summary>
        public virtual ApplicationUser? AtananUser { get; set; }

        // ── İlişkili Koleksiyonlar ───────────────────────────────────────────

        /// <summary>Bu mesai için gönderilen tüm tekliflerin listesi (sıralı teklif akışı).</summary>
        public virtual ICollection<OvertimeOffer> Offers { get; set; } = new List<OvertimeOffer>();

        /// <summary>Bu mesai için oluşturulmuş takas (değişim) taleplerinin listesi.</summary>
        public virtual ICollection<OvertimeSwapRequest> SwapRequests { get; set; } = new List<OvertimeSwapRequest>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MESAİ TEKLİFİ
    // Veritabanı Tablosu: OvertimeOffers
    // Adil sıra algoritması her mesai için sırayla teklifler oluşturur.
    // Personel kabul ederse mesai ona atanır; reddederse bir sonraki kişiye iletilir.
    // ─────────────────────────────────────────────────────────────────────────
    public class OvertimeOffer
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Hangi mesaiye ait olduğunu gösteren yabancı anahtar.</summary>
        public int OvertimeId { get; set; }

        /// <summary>İlgili mesai kaydının navigasyon referansı.</summary>
        public virtual Overtime? Overtime { get; set; }

        /// <summary>Teklifin gönderildiği personelin kullanıcı ID'si.</summary>
        public string PersonnelId { get; set; } = string.Empty;

        /// <summary>İlgili personelin navigasyon referansı.</summary>
        public virtual ApplicationUser? Personnel { get; set; }

        /// <summary>Teklifin sisteme gönderildiği tarih ve saat.</summary>
        public DateTime TeklifTarihi { get; set; } = DateTime.Now;

        /// <summary>
        /// Personelin yanıt vermesi için son tarih.
        /// Varsayılan: TeklifTarihi + 24 saat.
        /// Bu süre dolduğunda OvertimeOfferTimeoutBackgroundService teklifi "ZamanAsimi" yapar.
        /// </summary>
        public DateTime SonCevapTarihi { get; set; } = DateTime.Now.AddHours(24);

        /// <summary>
        /// Teklifin güncel durumu:
        /// Bekliyor = Personel henüz yanıtlamadı
        /// KabulEdildi = Personel kabul etti → Mesai atandı
        /// Reddedildi = Personel reddetti → Sonraki kişiye iletildi
        /// ZamanAsimi = 24 saat içinde yanıt verilmedi
        /// IptalEdildi = Mesai iptal edildi
        /// </summary>
        public OfferStatus Durum { get; set; } = OfferStatus.Bekliyor;

        /// <summary>
        /// Adil sıra akışındaki kaçıncı teklif olduğunu gösterir.
        /// 1 = İlk teklif gönderilen kişi, 2 = İkinci, vb.
        /// </summary>
        public int SiraNo { get; set; }

        /// <summary>Personelin yanıt verdiği tarih ve saat. Henüz yanıt verilmediyse null.</summary>
        public DateTime? CevapTarihi { get; set; }

        /// <summary>Personel reddettiyse girdiği mazeret açıklaması. Opsiyoneldir.</summary>
        public string? RedNedeni { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KUYRUK KAYDI (ADİL SIRA)
    // Veritabanı Tablosu: QueueItems
    // Her aktif personelin adil sıra listesindeki pozisyonunu ve istatistiklerini tutar.
    // Mesai kabul eden personel kuyruğun en sonuna taşınır (round-robin).
    // ─────────────────────────────────────────────────────────────────────────
    public class QueueItem
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Bu kuyruk kaydının ait olduğu personelin kullanıcı ID'si.</summary>
        public string PersonnelId { get; set; } = string.Empty;

        /// <summary>İlgili personelin navigasyon referansı.</summary>
        public virtual ApplicationUser? Personnel { get; set; }

        /// <summary>
        /// Personelin sıradaki pozisyonu (1 = en önde).
        /// Düşük sayı = teklif önce bu kişiye gider.
        /// Kabul sonrası bu değer en yükseğe çıkar (sona taşınır).
        /// </summary>
        public int SiraPozisyonu { get; set; }

        /// <summary>
        /// Personelin en son hangi mesaiyi yaptığı tarihi gösterir.
        /// Adil sıra sıfırlanırken bu bilgi referans alınır.
        /// Henüz mesai yapmadıysa null olur.
        /// </summary>
        public DateTime? SonMesaiTarihi { get; set; }

        /// <summary>Personelin sistem genelinde kaç mesai teklifini kabul ettiği.</summary>
        public int ToplamKabulEdilenMesai { get; set; } = 0;

        /// <summary>
        /// Personelin kaç teklifi reddettiği veya zaman aşımına uğrattığı.
        /// Hem manuel red hem de 24 saatlik zaman aşımı bu sayacı artırır.
        /// </summary>
        public int ToplamReddedilenMesai { get; set; } = 0;

        /// <summary>
        /// Personel aktif sırada mı?
        /// Pasif yapılan personeller (izin, işten ayrılma vb.) sıradan çıkarılır
        /// ve teklif göndermezden önce bu alan kontrol edilir.
        /// </summary>
        public bool AktifMi { get; set; } = true;

        /// <summary>Bu kayıt en son ne zaman güncellendiği (sıralama değişikliği, kabul/red vb.).</summary>
        public DateTime SonGuncellemeTarihi { get; set; } = DateTime.Now;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BİLDİRİM
    // Veritabanı Tablosu: Notifications
    // Personele ve yöneticilere gönderilen sistem bildirimleri.
    // Hem veritabanında kalıcı olarak saklanır hem de SignalR ile anlık iletilir.
    // ─────────────────────────────────────────────────────────────────────────
    public class Notification
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Bildirimin gönderildiği kullanıcının ID'si.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>İlgili kullanıcının navigasyon referansı.</summary>
        public virtual ApplicationUser? User { get; set; }

        /// <summary>Bildirim başlığı (kısa, öz, anlaşılır olmalı).</summary>
        public string Baslik { get; set; } = string.Empty;

        /// <summary>Bildirim içeriği (detaylı açıklama metni).</summary>
        public string Mesaj { get; set; } = string.Empty;

        /// <summary>Kullanıcı bu bildirimi görüntüledi mi? Navbar rozeti için kullanılır.</summary>
        public bool OkunduMu { get; set; } = false;

        /// <summary>Bildirimin oluşturulduğu tarih ve saat.</summary>
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        /// <summary>
        /// Bildirim tipi (ikonlar ve renkler bu tipe göre belirlenir):
        /// MesaiTeklifi = Yeni teklif geldi
        /// TeklifSonucu = Kabul/red bildirimi
        /// Duyuru = Genel sistem duyurusu
        /// Sistem = Sistem tarafından oluşturuldu
        /// MesaiDegisim = Takas talebi bildirimi
        /// </summary>
        public NotificationType Tip { get; set; } = NotificationType.Sistem;

        /// <summary>
        /// Bildirimi tıklayınca yönlendirilecek URL (örn: "/Overtime/PendingOffers").
        /// Null ise bildirimde bağlantı gösterilmez.
        /// </summary>
        public string? RelatedUrl { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DUYURU
    // Veritabanı Tablosu: Announcements
    // Yöneticiler tarafından tüm personele yönelik yapılan sistem duyuruları.
    // ─────────────────────────────────────────────────────────────────────────
    public class Announcement
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Duyuru başlığı.</summary>
        public string Baslik { get; set; } = string.Empty;

        /// <summary>Duyuru içerik metni (HTML desteklenebilir).</summary>
        public string Icerik { get; set; } = string.Empty;

        /// <summary>Duyurunun yayınlandığı tarih ve saat.</summary>
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        /// <summary>Duyuruyu yayınlayan yöneticinin kullanıcı ID'si.</summary>
        public string YayinlayanUserId { get; set; } = string.Empty;

        /// <summary>Yayınlayan yöneticinin navigasyon referansı.</summary>
        public virtual ApplicationUser? YayinlayanUser { get; set; }

        /// <summary>Duyuru aktif mi? false ise personel panelinde görüntülenmez.</summary>
        public bool AktifMi { get; set; } = true;

        /// <summary>Öncelikli duyurular sayfanın üstünde ve farklı renkte gösterilir.</summary>
        public bool OncelikliMi { get; set; } = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TAKAS TALEBİ
    // Veritabanı Tablosu: OvertimeSwapRequests
    // Kendisine mesai atanmış bir personelin, bu mesaini başka bir personelle
    // takas etmek istediği durumlarda oluşturulur.
    //
    // Akış:
    //  1. Personel A → Personel B'ye takas talebi gönderir
    //  2. Personel B bildirim alır, onaylar veya reddeder
    //  3. Onaylanırsa: Mesai Personel B'ye devredilir
    //     (ve aynı mesai için varsa diğer bekleyen talepler otomatik iptal edilir)
    // ─────────────────────────────────────────────────────────────────────────
    public class OvertimeSwapRequest
    {
        /// <summary>Birincil anahtar – otomatik artar.</summary>
        public int Id { get; set; }

        /// <summary>Takas talep edilen mesainin ID'si.</summary>
        public int OvertimeId { get; set; }

        /// <summary>İlgili mesainin navigasyon referansı.</summary>
        public virtual Overtime? Overtime { get; set; }

        /// <summary>Takas talebini başlatan (talep eden) personelin kullanıcı ID'si.</summary>
        public string IstekYapanUserId { get; set; } = string.Empty;

        /// <summary>Takas talebini başlatan personelin navigasyon referansı.</summary>
        public virtual ApplicationUser? IstekYapanUser { get; set; }

        /// <summary>Takasın teklif edildiği hedef personelin kullanıcı ID'si.</summary>
        public string HedefUserId { get; set; } = string.Empty;

        /// <summary>Hedef personelin navigasyon referansı.</summary>
        public virtual ApplicationUser? HedefUser { get; set; }

        /// <summary>
        /// Takas talebinin güncel durumu:
        /// Bekliyor = Hedef personel henüz yanıtlamadı
        /// Onaylandi = Hedef kabul etti → Mesai devredildi
        /// Reddedildi = Hedef reddetti
        /// IptalEdildi = Başka bir takas kabul edilince otomatik iptal
        /// </summary>
        public SwapStatus Durum { get; set; } = SwapStatus.Bekliyor;

        /// <summary>Talep sahibinin eklediği açıklama/mesaj (örn: "Bu gün müsait değilim...").</summary>
        public string Aciklama { get; set; } = string.Empty;

        /// <summary>Takas talebinin oluşturulduğu tarih ve saat.</summary>
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        /// <summary>Hedef personelin yanıt verdiği tarih ve saat. Henüz yanıt verilmediyse null.</summary>
        public DateTime? YanitTarihi { get; set; }
    }
}
