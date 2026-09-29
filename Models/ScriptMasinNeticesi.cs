namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 📜 Bir avtomobilin skript idxalı nəticəsi (hesabat sətri ✓).
    /// </summary>
    public sealed class ScriptMasinNeticesi
    {
        /// <summary>Skriptdəki sıra (1, 2, 3 … ✓).</summary>
        public int Sira { get; set; }

        /// <summary>Marka / model ✓.</summary>
        public string Marka { get; set; } = string.Empty;

        /// <summary>Dövlət nömrəsi ✓.</summary>
        public string Nomre { get; set; } = string.Empty;

        /// <summary>Buraxılış ili ✓.</summary>
        public int Il { get; set; }

        /// <summary>Alış tarixi ✓.</summary>
        public DateTime? AlisTarixi { get; set; }

        /// <summary>Alış qiyməti (AZN ✓).</summary>
        public decimal AlisQiymeti { get; set; }

        /// <summary>Avtomobilə əlavə olunan xərc sətirlərinin sayı ✓.</summary>
        public int XercSayi { get; set; }

        /// <summary>Alışdan BAŞQA bütün xərclərin cəmi ✓.</summary>
        public decimal XercCemi { get; set; }

        /// <summary>Alış qiyməti + xərclər = ÜMUMİ MAYA DƏYƏRİ ✓✓✓</summary>
        public decimal UmumiMaya => AlisQiymeti + XercCemi;

        /// <summary>Avtomobil üzrə qeyd (ən axırıncı xərcə yazılır ✓).</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Qeydin yazıldığı xərcin təsviri ✓ (məs. «Qeydiyyat» ✓).</summary>
        public string QeydYeri { get; set; } = string.Empty;

        /// <summary>Sıra nömrəsi (avtomatik verilibsə burada görünür ✓).</summary>
        public int SiraNomresi { get; set; }

        /// <summary>Avtomobil bazaya yazıldımı? ✓ (yoxlama rejimində <c>false</c> ✓)</summary>
        public bool ElaveOlundu { get; set; }

        /// <summary>Uyğunlaşdırılmış və ya yeni yaradılmış kateqoriyalar ✓.</summary>
        public List<string> Kateqoriyalar { get; } = new();

        /// <summary>Xəbərdarlıqlar / xətalar ✓.</summary>
        public List<string> Xeberdarliqlar { get; } = new();
    }
}
