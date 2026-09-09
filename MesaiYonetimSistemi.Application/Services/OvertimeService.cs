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
    /// <summary>
    /// Mesai oluşturma, teklif akışı yönetimi ve mesai iptali işlemlerini yürüten ana servis.
    /// Adil sıra (fair queue) algoritmasını kullanarak teklifleri otomatik iletir.
    /// </summary>
    public class OvertimeService : IOvertimeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IQueueService _queueService;
        private readonly ILeaveService _leaveService;

        public OvertimeService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            INotificationService notificationService,
            IQueueService queueService,
            ILeaveService leaveService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _notificationService = notificationService;
            _queueService = queueService;
            _leaveService = leaveService;
        }

        // ────────────────────────────────────────────────────
        // SORGULAMA METODLARı
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Tüm mesaileri, teklif geçmişleriyle birlikte döndürür.
        /// Her mesai için "şu an teklif gönderilen" ve "sıradaki aday" bilgileri de hesaplanır.
        /// </summary>
        public async Task<IEnumerable<OvertimeDto>> GetAllOvertimesAsync()
        {
            var overtimes = await _unitOfWork.Overtimes.GetOvertimesWithOffersAsync();
            var allOrderedQueue = (await _unitOfWork.Queue.GetOrderedQueueAsync()).ToList();
            var result = new List<OvertimeDto>();

            foreach (var ot in overtimes)
            {
                var dto = _mapper.Map<OvertimeDto>(ot);
                // Her mesai için aktif teklif ve sıradaki aday bilgilerini doldur
                await EnrichOvertimeDtoAsync(dto, ot, allOrderedQueue);
                result.Add(dto);
            }

            return result;
        }

        /// <summary>
        /// Belirli bir mesaiyi ID ile bulur ve teklif/aday bilgileriyle zenginleştirir.
        /// Bulunamazsa null döner.
        /// </summary>
        public async Task<OvertimeDto?> GetOvertimeByIdAsync(int id)
        {
            var overtime = await _unitOfWork.Overtimes.GetOvertimeDetailsAsync(id);
            if (overtime == null) return null;

            var allOrderedQueue = (await _unitOfWork.Queue.GetOrderedQueueAsync()).ToList();
            var dto = _mapper.Map<OvertimeDto>(overtime);
            await EnrichOvertimeDtoAsync(dto, overtime, allOrderedQueue);
            return dto;
        }

        /// <summary>
        /// OvertimeDto nesnesini aşağıdaki ek verilerle doldurur:
        ///  - Teklif geçmişi (OfferHistory)
        ///  - Şu an teklif gönderilen personelin adı (ActivePersonnelAdSoyad)
        ///  - Sıradaki potansiyel aday (NextCandidateAdSoyad)
        /// Not: Departman/Şube filtresine göre kuyruktan aday seçilir;
        ///      bulunamazsa genel kuyruğa fallback uygulanır.
        /// </summary>
        private async Task EnrichOvertimeDtoAsync(OvertimeDto dto, Overtime entity, List<QueueItem> allOrderedQueue)
        {
            // Teklifleri sıra numarasına göre sırala
            var offers = (entity.Offers ?? new List<OvertimeOffer>()).OrderBy(o => o.SiraNo).ToList();
            dto.OfferHistory = _mapper.Map<List<OfferDto>>(offers);

            // Bekleyen aktif teklifi bul ve DTO'ya aktar
            var activeOffer = offers.FirstOrDefault(o => o.Durum == OfferStatus.Bekliyor);
            if (activeOffer != null)
            {
                dto.ActiveOffer = _mapper.Map<OfferDto>(activeOffer);
                dto.ActivePersonnelAdSoyad = activeOffer.Personnel?.AdSoyad;
            }

            // Mesai henüz kimseye atanmamışsa ve aktif durumdaysa sıradaki adayı hesapla
            if (string.IsNullOrEmpty(entity.AtananUserId) && entity.Durum == OvertimeStatus.Planlandi)
            {
                // Daha önce teklif gönderilenlerin ID setini oluştur
                var offeredUserIds = offers.Select(o => o.PersonnelId).ToHashSet();

                // Önce departman/şube filtresine göre kuyruğu filtrele
                var deptQueue = allOrderedQueue.Where(q =>
                    (string.IsNullOrEmpty(entity.Departman) || q.Personnel?.Departman == entity.Departman) &&
                    (string.IsNullOrEmpty(entity.Sube) || q.Personnel?.Sube == entity.Sube)
                ).ToList();

                // Departman kuyruğunda henüz teklif gönderilmemiş ilk kişiyi seç
                var nextItem = deptQueue.FirstOrDefault(q => !offeredUserIds.Contains(q.PersonnelId));

                // Departmanda uygun kişi yoksa genel kuyrukta ara (fallback)
                if (nextItem == null)
                {
                    nextItem = allOrderedQueue.FirstOrDefault(q => !offeredUserIds.Contains(q.PersonnelId));
                }

                if (nextItem != null && nextItem.Personnel != null)
                {
                    dto.NextCandidatePersonnelId = nextItem.PersonnelId;
                    dto.NextCandidateAdSoyad = nextItem.Personnel.AdSoyad;
                    dto.NextCandidateSiraNo = nextItem.SiraPozisyonu;
                }
            }
            await Task.CompletedTask;
        }

        // ────────────────────────────────────────────────────
        // MESAI OLUŞTURMA
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Yeni bir mesai kaydı oluşturur ve adil sıraya göre ilk kişiye otomatik teklif gönderir.
        /// </summary>
        public async Task<OvertimeDto> CreateOvertimeAsync(CreateOvertimeDto dto)
        {
            var overtime = _mapper.Map<Overtime>(dto);
            overtime.Durum = OvertimeStatus.Planlandi;
            overtime.OlusturmaTarihi = DateTime.Now;

            await _unitOfWork.Overtimes.AddAsync(overtime);
            await _unitOfWork.SaveChangesAsync();

            if (!string.IsNullOrEmpty(dto.SecilenIlkPersonelId))
            {
                // [EKLEME] Personel o gün izinli mi kontrol et
                bool isPersonnelOnLeave = await _leaveService.IsPersonnelOnLeaveAsync(dto.SecilenIlkPersonelId, dto.Tarih);
                if (isPersonnelOnLeave)
                {
                    // Mesaiyi geri al (DB'den sil)
                    _unitOfWork.Overtimes.Remove(overtime);
                    await _unitOfWork.SaveChangesAsync();
                    
                    var personel = await _unitOfWork.Users.GetByIdAsync(dto.SecilenIlkPersonelId);
                    throw new InvalidOperationException($"Seçilen personel ({personel?.AdSoyad}) bu tarihte ({dto.Tarih:dd.MM.yyyy}) izinlidir. Mesai ataması yapılamaz.");
                }

                var newOffer = new OvertimeOffer
                {
                    OvertimeId = overtime.Id,
                    PersonnelId = dto.SecilenIlkPersonelId,
                    TeklifTarihi = DateTime.Now,
                    SonCevapTarihi = DateTime.Now.AddHours(24),
                    Durum = OfferStatus.Bekliyor,
                    SiraNo = 1
                };
                await _unitOfWork.OvertimeOffers.AddAsync(newOffer);
                await _unitOfWork.SaveChangesAsync();

                await _notificationService.SendNotificationAsync(
                    dto.SecilenIlkPersonelId,
                    "Yeni Hafta Sonu Mesai Teklifi!",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai için size özel teklif gönderildi! 24 saat içinde yanıt veriniz.",
                    NotificationType.MesaiTeklifi,
                    "/Overtime/PendingOffers");
            }
            else
            {
                // Oluşturulan mesai için adil kuyruktaki ilk kişiye teklif gönder
                await SendNextOfferForOvertimeAsync(overtime.Id);
            }

            return _mapper.Map<OvertimeDto>(overtime);
        }

        // ────────────────────────────────────────────────────
        // TEKLİF YANITI İŞLEME
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Personelin mesai teklifine verdiği yanıtı (kabul/red) işler.
        ///
        /// KABUL edilirse:
        ///  - Teklif "KabulEdildi" yapılır
        ///  - Personel mesaiye atanır
        ///  - [DÜZELTME - Madde 5] Personelin aynı tarih/saatte çakışan başka atanmış mesaisi varsa hata fırlatılır
        ///  - Sıra istatistikleri güncellenir ve personel kuyruğun sonuna taşınır
        ///  - Hem personele hem yöneticilere bildirim gönderilir
        ///
        /// REDDEDİLİRSE:
        ///  - Teklif "Reddedildi" yapılır
        ///  - Red istatistiği güncellenir
        ///  - Kuyruktaki bir sonraki kişiye otomatik teklif iletilir
        /// </summary>
        public async Task ProcessOfferResponseAsync(int offerId, bool isAccepted, string? rejectReason = null)
        {
            // Teklifi bul; yoksa veya zaten yanıtlanmışsa hata fırlat
            var offer = await _unitOfWork.OvertimeOffers.GetByIdAsync(offerId);
            if (offer == null || offer.Durum != OfferStatus.Bekliyor)
                throw new InvalidOperationException("Geçersiz veya zamanı dolmuş teklif.");

            var overtime = await _unitOfWork.Overtimes.GetOvertimeDetailsAsync(offer.OvertimeId);
            if (overtime == null) throw new InvalidOperationException("Mesai bulunamadı.");

            offer.CevapTarihi = DateTime.Now;

            if (isAccepted)
            {
                // ── [DÜZELTME - Madde 5] Çakışan Mesai Kontrolü ──────────────────────────────
                // Personelin bu mesai ile aynı gün ve çakışan saatte başka atanmış mesaisi var mı?
                // Önce personele atanmış TÜM aktif mesaileri bul
                var tumAtananMesailer = await _unitOfWork.Overtimes.FindAsync(o =>
                    o.AtananUserId == offer.PersonnelId &&
                    o.Durum == OvertimeStatus.Planlandi &&
                    o.Tarih.Date == overtime.Tarih.Date);  // Aynı günde olanları al

                // Saat aralığı çakışıyor mu? (Yeni mesainin başlangıcı mevcutun bitiş saatinden önce VE
                // yeni mesainin bitişi mevcutun başlangıç saatinden sonraysa çakışma vardır)
                var cakisanMesai = tumAtananMesailer.FirstOrDefault(o =>
                    o.Id != overtime.Id &&  // Aynı mesai değil
                    overtime.BaslangicSaati < o.BitisSaati &&
                    overtime.BitisSaati > o.BaslangicSaati);

                if (cakisanMesai != null)
                {
                    throw new InvalidOperationException(
                        $"Bu personel {overtime.Tarih:dd.MM.yyyy} tarihinde " +
                        $"{cakisanMesai.BaslangicSaati:hh\\:mm}-{cakisanMesai.BitisSaati:hh\\:mm} " +
                        $"saatleri arasında zaten başka bir mesaiye atanmış. Çakışma nedeniyle teklif kabul edilemez.");
                }
                // ── Çakışma Kontrolü Sonu ────────────────────────────────────────────────────

                offer.Durum = OfferStatus.KabulEdildi;
                overtime.AtananUserId = offer.PersonnelId;
                // Not: Kabul sonrası durum "Planlandi" olarak kalır; mesai yapıldıktan sonra "Tamamlandi" olarak manuel güncellenir
                overtime.Durum = OvertimeStatus.Planlandi;
                _unitOfWork.Overtimes.Update(overtime);

                // Personelin kuyruk istatistiklerini güncelle: kabul sayısını artır, son mesai tarihini kaydet
                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(offer.PersonnelId);
                if (qItem != null)
                {
                    qItem.ToplamKabulEdilenMesai++;
                    qItem.SonMesaiTarihi = overtime.Tarih;
                    _unitOfWork.Queue.Update(qItem);
                }

                await _unitOfWork.SaveChangesAsync();

                // Kabul eden personeli kuyruğun sonuna taşı (adil sıra prensibi)
                await _queueService.MoveUserToBackOfQueueAsync(offer.PersonnelId);

                // Personele atama bildirim gönder
                await _notificationService.SendNotificationAsync(
                    offer.PersonnelId,
                    "Mesai Atandı!",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai teklifini kabul ettiniz ve atanmanız gerçekleşti.",
                    NotificationType.TeklifSonucu,
                    "/Overtime/MyOvertimes");

                // Tüm yöneticilere kabul bildirim gönder
                await _notificationService.SendNotificationToRoleAsync(
                    "Yonetici",
                    "Mesai Kabul Edildi",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai teklifi personel tarafından kabul edildi.",
                    NotificationType.TeklifSonucu);
            }
            else
            {
                // Red işlemi: teklifi kapat ve sıradaki kişiye ilet
                offer.Durum = OfferStatus.Reddedildi;
                offer.RedNedeni = rejectReason ?? "Personel tarafından reddedildi.";

                // Red istatistiğini artır
                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(offer.PersonnelId);
                if (qItem != null)
                {
                    qItem.ToplamReddedilenMesai++;
                    _unitOfWork.Queue.Update(qItem);
                }

                _unitOfWork.OvertimeOffers.Update(offer);
                await _unitOfWork.SaveChangesAsync();

                // Reddedilen teklifi kuyruktaki bir sonraki personele ilet
                await SendNextOfferForOvertimeAsync(overtime.Id);
            }
        }

        // ────────────────────────────────────────────────────
        // ARKA PLAN SERVİSİ ÇAĞRILARI
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Süresi dolmuş (24 saati geçmiş) bekleyen teklifleri bulur ve işler.
        /// Zaman aşımına uğrayan her teklif için:
        ///  - Teklif "ZamanAsimi" olarak işaretlenir
        ///  - Personelin red istatistiği artırılır
        ///  - Kuyruktaki sıradaki personele yeni teklif gönderilir
        /// Bu metot OvertimeOfferTimeoutBackgroundService tarafından her 60 saniyede bir çağrılır.
        /// </summary>
        public async Task ProcessExpiredOffersAsync()
        {
            var now = DateTime.Now;
            // Son yanıt tarihi geçmiş ve hâlâ "Bekliyor" durumundaki teklifleri bul
            var expiredOffers = await _unitOfWork.OvertimeOffers.FindAsync(o =>
                o.Durum == OfferStatus.Bekliyor && o.SonCevapTarihi <= now);

            foreach (var offer in expiredOffers)
            {
                // Teklifi zaman aşımına uğramış olarak kapat
                offer.Durum = OfferStatus.ZamanAsimi;
                offer.CevapTarihi = now;
                _unitOfWork.OvertimeOffers.Update(offer);

                // Personelin istatistiğini güncelle (zaman aşımı, red gibi sayılır)
                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(offer.PersonnelId);
                if (qItem != null)
                {
                    qItem.ToplamReddedilenMesai++;
                    _unitOfWork.Queue.Update(qItem);
                }

                await _unitOfWork.SaveChangesAsync();

                // Personele zaman aşımı bildirim gönder
                await _notificationService.SendNotificationAsync(
                    offer.PersonnelId,
                    "Teklif Süresi Doldu",
                    "Size sunulan mesai teklifinin 24 saatlik yanıt süresi doldu.",
                    NotificationType.TeklifSonucu);

                // Bir sonraki kuyruktaki kişiye teklif ilet
                await SendNextOfferForOvertimeAsync(offer.OvertimeId);
            }
        }

        // ────────────────────────────────────────────────────
        // PERSONELİN TEKLİFLERİ
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Belirli bir personele ait, süresi henüz dolmamış bekleyen mesai tekliflerini döndürür.
        /// Dashboard ve "Bekleyen Tekliflerim" sayfasında kullanılır.
        /// </summary>
        public async Task<IEnumerable<OfferDto>> GetPendingOffersForUserAsync(string personnelId)
        {
            var offers = (await _unitOfWork.OvertimeOffers.FindAsync(o =>
                o.PersonnelId == personnelId &&
                o.Durum == OfferStatus.Bekliyor &&
                o.SonCevapTarihi > DateTime.Now)).ToList();

            if (!offers.Any()) return Enumerable.Empty<OfferDto>();

            var overtimeIds = offers.Select(o => o.OvertimeId).Distinct().ToList();
            var overtimes = (await _unitOfWork.Overtimes.FindAsync(o => overtimeIds.Contains(o.Id)))
                .ToDictionary(o => o.Id);

            var result = new List<OfferDto>();
            foreach (var offer in offers)
            {
                var dto = _mapper.Map<OfferDto>(offer);
                if (overtimes.TryGetValue(offer.OvertimeId, out var ot))
                {
                    dto.OvertimeTarih = ot.Tarih;
                    dto.OvertimeAciklama = ot.Aciklama;
                    dto.BaslangicSaati = ot.BaslangicSaati;
                    dto.BitisSaati = ot.BitisSaati;
                }
                result.Add(dto);
            }
            return result;
        }

        public async Task<IEnumerable<OfferDto>> GetAllPendingOffersAsync()
        {
            var offers = (await _unitOfWork.OvertimeOffers.FindAsync(o => o.Durum == OfferStatus.Bekliyor)).ToList();
            if (!offers.Any()) return Enumerable.Empty<OfferDto>();

            var overtimeIds = offers.Select(o => o.OvertimeId).Distinct().ToList();
            var personnelIds = offers.Select(o => o.PersonnelId).Distinct().ToList();

            var overtimes = (await _unitOfWork.Overtimes.FindAsync(o => overtimeIds.Contains(o.Id)))
                .ToDictionary(o => o.Id);
            var users = (await _unitOfWork.Users.FindAsync(u => personnelIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            var result = new List<OfferDto>();
            foreach (var offer in offers)
            {
                var dto = _mapper.Map<OfferDto>(offer);

                if (overtimes.TryGetValue(offer.OvertimeId, out var ot))
                {
                    dto.OvertimeTarih = ot.Tarih;
                    dto.OvertimeAciklama = ot.Aciklama;
                }

                if (users.TryGetValue(offer.PersonnelId, out var user))
                {
                    dto.PersonnelAdSoyad = user.AdSoyad;
                }
                result.Add(dto);
            }
            return result;
        }

        // ────────────────────────────────────────────────────
        // MESAI İPTALİ
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Bir mesayi iptal eder:
        ///  - Mesai "IptalEdildi" yapılır
        ///  - Bekleyen aktif teklifler de "IptalEdildi" yapılır
        ///  - Eğer mesaiye atanmış personel varsa, iptal bildirim gönderilir
        /// </summary>
        public async Task CancelOvertimeAsync(int overtimeId, string reason)
        {
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(overtimeId);
            if (overtime == null) return;

            overtime.Durum = OvertimeStatus.IptalEdildi;
            _unitOfWork.Overtimes.Update(overtime);

            // Bu mesaiye ait bekleyen tüm aktif teklifleri kapat
            var activeOffers = await _unitOfWork.OvertimeOffers.FindAsync(o =>
                o.OvertimeId == overtimeId && o.Durum == OfferStatus.Bekliyor);

            foreach (var offer in activeOffers)
            {
                offer.Durum = OfferStatus.IptalEdildi;
                _unitOfWork.OvertimeOffers.Update(offer);
            }

            await _unitOfWork.SaveChangesAsync();

            // Daha önce atanmış personel varsa iptal bildirimini gönder
            if (!string.IsNullOrEmpty(overtime.AtananUserId))
            {
                await _notificationService.SendNotificationAsync(
                    overtime.AtananUserId,
                    "Mesai İptal Edildi",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesainiz iptal edilmiştir. Neden: {reason}",
                    NotificationType.Sistem);
            }
        }

        // ────────────────────────────────────────────────────
        // SAAT BİLDİRİMİ VE ONAY İŞLEMLERİ (SAATİ BELİRSİZ MESAİLER)
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Saati belirsiz bir mesai için personelin gerçekleşen çalışma saatlerini bildirir.
        /// </summary>
        public async Task SubmitActualHoursAsync(SubmitOvertimeHoursDto dto, string personnelId)
        {
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(dto.OvertimeId);
            if (overtime == null)
                throw new InvalidOperationException("Mesai kaydı bulunamadı.");

            if (overtime.AtananUserId != personnelId)
                throw new InvalidOperationException("Bu mesaiye atanmış personel siz değilsiniz.");

            if (!overtime.SaatBelirsizMi)
                throw new InvalidOperationException("Bu mesainin çalışma saatleri zaten başlangıçta belirlenmiştir.");

            var today = DateTime.Now.Date;
            var overtimeDate = overtime.Tarih.Date;

            if (today < overtimeDate)
                throw new InvalidOperationException("Henüz mesai günü gelmediği için çalışma saatlerinizi bildiremezsiniz. Saat bildirimi mesai günü veya sonrasında yapılabilir.");

            if (dto.BaslangicSaati >= dto.BitisSaati)
                throw new InvalidOperationException("Bitiş saati, başlangıç saatinden sonra olmalıdır.");

            if (today == overtimeDate)
            {
                var currentTime = DateTime.Now.TimeOfDay;
                if (dto.BitisSaati > currentTime)
                {
                    throw new InvalidOperationException($"Henüz gerçekleşmemiş gelecek bir saati bildiremezsiniz. Şu an saat {currentTime.Hours:D2}:{currentTime.Minutes:D2}; bitiş saati en fazla şu anki saat olabilir.");
                }
                if (dto.BaslangicSaati > currentTime)
                {
                    throw new InvalidOperationException("Henüz gerçekleşmemiş gelecek bir başlangıç saati bildiremezsiniz.");
                }
            }

            overtime.BildirilenBaslangicSaati = dto.BaslangicSaati;
            overtime.BildirilenBitisSaati = dto.BitisSaati;
            overtime.PersonelSaatNotu = dto.PersonelSaatNotu;
            overtime.SaatBildirimDurumu = SaatBildirimStatus.OnayBekliyor;

            _unitOfWork.Overtimes.Update(overtime);
            await _unitOfWork.SaveChangesAsync();

            // Yöneticilere bildirim gönder
            var user = await _unitOfWork.Users.GetByIdAsync(personnelId);
            string personelAd = user?.AdSoyad ?? "Personel";

            await _notificationService.SendNotificationToRoleAsync(
                "Yonetici",
                "Mesai Saati Bildirimi Yapıldı",
                $"{personelAd}, {overtime.Tarih:dd.MM.yyyy} tarihli mesai için {dto.BaslangicSaati:hh\\:mm}-{dto.BitisSaati:hh\\:mm} saatlerini bildirdi. Onayınız bekleniyor.",
                NotificationType.Sistem);
        }

        /// <summary>
        /// Yöneticinin bildirilen çalışma saatlerini onaylamasını sağlar.
        /// Mesainin saatleri güncellenir ve mesai "Tamamlandi" statüsüne geçer.
        /// </summary>
        public async Task ApproveHoursAsync(int overtimeId, string managerUserId)
        {
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(overtimeId);
            if (overtime == null)
                throw new InvalidOperationException("Mesai kaydı bulunamadı.");

            if (overtime.SaatBildirimDurumu != SaatBildirimStatus.OnayBekliyor || !overtime.BildirilenBaslangicSaati.HasValue || !overtime.BildirilenBitisSaati.HasValue)
                throw new InvalidOperationException("Onay bekleyen geçerli bir saat bildirimi bulunmuyor.");

            // Bildirilen saatleri asıl mesai saatlerine aktar
            overtime.BaslangicSaati = overtime.BildirilenBaslangicSaati.Value;
            overtime.BitisSaati = overtime.BildirilenBitisSaati.Value;
            overtime.SaatBildirimDurumu = SaatBildirimStatus.Onaylandi;
            overtime.Durum = OvertimeStatus.Tamamlandi;

            _unitOfWork.Overtimes.Update(overtime);
            await _unitOfWork.SaveChangesAsync();

            // Atanan personele bildirim gönder
            if (!string.IsNullOrEmpty(overtime.AtananUserId))
            {
                await _notificationService.SendNotificationAsync(
                    overtime.AtananUserId,
                    "Mesai Saatleriniz Onaylandı",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihli mesainize ait bildirilen saatler ({overtime.BaslangicSaati:hh\\:mm}-{overtime.BitisSaati:hh\\:mm}) yönetici tarafından onaylandı ve mesainiz tamamlandı.",
                    NotificationType.TeklifSonucu,
                    "/Overtime/MyOvertimes");
            }
        }

        /// <summary>
        /// Yöneticinin bildirilen çalışma saatlerini reddetmesini sağlar.
        /// </summary>
        public async Task RejectHoursAsync(int overtimeId, string managerUserId, string? reason)
        {
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(overtimeId);
            if (overtime == null)
                throw new InvalidOperationException("Mesai kaydı bulunamadı.");

            overtime.SaatBildirimDurumu = SaatBildirimStatus.Reddedildi;
            _unitOfWork.Overtimes.Update(overtime);
            await _unitOfWork.SaveChangesAsync();

            // Atanan personele bildirim gönder
            if (!string.IsNullOrEmpty(overtime.AtananUserId))
            {
                await _notificationService.SendNotificationAsync(
                    overtime.AtananUserId,
                    "Mesai Saatleriniz Reddedildi",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihli mesai için bildirdiğiniz saatler yönetici tarafından reddedildi. Neden: {reason ?? "Saatlerde düzeltme yapılması gerekiyor."}. Lütfen saatlerinizi tekrar bildirin.",
                    NotificationType.TeklifSonucu,
                    "/Overtime/MyOvertimes");
            }
        }

        /// <summary>
        /// Saat onayı bekleyen saati belirsiz mesaileri getirir.
        /// </summary>
        public async Task<IEnumerable<OvertimeDto>> GetPendingHoursApprovalOvertimesAsync()
        {
            var overtimes = await _unitOfWork.Overtimes.FindAsync(o =>
                o.SaatBelirsizMi && o.SaatBildirimDurumu == SaatBildirimStatus.OnayBekliyor);

            var allOrderedQueue = (await _unitOfWork.Queue.GetOrderedQueueAsync()).ToList();
            var result = new List<OvertimeDto>();

            foreach (var ot in overtimes)
            {
                var dto = _mapper.Map<OvertimeDto>(ot);
                await EnrichOvertimeDtoAsync(dto, ot, allOrderedQueue);
                result.Add(dto);
            }

            return result;
        }

        // ────────────────────────────────────────────────────
        // ÖZEL YARDIMCI METOD: ADİL SIRADA SONRAKI KİŞİYE TEKLİF GÖNDER
        // ────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen mesai için adil sıra kuyruğundan bir sonraki uygun kişiyi bulur ve teklif gönderir.
        ///
        /// [DÜZELTME - Madde 2] Aşağıdaki durumlarda işlem yapılmaz:
        ///  - Mesai bulunamazsa
        ///  - Mesai zaten iptal edilmişse
        ///  - Mesai zaten birine atanmışsa (AtananUserId dolu)
        ///  - Mesai "Tamamlandi" statüsündeyse
        ///
        /// Aday seçimi önce departman/şubeye göre filtrelenir;
        /// uygun aday yoksa genel kuyruğa fallback uygulanır.
        ///
        /// Kuyrukta hiç aday kalmazsa yöneticilere "DİKKAT" bildirimi gönderilir.
        /// </summary>
        private async Task SendNextOfferForOvertimeAsync(int overtimeId)
        {
            var overtime = await _unitOfWork.Overtimes.GetByIdAsync(overtimeId);

            // ── [DÜZELTME - Madde 2] Erken çıkış kontrolleri ────────────────────────────
            if (overtime == null)
                return; // Mesai kaydı yok

            if (overtime.Durum == OvertimeStatus.IptalEdildi)
                return; // Mesai iptal edilmiş, teklif gönderilmez

            if (overtime.Durum == OvertimeStatus.Tamamlandi)
                return; // Mesai tamamlanmış, yeni teklif gönderilmez

            if (!string.IsNullOrEmpty(overtime.AtananUserId))
                return; // Mesai zaten birine atanmış, çifte teklif gönderilmez
            // ── Kontrol Sonu ─────────────────────────────────────────────────────────────

            // Bu mesai için daha önce teklif gönderilmiş kişilerin ID setini oluştur
            var existingOfferPersonnelIds = (await _unitOfWork.OvertimeOffers.FindAsync(o => o.OvertimeId == overtimeId))
                .Select(o => o.PersonnelId)
                .ToHashSet();

            // ── [DÜZELTME] Meşgul Personelleri Atlama ─────────────────────────────────────
            // Sistem genelinde şu an "Bekliyor" durumunda teklifi olan personellerin ID setini oluştur
            var busyPersonnelIds = (await _unitOfWork.OvertimeOffers.FindAsync(o => o.Durum == OfferStatus.Bekliyor))
                .Select(o => o.PersonnelId)
                .ToHashSet();

            // [EKLEME] Mesainin tarihinde izinli olan personellerin ID setini bul ve busyPersonnelIds'ye ekle
            var leavesOnThatDay = await _unitOfWork.PersonnelLeaves.FindAsync(l => 
                l.BaslangicTarihi.Date <= overtime.Tarih.Date && 
                l.BitisTarihi.Date >= overtime.Tarih.Date);
                
            foreach (var leave in leavesOnThatDay)
            {
                busyPersonnelIds.Add(leave.PersonnelId);
            }
            // ─────────────────────────────────────────────────────────────────────────────

            // Departman ve şubeye göre filtrelenmiş sıralı kuyruğu çek
            var orderedQueue = await _unitOfWork.Queue.GetOrderedQueueAsync(
                string.IsNullOrEmpty(overtime.Departman) ? null : overtime.Departman,
                string.IsNullOrEmpty(overtime.Sube) ? null : overtime.Sube
            );

            // Daha önce teklif gönderilmemiş VE şu an meşgul olmayan ilk kişiyi seç
            var nextCandidate = orderedQueue.FirstOrDefault(q => 
                !existingOfferPersonnelIds.Contains(q.PersonnelId) && 
                !busyPersonnelIds.Contains(q.PersonnelId));

            // Departmanda uygun aday yoksa genel kuyruğa fallback uygula
            if (nextCandidate == null)
            {
                nextCandidate = (await _unitOfWork.Queue.GetOrderedQueueAsync())
                    .FirstOrDefault(q => 
                        !existingOfferPersonnelIds.Contains(q.PersonnelId) && 
                        !busyPersonnelIds.Contains(q.PersonnelId));
            }

            if (nextCandidate != null)
            {
                // Sıra numarası: daha önce kaç kişiye teklif gidildiyse bir sonrası
                int maxSira = existingOfferPersonnelIds.Count + 1;

                var newOffer = new OvertimeOffer
                {
                    OvertimeId = overtimeId,
                    PersonnelId = nextCandidate.PersonnelId,
                    TeklifTarihi = DateTime.Now,
                    SonCevapTarihi = DateTime.Now.AddHours(24), // 24 saatlik yanıt süresi
                    Durum = OfferStatus.Bekliyor,
                    SiraNo = maxSira
                };

                await _unitOfWork.OvertimeOffers.AddAsync(newOffer);
                await _unitOfWork.SaveChangesAsync();

                // Adaya gerçek zamanlı (SignalR) + kalıcı (veritabanı) bildirim gönder
                await _notificationService.SendNotificationAsync(
                    nextCandidate.PersonnelId,
                    "Yeni Hafta Sonu Mesai Teklifi!",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai için sıra sizde! 24 saat içinde yanıt veriniz.",
                    NotificationType.MesaiTeklifi,
                    "/Overtime/PendingOffers");
            }
            else
            {
                // Kuyrukta hiç uygun aday kalmadı – yöneticileri uyar
                await _notificationService.SendNotificationToRoleAsync(
                    "Yonetici",
                    "DİKKAT: Mesai Teklifi Yanıtsız Kaldı",
                    $"{overtime.Tarih:dd.MM.yyyy} tarihindeki mesai için kuyruktaki tüm personeller teklifi reddetti " +
                    $"veya aday kalmadı. Manuel atama gereklidir.",
                    NotificationType.Sistem);
            }
        }
    }
}
