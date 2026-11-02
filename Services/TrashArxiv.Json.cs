using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>🧾 Arxiv qeydinin JSON-dan çıxarılmış metadatası ✓</summary>
    public sealed class ArxivMetadata
    {
        public string Nov { get; init; } = string.Empty;
        public string Tip { get; init; } = string.Empty;
        public string Basliq { get; init; } = string.Empty;
        public decimal Mebleg { get; init; }
        public DateTime Tarix { get; init; } = DateTime.Now;
        public string Xulase { get; init; } = string.Empty;
        public IReadOnlyList<(string Cedvel, long Id)> Idler { get; init; } =
            Array.Empty<(string, long)>();
    }

    /// <summary>
    /// 🗄️ <b>ARXİV — JSON emalı</b> ✓✓✓ (v6.2.86)
    /// <para>Snapshot JSON-undan başlıq/məbləğ və <b>silinmiş ID-lər</b> çıxarılır ✓</para>
    /// </summary>
    public static partial class TrashArxiv
    {
        /// <summary>«Silinmə» əməliyyatı ✓ (ə = U+0259 ✓ — kodlaşdırma riski YOX ✗)</summary>
        private const string Silinme = "Silinm\u0259";

        /// <summary>
        /// 💾 Snapshot JSON-unu arxivə yazır ✓ (metadata avtomatik çıxarılır ✓)
        /// </summary>
        public static void JsonYaz(string ad, string json)
        {
            var m = MetadatalariCixar(json);

            Yaz(ad, m.Tarix, m.Nov, m.Tip, m.Basliq, m.Mebleg, json, m.Xulase, m.Idler);
        }

        /// <summary>
        /// 🧾 <b>METADATANI ÇIXARIR</b> ✓✓✓ — bir dəfə parse ✓ (köçürmədə də istifadə olunur ✓)
        /// </summary>
        public static ArxivMetadata MetadatalariCixar(string json)
        {
            var nov = Silinme;
            var tip = string.Empty;
            var basliq = string.Empty;
            var mebleg = 0m;
            var tarix = DateTime.Now;
            var xulase = string.Empty;
            List<(string Cedvel, long Id)> idler = new();

            try
            {
                using var sen = JsonDocument.Parse(json);
                var kok = sen.RootElement;

                if (kok.TryGetProperty("Action", out var a) && a.ValueKind == JsonValueKind.String)
                {
                    nov = a.GetString() ?? nov;
                }

                if (kok.TryGetProperty("Kind", out var k) && k.ValueKind == JsonValueKind.String)
                {
                    tip = k.GetString() ?? string.Empty;
                }

                if (kok.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    basliq = t.GetString() ?? string.Empty;
                }

                if (kok.TryGetProperty("Amount", out var m) && m.ValueKind == JsonValueKind.Number &&
                    m.TryGetDecimal(out var meb))
                {
                    mebleg = meb;
                }

                if (kok.TryGetProperty("DeletedAt", out var d) && d.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(d.GetString(), out var dt))
                {
                    tarix = dt;
                }

                xulase = Ozet(kok);
                idler = SilinenIdlerCixar(kok, nov);
            }
            catch
            {
                // 🛡️ JSON xarab olsa — boş metadata ilə yenə yazılır ✓
            }

            return new ArxivMetadata
            {
                Nov = nov,
                Tip = tip,
                Basliq = basliq,
                Mebleg = mebleg,
                Tarix = tarix,
                Xulase = xulase,
                Idler = idler
            };
        }

        /// <summary>🧮 «1 avtomobil · 3 xərc» kimi qısa tərkib mətni ✓</summary>
        private static string Ozet(System.Text.Json.JsonElement kok)
        {
            var hisse = new List<string>();

            Say(kok, "Car", "avtomobil", hisse, obyekt: true);
            Say(kok, "Expenses", "xərc", hisse);
            Say(kok, "Sales", "satış", hisse);
            Say(kok, "Credits", "kredit", hisse);
            Say(kok, "Transactions", "əməliyyat", hisse);
            Say(kok, "Mohletler", "möhlət", hisse);
            Say(kok, "Attachments", "sənəd", hisse);

            return hisse.Count == 0 ? string.Empty : string.Join(" · ", hisse);
        }

        private static void Say(
            System.Text.Json.JsonElement kok, string ad, string soz, List<string> hedef, bool obyekt = false)
        {
            if (!kok.TryGetProperty(ad, out var el))
            {
                return;
            }

            var n = obyekt
                ? (el.ValueKind == JsonValueKind.Object ? 1 : 0)
                : (el.ValueKind == JsonValueKind.Array ? el.GetArrayLength() : 0);

            if (n > 0)
            {
                hedef.Add($"{n} {soz}");
            }
        }

        /// <summary>
        /// 🧾 Snapshot-dan <b>silinmiş ID-lər</b> ✓ (yalnız <c>Action = "Silinmə"</c> ✓)
        /// </summary>
        public static List<(string Cedvel, long Id)> SilinenIdleriCixar(string json)
        {
            var netice = new List<(string, long)>();

            try
            {
                using var sen = JsonDocument.Parse(json);
                var kok = sen.RootElement;

                if (!kok.TryGetProperty("Action", out var a) || a.ValueKind != JsonValueKind.String ||
                    !string.Equals(a.GetString(), Silinme, StringComparison.Ordinal))
                {
                    return netice;      // ✏️ «Dəyişiklik» → silinmiş ID YOX ✗
                }

                if (kok.TryGetProperty("Car", out var car))
                {
                    IdElave(netice, "Avtomobiller", car);
                }

                Liste(kok, "Expenses", "Xercler", netice);
                Liste(kok, "Sales", "Satislar", netice);
                Liste(kok, "Credits", "Kreditler", netice);
                Liste(kok, "Transactions", "KreditEmeliyyatlari", netice);
                Liste(kok, "Attachments", "Senedler", netice);
            }
            catch
            {
                // 🛡️
            }

            return netice;
        }

        /// <summary>🧾 Silinmiş ID-lər — artıq parse edilmiş elementdən ✓ (köçürmədə sürətli ✓)</summary>
        private static List<(string Cedvel, long Id)> SilinenIdlerCixar(JsonElement kok, string nov)
        {
            var netice = new List<(string, long)>();

            if (!string.Equals(nov, Silinme, StringComparison.Ordinal))
            {
                return netice;      // ✏️ «Dəyişiklik» → silinmiş ID YOX ✗
            }

            try
            {
                if (kok.TryGetProperty("Car", out var car))
                {
                    IdElave(netice, "Avtomobiller", car);
                }

                Liste(kok, "Expenses", "Xercler", netice);
                Liste(kok, "Sales", "Satislar", netice);
                Liste(kok, "Credits", "Kreditler", netice);
                Liste(kok, "Transactions", "KreditEmeliyyatlari", netice);
                Liste(kok, "Attachments", "Senedler", netice);
            }
            catch
            {
                // 🛡️
            }

            return netice;
        }

        private static void Liste(JsonElement kok, string xususiyyet, string cedvel, List<(string, long)> hedef)
        {
            if (!kok.TryGetProperty(xususiyyet, out var massiv) || massiv.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var oge in massiv.EnumerateArray())
            {
                IdElave(hedef, cedvel, oge);
            }
        }

        private static void IdElave(List<(string, long)> hedef, string cedvel, JsonElement oge)
        {
            if (oge.ValueKind != JsonValueKind.Object ||
                !oge.TryGetProperty("Id", out var id) ||
                id.ValueKind != JsonValueKind.Number ||
                !id.TryGetInt64(out var deyer))
            {
                return;
            }

            hedef.Add((cedvel, deyer));
        }
    }
}
