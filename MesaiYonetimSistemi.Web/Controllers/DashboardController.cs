using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.Interfaces;

namespace MesaiYonetimSistemi.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Yonetici"))
            {
                var adminData = await _dashboardService.GetAdminDashboardDataAsync();
                return View("Admin", adminData);
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var personnelData = await _dashboardService.GetPersonnelDashboardDataAsync(userId);
                return View("Personnel", personnelData);
            }
        }
    }
}
