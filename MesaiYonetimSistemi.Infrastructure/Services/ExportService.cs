using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.DTOs;
using MesaiYonetimSistemi.Application.Interfaces;

namespace MesaiYonetimSistemi.Infrastructure.Services
{
    public class ExportService : IExportService
    {
        static ExportService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public ExportService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<byte[]> ExportOvertimesToExcelAsync(IEnumerable<OvertimeDto> overtimes)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Mesai Raporu");

            // Headers
            string[] headers = { "Mesai ID", "Tarih", "Başlangıç", "Bitiş", "Departman", "Şube", "Açıklama", "Kontenjan", "Resmi Tatil", "Durum", "Atanan Personel" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(11, 25, 44)); // Dark Navy
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            int row = 2;
            foreach (var item in overtimes)
            {
                worksheet.Cells[row, 1].Value = item.Id;
                worksheet.Cells[row, 2].Value = item.Tarih.ToString("dd.MM.yyyy");
                worksheet.Cells[row, 3].Value = item.BaslangicSaati.ToString(@"hh\:mm");
                worksheet.Cells[row, 4].Value = item.BitisSaati.ToString(@"hh\:mm");
                worksheet.Cells[row, 5].Value = item.Departman ?? "-";
                worksheet.Cells[row, 6].Value = item.Sube ?? "-";
                worksheet.Cells[row, 7].Value = item.Aciklama ?? "-";
                worksheet.Cells[row, 8].Value = item.Kontenjan;
                worksheet.Cells[row, 9].Value = item.ResmiTatilMi ? "Evet" : "Hayır";
                worksheet.Cells[row, 10].Value = item.Durum ?? "-";
                worksheet.Cells[row, 11].Value = item.AtananUserAdSoyad ?? "Henüz Atanmadı";
                row++;
            }

            if (row > 2)
            {
                worksheet.Cells[1, 1, row - 1, headers.Length].AutoFitColumns();
            }
            return await package.GetAsByteArrayAsync();
        }

        public async Task<byte[]> ExportPersonnelToExcelAsync(IEnumerable<UserDto> personnelList)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Personel Listesi");

            string[] headers = { "Sicil No", "Ad Soyad", "TC Kimlik No", "E-posta", "Telefon", "Departman", "Görev", "Şube", "İşe Giriş Tarihi", "Sıra Pozisyonu", "Kabul Edilen", "Reddedilen", "Durum" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0, 168, 150)); // Teal
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            int row = 2;
            foreach (var item in personnelList)
            {
                worksheet.Cells[row, 1].Value = item.SicilNo ?? "-";
                worksheet.Cells[row, 2].Value = item.AdSoyad ?? "-";
                worksheet.Cells[row, 3].Value = item.TcKimlikNo ?? "-";
                worksheet.Cells[row, 4].Value = item.Email ?? "-";
                worksheet.Cells[row, 5].Value = item.PhoneNumber ?? "-";
                worksheet.Cells[row, 6].Value = item.Departman ?? "-";
                worksheet.Cells[row, 7].Value = item.Gorev ?? "-";
                worksheet.Cells[row, 8].Value = item.Sube ?? "-";
                worksheet.Cells[row, 9].Value = item.IseGirisTarihi.ToString("dd.MM.yyyy");
                worksheet.Cells[row, 10].Value = item.QueuePosition;
                worksheet.Cells[row, 11].Value = item.TotalAccepted;
                worksheet.Cells[row, 12].Value = item.TotalRejected;
                worksheet.Cells[row, 13].Value = item.AktifMi ? "Aktif" : "Pasif";
                row++;
            }

            if (row > 2)
            {
                worksheet.Cells[1, 1, row - 1, headers.Length].AutoFitColumns();
            }
            return await package.GetAsByteArrayAsync();
        }

        public async Task<byte[]> ExportOvertimeReportPdfAsync(IEnumerable<OvertimeDto> overtimes)
        {
            var totalCount = overtimes.Count();
            var assignedCount = overtimes.Count(o => !string.IsNullOrEmpty(o.AtananUserAdSoyad) || o.Durum == "Tamamlandi");
            var pendingCount = totalCount - assignedCount;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang='tr'><head><meta charset='utf-8'><title>Hafta Sonu Mesai Raporu</title>");
            sb.AppendLine("<link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css' rel='stylesheet' />");
            sb.AppendLine("<link href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.2/css/all.min.css' rel='stylesheet' />");
            sb.AppendLine("<style>");
            sb.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');");
            sb.AppendLine("body { font-family: 'Inter', system-ui, sans-serif; background-color: #f8fafc; color: #0f172a; padding: 30px; }");
            sb.AppendLine(".report-container { max-width: 1100px; margin: 0 auto; background: #ffffff; padding: 40px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); border: 1px solid #e2e8f0; }");
            sb.AppendLine(".brand-header { border-bottom: 3px solid #00a896; padding-bottom: 20px; margin-bottom: 30px; }");
            sb.AppendLine(".stat-badge { background: #f1f5f9; padding: 12px 20px; border-radius: 8px; border: 1px solid #cbd5e1; }");
            sb.AppendLine(".table-custom th { background-color: #0b192c !important; color: #ffffff !important; padding: 12px; font-weight: 600; text-transform: uppercase; font-size: 0.78rem; letter-spacing: 0.5px; }");
            sb.AppendLine(".table-custom td { padding: 12px; vertical-align: middle; border-bottom: 1px solid #e2e8f0; font-size: 0.88rem; }");
            sb.AppendLine("@media print { .no-print { display: none !important; } body { background-color: #ffffff; padding: 0; } .report-container { box-shadow: none; border: none; padding: 0; } }");
            sb.AppendLine("</style></head><body>");

            // Print Control Bar
            sb.AppendLine("<div class='no-print d-flex justify-content-between align-items-center mb-4 p-3 bg-dark text-white rounded-3 shadow-sm'>");
            sb.AppendLine("<div><i class='fa-solid fa-file-invoice text-info me-2 fs-5'></i><strong>Resmi Mesai Raporu Önizleme</strong></div>");
            sb.AppendLine("<div><button onclick='window.print()' class='btn btn-success me-2'><i class='fa-solid fa-print me-1'></i> Yazdır / PDF Kaydet</button><button onclick='window.close()' class='btn btn-outline-light'><i class='fa-solid fa-xmark me-1'></i> Kapat</button></div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='report-container'>");
            // Header
            sb.AppendLine("<div class='brand-header d-flex justify-content-between align-items-center'>");
            sb.AppendLine("<div>");
            sb.AppendLine("<h3 class='fw-bold text-dark m-0'><i class='fa-solid fa-atom text-cyan me-2'></i>Kocaeli Bilim Merkezi</h3>");
            sb.AppendLine("<p class='text-muted small m-0'>Hafta Sonu & Nöbetçi Mesai Yönetim Sistemi Resmi Rapor Çıktısı</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='text-end'>");
            sb.AppendLine($"<small class='text-muted d-block'>Rapor Tarihi: <strong>{DateTime.Now:dd.MM.yyyy HH:mm}</strong></small>");
            sb.AppendLine("<span class='badge bg-primary px-3 py-2'>Resmi Belge</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            // Metrics Summary
            sb.AppendLine("<div class='row g-3 mb-4'>");
            sb.AppendLine($"<div class='col-md-4'><div class='stat-badge text-center'><span class='text-muted small d-block'>Toplam Kayıtlı Mesai</span><h4 class='fw-bold text-dark m-0'>{totalCount}</h4></div></div>");
            sb.AppendLine($"<div class='col-md-4'><div class='stat-badge text-center'><span class='text-muted small d-block'>Atanan / Tamamlanan</span><h4 class='fw-bold text-success m-0'>{assignedCount}</h4></div></div>");
            sb.AppendLine($"<div class='col-md-4'><div class='stat-badge text-center'><span class='text-muted small d-block'>Atama Bekleyen</span><h4 class='fw-bold text-warning m-0'>{pendingCount}</h4></div></div>");
            sb.AppendLine("</div>");

            // Table
            sb.AppendLine("<div class='table-responsive'>");
            sb.AppendLine("<table class='table table-custom mb-0'>");
            sb.AppendLine("<thead><tr><th>ID</th><th>Tarih</th><th>Saat</th><th>Departman / Şube</th><th>Açıklama</th><th>Atanan Personel</th><th>Durum</th></tr></thead><tbody>");

            if (!overtimes.Any())
            {
                sb.AppendLine("<tr><td colspan='7' class='text-center text-muted py-4'>Raporlanacak mesai kaydı bulunamadı.</td></tr>");
            }

            foreach (var item in overtimes)
            {
                var isAssigned = !string.IsNullOrEmpty(item.AtananUserAdSoyad);
                var statusBadge = item.Durum == "Tamamlandi" || isAssigned 
                    ? "<span class='badge bg-success-subtle text-success border border-success px-2 py-1'>Atandı</span>" 
                    : (item.Durum == "IptalEdildi" ? "<span class='badge bg-danger-subtle text-danger border border-danger px-2 py-1'>İptal</span>" : "<span class='badge bg-warning-subtle text-warning border border-warning px-2 py-1'>Planlandı</span>");

                var personDisplay = isAssigned ? $"<strong class='text-dark'>{item.AtananUserAdSoyad}</strong>" : "<span class='text-muted small'>Atama Bekleniyor</span>";

                sb.AppendLine($"<tr><td><strong>#{item.Id}</strong></td><td><strong>{item.Tarih:dd.MM.yyyy}</strong></td><td><small>{item.BaslangicSaati:hh\\:mm} - {item.BitisSaati:hh\\:mm}</small></td><td>{item.Departman}<br/><small class='text-muted'>{item.Sube}</small></td><td>{item.Aciklama}</td><td>{personDisplay}</td><td>{statusBadge}</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");

            // Signatures
            sb.AppendLine("<div class='row mt-5 pt-4 text-center' style='border-top: 1px dashed #cbd5e1;'>");
            sb.AppendLine("<div class='col-6'><p class='fw-bold mb-5'>Düzenleyen / İnsan Kaynakları</p><p class='text-muted small'>(İmza / Mühür)</p></div>");
            sb.AppendLine("<div class='col-6'><p class='fw-bold mb-5'>Birim Sorumlusu / Yönetici Onayı</p><p class='text-muted small'>(İmza / Mühür)</p></div>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div></body></html>");

            return await Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public async Task<List<ExcelUserImportDto>> ImportPersonnelFromExcelAsync(Stream fileStream)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var list = new List<ExcelUserImportDto>();
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null || worksheet.Dimension == null) return list;

            int rowCount = worksheet.Dimension.Rows;
            for (int row = 2; row <= rowCount; row++)
            {
                var adSoyad = GetCellValue(worksheet, row, 1);
                var email = GetCellValue(worksheet, row, 4);

                if (string.IsNullOrEmpty(adSoyad) && string.IsNullOrEmpty(email)) continue;

                var dto = new ExcelUserImportDto
                {
                    AdSoyad = adSoyad,
                    SicilNo = GetCellValue(worksheet, row, 2),
                    TcKimlikNo = GetCellValue(worksheet, row, 3),
                    Email = email,
                    Telefon = GetCellValue(worksheet, row, 5),
                    Departman = GetCellValue(worksheet, row, 6),
                    Gorev = GetCellValue(worksheet, row, 7),
                    Sube = GetCellValue(worksheet, row, 8)
                };

                list.Add(dto);
            }

            return await Task.FromResult(list);
        }

        private static string GetCellValue(ExcelWorksheet worksheet, int row, int col)
        {
            var val = worksheet.Cells[row, col].Value;
            return val != null ? val.ToString()!.Trim() : string.Empty;
        }
    }
}
