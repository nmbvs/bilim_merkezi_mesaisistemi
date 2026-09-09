using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.Interfaces;

namespace MesaiYonetimSistemi.Web.Controllers
{
    [Authorize]
    public class QueueController : Controller
    {
        private readonly IQueueService _queueService;

        public QueueController(IQueueService queueService)
        {
            _queueService = queueService;
        }

        public async Task<IActionResult> Index(string? department, string? sube)
        {
            var queue = await _queueService.GetQueueListAsync(department, sube);
            return View(queue);
        }

        [Authorize(Roles = "Yonetici")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderQueue([FromBody] List<string> orderedPersonnelIds)
        {
            if (orderedPersonnelIds == null || orderedPersonnelIds.Count == 0)
                return BadRequest("Geçersiz sıralama verisi.");

            await _queueService.ReorderQueuePositionsAsync(orderedPersonnelIds);
            return Json(new { success = true, message = "Sıra pozisyonları güncellendi." });
        }

        [Authorize(Roles = "Yonetici")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetQueue()
        {
            await _queueService.ResetQueuePositionsAsync();
            TempData["SuccessMessage"] = "Sıralama varsayılan pozisyonlarına sıfırlandı.";
            return RedirectToAction("Index");
        }
    }
}
