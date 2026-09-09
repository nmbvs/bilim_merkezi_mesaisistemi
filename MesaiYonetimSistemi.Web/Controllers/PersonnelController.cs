using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Interfaces;

using MesaiYonetimSistemi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MesaiYonetimSistemi.Web.Controllers
{
    [Authorize(Roles = "Yonetici")]
    public class PersonnelController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IQueueService _queueService;
        private readonly IExportService _exportService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;

        public PersonnelController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IQueueService queueService,
            IExportService exportService,
            IUnitOfWork unitOfWork,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _queueService = queueService;
            _exportService = exportService;
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? department, string? sube, bool showPassive = false)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.QueueItem)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.AdSoyad.ToLower().Contains(s) ||
                                         u.SicilNo.ToLower().Contains(s) ||
                                         (u.Email != null && u.Email.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(u => u.Departman == department);

            if (!string.IsNullOrWhiteSpace(sube))
                query = query.Where(u => u.Sube == sube);

            var users = await query.ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();

            // Fetch roles for all retrieved users in 1 single database query
            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where userIds.Contains(ur.UserId)
                                   select new { ur.UserId, RoleName = r.Name })
                                  .ToListAsync();

            var roleLookup = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First().RoleName);

            var dtoList = users.Select(user => new UserDto
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
                Role = roleLookup.TryGetValue(user.Id, out var role) ? role : "Personel",
                QueuePosition = user.QueueItem?.SiraPozisyonu ?? 0,
                TotalAccepted = user.QueueItem?.ToplamKabulEdilenMesai ?? 0,
                TotalRejected = user.QueueItem?.ToplamReddedilenMesai ?? 0,
                IlkSifre = user.IlkSifre
            }).ToList();

            ViewBag.Departments = users.Select(u => u.Departman).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList();
            ViewBag.Subeler = users.Select(u => u.Sube).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            return View(dtoList.OrderBy(u => u.QueuePosition == 0 ? 999 : u.QueuePosition));
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Departments = _userManager.Users.Select(u => u.Departman).Distinct().Where(d => !string.IsNullOrEmpty(d)).ToList();
            ViewBag.Subeler = _userManager.Users.Select(u => u.Sube).Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();
            ViewBag.Gorevler = _userManager.Users.Select(u => u.Gorev).Distinct().Where(g => !string.IsNullOrEmpty(g)).ToList();
            return View(new CreateUserDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanılıyor.");
                return View(dto);
            }

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                AdSoyad = dto.AdSoyad,
                SicilNo = dto.SicilNo,
                TcKimlikNo = dto.TcKimlikNo,
                PhoneNumber = dto.PhoneNumber,
                Departman = dto.Departman,
                Gorev = dto.Gorev,
                Sube = dto.Sube,
                IseGirisTarihi = dto.IseGirisTarihi,
                AktifMi = true,
                IlkSifre = dto.Password,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (result.Succeeded)
            {
                var role = string.IsNullOrWhiteSpace(dto.Role) ? "Personel" : dto.Role;
                await _userManager.AddToRoleAsync(user, role);

                // Add to fair queue
                var totalQueue = await _unitOfWork.Queue.CountAsync();
                await _unitOfWork.Queue.AddAsync(new QueueItem
                {
                    PersonnelId = user.Id,
                    SiraPozisyonu = totalQueue + 1,
                    AktifMi = true
                });
                await _unitOfWork.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{user.AdSoyad} adlı personel başarıyla eklendi.";
                return RedirectToAction("Index");
            }

            foreach (var err in result.Errors)
                ModelState.AddModelError("", err.Description);

            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var dto = new UpdateUserDto
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
                Role = roles.FirstOrDefault() ?? "Personel"
            };

            ViewBag.Departments = _userManager.Users.Select(u => u.Departman).Distinct().Where(d => !string.IsNullOrEmpty(d)).ToList();
            ViewBag.Subeler = _userManager.Users.Select(u => u.Sube).Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();
            ViewBag.Gorevler = _userManager.Users.Select(u => u.Gorev).Distinct().Where(g => !string.IsNullOrEmpty(g)).ToList();

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UpdateUserDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var user = await _userManager.FindByIdAsync(dto.Id);
            if (user == null) return NotFound();

            user.AdSoyad = dto.AdSoyad;
            user.SicilNo = dto.SicilNo;
            user.TcKimlikNo = dto.TcKimlikNo;
            user.PhoneNumber = dto.PhoneNumber;
            user.Departman = dto.Departman;
            user.Gorev = dto.Gorev;
            user.Sube = dto.Sube;
            user.IseGirisTarihi = dto.IseGirisTarihi;
            user.AktifMi = dto.AktifMi;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(dto.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, dto.Role);
                }

                // Sync Queue item active status
                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(user.Id);
                if (qItem != null)
                {
                    qItem.AktifMi = user.AktifMi;
                    _unitOfWork.Queue.Update(qItem);
                    await _unitOfWork.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"{user.AdSoyad} bilgiler başarıyla güncellendi.";
                return RedirectToAction("Index");
            }

            foreach (var err in result.Errors)
                ModelState.AddModelError("", err.Description);

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.AktifMi = !user.AktifMi;
                await _userManager.UpdateAsync(user);

                var qItem = await _unitOfWork.Queue.GetQueueByPersonnelIdAsync(user.Id);
                if (qItem != null)
                {
                    qItem.AktifMi = user.AktifMi;
                    _unitOfWork.Queue.Update(qItem);
                    await _unitOfWork.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"{user.AdSoyad} durumu güncellendi ({(user.AktifMi ? "Aktif" : "Pasif")}).";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFromExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Lütfen geçerli bir Excel (.xlsx) dosyası seçin.";
                return RedirectToAction("Index");
            }

            try
            {
                using var stream = excelFile.OpenReadStream();
                var list = await _exportService.ImportPersonnelFromExcelAsync(stream);

                int successCount = 0;
                int skippedCount = 0;

                foreach (var item in list)
                {
                    var email = string.IsNullOrWhiteSpace(item.Email)
                        ? $"{item.AdSoyad.ToLower().Replace(" ", ".")}@mesai.com"
                        : item.Email;

                    var existing = await _userManager.FindByEmailAsync(email);
                    if (existing != null)
                    {
                        skippedCount++;
                        continue;
                    }

                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        AdSoyad = item.AdSoyad,
                        SicilNo = string.IsNullOrWhiteSpace(item.SicilNo) ? $"PER-{new Random().Next(100, 999)}" : item.SicilNo,
                        TcKimlikNo = string.IsNullOrWhiteSpace(item.TcKimlikNo) ? "11111111111" : item.TcKimlikNo,
                        PhoneNumber = item.Telefon,
                        Departman = string.IsNullOrWhiteSpace(item.Departman) ? "Genel" : item.Departman,
                        Gorev = string.IsNullOrWhiteSpace(item.Gorev) ? "Personel" : item.Gorev,
                        Sube = string.IsNullOrWhiteSpace(item.Sube) ? "Genel Merkez" : item.Sube,
                        IlkSifre = "Personel123!",
                        IseGirisTarihi = DateTime.Now,
                        AktifMi = true,
                        EmailConfirmed = true
                    };

                    var res = await _userManager.CreateAsync(user, "Personel123!");
                    if (res.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Personel");

                        var maxPos = await _unitOfWork.Queue.CountAsync();
                        await _unitOfWork.Queue.AddAsync(new QueueItem
                        {
                            PersonnelId = user.Id,
                            SiraPozisyonu = maxPos + 1,
                            AktifMi = true
                        });
                        await _unitOfWork.SaveChangesAsync();
                        successCount++;
                    }
                }

                TempData["SuccessMessage"] = $"{successCount} adet personel Excel'den başarıyla aktarıldı. ({skippedCount} adet mevcut kayıt atlandı)";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Excel içe aktarımında bir hata oluştu: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
