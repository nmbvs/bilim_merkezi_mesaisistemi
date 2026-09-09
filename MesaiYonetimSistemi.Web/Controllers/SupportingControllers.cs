using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Core.Interfaces;
using System.Linq;

namespace MesaiYonetimSistemi.Web.Controllers
{
    // ════════════════════════════════════════════════════════════════════════════
    // RAPOR CONTROLLER'I
    // Yalnızca Yönetici rolüne açıktır.
    // Mesai ve personel verilerini Excel ya da HTML formatında dışa aktarır.
    // ════════════════════════════════════════════════════════════════════════════

    [Authorize(Roles = "Yonetici")]
    public class ReportController : Controller
    {
        private readonly IOvertimeService _overtimeService;
        private readonly IQueueService _queueService;
        private readonly IExportService _exportService;
        private readonly IUnitOfWork _unitOfWork;

        public ReportController(
            IOvertimeService overtimeService,
            IQueueService queueService,
            IExportService exportService,
            IUnitOfWork unitOfWork)
        {
            _overtimeService = overtimeService;
            _queueService = queueService;
            _exportService = exportService;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Mesai raporları ana sayfasını yükler.
        /// Tüm mesaileri listeleme ve dışa aktarma seçeneklerini gösterir.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var overtimes = await _overtimeService.GetAllOvertimesAsync();
            return View(overtimes);
        }

        /// <summary>
        /// Tüm mesai kayıtlarını Excel (.xlsx) formatında indirir.
        /// Dosya adı "Mesai_Raporu_YYYYMMDD_HHmm.xlsx" şeklinde oluşturulur.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportOvertimesExcel()
        {
            var overtimes = await _overtimeService.GetAllOvertimesAsync();
            var excelBytes = await _exportService.ExportOvertimesToExcelAsync(overtimes);
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Mesai_Raporu_{System.DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>
        /// Personel listesini sıra bilgileriyle birlikte Excel (.xlsx) formatında indirir.
        /// Dosya adı "Personel_Listesi_YYYYMMDD_HHmm.xlsx" şeklinde oluşturulur.
        /// Not: u.QueueItem navigation property lazy load gerektirdiğinden
        ///      GetAllAsync sonrası manuel eşleme yapılmaktadır.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportPersonnelExcel()
        {
            var queue = await _queueService.GetQueueListAsync();
            var users = await _unitOfWork.Users.GetAllAsync();

            // Her kullanıcı için kuyruk bilgisini manuel olarak eşle
            var dtoList = users.Select(u => new UserDto
            {
                SicilNo = u.SicilNo,
                AdSoyad = u.AdSoyad,
                TcKimlikNo = u.TcKimlikNo,
                Email = u.Email ?? "",
                PhoneNumber = u.PhoneNumber ?? "",
                Departman = u.Departman,
                Gorev = u.Gorev,
                Sube = u.Sube,
                IseGirisTarihi = u.IseGirisTarihi,
                // QueueItem null olabilir (yönetici kullanıcısı kuyrukta olmayabilir)
                QueuePosition = u.QueueItem?.SiraPozisyonu ?? 0,
                TotalAccepted = u.QueueItem?.ToplamKabulEdilenMesai ?? 0,
                TotalRejected = u.QueueItem?.ToplamReddedilenMesai ?? 0,
                AktifMi = u.AktifMi
            });

            var excelBytes = await _exportService.ExportPersonnelToExcelAsync(dtoList);
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Personel_Listesi_{System.DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>
        /// Mesai raporunu HTML formatında indirir.
        ///
        /// [DÜZELTME - Madde 7] Önceki sürümde MIME type "text/html" olmasına rağmen
        /// dosya adı ".html" değil ".pdf" uzantısıyla veriliyordu ve kullanıcıda
        /// format/uzantı uyuşmazlığı oluşuyordu.
        /// Düzeltme: Dosya adı ".html" uzantısıyla verildi; gerçek PDF çıktısı için
        /// ilerleyen sürümde QuestPDF veya DinkToPdf entegrasyonu planlanmaktadır.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportHtmlReport()
        {
            var overtimes = await _overtimeService.GetAllOvertimesAsync();
            var htmlBytes = await _exportService.ExportOvertimeReportPdfAsync(overtimes);

            // MIME type ve dosya uzantısı tutarlı: her ikisi de HTML formatında
            return File(
                htmlBytes,
                "text/html; charset=utf-8",
                $"Mesai_Raporu_{System.DateTime.Now:yyyyMMdd_HHmm}.html");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // DUYURU CONTROLLER'I
    // Tüm giriş yapmış kullanıcılara açıktır.
    // Yönetici: oluşturma ve silme işlemleri yapabilir.
    // Personel: yalnızca aktif duyuruları görüntüleyebilir.
    // ════════════════════════════════════════════════════════════════════════════

    [Authorize]
    public class AnnouncementController : Controller
    {
        private readonly IAnnouncementService _announcementService;

        public AnnouncementController(IAnnouncementService announcementService)
        {
            _announcementService = announcementService;
        }

        /// <summary>
        /// Duyuru listesini getirir.
        /// Yönetici: aktif + pasif tüm duyurular gösterilir.
        /// Personel: yalnızca aktif duyurular gösterilir.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var list = User.IsInRole("Yonetici")
                ? await _announcementService.GetAllAnnouncementsAsync()
                : await _announcementService.GetActiveAnnouncementsAsync();

            return View(list);
        }

        /// <summary>
        /// Yeni duyuru oluşturma formunu gösterir. (Yalnızca Yönetici)
        /// </summary>
        [Authorize(Roles = "Yonetici")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AnnouncementDto());
        }

        /// <summary>
        /// Yeni duyuruyu kaydeder ve listeye yönlendirir. (Yalnızca Yönetici)
        /// Yayınlayan kullanıcı ID'si oturum bilgisinden otomatik alınır.
        /// </summary>
        [Authorize(Roles = "Yonetici")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AnnouncementDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _announcementService.AddAnnouncementAsync(dto, userId);

            TempData["SuccessMessage"] = "Duyuru başarıyla yayınlandı.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Belirtilen duyuruyu veritabanından kalıcı olarak siler. (Yalnızca Yönetici)
        /// Soft delete değil, hard delete uygulanır.
        /// </summary>
        [Authorize(Roles = "Yonetici")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _announcementService.DeleteAnnouncementAsync(id);
            TempData["SuccessMessage"] = "Duyuru silindi.";
            return RedirectToAction("Index");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // BİLDİRİM CONTROLLER'I
    // Giriş yapmış tüm kullanıcılara açıktır.
    // Kullanıcının kendi bildirimlerini listeler ve okundu olarak işaretler.
    // ════════════════════════════════════════════════════════════════════════════

    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Oturum açmış kullanıcının tüm bildirimlerini listeler (en yenisi üstte).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var list = await _notificationService.GetUserNotificationsAsync(userId);
            return View(list);
        }

        /// <summary>
        /// Belirli bir bildirimi "okundu" olarak işaretler.
        /// AJAX ile çağrılır; JSON { success: true } döner.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return Json(new { success = true });
        }

        /// <summary>
        /// Kullanıcının TÜM okunmamış bildirimlerini tek seferde okundu yapar.
        /// "Tümünü Okundu Yap" butonu için AJAX ile çağrılır.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _notificationService.MarkAllAsReadAsync(userId);
            return Json(new { success = true });
        }

        /// <summary>
        /// Kullanıcının okunmamış bildirim sayısını döndürür.
        /// Navbar'daki bildirim rozeti için periyodik AJAX sorgularında kullanılır.
        /// Yanıt: JSON { count: N }
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }
    }
}
