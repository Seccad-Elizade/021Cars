using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class KassaService : IKassaService
    {
        private readonly IKassaHereketRepository _hereketler;
        private readonly ISaleService _sales;
        private readonly ICreditService _credits;
        private readonly IExpenseService _expenses;
        private readonly IPartnerShareRepository _paylar;
        private readonly IPartnerPaymentRepository _terefdasOdenisleri;
        private readonly ILogger<KassaService> _logger;

        public KassaService(
            IKassaHereketRepository hereketler,
            ISaleService sales,
            ICreditService credits,
            IExpenseService expenses,
            IPartnerShareRepository paylar,
            IPartnerPaymentRepository terefdasOdenisleri,
            ILogger<KassaService> logger)
        {
            _hereketler = hereketler;
            _sales = sales;
            _credits = credits;
            _expenses = expenses;
            _paylar = paylar;
            _terefdasOdenisleri = terefdasOdenisleri;
            _logger = logger;
        }

        public Task<IReadOnlyList<KassaHereket>> GetHereketlerAsync(CancellationToken cancellationToken = default)
            => _hereketler.GetAllOrderedAsync(cancellationToken);

        public async Task<KassaHereket> AddHereketAsync(
            KassaHereket hereket,
            CancellationToken cancellationToken = default)
        {
            hereket.Tarix = hereket.Tarix.Date;
            hereket.Kateqoriya = hereket.Kateqoriya?.Trim() ?? string.Empty;
            hereket.Qeyd = hereket.Qeyd?.Trim() ?? string.Empty;

            await _hereketler.AddAsync(hereket, cancellationToken);
            await _hereketler.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "💵 Kassa hərəkəti əlavə edildi: {Tarix} · {Nov} · {Kateqoriya} · {Mebleg:N2} ₼",
                hereket.Tarix.ToString("dd.MM.yyyy"), hereket.Nov, hereket.Kateqoriya, hereket.Mebleg);

            return hereket;
        }

        public async Task UpdateHereketAsync(KassaHereket hereket, CancellationToken cancellationToken = default)
        {
            _hereketler.Update(hereket);
            await _hereketler.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteHereketAsync(int id, CancellationToken cancellationToken = default)
        {
            _hereketler.ClearTracker();
            await _hereketler.DeleteWhereAsync(k => k.Id == id, cancellationToken);
        }

        public async Task<KassaHesabati> BuildAllTimeAsync(CancellationToken cancellationToken = default)
            => await BuildAsync(DateTime.MinValue.Date, DateTime.MaxValue.Date, cancellationToken);

        /// <inheritdoc />
        public async Task<KassaHesabati> BuildAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            // ================================================================
            //  ⚡ BÜTÜN MƏNBƏLƏR (Maliyyə Paneli ilə EYNİ oxumalar ✓✓✓)
            // ================================================================
            var satislar = await _sales.GetSalesAsync(cancellationToken);
            var kreditler = await _credits.GetCreditsAsync(cancellationToken);
            var emeliyyatlar = await _credits.GetTransactionsAsync(cancellationToken);
            var xercler = await _expenses.GetExpensesAsync(cancellationToken);
            var butunPaylar = await _paylar.GetAllAsync(cancellationToken);
            var elIle = await _hereketler.GetAllOrderedAsync(cancellationToken);

            // 👥 «PUL VER» əməliyyatları — YALNIZ bunlar kassadan çıxır ✓✓✓
            //    (hesablanmış paylar kassada qalır ✓ — bax: KassaHesabi)
            var terefdasOdenisleri = await _terefdasOdenisleri.GetOrderedAsync(cancellationToken);

            // 👥 «XEYİR» payları: kredit bölgüsü ✓ satış bölgüsü ✓
            //    (əməliyyat payları ✗ — onlar aylıq taksitin bölgüsüdür ✓)
            //    ⚙️ SEÇİM MƏNTİQİ TƏKRARLANMIR ✗ — paylaşılan köməkçilər ✓✓✓
            var kreditPaylari = KassaHesabi.KreditPaylariniSec(butunPaylar);
            var satisPaylari = KassaHesabi.SatisPaylariniSec(butunPaylar);

            var hesabat = KassaHesabi.Qur(
                satislar,
                kreditler,
                emeliyyatlar,
                xercler,
                kreditPaylari,
                satisPaylari,
                terefdasOdenisleri,
                elIle,
                from,
                to);

            _logger.LogInformation(
                "💵 Kassa hesabatı: {From:dd.MM.yyyy} → {To:dd.MM.yyyy} · daxil {Daxil:N2} ₼ · xərc {Xerc:N2} ₼ · qalıq {Qaliq:N2} ₼ ({Say} sətir)",
                from, to, hesabat.Daxilolma, hesabat.Xerc, hesabat.Qaliq, hesabat.SetirSayi);

            return hesabat;
        }
    }
}
