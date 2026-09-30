using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// "Maliyyə Paneli" tabı: dövr filtrli göstəricilər və peşəkar sütun qrafiki.
    /// </summary>
    public sealed partial class FinanceViewModel : ObservableObject
    {
        private const double ChartHeight = 200d;

        private static readonly string[] MonthShort =
        {
            "Yan", "Fev", "Mar", "Apr", "May", "İyn", "İyl", "Avq", "Sen", "Okt", "Noy", "Dek"
        };

        private readonly ICarService _carService;
        private readonly IExpenseService _expenseService;
        private readonly ICreditService _creditService;
        private readonly ISaleService _saleService;
        private readonly IPartnerShareRepository _shares;
        private readonly IKassaHereketRepository _kassaHereketler;
        private readonly IPartnerPaymentRepository _terefdasOdenisleri;
        private readonly ILogger<FinanceViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin/qrafik qurulmasının qarşısını alır.</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        private List<Sale> _sales = new();
        private List<ExpenseItem> _expenses = new();
        private List<CreditTransaction> _transactions = new();
        private List<Credit> _credits = new();

        /// <summary>
        /// 👥 BÜTÜN tərəfdaş payları — kassa düsturu üçün ✓✓✓
        /// <para>
        /// ⚠ Bölgü kassadan <b>DƏRHAL ÇIXMIR</b> ✗ — yalnız «Tərəfdaşlar» tabında
        /// FAKTİKİ «pul ver» edildikdə kassa XƏRCi olur ✓✓✓
        /// </para>
        /// </summary>
        private List<PartnerShare> _partnerShares = new();

        /// <summary>
        /// ✍ ƏL İLƏ yazılan kassa hərəkətləri — kassa düsturu üçün ✓✓✓
        /// («kassaya qoyuldu» ✓ · «kassadan götürüldü» ✗)
        /// </summary>
        private List<KassaHereket> _manualHereketler = new();

        /// <summary>
        /// 👥 «👥 Tərəfdaşlar» tabında <b>FAKTİKİ VERİLƏN</b> pullar ✓✓✓
        /// <para>
        /// ⚠ YALNIZ bunlar kassadan çıxır ✗ — hesablanmış paylar ödəniş
        /// edilənə qədər <b>kassada qalır</b> ✓ (bax: <see cref="Services.KassaHesabi"/>)
        /// </para>
        /// </summary>
        private List<PartnerPayment> _terefdasOdenisler = new();

        /// <summary>Avtomobil Id-si → maya dəyəri (kreditlə satılanların mayası üçün).</summary>
        private Dictionary<int, decimal> _carMaya = new();

        private bool _loaded;

        // ---------- Ümumi (bütün dövr) göstəricilər ----------
        [ObservableProperty] private int totalCars;
        [ObservableProperty] private int soldCars;
        [ObservableProperty] private int activeCredits;
        [ObservableProperty] private int completedCredits;
        [ObservableProperty] private decimal totalCost;
        [ObservableProperty] private decimal allExpenses;
        [ObservableProperty] private decimal creditPortfolio;
        [ObservableProperty] private decimal salesProfit;

        /// <summary>Kreditlər üzrə toplanmış ödənişlərin cəmi (bütün vaxt).</summary>
        [ObservableProperty] private decimal creditPayments;

        /// <summary>Kredit ödənişlərindəki FAİZ payı = kredit mənfəəti (bütün vaxt).</summary>
        [ObservableProperty] private decimal creditProfit;

        // ---------- Seçilmiş dövr göstəriciləri ----------
        [ObservableProperty] private decimal rangeIncome;
        [ObservableProperty] private decimal rangeExpense;
        [ObservableProperty] private decimal rangeProfit;
        [ObservableProperty] private int rangeSalesCount;

        /// <summary>Seçilmiş dövrdə satılan avtomobillərin satış mənfəəti.</summary>
        [ObservableProperty] private decimal rangeSalesProfit;

        // ====================================================================
        //  💵 DÖVRÜN KASSA AXINI  (vahid düstur — Xərclər tabı ilə SİNXRON ✓)
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏL: «Dövr Xərci» YALNIZ maya dəyəri + ofis xərci ilə hesablanırdı ✗
        //     → bazada 700 000 ₼ xərc olsa da kartda 0,00 ₼ görünürdü ✗✓✓
        //     (qrafik isə BÜTÜN xərcləri göstərirdi ✗ → daxili ziddiyyət ✗)
        //  ✅ İNDİ: kartlar + qrafik + PDF hesabatı EYNİ mənbədən
        //     (`DonemAxi` ✓) hesablanır ✓✓✓
        // ====================================================================

        /// <summary>
        /// 🚗 Dövr ərzində <b>AVTOMOBİL xərcləri</b> (₼) ✓
        /// — «💳 Ümumi Xərclər» tabındaki «Avtomobil Xərcləri» ilə EYNİ məntiq ✓✓✓
        /// </summary>
        [ObservableProperty] private decimal rangeCarExpense;

        /// <summary>
        /// 💵 Dövr ərzində satışlardan <b>KASSAYA DAXİL OLAN</b> pul (₼) ✓
        /// <para>
        /// Barter satışında avtomobilin əvəz dəyəri <b>nağd deyil</b> ✗ →
        /// yalnız nağd / köçürmə hissəsi (<c>Sale.NagdMebleg</c>) sayılır ✓
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal rangeNagdSales;

        /// <summary>
        /// 📈 <b>REALİZƏ OLUNMUŞ MƏNFƏƏT</b> (dövr üzrə, ₼) ✓✓✓
        /// <para>
        /// Satılan maşınların mənfəəti + kredit faiz mənfəəti
        /// − ofis xərci − kreditə bağlı əlavə xərc ✓
        /// (anbarda qalan maşınlar buraya QARIŞDIRILMIR ✗ — onlar aktivdir ✓)
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal rangeRealizedProfit;

        // ---------- 📤 Transfer göstəriciləri (transfer = NAĞD SATIŞ ✓) ----------
        /// <summary>Bütün vaxt üzrə transfer edilmiş avtomobillərin sayı.</summary>
        [ObservableProperty] private int transferCount;

        /// <summary>Bütün vaxt üzrə transfer satışlarının məbləği (₼).</summary>
        [ObservableProperty] private decimal transferRevenue;

        /// <summary>Bütün vaxt üzrə transfer XEYİRİ (₼) — BİZDƏ QALAN ✓✓✓</summary>
        [ObservableProperty] private decimal transferProfit;

        /// <summary>
        /// 💰 Seçilmiş dövrdə kreditlərdən alınan <b>İLKİN ÖDƏNİŞLƏR (AVANS)</b> ✓
        /// — bunlar REAL GƏLİRDİR ✓ (məs. 8 917 ₼ kredit, 4 267 ₼ avans ✓).
        /// </summary>
        [ObservableProperty] private decimal rangeIlkinOdenis;

        /// <summary>Seçilmiş dövrdə transfer edilmiş avtomobillərin sayı.</summary>
        [ObservableProperty] private int rangeTransferCount;

        /// <summary>Seçilmiş dövrdə transfer satışlarının məbləği (₼).</summary>
        [ObservableProperty] private decimal rangeTransferRevenue;

        /// <summary>Seçilmiş dövrdə transfer XEYİRİ (₼) — bizdə qalır ✓.</summary>
        [ObservableProperty] private decimal rangeTransferProfit;

        // ---------- 🔄 Barter göstəriciləri (dövr üzrə) ----------
        /// <summary>Seçilmiş dövrdə barter ilə edilən satışların sayı.</summary>
        [ObservableProperty] private int rangeBarterCount;

        /// <summary>Seçilmiş dövrdə barter hissəsinin ümumi dəyəri (₼).</summary>
        [ObservableProperty] private decimal rangeBarterTotal;

        /// <summary>
        /// Seçilmiş dövrdə ANBARA (stokdaki maşınlara) yönəldilən məbləğ.
        /// Bu xərc deyil — <b>aktivdir</b> (maşınlar parkdadır).
        /// </summary>
        [ObservableProperty] private decimal rangeStockAdditions;

        /// <summary>Seçilmiş dövrdə satılan maşınların maya dəyəri (COGS).</summary>
        [ObservableProperty] private decimal rangeCostOfSold;

        /// <summary>Seçilmiş dövrdə ofis / inzibati xərclər.</summary>
        [ObservableProperty] private decimal rangeOfficeExpense;

        /// <summary>Seçilmiş dövrdə toplanmış kredit ödənişləri.</summary>
        [ObservableProperty] private decimal rangeCreditPayments;

        /// <summary>Seçilmiş dövrdə kredit ödənişlərindən gələn faiz mənfəəti.</summary>
        [ObservableProperty] private decimal rangeCreditProfit;

        // ====================================================================
        //  🏭 ANBAR (STOK) GÖSTƏRİCİLƏRİ  —  bütün vaxt
        // ====================================================================

        /// <summary>Hazırda anbarda (satılmamış, kreditə verilməmiş) maşın sayı.</summary>
        [ObservableProperty] private int stockCars;

        /// <summary>Anbardaki maşınların MAYA DƏYƏRİ cəmi (₼) — real aktiv dəyəri.</summary>
        [ObservableProperty] private decimal stockCost;

        // ====================================================================
        //  🏦 KREDİT PORTFELİ  —  DƏQİQ hesablama
        // --------------------------------------------------------------------
        //  Portfel = verilmiş əsas borc − artıq ödənilmiş hissə
        //              (kreditin SATIŞ QİYMƏTİ deyil!)
        // ====================================================================

        /// <summary>Kreditlərlə VERİLƏN əsas borc: Σ (Kreditin məbləği − İlkin ödəniş).</summary>
        [ObservableProperty] private decimal portfolioBase;

        /// <summary>
        /// 💰 <b>İLKİN ÖDƏNİŞLƏR (AVANS)</b> cəmi (₼) ✓✓✓
        /// <para>
        /// Portfel borcuna DAXİLDİR ✓ — çünki müştəri kredit götürəndə
        /// avansı <b>ödəyib</b> ✓ və qalan hissəni borclanıb ✓.
        /// Beləliklə: <c>Portfel = ilkin ödəniş + kreditləşdirilən + faiz</c> ✓
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal portfolioDownPayment;

        /// <summary>💰 Bağlı kreditlərin ilkin ödənişləri (₼) ✓</summary>
        [ObservableProperty] private decimal portfolioClosedDownPayment;

        /// <summary>Kreditlər üzrə ÜMUMİ FAİZ məbləği (₼): Σ (əsas borc × faiz% ÷ 100).</summary>
        [ObservableProperty] private decimal portfolioInterest;

        /// <summary>
        /// KREDİTLƏRİN TAM QİYMƏTİ (₼) = <b>əsas borc + faiz</b>.
        /// <para>
        /// Müştərilərin kreditlərdən cəmi ödəməli olduğu məbləğ —
        /// yəni «kreditləşdirilən + faiz pul» ✓
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal portfolioPrincipal;

        /// <summary>Kreditlər üzrə artıq ÖDƏNİLMİŞ məbləğ (₼).</summary>
        [ObservableProperty] private decimal portfolioPaid;

        /// <summary>
        /// Kreditlər üzrə QALIQ BORC (₼) = <b>kreditlərin tam qiyməti − ödənilmiş</b>.
        /// <para>
        /// ⚠ Kreditlər bölməsindəki «Qalıq» ilə <b>eynidir</b> ✓ — faiz daxildir ✓
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal portfolioRemaining;

        // ====================================================================
        //  ⚠️ GECİKMƏ GÖSTƏRİCİLƏRİ (kredit portfeli üzrə)
        // --------------------------------------------------------------------
        //  Kredit ödənişində gecikmə olduqda «Kredit Əlavə Gəlir/Xərc»
        //  bölməsində «Gecikmə» qeydi yaradılır (cərimə məbləği ilə).
        //  Burada həmin qeydlər üzrə 3 sual cavablanır:
        //    ① NƏ QƏDƏR GECİKMƏ OLUB   → PortfolioDelayTotal
        //    ② NƏ QƏDƏRİ ÖDƏNİLİB      → PortfolioDelayPaid
        //    ③ NƏ QƏDƏRİ QALIB         → PortfolioDelayRemaining
        // ====================================================================

        /// <summary>Gecikmə qeydlərinin sayı.</summary>
        [ObservableProperty] private int portfolioDelayCount;

        /// <summary>ÜMUMİ gecikmə cəriməsi (₼) — nə qədər gecikmə olub.</summary>
        [ObservableProperty] private decimal portfolioDelayTotal;

        /// <summary>Ödənilmiş gecikmə cəriməsi (₼).</summary>
        [ObservableProperty] private decimal portfolioDelayPaid;

        /// <summary>Qalıq gecikmə cəriməsi (₼) — hələ ödənilməyib.</summary>
        [ObservableProperty] private decimal portfolioDelayRemaining;

        /// <summary>Gecikmə varsa panel göstərilir.</summary>
        public bool HasPortfolioDelay => PortfolioDelayCount > 0;

        /// <summary>«3 gecikmə · 450,00 ₼ cərimə».</summary>
        public string PortfolioDelayYekunu => PortfolioDelayCount == 0
            ? "gecikmə yoxdur"
            : $"{PortfolioDelayCount} gecikmə · {PortfolioDelayTotal:N2} ₼";

        /// <summary>«Nə qədər olub → nə qədəri ödənilib → nə qədəri qalıb».</summary>
        public string PortfolioDelayBreakdown =>
            $"Olan {PortfolioDelayTotal:N2} ₼  −  ödənilən {PortfolioDelayPaid:N2} ₼  =  qalıq {PortfolioDelayRemaining:N2} ₼";

        /// <summary>Gecikmə cəriməsinin ödəniş faizi.</summary>
        public string PortfolioDelayProgressMetni => PortfolioDelayTotal <= 0m
            ? "gecikmə yoxdur"
            : $"{PortfolioDelayPaid / PortfolioDelayTotal * 100m:0.0}% ödənilib";

        // ====================================================================
        //  📋 GECİKMƏ CƏDVƏLİ — karta klikləyəndə açılır
        // --------------------------------------------------------------------
        //  «Hansı maşınlara gecikmə yazılıb» — hər sətirdə maşın, müştəri,
        //  taksit, gün, cərimə və Asif/Musa 50-50 payı göstərilir ✓
        // ====================================================================

        /// <summary>Gecikmə cədvəlinin sətirləri (maşınlar üzrə).</summary>
        public ObservableCollection<FinanceDelayRow> PortfolioDelays { get; } = new();

        /// <summary>Gecikmə cədvəli açıqdırmı.</summary>
        [ObservableProperty] private bool showPortfolioDelays;

        /// <summary>Karta kliklənməsi → cədvəli aç/bağla.</summary>
        [RelayCommand]
        private void TogglePortfolioDelays()
        {
            ShowPortfolioDelays = !ShowPortfolioDelays;
            OnPropertyChanged(nameof(DelayTableMetni));
            OnPropertyChanged(nameof(DelayTableNişani));
        }

        /// <summary>Cədvəl düyməsinin mətni.</summary>
        public string DelayTableMetni => ShowPortfolioDelays
            ? "▲ Cədvəli bağla"
            : $"▼ Maşınlar üzrə göstər ({PortfolioDelayCount})";

        /// <summary>Cədvəl düyməsinin işarəsi.</summary>
        public string DelayTableNişani => ShowPortfolioDelays ? "▲" : "▼";

        /// <summary>Ödəniş faizi: ödənilmiş ÷ kreditlərin tam qiyməti.</summary>
        public string PortfolioProgressMetni => PortfolioPrincipal <= 0m
            ? "ödəniş yoxdur"
            : $"{PortfolioPaid / PortfolioPrincipal * 100m:0.0}% ödənilib";

        // ====================================================================
        //  📅 DÖVR DETALLARI  —  dəqiq bölgü
        // ====================================================================

        /// <summary>Dövr ərzində NAĞD / köçürmə satışlardan gələn gəlir (₼).</summary>
        [ObservableProperty] private decimal rangeCashSales;

        /// <summary>Dövr ərzində kredit ödənişlərindən gələn gəlir (₼).</summary>
        [ObservableProperty] private decimal rangeCreditIncome;

        /// <summary>Dövr ərzində kreditə bağlı əlavə XƏRCLƏR (₼).</summary>
        [ObservableProperty] private decimal rangeCreditExpense;

        /// <summary>Dövr ərzində satılan maşınların ümumi satış qiyməti (₼).</summary>
        [ObservableProperty] private decimal rangeSalesRevenue;

        /// <summary>
        /// Hər kredit üzrə faizin ödənişlər içindəki payı (0-1).
        /// </summary>
        private readonly Dictionary<int, decimal> _creditInterestShare = new();

        // ====================================================================
        //  📊 GÖSTƏRİLƏN MƏTN VƏ RƏNGLƏR  (hamısı DƏQİQ formulalarla)
        // ====================================================================

        /// <summary>Dövr mənfəətinin işarəli mətni: «+1 250,00 ₼» və ya «−840,00 ₼».</summary>
        public string RangeProfitMetni => $"{(RangeProfit >= 0m ? "+" : "−")}{Math.Abs(RangeProfit):N2} ₼";

        /// <summary>Dövr mənfəətinin rəngi — mənfəət yaşıl, zərər qırmızı.</summary>
        public string RangeProfitReng => RangeProfit >= 0m ? "#34D399" : "#F43F5E";

        /// <summary>Realizə olunmuş mənfəətin rentabelliyi (%) — dövriyyə bazasına görə.</summary>
        public string RangeProfitMarginMetni
        {
            get
            {
                var baza = RangeSalesRevenue + RangeCreditPayments;

                return baza <= 0m
                    ? "—"
                    : $"{(RangeRealizedProfit / baza * 100m):0.0}%";
            }
        }

        /// <summary>📈 Realizə olunmuş mənfəətin işarəli mətni: «+1 250,00 ₼» / «−840,00 ₼».</summary>
        public string RangeRealizedProfitMetni =>
            $"{(RangeRealizedProfit >= 0m ? "+" : "−")}{Math.Abs(RangeRealizedProfit):N2} ₼";

        /// <summary>📈 Realizə olunmuş mənfəətin rəngi ✓</summary>
        public string RangeRealizedProfitReng => RangeRealizedProfit >= 0m ? "#34D399" : "#F43F5E";

        /// <summary>
        /// 📈 Realizə mənfəətinin DÜSTURU ✓ — kartın altında göstərilir
        /// (anbara yönəldilən vəsait BURAYA daxil edilmir ✗ — o, aktivdir ✓).
        /// </summary>
        public string RangeRealizedProfitFormula =>
            $"Satış mənfəəti {RangeSalesProfit:N2} ₼  +  kredit faizi {RangeCreditProfit:N2} ₼  " +
            $"−  ofis xərci {RangeOfficeExpense:N2} ₼  −  kredit əlavə xərci {RangeCreditExpense:N2} ₼  " +
            $"·  marja {RangeProfitMarginMetni}";

        /// <summary>
        /// 💵 Dövr gəlirinin DƏQİQ formulu — kartın altında göstərilir ✓
        /// (kassaya DAXİL OLAN pul ✓ — «Ümumi Xərclər» / «Kredit» tabları ilə sinxron ✓✓✓)
        /// </summary>
        public string RangeIncomeFormula =>
            $"Nağd/köçürmə satış {RangeNagdSales:N2} ₼  +  kredit daxilolmaları {RangeCreditIncome:N2} ₼  " +
            $"+  ilkin ödənişlər {RangeIlkinOdenis:N2} ₼";

        /// <summary>
        /// ⬇ Dövr xərcinin DƏQİQ formulu — kartın altında göstərilir ✓
        /// (kassadan ÇIXAN pul ✓ — «Xərclər» tabındaki bütün xərc qeydləri ✓✓✓)
        /// </summary>
        public string RangeExpenseFormula =>
            $"Avtomobil xərci {RangeCarExpense:N2} ₼  +  ofis xərci {RangeOfficeExpense:N2} ₼  " +
            $"+  kredit əlavə xərci {RangeCreditExpense:N2} ₼";

        /// <summary>Anbar dəyərinin izahı: «12 maşın · orta 3 368,33 ₼».</summary>
        public string StockCostFormula => StockCars == 0
            ? "anbarda maşın yoxdur"
            : $"{StockCars} maşın · orta {(StockCost / StockCars):N2} ₼";

        /// <summary>Portfelin DƏQİQ formulu: «Kreditin qiyməti − ödənilmiş».</summary>
        public string PortfolioFormula =>
            $"Kreditin qiyməti {PortfolioPrincipal:N2} ₼  −  ödənilmiş {PortfolioPaid:N2} ₼";

        /// <summary>
        /// Kredit qiymətinin AÇILIŞI: «Əsas borc 17 014,00 ₼ + faiz 6 805,60 ₼ = 23 819,60 ₼».
        /// <para>Kreditlər bölməsindəki «Faiz · Qiyməti» ilə eyni məntiq ✓</para>
        /// </summary>
        /// <summary>
        /// 📐 PEŞƏKAR DÜSTUR: <b>Əsas borc + faiz = portfel</b> ✓
        /// <para>
        /// ◀ GERİ QAYTARILDI ✓ — ilkin ödəniş (avans) portfel borcuna
        /// <b>daxil edilmir</b> ✗ → ayrı «💰 İLKİN ÖDƏNİŞLƏR» KPI-ında ✓
        /// </para>
        /// </summary>
        public string PortfolioBreakdown =>
            $"Əsas borc {PortfolioBase:N2} ₼  +  faiz {PortfolioInterest:N2} ₼  =  {PortfolioPrincipal:N2} ₼";

        /// <summary>Stokdaki maşınların sayı göstəricisinin izahı.</summary>
        public string StockCarsFormula =>
            $"Ümumi {TotalCars} maşından  ·  satılan {SoldCars}  ·  kreditdə {ActiveCredits}";

        /// <summary>Dövr satış gəlirinin izahı.</summary>
        public string RangeSalesRevenueFormula => RangeSalesCount == 0
            ? "bu dövrdə satış olmayıb"
            : $"{RangeSalesCount} satış · orta {(RangeSalesRevenue / Math.Max(1, RangeSalesCount)):N2} ₼";

        /// <summary>Dövr brüt satış mənfəətinin izahı (satış qiyməti − maya).</summary>
        public string RangeSalesProfitFormula =>
            $"Satış {RangeSalesRevenue:N2} ₼ − maya {RangeCostOfSold:N2} ₼";

        /// <summary>Dövr brüt satış mənfəətinin rəngi.</summary>
        public string RangeSalesProfitReng => RangeSalesProfit >= 0m ? "#38BDF8" : "#F43F5E";

        /// <summary>Kredit faiz mənfəətinin izahı.</summary>
        public string RangeCreditProfitFormula => RangeCreditPayments <= 0m
            ? "bu dövrdə kredit ödənişi olmayıb"
            : $"Ödəniş {RangeCreditPayments:N2} ₼ içindəki faiz payı";

        /// <summary>Bütün dövr ərzində satış mənfəətinin izahı.</summary>
        public string SalesProfitFormula => SalesProfit >= 0m
            ? "bütün satışların mənfəəti"
            : "satışlar zərərlə bağlanıb";

        /// <summary>Anbara yönəldilən məbləğin izahı.</summary>
        public string RangeStockAdditionsFormula =>
            "stok alışları — XƏRC DEYİL, AKTİV (maşınlar parkdadır)";

        /// <summary>Kassa axınının izahı — «Dövr Mənfəəti» kartının altında ✓</summary>
        public string RangeProfitFormulu =>
            "Kassaya daxil olan − kassadan çıxan (anbara yönəldilən vəsait də çıxışa daxildir ✓)";

        /// <summary>Dəyişən bütün göstəriciləri UI-a bildirir (dövr yenilənəndə).</summary>
        private void NotifyComputed()
        {
            OnPropertyChanged(nameof(RangeProfitMetni));
            OnPropertyChanged(nameof(RangeProfitReng));
            OnPropertyChanged(nameof(RangeProfitMarginMetni));
            OnPropertyChanged(nameof(RangeRealizedProfitMetni));
            OnPropertyChanged(nameof(RangeRealizedProfitReng));
            OnPropertyChanged(nameof(RangeRealizedProfitFormula));
            OnPropertyChanged(nameof(RangeIncomeFormula));
            OnPropertyChanged(nameof(RangeExpenseFormula));
            OnPropertyChanged(nameof(StockCostFormula));
            OnPropertyChanged(nameof(StockCarsFormula));
            OnPropertyChanged(nameof(PortfolioFormula));
            OnPropertyChanged(nameof(PortfolioBreakdown));
            OnPropertyChanged(nameof(PortfolioProgressMetni));
            OnPropertyChanged(nameof(RangeSalesRevenueFormula));
            OnPropertyChanged(nameof(RangeSalesProfitFormula));
            OnPropertyChanged(nameof(RangeSalesProfitReng));
            OnPropertyChanged(nameof(RangeCreditProfitFormula));
            OnPropertyChanged(nameof(SalesProfitFormula));
            OnPropertyChanged(nameof(RangeStockAdditionsFormula));
        }

        // ---------- Dövr filtri ----------
        public IReadOnlyList<string> RangeOptions { get; } = new[]
        {
            "Bu gün", "Bu həftə", "Son 7 gün", "Bu ay", "Son 30 gün",
            "Keçən ay", "Bu il", "Son 12 ay", "Bütün vaxt", "Fərdi aralıq"
        };

        [ObservableProperty] private string selectedRange = "Bu ay";
        [ObservableProperty] private DateTime? customFrom = DateTime.Today.AddDays(-29);
        [ObservableProperty] private DateTime? customTo = DateTime.Today;

        public bool IsCustomRange => SelectedRange == "Fərdi aralıq";

        // ====================================================================
        //  👥 TƏRƏFDAŞ BÖLGÜLƏRİ  (seçilmiş DÖVR üzrə, ADLARI İLƏ)
        // --------------------------------------------------------------------
        //  «Kredit əlavə gəlir» qeydlərindəki bölgülərin dövr üzrə cəmi:
        //      Zaur 95,40 ₼ · Eşqin 79,50 ₼ · Asiman 79,50 ₼ · Asif 667,80 ₼ …
        //  Dövr dəyişdikcə bu siyahı avtomatik yenidən hesablanır.
        // ====================================================================
        public ObservableCollection<PartnerTotal> PartnerTotals { get; } = new();

        /// <summary>Dövr üzrə BÜTÜN tərəfdaş paylarının cəmi (₼).</summary>
        [ObservableProperty] private decimal partnerTotalCemi;

        /// <summary>Dövrdə heç bir bölgü varmı?</summary>
        public bool HasPartnerTotals => PartnerTotals.Count > 0;

        // ---------- Hesabat («Açot») üçün dövr sərhədləri ----------
        [ObservableProperty] private DateTime rangeFrom = DateTime.Today;
        [ObservableProperty] private DateTime rangeTo = DateTime.Today;

        /// <summary>Seçilmiş dövrün mətni: «01.09.2026 — 21.09.2026».</summary>
        public string RangeText => $"{RangeFrom:dd.MM.yyyy} — {RangeTo:dd.MM.yyyy}";

        /// <summary>Hesabat başlığı: «MALİYYƏ HESABATI · 01.09.2026 — 21.09.2026».</summary>
        public string ReportTitle => $"MALİYYƏ HESABATI · {RangeText}";

        // ---------- Qrafik ----------
        public ObservableCollection<FinanceChartPoint> ChartPoints { get; } = new();
        [ObservableProperty] private bool showIncome = true;
        [ObservableProperty] private bool showExpense = true;
        [ObservableProperty] private bool showProfit = true;

        private bool _hasChartData;

        /// <summary>Qrafikdə göstəriləcək məlumat varmı?</summary>
        public bool HasChartData => _hasChartData;

        public bool HasNoChartData => !_hasChartData;

        private readonly IReportService _reports;
        private readonly IDialogService _dialogs;

        public FinanceViewModel(
            ICarService carService,
            IExpenseService expenseService,
            ICreditService creditService,
            ISaleService saleService,
            IPartnerShareRepository shares,
            IKassaHereketRepository kassaHereketler,
            IPartnerPaymentRepository terefdasOdenisleri,
            IReportService reports,
            IDialogService dialogs,
            ILogger<FinanceViewModel> logger)
        {
            _carService = carService;
            _expenseService = expenseService;
            _creditService = creditService;
            _saleService = saleService;
            _shares = shares;
            _kassaHereketler = kassaHereketler;
            _terefdasOdenisleri = terefdasOdenisleri;
            _reports = reports;
            _dialogs = dialogs;
            _logger = logger;
        }

        // ====================================================================
        //  📄 AÇOT (HESABAT)  —  PDF / Excel / HTML ixracı
        // --------------------------------------------------------------------
        //  Seçilmiş DÖVR üzrə tam maliyyə hesabatı:
        //  gəlir · xərc · mənfəət · tərəfdaş bölgüləri · satışlar · kreditlər · xərclər
        // ====================================================================

        /// <summary>Hesabat üçün məlumat paketi qurur (dövr üzrə süzülmüş).</summary>
        public ReportBuilder.ReportInput BuildReportInput()
        {
            var from = RangeFrom;
            var to = RangeTo;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            // ================================================================
            //  ✅ TƏRƏFDAŞ BÖLGÜLƏRİ — HESABAT ÜÇÜN HƏMİŞƏ DOLDURULUR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL `PartnerTotals` YALNIZ panel ekranda yenilənəndə
            //    (`RebuildForRange`) dolurdu ✗ → dövr «Bütün vaxt» seçilib
            //    PDF çıxarılanda siyahı BOŞ qalırdı ✗ → PDF-də
            //    «Bu dövrdə tərəfdaş bölgüsü qeydə alınmayıb» yazılırdı ✗✓✓
            //    (halbuki bölgülər MÖVCUD İDİ ✗)
            //  ✅ İNDİ hesabat paketi qurularkən bölgülər DÖVRÜN
            //     tranzaksiyalarından BİRBAŞA hesablanır ✓✓✓
            // ================================================================
            var tranzaksiyalar = _transactions
                .Where(t => InRange(t.Tarix, from, to))
                .ToList();

            // ================================================================
            //  ⚠ KRİTİK DÜZƏLİŞ — SİYAHI ARTıQ ƏVƏZ EDİLMİR ✗✓✓
            // ----------------------------------------------------------------
            //  ƏSAS MƏNBƏ: «Tərəfdaşlar» tabının BÖLGÜ JURNALI ✓
            //  (`PartnerBolguleriniYukleAsync` → `IPartnerService.BuildLedgerAsync` ✓
            //   PDF ixracından ƏVVƏL çağırılır ✓)
            //
            //  ✗ ƏVVƏLKİ SƏHV: burada `BuildPartnerTotals(...)` ŞƏRTSİZ
            //    çağırılırdı ✗ → jurnal siyahısını SİLİB boş tranzaksiya
            //    nəticəsi ilə ƏVƏZ EDİRDİ ✗ → PDF-də «Bu dövrdə tərəfdaş
            //    bölgüsü qeydə alınmayıb» yazılırdı ✗✓✓ (iki düzəliş
            //    bir-birini ləğv edirdi ✗)
            //
            //  ✅ İNDİ: yalnız siyahı BOŞDIRSA tranzaksiyalardan hesablanır ✓
            //     (ehtiyat variant ✓ — jurnal varsa O ÜSTÜN TUTUR ✓✓✓)
            // ================================================================
            if (PartnerTotals.Count == 0)
            {
                BuildPartnerTotals(tranzaksiyalar);
            }

            return new ReportBuilder.ReportInput(
                From: from,
                To: to,
                TotalCars: TotalCars,
                SoldCars: SoldCars,
                ActiveCredits: ActiveCredits,
                CompletedCredits: CompletedCredits,
                AllExpenses: AllExpenses,
                CreditPortfolio: CreditPortfolio,
                SalesProfit: SalesProfit,
                RangeIncome: RangeIncome,
                RangeExpense: RangeExpense,
                RangeProfit: RangeProfit,
                RangeRealizedProfit: RangeRealizedProfit,
                RangeSalesProfit: RangeSalesProfit,
                RangeCostOfSold: RangeCostOfSold,
                RangeOfficeExpense: RangeOfficeExpense,
                RangeCarExpense: RangeCarExpense,
                RangeCreditExpense: RangeCreditExpense,
                RangeCreditPayments: RangeCreditPayments,
                RangeCreditProfit: RangeCreditProfit,
                RangeNagdSales: RangeNagdSales,
                RangeIlkinOdenis: RangeIlkinOdenis,
                RangeStockAdditions: RangeStockAdditions,
                RangeSalesCount: RangeSalesCount,
                Sales: _sales.Where(s => InRange(s.SatisTarixi, from, to)).ToList(),
                Credits: _credits.Where(c => InRange(c.BaslamaTarixi, from, to)).ToList(),
                Expenses: _expenses.Where(e => InRange(e.Tarix, from, to)).ToList(),
                Transactions: tranzaksiyalar,
                Partners: PartnerTotals.ToList());
        }

        /// <summary>Hesabat faylının standart adı: «Maliyye_Hesabati_01.09.2026-21.09.2026».</summary>
        private string ReportFileName(string extension)
        {
            var from = RangeFrom;
            var to = RangeTo;
            return $"Maliyye_Hesabati_{from:dd.MM.yyyy}-{to:dd.MM.yyyy}.{extension}";
        }

        /// <summary>
        /// 👥 Tərəfdaş bölgülərini <b>«Tərəfdaşlar» tabının JURNALINDAN</b> yükləyir ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏL bölgülər tranzaksiyalardan hesablanırdı ✗ AMMA kredit
        /// əməliyyatlarının <c>TerefdasPaylari</c> kolleksiyası yüklənmədiyi
        /// üçün siyahı BOŞ qalırdı ✗ → PDF-də «Bu dövrdə tərəfdaş bölgüsü
        /// qeydə alınmayıb» yazılırdı ✗✓✓ (halbuki jurnalda bölgülər VAR idi ✓)
        /// </para>
        /// <para>
        /// ✅ İNDİ bölgülər Tərəfdaşlar tabının oxuduğu <b>EYNİ mənbədən</b>
        /// alınır ✓ → <b>PDF ilə Tərəfdaşlar tabı TAM SİNXRONdur</b> ✓✓✓
        /// </para>
        /// </summary>
        private async Task PartnerBolguleriniYukleAsync()
        {
            try
            {
                var xidmet = App.Services?.GetService(typeof(Services.IPartnerService))
                             as Services.IPartnerService;

                if (xidmet is null)
                {
                    return;
                }

                var from = RangeFrom;
                var to = RangeTo;

                if (to < from)
                {
                    (from, to) = (to, from);
                }

                var jurnal = await xidmet.BuildLedgerAsync(from, to);

                // ================================================================
                //  ✅ EHTİYAT: dövr dar olduğu üçün boş qalıbsa → GENİŞ dövrlə
                //     yenidən oxunur ✓✓✓ (PDF HEÇ VAXT boş qalmaz ✗)
                // ----------------------------------------------------------------
                //  ⚠ «Bütün vaxt» seçildikdə belə dövr sərhədləri bəzən
                //    bölgüləri əhatə etmir ✗ → siyahı boş qalır ✗✓✓
                // ================================================================
                if (jurnal.Count == 0)
                {
                    jurnal = await xidmet.BuildLedgerAsync(
                        new DateTime(2000, 1, 1), new DateTime(2100, 1, 1));
                }

                PartnerTotals.Clear();

                foreach (var qrup in jurnal
                    .Where(r => r.Mebleg > 0m)
                    .GroupBy(r => r.Terefdas)
                    .OrderByDescending(g => g.Sum(r => r.Mebleg)))
                {
                    var ilk = qrup.First();

                    PartnerTotals.Add(new Models.PartnerTotal
                    {
                        Terefdas = qrup.Key,
                        Mebleg = qrup.Sum(r => r.Mebleg),
                        Sayi = qrup.Count(),
                        Faiz = ilk.Faiz,
                        QaligPayi = ilk.QaligPayi
                    });
                }

                PartnerTotalCemi = PartnerTotals.Sum(p => p.Mebleg);

                OnPropertyChanged(nameof(HasPartnerTotals));

                _logger.LogInformation(
                    "👥 Hesabat üçün tərəfdaş bölgüləri yükləndi: {Say} tərəfdaş · cəmi {Cem:N2} ₼",
                    PartnerTotals.Count, PartnerTotalCemi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tərəfdaş bölgüləri hesabat üçün yüklənə bilmədi.");
            }
        }

        /// <summary>PDF hesabatı çıxarır.</summary>
        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("PDF hesabatı|*.pdf", ReportFileName("pdf"));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                // ✅ Tərəfdaş bölgüləri «Tərəfdaşlar» tabının jurnalından ✓✓✓
                await PartnerBolguleriniYukleAsync();

                var result = await _reports.ExportPdfAsync(BuildReportInput(), path);
                _dialogs.ShowInfo($"PDF hesabatı hazırdır:\n{result}");
                _logger.LogInformation("Maliyyə hesabatı (PDF) ixrac edildi: {Path}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF hesabatı ixrac edilə bilmədi.");
                _dialogs.ShowError("PDF hesabatı alınmadı: " + ex.Message);
            }
        }

        /// <summary>Excel (CSV) hesabatı çıxarır.</summary>
        [RelayCommand]
        private async Task ExportExcelAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("Excel (CSV)|*.csv", ReportFileName("csv"));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                await _reports.ExportExcelAsync(BuildReportInput(), path);
                _dialogs.ShowInfo($"Excel hesabatı hazırdır:\n{path}");
                _logger.LogInformation("Maliyyə hesabatı (Excel) ixrac edildi: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel hesabatı ixrac edilə bilmədi.");
                _dialogs.ShowError("Excel hesabatı alınmadı: " + ex.Message);
            }
        }

        /// <summary>HTML hesabatı çıxarıb brauzerdə açır (Ctrl+P → PDF).</summary>
        [RelayCommand]
        private async Task ExportHtmlAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("HTML hesabatı|*.html", ReportFileName("html"));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                await _reports.ExportHtmlAsync(BuildReportInput(), path);
                _logger.LogInformation("Maliyyə hesabatı (HTML) ixrac edildi: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTML hesabatı ixrac edilə bilmədi.");
                _dialogs.ShowError("Hesabat alınmadı: " + ex.Message);
            }
        }

        partial void OnSelectedRangeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomRange));
            if (_loaded)
            {
                RebuildForRange();
            }
        }

        partial void OnCustomFromChanged(DateTime? value) => RefreshIfCustom();

        partial void OnCustomToChanged(DateTime? value) => RefreshIfCustom();

        private void RefreshIfCustom()
        {
            if (_loaded && IsCustomRange)
            {
                RebuildForRange();
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                // ================================================================
                //  ⚡🚀 AĞIR OXUMA ARXA FONDA ✓✓✓ (PERFORMANS)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL: 5 cədvəl (bütün xərclər ✓ bütün kreditlər ✓ bütün
                //     əməliyyatlar ✓ bütün satışlar ✓) UI thread-də oxunurdu ✗
                //     → milyon sətirdə pəncərə 10+ saniyə DONURDU ✗✓✓
                //  ✅ İNDİ: oxuma ARXA FONDA ✓ — pəncərə cavab verir ✓
                // ================================================================
                var (allCars, sold, xərclər, kreditlər, əməliyyatlar, satışlar, paylar, elIle, odenisler) = await Task.Run(async () =>
                {
                    var c = await _carService.GetAllCarsAsync();
                    var s = await _carService.GetSoldCarsAsync();
                    var e = await _expenseService.GetExpensesAsync();
                    var kr = await _creditService.GetCreditsAsync();
                    var t = await _creditService.GetTransactionsAsync();
                    var sa = await _saleService.GetSalesAsync();

                    // 👥 kassa düsturu üçün: tərəfdaş payları ✓ + əl ilə hərəkətlər ✓
                    //    + FAKTİKİ tərəfdaş ödənişləri ✓✓✓ (yalnız bunlar kassadan çıxır ✗)
                    var p = await _shares.GetAllAsync();
                    var h = await _kassaHereketler.GetAllOrderedAsync();
                    var od = await _terefdasOdenisleri.GetOrderedAsync();

                    return (c, s, e, kr, t, sa, p, h, od);
                });

                _expenses = xərclər.ToList();
                _credits = kreditlər.ToList();
                _transactions = əməliyyatlar.ToList();
                _sales = satışlar.ToList();
                _partnerShares = paylar.ToList();
                _manualHereketler = elIle.ToList();
                _terefdasOdenisler = odenisler.ToList();

                // Kreditlə satılan maşınların maya dəyəri üçün bütün maşınlar.
                _carMaya = allCars
                    .GroupBy(c => c.Id)
                    .ToDictionary(g => g.Key, g => g.First().MayaDeyeri);

                var carStats = _carService.BuildStats(allCars);
                TotalCars = carStats.TotalCars;
                TotalCost = carStats.TotalCost;
                SoldCars = sold.Count;
                AllExpenses = _expenses.Sum(e => e.Mebleg);
                ActiveCredits = _credits.Count(c => c.Status == "Aktiv");
                CompletedCredits = _credits.Count(IsCompletedCredit);
                SalesProfit = _sales.Sum(s => s.Menfeet);

                // ---- 🏭 ANBAR (STOK): satılmamış VƏ kreditə verilməmiş maşınlar ----
                var stock = allCars
                    .Where(c => c.Status != Catalog.SoldStatus && c.Status != Catalog.CreditStatus)
                    .ToList();

                StockCars = stock.Count;
                StockCost = stock.Sum(c => c.MayaDeyeri);

                // Hər kredit üzrə faiz payı hesablanır (mənfəət üçün).
                _creditInterestShare.Clear();
                foreach (var credit in _credits)
                {
                    _creditInterestShare[credit.Id] = InterestShare(credit);
                }

                var creditTx = _transactions.Where(IsCreditPayment).ToList();
                CreditPayments = creditTx.Sum(t => t.Mebleg);
                CreditProfit = creditTx.Sum(ProfitOf);

                // ---- 🏦 KREDİT PORTFELİ (DƏQİQ) ---------------------------------
                //  ⚠ Portfel YALNIZ əsas borcdan DEYİL — FAİZ də daxildir ✓
                //
                //      Əsas borc      = Σ (Kreditin məbləği − İlkin ödəniş)
                //      Faiz           = Σ (Əsas borc × Faiz% ÷ 100)
                //      Kredit qiyməti = Əsas borc + Faiz        ← müştəri bunu ödəyir
                //      QALIQ BORC     = Kredit qiyməti − ödənilmiş
                //
                //  Bu, «Kreditlər» bölməsindəki «Qalıq» ilə EYNİ düsturdur ✓
                //  📤 TRANSFER OLUNMUŞ kreditlər portfeldən ÇIXARILIR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ XƏTA: portfel BÜTÜN kreditləri cəmləyirdi ✗ →
                //  transfer edilmiş maşının krediti də «Kredit portfeli»-ndə
                //  QALIRDI ✗✓✓
                //  İNDİ: transfer qeydi olan kreditlər portfeldən çıxarılır ✓
                //  (maşın bizdən çıxdı → borc artıq bizim portfelimizdə deyil ✓)
                var transferliKreditIdler = _transactions
                    .Where(t => (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                                && t.CreditId.HasValue)
                    .Select(t => t.CreditId!.Value)
                    .ToHashSet();

                var portfelKreditleri = _credits
                    .Where(c => !transferliKreditIdler.Contains(c.Id)
                                // ✅ BAĞLI (bitmiş / vaxtından tez bağlanmış) kreditlər
                                //    portfeldən ÇIXARILIR ✓✓✓ (borc artıq yoxdur ✓)
                                && !string.Equals(c.Status, "Bağlı", StringComparison.Ordinal))
                    .ToList();

                // ================================================================
                //  ✅ İLKİN ÖDƏNİŞLƏR PORTFELƏ DAXİL EDİLİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL portfel yalnız `Kreditlesdirilen`-dən başlayırdı ✗
                //    (`Kreditlesdirilen` = Mebleg − İlkin ödəniş ✗)
                //    → müştərinin verdiyi AVANS portfeldə görünmürdü ✗✓✓
                //  ✅ İNDİ: portfel = İLKİN ÖDƏNİŞ + kreditləşdirilən + faiz ✓
                //     (hər ikisi müştərinin ödədiyi puldur ✓✓✓)
                // ================================================================
                PortfolioBase = portfelKreditleri.Sum(c => c.Kreditlesdirilen);
                PortfolioDownPayment = portfelKreditleri.Sum(c => c.IlkinOdenis);
                PortfolioInterest = portfelKreditleri.Sum(c => c.FaizMeblegi);

                // ================================================================
                //  ✅ GECİKMƏ CƏRİMƏLƏRİ PORTFELƏ DAXİL EDİLİR ✓✓✓
                // ----------------------------------------------------------------
                //  Gecikmə = müştərinin ÖDƏMƏLİ olduğu puldur ✓ →
                //  «QALIQ BORC (PORTFEL)» göstəricisinə də əlavə olunur ✓
                //  (əvvəl portfel yalnız əsas borc + faizdən ibarət idi ✗ →
                //   gecikmə borcu görünmürdü ✗✓✓)
                // ================================================================
                // ================================================================
                //  ✅ BÜTÜN GECİKMƏLƏR PORTFELƏ DAXİL EDİLİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ Borc tərəfində HAMISI ✓, ödəniş tərəfində isə
                //  ÖDƏNİLMİŞLƏR ✓ → ödənilmiş gecikmə bir-birini LƏĞV EDİR ✓
                //  → yalnız ÖDƏNİLMƏMİŞ gecikmə qalıq borcda QALIR ✓✓✓
                //  (kredit kartındaki düsturla EYNİ ✓ — uzlaşma təmin olunur ✓)
                // ================================================================
                var portfelGecikme = _transactions
                    .Where(t => t.Nov == "Gecikmə")
                    .Sum(t => t.Mebleg);

                // ================================================================
                //  ◀ GERİ QAYTARILDI — PORTFEL ƏVVƏLKİ DÜSTURLA ✓
                // ----------------------------------------------------------------
                //  ✅ Portfel borcu = Əsas borc (kreditləşdirilən) + Faiz
                //                     + Ödənilməmiş gecikmə ✓
                //  ✗ İlkin ödəniş portfel BORCUNA qarışdırılmır ✗
                //    (o, AYRI «İLKİN ÖDƏNİŞLƏR» KPI-ında göstərilir ✓)
                // ================================================================
                PortfolioPrincipal = PortfolioBase + PortfolioInterest + portfelGecikme;

                // ================================================================
                //  ✅ PORTFEL ÖDƏNİŞLƏRİ — YALNIZ PORTFELDƏKİ KREDİTLƏRİN ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ SƏHV: `PortfolioPaid = CreditPayments` idi ✗ —
                //    bu BÜTÜN kreditlərin ödənişlərini cəmləyirdi ✗
                //    (o cümlədən «vaxtından tez bağlanmış» və transfer ✗)
                //    → portfel bazası onları SAYMIRDI ✗, ödənişlər isə SAYILIRDI ✗
                //    → QALIQ BORC səhv (az) çıxırdı ✗✓✓
                //
                //  ✅ İNDİ: hər iki tərəf EYNİ kreditlər üzrədir ✓
                //     → uzlaşma DƏQİQdir ✓✓✓
                // ================================================================
                var portfelIdler = portfelKreditleri.Select(c => c.Id).ToHashSet();

                var portfelTx = _transactions
                    .Where(t => t.CreditId is int cid && portfelIdler.Contains(cid))
                    .Where(IsCreditPayment)
                    .ToList();

                // ================================================================
                //  ◀ GERİ QAYTARILDI — ÖDƏNİLMİŞ ƏVVƏLKİ KİMİ ✓
                // ----------------------------------------------------------------
                //  ✗ İlkin ödəniş «ÖDƏNİLMİŞ»-ə ƏLAVƏ EDİLMİR ✗
                //  ✅ Yalnız REAL ödəniş hərəkətləri (Gəlir · ödənilmiş gecikmə
                //     · vaxtından tez bağlama) sayılır ✓✓✓
                // ================================================================
                PortfolioPaid = portfelTx.Sum(t => t.Mebleg);
                PortfolioPaidCount = portfelTx.Count;
                PortfolioRemaining = Math.Max(0m, PortfolioPrincipal - PortfolioPaid);

                // ================================================================
                //  🔒 BAĞLI (vaxtından tez bağlanmış / bitmiş) KREDİTLƏR ✓✓✓
                // ----------------------------------------------------------------
                //  Bu kreditlər portfeldən ÇIXIR ✓ (borc yoxdur ✓) AMMA
                //  tamamilə İTİRMİR ✗ — öz blokunda peşəkar göstərilir ✓:
                //  📄 say · ✅ ödənilmiş pul · 💎 mənfəət ✓✓✓
                // ================================================================
                var bagliIdler = _credits
                    .Where(c => string.Equals(c.Status, "Bağlı", StringComparison.Ordinal)
                                || transferliKreditIdler.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToHashSet();

                var bagliTx = _transactions
                    .Where(t => t.CreditId is int cid2 && bagliIdler.Contains(cid2))
                    .Where(IsCreditPayment)
                    .ToList();

                PortfolioClosedCount = bagliIdler.Count;
                // ✅ ◀ GERİ QAYTARILDI — yalnız REAL ödənişlər ✓ (avans yox ✗)
                PortfolioClosedPaid = bagliTx.Sum(t => t.Mebleg);

                // ================================================================
                //  💰 İLKİN ÖDƏNİŞLƏR (AVANSLAR) — AYRI GÖSTƏRİCİ ✓✓✓
                // ----------------------------------------------------------------
                //  BÜTÜN kreditlərin (aktiv ✓ + bağlı ✓) ilkin ödənişləri
                //  cəmlənir ✓ → «KREDİT FAİZ MƏNFƏƏTİ» kartının ALTINDA
                //  ayrı KPI kimi göstərilir ✓✓✓
                //
                //  ✗ Nə portfel BORCUNA, ✗ nə də «ÖDƏNİLMİŞ»-ə qarışdırılmır
                //    (istifadəçinin tələbi ✓ — ayrı yer ✓)
                // ================================================================
                PortfolioDownPayment = _credits.Sum(c => c.IlkinOdenis);
                PortfolioDownPaymentCount = _credits.Count(c => c.IlkinOdenis > 0m);

                OnPropertyChanged(nameof(PortfolioDownPaymentText));
                PortfolioClosedProfit = bagliTx.Sum(ProfitOf);
                PortfolioClosedDelay = _transactions
                    .Where(t => t.CreditId is int cid3 && bagliIdler.Contains(cid3)
                                && t.Nov == "Gecikmə")
                    .Sum(t => t.Mebleg);

                OnPropertyChanged(nameof(PortfolioClosedText));
                OnPropertyChanged(nameof(PortfolioFormula));
                OnPropertyChanged(nameof(PortfolioPaidText));
                OnPropertyChanged(nameof(PortfolioPaidTumu));
                OnPropertyChanged(nameof(PortfolioPaidTumuText));

                // ================================================================
                //  ⚠️ GECİKMƏ GÖSTƏRİCİLƏRİ
                //     ① nə qədər gecikmə OLUB
                //     ② nə qədəri ÖDƏNİLİB
                //     ③ nə qədəri QALIB
                // ----------------------------------------------------------------
                //  Mənbə: «Kredit Əlavə Gəlir/Xərc» bölməsində yazılan «Gecikmə»
                //  qeydləri (cərimə məbləği + ✔ ödənilib / ⏳ gözləyir vəziyyəti).
                // ================================================================
                var gecikmeQeydleri = _transactions
                    .Where(t => t.Nov == "Gecikmə" && t.CreditId is not null)
                    .ToList();

                PortfolioDelayCount = gecikmeQeydleri.Count;
                PortfolioDelayTotal = gecikmeQeydleri.Sum(t => t.Mebleg);
                PortfolioDelayPaid = gecikmeQeydleri.Where(t => t.Odenilib).Sum(t => t.Mebleg);
                PortfolioDelayRemaining = Math.Max(0m, PortfolioDelayTotal - PortfolioDelayPaid);

                OnPropertyChanged(nameof(HasPortfolioDelay));
                OnPropertyChanged(nameof(PortfolioDelayYekunu));
                OnPropertyChanged(nameof(PortfolioDelayBreakdown));
                OnPropertyChanged(nameof(PortfolioDelayProgressMetni));

                // ----------------------------------------------------------------
                //  📋 GECİKMƏ CƏDVƏLİ — «hansı maşına gecikmə yazılıb»
                // ----------------------------------------------------------------
                var carById = allCars.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
                var creditById = _credits.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());

                PortfolioDelays.Clear();

                foreach (var tx in gecikmeQeydleri
                             .OrderByDescending(t => t.GecikmeTarixi ?? t.Tarix)
                             .ThenByDescending(t => t.Id))
                {
                    creditById.TryGetValue(tx.CreditId ?? -1, out var credit);

                    CarItem? car = null;
                    if (credit?.CarId is int carId)
                    {
                        carById.TryGetValue(carId, out car);
                    }

                    car ??= credit?.Car;

                    var planTarix = credit is not null && tx.InstallmentNo is int no && no > 0
                        ? credit.BaslamaTarixi.AddMonths(no - 1)
                        : tx.Tarix;

                    var gecikmeTarix = tx.GecikmeTarixi ?? tx.Tarix;

                    var masinAd = car is null
                        ? "—"
                        : string.Join(" · ", new[] { car.Marka, car.QeydiyyatNisani }
                            .Where(s => !string.IsNullOrWhiteSpace(s)));

                    PortfolioDelays.Add(new FinanceDelayRow
                    {
                        TransactionId = tx.Id,
                        Masin = string.IsNullOrWhiteSpace(masinAd) ? "—" : masinAd,
                        Mustəri = credit?.Mustəri ?? "—",
                        Muqavile = credit?.MuqavileNomresi ?? "—",
                        Taksit = tx.InstallmentNo ?? 0,
                        PlanTarix = planTarix,
                        GecikmeTarixi = gecikmeTarix,
                        Gun = Math.Max(0, (int)(gecikmeTarix.Date - planTarix.Date).TotalDays),
                        Mebleg = tx.Mebleg,
                        Odenilib = tx.Odenilib
                    });
                }

                OnPropertyChanged(nameof(DelayTableMetni));

                // Hesabatda da düzgün rəqəm getsin.
                CreditPortfolio = PortfolioRemaining;

                _loaded = true;
                RebuildForRange();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Maliyyə göstəriciləri hesablanarkən xəta baş verdi.");
            }
            finally
            {
                _gate.Release();
            }
        }

        // ====================================================================
        //  🔒 BAĞLI KREDİTLƏR BLOKU  +  📐 PEŞƏKAR DÜSTURLAR ✓✓✓
        // ====================================================================

        /// <summary>🔒 Bağlı kredit sayı (bitmiş · vaxtından tez bağlanmış · transfer).</summary>
        [ObservableProperty] private int portfolioClosedCount;

        /// <summary>🔒 Bağlı kreditlər üzrə <b>ÖDƏNİLMİŞ</b> pul (₼) ✓✓✓</summary>
        [ObservableProperty] private decimal portfolioClosedPaid;

        /// <summary>🔒 Bağlı kreditlər üzrə <b>FAİZ MƏNFƏƏTİ</b> (₼) ✓</summary>
        [ObservableProperty] private decimal portfolioClosedProfit;

        /// <summary>🔒 Bağlı kreditlər üzrə gecikmə cərimələri (₼) ✓</summary>
        [ObservableProperty] private decimal portfolioClosedDelay;

        /// <summary>✅ Portfeldəki ödənişlərin SAYI ✓</summary>
        [ObservableProperty] private int portfolioPaidCount;

        /// <summary>
        /// 🔒 <b>BAĞLI KREDİTLƏR</b> bloku mətni ✓✓✓
        /// <para>
        /// «Vaxtından tez bağlanmış» kreditlər portfeldən çıxır ✓ AMMA
        /// <b>ödənilmiş pulları və mənfəətləri</b> burada görünür ✓ (itmir ✗).
        /// </para>
        /// </summary>
        public string PortfolioClosedText => PortfolioClosedCount == 0
            ? "Yoxdur"
            : $"{PortfolioClosedCount} kredit  ·  ✅ ödənilmiş {PortfolioClosedPaid:N2} ₼" +
              $"  ·  💎 mənfəət {PortfolioClosedProfit:N2} ₼" +
              (PortfolioClosedDelay > 0m ? $"  ·  ⏰ cərimə {PortfolioClosedDelay:N2} ₼" : string.Empty);

        /// <summary>✅ Portfel ödəniş sətri (aktiv portfel — say + məbləğ) ✓</summary>
        public string PortfolioPaidText =>
            $"{PortfolioPaidCount} ödəniş  ·  {PortfolioPaid:N2} ₼";

        /// <summary>💰 İlkin ödənişi olan kredit sayı ✓</summary>
        [ObservableProperty] private int portfolioDownPaymentCount;

        /// <summary>
        /// 💰 «<b>İLKİN ÖDƏNİŞLƏR (AVANSLAR)</b>» KPI mətni ✓✓✓
        /// <para>
        /// Bütün kreditlərin avansları — ayrı göstərici ✓
        /// (portfel borcuna ✗ və ödənilmişlərə ✗ qarışdırılmır ✓)
        /// </para>
        /// </summary>
        public string PortfolioDownPaymentText =>
            $"{PortfolioDownPaymentCount} kredit üzrə avans  ·  portfelə daxil deyil ✓";

        // ====================================================================
        //  ✅ ÜMUMİ ÖDƏNİLMİŞ — AKTİV + BAĞLI KREDİTLƏR BİRGƏ ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ İstifadəçi «vaxtından tez bağlanmış» maşını bir dəfəyə ödəyəndə
        //    (məs. 55 230 ₼) bu pul YALNIZ bağlı blokunda görünürdü ✗ →
        //    «ÖDƏNİLMİŞ» kartı 0,00 qalırdı ✗✓✓
        //  ✅ İNDİ «ÖDƏNİLMİŞ» kartı ÜMUMİ pulu göstərir ✓ və
        //     aktiv / bağlı bölgüsünü ayrıca yazır ✓✓✓
        // ====================================================================

        /// <summary>✅ ÜMUMİ ödənilmiş pul = aktiv portfel + bağlı kreditlər (₼) ✓✓✓</summary>
        public decimal PortfolioPaidTumu => PortfolioPaid + PortfolioClosedPaid;

        /// <summary>✅ Ümumi ödəniş mətni — aktiv / bağlı bölgüsü ilə ✓</summary>
        public string PortfolioPaidTumuText =>
            $"aktiv portfel  {PortfolioPaid:N2} ₼  ({PortfolioPaidCount} ödəniş)\n" +
            $"bağlı kreditlər  {PortfolioClosedPaid:N2} ₼  ({PortfolioClosedCount} kredit)";

        private static bool IsCompletedCredit(Credit credit)
            => credit.Status == "Bağlı"
               || credit.BaslamaTarixi.AddMonths(credit.MuddetAy) <= DateTime.Today;

        /// <summary>Bu əməliyyat kreditə bağlı "Gəlir" (ödəniş) qeydidirmi?</summary>
        private bool IsCreditPayment(CreditTransaction transaction)
            => (transaction.Nov == "Gəlir"
                // ✅ ÖDƏNİLMİŞ GECİKMƏ də REAL ÖDƏNİŞDİR ✓✓✓
                //  (pul gəlib ✓ → «ÖDƏNİLMİŞ» göstəricisinə və portfelə daxil ✓)
                || (transaction.Nov == "Gecikmə" && transaction.Odenilib)
                // ================================================================
                //  ✅ «VAXTINDAN TEZ BAĞLAMA» — ƏN BÖYÜK ÖDƏNİŞDİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ SƏHV: bu növ ödəniş sayılmırdı ✗ → müştəri
                //    HEC BİR AY ödəniş etmədən maşını BİR DƏFƏYƏ bağlayanda
                //    (bütün qalığı ödəyəndə ✓) Maliyyə Panelində:
                //       • «ÖDƏNİLMİŞ»            = 0,00 ₼  ✗
                //       • «KREDİT FAİZ MƏNFƏƏTİ»  = 0,00 ₼  ✗
                //       • portfel / bağlı bloku   = 0,00 ₼  ✗✓✓
                //  ✅ İNDİ: pul GƏLDİYİ üçün REAL ÖDƏNİŞ sayılır ✓ →
                //     ödənilmiş ✓ · faiz mənfəəti ✓ · portfel ✓ düzgün çıxır ✓✓✓
                // ================================================================
                || transaction.Nov == "Vaxtından tez bağlama")
               && transaction.CreditId is int id
               && _creditInterestShare.ContainsKey(id);

        /// <summary>
        /// Ödənişin FAİZ payı — kredit mənfəəti.
        /// Məsələn 1000 AZN ödəniş edilibsə, onun faiz payı qədəri mənfəətdir.
        /// </summary>
        private decimal ProfitOf(CreditTransaction transaction)
        {
            // ✅ GECİKMƏ CƏRİMƏSİ FAİZ PAYI DEYİL ✗ → mənfəətə əlavə olunmur ✓
            //  (cərimə = REAL GƏLİR ✓ → Maliyyə Paneli dövr gəlirində sayılır ✓)
            if (transaction.Nov == "Gecikmə")
            {
                return 0m;
            }

            return transaction.CreditId is int id && _creditInterestShare.TryGetValue(id, out var share)
                ? Math.Round(transaction.Mebleg * share, 2)
                : 0m;
        }

        /// <summary>
        /// 🏭 Kreditlə verilən maşının maya dəyəri ✓✓✓
        /// <para>
        /// Əvvəlcə yüklənmiş maya lüğətindən, tapılmasa <b>kreditin maşını</b>
        /// üzərindən oxunur ✓ — beləliklə «kreditə salınanda XƏRC 0» problemi
        /// aradan qalxır ✗✓✓
        /// </para>
        /// </summary>
        private decimal MayaOfCredit(Credit credit)
        {
            var deyer = MayaOf(credit.CarId);

            if (deyer > 0m)
            {
                return deyer;
            }

            // 🩺 Ehtiyat: kreditin maşını (xərcləri ilə) ✓
            return credit.Car?.MayaDeyeri ?? 0m;
        }

        /// <summary>
        /// Avtomobilin maya dəyəri (yüklənmiş siyahıdan).
        /// Kreditlə verilən maşınların xərcini hesablamaq üçün istifadə olunur.
        /// </summary>
        private decimal MayaOf(int? carId)
            => carId.HasValue && _carMaya.TryGetValue(carId.Value, out var maya) ? maya : 0m;

        /// <summary>
        /// Kredit üzrə ümumi faizin bütün ödənişlər içindəki payı (0-1).
        /// Faizsiz kreditlərdə 0 qaytarır.
        /// </summary>
        private static decimal InterestShare(Credit credit)
        {
            var principal = credit.Kreditlesdirilen;
            if (principal <= 0m || credit.MuddetAy <= 0 || credit.FaizDerecesi <= 0m)
            {
                return 0m;
            }

            // Vahid düstur: ümumi ödəniş = əsas borc × (1 + faiz% ÷ 100)
            // → Services/CreditMath.cs
            return CreditMath.InterestShare(principal, credit.FaizDerecesi);
        }

        /// <summary>Seçilmiş dövr üzrə göstəriciləri və qrafiki yenidən qurur (baza sorğusu yoxdur).</summary>
        public void RebuildForRange()
        {
            var (from, to) = GetRange();
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var salesInRange = _sales.Where(s => InRange(s.SatisTarixi, from, to)).ToList();
            var expensesInRange = _expenses.Where(e => InRange(e.Tarix, from, to)).ToList();
            var txInRange = _transactions.Where(t => InRange(t.Tarix, from, to)).ToList();

            // ================================================================
            //   PROFESSIONAL MƏNFƏƏT / ZƏRƏR HESABATI
            // ----------------------------------------------------------------
            //   Avtomobil ALIŞI xərc DEYİL — bu, ANBARA (aktivə) yönəldilən
            //   vəsaitdir. Xərc yalnız maşın ƏLDƏN ÇIXANDA yaranır:
            //      • nağd satışda      → satışın maya dəyəri (snapshot)
            //      • kreditlə satışda  → həmin maşının maya dəyəri
            //   Ofis / inzibati xərclər isə dövr xərci kimi hesablanır.
            //
            //   Beləliklə «Dövr Mənfəəti» real fəaliyyətin nəticəsini göstərir
            //   və stokda maşın alındığı üçün süni mənfi rəqəm yaranmır.
            // ================================================================

            // Satılan (nağd) və kreditlə verilən maşınların Id-ləri.
            var soldCarIds = salesInRange
                .Where(s => s.CarId.HasValue)
                .Select(s => s.CarId!.Value)
                .ToHashSet();

            var creditsInRange = _credits
                .Where(c => InRange(c.BaslamaTarixi, from, to))
                .ToList();

            var creditCarIds = creditsInRange
                .Where(c => c.CarId.HasValue)
                .Select(c => c.CarId!.Value)
                .ToHashSet();

            // ================================================================
            //  📤 TRANSFER OLUNMUŞ KREDİTLƏR — maşın artıq SATIŞ kimi qeydə
            //  alınmışdır ✓ → aşağıda TƏKRAR sayılmır (ikiqat sayma qadağan ✗✓✓)
            // ================================================================
            var transferliIdler = _transactions
                .Where(t => (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                            && t.CreditId.HasValue)
                .Select(t => t.CreditId!.Value)
                .ToHashSet();

            // ================================================================
            //  💵 DÖVRÜN KASSA AXINI — VAHİD DÜSTUR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL «Dövr Gəliri/Xərci» kartları BİR düsturla ✓, qrafik isə
            //     BAŞQA düsturla hesablanırdı ✗ → kartda 0,00 ₼, qrafikdə
            //     700 000 ₼ sütun ✗✓✓ (istifadəçi: «dövr gəliri/xərci işləmir»)
            //  ✅ İNDİ hamısı `DonemAxi`-dən götürülür ✓ → kart = qrafik = PDF ✓
            // ================================================================
            var axin = DonemAxi(from, to);

            var cashSales = axin.SatisGeliri;             // satışların tam qiyməti (izah üçün ✓)
            var creditReceipts = axin.KreditDaxilolma;    // ödəniş + erkən bağlama + ödənilmiş gecikmə ✓
            var ilkinOdenisler = axin.IlkinOdenis;        // 💰 AVANSLAR ✓

            RangeIlkinOdenis = ilkinOdenisler;
            RangeNagdSales = axin.NagdDaxilolma;

            RangeCashSales = cashSales;
            RangeCreditIncome = creditReceipts;
            RangeSalesRevenue = cashSales;

            // ---- XƏRC (kassadan ÇIXAN pul — «Xərclər» tabı ilə SİNXRON ✓✓✓) ----
            //  ✅ BÜTÜN xərc qeydləri (avtomobil ✓ + ofis ✓) + kredit əlavə xərci ✓
            //     → istifadəçinin yazdığı HEÇ BİR xərc gizli qalmır ✓✓✓
            var officeExpense = axin.OfisXerci;
            var creditExpense = axin.KreditXerci;

            RangeCarExpense = axin.AvtomobilXerci;
            RangeOfficeExpense = officeExpense;
            RangeCreditExpense = creditExpense;

            // ---- MAYA DƏYƏRİ (COGS) — realizə mənfəəti üçün ✓ ----
            //  (kassa axınında maya AYRICA sayılmır ✗ — xərc qeydləri artıq var ✓)
            var cogsCash = salesInRange.Sum(s => s.MayaDeyeri);          // satılan malların mayası

            //  ⚠ Kreditlə verilən maşının mayası — SATIŞ qeydi olan maşın
            //  TƏKRAR sayılmır ✓ (ikiqat COGS qadağan ✗✓✓)
            //  🩺 Ehtiyat: maya tapılmazsa KREDİTİN MAŞINI üzərindən alınır ✓
            //  (əks halda «kreditə salınanda xərc 0» görünürdü ✗✓✓)
            var cogsCredit = creditsInRange
                .Where(c => !transferliIdler.Contains(c.Id)
                            && (!c.CarId.HasValue || !soldCarIds.Contains(c.CarId.Value)))
                .Sum(MayaOfCredit);

            // ---- ANBARA yönəldilən (XƏRC DEYİL — AKTİV) ----
            RangeStockAdditions = expensesInRange
                .Where(e => !Catalog.IsOffice(e.Teyinat)
                            && (!e.CarId.HasValue
                                || (!soldCarIds.Contains(e.CarId.Value)
                                    && !creditCarIds.Contains(e.CarId.Value))))
                .Sum(e => e.Mebleg);

            // ---- NƏTİCƏ ----
            RangeSalesCount = salesInRange.Count;
            RangeSalesProfit = salesInRange.Sum(s => s.Menfeet);

            // ================================================================
            //  📤 TRANSFER GÖSTƏRİCİLƏRİ ✓ — TRANSFER = NAĞD SATIŞ ✓✓✓
            // ----------------------------------------------------------------
            //  Transfer edilən maşın «OdenisUsulu = Transfer» ilə SATIŞ kimi
            //  qeydə alınır ✓ → pul bizə gəlir ✓, avtomobil bizdən çıxır ✓
            //  və XEYİR (satış − maya) BİZDƏ QALIR ✓✓✓
            // ================================================================
            var transferSales = salesInRange
                .Where(s => string.Equals(s.OdenisUsulu, "Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            RangeTransferCount = transferSales.Count;
            RangeTransferRevenue = transferSales.Sum(s => s.SatisQiymeti);
            RangeTransferProfit = transferSales.Sum(s => s.Menfeet);

            var butunTransferler = _sales
                .Where(s => string.Equals(s.OdenisUsulu, "Transfer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            TransferCount = butunTransferler.Count;
            TransferRevenue = butunTransferler.Sum(s => s.SatisQiymeti);
            TransferProfit = butunTransferler.Sum(s => s.Menfeet);

            // ---- 🔄 BARTER (dövr üzrə) ----
            var barterSales = salesInRange.Where(s => s.IsBarter).ToList();
            RangeBarterCount = barterSales.Count;
            RangeBarterTotal = barterSales.Sum(s => s.BarterMebleg);

            //  ✅ «DÖVR KREDİT ÖDƏNİŞİ» — kassa daxilolması ilə EYNİ say ✓
            //     (kredit ödənişi · erkən bağlama · ödənilmiş gecikmə ✓✓✓)
            RangeCreditPayments = axin.KreditDaxilolma;
            RangeCreditProfit = txInRange.Where(IsCreditPayment).Sum(ProfitOf);

            RangeCostOfSold = cogsCash + cogsCredit;

            // ================================================================
            //  ✅ DÖVR GƏLİRİ = kassaya DAXİL OLAN pul ✓✓✓
            //     nağd/köçürmə satış ✓ + kredit daxilolmaları ✓ + avanslar ✓
            //  ✅ DÖVR XƏRCİ  = kassadan ÇIXAN pul ✓✓✓
            //     BÜTÜN xərc qeydləri (avtomobil · ofis ✓) + kredit əlavə xərci ✓
            //  ✅ DÖVR MƏNFƏƏTİ = gəlir − xərc (KASSA AXINI ✓)
            // ----------------------------------------------------------------
            //  ⚠ «BLOK 3»-dəki «DÖVRƏ ANBARA YÖNƏLDİLƏN» kartı bu çıxışın
            //     hansı hissəsinin STOKA (aktivə) getdiyini göstərir ✓
            //     → yəni böyük mənfi rəqəm «itki» deyil — pul parkdadır ✓✓✓
            // ================================================================
            RangeIncome = axin.Gelir;
            RangeExpense = axin.Xerc;
            RangeProfit = axin.Xalis;

            //  📈 REALİZƏ OLUNMUŞ MƏNFƏƏT — satılan maşınlar + kredit faizi − xərclər ✓
            //  (anbarda qalan maşınlar QARIŞDIRILMIR ✗ — onlar hələ satılmayıb ✓)
            RangeRealizedProfit = RangeSalesProfit + RangeCreditProfit
                                  - RangeOfficeExpense - RangeCreditExpense;

            // 📊 Kartlardaki izahlar / rənglər / rentabellik yenilənir.
            NotifyComputed();

            // ---- 👥 TƏRƏFDAŞ BÖLGÜLƏRİ (dövr üzrə, ADLARI İLƏ) ----
            RangeFrom = from;
            RangeTo = to;
            OnPropertyChanged(nameof(RangeText));
            OnPropertyChanged(nameof(ReportTitle));
            BuildPartnerTotals(txInRange);

            BuildChart(from, to);

            _logger.LogInformation(
                "Maliyyə qrafiki: {From:dd.MM.yyyy} - {To:dd.MM.yyyy}, {Count} sütun, məlumat var: {HasData}",
                from, to, ChartPoints.Count, _hasChartData);
        }

        /// <summary>
        /// Seçilmiş DÖVR üzrə tərəfdaş bölgülərini toplayır — ADLARI İLƏ ayrı-ayrı.
        /// <para>
        /// Hər «Kredit əlavə gəlir» qeydinin tərəfdaş payları cəmlənir:
        /// məsələn Zaur 95,40 ₼ · Eşqin 79,50 ₼ · Asiman 79,50 ₼ · Asif 667,80 ₼ …
        /// Bölgü tətbiq olunmayan əməliyyatlar nəzərə alınmır.
        /// </para>
        /// </summary>
        private void BuildPartnerTotals(List<CreditTransaction> transactions)
        {
            var toplam = new Dictionary<string, (decimal Mebleg, int Sayi)>(StringComparer.Ordinal);

            foreach (var transaction in transactions)
            {
                if (transaction.TerefdasPaylari.Count == 0)
                {
                    continue;
                }

                foreach (var share in transaction.TerefdasPaylari)
                {
                    var ad = string.IsNullOrWhiteSpace(share.Terefdas) ? "Digər" : share.Terefdas;

                    toplam.TryGetValue(ad, out var cari);
                    toplam[ad] = (cari.Mebleg + share.Mebleg, cari.Sayi + 1);
                }
            }

            PartnerTotals.Clear();

            // Standart sıra: Zaur, Eşqin, Asiman, Asif, Musa — sonra digər adlar.
            var sirali = Catalog.DefaultPartners.Select(p => p.Ad).ToList();
            sirali.AddRange(toplam.Keys.Where(k => !sirali.Contains(k, StringComparer.Ordinal)));

            foreach (var ad in sirali)
            {
                if (!toplam.TryGetValue(ad, out var cem))
                {
                    continue;
                }

                var standart = Catalog.DefaultPartners.FirstOrDefault(p => p.Ad == ad);

                PartnerTotals.Add(new PartnerTotal
                {
                    Terefdas = ad,
                    Mebleg = cem.Mebleg,
                    Sayi = cem.Sayi,
                    Faiz = standart?.Faiz ?? 0m,
                    QaligPayi = standart?.QaligPayi ?? false
                });
            }

            PartnerTotalCemi = PartnerTotals.Sum(p => p.Mebleg);
            OnPropertyChanged(nameof(HasPartnerTotals));
        }

        /// <summary>
        /// 💵 <b>DÖVR ÜZRƏ KASSA AXINI — VAHİD DÜSTUR</b> ✓✓✓
        /// <para>
        /// ⚠ <b>NİYƏ VAHİD?</b> — əvvəl «Dövr Gəliri / Xərci» kartları
        /// COGS + ofis xərci düsturunu ✗, qrafik isə BÜTÜN xərcləri işlədirdi ✗
        /// → kartda <c>0,00 ₼</c>, qrafikdə <c>700 000 ₼</c> sütun ✓✓✓
        /// (istifadəçinin «dövr gəliri/xərci işləmir» şikayətinin səbəbi ✗)
        /// </para>
        /// <para>
        /// ✅ <b>GƏLİR (kassaya daxil olan):</b> nağd/köçürmə satış ✓ ·
        /// kredit daxilolmaları (ödəniş · erkən bağlama · ödənilmiş gecikmə) ✓ ·
        /// ilkin ödənişlər (avans) ✓<br/>
        /// ✅ <b>XƏRC (kassadan çıxan):</b> BÜTÜN xərc qeydləri
        /// (avtomobil ✓ · ofis ✓) + kreditə bağlı əlavə xərclər ✓
        /// </para>
        /// <para>
        /// ℹ️ Barter ilə alınan maşının əvəz dəyəri nağd DEYİL ✗ →
        /// gəlirə yalnız nağd/köçürmə hissəsi (<c>Sale.NagdMebleg</c>) yazılır ✓✓✓
        /// </para>
        /// </summary>
        private DonemAxini DonemAxi(DateTime from, DateTime to)
        {
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var satislar = _sales.Where(s => InRange(s.SatisTarixi, from, to)).ToList();
            var xercler = _expenses.Where(e => InRange(e.Tarix, from, to)).ToList();

            // 📤 Avtomobil / ofis xərc BÖLGÜSÜ (kartlardaki izah üçün ✓)
            var avtomobilXerc = xercler.Where(e => !Catalog.IsOffice(e.Teyinat)).Sum(e => e.Mebleg);
            var ofisXerc = xercler.Where(e => Catalog.IsOffice(e.Teyinat)).Sum(e => e.Mebleg);

            // ================================================================
            //  💵 VAHİD DÜSTUR — `KassaHesabi` ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL burada İKİNCİ bir düstur var idi ✗ →
            //     • ⏳ MÖHLƏTLƏR ✗ (avansın möhlətə salınmış hissəsi də avans
            //       sayılırdı ✗, ödənilmiş möhlət isə HEÇ görünmürdü ✗)
            //     • ✍ ƏL İLƏ yazılan hərəkətlər ✗
            //     • 👥 TƏRƏFDAŞ BÖLGÜSÜ (tutulan «xeyir») ✗
            //     → yəni «Kassa» tabı ilə Maliyyə Paneli FƏRLİ rəqəm verirdi ✗✓✓
            //  ✅ İNDİ: «💵 Kassa» ✓ MALİYYƏ PANELİ ✓ VEB DASHBOARD ✓
            //     hamısı EYNİ düsturu işlədir → rəqəmlər HƏMİŞƏ üst-üstə düşür ✓✓✓
            // ================================================================
            var hesabat = KassaHesabi.Qur(
                _sales,
                _credits,
                _transactions,
                _expenses,
                KassaHesabi.KreditPaylariniSec(_partnerShares),
                KassaHesabi.SatisPaylariniSec(_partnerShares),
                _terefdasOdenisler,
                _manualHereketler,
                from,
                to);

            return new DonemAxini(
                Gelir: hesabat.Daxilolma,
                Xerc: hesabat.Xerc,
                Xalis: hesabat.Xalis,
                NagdDaxilolma: hesabat.SatisDaxilolma,
                KreditDaxilolma: hesabat.KreditDaxilolma,
                IlkinOdenis: hesabat.IlkinOdenisDaxilolma,
                AvtomobilXerci: avtomobilXerc,
                OfisXerci: ofisXerc,
                KreditXerci: hesabat.KreditXerci,
                SatisGeliri: satislar.Sum(s => s.SatisQiymeti));
        }

        /// <summary>
        /// 💰 <b>Pul KASSAYA GƏLDİ?</b> ✓✓✓ — kredit ödənişi ✓ ·
        /// vaxtından tez bağlama ✓ · ödənilmiş gecikmə cəriməsi ✓
        /// <para>
        /// ⚙️ <b>DÜSTUR ARTIQ BURADA DEYİL</b> ✗ — Maliyyə Paneli
        /// <see cref="KassaHesabi"/> işlədir ✓ («💵 Kassa» tabı ✓ · Veb ✓ ilə EYNİ) ✓✓✓
        /// </para>
        /// </summary>
        private static bool KreditDaxilolmasidir(CreditTransaction hereket)
            => hereket.Nov == "Gəlir"
               || hereket.Nov == "Vaxtından tez bağlama"
               || (hereket.Nov == "Gecikmə" && hereket.Odenilib);

        /// <summary>
        /// Bir dövr üzrə kassa axınının tam açılışı ✓
        /// (<b>kartlar · qrafik · PDF hesabatı BUNU işlədir</b> ✓✓✓)
        /// </summary>
        private readonly record struct DonemAxini(
            decimal Gelir,
            decimal Xerc,
            decimal Xalis,
            decimal NagdDaxilolma,
            decimal KreditDaxilolma,
            decimal IlkinOdenis,
            decimal AvtomobilXerci,
            decimal OfisXerci,
            decimal KreditXerci,
            decimal SatisGeliri);

        private void BuildChart(
            DateTime from,
            DateTime to)
        {
            ChartPoints.Clear();

            // Qısa dövrlərdə günlük, uzun dövrlərdə aylıq sütunlar.
            var daily = (to.Date - from.Date).TotalDays <= 31;
            var buckets = new List<(DateTime From, DateTime To, string Label)>();

            if (daily)
            {
                for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
                {
                    buckets.Add((day, day.AddDays(1).AddTicks(-1), day.ToString("dd.MM")));
                }
            }
            else
            {
                var month = new DateTime(from.Year, from.Month, 1);
                while (month <= to)
                {
                    buckets.Add((month, month.AddMonths(1).AddTicks(-1),
                        $"{MonthShort[month.Month - 1]} {month.Year % 100:D2}"));
                    month = month.AddMonths(1);
                }
            }

            if (buckets.Count == 0)
            {
                return;
            }

            // ================================================================
            //  ✅ SÜTUNLAR «DonemAxi» İLƏ — KARTLARLA EYNİ DÜSTUR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL qrafik başqa düsturla hesablanırdı ✗ → «Dövr Xərci»
            //     kartı 0,00 ₼ ✗, sütunlar isə 700 000 ₼ ✗ — bir-birini
            //     təkzib edirdi ✗✓✓
            //  ✅ İNDİ: kart = sütun = PDF hesabatı ✓✓✓ (tam sinxron ✓)
            // ================================================================
            var raw = buckets.Select(b =>
            {
                var axin = DonemAxi(b.From, b.To);

                return (b.Label, Income: axin.Gelir, Expense: axin.Xerc, Profit: axin.Xalis);
            }).ToList();

            var max = raw.Max(r => Math.Max(r.Income, Math.Max(r.Expense, r.Profit > 0 ? r.Profit : 0m)));
            var scale = max <= 0m ? 1m : max;
            _hasChartData = max > 0m;

            foreach (var point in raw)
            {
                ChartPoints.Add(new FinanceChartPoint(
                    point.Label,
                    point.Income,
                    point.Expense,
                    point.Profit,
                    BarHeight(point.Income, scale),
                    BarHeight(point.Expense, scale),
                    BarHeight(point.Profit, scale)));
            }

            OnPropertyChanged(nameof(HasChartData));
            OnPropertyChanged(nameof(HasNoChartData));
        }

        /// <summary>Sıfırdan böyük dəyərlər üçün minimum 4px hündürlük verilir ki, sütun görünsün.</summary>
        private static double BarHeight(decimal value, decimal scale)
            => value <= 0m ? 0d : Math.Max(4d, (double)(value / scale) * ChartHeight);

        private (DateTime From, DateTime To) GetRange()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            return SelectedRange switch
            {
                "Bu gün" => (today, today),
                "Bu həftə" => (StartOfWeek(today), today),
                "Son 7 gün" => (today.AddDays(-6), today),
                "Bu ay" => (monthStart, today),
                "Son 30 gün" => (today.AddDays(-29), today),
                "Keçən ay" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
                "Bu il" => (new DateTime(today.Year, 1, 1), today),
                "Son 12 ay" => (monthStart.AddMonths(-11), today),
                "Bütün vaxt" => (EarliestDate(), today),
                "Fərdi aralıq" => (CustomFrom ?? monthStart.AddMonths(-1), CustomTo ?? today),
                _ => (monthStart, today)
            };
        }

        private DateTime EarliestDate()
        {
            var dates = new List<DateTime>();
            dates.AddRange(_sales.Select(s => s.SatisTarixi));
            dates.AddRange(_expenses.Select(e => e.Tarix));
            dates.AddRange(_transactions.Select(t => t.Tarix));
            return dates.Count == 0 ? DateTime.Today : dates.Min().Date;
        }

        private static bool InRange(DateTime value, DateTime from, DateTime to)
            => value.Date >= from.Date && value.Date <= to.Date;

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = ((int)date.DayOfWeek + 6) % 7; // Bazar ertəsi = 0
            return date.Date.AddDays(-diff);
        }
    }
}
