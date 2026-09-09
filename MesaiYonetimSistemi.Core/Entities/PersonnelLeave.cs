using System;

// ════════════════════════════════════════════════════════════════════════════════
// DOSYA: PersonnelLeave.cs
// KATMAN: Core (Alan Modeli - Domain Layer)
//
// Bu sınıf personellerin izin durumlarını temsil eder.
// Sadece yönetici tarafından eklenebilir, silinebilir veya güncellenebilir.
// ════════════════════════════════════════════════════════════════════════════════
namespace MesaiYonetimSistemi.Core.Entities
{
    /// <summary>
    /// Personelin kullanmış olduğu veya kullanacağı izinleri temsil eder.
    /// Mesai atamalarında izinli günlere mesai atanmasını önlemek için kullanılır.
    /// </summary>
    public class PersonnelLeave
    {
        public int Id { get; set; }

        /// <summary>İzni kullanan personelin kullanıcı ID'si.</summary>
        public string PersonnelId { get; set; } = string.Empty;

        /// <summary>İlgili personelin navigasyon referansı.</summary>
        public virtual ApplicationUser? Personnel { get; set; }

        /// <summary>İzinin başlangıç tarihi (Bu tarih dahil).</summary>
        public DateTime BaslangicTarihi { get; set; }

        /// <summary>İzinin bitiş tarihi (Bu tarih dahil).</summary>
        public DateTime BitisTarihi { get; set; }

        /// <summary>İzin türü (Örn: "Yıllık İzin", "Mazeret İzni", "Haftalık İzin").</summary>
        public string IzinTuru { get; set; } = string.Empty;

        /// <summary>İzin için girilen ekstra açıklama.</summary>
        public string Aciklama { get; set; } = string.Empty;

        /// <summary>İzin kaydının oluşturulduğu tarih.</summary>
        public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;

        /// <summary>Bu izni sisteme giren yöneticinin kullanıcı ID'si.</summary>
        public string OlusturanUserId { get; set; } = string.Empty;

        /// <summary>Oluşturan yöneticinin navigasyon referansı.</summary>
        public virtual ApplicationUser? OlusturanUser { get; set; }
    }
}
