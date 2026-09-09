// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: DomainEnums.cs
// KATMAN: Core (Alan Modeli)
//
// Sistemdeki tüm enum (sabit değer listeleri) tanımlamalarını içerir.
// Enum'lar; veritabanında integer olarak saklanır, kodda anlamlı isimlerle kullanılır.
//
// UYARI: Enum değerlerinin sayısal karşılıklarını (= 0, = 1, vb.) asla değiştirmeyin!
// Veritabanında mevcut kayıtlar bu sayısal değerlere göre eşleştirilir.
// Yeni değer eklenecekse en sona ekleyin.
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Core.Enums
{
    /// <summary>
    /// Bir mesai teklifinin (OvertimeOffer) olası durumları.
    /// Adil sıra akışı bu durum makinesi üzerinden ilerler.
    ///
    /// Durum Geçişleri:
    ///   Bekliyor → KabulEdildi  (Personel kabul etti)
    ///   Bekliyor → Reddedildi   (Personel reddetti)
    ///   Bekliyor → ZamanAsimi   (24 saat içinde yanıt verilmedi – Background service tarafından)
    ///   Bekliyor → IptalEdildi  (Mesai iptal edildi – CancelOvertimeAsync tarafından)
    /// </summary>
    public enum OfferStatus
    {
        /// <summary>Teklif gönderildi, personel henüz yanıtlamadı. Saydaç işliyor.</summary>
        Bekliyor = 0,

        /// <summary>Personel teklifi kabul etti. Mesai bu kişiye atandı.</summary>
        KabulEdildi = 1,

        /// <summary>Personel teklifi reddetti. Bir sonraki kişiye yeni teklif gönderildi.</summary>
        Reddedildi = 2,

        /// <summary>
        /// 24 saatlik yanıt süresi doldu, personel yanıt vermedi.
        /// OvertimeOfferTimeoutBackgroundService tarafından her dakika kontrol edilir.
        /// Red gibi sayılır: ToplamReddedilenMesai artar.
        /// </summary>
        ZamanAsimi = 3,

        /// <summary>Bağlı olduğu mesai iptal edildiğinde tüm bekleyen teklifler bu duruma alınır.</summary>
        IptalEdildi = 4
    }

    /// <summary>
    /// Bir mesainin (Overtime) olası durumları.
    /// Mesai oluşturulduğunda "Planlandi" olarak başlar.
    ///
    /// Durum Geçişleri:
    ///   Planlandi → Tamamlandi  (Yönetici mesainin yapıldığını onaylar)
    ///   Planlandi → IptalEdildi (Yönetici iptal eder)
    /// </summary>
    public enum OvertimeStatus
    {
        /// <summary>
        /// Mesai planlandı ve aktif durumda.
        /// Henüz yapılmadı; teklif süreci devam ediyor ya da personel atandı, mesai bekleniyor.
        /// </summary>
        Planlandi = 0,

        /// <summary>Mesai gerçekleşti ve tamamlandı. Artık düzenlenemez.</summary>
        Tamamlandi = 1,

        /// <summary>Mesai yönetici tarafından iptal edildi. Tüm aktif teklifler de iptal edildi.</summary>
        IptalEdildi = 2
    }

    /// <summary>
    /// Personeller arası mesai takas talebinin (OvertimeSwapRequest) olası durumları.
    ///
    /// Durum Geçişleri:
    ///   Bekliyor → Onaylandi   (Hedef personel kabul etti; mesai devredildi)
    ///   Bekliyor → Reddedildi  (Hedef personel reddetti)
    ///   Bekliyor → IptalEdildi (Başka bir takas talebi onaylanınca bu otomatik iptal olur)
    /// </summary>
    public enum SwapStatus
    {
        /// <summary>Takas talebi gönderildi, hedef personel henüz yanıtlamadı.</summary>
        Bekliyor = 0,

        /// <summary>Hedef personel kabul etti. Mesai artık hedef kişiye ait.</summary>
        Onaylandi = 1,

        /// <summary>Hedef personel reddetti. Mevcut atama değişmedi.</summary>
        Reddedildi = 2,

        /// <summary>
        /// Aynı mesai için başka bir takas onaylanınca bu talep otomatik iptal edilir.
        /// (Madde 6 düzeltmesiyle eklendi: çifte kabul önleme mekanizması)
        /// </summary>
        IptalEdildi = 3
    }

    /// <summary>
    /// Bildirim türleri. Navbar'da ikon ve renk seçimi bu tipe göre yapılır.
    /// SignalR mesajlarında string olarak iletilir (ToString() ile dönüştürülür).
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Yeni bir mesai teklifi geldi. Sarı/turuncu ikon ile gösterilir.</summary>
        MesaiTeklifi = 0,

        /// <summary>Gönderilen teklifin sonucu (kabul/red/zaman aşımı). Yeşil/kırmızı ikon ile gösterilir.</summary>
        TeklifSonucu = 1,

        /// <summary>Yönetici tarafından yayınlanan genel duyuru. Mavi ikon ile gösterilir.</summary>
        Duyuru = 2,

        /// <summary>Sistem tarafından oluşturulan otomatik bildirim (iptal, uyarı vb.). Gri ikon ile gösterilir.</summary>
        Sistem = 3,

        /// <summary>Mesai takas talebi ile ilgili bildirim (gönderildi, onaylandı, reddedildi). Mor ikon ile gösterilir.</summary>
        MesaiDegisim = 4
    }

    /// <summary>
    /// Saatleri belirsiz mesailerde personelin fiili saat bildirimi durumları.
    /// </summary>
    public enum SaatBildirimStatus
    {
        /// <summary>Henüz saat bildirimi yapılmamış veya mesai saati baştan belirli.</summary>
        Yok = 0,

        /// <summary>Personel gerçekleşen çalışma saatlerini bildirdi, yönetici onayı bekleniyor.</summary>
        OnayBekliyor = 1,

        /// <summary>Yönetici bildirilen saatleri onayladı ve mesai tamamlandı.</summary>
        Onaylandi = 2,

        /// <summary>Yönetici bildirilen saatleri reddetti / düzeltme istedi.</summary>
        Reddedildi = 3
    }
}
