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
    /// ✅ İNDİ: <see cref="ReplaceAll"/> → tək <c>Reset</c> ✓,
    /// <see cref="AddRange"/> → tək <c>Add</c> (çox-elementli ✓) ✓
    /// → cədvəl YALNIZ BİR DƏFƏ yenilənir ✓ (1 000 000 sətirdə də sürətli ✓✓✓)
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
        /// Siyahının SONUNA dəstə ilə əlavə edir ✓ — <b>tək</b> bildirişlə ✓
        /// (10 000 dənə <c>Add()</c> yerinə 1 hadisə ✓✓✓)
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            var siyahı = items as IList<T> ?? items.ToList();

            if (siyahı.Count == 0)
            {
                return;
            }

            var başlanğıc = Items.Count;

            foreach (var item in siyahı)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add, siyahı, başlanğıc));
        }
    }
}
