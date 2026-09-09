using System;
using System.Collections.Generic;

namespace MesaiYonetimSistemi.Application.DTOs
{
    public class QueueDto
    {
        public int Id { get; set; }
        public string PersonnelId { get; set; } = string.Empty;
        public string PersonnelAdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public int SiraPozisyonu { get; set; }
        public DateTime? SonMesaiTarihi { get; set; }
        public int ToplamKabulEdilenMesai { get; set; }
        public int ToplamReddedilenMesai { get; set; }
        public bool AktifMi { get; set; }
    }

    public class AnnouncementDto
    {
        public int Id { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string Icerik { get; set; } = string.Empty;
        public DateTime OlusturmaTarihi { get; set; }
        public string YayinlayanAdSoyad { get; set; } = string.Empty;
        public bool AktifMi { get; set; }
        public bool OncelikliMi { get; set; }
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public string Mesaj { get; set; } = string.Empty;
        public bool OkunduMu { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string Tip { get; set; } = string.Empty;
        public string? RelatedUrl { get; set; }
    }

    public class DashboardDto
    {
        public int TotalPersonnelCount { get; set; }
        public int ActiveOvertimesCount { get; set; }
        public int PendingOffersCount { get; set; }
        public int CompletedOvertimesCount { get; set; }
        public double AcceptanceRate { get; set; }
        public IEnumerable<OvertimeDto> RecentOvertimes { get; set; } = new List<OvertimeDto>();
        public IEnumerable<OfferDto> PendingOffers { get; set; } = new List<OfferDto>();
        public IEnumerable<AnnouncementDto> RecentAnnouncements { get; set; } = new List<AnnouncementDto>();
        public Dictionary<string, int> DepartmentDistribution { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> MonthlyOvertimeStats { get; set; } = new Dictionary<string, int>();
    }
}
