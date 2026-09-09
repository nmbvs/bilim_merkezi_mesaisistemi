using System.Threading.Tasks;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: INotificationHubService.cs
// KATMAN: Core (Alan Modeli - Sözleşmeler)
//
// SignalR üzerinden gerçek zamanlı bildirim gönderme işleminin sözleşmesini tanımlar.
//
// Neden Core katmanında?
//  Core katmanı dış bağımlılıklardan bağımsız olmalıdır.
//  SignalR'ın somut implementasyonu Infrastructure katmanındadır (NotificationHubService.cs),
//  ancak sözleşme Core'da tutularak Application katmanı bu interface'e bağımlı olabilir.
//  Bu sayede Application katmanı SignalR'a doğrudan bağımlı olmaz.
//
// Implementasyon: Infrastructure/Hubs/NotificationHubService.cs
// ════════════════════════════════════════════════════════════════════════════════

namespace MesaiYonetimSistemi.Core.Interfaces
{
    /// <summary>
    /// SignalR üzerinden anlık bildirim gönderimini soyutlayan interface.
    /// NotificationService (BasicServices.cs) tarafından kullanılır.
    ///
    /// SignalR Çalışma Mantığı:
    ///  - Her kullanıcı bağlandığında "User_{userId}" grubuna katılır
    ///  - Yöneticiler aynı zamanda "Role_Yonetici" grubuna da katılır
    ///  - Bildirim gönderilirken bu grup isimleri hedef olarak kullanılır
    ///  - İstemci tarafında site.js'deki ReceiveNotification olayı tetiklenir
    /// </summary>
    public interface INotificationHubService
    {
        /// <summary>
        /// Belirli bir kullanıcıya ait tüm bağlı istemcilere (tarayıcı sekmeleri dahil)
        /// anlık bildirim gönderir.
        /// Hedef Grup: "User_{userId}"
        /// İstemci Olayı: "ReceiveNotification"
        /// </summary>
        /// <param name="userId">Bildirimi alacak kullanıcının GUID ID'si.</param>
        /// <param name="payload">Gönderilecek veri nesnesi (baslik, mesaj, tip, relatedUrl vb.).</param>
        Task SendUserNotificationAsync(string userId, object payload);

        /// <summary>
        /// Belirli bir roldeki tüm bağlı kullanıcılara anlık bildirim gönderir.
        /// Örn: "Yonetici" rolündeki tüm aktif oturumlara aynı anda bildirim gönderir.
        /// Hedef Grup: "Role_{roleName}"
        /// İstemci Olayı: "ReceiveNotification"
        /// NOT: Bu metot veritabanına bildirim kaydetmez; yalnızca anlık push gönderir.
        /// </summary>
        /// <param name="roleName">Bildirimi alacak rolün adı (örn: "Yonetici").</param>
        /// <param name="payload">Gönderilecek veri nesnesi.</param>
        Task SendRoleNotificationAsync(string roleName, object payload);
    }
}
