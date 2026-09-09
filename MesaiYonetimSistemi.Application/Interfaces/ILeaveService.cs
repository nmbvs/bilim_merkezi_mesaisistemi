using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;

namespace MesaiYonetimSistemi.Application.Interfaces
{
    public interface ILeaveService
    {
        /// <summary>
        /// Yeni bir personel izni oluşturur.
        /// İş kuralı: Aynı tarihler arasında mevcut izni olup olmadığı kontrol edilir.
        /// </summary>
        Task<LeaveDto> CreateLeaveAsync(CreateLeaveDto dto);

        /// <summary>
        /// İzni veritabanından siler.
        /// </summary>
        Task DeleteLeaveAsync(int id);

        /// <summary>
        /// Tüm izin kayıtlarını getirir.
        /// </summary>
        Task<IEnumerable<LeaveDto>> GetAllLeavesAsync();

        /// <summary>
        /// Belirli bir personelin izin kayıtlarını getirir.
        /// </summary>
        Task<IEnumerable<LeaveDto>> GetLeavesByPersonnelIdAsync(string personnelId);

        /// <summary>
        /// Belirli bir personelin, verilen tarihte izinli olup olmadığını kontrol eder.
        /// </summary>
        Task<bool> IsPersonnelOnLeaveAsync(string personnelId, DateTime date);
    }
}
