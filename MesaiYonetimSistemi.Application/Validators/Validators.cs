using FluentValidation;
using MesaiYonetimSistemi.Application.DTOs;
using System;

namespace MesaiYonetimSistemi.Application.Validators
{
    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.AdSoyad)
                .NotEmpty().WithMessage("Ad Soyad alanı boş geçilemez.")
                .Length(3, 100).WithMessage("Ad Soyad 3 ile 100 karakter arasında olmalıdır.");

            RuleFor(x => x.SicilNo)
                .NotEmpty().WithMessage("Sicil No boş geçilemez.");

            RuleFor(x => x.TcKimlikNo)
                .NotEmpty().WithMessage("TC Kimlik No boş geçilemez.")
                .Length(11).WithMessage("TC Kimlik No 11 haneli olmalıdır.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-posta adresi boş geçilemez.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

            RuleFor(x => x.Departman)
                .NotEmpty().WithMessage("Departman boş geçilemez.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Şifre boş geçilemez.")
                .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
        }
    }

    public class CreateOvertimeValidator : AbstractValidator<CreateOvertimeDto>
    {
        public CreateOvertimeValidator()
        {
            RuleFor(x => x.Tarih)
                .GreaterThanOrEqualTo(DateTime.Today).WithMessage("Mesai tarihi geçmiş bir tarih olamaz.");

            RuleFor(x => x.Aciklama)
                .NotEmpty().WithMessage("Açıklama alanı boş bırakılamaz.")
                .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");

            RuleFor(x => x.Kontenjan)
                .GreaterThan(0).WithMessage("Kontenjan en az 1 olmalıdır.");

            RuleFor(x => x.BaslangicSaati)
                .LessThan(x => x.BitisSaati).WithMessage("Başlangıç saati bitiş saatinden önce olmalıdır.");
        }
    }

    public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Mevcut şifre gereklidir.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Yeni şifre gereklidir.")
                .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");

            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("Yeni şifre ile onay şifresi eşleşmiyor.");
        }
    }

    public class AnnouncementValidator : AbstractValidator<AnnouncementDto>
    {
        public AnnouncementValidator()
        {
            RuleFor(x => x.Baslik)
                .NotEmpty().WithMessage("Duyuru başlığı boş olamaz.")
                .MaximumLength(150).WithMessage("Başlık en fazla 150 karakter olabilir.");

            RuleFor(x => x.Icerik)
                .NotEmpty().WithMessage("Duyuru içeriği boş olamaz.");
        }
    }
}
