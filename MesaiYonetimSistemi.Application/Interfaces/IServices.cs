using MesaiYonetimSistemi.Application.DTOs;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MesaiYonetimSistemi.Application.Interfaces
{
    // ════════════════════════════════════════════════════════════════════════════
    // UYGULAMA SERVİS INTERFACE'LERİ
    // Bu dosya tüm uygulama katmanı servislerinin sözleşmelerini (contract) içerir.
    // Dependency Injection (DI) için somut implementasyonlar yerine bu interface'ler kullanılır.
    // Bu sayede servislerin test edilmesi, değiştirilmesi veya mock'lanması kolaylaşır.
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adil sıra (fair queue) yönetimi için servis sözleşmesi.
    /// Personellerin mesai teklifi alma sırasını ve sıra güncellemelerini yönetir.
    /// Implementasyon: QueueService (BasicServices.cs)
    /// </summary>
    public interface IQueueService
    {
        /// <summary>Filtrelenmiş ve sıralanmış kuyruk listesini döndürür.</summary>
        Task<IEnumerable<QueueDto>> GetQueueListAsync(string? department = null, string? sube = null);

        /// <summary>Mesai kabul eden personeli kuyruğun sonuna taşır (round-robin prensibi).</summary>
        Task MoveUserToBackOfQueueAsync(string personnelId);

        /// <summary>Yöneticinin belirlediği sırayı kaydeder (manuel sıralama).</summary>
        Task ReorderQueuePositionsAsync(List<string> orderedPersonnelIds);

        /// <summary>Tüm sıralamayı işe giriş tarihine göre sıfırdan oluşturur.</summary>
        Task ResetQueuePositionsAsync();
    }

    /// <summary>
    /// Mesai oluşturma, teklif akışı ve iptal işlemleri için servis sözleşmesi.
    /// Adil sıra algoritmasını kullanarak teklifleri otomatik iletir.
    /// Implementasyon: OvertimeService (OvertimeService.cs)
    /// </summary>
    public interface IOvertimeService
    {
        /// <summary>Tüm mesaileri teklif geçmişiyle birlikte döndürür.</summary>
        Task<IEnumerable<OvertimeDto>> GetAllOvertimesAsync();

        /// <summary>Belirli bir mesaiyi ID ile bulur ve detaylarıyla döndürür. Bulunamazsa null.</summary>
        Task<OvertimeDto?> GetOvertimeByIdAsync(int id);

        /// <summary>Yeni mesai oluşturur ve adil sıradaki ilk kişiye otomatik teklif gönderir.</summary>
        Task<OvertimeDto> CreateOvertimeAsync(CreateOvertimeDto dto);

        /// <summary>
        /// Personelin teklif yanıtını işler (kabul veya red).
        /// Kabul: çakışma kontrolü yapılır, personel atanır, kuyruğun sonuna taşınır.
        /// Red: sıradaki personele yeni teklif gönderilir.
        /// </summary>
        Task ProcessOfferResponseAsync(int offerId, bool isAccepted, string? rejectReason = null);

        /// <summary>
        /// Süresi dolmuş bekleyen teklifleri kapatır ve sonraki kişiye iletir.
        /// OvertimeOfferTimeoutBackgroundService tarafından her 60 saniyede bir çağrılır.
        /// </summary>
        Task ProcessExpiredOffersAsync();

        /// <summary>Belirli bir personelin süresi henüz dolmamış bekleyen tekliflerini döndürür.</summary>
        Task<IEnumerable<OfferDto>> GetPendingOffersForUserAsync(string personnelId);

        /// <summary>Sistemdeki tüm bekleyen teklifleri döndürür (yönetici özet ekranı için).</summary>
        Task<IEnumerable<OfferDto>> GetAllPendingOffersAsync();

        /// <summary>Mesaiyi iptal eder; aktif teklifleri kapatır ve atanan personele bildirim gönderir.</summary>
        Task CancelOvertimeAsync(int overtimeId, string reason);

        /// <summary>Saati belirsiz mesaide personelin gerçekleşen çalışma saatlerini bildirmesini sağlar.</summary>
        Task SubmitActualHoursAsync(SubmitOvertimeHoursDto dto, string personnelId);

        /// <summary>Yöneticinin bildirilen çalışma saatlerini onaylamasını sağlar (mesai Tamamlandi durumuna geçer).</summary>
        Task ApproveHoursAsync(int overtimeId, string managerUserId);

        /// <summary>Yöneticinin bildirilen çalışma saatlerini reddetmesini / düzeltme istemesini sağlar.</summary>
        Task RejectHoursAsync(int overtimeId, string managerUserId, string? reason);

        /// <summary>Saat onayı bekleyen saati belirsiz mesaileri döndürür (yönetici için).</summary>
        Task<IEnumerable<OvertimeDto>> GetPendingHoursApprovalOvertimesAsync();
    }

    /// <summary>
    /// Bildirim gönderme, listeleme ve okundu işaretleme için servis sözleşmesi.
    /// Bildirimler hem veritabanına kaydedilir hem de SignalR ile anlık iletilir.
    /// Implementasyon: NotificationService (BasicServices.cs)
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Belirli bir kullanıcıya veritabanı + SignalR kombinasyonuyla bildirim gönderir.</summary>
        Task SendNotificationAsync(string userId, string baslik, string mesaj, Core.Enums.NotificationType tip, string? relatedUrl = null);

        /// <summary>Bir roldeki tüm kullanıcılara yalnızca SignalR üzerinden anlık bildirim gönderir.</summary>
        Task SendNotificationToRoleAsync(string roleName, string baslik, string mesaj, Core.Enums.NotificationType tip);

        /// <summary>Kullanıcının tüm bildirimlerini en yeniden eskiye sıralı döndürür.</summary>
        Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId);

        /// <summary>Belirtilen bildirimi okundu olarak işaretler.</summary>
        Task MarkAsReadAsync(int notificationId);

        /// <summary>Kullanıcının tüm okunmamış bildirimlerini okundu yapar.</summary>
        Task MarkAllAsReadAsync(string userId);

        /// <summary>Kullanıcının okunmamış bildirim sayısını döndürür (navbar rozeti için).</summary>
        Task<int> GetUnreadCountAsync(string userId);
    }

    /// <summary>
    /// Dashboard verileri için servis sözleşmesi.
    /// Yönetici ve personel panelleri için ayrı metotlar içerir.
    /// Implementasyon: DashboardService (OtherServices.cs)
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>Yönetici paneli için istatistik, son mesailer ve grafik verilerini toplar.</summary>
        Task<DashboardDto> GetAdminDashboardDataAsync();

        /// <summary>Personel paneli için bekleyen teklifler, atanan mesailer ve duyuruları toplar.</summary>
        Task<DashboardDto> GetPersonnelDashboardDataAsync(string userId);
    }

    /// <summary>
    /// Dışa aktarma (Excel, HTML rapor) ve içe aktarma (Excel'den personel) işlemleri için servis sözleşmesi.
    /// Implementasyon: ExportService (Infrastructure/Services/ExportService.cs)
    /// </summary>
    public interface IExportService
    {
        /// <summary>Mesai listesini Excel byte dizisi olarak döndürür.</summary>
        Task<byte[]> ExportOvertimesToExcelAsync(IEnumerable<OvertimeDto> overtimes);

        /// <summary>Personel listesini Excel byte dizisi olarak döndürür.</summary>
        Task<byte[]> ExportPersonnelToExcelAsync(IEnumerable<UserDto> personnelList);

        /// <summary>Mesai raporunu HTML byte dizisi olarak döndürür (ilerleyen sürümde gerçek PDF planlanmaktadır).</summary>
        Task<byte[]> ExportOvertimeReportPdfAsync(IEnumerable<OvertimeDto> overtimes);

        /// <summary>Excel stream'inden personel listesi okuyup DTO listesi olarak döndürür.</summary>
        Task<List<ExcelUserImportDto>> ImportPersonnelFromExcelAsync(Stream fileStream);
    }

    /// <summary>
    /// Duyuru yönetimi için servis sözleşmesi.
    /// Implementasyon: AnnouncementService (OtherServices.cs)
    /// </summary>
    public interface IAnnouncementService
    {
        /// <summary>Yalnızca aktif duyuruları döndürür (personel görünümü).</summary>
        Task<IEnumerable<AnnouncementDto>> GetActiveAnnouncementsAsync();

        /// <summary>Tüm duyuruları (aktif + pasif) döndürür (yönetici görünümü).</summary>
        Task<IEnumerable<AnnouncementDto>> GetAllAnnouncementsAsync();

        /// <summary>Yeni duyuru oluşturur. Yayınlayan kişi ID'si parametre olarak verilir.</summary>
        Task AddAnnouncementAsync(AnnouncementDto dto, string authorUserId);

        /// <summary>Duyuruyu kalıcı olarak siler (hard delete).</summary>
        Task DeleteAnnouncementAsync(int id);
    }

    /// <summary>
    /// Personeller arası mesai takas (değişim) talepleri için servis sözleşmesi.
    /// Implementasyon: SwapRequestService (OtherServices.cs)
    /// </summary>
    public interface ISwapRequestService
    {
        /// <summary>
        /// Yeni takas talebi oluşturur. Yalnızca talep sahibinin atandığı mesai için geçerlidir.
        /// Hedef kişiye bildirim gönderilir.
        /// </summary>
        Task<OvertimeSwapDto> CreateSwapRequestAsync(int overtimeId, string requesterUserId, string targetUserId, string aciklama);

        /// <summary>
        /// Takas talebine yanıt verir.
        /// Onaylanırsa mesai hedef kişiye devredilir ve diğer bekleyen talepler otomatik iptal edilir.
        /// </summary>
        Task ProcessSwapResponseAsync(int swapRequestId, bool isApproved);

        /// <summary>Kullanıcının hedef olduğu bekleyen takas taleplerini döndürür.</summary>
        Task<IEnumerable<OvertimeSwapDto>> GetPendingSwapRequestsForUserAsync(string userId);
    }
}
