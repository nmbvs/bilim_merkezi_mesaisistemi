using AutoMapper;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Core.Entities;
using System;

namespace MesaiYonetimSistemi.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User Mappings
            CreateMap<ApplicationUser, UserDto>()
                .ForMember(dest => dest.QueuePosition, opt => opt.MapFrom(src => src.QueueItem != null ? src.QueueItem.SiraPozisyonu : 0))
                .ForMember(dest => dest.TotalAccepted, opt => opt.MapFrom(src => src.QueueItem != null ? src.QueueItem.ToplamKabulEdilenMesai : 0))
                .ForMember(dest => dest.TotalRejected, opt => opt.MapFrom(src => src.QueueItem != null ? src.QueueItem.ToplamReddedilenMesai : 0));

            CreateMap<CreateUserDto, ApplicationUser>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.IlkSifre, opt => opt.MapFrom(src => src.Password));

            CreateMap<UpdateUserDto, ApplicationUser>();

            // Overtime Mappings
            CreateMap<Overtime, OvertimeDto>()
                .ForMember(dest => dest.Durum, opt => opt.MapFrom(src => src.Durum.ToString()))
                .ForMember(dest => dest.AtananUserAdSoyad, opt => opt.MapFrom(src => src.AtananUser != null ? src.AtananUser.AdSoyad : null));

            CreateMap<CreateOvertimeDto, Overtime>();

            // Offer Mappings
            CreateMap<OvertimeOffer, OfferDto>()
                .ForMember(dest => dest.PersonnelAdSoyad, opt => opt.MapFrom(src => src.Personnel != null ? src.Personnel.AdSoyad : ""))
                .ForMember(dest => dest.OvertimeTarih, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.Tarih : DateTime.MinValue))
                .ForMember(dest => dest.OvertimeAciklama, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.Aciklama : ""))
                .ForMember(dest => dest.BaslangicSaati, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.BaslangicSaati : TimeSpan.Zero))
                .ForMember(dest => dest.BitisSaati, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.BitisSaati : TimeSpan.Zero))
                .ForMember(dest => dest.Durum, opt => opt.MapFrom(src => src.Durum.ToString()))
                .ForMember(dest => dest.KalanSaat, opt => opt.MapFrom(src => Math.Max(0, (src.SonCevapTarihi - DateTime.Now).TotalHours)));

            // Queue Mappings
            CreateMap<QueueItem, QueueDto>()
                .ForMember(dest => dest.PersonnelAdSoyad, opt => opt.MapFrom(src => src.Personnel != null ? src.Personnel.AdSoyad : ""))
                .ForMember(dest => dest.SicilNo, opt => opt.MapFrom(src => src.Personnel != null ? src.Personnel.SicilNo : ""))
                .ForMember(dest => dest.Departman, opt => opt.MapFrom(src => src.Personnel != null ? src.Personnel.Departman : ""))
                .ForMember(dest => dest.Sube, opt => opt.MapFrom(src => src.Personnel != null ? src.Personnel.Sube : ""));

            // Announcement Mappings
            CreateMap<Announcement, AnnouncementDto>()
                .ForMember(dest => dest.YayinlayanAdSoyad, opt => opt.MapFrom(src => src.YayinlayanUser != null ? src.YayinlayanUser.AdSoyad : "Yönetim"));

            // Notification Mappings
            CreateMap<Notification, NotificationDto>()
                .ForMember(dest => dest.Tip, opt => opt.MapFrom(src => src.Tip.ToString()));

            // Swap Request Mappings
            CreateMap<OvertimeSwapRequest, OvertimeSwapDto>()
                .ForMember(dest => dest.IstekYapanAdSoyad, opt => opt.MapFrom(src => src.IstekYapanUser != null ? src.IstekYapanUser.AdSoyad : ""))
                .ForMember(dest => dest.HedefAdSoyad, opt => opt.MapFrom(src => src.HedefUser != null ? src.HedefUser.AdSoyad : ""))
                .ForMember(dest => dest.OvertimeTarih, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.Tarih : DateTime.MinValue))
                .ForMember(dest => dest.OvertimeAciklama, opt => opt.MapFrom(src => src.Overtime != null ? src.Overtime.Aciklama : ""))
                .ForMember(dest => dest.Durum, opt => opt.MapFrom(src => src.Durum.ToString()));
        }
    }
}
