using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace EnterpriseAeroStudio.Collections
{
    /// <summary>
    /// 🚀 <b>ON MINLƏRLƏ SƏTİRİ TƏK BİLDİRİŞLƏ YENİLƏYƏN KOLLEKSİYA</b> ✓✓✓
    /// <para>
    /// <b>NƏ ÜÇÜN LAZIMDIR?</b>
    /// <c>ObservableCollection</c>-a 10 000 sətri <c>foreach (… Add(…))</c> ilə
    /// yazsaq ✗ → WPF 10 000 <c>CollectionChanged</c> hadisəsi alır ✗ →
    /// cədvəl 10 000 dəfə yenidən qurulur ✗ → <b>proqram DONUR</b> ✗✓✓
    /// </para>
    /// <para>
    /// ✅ İNDİ: <see cref="ReplaceAll"/> → tək <c>Reset</c> ✓ (böyük yükləmələr ✓),
    /// <see cref="AddRange"/> → WPF-də <b>TƏHLÜKƏSİZ</b> tək-tək <c>Add</c> ✓✓✓
    /// → cədvəl DONMUR ✗ (virtualizasiya sayəsində 200 bildiriş də anidir ✓)
    /// </para>
    /// <para>
    /// ⚠⚠ <b>ÇOX-ELEMENTLİ <c>Add</c> (range) bildirişi QADAĞANDIR ✗</b> —
    /// WPF onu dəstəkləmir ✗ → «<i>An ItemsControl is inconsistent with its items
    /// source</i>» xətası verir ✗✓✓ (ətraflı: <see cref="AddRange"/> ✓)
    /// </para>
    /// <para>
    /// ⚠ <see cref="AddRange"/> cədvəlin scroll vəziyyətini POZMUIR ✗✓✓ —
    /// «⬇️ Daha çox yüklə» basdıqda istifadəçi yuxarıya atmır ✓
    /// </para>
    /// </summary>
    /// <typeparam name="T">Sətir tipi (məs. <c>ExpenseItem</c>).</typeparam>
    public sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public BulkObservableCollection()
        {
        }

        public BulkObservableCollection(IEnumerable<T> items) : base(items)
        {
        }

        /// <summary>
        /// Siyahını TAMAMİLƏ dəyişir ✓ — <b>tək</b> <c>Reset</c> bildirişi ilə ✓
        /// (köhnə <c>Clear()</c> + N dənə <c>Add()</c> yerinə ✓✓✓)
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            var siyahı = items as IList<T> ?? items.ToList();

            Items.Clear();

            foreach (var item in siyahı)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Siyahının SONUNA dəstə ilə əlavə edir ✓ — <b>WPF üçün TƏHLÜKƏSİZ üsulla</b> ✓✓✓
        /// <para>
        /// ⚠⚠ <b>NƏ ÜÇÜN BİR-BİR ƏLAVƏ EDİLİR? (XƏTANIN KÖKÜ ✓✓✓)</b>
        /// WPF-in <c>ItemCollection</c> / <c>ListCollectionView</c> mexanizmi
        /// <b>ÇOX-ELEMENTLİ <c>Add</c> (range) bildirişini DƏSTƏKLƏMİR</b> ✗ →
        /// <c>«Added item does not appear at given index»</c> xətası, ardınca isə
        /// <c>«An ItemsControl is inconsistent with its items source»</c> xətası
        /// verirdi ✗✓✓ (= istifadəçinin şikayət etdiyi ÇÖKMƏ ✗)
        /// </para>
        /// <para>
        /// ✅ <c>Add()</c> isə <b>TƏK-ELEMENTLİ</b> standart bildiriş göndərir ✓ —
        /// WPF onu 100% dəstəkləyir ✓. Cədvəl ARTIQ VİRTUALİZASİYA ilə işlədiyi üçün
        /// 200 bildiriş də ani (millisaniyə) olur ✓✓✓ — donma YOXDUR ✗
        /// </para>
        /// <para>
        /// 🚀 <b>10 000+ sətirlik böyük dəstələr üçün</b> <see cref="ReplaceAll"/>
        /// istifadə edin ✓ (tək <c>Reset</c> ✓✓✓)
        /// </para>
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            var siyahı = items as IList<T> ?? items.ToList();

            if (siyahı.Count == 0)
            {
                return;
            }

            // ⚠ `Add()` — TƏK-ELEMENTLİ bildiriş ✓ (WPF-də 100% dəstəklənir ✓✓✓)
            foreach (var item in siyahı)
            {
                Add(item);
            }
        }
    }
}
