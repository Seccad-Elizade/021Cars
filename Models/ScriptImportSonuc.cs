namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 📜 <b>SKRİPT İDXALI NƏTİCƏSİ</b> ✓✓✓ — hesabat, cəmlər və sətir-sətir gündəlik.
    /// <para>
    /// «🔎 Yoxla» rejimində heç nə YAZILMIR ✗ — yalnız nə olacağı göstərilir ✓.
    /// «📥 İdxal et» rejimində bütün avtomobil və xərclər <b>əsl</b> kimi yazılır ✓✓✓
    /// </para>
    /// </summary>
    public sealed class ScriptImportSonuc
    {
        /// <summary>Yalnız yoxlama (yazmadan ✓) rejimidir?</summary>
        public bool YalnizYoxlama { get; set; }

        /// <summary>Ümumi uğur ✓ (JSON oxundu və ən azı bir avtomobil işləndi ✓).</summary>
        public bool Ugurlu { get; set; } = true;

        /// <summary>Kritik xəta mətni (JSON pozulubsa ✓).</summary>
        public string? Xeta { get; set; }

        /// <summary>Skriptdəki avtomobil sayı ✓.</summary>
        public int SkriptMasinSayi { get; set; }

        /// <summary>Bazaya yazılan avtomobil sayı ✓.</summary>
        public int ElaveOlunanMasin { get; set; }

        /// <summary>Artıq mövcud olduğu üçün keçilən avtomobil sayı ✓.</summary>
        public int KecirilenMasin { get; set; }

        /// <summary>Xətası olan (yazıla bilməyən) avtomobil sayı ✓.</summary>
        public int XetaliMasin { get; set; }

        /// <summary>Əlavə olunan xərc sətirlərinin sayı ✓.</summary>
        public int ElaveOlunanXerc { get; set; }

        /// <summary>Yeni yaradılmış kateqoriya sayı ✓.</summary>
        public int YeniKateqoriya { get; set; }

        /// <summary>Yeni yaradılmış qrup sayı ✓.</summary>
        public int YeniQrup { get; set; }

        /// <summary>Uyğunlaşdırılmış (mövcud) kateqoriya sayı ✓.</summary>
        public int UygunlasdirilanKateqoriya { get; set; }

        /// <summary>Bütün alış qiymətlərinin cəmi ✓.</summary>
        public decimal UmumiAlis { get; set; }

        /// <summary>Bütün əlavə xərclərin cəmi (alış xaric ✓).</summary>
        public decimal UmumiXerc { get; set; }

        /// <summary>ÜMUMİ MAYA DƏYƏRİ = alış + xərclər ✓✓✓</summary>
        public decimal UmumiMaya => UmumiAlis + UmumiXerc;

        /// <summary>Sətir-sətir gündəlik (istifadəçiyə göstərilir ✓).</summary>
        public List<string> Setirler { get; } = new();

        /// <summary>Avtomobil üzrə nəticələr ✓.</summary>
        public List<ScriptMasinNeticesi> Masinlar { get; } = new();

        /// <summary>Gündəliyə sətir əlavə edir ✓.</summary>
        public void Yaz(string setir) => Setirler.Add(setir);

        /// <summary>Kritik xəta ilə bitir ✓.</summary>
        public void Ugursuz(string xeta)
        {
            Ugurlu = false;
            Xeta = xeta;
            Yaz(xeta);
        }
    }
}
