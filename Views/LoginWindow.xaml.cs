using System.Windows;
using System.Windows.Input;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// 🔐 <b>GİRİŞ PƏNCƏRƏSİ</b> ✓✓✓
    /// <para>
    /// Proqram açılanda İLK bu pəncərə gəlir ✓ — uğurlu girişdən sonra
    /// əsas pəncərə açılır ✓ və istifadəçinin ROLU tətbiq olunur ✓:
    /// 🌐 Veb Sayt (yalnız Admin ✗) · ⚙️ Tənzimləmələr (Asif + Admin ✓)
    /// </para>
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>✅ Uğurlu giriş edən istifadəçi ✓ (ləğv edilərsə <c>null</c> ✗)</summary>
        public Istifadeci? Istifadeci { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            Loaded += (_, _) => IstifadeciAdi.Focus();
        }

        private void Giris_Click(object sender, RoutedEventArgs e) => GirisEt();

        private void Sifre_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                GirisEt();
            }
        }

        /// <summary>🔓 Girişi yoxlayır ✓✓✓</summary>
        private void GirisEt()
        {
            var sifre = SifreQutusu.Visibility == Visibility.Visible
                ? SifreQutusu.Password
                : SifreAciq.Text;

            var istifadeci = AuthService.GirisEle(IstifadeciAdi.Text, sifre);

            if (istifadeci is null)
            {
                XetaMetni.Text = "❌ İstifadəçi adı və ya şifrə YANLIŞDIR!";
                SifreQutusu.Clear();
                SifreAciq.Clear();
                SifreQutusu.Focus();
                return;
            }

            Istifadeci = istifadeci;
            DialogResult = true;
        }

        private void Cixis_Click(object sender, RoutedEventArgs e)
        {
            AuthService.CixisEt();
            DialogResult = false;
        }

        /// <summary>👁 Şifrəni göstər / gizlət ✓</summary>
        private void SifreGoster_Changed(object sender, RoutedEventArgs e)
        {
            if (SifreAciq is null || SifreQutusu is null)
            {
                return;
            }

            var goster = SifreGoster.IsChecked == true;

            if (goster)
            {
                SifreAciq.Text = SifreQutusu.Password;
                SifreQutusu.Visibility = Visibility.Collapsed;
                SifreAciq.Visibility = Visibility.Visible;
                SifreAciq.Focus();
                SifreAciq.CaretIndex = SifreAciq.Text.Length;
            }
            else
            {
                SifreQutusu.Password = SifreAciq.Text;
                SifreAciq.Visibility = Visibility.Collapsed;
                SifreQutusu.Visibility = Visibility.Visible;
                SifreQutusu.Focus();
            }
        }
    }
}
