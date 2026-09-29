using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class SaleService : ISaleService
    {
        private readonly ISaleRepository _sales;
        private readonly ICarRepository _cars;
        private readonly ICarService _carService;
        private readonly IPartnerShareRepository _shares;
        private readonly ITrashService _trash;
        private readonly ILogger<SaleService> _logger;

        public SaleService(
            ISaleRepository sales,
            ICarRepository cars,
            ICarService carService,
            IPartnerShareRepository shares,
            ITrashService trash,
            ILogger<SaleService> logger)
        {
            _sales = sales;
            _cars = cars;
            _carService = carService;
            _shares = shares;
            _trash = trash;
            _logger = logger;
        }

        /// <summary>
        /// SATIŞA bağlı tərəfdaş paylarını qaytarır.
        /// <para>Maşın satılanda mənfəəti
        /// (<c>Satış qiyməti − Maya dəyəri</c>) tərəfdaşlara bölünür.</para>
        /// </summary>
        public Task<IReadOnlyList<PartnerShare>> GetSaleSharesAsync(
            int saleId,
            CancellationToken cancellationToken = default)
            => _shares.FindAsync(p => p.SaleId == saleId, cancellationToken);

        /// <summary>
        /// Satışın tərəfdaş paylarını yenidən yazır (əvvəlkilər silinir).
        /// </summary>
        public async Task SaveSaleSharesAsync(
            int saleId,
            bool tetbiqOlunub,
            IReadOnlyList<PartnerShare> rows,
            CancellationToken cancellationToken = default)
        {
            var existing = await _shares.FindAsync(p => p.SaleId == saleId, cancellationToken);

            // Əvvəlki paylar BİRBAŞA BAZADAN silinir — uzunömürlü kontekstdəki
            // köhnə nüsxələr «expected to affect 1 row(s), but actually 0»
            // xətası yaratmasın.
            if (existing.Count > 0)
            {
                _shares.ClearTracker();
            }

            await _shares.DeleteWhereAsync(p => p.SaleId == saleId, cancellationToken);

            if (!tetbiqOlunub || rows.Count == 0)
            {
                return;
            }

            var sira = 0;
            foreach (var row in rows.OrderBy(p => p.Sira))
            {
                await _shares.AddAsync(new PartnerShare
                {
                    SaleId = saleId,
                    Terefdas = row.Terefdas,
                    Faiz = row.Faiz,
                    Mebleg = row.Mebleg,
                    QaligPayi = row.QaligPayi,
                    Aktiv = row.Aktiv,
                    Sira = sira++
                }, cancellationToken);
            }

            await _shares.SaveChangesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<Sale>> GetSalesAsync(CancellationToken cancellationToken = default)
            => _sales.GetWithCarAsync(cancellationToken);

        public async Task<Sale> AddSaleAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            CarItem? car = null;

            if (sale.CarId.HasValue)
            {
                car = await _cars.GetWithDetailsAsync(sale.CarId.Value, cancellationToken);
                if (car is not null)
                {
                    // Maya dəyəri satış anında hesablanıb saxlanılır (snapshot).
                    sale.MayaDeyeri = car.MayaDeyeri;
                    car.Status = Catalog.SoldStatus;
                }
            }

            await _sales.AddAsync(sale, cancellationToken);
            await _sales.SaveChangesAsync(cancellationToken);

            if (car is not null)
            {
                _cars.Update(car);
                await _cars.SaveChangesAsync(cancellationToken);
            }

            // ---- BARTER: alıcının verdiyi maşın AVTO PARKA daxil edilir ----
            await AddReceivedBarterCarAsync(sale, car, cancellationToken);

            return sale;
        }

        /// <summary>
        /// Barter satışında alıcının verdiyi avtomobili avto parka əlavə edir.
        /// <para>
        /// <b>MAYA DƏYƏRİ = BARTER DƏYƏRİ</b> (<see cref="Sale.BarterMebleg"/>):
        /// həmin maşın üçün faktiki olaraq bu qədər "ödənilib" — çünki satılan
        /// avtomobilin qiymətindən bu hissə maşınla qarşılanıb.
        /// </para>
        /// <para>
        /// Beləliklə uçot tam uyğun gəlir:
        /// <code>
        /// Satılan maşın : gəlir = SatisQiymeti, maya = köhnə maya  → mənfəət düzgün
        /// Alınan maşın  : maya  = BarterMebleg                    → parka düzgün düşür
        /// </code>
        /// Əlavə olaraq «Alış» xərci avtomatik yaradılır ki, maşına sonradan
        /// edilən xərclərlə birlikdə ümumi maya dəyəri dəqiq hesablansın.
        /// </para>
        /// </summary>
        private async Task AddReceivedBarterCarAsync(
            Sale sale,
            CarItem? soldCar,
            CancellationToken cancellationToken)
        {
            if (!sale.IsBarter || sale.BarterMebleg <= 0m)
            {
                return;
            }

            var received = sale.ReceivedCar;

            // Maşının markası yazılmayıbsa parka əlavə edilmir (yalnız təsvir kifayətdir).
            if (received is null || string.IsNullOrWhiteSpace(received.Marka))
            {
                return;
            }

            received.Id = 0;
            received.SiraNomresi = 0;                       // avtomatik ardıcıl nömrə
            received.AlisTarixi = sale.SatisTarixi;
            received.AlisUsulu = Catalog.BarterPurchase;
            received.AlisQiymeti = sale.BarterMebleg;       // ← MAYА = barter dəyəri
            received.Status = Catalog.StockStatus;
            received.BarterSaleId = sale.Id;
            received.KreditNomresi = string.Empty;

            if (string.IsNullOrWhiteSpace(received.BarterTesviri))
            {
                received.BarterTesviri = BuildBarterOriginNote(sale, soldCar);
            }

            await _carService.AddCarAsync(received, cancellationToken);

            sale.ReceivedCarId = received.Id;
            _logger.LogInformation(
                "Barter satışından parka maşın əlavə edildi: {Car} (maya {Maya} AZN, satış #{SaleId})",
                received.DisplayName, received.AlisQiymeti, sale.Id);
        }

        /// <summary>Barterdən gələn maşının mənşə qeydini hazırlayır.</summary>
        private static string BuildBarterOriginNote(Sale sale, CarItem? soldCar)
        {
            var contract = string.IsNullOrWhiteSpace(sale.MuqavileNomresi)
                ? $"satış #{sale.Id}"
                : $"barter satışı {sale.MuqavileNomresi}";

            var sold = soldCar?.DisplayName;

            return string.IsNullOrWhiteSpace(sold)
                ? $"🔄 {contract} nəticəsində parka gəlib"
                : $"🔄 {contract} — {sold} üçün alınıb";
        }

        public async Task DeleteSaleAsync(int id, CancellationToken cancellationToken = default)
        {
            var sale = await _sales.GetByIdAsync(id, cancellationToken);
            if (sale is null)
            {
                return;
            }

            // Silmədən əvvəl bərpa surəti.
            await _trash.BackupSaleAsync(id, cancellationToken);

            // Bu satışdan parka gələn barter maşını varsa, bağlantı kəsilir
            // (maşın parkda qalır — onun maya dəyəri və xərcləri artıq uçota alınıb).
            var received = await _cars.FindAsync(c => c.BarterSaleId == id, cancellationToken);
            foreach (var car in received)
            {
                var tracked = await _cars.GetByIdAsync(car.Id, cancellationToken);
                if (tracked is not null)
                {
                    tracked.BarterSaleId = null;
                }
            }

            // Avtomobil repo-su öz kontekstində saxlayır (AppDbContext transient-dir).
            await _cars.SaveChangesAsync(cancellationToken);

            _sales.Remove(sale);
            await _sales.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteSalesByCarAsync(int carId, CancellationToken cancellationToken = default)
        {
            // ⚠ Köhnə (stale) izlər buraxılır — əks halda artıq bazada olmayan
            //   sətirlər üçün DELETE göndərilir və «0 sətir» xətası yaranır ✗
            _sales.ClearTracker();

            // Satış qeydləri BİRBAŞA BAZADAN silinir (tracker-dən asılı deyil ✓)
            var silinen = await _sales.DeleteWhereAsync(s => s.CarId == carId, cancellationToken);

            if (silinen > 0)
            {
                _logger.LogInformation(
                    "🗑️ Avtomobilin {Count} satış qeydi silindi (geri qaytarma): CarId={CarId}",
                    silinen, carId);
            }
        }

        public SaleStats BuildStats(IEnumerable<Sale> sales)
        {
            var list = sales.ToList();
            var revenue = list.Sum(s => s.SatisQiymeti);
            var profit = list.Sum(s => s.Menfeet);
            var monthRevenue = list
                .Where(s => s.SatisTarixi.Year == DateTime.Today.Year && s.SatisTarixi.Month == DateTime.Today.Month)
                .Sum(s => s.SatisQiymeti);

            // Ödəniş bölgüsü: barter hissəsi maşınla, qalanı nağd / köçürmə ilə.
            var barterList = list.Where(s => s.IsBarter).ToList();
            var barterTotal = barterList.Sum(s => s.BarterMebleg);
            var nagdTotal = list.Sum(s => s.NagdMebleg);

            return new SaleStats(
                list.Count,
                revenue,
                profit,
                monthRevenue,
                barterList.Count,
                barterTotal,
                nagdTotal);
        }
    }
}
