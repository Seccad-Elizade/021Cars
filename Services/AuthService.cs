using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>👤 İstifadəçi rolları ✓✓✓</summary>
    public enum IstifadeciRolu
    {
        /// <summary>🔑 TAM GİRİŞ — hər şey görünür ✓</summary>
        Admin = 0,

        /// <summary>⚙️ Asif — Veb Sayt ✗ · «Tənzimləmələr» ✓ (Sahildən fərqi ✓)</summary>
        Asif = 1,

        /// <summary>🚗 Sahil — Veb Sayt ✗ · «Tənzimləmələr» ✗</summary>
        Sahil = 2
    }

    /// <summary>
    /// 👤 İstifadəçi ✓ — adı · KODU (şifrəsi) · rolu DƏYİŞİLƏ BİLƏR ✓✓✓
    /// </summary>
    public sealed class Istifadeci
    {
        public string Ad { get; set; } = string.Empty;

        public string IstifadeciAdi { get; set; } = string.Empty;

        /// <summary>🔑 Kod (şifrə) ✓ — idarəçilər görür və dəyişir ✓</summary>
        public string Sifre { get; set; } = string.Empty;

        public IstifadeciRolu Rol { get; set; }

        /// <summary>Rol adı ✓ — yüksək təbəqə adları ✓</summary>
        [JsonIgnore]
        public string RolMetni => Rol switch
        {
            IstifadeciRolu.Admin => "👑 BAŞ ADMIN (Tam Giriş)",
            IstifadeciRolu.Asif => "🛡️ BAŞ İNZİBATÇI (Yüksək Təbəqə)",
            _ => "🚗 İstifadəçi"
        };

        /// <summary>🌐 Veb Sayt YALNIZ Admin ✓ (Sahil &amp; Asif ✗)</summary>
        [JsonIgnore] public bool VebSaytGoruner => Rol == IstifadeciRolu.Admin;

        /// <summary>⚙️ Tənzimləmələr — Asif + Admin ✓ (Sahil ✗)</summary>
        [JsonIgnore] public bool TenzimlemelerGoruner => Rol != IstifadeciRolu.Sahil;

        /// <summary>🔑 Tam giriş?</summary>
        [JsonIgnore] public bool TamGiris => Rol == IstifadeciRolu.Admin;

        /// <summary>
        /// 🗑️ <b>SİLMƏ İCAZƏSİ — BÜTÜN İSTİFADƏÇİLƏRDƏ VAR</b> ✓✓✓
        /// (Sahil · Asif · Admin — hər kəs silə bilər ✓)
        /// </summary>
        [JsonIgnore] public bool SilmeIcazesi => true;

        /// <summary>👑 İdarəçi (istifadəçiləri idarə edir ✓)</summary>
        [JsonIgnore] public bool IdareciMi => Rol != IstifadeciRolu.Sahil;

        /// <summary>📋 Cədvəl üçün «Ad (Kod)» ✓</summary>
        [JsonIgnore] public string AdKod => $"{Ad}  ({IstifadeciAdi} / {Sifre})";
    }

    /// <summary>
    /// 🔐 <b>GİRİŞ + İSTİFADƏÇİ İDARƏETMƏSİ</b> ✓✓✓
    /// <para>
    /// Standart 3 istifadəçi ✓: <b>Sahil</b>/<c>Sahil021021</c> ·
    /// <b>Asif</b>/<c>AsifAdmin</c> · <b>Seccad</b>/<c>Admin</c> ✓
    /// </para>
    /// <para>
    /// ✅ <b>Asif və Admin</b> istifadəçilərin adını, KODUNU (şifrəsini) və rolunu
    /// görür ✓, dəyişir ✓, silir ✓, <b>YENİ istifadəçi yaradır</b> ✓ —
    /// bütün dəyişikliklər JSON faylında SAXLANILIR ✓ (yenidən açılışda qalır ✓).
    /// </para>
    /// </summary>
    public static class AuthService
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        /// <summary>💾 İstifadəçilərin saxlanıldığı fayl ✓</summary>
        public static string Fayl { get; } = Path.Combine(
            Cas0201.Kok.Qovluq,
            "EnterpriseAeroStudio", "istifadeciler.json");

        /// <summary>👥 Bütün istifadəçilər ✓ (idarəçilər dəyişə bilər ✓)</summary>
        public static List<Istifadeci> Istifadeciler { get; private set; } = new();

        /// <summary>👤 Hazırda daxil olmuş istifadəçi ✓</summary>
        public static Istifadeci? Cari { get; private set; }

        public static bool GirisEdilib => Cari is not null;

        static AuthService() => Yukle();

        /// <summary>📥 Fayldan yükləyir ✓ (yoxdursa 3 standart istifadəçi ✓)</summary>
        public static void Yukle()
        {
            try
            {
                if (File.Exists(Fayl))
                {
                    var metn = File.ReadAllText(Fayl);
                    var siyahi = JsonSerializer.Deserialize<List<Istifadeci>>(metn, Json);

                    if (siyahi is { Count: > 0 })
                    {
                        Istifadeciler = siyahi;
                        return;
                    }
                }
            }
            catch
            {
                // ✗ fayl zədəlidirsə standart siyahıya düşürük ✓
            }

            Istifadeciler = Standart();
            YaddaSaxla();
        }

        /// <summary>📤 Dəyişiklikləri fayla yazır ✓✓✓</summary>
        public static void YaddaSaxla()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Fayl) ?? ".");
                File.WriteAllText(Fayl, JsonSerializer.Serialize(Istifadeciler, Json));
            }
            catch
            {
                // ✗ yazmaq alınmadı — yaddaşda qalır ✓
            }
        }

        /// <summary>🏭 Standart 3 istifadəçi ✓</summary>
        public static List<Istifadeci> Standart() => new()
        {
            new() { Ad = "Sahil",  IstifadeciAdi = "Sahil",  Sifre = "Sahil021021", Rol = IstifadeciRolu.Sahil },
            new() { Ad = "Asif",   IstifadeciAdi = "Asif",   Sifre = "AsifAdmin",   Rol = IstifadeciRolu.Asif },
            new() { Ad = "Seccad", IstifadeciAdi = "Seccad", Sifre = "Admin",       Rol = IstifadeciRolu.Admin }
        };

        /// <summary>🔐 Giriş yoxlanılır ✓</summary>
        public static Istifadeci? GirisEle(string? istifadeciAdi, string? sifre)
        {
            var ad = (istifadeciAdi ?? string.Empty).Trim();

            Cari = Istifadeciler.FirstOrDefault(i =>
                string.Equals(i.IstifadeciAdi, ad, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.Sifre, sifre ?? string.Empty, StringComparison.Ordinal));

            return Cari;
        }

        /// <summary>🚪 Çıxış ✓</summary>
        public static void CixisEt() => Cari = null;

        /// <summary>🔎 Bu istifadəçi adı artıq varmı? ✓</summary>
        public static bool AdMovcuddur(string? ad, Istifadeci? istisna = null)
        {
            var a = (ad ?? string.Empty).Trim();

            return Istifadeciler.Any(i => !ReferenceEquals(i, istisna)
                && string.Equals(i.IstifadeciAdi, a, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>➕ YENİ istifadəçi yaradır ✓✓✓</summary>
        public static string ElaveEt(string ad, string istifadeciAdi, string sifre, IstifadeciRolu rol)
        {
            ad = (ad ?? string.Empty).Trim();
            istifadeciAdi = (istifadeciAdi ?? string.Empty).Trim();

            if (istifadeciAdi.Length == 0) return "⚠️ İstifadəçi adı boşdur ✗";
            if (string.IsNullOrWhiteSpace(sifre)) return "⚠️ Kod (şifrə) boşdur ✗";
            if (AdMovcuddur(istifadeciAdi)) return "⚠️ Bu istifadəçi adı artıq mövcuddur ✗";

            Istifadeciler.Add(new Istifadeci
            {
                Ad = ad.Length > 0 ? ad : istifadeciAdi,
                IstifadeciAdi = istifadeciAdi,
                Sifre = sifre,
                Rol = rol
            });

            YaddaSaxla();

            return $"✅ Yeni istifadəçi yaradıldı: {istifadeciAdi} ✓";
        }

        /// <summary>➖ İstifadəçini silir ✓ (son Admin silinmir ✗)</summary>
        public static string Sil(Istifadeci istifadeci)
        {
            if (istifadeci.Rol == IstifadeciRolu.Admin
                && Istifadeciler.Count(i => i.Rol == IstifadeciRolu.Admin) <= 1)
            {
                return "⚠️ SON ADMIN silinə bilməz ✗";
            }

            Istifadeciler.Remove(istifadeci);
            YaddaSaxla();

            return "✅ İstifadəçi silindi ✓";
        }

        /// <summary>✏️ Dəyişiklikləri (ad · kod · rol ✓) yadda saxlayır ✓✓✓</summary>
        public static string YaddaSaxlaVeQeyd()
        {
            YaddaSaxla();

            return "✅ İstifadəçi məlumatları yadda saxlanıldı ✓";
        }
    }
}
