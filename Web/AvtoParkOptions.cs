namespace EnterpriseAeroStudio.Web;

/// <summary>
/// appsettings.json → "AvtoPark" bölməsinin güclü tipli təsviri.
/// Səhifələrə <see cref="Microsoft.Extensions.Options.IOptions{AvtoParkOptions}"/>
/// vasitəsilə ötürülür.
/// </summary>
public sealed class AvtoParkOptions
{
    /// <summary>Brauzer başlığı / başlıq panelində göstərilən ad.</summary>
    public string SiteTitle { get; set; } = "Avtomobil Parkı";

    /// <summary>Veb interfeysdən yazma (əlavə/redaktə/silmə) icazəlidirmi?</summary>
    public bool AllowWrite { get; set; } = true;

    /// <summary>Giriş şifrəsi tələb olunurmu?</summary>
    public bool RequireLogin { get; set; } = true;
}
