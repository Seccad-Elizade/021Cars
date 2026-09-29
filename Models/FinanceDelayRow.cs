namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// «💰 Maliyyə» panelində <b>⚠️ GECİKMƏ (CƏRİMƏ)</b> kartına klikləyəndə
    /// açılan cədvəlin BİR SƏTRİ — «hansı maşına gecikmə yazılıb».
    /// </summary>
    public sealed class FinanceDelayRow
    {
        /// <summary>Gecikmə qeydinin (CreditTransaction) identifikatoru.</summary>
        public int TransactionId { get; set; }

        /// <summary>Avtomobil: «Toyota Camry · 90 AA 123».</summary>
        public string Masin { get; set; } = "—";

        /// <summary>Müştəri adı.</summary>
        public string Mustəri { get; set; } = "—";

        /// <summary>Müqavilə nömrəsi.</summary>
        public string Muqavile { get; set; } = "—";

        /// <summary>Neçənci taksitə aiddir.</summary>
        public int Taksit { get; set; }

        /// <summary>Taksitin plan tarixi (ödənilməli idi).</summary>
        public DateTime PlanTarix { get; set; }

        /// <summary>Gecikmə tarixi (faktiki ödəniş tarixi).</summary>
        public DateTime? GecikmeTarixi { get; set; }

        /// <summary>Neçə gün gecikib.</summary>
        public int Gun { get; set; }

        /// <summary>Cərimə məbləği (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Ödənilibmi.</summary>
        public bool Odenilib { get; set; }

        /// <summary>Asif-in payı — cərimənin YARISI ✓ (50/50 bölgü).</summary>
        public decimal AsifPayi => Math.Round(Mebleg / 2m, 2);

        /// <summary>Musa-nın payı — cərimənin YARISI ✓ (50/50 bölgü).</summary>
        public decimal MusaPayi => Mebleg - AsifPayi;   // yuvarlaqlaşdırma fərqi Musaya ✓

        public string MasinMetni => string.IsNullOrWhiteSpace(Masin) ? "—" : Masin;
        public string MustəriMetni => string.IsNullOrWhiteSpace(Mustəri) ? "—" : Mustəri;
        public string TaksitMetni => Taksit > 0 ? $"{Taksit}-ci taksit" : "—";
        public string PlanTarixMetni => PlanTarix == default ? "—" : PlanTarix.ToString("dd.MM.yyyy");
        public string GecikmeTarixiMetni => GecikmeTarixi is DateTime d ? d.ToString("dd.MM.yyyy") : "—";
        public string GunMetni => Gun > 0 ? $"{Gun} gün" : "—";
        public string MeblegMetni => $"{Mebleg:N2} ₼";
        public string AsifPayiMetni => $"{AsifPayi:N2} ₼";
        public string MusaPayiMetni => $"{MusaPayi:N2} ₼";
        public string VeziyyetMetni => Odenilib ? "✔ ödənilib" : "⏳ gözləyir";

        public override string ToString() => $"{MasinMetni} — {MeblegMetni} ({VeziyyetMetni})";
    }
}
