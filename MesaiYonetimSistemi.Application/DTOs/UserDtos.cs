using System;

namespace MesaiYonetimSistemi.Application.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string TcKimlikNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public DateTime IseGirisTarihi { get; set; }
        public bool AktifMi { get; set; }
        public string Role { get; set; } = string.Empty;
        public int QueuePosition { get; set; }
        public int TotalAccepted { get; set; }
        public int TotalRejected { get; set; }
        public string IlkSifre { get; set; } = string.Empty;
    }

    public class CreateUserDto
    {
        public string AdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string TcKimlikNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public DateTime IseGirisTarihi { get; set; } = DateTime.Now;
        public string Role { get; set; } = "Personel";
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string TcKimlikNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public DateTime IseGirisTarihi { get; set; }
        public bool AktifMi { get; set; } = true;
        public string Role { get; set; } = "Personel";
    }

    public class ChangePasswordDto
    {
        public string UserId { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class ExcelUserImportDto
    {
        public string AdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string TcKimlikNo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
    }
}
