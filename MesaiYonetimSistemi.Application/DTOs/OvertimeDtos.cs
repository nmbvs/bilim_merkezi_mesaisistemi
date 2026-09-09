using System;

namespace MesaiYonetimSistemi.Application.DTOs
{
    public class OvertimeDto
    {
        public int Id { get; set; }
        public DateTime Tarih { get; set; }
        public TimeSpan BaslangicSaati { get; set; }
        public TimeSpan BitisSaati { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public int Kontenjan { get; set; }
        public string Departman { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public bool ResmiTatilMi { get; set; }
        public bool SaatBelirsizMi { get; set; }
        public TimeSpan? BildirilenBaslangicSaati { get; set; }
        public TimeSpan? BildirilenBitisSaati { get; set; }
        public MesaiYonetimSistemi.Core.Enums.SaatBildirimStatus SaatBildirimDurumu { get; set; }
        public string? PersonelSaatNotu { get; set; }
        public string Durum { get; set; } = string.Empty;
        public string? AtananUserId { get; set; }
        public string? AtananUserAdSoyad { get; set; }
        public OfferDto? ActiveOffer { get; set; }
        public string? ActivePersonnelAdSoyad { get; set; }
        public string? NextCandidatePersonnelId { get; set; }
        public string? NextCandidateAdSoyad { get; set; }
        public int NextCandidateSiraNo { get; set; }
        public System.Collections.Generic.List<OfferDto> OfferHistory { get; set; } = new System.Collections.Generic.List<OfferDto>();
        public DateTime OlusturmaTarihi { get; set; }
    }

    public class CreateOvertimeDto
    {
        public DateTime Tarih { get; set; } = DateTime.Today.AddDays(1);
        public TimeSpan BaslangicSaati { get; set; } = new TimeSpan(08, 00, 00);
        public TimeSpan BitisSaati { get; set; } = new TimeSpan(17, 00, 00);
        public bool SaatBelirsizMi { get; set; } = false;
        public string Aciklama { get; set; } = string.Empty;
        public int Kontenjan { get; set; } = 1;
        public string Departman { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public bool ResmiTatilMi { get; set; } = false;
        public string OlusturanUserId { get; set; } = string.Empty;
        public string? SecilenIlkPersonelId { get; set; }
    }

    public class SubmitOvertimeHoursDto
    {
        public int OvertimeId { get; set; }
        public TimeSpan BaslangicSaati { get; set; }
        public TimeSpan BitisSaati { get; set; }
        public string? PersonelSaatNotu { get; set; }
    }

    public class OfferDto
    {
        public int Id { get; set; }
        public int OvertimeId { get; set; }
        public DateTime OvertimeTarih { get; set; }
        public string OvertimeAciklama { get; set; } = string.Empty;
        public TimeSpan BaslangicSaati { get; set; }
        public TimeSpan BitisSaati { get; set; }
        public string PersonnelId { get; set; } = string.Empty;
        public string PersonnelAdSoyad { get; set; } = string.Empty;
        public DateTime TeklifTarihi { get; set; }
        public DateTime SonCevapTarihi { get; set; }
        public string Durum { get; set; } = string.Empty;
        public int SiraNo { get; set; }
        public double KalanSaat { get; set; }
        public string? RedNedeni { get; set; }
    }

    public class OvertimeSwapDto
    {
        public int Id { get; set; }
        public int OvertimeId { get; set; }
        public DateTime OvertimeTarih { get; set; }
        public string OvertimeAciklama { get; set; } = string.Empty;
        public string IstekYapanUserId { get; set; } = string.Empty;
        public string IstekYapanAdSoyad { get; set; } = string.Empty;
        public string HedefUserId { get; set; } = string.Empty;
        public string HedefAdSoyad { get; set; } = string.Empty;
        public string Durum { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public DateTime OlusturmaTarihi { get; set; }
    }
}
