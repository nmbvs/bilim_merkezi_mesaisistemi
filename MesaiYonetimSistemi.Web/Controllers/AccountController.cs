using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Core.Entities;

namespace MesaiYonetimSistemi.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string? portalType = "Personel", string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            ViewBag.PortalType = portalType ?? "Personel";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? portalType = "Personel", string? returnUrl = null)
        {
            ViewBag.PortalType = portalType ?? "Personel";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Lütfen e-posta ve şifre giriniz.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.AktifMi)
            {
                ModelState.AddModelError("", "Kullanıcı bulunamadı veya hesabınız pasif durumda.");
                return View();
            }

            // Verify Portal Role Access
            var roles = await _userManager.GetRolesAsync(user);
            bool isYonetici = roles.Contains("Yonetici");

            if (portalType == "Yonetici" && !isYonetici)
            {
                ModelState.AddModelError("", "Bu portal yalnızca Yöneticiler içindir. Lütfen Personel Girişi sekmesini kullanınız.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Dashboard");
            }

            ModelState.AddModelError("", "Hatalı e-posta veya şifre.");
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserDto
            {
                Id = user.Id,
                AdSoyad = user.AdSoyad,
                SicilNo = user.SicilNo,
                TcKimlikNo = user.TcKimlikNo,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber ?? "",
                Departman = user.Departman,
                Gorev = user.Gorev,
                Sube = user.Sube,
                IseGirisTarihi = user.IseGirisTarihi,
                AktifMi = user.AktifMi,
                Role = roles.Count > 0 ? roles[0] : "Personel"
            };

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordDto());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirildi.";
                return RedirectToAction("Profile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(dto);
        }
    }
}
