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
    [Authorize]
    public class OvertimeController : Controller
    {
        private readonly IOvertimeService _overtimeService;
        private readonly ISwapRequestService _swapRequestService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILeaveService _leaveService;
        private readonly Microsoft.AspNetCore.Identity.UserManager<MesaiYonetimSistemi.Core.Entities.ApplicationUser> _userManager;

        public OvertimeController(
            IOvertimeService overtimeService,
            ISwapRequestService swapRequestService,
            IUnitOfWork unitOfWork,
            ILeaveService leaveService,
            Microsoft.AspNetCore.Identity.UserManager<MesaiYonetimSistemi.Core.Entities.ApplicationUser> userManager)
        {
            _overtimeService = overtimeService;
            _swapRequestService = swapRequestService;
            _unitOfWork = unitOfWork;
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var overtimes = await _overtimeService.GetAllOvertimesAsync();
            return View(overtimes);
        }

        private async Task PopulateCreateViewBagsAsync()
        {
            var users = (await _unitOfWork.Users.GetAllAsync()).ToList();

            var departments = users
                .Select(u => u.Departman).Distinct().Where(d => !string.IsNullOrEmpty(d) && d != "Yönetim").ToList();

            var subeler = users
                .Select(u => u.Sube).Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();

            var personnelList = users.Where(u => u.AktifMi).OrderBy(u => u.AdSoyad).ToList();

            ViewBag.Departments = departments;
            ViewBag.Subeler = subeler;
            ViewBag.PersonnelList = personnelList;
        }

        [Authorize(Roles = "Yonetici")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateCreateViewBagsAsync();
            return View(new CreateOvertimeDto());
        }

        [Authorize(Roles = "Yonetici")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOvertimeDto dto)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCreateViewBagsAsync();
                return View(dto);
            }

            try
            {
                dto.OlusturanUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var created = await _overtimeService.CreateOvertimeAsync(dto);

                TempData["SuccessMessage"] = $"{created.Tarih:dd.MM.yyyy} tarihli mesai başarıyla oluşturuldu ve adil sıradaki personele teklif gönderildi.";
                return RedirectToAction("Index");
            }
            catch (System.InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                await PopulateCreateViewBagsAsync();
                return View(dto);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Beklenmeyen bir hata oluştu: " + ex.Message;
                await PopulateCreateViewBagsAsync();
                return View(dto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingOffers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var offers = await _overtimeService.GetPendingOffersForUserAsync(userId);
            return View(offers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondOffer(int offerId, bool isAccepted, string? rejectReason)
        {
            try
            {
                await _overtimeService.ProcessOfferResponseAsync(offerId, isAccepted, rejectReason);
                TempData["SuccessMessage"] = isAccepted
                    ? "Mesai teklifini kabul ettiniz. Sıranız güncellendi."
                    : "Mesai teklifini reddettiniz. Teklif sıradaki personele iletildi.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("PendingOffers");
        }

        [HttpGet]
        public async Task<IActionResult> MyOvertimes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var all = await _overtimeService.GetAllOvertimesAsync();
            var myOvertimes = all.Where(o => o.AtananUserId == userId);
            return View(myOvertimes);
        }

        [HttpGet]
        public async Task<IActionResult> Calendar()
        {
            var overtimes = await _overtimeService.GetAllOvertimesAsync();
            var events = new System.Collections.Generic.List<object>();
            
            events.AddRange(overtimes.Select(o => {
                string title;
                if (!string.IsNullOrEmpty(o.AtananUserId))
                {
                    title = o.AtananUserAdSoyad ?? "Atandı";
                }
                else if (!string.IsNullOrEmpty(o.ActivePersonnelAdSoyad))
                {
                    title = $"Bekliyor ({o.ActivePersonnelAdSoyad})";
                }
                else if (!string.IsNullOrEmpty(o.NextCandidateAdSoyad))
                {
                    title = $"Bekliyor ({o.NextCandidateAdSoyad})";
                }
                else
                {
                    title = "Bekliyor (Aday Aranıyor)";
                }

                return new
                {
                    id = "o_" + o.Id,
                    title = title,
                    start = o.Tarih.ToString("yyyy-MM-dd") + "T" + o.BaslangicSaati.ToString(@"hh\:mm"),
                    end = o.Tarih.ToString("yyyy-MM-dd") + "T" + o.BitisSaati.ToString(@"hh\:mm"),
                    color = o.AtananUserId != null ? "#10b981" : "#f59e0b",
                    description = o.Aciklama,
                    departman = o.Departman,
                    sube = o.Sube,
                    personel = o.AtananUserAdSoyad ?? o.ActivePersonnelAdSoyad ?? o.NextCandidateAdSoyad ?? "Henüz Belirlenmedi",
                    durum = o.AtananUserId != null ? "Atandı / Onaylandı" : (!string.IsNullOrEmpty(o.ActivePersonnelAdSoyad) ? $"Yanıt Bekleniyor ({o.ActivePersonnelAdSoyad})" : "Aday Aranıyor")
                };
            }));

            var leaves = await _leaveService.GetAllLeavesAsync();
            events.AddRange(leaves.Select(l => new
            {
                id = "l_" + l.Id,
                title = $"İzin: {l.PersonnelAdSoyad}",
                start = l.BaslangicTarihi.ToString("yyyy-MM-dd"),
                end = l.BitisTarihi.AddDays(1).ToString("yyyy-MM-dd"), // FullCalendar allDay end is exclusive
                color = "#dc3545", // Kırmızı
                description = $"İzin Türü: {l.IzinTuru} \nAçıklama: {l.Aciklama}",
                personel = l.PersonnelAdSoyad,
                durum = "İzinli",
                departman = "-",
                sube = "-"
            }));

            ViewBag.CalendarEventsJson = System.Text.Json.JsonSerializer.Serialize(events);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SwapRequests()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var pendingRequests = await _swapRequestService.GetPendingSwapRequestsForUserAsync(userId);
            return View(pendingRequests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondSwap(int swapRequestId, bool isApproved)
        {
            try
            {
                await _swapRequestService.ProcessSwapResponseAsync(swapRequestId, isApproved);
                TempData["SuccessMessage"] = isApproved ? "Mesai değişimi onaylandı." : "Mesai değişimi reddedildi.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("SwapRequests");
        }

        [HttpPost]
        [Authorize(Roles = "Yonetici")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int overtimeId, string reason)
        {
            await _overtimeService.CancelOvertimeAsync(overtimeId, reason);
            TempData["SuccessMessage"] = "Mesai iptal edildi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitHours(SubmitOvertimeHoursDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                await _overtimeService.SubmitActualHoursAsync(dto, userId);
                TempData["SuccessMessage"] = "Çalışma saatleriniz başarıyla bildirildi ve yöneticinin onayına sunuldu.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("MyOvertimes");
        }

        [HttpPost]
        [Authorize(Roles = "Yonetici")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveHours(int overtimeId)
        {
            try
            {
                var managerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                await _overtimeService.ApproveHoursAsync(overtimeId, managerUserId);
                TempData["SuccessMessage"] = "Bildirilen çalışma saatleri onaylandı ve mesai tamamlandı.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Yonetici")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectHours(int overtimeId, string? reason)
        {
            try
            {
                var managerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                await _overtimeService.RejectHoursAsync(overtimeId, managerUserId, reason);
                TempData["SuccessMessage"] = "Bildirilen çalışma saatleri reddedildi.";
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            return RedirectToAction("Index");
        }
    }
}
