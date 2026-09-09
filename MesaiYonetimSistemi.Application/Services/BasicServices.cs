using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Enums;
using MesaiYonetimSistemi.Core.Interfaces;

namespace MesaiYonetimSistemi.Application.Services
{
    // ════════════════════════════════════════════════════════════════════════════
    // KUYRUK SERVİSİ
    // Adil mesai sırasını (fair queue) yöneten servis.
    // Personeller sıra numarasına göre mesai teklifleri alır.
    // Kabul eden personel kuyruğun sonuna taşınır (round-robin prensibi).
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adil sıra (fair queue) yönetimini sağlar.
    /// Sıra yönetimi kuralları:
    ///  - En düşük SiraPozisyonu değerine sahip personel önce teklif alır.
    ///  - Kabul eden personel kuyruğun EN SONUNA eklenir.
    ///  - Reddeden veya zaman aşımına uğrayan personel sırası değişmez.
    /// </summary>
    public class QueueService : IQueueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public QueueService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        /// <summary>
        /// Sıradaki aktif personel listesini döndürür.
        /// İsteğe bağlı departman ve şube filtresi uygulanabilir.
        /// Sonuçlar SiraPozisyonu değerine göre artan sırayla gelir.
        /// </summary>
        public async Task<IEnumerable<QueueDto>> GetQueueListAsync(string? department = null, string? sube = null)
        {
            var queueItems = await _unitOfWork.Queue.GetOrderedQueueAsync(department, sube);
            return _mapper.Map<IEnumerable<QueueDto>>(queueItems);
        }

        /// <summary>
        /// Belirtilen personeli kuyruğun sonuna taşır (mesai kabul ettiğinde çağrılır).
        /// İşlem adımları:
        ///  1. Şu anki en yüksek SiraPozisyonu değerini bul
        ///  2. Personelin pozisyonunu max + 1 yap (geçici olarak en sona koy)
        ///  3. Tüm kuyruğu 1'den başlayarak sırayla yeniden numaralandır (boşlukları kapat)
        /// </summary>
        public async Task MoveUserToBackOfQueueAsync(string personnelId)
        {
            var queueItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(personnelId);
            if (queueItem == null) return;

            // Mevcut en yüksek sıra numarasını bul
            var allItems = (await _unitOfWork.Queue.GetOrderedQueueAsync()).ToList();
            int maxPos = allItems.Count > 0 ? allItems.Max(q => q.SiraPozisyonu) : 1;

            // Personeli geçici olarak en sona ekle
            queueItem.SiraPozisyonu = maxPos + 1;
            queueItem.SonGuncellemeTarihi = DateTime.Now;

            _unitOfWork.Queue.Update(queueItem);
            await _unitOfWork.SaveChangesAsync();

            // Kuyruğu 1'den yeniden numaralandırarak boşlukları kapat
            await _unitOfWork.Queue.ReorderQueueAsync();
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Yöneticinin manuel olarak belirlediği personel sırasını kaydeder.
        /// Liste indeksi 0-bazlı olup SiraPozisyonu 1'den başlar.
        /// Dikkat: Tüm personel listesi gönderilmeli; eksik ID'ler güncellenmez.
        /// </summary>
        public async Task ReorderQueuePositionsAsync(List<string> orderedPersonnelIds)
        {
            for (int i = 0; i < orderedPersonnelIds.Count; i++)
            {
                var item = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(orderedPersonnelIds[i]);
                if (item != null)
                {
                    item.SiraPozisyonu = i + 1; // 1-bazlı sıra numarası
                    _unitOfWork.Queue.Update(item);
                }
            }
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Tüm sırayı işe giriş tarihine göre sıfırdan oluşturur.
        /// Kullanım senaryoları:
        ///  - Yıl başı sıra sıfırlama
        ///  - Test/demo verileri yüklendiğinde
        /// İşlem adımları:
        ///  1. Aktif personelleri işe giriş tarihine göre sırala (en kıdemlisi 1. sırada)
        ///  2. Her personel için QueueItem'ı güncelle veya yoksa oluştur
        ///  3. İstatistikleri (kabul/red sayıları) sıfırla
        /// Not: Pasif personel QueueItem'ları güncellenmez; mevcut pozisyonlarında kalır.
        ///      Bu durum sıra çakışmasına yol açabilir. Sonraki geliştirme planında ele alınacaktır.
        /// </summary>
        public async Task ResetQueuePositionsAsync()
        {
            // Aktif personelleri işe giriş tarihine göre sırala
            var users = (await _unitOfWork.Users.FindAsync(u => u.AktifMi))
                .OrderBy(u => u.IseGirisTarihi)
                .ToList();

            for (int i = 0; i < users.Count; i++)
            {
                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(users[i].Id);
                if (qItem != null)
                {
                    // Mevcut kaydı sıfırla
                    qItem.SiraPozisyonu = i + 1;
                    qItem.ToplamKabulEdilenMesai = 0;
                    qItem.ToplamReddedilenMesai = 0;
                    qItem.SonMesaiTarihi = null;
                    _unitOfWork.Queue.Update(qItem);
                }
                else
                {
                    // Kuyruk kaydı yoksa yeni oluştur (örn. sıfırlama öncesi eklenen personel)
                    await _unitOfWork.Queue.AddAsync(new QueueItem
                    {
                        PersonnelId = users[i].Id,
                        SiraPozisyonu = i + 1,
                        AktifMi = true
                    });
                }
            }
            await _unitOfWork.SaveChangesAsync();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // BİLDİRİM SERVİSİ
    // Veritabanına kalıcı bildirim kaydeder ve SignalR üzerinden gerçek zamanlı iletir.
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kullanıcılara ve rol gruplarına bildirim gönderir.
    /// Her bildirim hem veritabanına kaydedilir (kalıcı, okundu/okunmadı takibi için)
    /// hem de SignalR ile anlık olarak kullanıcının ekranına iletilir.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationHubService _hubService;

        public NotificationService(IUnitOfWork unitOfWork, IMapper mapper, INotificationHubService hubService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _hubService = hubService;
        }

        /// <summary>
        /// Belirli bir kullanıcıya bildirim gönderir.
        /// Bildirim veritabanına kaydedilir ve SignalR ile anlık olarak iletilir.
        /// relatedUrl parametresi: bildirimdeki "Git" bağlantısı için kullanılır (opsiyonel).
        /// </summary>
        public async Task SendNotificationAsync(
            string userId, string baslik, string mesaj, NotificationType tip, string? relatedUrl = null)
        {
            // Kalıcı bildirim kaydı oluştur
            var notification = new Notification
            {
                UserId = userId,
                Baslik = baslik,
                Mesaj = mesaj,
                Tip = tip,
                RelatedUrl = relatedUrl,
                OlusturmaTarihi = DateTime.Now,
                OkunduMu = false
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            // SignalR üzerinden kullanıcının bağlı tüm istemcilerine anlık push gönder
            await _hubService.SendUserNotificationAsync(userId, new
            {
                id = notification.Id,
                baslik = notification.Baslik,
                mesaj = notification.Mesaj,
                tip = notification.Tip.ToString(),
                olusturmaTarihi = notification.OlusturmaTarihi.ToString("dd.MM.yyyy HH:mm"),
                relatedUrl = notification.RelatedUrl
            });
        }

        /// <summary>
        /// Belirli bir roldeki TÜM kullanıcılara (örn. "Yonetici") toplu bildirim gönderir.
        /// Not: Bu metot yalnızca SignalR üzerinden gönderir; veritabanına kaydedilmez.
        /// Yönetici uyarıları gibi geçici/acil durumlar için tasarlanmıştır.
        /// </summary>
        public async Task SendNotificationToRoleAsync(
            string roleName, string baslik, string mesaj, NotificationType tip)
        {
            await _hubService.SendRoleNotificationAsync(roleName, new
            {
                baslik = baslik,
                mesaj = mesaj,
                tip = tip.ToString(),
                olusturmaTarihi = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
            });
        }

        /// <summary>
        /// Belirli bir kullanıcının tüm bildirimlerini en yeniden eskiye sıralı döndürür.
        /// Bildirim sayfasında görüntülenir.
        /// </summary>
        public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(string userId)
        {
            var list = await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId);
            return _mapper.Map<IEnumerable<NotificationDto>>(list.OrderByDescending(n => n.OlusturmaTarihi));
        }

        /// <summary>
        /// Belirli bir bildirimi okundu olarak işaretler.
        /// Navbar'daki bildirim rozeti sayısını düşürmek için kullanılır.
        /// </summary>
        public async Task MarkAsReadAsync(int notificationId)
        {
            var notif = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notif != null)
            {
                notif.OkunduMu = true;
                _unitOfWork.Notifications.Update(notif);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Kullanıcının okunmamış TÜM bildirimlerini tek seferde okundu yapar.
        /// "Tümünü Okundu Yap" butonu tarafından çağrılır.
        /// </summary>
        public async Task MarkAllAsReadAsync(string userId)
        {
            var list = await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId && !n.OkunduMu);
            foreach (var item in list)
            {
                item.OkunduMu = true;
                _unitOfWork.Notifications.Update(item);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Kullanıcının okunmamış bildirim sayısını döndürür.
        /// Navbar rozeti için periyodik olarak sorgulanan endpoint tarafından kullanılır.
        /// </summary>
        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _unitOfWork.Notifications.CountAsync(n => n.UserId == userId && !n.OkunduMu);
        }
    }
}


