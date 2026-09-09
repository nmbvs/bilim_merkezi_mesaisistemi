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
    // DASHBOARD SERVİSİ
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Yönetici ve personel dashboard sayfalarında görüntülenecek verileri hazırlar.
    /// Mesai istatistikleri, bekleyen teklifler, duyurular ve personel dağılımı bilgilerini toplar.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IOvertimeService _overtimeService;

        public DashboardService(IUnitOfWork unitOfWork, IMapper mapper, IOvertimeService overtimeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _overtimeService = overtimeService;
        }

        /// <summary>
        /// Yönetici paneli için tüm istatistikleri tek bir DTO'da toplar:
        ///  - Toplam aktif personel sayısı
        ///  - Planlı / tamamlanmış mesai sayıları
        ///  - Bekleyen / kabul edilen teklif sayıları ve kabul oranı
        ///  - Son 5 mesai kaydı
        ///  - Son 5 duyuru
        ///  - Departman bazlı personel dağılımı (grafik için)
        ///  - Aylık mesai istatistikleri (grafik için)
        /// </summary>
        public async Task<DashboardDto> GetAdminDashboardDataAsync()
        {
            // Aktif personel sayısını al
            var usersCount = await _unitOfWork.Users.CountAsync(u => u.AktifMi);

            var overtimes = (await _unitOfWork.Overtimes.GetAllAsync()).ToList();

            // Mesai durumlarına göre sayım yap
            var activeOvertimes = overtimes.Count(o => o.Durum == OvertimeStatus.Planlandi);
            var completedOvertimes = overtimes.Count(o => o.Durum == OvertimeStatus.Tamamlandi);

            var offers = (await _unitOfWork.OvertimeOffers.GetAllAsync()).ToList();
            var pendingOffers = offers.Count(o => o.Durum == OfferStatus.Bekliyor);
            var acceptedOffers = offers.Count(o => o.Durum == OfferStatus.KabulEdildi);

            // Yanıtlanan teklif sayısı: kabul + red + zaman aşımı (bekleyenler dahil değil)
            var totalResponded = offers.Count(o =>
                o.Durum == OfferStatus.KabulEdildi ||
                o.Durum == OfferStatus.Reddedildi ||
                o.Durum == OfferStatus.ZamanAsimi);

            // Kabul oranı hesapla (yanıt yoksa varsayılan %100)
            double acceptRate = totalResponded > 0
                ? Math.Round((double)acceptedOffers / totalResponded * 100, 1)
                : 100.0;

            // Son 5 mesaiyi ve bekleyen teklifleri zengin DTO ile al
            var recentOvertimes = (await _overtimeService.GetAllOvertimesAsync()).Take(5);
            var pendingOffersList = await _overtimeService.GetAllPendingOffersAsync();

            // Aktif duyuruları en yeni 5 tanesi ile sınırla
            var announcements = await _unitOfWork.Announcements.FindAsync(a => a.AktifMi);
            var recentAnnouncements = _mapper.Map<IEnumerable<AnnouncementDto>>(
                announcements.OrderByDescending(a => a.OlusturmaTarihi).Take(5));

            // Grafik verileri: departman bazlı personel dağılımı
            var allUsers = await _unitOfWork.Users.GetAllAsync();
            var deptDistribution = allUsers
                .Where(u => !string.IsNullOrEmpty(u.Departman))
                .GroupBy(u => u.Departman)
                .ToDictionary(g => g.Key, g => g.Count());

            // Grafik verileri: aylık mesai sayısı
            var monthlyStats = overtimes
                .GroupBy(o => o.Tarih.ToString("MMM yyyy"))
                .ToDictionary(g => g.Key, g => g.Count());

            return new DashboardDto
            {
                TotalPersonnelCount = usersCount,
                ActiveOvertimesCount = activeOvertimes,
                PendingOffersCount = pendingOffers,
                CompletedOvertimesCount = completedOvertimes,
                AcceptanceRate = acceptRate,
                RecentOvertimes = recentOvertimes,
                PendingOffers = pendingOffersList,
                RecentAnnouncements = recentAnnouncements,
                DepartmentDistribution = deptDistribution,
                MonthlyOvertimeStats = monthlyStats
            };
        }

        /// <summary>
        /// Personel paneli için kullanıcıya özel verileri toplar:
        ///  - Personelin bekleyen mesai teklifleri
        ///  - Personele atanmış mesailerin listesi (en yeniden eskiye)
        ///  - Aktif duyurular
        /// </summary>
        public async Task<DashboardDto> GetPersonnelDashboardDataAsync(string userId)
        {
            // Personelin bekleyen tekliflerini al
            var myPendingOffers = await _overtimeService.GetPendingOffersForUserAsync(userId);

            // Bu personele atanmış mesaileri bul ve tarihe göre sırala
            var overtimes = await _unitOfWork.Overtimes.GetOvertimesWithOffersAsync();
            var myAssignedOvertimes = _mapper.Map<IEnumerable<OvertimeDto>>(
                overtimes.Where(o => o.AtananUserId == userId).OrderByDescending(o => o.Tarih));

            // Aktif duyuruları al
            var announcements = await _unitOfWork.Announcements.FindAsync(a => a.AktifMi);
            var recentAnnouncements = _mapper.Map<IEnumerable<AnnouncementDto>>(
                announcements.OrderByDescending(a => a.OlusturmaTarihi).Take(5));

            return new DashboardDto
            {
                PendingOffers = myPendingOffers,
                RecentOvertimes = myAssignedOvertimes,
                RecentAnnouncements = recentAnnouncements
            };
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DUYURU SERVİSİ
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sistem duyurularının oluşturulması, listelenmesi ve silinmesi işlemlerini yönetir.
    /// Personel yalnızca aktif duyuruları; yönetici ise tüm duyuruları görebilir.
    /// </summary>
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AnnouncementService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        /// <summary>
        /// Yalnızca aktif (AktifMi = true) duyuruları en yeniden eskiye sıralı döndürür.
        /// Personel panelinde kullanılır.
        /// </summary>
        public async Task<IEnumerable<AnnouncementDto>> GetActiveAnnouncementsAsync()
        {
            var list = await _unitOfWork.Announcements.FindAsync(a => a.AktifMi);
            return _mapper.Map<IEnumerable<AnnouncementDto>>(list.OrderByDescending(a => a.OlusturmaTarihi));
        }

        /// <summary>
        /// Aktif/pasif tüm duyuruları döndürür.
        /// Yönetici panelinde kullanılır.
        /// </summary>
        public async Task<IEnumerable<AnnouncementDto>> GetAllAnnouncementsAsync()
        {
            var list = await _unitOfWork.Announcements.GetAllAsync();
            return _mapper.Map<IEnumerable<AnnouncementDto>>(list.OrderByDescending(a => a.OlusturmaTarihi));
        }

        /// <summary>
        /// Yeni bir duyuru oluşturur ve veritabanına kaydeder.
        /// Yayınlayan kullanıcı ID'si parametre olarak verilir.
        /// </summary>
        public async Task AddAnnouncementAsync(AnnouncementDto dto, string authorUserId)
        {
            var announcement = new Announcement
            {
                Baslik = dto.Baslik,
                Icerik = dto.Icerik,
                OncelikliMi = dto.OncelikliMi,
                AktifMi = true,
                YayinlayanUserId = authorUserId,
                OlusturmaTarihi = DateTime.Now
            };

            await _unitOfWork.Announcements.AddAsync(announcement);
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Belirtilen ID'deki duyuruyu veritabanından kalıcı olarak siler.
        /// Soft delete (pasife alma) yerine hard delete uygulanır.
        /// </summary>
        public async Task DeleteAnnouncementAsync(int id)
        {
            var ann = await _unitOfWork.Announcements.GetByIdAsync(id);
            if (ann != null)
            {
                _unitOfWork.Announcements.Remove(ann);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // MESAI TAKAS SERVİSİ
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Personeller arası mesai değişim (takas) taleplerini yönetir.
    /// Bir personel kendi mesaisini başka bir personelle takas etmek istediğinde
    /// bu servis üzerinden talep oluşturulur ve yanıtlanır.
    /// </summary>
    public class SwapRequestService : ISwapRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;

        public SwapRequestService(IUnitOfWork unitOfWork, IMapper mapper, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Bir personelin kendi mesaisini belirli bir hedefe takaslamak için talep oluşturur.
        /// Yalnızca kişinin kendisine atanmış mesai için takas talep edilebilir.
        /// Hedef kişiye gerçek zamanlı bildirim gönderilir.
        /// </summary>
        public async Task<OvertimeSwapDto> CreateSwapRequestAsync(
            int overtimeId, string requesterUserId, string targetUserId, string aciklama)
        {
            // Güvenlik kontrolü: yalnızca kendi mesaisi için takas talep edilebilir
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(overtimeId);
            if (overtime == null || overtime.AtananUserId != requesterUserId)
                throw new InvalidOperationException(
                    "Yalnızca tarafınıza atanmış mesailer için değişim talebinde bulunabilirsiniz.");

            var request = new OvertimeSwapRequest
            {
                OvertimeId = overtimeId,
                IstekYapanUserId = requesterUserId,
                HedefUserId = targetUserId,
                Aciklama = aciklama,
                Durum = SwapStatus.Bekliyor,
                OlusturmaTarihi = DateTime.Now
            };

            await _unitOfWork.OvertimeSwapRequests.AddAsync(request);
            await _unitOfWork.SaveChangesAsync();

            // Hedef kişiye takas talebi bildirimi gönder
            await _notificationService.SendNotificationAsync(
                targetUserId,
                "Mesai Değişim Talebi!",
                $"Mesai arkadaşınız sizinle {overtime.Tarih:dd.MM.yyyy} tarihindeki mesaiyi değiştirmek istiyor.",
                NotificationType.MesaiDegisim,
                "/Overtime/SwapRequests");

            return _mapper.Map<OvertimeSwapDto>(request);
        }

        /// <summary>
        /// Hedef personelin takas talebine verdiği yanıtı (onay/red) işler.
        ///
        /// ONAYLANIRSA:
        ///  - Takas talebi "Onaylandi" yapılır
        ///  - Mesainin AtananUserId, hedef personel olarak güncellenir
        ///  - [DÜZELTME - Madde 6] Aynı mesaiye ait bekleyen DİĞER takas talepleri
        ///    otomatik olarak "IptalEdildi" yapılır ve talep sahiplerine bildirim gönderilir.
        ///    Bu sayede "çifte kabul" durumu önlenir.
        ///  - Talep sahibine onay bildirimi gönderilir
        ///
        /// REDDEDİLİRSE:
        ///  - Takas talebi "Reddedildi" yapılır
        ///  - Talep sahibine red bildirimi gönderilir
        /// </summary>
        public async Task ProcessSwapResponseAsync(int swapRequestId, bool isApproved)
        {
            // Talebi bul; yoksa veya zaten yanıtlanmışsa hata fırlat
            var request = await _unitOfWork.OvertimeSwapRequests.GetByIdAsync(swapRequestId);
            if (request == null || request.Durum != SwapStatus.Bekliyor)
                throw new InvalidOperationException("Geçersiz değişim talebi.");

            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(request.OvertimeId);
            if (overtime == null) throw new InvalidOperationException("Mesai bulunamadı.");

            request.YanitTarihi = DateTime.Now;

            if (isApproved)
            {
                // Talebi onayla ve mesaiyi hedef personele ata
                request.Durum = SwapStatus.Onaylandi;
                overtime.AtananUserId = request.HedefUserId;
                _unitOfWork.Overtimes.Update(overtime);

                // ── [DÜZELTME - Madde 6] Diğer Bekleyen Takas Taleplerini İptal Et ─────────
                // Aynı mesaiye ait, bu talep dışındaki bekleyen takas taleplerini bul
                var digerBekleyenTalepler = await _unitOfWork.OvertimeSwapRequests.FindAsync(s =>
                    s.OvertimeId == request.OvertimeId &&
                    s.Id != request.Id &&                   // Bu talebin kendisi hariç
                    s.Durum == SwapStatus.Bekliyor);        // Yalnızca bekleyen durumdakiler

                foreach (var digerTalep in digerBekleyenTalepler)
                {
                    // Her birini iptal et
                    digerTalep.Durum = SwapStatus.IptalEdildi;
                    digerTalep.YanitTarihi = DateTime.Now;
                    _unitOfWork.OvertimeSwapRequests.Update(digerTalep);

                    // Talep sahibine "başka birinin kabul ettiği" bildirimi gönder
                    await _notificationService.SendNotificationAsync(
                        digerTalep.IstekYapanUserId,
                        "Mesai Değişim Talebi İptal Edildi",
                        $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai değişim talebiniz, " +
                        $"mesai başka bir personele devredildiği için otomatik iptal edildi.",
                        NotificationType.MesaiDegisim);
                }
                // ── İptal İşlemi Sonu ─────────────────────────────────────────────────────

                // Talep sahibine onay bildirim gönder
                await _notificationService.SendNotificationAsync(
                    request.IstekYapanUserId,
                    "Mesai Değişimi Onaylandı!",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai değişimi talebiniz onaylandı. " +
                    $"Yeni görevli mesai arkadaşınız.",
                    NotificationType.MesaiDegisim);
            }
            else
            {
                // Talebi reddet ve talep sahibine bildir
                request.Durum = SwapStatus.Reddedildi;
                await _notificationService.SendNotificationAsync(
                    request.IstekYapanUserId,
                    "Mesai Değişimi Reddedildi",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai değişimi talebiniz reddedildi.",
                    NotificationType.MesaiDegisim);
            }

            _unitOfWork.OvertimeSwapRequests.Update(request);
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Belirli bir kullanıcıya yönelik bekleyen (yanıtlanmamış) takas taleplerini döndürür.
        /// "Mesai Değişim Talepleri" sayfasında görüntülenir.
        /// Not: Yalnızca bu kullanıcının HEDEF olduğu (HedefUserId == userId) talepler listelenir.
        /// </summary>
        public async Task<IEnumerable<OvertimeSwapDto>> GetPendingSwapRequestsForUserAsync(string userId)
        {
            // Bu kullanıcının hedef olduğu bekleyen talepleri bul
            var list = await _unitOfWork.OvertimeSwapRequests.FindAsync(s =>
                s.HedefUserId == userId && s.Durum == SwapStatus.Bekliyor);

            var result = new List<OvertimeSwapDto>();
            foreach (var req in list)
            {
                var dto = _mapper.Map<OvertimeSwapDto>(req);

                // Mesai bilgilerini DTO'ya ekle
                var ot = await _unitOfWork.Overtimes.GetByIdAsync(req.OvertimeId);
                var requester = await _unitOfWork.Users.GetByIdAsync(req.IstekYapanUserId);

                if (ot != null)
                {
                    dto.OvertimeTarih = ot.Tarih;
                    dto.OvertimeAciklama = ot.Aciklama;
                }
                if (requester != null)
                {
                    dto.IstekYapanAdSoyad = requester.AdSoyad;
                }
                result.Add(dto);
            }
            return result;
        }
    }
}
