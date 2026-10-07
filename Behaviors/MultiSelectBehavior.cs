using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace EnterpriseAeroStudio.Behaviors
{
    /// <summary>
    /// 🖱️ <b>ÇOXLU SEÇİM KÖRPÜSÜ</b> ✓✓✓ (v6.2.29)
    /// <para>
    /// WPF <see cref="DataGrid"/>-in <c>SelectedItems</c> kolleksiyası birbaşa
    /// <b>bind olunmur</b> ✗. Bu əlavə xassə onu ViewModel-dəki siyahıya
    /// <b>sinxronlaşdırır</b> ✓ → istifadəçi <b>Ctrl / Shift + sol klik</b> ilə
    /// bir neçə sətir seçə bilir ✓ və VM onları görür ✓✓✓
    /// </para>
    /// <example>
    /// <code>
    /// &lt;DataGrid SelectionMode="Extended"
    ///           bhv:MultiSelectBehavior.SelectedItems="{Binding Vm.Secilmisler}"&gt;
    /// </code>
    /// </example>
    /// </summary>
    public static class MultiSelectBehavior
    {
        /// <summary>DataGrid-in seçilmiş sətirlərini VM siyahısına bağlayan əlavə xassə ✓.</summary>
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(MultiSelectBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static IList? GetSelectedItems(DependencyObject element)
            => (IList?)element.GetValue(SelectedItemsProperty);

        public static void SetSelectedItems(DependencyObject element, IList? value)
            => element.SetValue(SelectedItemsProperty, value);

        private static void OnSelectedItemsChanged(
            DependencyObject element,
            DependencyPropertyChangedEventArgs e)
        {
            if (element is not DataGrid grid)
            {
                return;
            }

            grid.SelectionChanged -= GridSelectionChanged;

            if (e.OldValue is IList köhnə)
            {
                köhnə.Clear();
            }

            if (e.NewValue is IList)
            {
                grid.SelectionChanged += GridSelectionChanged;
            }

            Sync(grid, e.NewValue as IList);
        }

        private static void GridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                Sync(grid, GetSelectedItems(grid));
            }
        }

        /// <summary>DataGrid seçimini VM siyahısına köçürür ✓.</summary>
        private static void Sync(DataGrid grid, IList? hedef)
        {
            if (hedef is null)
            {
                return;
            }

            hedef.Clear();

            foreach (var element in grid.SelectedItems)
            {
                hedef.Add(element);
            }
        }
    }
}
