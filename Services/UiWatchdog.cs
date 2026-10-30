using System;
using System.Threading;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🩺 <b>UI DONMA NƏZARƏTÇİSİ</b> ✓✓✓ (v6.2.81)
    /// <para>
    /// «Proqram donur» şikayətində <b>hansı fazada</b> və <b>nə qədər</b>
    /// donduğunu BİLMƏK üçün real ölçmə ✓✓✓
    /// </para>
    /// <para>
    /// <b>NECƏ İŞLƏYİR:</b> UI axını (WPF tərəfində 250 ms-lik <c>DispatcherTimer</c>)
    /// hər dövrədə <see cref="Nəbz"/> çağırır ✓. Fon <see cref="Timer"/> hər saniyə
    /// nəbzi yoxlayır ✗ — 2,5 saniyədən çox gecikmə varsa UI axını BLOKLANIB ✗ →
    /// loga fazanın adı yazılır ✓, bərpa olanda müddət də yazılır ✓
    /// </para>
    /// <para>
    /// ⚠ Bu sinif <b>WPF-dən asılı deyil</b> ✗ — Veb layihəsi də kompilyasiya edir ✓
    /// (nəbzi WPF tərəfi ötürür ✓)
    /// </para>
    /// </summary>
    public static class UiWatchdog
    {
        private static readonly TimeSpan Hədd = TimeSpan.FromMilliseconds(2500);

        private static Timer? _nəzarət;
        private static DateTime _sonNəbz = DateTime.UtcNow;
        private static DateTime? _donmaBaşladı;
        private static bool _xəbərdarlıqYazıldı;

        /// <summary>🩺 Fon nəzarətçisini işə salır ✓ (yalnız BİR dəfə ✓)</summary>
        public static void Başlat()
        {
            if (_nəzarət is not null)
            {
                return;
            }

            try
            {
                _sonNəbz = DateTime.UtcNow;
                _nəzarət = new Timer(_ => Yoxla(), null, 1000, 1000);
            }
            catch
            {
                // 🛡️ nəzarətçi qurulmasa → proqram normal işləyir ✓
            }
        }

        /// <summary>❤️ <b>UI NƏBZİ</b> ✓ — UI axınından çağırılır (250 ms) ✓</summary>
        public static void Nəbz()
        {
            _sonNəbz = DateTime.UtcNow;

            if (_donmaBaşladı is null)
            {
                return;
            }

            try
            {
                var müddət = (DateTime.UtcNow - _donmaBaşladı.Value).TotalSeconds;

                AppLogger.Melumat($"🩺 UI BƏRPA OLUNDU ✓ — donma müddəti {müddət:0.0} saniyə ✓");
            }
            catch { }

            _donmaBaşladı = null;
            _xəbərdarlıqYazıldı = false;
        }

        /// <summary>🩺 Nəzarəti dayandırır ✓</summary>
        public static void Dayandır()
        {
            try { _nəzarət?.Dispose(); } catch { }

            _nəzarət = null;
        }

        private static void Yoxla()
        {
            try
            {
                var gecikmə = DateTime.UtcNow - _sonNəbz;

                if (gecikmə <= Hədd)
                {
                    return;
                }

                _donmaBaşladı ??= _sonNəbz;

                // ⚠ Yalnız BİR dəfə yazılır ✓ (log şişməsin ✗)
                if (_xəbərdarlıqYazıldı)
                {
                    return;
                }

                _xəbərdarlıqYazıldı = true;

                AppLogger.Melumat(
                    $"🩺 UI DONDU ✗ — {gecikmə.TotalSeconds:0.0} saniyə · faz: «{PerfTracker.Faza}» ✓ (diaqnostika)");
            }
            catch
            {
                // 🛡️ səssizcə keçir ✓
            }
        }
    }
}
