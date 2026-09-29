using System.Globalization;
using System.IO;
using System.Text;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// CSV (Excel uyğun, UTF-8 BOM, nöqtəli vergüllə) ixrac xidməti.
    /// Bütün I/O işləri <see cref="Task.Run"/> üzərində icra olunur ki, UI donmasın.
    /// </summary>
    public sealed class ExportService : IExportService
    {
        private static readonly CultureInfo Az = CultureInfo.GetCultureInfo("az-AZ");

        public Task ExportCarsAsync(IEnumerable<CarItem> cars, string filePath, CancellationToken cancellationToken = default)
        {
            var rows = new List<string>
            {
                "ID;Marka / Model;Dövlət Nömrəsi;VIN Kod;İl;Yürüş (km);Yanacaq;Alış Tarixi;Alış Saatı;Alış Qiyməti (AZN);Xərclər (AZN);Maya Dəyəri (AZN);Status"
            };

            foreach (var car in cars)
            {
                // 🚀 `ButunXercler` — xərc siyahısı yüklənməyibsə SQL cəmini verir ✓✓✓
                var expenses = car.ButunXercler;
                var cost = car.AlisQiymeti + expenses;
                rows.Add(string.Join(';',
                    car.Id,
                    Escape(car.Marka),
                    Escape(car.QeydiyyatNisani),
                    Escape(car.Vin),
                    car.Il,
                    car.Yurus,
                    Escape(car.Yanacaq),
                    car.AlisTarixi?.ToString("dd.MM.yyyy", Az) ?? string.Empty,
                    Escape(car.AlisSaati),
                    car.AlisQiymeti.ToString("0.00", Az),
                    expenses.ToString("0.00", Az),
                    cost.ToString("0.00", Az),
                    Escape(car.Status)));
            }

            return WriteAsync(filePath, rows, cancellationToken);
        }

        public Task ExportExpensesAsync(IEnumerable<ExpenseItem> expenses, string filePath, CancellationToken cancellationToken = default)
        {
            var rows = new List<string>
            {
                "ID;Tarix;Təyinat;Qrup;Kateqoriya;Avtomobil;Məbləğ (AZN);Ödəniş Üsulu;Qeyd"
            };

            foreach (var expense in expenses)
            {
                rows.Add(string.Join(';',
                    expense.Id,
                    expense.Tarix.ToString("dd.MM.yyyy", Az),
                    Escape(expense.Teyinat),
                    Escape(expense.Qrup),
                    Escape(expense.Kategoriya),
                    Escape(expense.CarInfo),
                    expense.Mebleg.ToString("0.00", Az),
                    Escape(expense.OdenisUsulu),
                    Escape(expense.Qeyd)));
            }

            return WriteAsync(filePath, rows, cancellationToken);
        }

        public Task ExportPurchasesAsync(IEnumerable<CarItem> cars, string filePath, CancellationToken cancellationToken = default)
        {
            var rows = new List<string>
            {
                "ID;Sıra №;Alış Tarixi;Alış Saatı;Marka / Model;Dövlət Nömrəsi;VIN Kod;İl;" +
                "Alış Üsulu;Barter Təsviri;Alış Qiyməti (AZN);Xərclər (AZN);Maya Dəyəri (AZN);Status"
            };

            foreach (var car in cars)
            {
                rows.Add(string.Join(';',
                    car.Id,
                    car.SiraNomresi,
                    car.AlisTarixi?.ToString("dd.MM.yyyy", Az) ?? string.Empty,
                    Escape(car.AlisSaati),
                    Escape(car.Marka),
                    Escape(car.QeydiyyatNisani),
                    Escape(car.Vin),
                    car.Il,
                    Escape(car.AlisUsulu),
                    Escape(car.BarterTesviri),
                    car.AlisQiymeti.ToString("0.00", Az),
                    car.Xercler.ToString("0.00", Az),
                    car.MayaDeyeri.ToString("0.00", Az),
                    Escape(car.Status)));
            }

            return WriteAsync(filePath, rows, cancellationToken);
        }

        public Task ExportPartnersAsync(
            IEnumerable<PartnerCardRow> cards,
            IEnumerable<PartnerLedgerRow> ledger,
            IEnumerable<PartnerPayment> payments,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var rows = new List<string>
            {
                "TƏRƏFDAŞ BÖLGÜSÜ HESABATI",
                string.Empty,
                "1 · TƏRƏFDAŞ KARTLARI (qazanılmış / verilmiş / qalıq)",
                "Tərəfdaş;Faiz;Satışdan;Barter/Kreditdən;Əlavə Gəlirdən;" +
                "Qazanılmış (AZN);Verilmiş (AZN);Qalıq (AZN);Bölgü Sayı;Dövr"
            };

            foreach (var card in cards)
            {
                rows.Add(string.Join(';',
                    Escape(card.Terefdas),
                    Escape(card.FaizMetni),
                    card.SatisQazanc.ToString("0.00", Az),
                    card.KreditQazanc.ToString("0.00", Az),
                    card.GelirQazanc.ToString("0.00", Az),
                    card.Qazanilmis.ToString("0.00", Az),
                    card.Verilmis.ToString("0.00", Az),
                    card.Qaliq.ToString("0.00", Az),
                    card.BolguSayi,
                    Escape(card.DonemMetni)));
            }

            rows.Add(string.Empty);
            rows.Add($"CƏMİ;{(cards.Sum(c => c.Qazanilmis)).ToString("0.00", Az)};" +
                     $"{cards.Sum(c => c.Verilmis).ToString("0.00", Az)};" +
                     $"{cards.Sum(c => c.Qaliq).ToString("0.00", Az)}");

            rows.Add(string.Empty);
            rows.Add("2 · BÖLGÜ JURNALI (hansı maşından, hansı tarixdə, kimə nə qədər)");
            rows.Add("Tarix;Dövr;Mənbə;Avtomobil;Müqavilə / Müştəri;Tərəfdaş;Faiz;Baza (AZN);Pay (AZN)");

            foreach (var row in ledger)
            {
                rows.Add(string.Join(';',
                    row.TarixMetni,
                    Escape(row.AyMetni),
                    Escape(row.Menbe),
                    Escape(row.Avtomobil),
                    Escape(row.SenedMetni),
                    Escape(row.Terefdas),
                    Escape(row.FaizMetni),
                    row.Baza.ToString("0.00", Az),
                    row.Mebleg.ToString("0.00", Az)));
            }

            rows.Add(string.Empty);
            rows.Add("3 · TƏRƏFDAŞLARA VERİLƏN PULLAR");
            rows.Add("Tarix;Dövr;Tərəfdaş;Məbləğ (AZN);Ödəniş Üsulu;Qeyd");

            foreach (var payment in payments)
            {
                rows.Add(string.Join(';',
                    payment.Tarix.ToString("dd.MM.yyyy", Az),
                    Escape(payment.AyMetni),
                    Escape(payment.Terefdas),
                    payment.Mebleg.ToString("0.00", Az),
                    Escape(payment.OdenisUsulu),
                    Escape(payment.Qeyd)));
            }

            return WriteAsync(filePath, rows, cancellationToken);
        }

        private static Task WriteAsync(string filePath, IEnumerable<string> rows, CancellationToken cancellationToken)
            => Task.Run(async () =>
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                foreach (var row in rows)
                {
                    builder.AppendLine(row);
                }

                // UTF-8 + BOM: Excel Azərbaycan hərflərini düzgün göstərsin.
                await File.WriteAllTextAsync(filePath, builder.ToString(), new UTF8Encoding(true), cancellationToken);
            }, cancellationToken);

        private static string Escape(string? value)
        {
            value ??= string.Empty;
            if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
