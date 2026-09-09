using System;
using System.ComponentModel.DataAnnotations;

namespace MesaiYonetimSistemi.Application.DTOs
{
    public class LeaveDto
    {
        public int Id { get; set; }
        public string PersonnelId { get; set; } = string.Empty;
        public string PersonnelAdSoyad { get; set; } = string.Empty;
        
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime BaslangicTarihi { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime BitisTarihi { get; set; }
        
        public string IzinTuru { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public DateTime OlusturulmaTarihi { get; set; }
        public string OlusturanUserId { get; set; } = string.Empty;
        public string OlusturanUserAdSoyad { get; set; } = string.Empty;
    }

    public class CreateLeaveDto
    {
        [Required(ErrorMessage = "Personel seçimi zorunludur.")]
        public string PersonnelId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
        public DateTime BaslangicTarihi { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Bitiş tarihi zorunludur.")]
        public DateTime BitisTarihi { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "İzin türü zorunludur.")]
        public string IzinTuru { get; set; } = string.Empty;

        public string Aciklama { get; set; } = string.Empty;
        
        public string OlusturanUserId { get; set; } = string.Empty;
    }
}
