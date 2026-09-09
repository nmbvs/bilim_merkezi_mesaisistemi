using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Interfaces;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: LeaveService.cs
// KATMAN: Application (Uygulama/İş Mantığı Katmanı)
//
// İzin süreçlerini koordine eder. Tarih çakışması, izin kontrolü
// gibi temel iş kurallarını (business logic) uygular.
// ════════════════════════════════════════════════════════════════════════════════
namespace MesaiYonetimSistemi.Application.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly IUnitOfWork _unitOfWork;

        public LeaveService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<LeaveDto> CreateLeaveAsync(CreateLeaveDto dto)
        {
            // Bitiş tarihi başlangıç tarihinden küçük olamaz
            if (dto.BitisTarihi.Date < dto.BaslangicTarihi.Date)
                throw new InvalidOperationException("İzin bitiş tarihi, başlangıç tarihinden önce olamaz.");

            // Personelin aynı tarihlerde mevcut bir izni var mı? (Tarih kesişimi kontrolü)
            var existingLeaves = await _unitOfWork.PersonnelLeaves.FindAsync(l => 
                l.PersonnelId == dto.PersonnelId &&
                (l.BaslangicTarihi.Date <= dto.BitisTarihi.Date && l.BitisTarihi.Date >= dto.BaslangicTarihi.Date)
            );

            if (existingLeaves.Any())
            {
                throw new InvalidOperationException($"Bu personelin seçilen tarih aralığında ({dto.BaslangicTarihi:dd.MM.yyyy} - {dto.BitisTarihi:dd.MM.yyyy}) zaten bir izni bulunmaktadır.");
            }

            var leave = new PersonnelLeave
            {
                PersonnelId = dto.PersonnelId,
                BaslangicTarihi = dto.BaslangicTarihi.Date,
                BitisTarihi = dto.BitisTarihi.Date,
                IzinTuru = dto.IzinTuru,
                Aciklama = dto.Aciklama,
                OlusturanUserId = dto.OlusturanUserId,
                OlusturulmaTarihi = DateTime.Now
            };

            await _unitOfWork.PersonnelLeaves.AddAsync(leave);
            await _unitOfWork.SaveChangesAsync();

            return await GetLeaveDtoByIdAsync(leave.Id);
        }

        public async Task DeleteLeaveAsync(int id)
        {
            var leave = await _unitOfWork.PersonnelLeaves.GetByIdAsync(id);
            if (leave != null)
            {
                _unitOfWork.PersonnelLeaves.Remove(leave);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<LeaveDto>> GetAllLeavesAsync()
        {
            var allLeaves = (await _unitOfWork.PersonnelLeaves.GetAllAsync())
                            .OrderByDescending(x => x.BaslangicTarihi)
                            .ToList();
                            
            var dtoList = new List<LeaveDto>();
            foreach (var leave in allLeaves)
            {
                dtoList.Add(await PopulateDtoAsync(leave));
            }

            return dtoList;
        }

        public async Task<IEnumerable<LeaveDto>> GetLeavesByPersonnelIdAsync(string personnelId)
        {
            var leaves = await _unitOfWork.PersonnelLeaves.FindAsync(x => x.PersonnelId == personnelId);
            leaves = leaves.OrderByDescending(x => x.BaslangicTarihi).ToList();
            
            var dtoList = new List<LeaveDto>();
            foreach (var leave in leaves)
            {
                dtoList.Add(await PopulateDtoAsync(leave));
            }
            return dtoList;
        }

        public async Task<bool> IsPersonnelOnLeaveAsync(string personnelId, DateTime date)
        {
            var onLeave = await _unitOfWork.PersonnelLeaves.FindAsync(l => 
                l.PersonnelId == personnelId && 
                l.BaslangicTarihi.Date <= date.Date && 
                l.BitisTarihi.Date >= date.Date
            );

            return onLeave.Any();
        }

        private async Task<LeaveDto> GetLeaveDtoByIdAsync(int id)
        {
            var leave = await _unitOfWork.PersonnelLeaves.GetByIdAsync(id);
            if (leave == null) return null;
            return await PopulateDtoAsync(leave);
        }

        private async Task<LeaveDto> PopulateDtoAsync(PersonnelLeave leave)
        {
            var dto = new LeaveDto
            {
                Id = leave.Id,
                PersonnelId = leave.PersonnelId,
                BaslangicTarihi = leave.BaslangicTarihi,
                BitisTarihi = leave.BitisTarihi,
                IzinTuru = leave.IzinTuru,
                Aciklama = leave.Aciklama,
                OlusturanUserId = leave.OlusturanUserId,
                OlusturulmaTarihi = leave.OlusturulmaTarihi
            };

            var personel = await _unitOfWork.Users.GetByIdAsync(leave.PersonnelId);
            if (personel != null)
                dto.PersonnelAdSoyad = personel.AdSoyad;

            var olusturan = await _unitOfWork.Users.GetByIdAsync(leave.OlusturanUserId);
            if (olusturan != null)
                dto.OlusturanUserAdSoyad = olusturan.AdSoyad;

            return dto;
        }
    }
}
