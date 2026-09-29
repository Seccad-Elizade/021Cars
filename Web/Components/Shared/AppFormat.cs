using System.Globalization;

namespace EnterpriseAeroStudio.Web.Components.Shared;

/// <summary>
/// Veb interfeysdə vahid mətn formatlaşdırması (məbləğ, tarix, faiz).
/// Masaüstü tətbiqin gg.aa.iiii tarix formatı ilə uyğundur.
/// </summary>
public static class AppFormat
{
    /// <summary>Məbləğ: "12 500.00 ₼".</summary>
    public static string Money(decimal value)
        => value.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ") + " ₼";

    /// <summary>İşarəli məbləğ (mənfəət/zərər üçün): "+1 200.00 ₼" / "−320.00 ₼".</summary>
    public static string MoneySigned(decimal value)
        => (value > 0 ? "+" : value < 0 ? "−" : string.Empty)
           + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ")
           + " ₼";

    /// <summary>Qısa məbləğ: 1 250 000 → "1.25M ₼", 12 500 → "12.5K ₼".</summary>
    public static string MoneyShort(decimal value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000m)
        {
            return (value / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M ₼";
        }

        if (abs >= 1_000m)
        {
            return (value / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "K ₼";
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture) + " ₼";
    }

    /// <summary>Faiz: "% 12.5".</summary>
    public static string Percent(decimal value)
        => "% " + value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Tarix: "10.09.2026" (boşdursa "—").</summary>
    public static string Date(DateTime? value)
        => value?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "—";

    public static string Date(DateTime value)
        => value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Tarix + saat: "10.09.2026 14:35".</summary>
    public static string DateTimeText(DateTime value)
        => value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Ayın qısa adı (1 → "Yan").</summary>
    public static string MonthShort(int month)
        => month is >= 1 and <= 12
            ? new[] { "Yan", "Fev", "Mar", "Apr", "May", "İyn", "İyl", "Avq", "Sen", "Okt", "Noy", "Dek" }[month - 1]
            : "—";

    /// <summary>Boş mətnin yerinə tire.</summary>
    public static string Text(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value;

    /// <summary>HTML atributlarında təhlükəsiz mətn (status sinfi üçün).</summary>
    public static string Slug(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "none"
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>Status üçün CSS sinfi.</summary>
    public static string StatusClass(string? status) => status switch
    {
        "Stokda"   => "st stok",
        "Satışda"  => "st satis",
        "Kreditdə" => "st kredit",
        "Satıldı"  => "st satildi",
        "Barter edildi" => "st kredit",
        "Təmirdə"  => "st temir",
        "Aktiv"    => "st aktiv",
        "Bağlı"    => "st bagli",
        "Gecikmiş" => "st gecikmis",
        "Gəlir"    => "st gelir",
        "Xərc"     => "st xerc",
        "Möhlət"   => "st mohlet",
        "Gecikmə"  => "st gecikme",
        _          => "st none"
    };
}
