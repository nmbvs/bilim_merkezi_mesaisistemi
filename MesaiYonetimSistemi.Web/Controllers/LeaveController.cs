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
    [Authorize(Roles = "Yonetici")] // Sadece yöneticiler izin ekleyip silebilir
    public class LeaveController : Controller
    {
        private readonly ILeaveService _leaveService;
        private readonly IUnitOfWork _unitOfWork;

        public LeaveController(ILeaveService leaveService, IUnitOfWork unitOfWork)
        {
            _leaveService = leaveService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var leaves = await _leaveService.GetAllLeavesAsync();
            return View(leaves);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.AktifMi);
            ViewBag.Users = users.OrderBy(u => u.AdSoyad).ToList();
            return View(new CreateLeaveDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateLeaveDto dto)
        {
            if (!ModelState.IsValid)
            {
                var users = await _unitOfWork.Users.FindAsync(u => u.AktifMi);
                ViewBag.Users = users.OrderBy(u => u.AdSoyad).ToList();
                return View(dto);
            }

            try
            {
                dto.OlusturanUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                await _leaveService.CreateLeaveAsync(dto);
                TempData["SuccessMessage"] = "Personel izni başarıyla oluşturuldu.";
                return RedirectToAction("Index");
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                var users = await _unitOfWork.Users.FindAsync(u => u.AktifMi);
                ViewBag.Users = users.OrderBy(u => u.AdSoyad).ToList();
                return View(dto);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _leaveService.DeleteLeaveAsync(id);
            TempData["SuccessMessage"] = "İzin kaydı silindi.";
            return RedirectToAction("Index");
        }
    }
}
